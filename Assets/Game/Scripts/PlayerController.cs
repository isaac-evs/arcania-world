using UnityEngine;
using UnityEngine.InputSystem;

// Port of Player in entities.py
public class PlayerController : Entity
{
    [HideInInspector] public bool noticed;

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
        transform.position += new Vector3(direction.x * speed * dt, 0, 0);
        Collisions(horizontal: true);

        transform.position += new Vector3(0, direction.y * speed * dt, 0);
        Collisions(horizontal: false);
    }

    // Same axis-by-axis resolution as Player.collisions in entities.py (touching edges don't count as a hit)
    void Collisions(bool horizontal)
    {
        foreach (var rect in WorldColliders.Rects)
        {
            var hitbox = Hitbox;
            if (!WorldColliders.Overlap(hitbox, rect)) continue;

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
    }

    void Update()
    {
        UpdateYSort();
        if (!blocked)
        {
            ReadInput();
            Move(Time.deltaTime);
        }
        Animate(Time.deltaTime);
    }
}
