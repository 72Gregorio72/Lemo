using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class XRButtonVisualFeedback : MonoBehaviour
{
    // Enum for button types to use in dropdown
    public enum XRButtonType
    {
        PrimaryButton,  // A or X
        SecondaryButton, // B or Y
        Trigger,
        Grip,
        Thumbstick,
        Menu
    }

    [System.Serializable]
    public class XRButton
    {
        public string buttonName; // Keep for display purposes
        [Tooltip("Button type to detect input for")]
        public XRButtonType buttonType = XRButtonType.PrimaryButton;
        
        public MeshRenderer meshRenderer;
        
        [Header("Colors")]
        [ColorUsage(false, true, 0f, 1f, 0f, 1f)]
        public Color normalColor = Color.white;
        
        [ColorUsage(false, true, 0f, 1f, 0f, 1f)]
        public Color pressedColor = Color.green;

        [Header("Image")]
        public bool useImage = false;
        public Sprite imageSprite;
        public Vector3 imageScale = Vector3.one;
        public Vector3 imagePosition = Vector3.zero;
        public Vector3 imageRotation = Vector3.zero;

        [HideInInspector] public Image imageComponent;
        [HideInInspector] public bool isPressed = false;
    }

    public XRButton[] buttons;
    
    [Header("Controller Settings")]
    public bool isLeftController = false;
    
    // Device references - explicitly use UnityEngine.XR.InputDevice
    private UnityEngine.XR.InputDevice targetDevice;
    private bool deviceConnected = false;

    void Start()
    {
        foreach (var btn in buttons)
        {
            if (btn.meshRenderer)
                btn.meshRenderer.material.color = btn.normalColor;

            if (btn.useImage && btn.imageSprite)
            {
                // Create Canvas - simple WorldSpace canvas as child of the button
                GameObject canvasObj = new GameObject($"{btn.buttonType}_Canvas");
                canvasObj.transform.SetParent(btn.meshRenderer.transform);
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                
                // Set default position and scale
                canvas.transform.localPosition = new Vector3(0, 0.001f, 0);
                canvas.transform.localRotation = Quaternion.identity;
                canvas.transform.localScale = Vector3.one;

                // Create Image - basic centered image
                GameObject imgObj = new GameObject($"{btn.buttonType}_Image");
                imgObj.transform.SetParent(canvasObj.transform, false);
                btn.imageComponent = imgObj.AddComponent<Image>();
                btn.imageComponent.sprite = btn.imageSprite;
                btn.imageComponent.preserveAspect = true;
                
                // Setup RectTransform
                RectTransform rectTransform = imgObj.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(1, 1);
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                
                // Apply the scale from inspector
                rectTransform.localScale = btn.imageScale;
                rectTransform.localPosition = btn.imagePosition;
                rectTransform.localRotation = Quaternion.Euler(btn.imageRotation);

                // Set initial color
                btn.imageComponent.color = btn.normalColor;
            }
        }
    }
    
    void Update()
    {
        if (!deviceConnected || !targetDevice.isValid)
        {
            TryInitializeDevice();
            return;
        }
        
        UpdateButtonStates();
    }
    
    private void TryInitializeDevice()
    {
        // Define characteristics for the controller
        InputDeviceCharacteristics controllerCharacteristics = InputDeviceCharacteristics.Controller;
        
        // Add left or right characteristic
        if (isLeftController)
            controllerCharacteristics |= InputDeviceCharacteristics.Left;
        else
            controllerCharacteristics |= InputDeviceCharacteristics.Right;
            
        // Find all devices with these characteristics
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerCharacteristics, devices);
        
        // If a matching controller is found, use it
        if (devices.Count > 0)
        {
            targetDevice = devices[0];
            deviceConnected = true;
            Debug.Log($"Connected to {(isLeftController ? "left" : "right")} controller");
        }
    }
    
    private void UpdateButtonStates()
    {
        foreach (var button in buttons)
        {
            bool isCurrentlyPressed = false;
            
            // Check button states based on dropdown selection
            switch (button.buttonType)
            {
                case XRButtonType.PrimaryButton:
                    targetDevice.TryGetFeatureValue(CommonUsages.primaryButton, out isCurrentlyPressed);
                    break;
                    
                case XRButtonType.SecondaryButton:
                    targetDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out isCurrentlyPressed);
                    break;
                    
                case XRButtonType.Trigger:
                    float triggerValue = 0f;
                    if (targetDevice.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
                        isCurrentlyPressed = triggerValue > 0.5f; // Pressed when more than halfway
                    break;
                    
                case XRButtonType.Grip:
                    float gripValue = 0f;
                    if (targetDevice.TryGetFeatureValue(CommonUsages.grip, out gripValue))
                        isCurrentlyPressed = gripValue > 0.5f; // Pressed when more than halfway
                    break;
                    
                case XRButtonType.Thumbstick:
                    targetDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out isCurrentlyPressed);
                    break;
                    
                case XRButtonType.Menu:
                    targetDevice.TryGetFeatureValue(CommonUsages.menuButton, out isCurrentlyPressed);
                    break;
            }
            
            // Update button visual state only if state changed
            if (isCurrentlyPressed != button.isPressed)
            {
                button.isPressed = isCurrentlyPressed;
                UpdateButtonVisual(button);
            }
        }
    }

    private void UpdateButtonVisual(XRButton button)
    {
        // Update mesh color
        if (button.meshRenderer)
        {
            button.meshRenderer.material.color = button.isPressed ? button.pressedColor : button.normalColor;
        }
        
        // Update image color and visibility
        if (button.useImage && button.imageComponent != null)
        {
            button.imageComponent.color = button.isPressed ? button.pressedColor : button.normalColor;
            
            // Hide image when pressed, show when released
            button.imageComponent.enabled = !button.isPressed;
        }
    }
} 