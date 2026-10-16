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
        transform.position += (Vector3)(direction * speed * dt);
    }

    void Update()
    {
        if (!blocked)
        {
            ReadInput();
            Move(Time.deltaTime);
        }
        Animate(Time.deltaTime);
    }
}
