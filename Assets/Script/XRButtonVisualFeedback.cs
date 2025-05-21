using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class XRButtonVisualFeedback : MonoBehaviour
{
    [System.Serializable]
    public class ButtonVisuals
    {
        public string buttonName;
        public MeshRenderer buttonMeshRenderer;
        public Color normalColor = Color.white;
        public Color hoveredColor = new Color(0.5f, 0.8f, 1f, 1f);
        public Color pressedColor = new Color(0.3f, 0.6f, 1f, 1f);
        public Material originalMaterial;
        public Material outlineMaterial;
    }

    [Header("Button Setup")]
    public ButtonVisuals[] buttons;
    
    [Header("Controller Settings")]
    public bool isLeftController;
    
    private Dictionary<string, bool> previousButtonStates = new Dictionary<string, bool>();
    private InputDevice targetDevice;
    private bool deviceInitialized = false;

    private void Start()
    {
        // Store original materials and create outline materials
        foreach (var button in buttons)
        {
            button.originalMaterial = button.buttonMeshRenderer.material;
            
            // Create outline material using URP Lit shader
            button.outlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (button.outlineMaterial.shader == null)
            {
                // Fallback to URP Simple Lit if Lit is not found
                button.outlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            }
            
            // Configure the material for transparency
            button.outlineMaterial.SetFloat("_Surface", 1); // 0 = opaque, 1 = transparent
            button.outlineMaterial.SetFloat("_Blend", 0); // 0 = alpha, 1 = premultiply
            button.outlineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            button.outlineMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            button.outlineMaterial.renderQueue = 3000;
            
            // Set material properties
            button.outlineMaterial.SetFloat("_Smoothness", 0.5f);
            button.outlineMaterial.SetFloat("_Metallic", 0.0f);
        }
    }

    private void Update()
    {
        if (!deviceInitialized)
        {
            InitializeDevice();
            return;
        }

        if (!targetDevice.isValid)
        {
            deviceInitialized = false;
            return;
        }

        UpdateButtonVisuals();
    }

    private void InitializeDevice()
    {
        var characteristics = InputDeviceCharacteristics.Controller;
        characteristics |= isLeftController ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
        
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

        if (devices.Count > 0)
        {
            targetDevice = devices[0];
            deviceInitialized = true;
        }
    }

    private void UpdateButtonVisuals()
    {
        foreach (var button in buttons)
        {
            bool isPressed = false;
            bool wasPressed = false;
            previousButtonStates.TryGetValue(button.buttonName, out wasPressed);

            // Check button state based on button name
            switch (button.buttonName.ToLower())
            {
                case "button a":
                case "button x":
                    targetDevice.TryGetFeatureValue(CommonUsages.primaryButton, out isPressed);
                    break;
                case "button b":
                case "button y":
                    targetDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out isPressed);
                    break;
                case "trigger":
                    targetDevice.TryGetFeatureValue(CommonUsages.triggerButton, out isPressed);
                    break;
                case "grip":
                    targetDevice.TryGetFeatureValue(CommonUsages.gripButton, out isPressed);
                    break;
                case "bumper":
                    float triggerValue = 0f;
                    if (targetDevice.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
                    {
                        isPressed = triggerValue > 0.1f;
                    }
                    break;
                default:
                    Debug.LogWarning($"Button name '{button.buttonName}' not recognized. Please use: 'Button A', 'Button B', 'Button X', 'Button Y', 'Trigger', 'Grip', or 'Bumper'");
                    break;
            }

            // Debug information
            if (isPressed)
            {
                Debug.Log($"Button {button.buttonName} is pressed!");
            }

            // Update button visuals
            if (isPressed)
            {
                ApplyButtonEffect(button, button.pressedColor);
            }
            else if (wasPressed)
            {
                ApplyButtonEffect(button, button.hoveredColor);
            }
            else
            {
                ApplyButtonEffect(button, button.normalColor);
            }

            previousButtonStates[button.buttonName] = isPressed;
        }
    }

    private void ApplyButtonEffect(ButtonVisuals button, Color color)
    {
        // Set the original material with the specified color
        Material[] materials = button.buttonMeshRenderer.materials;
        materials[0] = button.originalMaterial;
        materials[0].color = color;

        // Add outline material
        if (materials.Length < 2)
        {
            System.Array.Resize(ref materials, 2);
        }
        materials[1] = button.outlineMaterial;
        materials[1].color = new Color(color.r, color.g, color.b, 0.5f);
        
        button.buttonMeshRenderer.materials = materials;
    }

    private void OnDestroy()
    {
        // Clean up materials
        foreach (var button in buttons)
        {
            if (button.outlineMaterial != null)
            {
                Destroy(button.outlineMaterial);
            }
        }
    }
} 