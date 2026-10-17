using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AttackSprite in sprites.py: the animation that plays on the target of an attack
public partial class Battle
{
    const float AttackAnimationSpeed = 6f; // ANIMATION_SPEED in settings.py

    class AttackSprite
    {
        public Image image;
        public Sprite[] frames;
        public float frameIndex;
    }

    readonly List<AttackSprite> attackSprites = new List<AttackSprite>();

    // AttackSprite(target_sprite.rect.center, frames, battle_sprites): drawn in the overlay layer, above everything else on the field
    void CreateAttackSprite(Vector2 center, Sprite[] frames)
    {
        attackSprites.Add(new AttackSprite
        {
            image = UiKit.Picture(layerOverlay, frames[0], Anchor.Center, center.x, center.y),
            frames = frames,
        });
    }

    // AttackSprite.animate: through the frames once, then it is gone
    void UpdateAttackSprites(float dt)
    {
        for (int i = attackSprites.Count - 1; i >= 0; i--)
        {
            var sprite = attackSprites[i];
            sprite.frameIndex += AttackAnimationSpeed * dt;
            if (sprite.frameIndex < sprite.frames.Length)
            {
                sprite.image.sprite = sprite.frames[(int)sprite.frameIndex];
            }
            else
            {
                Object.Destroy(sprite.image.gameObject);
                attackSprites.RemoveAt(i);
            }
        }
    }
}
