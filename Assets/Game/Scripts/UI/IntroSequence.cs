using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The story that plays once, for a brand new save: five pictures, each with a text bubble in the style of the dialogue boxes
// (white, rounded, black text in the dialogue font), over the intro music. Space moves on to the next picture and the last one
// leads into the game. The TitleMenu starts it; one per scene, add it to the Game object.
public class IntroSequence : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;
    const float InputLockTime = 0.5f;   // like the dialogue: Space is ignored this long after a new picture appears

    [SerializeField] Texture2D[] slideImages;   // graphics/menu/intro1 ... intro5
    [SerializeField] Font regularFont;          // PixeloidSans, the dialogue font

    static readonly string[] Texts =
    {
        "Once every century, the sky over Arcania dances with silver light. The Great Starfall returns.",
        "It is a season of joyous celebration. Tamers and their companions gather from across the land to test their bonds and crown a new King.",
        "Even the wild creatures are drawn from their habitats. They stare up at the unnaturally large moon, restless... stirred by an ancient instinct we no longer understand.",
        "We cheer for the falling stars and celebrate the turning of the age, carrying on a tradition so old that its true origin has faded into myth.",
        "The festival lights burn bright, and tonight, the stars seem to fall closer to the earth than ever before. The stage is set... your journey begins.",
    };

    GameObject canvasObject;
    RawImage picture;
    RectTransform items;
    ScreenTint tint;
    Action finished;

    int index;
    bool active;
    bool fading;        // a picture is changing behind the black screen
    float lockTimer;

    public bool IsPlaying => active;

    void Start()
    {
        BuildCanvas();
    }

    void BuildCanvas()
    {
        canvasObject = new GameObject("Intro");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // over the title screen (800), under the screen fade (1000)
        canvas.pixelPerfect = true; // snap the interface to whole screen pixels, so text and edges stay sharp

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // the picture covers the whole screen whatever its shape: it is cropped, never stretched
        picture = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
        picture.transform.SetParent(canvasObject.transform, false);
        picture.raycastTarget = false;
        var pictureRect = picture.rectTransform;
        pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0.5f, 0.5f);
        pictureRect.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 2752f / 1536f;

        // a 1280x720 panel in the middle of the screen: everything below uses pygame coordinates inside it
        var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        panel.anchoredPosition = Vector2.zero;

        items = new GameObject("Bubble", typeof(RectTransform)).GetComponent<RectTransform>();
        items.SetParent(panel, false);
        items.anchorMin = Vector2.zero;
        items.anchorMax = Vector2.one;
        items.offsetMin = items.offsetMax = Vector2.zero;

        canvasObject.SetActive(false);
    }

    // Starts the story; whenFinished runs once the last picture has been left, behind a black screen
    public void Play(Action whenFinished)
    {
        finished = whenFinished;
        tint = FindFirstObjectByType<ScreenTint>();
        index = 0;
        active = true;
        fading = false;
        lockTimer = InputLockTime;

        canvasObject.SetActive(true);
        ShowSlide(0);
        AudioManager.PlayMusic("intro");
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (!active || fading || keyboard == null) return;

        if (lockTimer > 0f) lockTimer -= Time.deltaTime;
        if (keyboard.spaceKey.wasPressedThisFrame && lockTimer <= 0f) Next();
    }

    // Space: the next picture, with a quick fade through black; after the last one, into the game
    void Next()
    {
        index++;
        fading = true;

        if (index < Texts.Length)
        {
            tint.Begin(() =>
            {
                ShowSlide(index);
                lockTimer = InputLockTime;
                fading = false;
            });
        }
        else
        {
            tint.Begin(() =>
            {
                active = false;
                canvasObject.SetActive(false);
                fading = false;
                finished?.Invoke();
            });
        }
    }

    void ShowSlide(int slide)
    {
        picture.texture = slideImages[slide];
        BuildBubble(Texts[slide]);
    }

    // A white rounded box with the text in black, like DialogSprite but wrapped over several lines, near the top of the picture
    void BuildBubble(string text)
    {
        for (int i = items.childCount - 1; i >= 0; i--) Destroy(items.GetChild(i).gameObject);

        var font = new UiFont(regularFont, 30, 38);
        const float maxTextWidth = 980, padding = 14, top = 36;

        var lines = Wrap(text, font, maxTextWidth);
        float widest = 0;
        foreach (var line in lines) widest = Mathf.Max(widest, UiKit.Measure(font, line));

        float width = widest + padding * 2, height = lines.Count * font.surfaceHeight + padding * 2;
        float x = (WindowWidth - width) / 2f;
        UiKit.Box(items, x, top, width, height, Color.white, 4); // COLORS['pure white'], radius 4

        for (int i = 0; i < lines.Count; i++)
            UiKit.Label(items, lines[i], font, Color.black, Anchor.MidTop, WindowWidth / 2f, top + padding + i * font.surfaceHeight);
    }

    // Breaks the text into lines that are no wider than maxWidth, between words
    static List<string> Wrap(string text, UiFont font, float maxWidth)
    {
        var lines = new List<string>();
        string line = "";
        foreach (var word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && UiKit.Measure(font, candidate) > maxWidth)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the five pictures and the font
    void Reset()
    {
        slideImages = new Texture2D[5];
        for (int i = 0; i < 5; i++)
            slideImages[i] = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/graphics/menu/intro{i + 1}.jpeg");
        regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/PixeloidSans.ttf");
    }
#endif
}
