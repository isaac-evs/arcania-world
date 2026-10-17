using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The title screen: a picture, the game's logo and two choices, Play and Quit. The game waits behind it.
// One per scene: add it to the Game object. Play fades to black and then MapLoader starts the world.
// Keyboard only, like the rest of the game: up and down choose, Enter or Space confirms.
public class TitleMenu : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;

    [SerializeField] Texture2D background;    // graphics/menu/menu.jpeg
    [SerializeField] Texture2D titleImage;    // graphics/menu/title.png
    [SerializeField] Font regularFont;        // PixeloidSans

    // COLORS in settings.py
    static readonly Color White = UiKit.Hex("#f4fefa"), Purple = UiKit.Hex("#2e1b53");

    static readonly string[] Options = { "Play", "Quit" };

    // True while the title screen is up: the rest of the game ignores its keys
    public static bool IsOpen { get; private set; }

    MapLoader mapLoader;
    GameObject canvasObject;
    RectTransform content, items;
    int index;
    bool leaving;

    void Awake()
    {
        IsOpen = true;
    }

    void Start()
    {
        mapLoader = FindFirstObjectByType<MapLoader>();
        BuildCanvas();
        Rebuild();
        AudioManager.PlayMusic("menu");
    }

    // The scene is reused for every Play press, so the flag must not stay on from the last one
    public static void ResetState()
    {
        IsOpen = false;
    }

    void BuildCanvas()
    {
        canvasObject = new GameObject("Title Menu");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800; // over everything of the game, under the screen fade (1000)

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
            Debug.LogError("The Title Menu has no background picture: right-click its header and choose Reset.");
            var black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            black.transform.SetParent(canvasObject.transform, false);
            black.color = Color.black;
            black.rectTransform.anchorMin = Vector2.zero;
            black.rectTransform.anchorMax = Vector2.one;
            black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero;
        }

        // a 1280x720 panel in the middle of the screen: everything below uses pygame coordinates inside it
        var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        panel.anchoredPosition = Vector2.zero;

        content = Stretched(panel, "Content");

        // the logo, at its own size so its pixels stay sharp (the image has empty space around the lettering)
        if (titleImage != null)
        {
            var logo = UiKit.SpriteFrom(titleImage, new Rect(0, 0, titleImage.width, titleImage.height));
            UiKit.Picture(content, logo, Anchor.Center, WindowWidth / 2f, 165);
        }
        else
        {
            Debug.LogError("The Title Menu has no title picture: right-click its header and choose Reset.");
        }

        items = Stretched(content, "Items");
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

    // The two buttons, identical in shape, and a pixel arrow next to the selected one
    void Rebuild()
    {
        for (int i = items.childCount - 1; i >= 0; i--) Destroy(items.GetChild(i).gameObject);

        var font = new UiFont(regularFont, 30, 38);
        const float width = 360, height = 68, top = 395, gap = 96;
        for (int i = 0; i < Options.Length; i++)
        {
            float x = (WindowWidth - width) / 2f, y = top + i * gap;
            PixelButton(x, y, width, height, Options[i], font);
            if (i == index) PixelArrow(x - 64, y + height / 2f);
        }
    }

    // White with a purple border and text, and a hard purple block behind it, offset like a shadow
    void PixelButton(float x, float y, float width, float height, string label, UiFont font)
    {
        const float border = 4, shadow = 8;
        UiKit.Box(items, x + shadow, y + shadow, width, height, Purple);              // shadow
        UiKit.Box(items, x, y, width, height, Purple);                                // border
        UiKit.Box(items, x + border, y + border, width - border * 2, height - border * 2, White);
        UiKit.Label(items, label, font, Purple, Anchor.Center, x + width / 2f, y + height / 2f);
    }

    // A right-pointing triangle made of 6 px steps: white with the same purple shadow as the buttons
    void PixelArrow(float x, float centerY)
    {
        const float step = 6;
        float[] widths = { 6, 12, 18, 24, 18, 12, 6 };
        float top = centerY - widths.Length * step / 2f;

        for (int pass = 0; pass < 2; pass++)
        {
            float offset = pass == 0 ? 5 : 0; // the shadow first, then the white shape
            Color color = pass == 0 ? Purple : White;
            for (int row = 0; row < widths.Length; row++)
                UiKit.Box(items, x + offset, top + row * step + offset, widths[row], step, color);
        }
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || leaving || !IsOpen) return;

        if (keyboard.downArrowKey.wasPressedThisFrame) { index = (index + 1) % Options.Length; Rebuild(); }
        if (keyboard.upArrowKey.wasPressedThisFrame) { index = (index - 1 + Options.Length) % Options.Length; Rebuild(); }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
        {
            if (index == 0) Play();
            else Quit();
        }
    }

    // The screen fades to black, the menu goes away behind it and the world is built; then the screen fades back in
    void Play()
    {
        leaving = true;
        var tint = FindFirstObjectByType<ScreenTint>();
        if (tint != null) tint.Begin(StartGame);
        else StartGame();
    }

    void StartGame()
    {
        IsOpen = false;
        canvasObject.SetActive(false);
        mapLoader.StartGame();
    }

    void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the pictures and the font
    void Reset()
    {
        background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/graphics/menu/menu.jpeg");
        titleImage = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/menu/title.png"); // sharp pixels, no smoothing
        regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/PixeloidSans.ttf");
    }
#endif
}
