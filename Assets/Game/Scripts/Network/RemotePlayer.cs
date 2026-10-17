using UnityEngine;

// The other player, as seen on this copy of the game: a character that walks to where the messages say the other player is.
// It is an Entity, so it gets the same walking animation, shadow and draw order as everybody else.
public class RemotePlayer : Entity
{
    const float TeleportDistance = 3f;   // further than this (in tiles) means a new map: it does not walk there, it appears
    const float CatchUpSpeed = 1.3f;     // a little faster than the player, so it never falls behind

    Vector2 target;
    Direction remoteFacing = Direction.Down;
    bool hasTarget;

    // The latest position, facing and whether the other player is walking
    public void SetState(Vector2 position, Direction facing)
    {
        target = position;
        remoteFacing = facing;
        if (!hasTarget) transform.position = position;
        hasTarget = true;
    }

    void Update()
    {
        UpdateYSort();

        if (hasTarget)
        {
            Vector2 position = transform.position;
            Vector2 delta = target - position;

            if (delta.magnitude > TeleportDistance)
            {
                transform.position = target;
                direction = Vector2.zero;
            }
            else
            {
                transform.position = Vector2.MoveTowards(position, target, speed * CatchUpSpeed * Time.deltaTime);
                direction = delta.magnitude > 0.05f ? delta.normalized : Vector2.zero;
            }
        }

        // standing still it looks the way the other player looks
        if (direction == Vector2.zero) SetFacing(remoteFacing);
        Animate(Time.deltaTime);
    }
}
