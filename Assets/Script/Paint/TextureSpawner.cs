using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;

public class PrefabSpawnerOnGrab : MonoBehaviour
{
    [Header("Prefab Settings")]
    public GameObject prefabToSpawn;
    public float moveSpeed = 1f;

    private Transform spawnPoint;
    private Transform endPoint;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    void Start()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnGrab);

        // Find spawn and end point by name in the scene
        GameObject spawnerObj = GameObject.Find("Texture Ball Spawner");
        GameObject endPointObj = GameObject.Find("Texture Ball End-Point");

        if (spawnerObj == null || endPointObj == null)
        {
            Debug.LogError("Spawn or End point not found in scene. Check object names!");
            return;
        }

        spawnPoint = spawnerObj.transform;
        endPoint = endPointObj.transform;
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        StartCoroutine(SpawnAndMovePrefab());
    }

    IEnumerator SpawnAndMovePrefab()
    {
        if (prefabToSpawn == null || spawnPoint == null || endPoint == null)
            yield break;

        GameObject spawned = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);

        LevitateOnIdle levitateScript = spawned.GetComponent<LevitateOnIdle>();
        if (levitateScript != null)
            levitateScript.enabled = false;

        // Move toward end point
        while (Vector3.Distance(spawned.transform.position, endPoint.position) > 0.01f)
        {
            spawned.transform.position = Vector3.MoveTowards(
                spawned.transform.position,
                endPoint.position,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (levitateScript != null)
            levitateScript.enabled = true;
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.RemoveListener(OnGrab);
    }
}
