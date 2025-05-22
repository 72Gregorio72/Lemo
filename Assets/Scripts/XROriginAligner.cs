using UnityEngine;

public class XROriginAligner : MonoBehaviour
{
    [Header("Reference to the XR Origin or Player Object")]
    public Transform xrOrigin;

    [Header("Desired Look Target")]
    public Transform lookTarget;

    private void Start()
    {
        if (xrOrigin == null)
        {
            Debug.LogError("XROriginAligner: XR Origin is not assigned.");
            return;
        }

        if (lookTarget == null)
        {
            Debug.LogError("XROriginAligner: Look Target is not assigned.");
            return;
        }

        AlignXROrigin();
    }

    private void AlignXROrigin()
    {
        Vector3 directionToTarget = lookTarget.position - xrOrigin.position;
        directionToTarget.y = 0; // Keep only horizontal rotation

        if (directionToTarget != Vector3.zero)
        {
            xrOrigin.rotation = Quaternion.LookRotation(directionToTarget);
        }
    }
}
