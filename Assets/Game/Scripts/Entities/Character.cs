using System;
using System.Collections.Generic;
using UnityEngine;

// Port of Character in entities.py: looks around, notices the player in its line of sight and walks up to them.
public class Character : Entity
{
    const float TileSize = 64f;
    const float LookAroundInterval = 1.5f; // Timer(1500, repeat = True)
    const float NoticeDelay = 0.5f;        // Timer(500, func = self.start_move)

    [HideInInspector] public string characterId;
    [HideInInspector] public float radius; // pixels, like the 'radius' property in the map
    [HideInInspector] public TrainerData data;
    [HideInInspector] public bool canRotate = true;
    [HideInInspector] public bool nurse; // character_id == 'Nurse': heals the player's monsters after the dialog
    [HideInInspector] public PlayerController player;
    [HideInInspector] public Action<Character> createDialog;
    [HideInInspector] public List<Character> earlierCharacters = new List<Character>(); // the other characters that existed when this one was created

    bool hasMoved, hasNoticed;
    float lookTimer;
    float noticeTimer = -1f; // -1 = inactive

    void Update()
    {
        float dt = Time.deltaTime;

        lookTimer += dt;
        if (lookTimer >= LookAroundInterval)
        {
            lookTimer = 0f;
            RandomViewDirection();
        }

        if (noticeTimer >= 0f)
        {
            noticeTimer += dt;
            if (noticeTimer >= NoticeDelay)
            {
                noticeTimer = -1f;
                StartMove();
            }
        }

        Animate(dt);

        if (data != null && data.lookAround && player != null)
        {
            Raycast();
            Move(dt);
        }
    }

    void RandomViewDirection()
    {
        if (canRotate && data != null && data.directions.Length > 0)
            facingDirection = data.directions[UnityEngine.Random.Range(0, data.directions.Length)];
    }

    void Raycast()
    {
        if (CheckConnections(radius, this, player) && HasLineOfSight() && !hasMoved && !hasNoticed)
        {
            player.Block();
            player.ChangeFacingDirection(transform.position);
            noticeTimer = 0f;
            canRotate = false;
            hasNoticed = true;
            player.noticed = true;
            // TODO: notice sound (audio isn't imported yet)
        }
    }

    // has_los: nothing solid between the character and the player
    bool HasLineOfSight()
    {
        Vector2 from = transform.position, to = player.transform.position;
        if (Vector2.Distance(from, to) >= radius / TileSize) return false;

        foreach (var rect in WorldColliders.SightBlockers)
        {
            if (SegmentHitsRect(from, to, rect)) return false;
        }
        foreach (var other in earlierCharacters)
        {
            // an entity's rect is the whole 128x128 sprite
            var rect = new Rect((Vector2)other.transform.position - Vector2.one, new Vector2(2f, 2f));
            if (SegmentHitsRect(from, to, rect)) return false;
        }
        return true;
    }

    void StartMove()
    {
        Vector2 relation = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
        direction = new Vector2(Mathf.Round(relation.x), Mathf.Round(relation.y));
    }

    void Move(float dt)
    {
        if (hasMoved || direction == Vector2.zero) return;

        // hitbox.inflate(10, 10): stop when the (slightly grown) hitboxes touch
        var grown = Hitbox;
        grown.xMin -= 5f / TileSize; grown.xMax += 5f / TileSize;
        grown.yMin -= 5f / TileSize; grown.yMax += 5f / TileSize;

        if (!WorldColliders.Overlap(grown, player.HitboxRect))
        {
            transform.position += (Vector3)(direction * speed * dt);
        }
        else
        {
            direction = Vector2.zero;
            hasMoved = true;
            createDialog?.Invoke(this);
            player.noticed = false;
        }
    }

    // Liang-Barsky: does the segment a-b touch the rectangle (pygame's Rect.clipline)?
    static bool SegmentHitsRect(Vector2 a, Vector2 b, Rect r)
    {
        float t0 = 0f, t1 = 1f;
        Vector2 d = b - a;
        return Clip(-d.x, a.x - r.xMin, ref t0, ref t1) && Clip(d.x, r.xMax - a.x, ref t0, ref t1)
            && Clip(-d.y, a.y - r.yMin, ref t0, ref t1) && Clip(d.y, r.yMax - a.y, ref t0, ref t1);
    }

    static bool Clip(float p, float q, ref float t0, ref float t1)
    {
        if (p == 0f) return q >= 0f;
        float r = q / p;
        if (p < 0f)
        {
            if (r > t1) return false;
            if (r > t0) t0 = r;
        }
        else
        {
            if (r < t0) return false;
            if (r < t1) t1 = r;
        }
        return true;
    }

    // Select a character while playing to see its hitbox
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(HitboxRect.center, HitboxRect.size);
    }
}
