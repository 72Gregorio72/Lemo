using UnityEngine;
using Oculus;

public class EraserPartial : MonoBehaviour
{
    public float eraseRadius = 0.01f;
    public OVRInput.Button eraseAllButton = OVRInput.Button.Two; // 'B' su Oculus

    private void OnTriggerStay(Collider other)
    {
        // Cerchiamo un LineRenderer nel collider o nei suoi parent
        LineRenderer lr = other.GetComponent<LineRenderer>();
        if (lr == null) lr = other.GetComponentInParent<LineRenderer>();

        if (lr == null) return;

        // Se il pulsante 'B' è premuto, cancella l'intera linea
        if (OVRInput.Get(eraseAllButton))
        {
            Destroy(lr.gameObject);
        }
    }
}
