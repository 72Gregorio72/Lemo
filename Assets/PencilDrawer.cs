using UnityEngine;

public class PencilDrawer : MonoBehaviour
{
    public Transform tip; // la punta della matita
    public float tipRadius = 0.01f;
    public LayerMask drawMask;

    private void Update()
    {
        RaycastHit hit;
        if (Physics.SphereCast(tip.position, tipRadius, tip.forward, out hit, 0.01f, drawMask))
        {
            WallPainter painter = hit.collider.GetComponent<WallPainter>();
            if (painter != null)
            {
                painter.DrawAt(hit.textureCoord);
            }
        }
    }
}
