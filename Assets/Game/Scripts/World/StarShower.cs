using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shooting stars that fall across the screen on the darkest part of the night: bright pixel heads with fading tails.
// Two uses: DayNightCycle creates one that is drawn over the world and under the menus, and a night battle builds one
// inside its own screen (BuildInto). The strength is set from outside (0 = none, 1 = deep night).
public class StarShower : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;
    const int MaxMeteors = 3;
    const int TailSegments = 14;
    const float SegmentStep = 16f;   // pixels between the squares of a tail
    const float SegmentSize = 16f;

    static readonly Color Head = new Color(0.996f, 0.996f, 0.988f);   // #fefefc, like the menu buttons
    static readonly Color Tail = new Color(0.70f, 0.79f, 0.85f);      // #b3c9d9, the pale blue of their shadows

    class Meteor
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 position, velocity;   // pygame coordinates: y grows downwards
        public float age, life;
    }

    public float Intensity { get; set; }

    // Where the stars fade away on their way down (pygame y): a battle picture has the ground in its lower part
    public float HorizonY { get; set; } = WindowHeight;

    readonly List<Meteor> meteors = new List<Meteor>();
    RectTransform panel;
    float spawnTimer;
    bool built;

    // Builds the shower inside an existing 1280 x 720 panel (the battle screen) instead of on a canvas of its own
    public void BuildInto(Transform parent)
    {
        panel = UiKit.NewRect(parent, "Star Shower", 0, 0, WindowWidth, WindowHeight);
        built = true;
    }

    void Start()
    {
        if (!built) BuildOwnCanvas();
    }

    void BuildOwnCanvas()
    {
        built = true;
        var canvasObject = new GameObject("Star Shower");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // over the world, under every menu
        canvas.pixelPerfect = true;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        panel.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // new stars appear now and then, more often the darker the night is
        if (Intensity > 0.3f && meteors.Count < MaxMeteors)
        {
            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                Spawn();
                spawnTimer = Random.Range(3f, 8f) / Intensity;
            }
        }

        for (int i = meteors.Count - 1; i >= 0; i--)
        {
            var meteor = meteors[i];
            meteor.age += dt;
            meteor.position += meteor.velocity * dt;
            meteor.rect.anchoredPosition = new Vector2(Mathf.Floor(meteor.position.x), -Mathf.Floor(meteor.position.y));

            // fade in quickly and out slowly, and the shower stops when the day comes
            float life = meteor.age / meteor.life;
            float horizon = 1f - Mathf.Clamp01((meteor.position.y - (HorizonY - 140f)) / 140f); // gone before the ground
            meteor.group.alpha = Mathf.Min(1f, life * 8f) * (1f - Mathf.Clamp01((life - 0.6f) / 0.4f)) * Mathf.Clamp01(Intensity * 2f) * horizon;

            if (life >= 1f || meteor.position.y > WindowHeight + 100f || meteor.position.x < -150f)
            {
                Destroy(meteor.rect.gameObject);
                meteors.RemoveAt(i);
            }
        }
    }

    // A star comes in over the top or the right edge and falls towards the bottom left
    void Spawn()
    {
        float angle = Random.Range(28f, 46f) * Mathf.Deg2Rad;
        float speed = Random.Range(420f, 680f);
        var direction = new Vector2(-Mathf.Cos(angle), Mathf.Sin(angle)); // left and down

        var meteor = new Meteor
        {
            position = new Vector2(Random.Range(250f, WindowWidth + 350f), -60f),
            velocity = direction * speed,
            life = Random.Range(1.0f, 1.6f),
        };

        meteor.rect = UiKit.NewRect(panel, "Shooting star", 0, 0, 0, 0);
        meteor.group = meteor.rect.gameObject.AddComponent<CanvasGroup>();
        meteor.group.alpha = 0f;

        // a bright square for the head and squares behind it, up and to the right, that fade out: whole pixels, so they stay crisp
        for (int i = 0; i < TailSegments; i++)
        {
            float t = i / (float)TailSegments;
            var color = i == 0 ? Head : Color.Lerp(Tail, Head, 0.3f);
            color.a = Mathf.Pow(1f - t, 1.6f);
            float size = Mathf.Max(5f, SegmentSize - i * 0.8f);
            UiKit.Box(meteor.rect, Mathf.Round(-direction.x * SegmentStep * i), Mathf.Round(-direction.y * SegmentStep * i), size, size, color);
        }

        meteors.Add(meteor);
    }
}
