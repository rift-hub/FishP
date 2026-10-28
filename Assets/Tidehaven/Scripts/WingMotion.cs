using UnityEngine;

/// <summary>
/// Smoothly plays the seamless 'flap' skeletal animation from seagull-2.glb on the GPU,
/// modulating playback speed between active flapping and gentle soaring without ever
/// switching to the detached 'planer' clip.
/// </summary>
[DisallowMultipleComponent]
public class WingMotion : MonoBehaviour
{
    [Min(0.1f)] public float flapsPerSecond = 1.45f;
    public float phaseOffset;
    [Range(0f, 80f)] public float upstrokeDegrees = 48f;
    [Range(0f, 80f)] public float downstrokeDegrees = 32f;

    public AnimationClip flapClip;
    public AnimationClip glideClip;
    public GameObject animationRoot;

    float animTime;

    void Start()
    {
        if (!animationRoot)
        {
            var arm = transform.Find("Seagull");
            animationRoot = arm ? arm.gameObject : gameObject;
        }
        animTime = phaseOffset * 0.85f;
    }

    void LateUpdate()
    {
        if (flapClip && animationRoot)
        {
            // Smoothly modulate speed between active flapping (1.6x) and gentle soaring (0.35x)
            // using a continuous sine wave so there is never any pose snap or bone detachment.
            float breeze = 0.5f + 0.5f * Mathf.Sin(Time.time * 0.65f + phaseOffset * 1.9f);
            float speed = Mathf.Lerp(0.38f, 1.55f, breeze) * flapsPerSecond;
            animTime = (animTime + Time.deltaTime * speed) % Mathf.Max(0.01f, flapClip.length);
            flapClip.SampleAnimation(animationRoot, animTime);
        }
    }
}
