using System;
using UnityEngine;
using UnityEngine.UI;

// Port of Game.tint_screen in main.py: a black overlay that fades in, runs an action at full black, then fades out.
public class ScreenTint : MonoBehaviour
{
    const float TintSpeed = 600f; // tint_speed, in alpha units (0-255) per second

    Image overlay;
    float progress;                 // tint_progress, 0-255
    bool tinting;                   // tint_mode == 'tint'
    Action atFullTint;

    public bool Busy => tinting || progress > 0f;

    void Awake()
    {
        var canvasObject = new GameObject("Screen Tint");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var imageObject = new GameObject("Black");
        imageObject.transform.SetParent(canvasObject.transform, false);
        overlay = imageObject.AddComponent<Image>();
        overlay.color = new Color(0, 0, 0, 0);
        overlay.raycastTarget = false;
        var rect = overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Fades to black; when it is fully black, action runs once and the screen fades back in
    public void Begin(Action action)
    {
        tinting = true;
        atFullTint = action;
    }

    void Update()
    {
        if (tinting)
        {
            progress += TintSpeed * Time.deltaTime;
            if (progress >= 255f)
            {
                tinting = false;
                var action = atFullTint;
                atFullTint = null;
                action?.Invoke();
            }
        }
        else
        {
            progress -= TintSpeed * Time.deltaTime;
        }

        progress = Mathf.Clamp(progress, 0f, 255f);
        overlay.color = new Color(0, 0, 0, progress / 255f);
    }
}
