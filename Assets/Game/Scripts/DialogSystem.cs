using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Port of DialogTree / DialogSprite in dialog.py, plus the Space-to-talk part of Game.input in main.py.
// Attach to the imported map root (the "world" object).
public class DialogSystem : MonoBehaviour
{
    const float PixelsPerUnit = 64f;
    const int FontSize = 30;            // fonts['dialog'] in main.py
    const int LineHeight = 37;          // font.get_height() of PixeloidSans at 30 px
    const int Padding = 5;
    const float TalkRadius = 100f;      // check_connections(100, player, character)
    const float InputLockTime = 0.5f;   // Timer(500): Space is ignored this long after a new line appears
    const float BubbleGap = 10f;        // the bubble floats 10 px above the character's head

    [SerializeField] Font font;

    PlayerController player;
    Character character;               // the character we are talking to, null when there is no dialog
    string[] lines;
    int lineIndex;
    float lockTimer;
    GameObject bubble;
    Sprite bubbleSprite;

    public bool Active => character != null;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        bubbleSprite = BuildBubbleSprite();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        bool space = keyboard.spaceKey.wasPressedThisFrame;

        // Game.input: Space next to a character that you are facing starts a dialog
        if (!Active && space)
        {
            foreach (var other in FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                if (Entity.CheckConnections(TalkRadius, player, other))
                {
                    player.Block();
                    other.ChangeFacingDirection(player.transform.position);
                    CreateDialog(other);
                    other.canRotate = false;
                }
            }
        }

        // DialogTree.update / input
        if (Active)
        {
            if (lockTimer > 0f) lockTimer -= Time.deltaTime;
            if (space && lockTimer <= 0f) NextLine();
        }
    }

    public void CreateDialog(Character target)
    {
        if (Active) return;

        character = target;
        var data = target.data;
        lines = data.defeated && data.dialogDefeated != null ? data.dialogDefeated : data.dialogDefault;
        lineIndex = 0;
        ShowLine();
    }

    void NextLine()
    {
        Destroy(bubble);
        lineIndex++;
        if (lineIndex < lines.Length) ShowLine();
        else EndDialog();
    }

    void EndDialog()
    {
        var finished = character;
        character = null;

        // TODO: the nurse heals the player's monsters, and a trainer that isn't defeated starts a battle
        // (the player stays blocked until it ends). Until those exist, the player is simply released.
        player.Unblock();
    }

    // DialogSprite: white rounded box with black text, midbottom 10 px above the character's head
    void ShowLine()
    {
        lockTimer = InputLockTime;
        string message = lines[lineIndex];

        float textWidth = MeasureWidth(message);
        float width = Mathf.Max(30f, textWidth + Padding * 2);
        float height = LineHeight + Padding * 2;

        bubble = new GameObject("Dialog");
        // character.rect.midtop is one unit above the centre of the 128 px sprite
        var position = character.transform.position + new Vector3(0, 1f + BubbleGap / PixelsPerUnit, 0);
        // whole pixels only, like pygame's blit: a bubble between two pixels would blur the text
        bubble.transform.position = new Vector3(Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit, Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit, 0);

        var background = bubble.AddComponent<SpriteRenderer>();
        background.sprite = bubbleSprite;
        background.drawMode = SpriteDrawMode.Sliced;
        background.size = new Vector2(width / PixelsPerUnit, height / PixelsPerUnit);
        background.sortingOrder = WorldSorting.Top + 1; // z = WORLD_LAYERS['top']

        // pixel-for-pixel text: one font pixel is 1/64 unit (TextMesh scales by characterSize * 0.1)
        font.RequestCharactersInTexture(message, FontSize);
        font.material.mainTexture.filterMode = FilterMode.Point;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(bubble.transform, false);
        // the text is centred in the box: top-left corner at (-textWidth / 2, top edge minus the padding)
        textObject.transform.localPosition = new Vector3(-textWidth / 2f / PixelsPerUnit, (height - Padding) / PixelsPerUnit, 0);

        var text = textObject.AddComponent<TextMesh>();
        text.font = font;
        text.fontSize = FontSize;
        text.characterSize = 10f / PixelsPerUnit;
        text.anchor = TextAnchor.UpperLeft;
        text.alignment = TextAlignment.Left;
        text.color = Color.black;
        text.text = message;

        var textRenderer = textObject.GetComponent<MeshRenderer>();
        textRenderer.sharedMaterial = font.material;
        textRenderer.sortingOrder = WorldSorting.Top + 2;
    }

    // pygame's font.render width is the sum of the character advances
    float MeasureWidth(string message)
    {
        font.RequestCharactersInTexture(message, FontSize);
        float width = 0f;
        foreach (char c in message)
        {
            if (font.GetCharacterInfo(c, out CharacterInfo info, FontSize)) width += info.advance;
        }
        return width;
    }

    // pygame.draw.rect(..., radius = 4) as a 9-sliced sprite, so it stretches to any size without blurring
    static Sprite BuildBubbleSprite()
    {
        const int size = 16, radius = 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x < radius ? radius - (x + 0.5f) : x >= size - radius ? (x + 0.5f) - (size - radius) : 0f;
                float dy = y < radius ? radius - (y + 0.5f) : y >= size - radius ? (y + 0.5f) - (size - radius) : 0f;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
            }
        }
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu)
    void Reset()
    {
        // hinted raster = no anti-aliasing, which a pixel font needs to stay sharp
        var importer = AssetImporter.GetAtPath("Assets/Game/graphics/fonts/PixeloidSans.ttf") as TrueTypeFontImporter;
        if (importer != null && importer.fontRenderingMode != FontRenderingMode.HintedRaster)
        {
            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            importer.SaveAndReimport();
        }
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/PixeloidSans.ttf");
    }
#endif
}
