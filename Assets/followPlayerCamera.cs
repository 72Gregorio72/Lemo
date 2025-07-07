using UnityEngine;

public class FollowPlayerView : MonoBehaviour
{
    public Transform playerCamera; // la camera VR del player (es. XR Rig camera)
    public float distanceFromCamera = 2f; // distanza del canvas davanti alla vista
    public float heightOffset = 0f; // offset verticale, se vuoi tenerlo un po' più su o più giù
    public float followSpeed = 5f; // velocità di aggiornamento posizione/rotazione

    void LateUpdate()
    {
        if (playerCamera == null) return;

        Vector3 targetPosition = playerCamera.position + playerCamera.forward * distanceFromCamera;
        targetPosition.y += heightOffset;

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);

        Quaternion targetRotation = Quaternion.LookRotation(transform.position - playerCamera.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followSpeed);
    }
}
