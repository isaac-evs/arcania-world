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

#if UNITY_EDITOR
        if (noticeTexture == null) noticeTexture = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/ui/notice.png");
#endif
        // groups.py draws notice.png with its midbottom at the player's midtop while player.noticed
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
        yield return null; // OverworldCharacters spawns them in its own Start
        characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        Debug.Log($"[PlayerController] static colliders: {WorldColliders.Rects.Count}, characters found: {characters.Length}");
    }

#if UNITY_EDITOR
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
        if (kb.upArrowKey.isPressed)    input.y += 1;
        if (kb.downArrowKey.isPressed)  input.y -= 1;
        if (kb.leftArrowKey.isPressed)  input.x -= 1;
        if (kb.rightArrowKey.isPressed) input.x += 1;

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
        noticeRenderer.enabled = noticed;
        UpdateYSort();
        if (!blocked)
        {
            ReadInput();
            Move(Time.deltaTime);
        }
        Animate(Time.deltaTime);
    }
}
