using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using DG.Tweening;

public class XRSceneInputManager : MonoBehaviour
{
    [Header("Button Events")]
    public UnityEvent OnPrimaryButtonPressed;
    public UnityEvent OnPrimaryButtonReleased;
    
    public UnityEvent OnSecondaryButtonPressed;
    public UnityEvent OnSecondaryButtonReleased;
    
    public UnityEvent OnGripButtonPressed;
    public UnityEvent OnGripButtonReleased;

    [Header("Return To Menu Settings")]
    [SerializeField] private GameObject returnToMenuPrefab;
    [SerializeField] private Transform xrOrigin; // Reference to XR Origin transform
    [SerializeField] private float spawnDistance = 2f; // Distance in meters from XR Origin
    [SerializeField] private float spawnHeight = 0f; // Vertical offset from camera forward

    private List<InputDevice> devices = new();
    private Dictionary<InputDevice, bool> previousPrimaryStates = new();
    private Dictionary<InputDevice, bool> previousSecondaryStates = new();
    private Dictionary<InputDevice, bool> previousGripStates = new();
    private Dictionary<InputDevice, bool> previousTriggerStates = new();

    void Start()
    {
        InputDevices.GetDevices(devices);
    }

    void Update()
    {
        InputDevices.GetDevices(devices);

        foreach (var device in devices)
        {
            // PRIMARY BUTTON
            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool isPrimaryHeld))
            {
                previousPrimaryStates.TryGetValue(device, out bool wasPrimaryHeld);

                if (isPrimaryHeld && !wasPrimaryHeld)
                {
                    OnPrimaryButtonPressed?.Invoke();
                }
                else if (!isPrimaryHeld && wasPrimaryHeld)
                {
                    OnPrimaryButtonReleased?.Invoke();
                }

                previousPrimaryStates[device] = isPrimaryHeld;
            }

            // SECONDARY BUTTON
            if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool isSecondaryHeld))
            {
                previousSecondaryStates.TryGetValue(device, out bool wasSecondaryHeld);

                if (isSecondaryHeld && !wasSecondaryHeld)
                {
                    OnSecondaryButtonPressed?.Invoke();
                }
                else if (!isSecondaryHeld && wasSecondaryHeld)
                {
                    OnSecondaryButtonReleased?.Invoke();
                }

                previousSecondaryStates[device] = isSecondaryHeld;
            }

            // GRIP BUTTON
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool isGripHeld))
            {
                previousGripStates.TryGetValue(device, out bool wasGripHeld);

                if (isGripHeld && !wasGripHeld)
                {
                    OnGripButtonPressed?.Invoke();
                }
                else if (!isGripHeld && wasGripHeld)
                {
                    OnGripButtonReleased?.Invoke();
                }

                previousGripStates[device] = isGripHeld;
            }

            // TRIGGER BUTTON
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool isTriggerHeld))
            {
                previousTriggerStates.TryGetValue(device, out bool wasTriggerHeld);

                if (isTriggerHeld && !wasTriggerHeld)
                {
                    SpawnReturnToMenuPrefab();
                }

                previousTriggerStates[device] = isTriggerHeld;
            }
        }
    }

    private void SpawnReturnToMenuPrefab()
    {
        if (returnToMenuPrefab == null)
        {
            Debug.LogError("[SpawnReturnToMenuPrefab] Return to menu prefab is not assigned!");
            return;
        }

        if (xrOrigin == null)
        {
            Debug.LogError("[SpawnReturnToMenuPrefab] XR Origin reference is missing!");
            return;
        }

        // Find the camera (assuming it's a child of XR Origin)
        var mainCamera = xrOrigin.GetComponentInChildren<Camera>();
        if (mainCamera == null)
        {
            Debug.LogError("[SpawnReturnToMenuPrefab] Cannot find camera in XR Origin!");
            return;
        }

        // Calculate spawn position
        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0; // Zero out vertical component for consistent height
        forward.Normalize();

        // Calculate position: camera position + forward direction * distance + height offset
        Vector3 spawnPosition = mainCamera.transform.position + forward * spawnDistance;
        spawnPosition.y += spawnHeight; // Add height offset

        // Instantiate the return to menu prefab with identity rotation (0,0,0)
        GameObject menuInstance = Instantiate(returnToMenuPrefab);
        
        // Set position and keep original rotation
        menuInstance.transform.position = spawnPosition;
        menuInstance.transform.rotation = Quaternion.identity;

        // Optional: make the menu face the player
        menuInstance.transform.LookAt(new Vector3(mainCamera.transform.position.x, menuInstance.transform.position.y, mainCamera.transform.position.z));
        menuInstance.transform.Rotate(0, 180, 0); // Rotate 180 degrees to face the player
    }

    // Optional: Add method to visualize the spawn position in the editor
    private void OnDrawGizmosSelected()
    {
        if (xrOrigin != null)
        {
            var mainCamera = xrOrigin.GetComponentInChildren<Camera>();
            if (mainCamera != null)
            {
                Vector3 forward = mainCamera.transform.forward;
                forward.y = 0;
                forward.Normalize();
                
                Vector3 spawnPosition = mainCamera.transform.position + forward * spawnDistance;
                spawnPosition.y += spawnHeight;
                
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(spawnPosition, 0.2f);
                Gizmos.DrawLine(mainCamera.transform.position, spawnPosition);
            }
        }
    }
} 