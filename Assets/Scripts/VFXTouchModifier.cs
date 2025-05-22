using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

public class VFXTouchModifier : MonoBehaviour
{
    public VisualEffect vfx;

    private XRHandVelocityTracker velocityTracker;

    [Header("Base Values")]
    public float baseIntensity = 0.5f;
    public float baseDrag = 0.2f;
    public float baseFrequency = 1.0f;

    [Header("Speed to Step Mapping")]
    public float minSpeed = 0.0f;
    public float maxSpeed = 2.0f;
    public float speedMultiplier = 1.0f;
    public float maxIntensityStep = 1.0f;
    public float maxDragStep = 0.5f;
    public float maxFrequencyStep = 1.0f;

    [Header("Return Timing")]
    public float returnDuration = 2.0f;

    private float currentIntensity;
    private float currentDrag;
    private float currentFrequency;

    private Coroutine returnCoroutine;

    void Start()
    {
        // Auto-find the velocity tracker in the scene
        velocityTracker = FindFirstObjectByType<XRHandVelocityTracker>();

        if (velocityTracker == null)
        {
            Debug.LogError("XRHandVelocityTracker not found in scene! VFXTouchModifier will not work.");
        }

        currentIntensity = baseIntensity;
        currentDrag = baseDrag;
        currentFrequency = baseFrequency;

        ApplyValuesToVFX();
    }

    void OnTriggerEnter(Collider other)
    {
        if (velocityTracker == null || !other.CompareTag("Hand"))
            return;

        float speed = 0f;

        // Use name to determine left vs right hand
        string name = other.name.ToLower();

        if (name.Contains("left"))
        {
            speed = velocityTracker.LeftControllerVelocity.magnitude * speedMultiplier;
            Debug.Log($"LEFT hand touched. Speed: {speed:F2} m/s");
        }
        else if (name.Contains("right"))
        {
            speed = velocityTracker.RightControllerVelocity.magnitude * speedMultiplier;
            Debug.Log($"RIGHT hand touched. Speed: {speed:F2} m/s");
        }
        else
        {
            Debug.LogWarning("Hand object name does not indicate left or right.");
        }

        IncreaseValuesBasedOnSpeed(speed);
    }

    void IncreaseValuesBasedOnSpeed(float speed)
    {
        float speed01 = Mathf.InverseLerp(minSpeed, maxSpeed, speed);

        float intensityStep = maxIntensityStep * speed01;
        float dragStep = maxDragStep * speed01;
        float frequencyStep = maxFrequencyStep * speed01;

        currentIntensity += intensityStep;
        currentDrag += dragStep;
        currentFrequency += frequencyStep;

        ApplyValuesToVFX();

        if (returnCoroutine != null)
            StopCoroutine(returnCoroutine);

        returnCoroutine = StartCoroutine(SmoothReturnToBase());
    }

    void ApplyValuesToVFX()
    {
        vfx.SetFloat("Turbulence intensity", currentIntensity);
        vfx.SetFloat("Turbulence drag", currentDrag);
        vfx.SetFloat("Turbulence frequency", currentFrequency);
    }

    IEnumerator SmoothReturnToBase()
    {
        float t = 0f;
        float startIntensity = currentIntensity;
        float startDrag = currentDrag;
        float startFrequency = currentFrequency;

        while (t < returnDuration)
        {
            t += Time.deltaTime;
            float lerpT = t / returnDuration;

            currentIntensity = Mathf.Lerp(startIntensity, baseIntensity, lerpT);
            currentDrag = Mathf.Lerp(startDrag, baseDrag, lerpT);
            currentFrequency = Mathf.Lerp(startFrequency, baseFrequency, lerpT);

            ApplyValuesToVFX();
            yield return null;
        }

        currentIntensity = baseIntensity;
        currentDrag = baseDrag;
        currentFrequency = baseFrequency;
        ApplyValuesToVFX();

        returnCoroutine = null;
    }
}
