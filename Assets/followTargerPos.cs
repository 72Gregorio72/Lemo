using UnityEngine;

public class followTargerPos : MonoBehaviour
{
    public Transform trackingTarget; // Oggetto da seguire
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (trackingTarget != null)
        {
            // Aggiorna la posizione dell'oggetto corrente per seguire il target
            transform.position = trackingTarget.position;
            // Se vuoi mantenere la rotazione, puoi anche aggiungere:
            // transform.rotation = trackingTarget.rotation;
        }
        else
        {
            Debug.LogWarning("Tracking target is not assigned.");
        }
    }
}
