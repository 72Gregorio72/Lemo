using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class XROriginSpawnAtPosition : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;

    void Start()
    {
        MoveToSpawnPoint();
    }

    private void MoveToSpawnPoint()
    {
        transform.position = spawnPosition;
    }
}
