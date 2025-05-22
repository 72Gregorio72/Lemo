using UnityEngine;

public class spawnButton : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("HandHitbox"))
        {
            Debug.Log("Spawn button pressed!");
            // Trova lo script shootPaintball e chiama il metodo FirePaintball
            shootPaintball paintballShooter = Object.FindFirstObjectByType<shootPaintball>();
            if (paintballShooter != null)
            {
                paintballShooter.FirePaintball();
            }
            else
            {
                Debug.LogError("shootPaintball non trovato nella scena!");
            }
        }
    }
}
