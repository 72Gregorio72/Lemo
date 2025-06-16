using UnityEngine;

// If Interactor is not defined in Meta.XR.Interaction, define a placeholder for compilation
#if !INTERACTOR_DEFINED
public class Interactor : MonoBehaviour { }
#endif

public interface IInteractable
{
    void Interact(Interactor interactor);
    void EndInteraction(Interactor interactor);
}

public class PencilGrabbable : MonoBehaviour, IInteractable
{
    private Transform originalParent;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        originalParent = transform.parent;
    }

    public void Interact(Interactor interactor)
    {
        Debug.Log("Grab started!");
        transform.SetParent(interactor.transform);
        rb.isKinematic = true;
    }

    public void EndInteraction(Interactor interactor)
    {
        Debug.Log("Grab released!");
        transform.SetParent(originalParent);
        rb.isKinematic = false;
    }
}
