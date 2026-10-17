using System;

// Port of Timer in timer.py (without repeat): activate it, call Update every frame, and func runs once the duration has passed
public class Countdown
{
    public float duration;       // seconds
    public bool active;
    readonly Action func;
    float elapsed;

    public Countdown(float duration, Action func = null)
    {
        this.duration = duration;
        this.func = func;
    }

    public void Activate()
    {
        active = true;
        elapsed = 0f;
    }

    public void Update(float dt)
    {
        if (!active) return;

        elapsed += dt;
        if (elapsed >= duration)
        {
            func?.Invoke();
            active = false; // like Timer.deactivate; func may have activated it again, which Activate handles
        }
    }
}
