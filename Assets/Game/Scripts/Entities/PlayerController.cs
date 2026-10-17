using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Port of Player in entities.py
public class PlayerController : Entity
{
    [HideInInspector] public bool noticed;

    [SerializeField] Texture2D noticeTexture;
    SpriteRenderer noticeRenderer;

    Character[] characters = new Character[0]; // the characters in collision_sprites

    protected override void Awake()
    {
        base.Awake();

        // groups.py draws notice.png with its midbottom at the player's midtop while player.noticed.
        // Without the picture the icon is skipped: the player must work either way. (Before, a missing picture stopped Awake halfway
        // in an exported game, because the editor-only code that used to load it by name does not exist there, and the half-made
        // player then failed on every frame, so it never moved.)
        if (noticeTexture == null)
        {
            Debug.LogWarning("The Player Controller has no Notice Texture: right-click its header and choose Reset. The '!' will not show.");
            return;
        }

        var go = new GameObject("Notice");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0, 1f, 0); // the sprite is 128 px tall, so its top is 1 unit above the centre
        noticeRenderer = go.AddComponent<SpriteRenderer>();
        noticeRenderer.sprite = Sprite.Create(noticeTexture, new Rect(0, 0, noticeTexture.width, noticeTexture.height), new Vector2(0.5f, 0f), 64f);
        noticeRenderer.sortingOrder = 2; // above the sprite, inside the player's sorting group
        noticeRenderer.enabled = false;
    }

    IEnumerator Start()
    {
        if (FindFirstObjectByType<MapLoader>() == null)
            Debug.LogError("There is no Map Loader in the scene, so no map gets built (no collisions, water or characters). Create an empty object named 'Game', add the Map Loader component to it, and delete the 'world' object from the Hierarchy.");

        yield return null; // OverworldCharacters spawns them in its own Start
        characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        Debug.Log($"[PlayerController] static colliders: {WorldColliders.Rects.Count}, characters found: {characters.Length}");
    }

#if UNITY_EDITOR
    // The picture has to be stored in the scene, because an exported game cannot look files up by name. If it is missing it is filled
    // in as soon as the editor looks at this component (a moment later: importing is not allowed in the middle of OnValidate).
    void OnValidate()
    {
        if (noticeTexture != null) return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || noticeTexture != null) return;
            noticeTexture = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/ui/notice.png");
            UnityEditor.EditorUtility.SetDirty(this);
        };
    }

    protected override void Reset()
    {
        base.Reset();
        noticeTexture = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/ui/notice.png");
    }
#endif

    void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;
        // the arrow keys, as in the Python game, and W A S D as well
        if (kb.upArrowKey.isPressed || kb.wKey.isPressed)    input.y += 1;
        if (kb.downArrowKey.isPressed || kb.sKey.isPressed)  input.y -= 1;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed)  input.x -= 1;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) input.x += 1;

        direction = input.normalized;
    }

    void Move(float dt)
    {
        characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        transform.position += new Vector3(direction.x * speed * dt, 0, 0);
        Collisions(horizontal: true);

        transform.position += new Vector3(0, direction.y * speed * dt, 0);
        Collisions(horizontal: false);
    }

    // Same axis-by-axis resolution as Player.collisions in entities.py (touching edges don't count as a hit)
    void Collisions(bool horizontal)
    {
        foreach (var rect in WorldColliders.Rects)
            Resolve(rect, horizontal);

        foreach (var character in characters)
            Resolve(character.HitboxRect, horizontal);
    }

    void Resolve(Rect rect, bool horizontal)
    {
        var hitbox = Hitbox;
        if (!WorldColliders.Overlap(hitbox, rect)) return;

        var pos = transform.position;
        if (horizontal)
        {
            if (direction.x > 0) pos.x = rect.xMin - hitbox.width / 2f;
            if (direction.x < 0) pos.x = rect.xMax + hitbox.width / 2f;
        }
        else
        {
            if (direction.y > 0) pos.y = rect.yMin - hitbox.height / 2f; // moving up: top edge stops at the rect's bottom
            if (direction.y < 0) pos.y = rect.yMax + hitbox.height / 2f;
        }
        transform.position = pos;
    }

    void Update()
    {
        if (noticeRenderer != null) noticeRenderer.enabled = noticed;
        UpdateYSort();
        if (!blocked)
        {
            ReadInput();
            Move(Time.deltaTime);
        }
        Animate(Time.deltaTime);
    }
}
