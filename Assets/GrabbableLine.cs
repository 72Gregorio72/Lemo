using UnityEngine;

public class GrabbableLine : OVRGrabbable
{
    protected override void Start()
    {
        // Trova tutti i collider sullo stesso GameObject (es. il MeshCollider della linea)
        Collider[] grabPoints = GetComponents<Collider>();
        if (grabPoints.Length == 0)
        {
            Debug.LogError("GrabbableLine requires at least one Collider.");
        }

        this.m_grabPoints = grabPoints; // usa il campo protetto, non la property
        base.Start();
    }
}
