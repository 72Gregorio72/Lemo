using UnityEngine;

public class PainterPistol : MonoBehaviour
{
    public GameObject paintballPrefab; // Prefab della palla di vernice
    public Transform spawnPoint; // Punto di spawn della palla

    private bool isShooting = false;

    public float strenght = 100f; // Forza di sparo
    public float fireRate = 0.1f; // Frequenza di sparo
    private float nextFireTime = 0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Controlla se è il momento di sparare
        if (isShooting && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate; // Aggiorna il tempo per il prossimo
            FirePaintball();
        }
    }

    public void StartShooting()
    {
        isShooting = true;
    }

    public void StopShooting()
    {
        isShooting = false;
    }

    private void FirePaintball()
    {
        GameObject paintball = Instantiate(paintballPrefab, spawnPoint.position, spawnPoint.rotation);
                
        // Aggiungi una forza alla palla per farla volare
        Rigidbody rb = paintball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(spawnPoint.forward * strenght); // Modifica la forza in base alle tue esigenze
        }
    }
}
