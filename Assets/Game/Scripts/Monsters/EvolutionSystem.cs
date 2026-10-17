using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Port of check_evolution / end_evolution in main.py and the Evolution screen in evolution.py.
// One per scene: add it to the Game object.
public class EvolutionSystem : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;

    [SerializeField] Font boldFont;               // fonts['bold']: dogicapixelbold, 20
    [SerializeField] Texture2D[] monsterSheets;   // graphics/monsters (readable: they are enlarged and turned white from their pixels)
    [SerializeField] Texture2D[] starFrames;      // graphics/other/star animation, in order (readable)

    PlayerController player;
    GameObject canvasObject;
    RectTransform panel;
    Image tint;
    EvolutionScreen evolution;

    public bool Active => evolution != null;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        BuildCanvas();
    }

    void BuildCanvas()
    {
        canvasObject = new GameObject("Evolution");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600; // over the battle (400) and the monster index (500), under the screen fade (1000)

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // tint_surf: the whole screen, black, alpha 200
        tint = new GameObject("Tint", typeof(RectTransform)).AddComponent<Image>();
        tint.transform.SetParent(canvasObject.transform, false);
        tint.color = new Color(0, 0, 0, 200f / 255f);
        tint.raycastTarget = false;
        var tintRect = tint.rectTransform;
        tintRect.anchorMin = Vector2.zero;
        tintRect.anchorMax = Vector2.one;
        tintRect.offsetMin = tintRect.offsetMax = Vector2.zero;

        panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        panel.anchoredPosition = Vector2.zero;

        canvasObject.SetActive(false);
    }

    // check_evolution: a monster evolves when its level is exactly its evolution level
    public void CheckEvolution()
    {
        var party = PlayerParty.Monsters;
        for (int i = 0; i < party.Count; i++)
        {
            var monster = party[i];
            if (!monster.CanEvolve || monster.level != monster.EvolveLevel) continue;

            AudioManager.Play("evolution");
            player.Block();
            Begin(monster.name, monster.EvolveInto);
            party[i] = new Monster(monster.EvolveInto, monster.level); // a brand new monster: full health, no xp
        }

        if (!Active) AudioManager.Play("overworld", loop: true);
    }

    void Begin(string startMonster, string endMonster)
    {
        evolution?.Destroy();

        Sprite FirstFrame(string name)
        {
            foreach (var sheet in monsterSheets)
            {
                if (sheet.name == name) return UiKit.CutRow(sheet, 4, 2, 0)[0];
            }
            throw new InvalidOperationException($"No monster sheet named '{name}'");
        }

        evolution = new EvolutionScreen(panel, tint, FirstFrame(startMonster), FirstFrame(endMonster), startMonster, endMonster, boldFont, starFrames, EndEvolution);
        canvasObject.SetActive(true);
    }

    // end_evolution
    void EndEvolution()
    {
        evolution.Destroy();
        evolution = null;
        canvasObject.SetActive(false);
        player.Unblock();
        AudioManager.Stop("evolution");
        AudioManager.Play("overworld", loop: true);
    }

    void Update()
    {
        if (evolution != null) evolution.Update(Time.deltaTime);
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the font and pictures
    void Reset()
    {
        boldFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/dogicapixelbold.otf");

        var sheets = new List<Texture2D>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/graphics/monsters" }))
            sheets.Add(OverworldAnimations.LoadPixelArt(AssetDatabase.GUIDToAssetPath(guid), readable: true));
        monsterSheets = sheets.ToArray();

        // import_folder sorts the frames by their number
        var stars = new List<Texture2D>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/graphics/other/star animation" }))
            stars.Add(OverworldAnimations.LoadPixelArt(AssetDatabase.GUIDToAssetPath(guid), readable: true));
        stars.Sort((a, b) => int.Parse(a.name).CompareTo(int.Parse(b.name)));
        starFrames = stars.ToArray();
    }
#endif
}

// The Evolution class of evolution.py
public class EvolutionScreen
{
    const float WindowWidth = 1280f, WindowHeight = 720f;
    static readonly Color White = UiKit.Hex("#f4fefa"), Black = Color.black;

    readonly Countdown startTimer, endTimer;
    readonly Image tint;
    readonly GameObject root;

    // what is shown in each stage
    readonly Image monsterImage, whiteImage, starImage;
    readonly Sprite startMonster, endMonster;
    readonly GameObject startText, endText;

    // star animation
    readonly Texture2D[] starFrames;
    readonly Dictionary<int, Sprite> starSprites = new Dictionary<int, Sprite>();
    float frameIndex;

    // white tint
    float tintAmount;
    const float TintSpeed = 80f;

