using UnityEngine;
using UnityEngine.Rendering.Universal; // Per DecalProjector

public class PaintBallBehavior : MonoBehaviour
{
    public GameObject splashEffectPrefab; // Prefab con DecalProjector

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            ContactPoint contact = collision.contacts[0];

            // Istanzia decal alla collisione
            GameObject decal = Instantiate(
                splashEffectPrefab,
                contact.point + contact.normal * 0.01f,
                Quaternion.LookRotation(-contact.normal)
            );

            // Prendi colore corrente della paintball
            Color paintColor = GetComponent<Renderer>().material.color;

            // Prendi il decal projector
            DecalProjector projector = decal.GetComponent<DecalProjector>();
            if (projector != null && this.tag != "TexturePaintBall")
            {
                // 🔥 CREA nuova istanza del materiale
                Material decalMatInstance = new Material(projector.material);
                decalMatInstance.color = paintColor;

                // ASSEGNA nuova istanza
                projector.material = decalMatInstance;
            }

            if (projector != null && this.tag == "TexturePaintBall")
            {
                // 🔥 CREA nuova istanza del materiale
                Material decalMatInstance = new Material(projector.material);
                Texture tex = GetComponent<Renderer>().material.GetTexture("_BaseMap");
                decalMatInstance.SetTexture("_BaseMap", tex);

                // ASSEGNA nuova istanza
                projector.material = decalMatInstance;
            }

            Destroy(gameObject);
        }
    }
}
