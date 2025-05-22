using UnityEngine;

public class shootPaintball : MonoBehaviour
{
    public GameObject paintballPrefab; // Prefab della palla di vernice
    public Transform spawnPoint; // Punto di spawn della palla
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void FirePaintball()
    {
        // Istanzia la palla di vernice
        GameObject paintball = Instantiate(paintballPrefab, spawnPoint.position, spawnPoint.rotation);
        
        // Aggiungi una forza alla palla per farla volare
        Rigidbody rb = paintball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(spawnPoint.forward * 10); // Modifica la forza in base alle tue esigenze
        }
    }
}