    public EvolutionScreen(Transform panel, Image screenTint, Sprite startFrame, Sprite endFrame, string startName, string endName,
        Font boldFont, Texture2D[] starFrames, Action endEvolution)
    {
        tint = screenTint;
        this.starFrames = starFrames;
        startTimer = new Countdown(0.8f);
        startTimer.Activate(); // 'start': Timer(800, autostart = True)
        endTimer = new Countdown(1.8f, endEvolution);

        // pygame.transform.scale2x(frames[monster]['idle'][0])
        startMonster = PixelArt.Scale2x(startFrame);
        endMonster = PixelArt.Scale2x(endFrame);
        var whiteMonster = MonsterFrames.Silhouette(startMonster);

        root = new GameObject("Evolution Screen", typeof(RectTransform));
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(panel, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

        float centerX = WindowWidth / 2f, centerY = WindowHeight / 2f;
        monsterImage = UiKit.Picture(root.transform, startMonster, Anchor.Center, centerX, centerY);
        whiteImage = UiKit.Picture(root.transform, whiteMonster, Anchor.Center, centerX, centerY);
        whiteImage.color = new Color(1, 1, 1, 0);

        // text: midtop 20 px under the monster, on a white box that is 20 px bigger than the text
        float textTop = centerY + startMonster.rect.height / 2f + 20f;
        startText = TextBox(root.transform, $"{startName} is evolving", boldFont, centerX, textTop);
        endText = TextBox(root.transform, $"{startName} evolved into {endName}", boldFont, centerX, textTop);
        endText.SetActive(false);

        starImage = UiKit.Picture(root.transform, startMonster, Anchor.Center, centerX, centerY);
        starImage.gameObject.SetActive(false);

        root.SetActive(false); // nothing is shown during the first 800 ms
        tint.gameObject.SetActive(false);
    }

    static GameObject TextBox(Transform parent, string content, Font font, float centerX, float top)
    {
        // a rendered line of the bold font is 21 px high when it has a letter that hangs below the baseline, 18 px otherwise
        int height = content.IndexOfAny(new[] { 'g', 'j', 'p', 'q' }) >= 0 ? 21 : 18;
        var uiFont = new UiFont(font, 20, height);

        var group = new GameObject("Text", typeof(RectTransform));
        var groupRect = (RectTransform)group.transform;
        groupRect.SetParent(parent, false);
        groupRect.anchorMin = Vector2.zero;
        groupRect.anchorMax = Vector2.one;
        groupRect.offsetMin = groupRect.offsetMax = Vector2.zero;

        float width = UiKit.Measure(uiFont, content);
        var textRect = UiKit.Place(Anchor.MidTop, centerX, top, width, height);
        UiKit.Box(group.transform, textRect.x - 10, textRect.y - 10, textRect.width + 20, textRect.height + 20, White, 5);
        UiKit.Label(group.transform, content, uiFont, Black, Anchor.MidTop, centerX, top);
        return group;
    }

    public void Update(float dt)
    {
        startTimer.Update(dt);
        endTimer.Update(dt);

        if (startTimer.active) return;

        root.SetActive(true);
        tint.gameObject.SetActive(true);

        if (tintAmount < 255f)
        {
            // the monster turns white
            monsterImage.sprite = startMonster;
            startText.SetActive(true);

            tintAmount += TintSpeed * dt;
            whiteImage.color = new Color(1, 1, 1, Mathf.Clamp(tintAmount, 0f, 255f) / 255f);
        }
        else
        {
            // the new monster, with the stars
            monsterImage.sprite = endMonster;
            whiteImage.gameObject.SetActive(false);
            startText.SetActive(false);
            endText.SetActive(true);
            DisplayStars(dt);

            if (!endTimer.active) endTimer.Activate();
        }
    }

    // display_stars: the frames run once, at 20 per second, filling the screen
    void DisplayStars(float dt)
    {
        frameIndex += 20f * dt;
        if (frameIndex < starFrames.Length)
        {
            int index = (int)frameIndex;
            if (!starSprites.TryGetValue(index, out var sprite))
            {
                var texture = starFrames[index];
                sprite = starSprites[index] = PixelArt.Scale2x(UiKit.SpriteFrom(texture, new Rect(0, 0, texture.width, texture.height)));
            }
            starImage.sprite = sprite;
            starImage.SetNativeSize();
            // frame.get_frect(center = (WINDOW_WIDTH / 2, WINDOW_HEIGHT / 2))
            var size = starImage.rectTransform.sizeDelta;
            starImage.rectTransform.anchoredPosition = new Vector2(Mathf.Floor(WindowWidth / 2f - size.x / 2f), -Mathf.Floor(WindowHeight / 2f - size.y / 2f));
            starImage.gameObject.SetActive(true);
        }
        else
        {
            starImage.gameObject.SetActive(false);
        }
    }

    public void Destroy()
    {
        tint.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(root);
    }
}
