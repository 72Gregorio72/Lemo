using System;
using UnityEngine;
using UnityEngine.VFX;

public class HandPositionDebugger : MonoBehaviour
{
    public enum HandSelection
    {
        Both,
        Left,
        Right
    }

    [Header("VFX Settings")]
    public VisualEffect vfx;
    

    [Header("References")]
    public Transform headTransform;
    public Transform leftHandTransform;
    public Transform rightHandTransform;

    [Header("Options")]
    public HandSelection handSelection = HandSelection.Both;

    [Header("Height Settings")]
    private float maxHeightAboveHead = 3f;
    private float multiplier = 8f;

    
    private const string HEIGHT_PROPERTY = "Height";
    private const string TURBULENCE_PROPERTY = "Turbulence Intensity Range";

    // Internal smooth values
    private float currentHeightValue = 0f;
    private Vector2 currentTurbulence = new Vector2(-2f, 2f);

    void Start()
    {
        if (headTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                headTransform = mainCam.transform;
        }
    }

    void Update()
{
    float leftHeight = GetHandHeight(leftHandTransform);
    float rightHeight = GetHandHeight(rightHandTransform);

    float targetHeight = 0f;

    if (handSelection == HandSelection.Both)
        targetHeight = Mathf.Max(leftHeight, rightHeight);
    else if (handSelection == HandSelection.Left)
        targetHeight = leftHeight;
    else if (handSelection == HandSelection.Right)
        targetHeight = rightHeight;

    // Smoothly interpolate VFX height
    currentHeightValue = Mathf.MoveTowards(currentHeightValue, targetHeight, Time.deltaTime * 6f);
    vfx.SetFloat(HEIGHT_PROPERTY, currentHeightValue);

    // Interpolate turbulence based on hand height
    float t = currentHeightValue / maxHeightAboveHead; // normalized [0,1]
    Vector2 targetTurbulence = Vector2.Lerp(new Vector2(-2f, 2f), new Vector2(2f, 6f), t);
    currentTurbulence = Vector2.Lerp(currentTurbulence, targetTurbulence, Time.deltaTime * 3f);
    vfx.SetVector2(TURBULENCE_PROPERTY, currentTurbulence);

    // ---- DEBUG LOGGING ----
    Debug.Log($"[Hand Heights] Left: {leftHeight:F2}, Right: {rightHeight:F2}");
    Debug.Log($"[VFX Height Property] Target: {targetHeight:F2}, Smoothed: {currentHeightValue:F2}");
    Debug.Log($"[Turbulence] Target: {targetTurbulence}, Smoothed: {currentTurbulence}");
}


    float GetHandHeight(Transform handTransform)
{
    if (handTransform == null || headTransform == null)
        return 0f;

    float verticalDistance = (handTransform.position.y - headTransform.position.y) * multiplier; // Calculate height difference
    return Mathf.Max(0f, verticalDistance); // Allow values > 3 if hands are high
}

}
