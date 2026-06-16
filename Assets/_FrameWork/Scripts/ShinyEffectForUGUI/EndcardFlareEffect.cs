using UnityEngine;
using System.Collections;

public class EndcardFlareEffect : MonoBehaviour
{
    [Header("Flare References")]
    [Tooltip("Sprite Renderer object for Flare 1")]
    public Transform Flare1;
    [Tooltip("Sprite Renderer object for Flare 2")]
    public Transform Flare2;

    [Header("Scale Animation Settings")]
    [Tooltip("Initial scale of the flares before animation starts")]
    public float StartScale = 0.2f;
    [Tooltip("Target scale of the flares after animation")]
    public float TargetScale = 1.0f;
    [Tooltip("Duration of the scaling animation in seconds")]
    public float ScaleDuration = 0.5f;

    [Header("Rotation Animation Settings")]

    [Tooltip("Rotation speed around the Z axis ")]
    public float RotationSpeedZ = 20f;

    [Tooltip("If true, Flare 2 will rotate in the opposite direction of Flare 1")]
    public bool RotateOpposite = true;

    private bool _isScalingDone = false;

    private void OnEnable()
    {
        _isScalingDone = false;

        Vector3 initialScale = Vector3.one * StartScale;
        if (Flare1 != null) Flare1.localScale = initialScale;
        if (Flare2 != null) Flare2.localScale = initialScale;

        StartCoroutine(ScaleUpRoutine());
    }

    private IEnumerator ScaleUpRoutine()
    {
        float timeElapsed = 0f;
        Vector3 initialScale = Vector3.one * StartScale;
        Vector3 target = Vector3.one * TargetScale;

        while (timeElapsed < ScaleDuration)
        {
            timeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(timeElapsed / ScaleDuration);
            // Smooth step (Ease Out/In) for a nicer pop effect
            float smoothedT = t * t * (3f - 2f * t);

            if (Flare1 != null) Flare1.localScale = Vector3.Lerp(initialScale, target, smoothedT);
            if (Flare2 != null) Flare2.localScale = Vector3.Lerp(initialScale, target, smoothedT);

            yield return null;
        }

        if (Flare1 != null) Flare1.localScale = target;
        if (Flare2 != null) Flare2.localScale = target;

        _isScalingDone = true;
    }

    private void Update()
    {
        if (_isScalingDone)
        {

            float rotZ = RotationSpeedZ * Time.deltaTime;

            if (Flare1 != null)
            {
                Flare1.Rotate(0, 0, rotZ);
            }

            if (Flare2 != null)
            {
                if (RotateOpposite)
                    Flare2.Rotate(0, 0, -rotZ);
                else
                    Flare2.Rotate(0, 0, rotZ);
            }
        }
    }
}
