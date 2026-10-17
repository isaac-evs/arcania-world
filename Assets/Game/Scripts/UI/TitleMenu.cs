using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The title screen: a picture, the game's logo and the menus. The game waits behind it.
//   Main:    Play, Quit
//   Saves:   the existing saves (up to three), New Save, Delete, Back
//   Delete:  pick a save, then confirm
// Choosing a save fades to black and MapLoader starts the world from it.
// One per scene: add it to the Game object. Keyboard only, like the rest of the game:
// up and down choose, Enter or Space confirms, Escape goes back.
public class TitleMenu : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;

    [SerializeField] Texture2D background;    // graphics/menu/menu.jpeg
    [SerializeField] Texture2D titleImage;    // graphics/menu/title.png
    [SerializeField] Font regularFont;        // PixeloidSans

    // COLORS in settings.py
    static readonly Color Fill = UiKit.Hex("#fefefc"), Shadow = UiKit.Hex("#b3c9d9"), Purple = UiKit.Hex("#2e1b53");

    enum Screen { Main, Saves, DeleteWhich, ConfirmDelete }

    struct Entry
    {
        public string label;
        public Action action;
    }

    // True while the title screen is up: the rest of the game ignores its keys
    public static bool IsOpen { get; private set; }

    MapLoader mapLoader;
    GameObject canvasObject;
    RectTransform content, items;
    int index;
    bool leaving;

    Screen screen = Screen.Main;
    readonly List<Entry> entries = new List<Entry>();
    string header;          // a line shown above the buttons (the delete question)
    int pendingDelete;      // the save the delete confirmation is about

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
        canvas.pixelPerfect = true; // snap the interface to whole screen pixels, so text and edges stay sharp

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

    // What the current screen offers
    void BuildEntries()
    {
        entries.Clear();
        header = null;

        switch (screen)
        {
            case Screen.Main:
                Add("Play", () => Go(Screen.Saves));
                Add("Quit", Quit);
                break;

            case Screen.Saves:
                var slots = SaveSystem.ExistingSlots();
                foreach (int existing in slots)
                {
                    int slot = existing;
                    Add($"Save {slot}", () => StartSave(slot));
                }
                if (slots.Count < SaveSystem.MaxSlots) Add("New Save", () => StartSave(SaveSystem.Create()));
                if (slots.Count > 0) Add("Delete", () => Go(Screen.DeleteWhich));
                Add("Back", () => Go(Screen.Main));
                break;

            case Screen.DeleteWhich:
                foreach (int existing in SaveSystem.ExistingSlots())
                {
                    int slot = existing;
                    Add($"Save {slot}", () => { pendingDelete = slot; Go(Screen.ConfirmDelete); });
                }
                Add("Back", () => Go(Screen.Saves));
                break;

            case Screen.ConfirmDelete:
                header = $"Delete Save {pendingDelete}?";
                Add("Yes", () => { SaveSystem.Delete(pendingDelete); Go(Screen.Saves); });
                Add("No", () => Go(Screen.Saves));
                break;
        }
    }

    void Add(string label, Action action)
    {
        entries.Add(new Entry { label = label, action = action });
    }

    void Go(Screen next)
    {
        screen = next;
        index = 0;
        Rebuild();
    }

    // The buttons, identical in shape, and a pixel arrow next to the selected one
    void Rebuild()
    {
        BuildEntries();
        index = Mathf.Clamp(index, 0, entries.Count - 1);
        for (int i = items.childCount - 1; i >= 0; i--) Destroy(items.GetChild(i).gameObject);

        var font = new UiFont(regularFont, 30, 38);
        int count = entries.Count;

        // up to three buttons keep the size of the first design; four or five are smaller so they fit under the logo
        float width = 360, height = count <= 3 ? 68 : 56, gap = count <= 3 ? 96 : 68;
        float top = count <= 3 ? 395 : 330 - (count - 4) * 30;
        float x = (WindowWidth - width) / 2f;

        if (header != null) PixelButton(x - 60, 300, width + 120, 56, header, font);

        for (int i = 0; i < count; i++)
        {
            float y = top + i * gap;
            PixelButton(x, y, width, height, entries[i].label, font);
            if (i == index) PixelArrow(x - 64, y + height / 2f);
        }
    }

    // A flat off-white button with purple text and a hard pale-blue block behind it, offset like a pixel shadow
    void PixelButton(float x, float y, float width, float height, string label, UiFont font)
    {
        const float shadow = 8;
        UiKit.Box(items, x + shadow, y + shadow, width, height, Shadow);
        UiKit.Box(items, x, y, width, height, Fill);
        UiKit.Label(items, label, font, Purple, Anchor.Center, x + width / 2f, y + height / 2f);
    }

    // A right-pointing triangle made of 6 px steps: the same fill and shadow as the buttons
    void PixelArrow(float x, float centerY)
    {
        const float step = 6;
        float[] widths = { 6, 12, 18, 24, 18, 12, 6 };
        float top = centerY - widths.Length * step / 2f;

        for (int pass = 0; pass < 2; pass++)
        {
            float offset = pass == 0 ? 5 : 0; // the shadow first, then the shape
            Color color = pass == 0 ? Shadow : Fill;
            for (int row = 0; row < widths.Length; row++)
                UiKit.Box(items, x + offset, top + row * step + offset, widths[row], step, color);
        }
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || leaving || !IsOpen) return;

        if (keyboard.downArrowKey.wasPressedThisFrame) { index = (index + 1) % entries.Count; Rebuild(); }
        if (keyboard.upArrowKey.wasPressedThisFrame) { index = (index - 1 + entries.Count) % entries.Count; Rebuild(); }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            entries[index].action();
        else if (keyboard.escapeKey.wasPressedThisFrame)
            GoBack();
    }

    // Escape: one screen back
    void GoBack()
    {
        if (screen == Screen.Saves) Go(Screen.Main);
        else if (screen == Screen.DeleteWhich || screen == Screen.ConfirmDelete) Go(Screen.Saves);
    }

    // A save was chosen: the screen fades to black, the menu goes away behind it and, for a save that has never been played,
    // the story plays. Then the world is built from the save.
    void StartSave(int slot)
    {
        if (slot == 0) return;

        leaving = true;
        SaveSystem.CurrentSlot = slot;
        var data = SaveSystem.Load(slot);

        // a brand new save has no position and no monsters yet: that is when the story plays and the first monster is chosen
        bool brandNew = !data.hasPosition && data.party.Count == 0;
        var intro = brandNew ? FindFirstObjectByType<IntroSequence>() : null;
        var starter = brandNew ? FindFirstObjectByType<StarterSelect>() : null;
        if (brandNew && intro == null) Debug.LogWarning("There is no Intro Sequence in the scene: add one to the Game object to play the story for new saves.");
        if (brandNew && starter == null) Debug.LogWarning("There is no Starter Select in the scene: add one to the Game object to choose the first monster. The default party is used instead.");

        // each step hands over to the next one behind a black screen: intro, then the choice of the first monster, then the world
        Action toWorld = () => StartGame(data);
        Action toStarter = () =>
        {
            if (starter != null) starter.Play(data, toWorld);
            else toWorld();
        };

        Action begin = () =>
        {
            canvasObject.SetActive(false);
            if (intro != null) intro.Play(toStarter);
            else toStarter();
        };

        var tint = FindFirstObjectByType<ScreenTint>();
        if (tint != null) tint.Begin(begin);
        else begin();
    }

    void StartGame(SaveData data)
    {
        IsOpen = false;
        canvasObject.SetActive(false);
        mapLoader.StartGame(data);
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
