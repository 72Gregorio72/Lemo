using System;
using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class CheckHitbox : MonoBehaviour
{
    private MemoryGameManager gameManager;
    
    [Header("Haptic Feedback Settings")]
    [Tooltip("Haptic intensity for card collision (0.0 to 1.0)")]
    [Range(0f, 1f)]
    public float hapticAmplitude = 0.3f;
    
    [Tooltip("Haptic duration in seconds")]
    [Range(0.05f, 1f)]
    public float hapticDuration = 0.1f;
    
    [Tooltip("Haptic frequency (0 for default)")]
    [Range(0f, 320f)]
    public float hapticFrequency = 0f;
    
    // Cache for input devices
    private static List<InputDevice> inputDevices = new List<InputDevice>();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameObject gameManagerObject = GameObject.FindGameObjectWithTag("GameManager");
        if (gameManagerObject != null)
        {
            gameManager = gameManagerObject.GetComponent<MemoryGameManager>();
        }
        if (gameManager == null)
        {
            Debug.LogError("MemoryGameManager non trovato nella scena!");
            return;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger: " + other.name);
        if (other.CompareTag("HandHitbox"))
        {
            // Trigger haptic feedback on the appropriate controller
            TriggerHapticFeedback(other);
            
            if (!this.gameObject.GetComponent<MemoryCard>().IsPermanentlyRevealed)
            {
                gameManager.CardSelected(this.gameObject);
            }
            else
            {
                Debug.Log("Carta già rivelata!");
            } 
        }
    }
    
    /// <summary>
    /// Triggers haptic feedback on the appropriate controller based on the colliding hand hitbox
    /// </summary>
    /// <param name="handHitbox">The hand hitbox collider that triggered the collision</param>
    private void TriggerHapticFeedback(Collider handHitbox)
    {
        try
        {
            // Determine which controller based on the hitbox name/parent
            bool isLeftHand = IsLeftHandHitbox(handHitbox);
            
            // Get the appropriate input device
            InputDevice targetDevice = GetControllerDevice(isLeftHand);
            
            if (targetDevice.isValid)
            {
                // Create haptic impulse
                HapticCapabilities capabilities;
                if (targetDevice.TryGetHapticCapabilities(out capabilities))
                {
                    if (capabilities.supportsImpulse)
                    {
                        // Send haptic impulse
                        targetDevice.SendHapticImpulse(0, hapticAmplitude, hapticDuration);
                        Debug.Log($"Haptic feedback triggered for {(isLeftHand ? "left" : "right")} controller");
                    }
                    else
                    {
                        Debug.LogWarning($"Controller {(isLeftHand ? "left" : "right")} does not support haptic impulse");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Could not find valid {(isLeftHand ? "left" : "right")} controller device");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error triggering haptic feedback: {e.Message}");
        }
    }
    
    /// <summary>
    /// Determines if the hitbox belongs to the left hand based on name or parent hierarchy
    /// </summary>
    /// <param name="hitbox">The hitbox collider</param>
    /// <returns>True if it's a left hand hitbox, false otherwise</returns>
    private bool IsLeftHandHitbox(Collider hitbox)
    {
        // Check the hitbox name first
        string hitboxName = hitbox.name.ToLower();
        if (hitboxName.Contains("left"))
            return true;
        if (hitboxName.Contains("right"))
            return false;
            
        // Check parent names if the hitbox name doesn't contain directional info
        Transform current = hitbox.transform;
        while (current != null)
        {
            string parentName = current.name.ToLower();
            if (parentName.Contains("left"))
                return true;
            if (parentName.Contains("right"))
                return false;
            current = current.parent;
        }
        
        // Default to right hand if we can't determine
        Debug.LogWarning($"Could not determine hand direction for hitbox: {hitbox.name}, defaulting to right hand");
        return false;
    }
    
    /// <summary>
    /// Gets the input device for the specified controller
    /// </summary>
    /// <param name="isLeftHand">True for left controller, false for right controller</param>
    /// <returns>The InputDevice for the controller</returns>
    private InputDevice GetControllerDevice(bool isLeftHand)
    {
        // Refresh the device list
        InputDevices.GetDevices(inputDevices);
        
        // Define characteristics we're looking for
        InputDeviceCharacteristics targetCharacteristics = InputDeviceCharacteristics.Controller;
        targetCharacteristics |= isLeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
        
        // Find the matching device
        foreach (var device in inputDevices)
        {
            if ((device.characteristics & targetCharacteristics) == targetCharacteristics)
            {
                return device;
            }
        }
        
        // Fallback: try using XRNode
        XRNode targetNode = isLeftHand ? XRNode.LeftHand : XRNode.RightHand;
        return InputDevices.GetDeviceAtXRNode(targetNode);
    }
}
