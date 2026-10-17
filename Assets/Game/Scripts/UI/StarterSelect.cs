using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The choice of the first monster, after the intro of a brand new save. The three starters are painted on the table of
// selection.jpeg; a bouncing arrow shows which one is chosen, with its name, type and level below.
// Left and right choose, Space or Enter confirms. You start with only that monster, at level 12.
// The TitleMenu starts it; one per scene, add it to the Game object.
public class StarterSelect : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;
    const float InputLockTime = 0.5f;   // like the dialogue: Space is ignored this long after the screen appears
    public const int StarterLevel = 12;

    [SerializeField] Texture2D background;   // graphics/menu/selection.jpeg
    [SerializeField] Font regularFont;       // PixeloidSans

    // The three starters, from left to right as they stand on the table, and where each one stands on the 1280 x 720 screen
    // (the middle of the monster; the arrow floats above its head)
    static readonly string[] Starters = { "Plumette", "Finsta", "Sparchu" };
    static readonly Vector2[] Positions = { new Vector2(356, 322), new Vector2(659, 339), new Vector2(969, 326) };
    const float ArrowHeightAboveCenter = 118f;

    static readonly Color Fill = UiKit.Hex("#fefefc"), Shadow = UiKit.Hex("#b3c9d9");
    static readonly Dictionary<string, Color> ElementColors = new Dictionary<string, Color>
    {
        { "fire", UiKit.Hex("#f8a060") }, { "water", UiKit.Hex("#50b0d8") }, { "plant", UiKit.Hex("#64a990") },
    };

    GameObject canvasObject;
    RectTransform content, info;
    RectTransform arrow;
    ScreenTint tint;
    SaveData data;
    Action finished;

    int index = 1;      // the one in the middle starts selected
    bool active;
    bool leaving;
    float lockTimer;
    float bobTime;

    void Start()
    {
        BuildCanvas();
    }

    void BuildCanvas()
    {
        canvasObject = new GameObject("Starter Select");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // over the title screen (800), under the screen fade (1000)
        canvas.pixelPerfect = true;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // the picture covers the whole screen whatever its shape: it is cropped, never stretched
        if (background != null)
        {
            var picture = new GameObject("Background", typeof(RectTransform)).AddComponent<RawImage>();
            picture.transform.SetParent(canvasObject.transform, false);
            picture.texture = background;
            picture.raycastTarget = false;
            var pictureRect = picture.rectTransform;
            pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0.5f, 0.5f);
            pictureRect.sizeDelta = new Vector2(WindowWidth, WindowHeight);
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.width / (float)background.height;
        }
        else
        {
            Debug.LogError("The Starter Select has no background picture: right-click its header and choose Reset.");
        }

        // a 1280x720 panel in the middle of the screen: everything below uses pygame coordinates inside it
        var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        panel.anchoredPosition = Vector2.zero;

        content = Stretched(panel, "Content");
        info = Stretched(content, "Info");

        // the question, in the style of the dialogue bubbles
        var font = new UiFont(regularFont, 30, 38);
        const string question = "Choose your partner";
        float width = UiKit.Measure(font, question) + 28;
        UiKit.Box(content, (WindowWidth - width) / 2f, 28, width, font.surfaceHeight + 28, Color.white, 4);
        UiKit.Label(content, question, font, Color.black, Anchor.MidTop, WindowWidth / 2f, 28 + 14);

        // arrows at the sides: left and right choose
        PixelArrow(content, 56, WindowHeight / 2f, pointingRight: false);
        PixelArrow(content, WindowWidth - 56 - 24, WindowHeight / 2f, pointingRight: true);

        // the arrow over the chosen starter
        arrow = UiKit.NewRect(content, "Selector", 0, 0, 0, 0);
        PixelArrowDown(arrow);

        canvasObject.SetActive(false);
    }

    static RectTransform Stretched(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    // Starts the choice; whenFinished runs after the starter has been put in the save, behind a black screen
    public void Play(SaveData saveData, Action whenFinished)
    {
        data = saveData;
        finished = whenFinished;
        tint = FindFirstObjectByType<ScreenTint>();
        active = true;
        leaving = false;
        lockTimer = InputLockTime;
        index = 1;

        canvasObject.SetActive(true);
        ShowInfo();
    }

    void Update()
    {
        if (!active) return;

        // the arrow bobs up and down over the chosen monster
        bobTime += Time.deltaTime;
        var spot = Positions[index];
        float bob = Mathf.Round(Mathf.Abs(Mathf.Sin(bobTime * 4f)) * 10f);
        arrow.anchoredPosition = new Vector2(Mathf.Floor(spot.x - 15), -Mathf.Floor(spot.y - ArrowHeightAboveCenter - bob));

        var keyboard = Keyboard.current;
        if (leaving || keyboard == null) return;

        if (lockTimer > 0f) lockTimer -= Time.deltaTime;

        if (keyboard.leftArrowKey.wasPressedThisFrame) { index = (index - 1 + Starters.Length) % Starters.Length; ShowInfo(); }
        if (keyboard.rightArrowKey.wasPressedThisFrame) { index = (index + 1) % Starters.Length; ShowInfo(); }

        if (lockTimer <= 0f && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) Confirm();
    }

    // The name in a bubble, with the type and the level of the chosen starter underneath
    void ShowInfo()
    {
        for (int i = info.childCount - 1; i >= 0; i--) Destroy(info.GetChild(i).gameObject);

        string name = Starters[index];
        var font = new UiFont(regularFont, 30, 38);

        float nameWidth = UiKit.Measure(font, name) + 28;
        UiKit.Box(info, (WindowWidth - nameWidth) / 2f, 548, nameWidth, font.surfaceHeight + 28, Color.white, 4);
        UiKit.Label(info, name, font, Color.black, Anchor.MidTop, WindowWidth / 2f, 548 + 14);

        // two tags side by side: the type, in its colour, and the level
        string element = MonsterDatabase.Monsters[name].element;
        string typeText = char.ToUpper(element[0]) + element.Substring(1);
        string levelText = $"Lvl {StarterLevel}";
        float typeWidth = UiKit.Measure(font, typeText) + 24, levelWidth = UiKit.Measure(font, levelText) + 24;
        const float gap = 16, top = 628, height = 54;
        float left = (WindowWidth - (typeWidth + gap + levelWidth)) / 2f;

        UiKit.Box(info, left, top, typeWidth, height, ElementColors[element], 4);
        UiKit.Label(info, typeText, font, Color.black, Anchor.Center, left + typeWidth / 2f, top + height / 2f);
        UiKit.Box(info, left + typeWidth + gap, top, levelWidth, height, Color.white, 4);
        UiKit.Label(info, levelText, font, Color.black, Anchor.Center, left + typeWidth + gap + levelWidth / 2f, top + height / 2f);
    }

    // The starter is chosen: fade to black, put it in the save as the only monster, and go on
    void Confirm()
    {
        leaving = true;
        string name = Starters[index];

        tint.Begin(() =>
        {
            var starter = new Monster(name, StarterLevel);
            data.party = new List<MonsterSave>
            {
                new MonsterSave { name = starter.name, level = starter.level, xp = 0, health = starter.health, energy = starter.energy },
            };

            active = false;
            canvasObject.SetActive(false);
            finished?.Invoke();
        });
    }

    // A pixel arrow pointing left or right, made of 6 px steps, with the pale-blue shadow of the menu buttons
    static void PixelArrow(Transform parent, float x, float centerY, bool pointingRight)
    {
        const float step = 6;
        float[] widths = { 6, 12, 18, 24, 18, 12, 6 };
        float top = centerY - widths.Length * step / 2f;

        for (int pass = 0; pass < 2; pass++)
        {
            float offset = pass == 0 ? 5 : 0;
            Color color = pass == 0 ? Shadow : Fill;
            for (int row = 0; row < widths.Length; row++)
            {
                float rowX = pointingRight ? x : x + 24 - widths[row];
                UiKit.Box(parent, rowX + offset, top + row * step + offset, widths[row], step, color);
            }
        }
    }

    // The same, pointing down; its tip is the middle of the bottom edge and the top left corner is the point of the container
    static void PixelArrowDown(RectTransform container)
    {
        const float step = 6;
        float[] widths = { 30, 24, 18, 12, 6 };

        for (int pass = 0; pass < 2; pass++)
        {
            float offset = pass == 0 ? 5 : 0;
            Color color = pass == 0 ? Shadow : Fill;
            for (int row = 0; row < widths.Length; row++)
                UiKit.Box(container, (30 - widths[row]) / 2f + offset, row * step + offset, widths[row], step, color);
        }
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the picture and the font
    void Reset()
    {
        background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/graphics/menu/selection.jpeg");
        regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/PixeloidSans.ttf");
    }
#endif
}
