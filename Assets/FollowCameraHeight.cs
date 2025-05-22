using UnityEngine;

public class FollowCameraHeight : MonoBehaviour
{
    public GameObject cameraObject; // The camera object to follow

    public GameObject currentRow;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (cameraObject == null)
        {
            cameraObject = Camera.main.gameObject; // Assign the main camera if not set
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (currentRow.tag == "UpperRow")
        {
            this.transform.position = new Vector3(this.transform.position.x, cameraObject.transform.position.y + 2f, this.transform.position.z);
        }
        else if (currentRow.tag == "LowerRow")
        {
            this.transform.position = new Vector3(this.transform.position.x, cameraObject.transform.position.y - 2f, this.transform.position.z);
        }
        else if (currentRow.tag == "MiddleRow")
        {
            this.transform.position = new Vector3(this.transform.position.x, cameraObject.transform.position.y, this.transform.position.z);
        }
    }
}
