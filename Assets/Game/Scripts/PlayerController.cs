using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] float speed = 250f / 64f; // pygame: 250 px/s, 64 px per tile

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;
        if (kb.upArrowKey.isPressed)    input.y += 1;
        if (kb.downArrowKey.isPressed)  input.y -= 1;
        if (kb.leftArrowKey.isPressed)  input.x -= 1;
        if (kb.rightArrowKey.isPressed) input.x += 1;

        transform.position += (Vector3)(input.normalized * speed * Time.deltaTime);
    }
}
