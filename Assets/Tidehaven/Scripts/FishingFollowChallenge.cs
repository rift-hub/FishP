using UnityEngine;

/// <summary>Small, deterministic fishing challenge shared by the live controls and play checks.</summary>
public sealed class FishingFollowChallenge
{
    public enum Outcome { Ongoing, Caught, Escaped }
    public const float Tolerance = 0.22f;
    public const float ArrowSpeed = 0.32f;
    public const float TimeLimit = 60f;
    public const float InitialGrace = 1.2f;
    public const float MissGrace = 0.65f;

    public float TargetPosition { get; private set; } = 0.5f;
    public float ArrowPosition { get; private set; } = 0.5f;
    public float Progress { get; private set; }
    public float Escape { get; private set; }
    public float Elapsed { get; private set; }
    public bool Tracking { get; private set; }
    public bool IsHeld { get; private set; }
    float seed;
    float missTime;
    float idleTime;
    Outcome outcome;

    public void Begin(float motionSeed)
    {
        seed = motionSeed;
        Elapsed = 0f;
        Progress = 0.15f;
        Escape = 0f;
        TargetPosition = TargetAt(0f, seed);
        ArrowPosition = Mathf.Clamp(TargetPosition, Tolerance, 1f - Tolerance);
        Tracking = true;
        IsHeld = false;
        missTime = 0f;
        idleTime = 0f;
        outcome = Outcome.Ongoing;
    }

    public static float TargetAt(float elapsed, float motionSeed)
    {
        float t = Mathf.Max(0f, elapsed - InitialGrace);
        return 0.5f + 0.22f * Mathf.Sin(t * 0.45f + motionSeed)
                    + 0.07f * Mathf.Sin(t * 0.9f + motionSeed * 1.73f);
    }

    /// <summary>Hold to guide the white bar right; release to let it move left.</summary>
    public Outcome StepHeld(bool held, float deltaTime)
    {
        if (outcome != Outcome.Ongoing) return outcome;
        IsHeld = held;
        return Step(held ? 1f : 0f, deltaTime);
    }

    public Outcome Step(float desiredArrowPosition, float deltaTime)
    {
        if (outcome != Outcome.Ongoing) return outcome;
        float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
        Elapsed += dt;
        TargetPosition = TargetAt(Elapsed, seed);
        float previousPosition = ArrowPosition;
        ArrowPosition = Mathf.MoveTowards(ArrowPosition,
            Mathf.Clamp(desiredArrowPosition, Tolerance, 1f - Tolerance), ArrowSpeed * dt);
        idleTime = Mathf.Abs(ArrowPosition - previousPosition) > 0.00001f ? 0f : idleTime + dt;
        Tracking = Mathf.Abs(ArrowPosition - TargetPosition) <= Tolerance;

        // Brief mistakes cost nothing. Holding the bar still against an end stop is
        // allowed for a moment, but cannot passively catch a fish without guiding it.
        if (Elapsed > InitialGrace)
        {
            bool activelyTracking = Tracking && idleTime <= 1.8f;
            if (activelyTracking)
            {
                missTime = 0f;
                Progress = Mathf.Clamp01(Progress + dt * 0.18f);
                Escape = Mathf.Clamp01(Escape - dt * 0.30f);
            }
            else
            {
                float previousMissTime = missTime;
                missTime += dt;
                float penaltyTime = Mathf.Max(0f, missTime - MissGrace) - Mathf.Max(0f, previousMissTime - MissGrace);
                Progress = Mathf.Clamp01(Progress - penaltyTime * 0.018f);
                Escape = Mathf.Clamp01(Escape + penaltyTime * 0.10f);
            }
        }
        if (Progress >= 1f) outcome = Outcome.Caught;
        else if (Escape >= 1f || Elapsed >= TimeLimit) outcome = Outcome.Escaped;
        return outcome;
    }
}
