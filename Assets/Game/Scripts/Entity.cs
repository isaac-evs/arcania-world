using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum Direction { Down, Left, Right, Up }

// Port of Entity in entities.py. Shared by the player and (later) the NPC characters.
[RequireComponent(typeof(SpriteRenderer))]
public abstract class Entity : MonoBehaviour
{
    const int TileSize = 64;
    const float AnimationSpeed = 6f; // ANIMATION_SPEED in settings.py

    // character_importer in support.py: rows of the 4x4 sheet
    static readonly Direction[] SheetRows = { Direction.Down, Direction.Left, Direction.Right, Direction.Up };

    [Header("Graphics")]
    [SerializeField] protected Texture2D spriteSheet;
    [SerializeField] protected Texture2D shadowTexture;
    [SerializeField] protected Direction facingDirection = Direction.Down;
    [SerializeField] int sortingOrder = 10;

    [Header("Movement")]
    [SerializeField] protected float speed = 250f / 64f; // 250 px/s in pygame, 64 px per tile

    // Unity coordinates: +y is up (pygame's +y is down)
    [HideInInspector] public Vector2 direction;
    [HideInInspector] public bool blocked;

    readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
    SpriteRenderer spriteRenderer;
    float frameIndex;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        BuildFrames();
        BuildSortingAndShadow();
        spriteRenderer.sprite = frames[GetState()][0];
    }

    void BuildFrames()
    {
        // import_tilemap(4, 4): the sheet is cut into a grid of equal cells
        float cellWidth = spriteSheet.width / 4f, cellHeight = spriteSheet.height / 4f;
        for (int row = 0; row < SheetRows.Length; row++)
        {
            var moving = new Sprite[4];
            for (int col = 0; col < 4; col++)
            {
                // texture rects start at the bottom-left, grid rows count from the top
                var rect = new Rect(col * cellWidth, spriteSheet.height - (row + 1) * cellHeight, cellWidth, cellHeight);
                moving[col] = Sprite.Create(spriteSheet, rect, new Vector2(0.5f, 0.5f), TileSize);
            }
            string name = SheetRows[row].ToString().ToLower();
            frames[name] = moving;
            frames[name + "_idle"] = new[] { moving[0] };
        }
    }

    // groups.py draws the shadow right before each entity, and sorts entities by their centre y.
    // A SortingGroup keeps the shadow and the sprite together and sorts them as one unit.
    void BuildSortingAndShadow()
    {
        var group = gameObject.GetComponent<SortingGroup>();
        if (group == null) group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = sortingOrder;
        spriteRenderer.sortingOrder = 1;

        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(transform, false);
        // shadow.png is blitted at rect.topleft + (40, 110); the sprite is 128x128, so its centre is (66, 120) from the top-left
        shadow.transform.localPosition = new Vector3((66f - 64f) / TileSize, -(120f - 64f) / TileSize, 0);
        var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = Sprite.Create(shadowTexture, new Rect(0, 0, shadowTexture.width, shadowTexture.height), new Vector2(0.5f, 0.5f), TileSize);
        shadowRenderer.sortingOrder = 0;
    }

    protected void Animate(float dt)
    {
        frameIndex += AnimationSpeed * dt;
        var current = frames[GetState()];
        spriteRenderer.sprite = current[(int)(frameIndex % current.Length)];
    }

    string GetState()
    {
        bool moving = direction != Vector2.zero;
        if (moving)
        {
            if (direction.x != 0) facingDirection = direction.x > 0 ? Direction.Right : Direction.Left;
            if (direction.y != 0) facingDirection = direction.y > 0 ? Direction.Up : Direction.Down; // vertical wins on diagonals
        }
        return facingDirection.ToString().ToLower() + (moving ? "" : "_idle");
    }

    public void ChangeFacingDirection(Vector2 targetPos)
    {
        Vector2 relation = targetPos - (Vector2)transform.position;
        if (Mathf.Abs(relation.y) < 30f / TileSize)
            facingDirection = relation.x > 0 ? Direction.Right : Direction.Left;
        else
            facingDirection = relation.y > 0 ? Direction.Up : Direction.Down;
    }

    public void Block()
    {
        blocked = true;
        direction = Vector2.zero;
    }

    public void Unblock()
    {
        blocked = false;
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the textures
    protected virtual void Reset()
    {
        spriteSheet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/graphics/characters/player.png");
        shadowTexture = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/other/shadow.png");
    }
#endif
}
