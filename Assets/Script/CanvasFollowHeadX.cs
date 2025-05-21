using UnityEngine;
using UnityEngine.XR;

public class CanvasFollowHeadX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform xrCamera; // Your XR head/camera (usually Main Camera under XR Origin)

    [Header("Settings")]
    [SerializeField] private float xOffsetThreshold = 0.5f; // Distance before the canvas moves
    [SerializeField] private float followSpeed = 2f;        // Smooth follow speed

    private void LateUpdate()
    {
        if (xrCamera == null) return;

        Vector3 canvasPosition = transform.position;
        Vector3 headPosition = xrCamera.position;

        float deltaX = headPosition.x - canvasPosition.x;

        // Only follow if head is outside threshold
        if (Mathf.Abs(deltaX) > xOffsetThreshold)
        {
            float targetX = Mathf.Lerp(canvasPosition.x, headPosition.x, Time.deltaTime * followSpeed);
            transform.position = new Vector3(targetX, canvasPosition.y, canvasPosition.z);
        }
    }
}
