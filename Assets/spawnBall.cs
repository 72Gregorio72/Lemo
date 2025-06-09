using UnityEngine;

public class spawnBall : MonoBehaviour
{
    public GameObject ballPrefab; // Prefab del pallone da spawnare
    public Transform spawnPoint; // Punto di spawn del pallone
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SpawnBallInHand()
    {
        Debug.Log("Spawn ball in hand!");
    }

    public void SpawnBallInWorld()
    {
        Debug.Log("Spawn ball in world!");
        GameObject ball = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
        ball.transform.SetParent(transform); // Set the parent to the current object
        ball.GetComponent<Rigidbody>().isKinematic = true; // Ensure the ball is not kinematic
    }
}
