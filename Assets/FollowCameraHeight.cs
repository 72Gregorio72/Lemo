using UnityEngine;

public class FollowCameraHeight : MonoBehaviour
{
	public Vector3 offset = new Vector3(0, 0, 0); // Offset from the center point

	public GameObject center;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{
		if (center != null)
		{
			Vector3 newPosition = center.transform.position + offset;
			transform.position = newPosition;
		}
		else
		{
			Debug.LogWarning("Center GameObject is not assigned in FollowCameraHeight script.");
		}
    }
}
