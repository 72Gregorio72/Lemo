using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(OVRGrabbable))]
public class GrabListener : MonoBehaviour
{
    [Header("Eventi da chiamare")]
    public UnityEvent onGrab;
    public UnityEvent onRelease;

    private OVRGrabbable grabbable;
    private bool wasGrabbedLastFrame = false;

    void Start()
    {
        grabbable = GetComponent<OVRGrabbable>();
    }

    void Update()
    {
        bool isCurrentlyGrabbed = grabbable.isGrabbed;

        if (!wasGrabbedLastFrame && isCurrentlyGrabbed)
        {
            onGrab?.Invoke();
        }
        else if (wasGrabbedLastFrame && !isCurrentlyGrabbed)
        {
            onRelease?.Invoke();
        }

        wasGrabbedLastFrame = isCurrentlyGrabbed;
    }
}
