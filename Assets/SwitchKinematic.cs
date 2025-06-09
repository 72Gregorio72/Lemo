using UnityEngine;
#if UNITY_XR_INTERACTION_TOOLKIT
using UnityEngine.XR.Interaction.Toolkit;
#if UNITY_XR_INTERACTION_TOOLKIT
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
#endif
public class SwitchKinematic : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrab);
    }

    void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        rb.isKinematic = false;
    }
}
#endif // UNITY_XR_INTERACTION_TOOLKIT