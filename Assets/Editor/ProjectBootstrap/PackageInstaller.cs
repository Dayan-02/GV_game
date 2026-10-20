using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectBootstrap
{
    /// <summary>Enables only the built-in engine modules used by Phases 1-3.</summary>
    public static class PackageInstaller
    {
        private static AddAndRemoveRequest request;
        private static double deadline;
        public static void Install()
        {
            request = Client.AddAndRemove(new[]
            {
                "com.unity.modules.animation@1.0.0", "com.unity.modules.audio@1.0.0",
                "com.unity.modules.physics@1.0.0",
                "com.unity.modules.particlesystem@1.0.0"
            });
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Poll;
        }

        // Invoke headlessly without -quit; Package Manager must finish asynchronously.
        public static void InstallHUD()
        {
            request = Client.AddAndRemove(new[] { "com.unity.ugui@2.5.0" });
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Poll;
        }
        public static void InstallNavigation()
        {
            request = Client.AddAndRemove(new[] { "com.unity.ai.navigation@2.0.15" });
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Poll;
        }
        private static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            bool success = request.IsCompleted && request.Status == StatusCode.Success;
            if (success) Debug.Log("Requested project packages resolved successfully.");
            else Debug.LogError(request.IsCompleted ? request.Error.message : "Package resolution timed out.");
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
