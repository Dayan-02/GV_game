using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Creates the Phase 4 Canvas layout with native Unity UI components.</summary>
public static class Phase4UIFactory
{
    private static readonly Color Panel = new Color(0.035f, 0.055f, 0.075f, 0.93f);
    private static readonly Color Ink = new Color(0.95f, 0.97f, 0.99f);
    private static readonly Color Muted = new Color(0.65f, 0.72f, 0.78f);
    private static readonly Color Accent = new Color(0.28f, 0.88f, 0.72f);
    private static TMP_FontAsset font;
    private static Sprite solid;
    public const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    public static bool TextResourcesReady => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) != null;

    public static void PrepareResources()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new InvalidOperationException("Import TMP Essentials before creating the layout.");
        Phase2AnimationAssets.Folder("Assets/UI/HUD");
        const string spritePath = "Assets/UI/HUD/SolidUI.asset";
        solid = AssetDatabase.LoadAllAssetsAtPath(spritePath).OfType<Sprite>().FirstOrDefault();
        if (solid != null) return;
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "SolidUI", filterMode = FilterMode.Point };
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        AssetDatabase.CreateAsset(texture, spritePath);
        solid = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        solid.name = "Solid";
        AssetDatabase.AddObjectToAsset(solid, texture);
        AssetDatabase.SaveAssets();
    }

    public static GameObject Create()
    {
        PrepareResources();
        GameObject root = new GameObject("GameUI");
        GameUIController screens = root.AddComponent<GameUIController>();
        RectTransform hud = MakeCanvas(root.transform, "HUDCanvas", 10);
        CanvasGroup group = hud.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        PlayerHudPresenter presenter = hud.gameObject.AddComponent<PlayerHudPresenter>();
        Ref(presenter, "screens", screens);
        Ref(presenter, "hudGroup", group);

        RectTransform health = Card(hud, "Health", new Vector2(360, 136), Vector2.zero, new Vector2(48, 48));
        Label(health, "HealthLabel", "HEALTH", 18, Muted, new Vector2(22, 96), new Vector2(220, 26));
        TMP_Text healthValue = Label(health, "HealthValue", "100 / 100", 38, Ink, new Vector2(22, 43), new Vector2(306, 54));
        RectTransform track = Rect(health, "HealthTrack", new Vector2(316, 9), Vector2.zero, new Vector2(22, 22));
        Picture(track, new Color(0.15f, 0.2f, 0.24f));
        RectTransform fill = Stretch(track, "HealthFill");
        UnityEngine.UI.Image healthFill = Picture(fill, Accent);
        healthFill.type = UnityEngine.UI.Image.Type.Filled;
        healthFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
        healthFill.fillAmount = 1f;
        Ref(presenter, "healthFill", healthFill);
        Ref(presenter, "healthText", healthValue);

        RectTransform weapon = Card(hud, "Weapon", new Vector2(300, 180), new Vector2(1, 0), new Vector2(-48, 48));
        RectTransform stripe = Rect(weapon, "Accent", new Vector2(4, 32), Vector2.zero, new Vector2(0, 126));
        Picture(stripe, Accent);
        TMP_Text weaponName = Label(weapon, "WeaponName", "RIFLE", 28, Ink, new Vector2(22, 126), new Vector2(256, 42));
        TMP_Text magazine = Label(weapon, "MagazineAmmo", "30", 74, Ink, new Vector2(20, 38), new Vector2(130, 92));
        TMP_Text reserve = Label(weapon, "ReserveAmmo", "120", 32, Muted, new Vector2(168, 61), new Vector2(112, 43));
        Label(weapon, "MagazineLabel", "MAGAZINE", 16, Muted, new Vector2(24, 21), new Vector2(132, 24));
        Label(weapon, "ReserveLabel", "RESERVE", 16, Muted, new Vector2(168, 37), new Vector2(112, 24));
        RectTransform divider = Rect(weapon, "Divider", new Vector2(1, 56), Vector2.zero, new Vector2(150, 44));
        Picture(divider, new Color(0.35f, 0.43f, 0.49f, 0.6f));
        Ref(presenter, "weaponText", weaponName);
        Ref(presenter, "magazineText", magazine);
        Ref(presenter, "reserveText", reserve);
        RectTransform reloading = Card(hud, "ReloadingIndicator", new Vector2(300, 42), new Vector2(1, 0), new Vector2(-48, 242));
        Label(reloading, "Label", "RELOADING", 20, new Color(1f, 0.78f, 0.32f), new Vector2(20, 6), new Vector2(260, 30));
        Ref(presenter, "reloadingIndicator", reloading.gameObject);
        reloading.gameObject.SetActive(false);

        RectTransform equipment = Rect(hud, "EquipmentPlaceholders", new Vector2(336, 120), new Vector2(0.5f, 0), new Vector2(0, 48));
        Label(equipment, "Label", "EQUIPMENT", 17, Muted, new Vector2(0, 90), new Vector2(336, 26), TextAlignmentOptions.Center);
        RectTransform slots = Rect(equipment, "Slots", new Vector2(336, 80), Vector2.zero, Vector2.zero);
        UnityEngine.UI.HorizontalLayoutGroup layout = slots.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        for (int i = 1; i <= 3; i++)
        {
            RectTransform slot = Card(slots, "Slot" + i, new Vector2(104, 80), Vector2.zero, Vector2.zero);
            Label(slot, "Number", "0" + i, 21, Muted, new Vector2(10, 40), new Vector2(84, 30), TextAlignmentOptions.Center);
            Label(slot, "Status", "EMPTY", 16, Muted, new Vector2(10, 12), new Vector2(84, 24), TextAlignmentOptions.Center);
        }

        RectTransform wave = Card(hud, "WavePlaceholders", new Vector2(340, 104), Vector2.one, new Vector2(-48, -48));
        Label(wave, "WaveLabel", "CURRENT WAVE", 18, Muted, new Vector2(22, 59), new Vector2(245, 30));
        Label(wave, "WaveValue", "--", 28, Ink, new Vector2(278, 55), new Vector2(40, 38), TextAlignmentOptions.Right);
        Label(wave, "RemainingLabel", "ZOMBIES REMAINING", 18, Muted, new Vector2(22, 17), new Vector2(245, 30));
        Label(wave, "RemainingValue", "--", 28, Ink, new Vector2(278, 13), new Vector2(40, 38), TextAlignmentOptions.Right);

        RectTransform crosshair = Rect(hud, "Crosshair", new Vector2(44, 44), new Vector2(0.5f, 0.5f), Vector2.zero);
        CrosshairPart(crosshair, "Dot", new Vector2(4, 4), Vector2.zero);
        CrosshairPart(crosshair, "Left", new Vector2(10, 3), new Vector2(-13, 0));
        CrosshairPart(crosshair, "Right", new Vector2(10, 3), new Vector2(13, 0));
        CrosshairPart(crosshair, "Top", new Vector2(3, 10), new Vector2(0, 13));
        CrosshairPart(crosshair, "Bottom", new Vector2(3, 10), new Vector2(0, -13));
        crosshair.gameObject.SetActive(false);
        Ref(presenter, "crosshair", crosshair.gameObject);

        RectTransform modals = MakeCanvas(root.transform, "ModalCanvas", 20);
        GamePanelsPresenter panels = modals.gameObject.AddComponent<GamePanelsPresenter>();
        Ref(panels, "screens", screens);
        RectTransform pause = Modal(modals, "PausePanel", "PAUSED", "Your run is on hold.", Accent);
        RectTransform pauseContent = (RectTransform)pause.Find("Content");
        UnityEngine.UI.Button resume = Button(pauseContent, "ResumeButton", "RESUME", 134, true);
        UnityEngine.UI.Button pauseRestart = Button(pauseContent, "RestartButton", "RESTART", 54, false);
        Ref(panels, "pausePanel", pause.gameObject);
        Ref(panels, "resumeButton", resume);
        Ref(panels, "pauseRestartButton", pauseRestart);

        RectTransform gameOver = Modal(modals, "GameOverPanel", "GAME OVER", "Your run has ended.", new Color(1f, 0.38f, 0.34f));
        Ref(panels, "gameOverPanel", gameOver.gameObject);
        Ref(panels, "gameOverRestartButton", Button((RectTransform)gameOver.Find("Content"), "RestartButton", "TRY AGAIN", 72, true));
        RectTransform victory = Modal(modals, "VictoryPanel", "VICTORY", "You made it through.", Accent);
        Ref(panels, "victoryPanel", victory.gameObject);
        Ref(panels, "victoryRestartButton", Button((RectTransform)victory.Find("Content"), "RestartButton", "PLAY AGAIN", 72, true));
        pause.gameObject.SetActive(false);
        gameOver.gameObject.SetActive(false);
        victory.gameObject.SetActive(false);
        return root;
    }

    public static RectTransform MakeCanvas(Transform parent, string name, int order)
    {
        RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
        Canvas canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        UnityEngine.UI.CanvasScaler scaler = rect.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        rect.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return rect;
    }

    private static RectTransform Modal(RectTransform parent, string name, string title, string description, Color accent)
    {
        RectTransform overlay = Stretch(parent, name);
        Picture(overlay, new Color(0.015f, 0.025f, 0.04f, 0.8f), true);
        RectTransform content = Card(overlay, "Content", new Vector2(620, 460), new Vector2(0.5f, 0.5f), Vector2.zero);
        RectTransform line = Rect(content, "Accent", new Vector2(620, 4), new Vector2(0, 1), Vector2.zero);
        Picture(line, accent);
        Label(content, "Eyebrow", "DEAD CITY", 18, Muted, new Vector2(50, 382), new Vector2(520, 30), TextAlignmentOptions.Center);
        Label(content, "Title", title, 54, accent, new Vector2(50, 297), new Vector2(520, 75), TextAlignmentOptions.Center);
        Label(content, "Description", description, 23, Ink, new Vector2(50, 250), new Vector2(520, 42), TextAlignmentOptions.Center);
        return overlay;
    }

    public static UnityEngine.UI.Button Button(RectTransform parent, string name, string caption, float y, bool primary)
    {
        RectTransform rect = Rect(parent, name, new Vector2(468, 64), new Vector2(0.5f, 0), new Vector2(0, y));
        UnityEngine.UI.Image image = Picture(rect, primary ? Accent : new Color(0.14f, 0.19f, 0.23f), true);
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        UnityEngine.UI.ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.selectedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Label(rect, "Label", caption, 24, primary ? new Color(0.025f, 0.1f, 0.1f) : Ink,
            Vector2.zero, new Vector2(468, 64), TextAlignmentOptions.Center);
        return button;
    }

    private static void CrosshairPart(RectTransform parent, string name, Vector2 size, Vector2 position)
    {
        RectTransform rect = Rect(parent, name, size, new Vector2(0.5f, 0.5f), position);
        Picture(rect, Ink);
        UnityEngine.UI.Shadow shadow = rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);
    }

    public static RectTransform Card(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 position)
    {
        RectTransform rect = Rect(parent, name, size, anchor, position);
        Picture(rect, Panel);
        return rect;
    }

    public static UnityEngine.UI.Image Picture(RectTransform rect, Color color, bool raycast = false)
    {
        UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = solid;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    public static TMP_Text Label(Transform parent, string name, string value, float size, Color color,
        Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        RectTransform rect = Rect(parent, name, dimensions, Vector2.zero, position);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 position)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5; // UI
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static RectTransform Stretch(Transform parent, string name)
    {
        RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static void Ref(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        SerializedObject so = new SerializedObject(owner);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) throw new InvalidOperationException(owner.name + " has no field " + field);
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
