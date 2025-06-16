using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class LevitateOnIdle : MonoBehaviour
{
    public float amplitude = 0.25f;         // How far it moves up and down
    public float frequency = 1f;            // How fast it moves
    public float massMultiplierOnGrab = 3f; // Mass multiplier when grabbed

    private Rigidbody rb;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private float originalMass;
    private Vector3 startPosition;
    private float timeOffset;

    private GameObject spawnPositionObject;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        originalMass = rb.mass;
        //startPosition = transform.position;
        //timeOffset = Random.Range(0f, 2f * Mathf.PI); // Stagger bobbing

        grabInteractable.selectEntered.AddListener(OnGrab);

        spawnPositionObject = GameObject.FindGameObjectWithTag("SpawnBalls");
        //grabInteractable.selectExited.AddListener(OnRelease);

        // Start levitating
        rb.isKinematic = true;
        rb.useGravity = false;
    }

   void FixedUpdate()
   {
       if (rb.isKinematic)
       {
           float newY = spawnPositionObject.transform.position.y + Mathf.Sin((Time.time + timeOffset) * frequency) * amplitude;
           Vector3 newPosition = new Vector3(spawnPositionObject.transform.position.x, newY, spawnPositionObject.transform.position.z);
           rb.MovePosition(newPosition);
       }
   }

    private void OnGrab(SelectEnterEventArgs args)
    {
        Debug.Log("<color=red>Grabbed: " + gameObject.name + "</color>");
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = originalMass * massMultiplierOnGrab;
        SpawnNewBall();
    }

    private void SpawnNewBall()
    {
        Instantiate(gameObject, spawnPositionObject.transform.position, Quaternion.identity);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        startPosition = transform.position; // Reset float origin
        timeOffset = Random.Range(0f, 2f * Mathf.PI);
        rb.mass = originalMass;
    }

    void OnDestroy()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        //grabInteractable.selectExited.RemoveListener(OnRelease);
    }
}
