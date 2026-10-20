using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Native camera/Canvas capture used only by Editor verification tools.</summary>
public static class OutbreakCapture
{
    public static void Save(string name, int width = 1280, int height = 720)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return;
        Camera camera = Camera.main;
        if (camera == null)
            camera = Object.FindAnyObjectByType<Camera>();
        var previous = camera.targetTexture;
        var previousActive = RenderTexture.active;
        float aspect = camera.aspect;
        var render = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var changed = new List<System.Action>();
        try
        {
            camera.targetTexture = render;
            camera.aspect = (float)width / height;
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                var current = canvas;
                var oldCamera = canvas.worldCamera;
                float plane = canvas.planeDistance, scale = canvas.scaleFactor;
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                bool scalerEnabled = scaler != null && scaler.enabled;
                if (scaler != null)
                    scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
                canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
                changed.Add(() =>
                {
                    current.renderMode = RenderMode.ScreenSpaceOverlay;
                    current.worldCamera = oldCamera;
                    current.planeDistance = plane;
                    current.scaleFactor = scale;
                    if (scaler != null)
                        scaler.enabled = scalerEnabled;
                });
            }

            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = render;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            var pixels = image.GetPixels32();
            Directory.CreateDirectory("Logs/Captures");
            using (var writer = new BinaryWriter(File.Create("Logs/Captures/" + name + ".ppm")))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("P6\n" + width + " " + height + "\n255\n"));
                for (int y = height - 1; y >= 0; y--)
                    for (int x = 0; x < width; x++)
                    {
                        var pixel = pixels[y * width + x];
                        writer.Write(pixel.r);
                        writer.Write(pixel.g);
                        writer.Write(pixel.b);
                    }
            }
        }
        finally
        {
            foreach (var restore in changed)
                restore();
            camera.targetTexture = previous;
            camera.aspect = aspect;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            render.Release();
            Object.DestroyImmediate(render);
        }
    }
}
