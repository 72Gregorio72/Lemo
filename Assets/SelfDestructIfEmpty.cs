using UnityEngine;
using System.Collections;

public class SelfDestructIfEmpty : MonoBehaviour
{
    public float checkDelay = 0.1f;
    public float destroyDelay = 1f;

    private LineRenderer line;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        InvokeRepeating(nameof(CheckAndDestroy), checkDelay, checkDelay);
    }

    void CheckAndDestroy()
    {
        if (line == null || line.positionCount == 0)
        {
            CancelInvoke(nameof(CheckAndDestroy));

            // Disabilita tutti i collider per evitare che OVRGrabber li tocchi
            foreach (var col in GetComponents<Collider>())
            {
                if (col != null)
                    col.enabled = false;
            }

            // Disattiva la linea
            if (line != null)
                line.enabled = false;

            // Avvia la distruzione nel frame successivo (per evitare race condition con OVRGrabber)
            StartCoroutine(DelayedDestroy());
        }
    }

    IEnumerator DelayedDestroy()
    {
        yield return null; // aspetta un frame intero
        Destroy(gameObject, 10f); // ora è sicuro
    }
}
