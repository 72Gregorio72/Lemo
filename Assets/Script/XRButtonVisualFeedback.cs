using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class XRButtonVisualFeedback : MonoBehaviour
{
    [System.Serializable]
    public class ButtonVisuals
    {
        public string buttonName;
        public MeshRenderer buttonMeshRenderer;
        [Header("Button Image")]
        public Sprite buttonIcon;
        [Header("Image Settings")]
        public bool useImage = true;
        public Vector3 imagePosition = new Vector3(0, 0.005f, 0);
        public Vector3 imageRotation = new Vector3(90, 0, 0);
        public Vector3 imageScale = new Vector3(1f, 1f, 1f);
        public float imageSize = 0.02f; // Overall size in world units
        [Header("Colors")]
        public Color normalColor = Color.white;
        public Color hoveredColor = new Color(0.5f, 0.8f, 1f, 1f);
        public Color pressedColor = new Color(0.3f, 0.6f, 1f, 1f);
        [Header("Materials")]
        public Material originalMaterial;
        public Material outlineMaterial;

        // Internal reference
        [HideInInspector]
        public Image imageComponent;
        [HideInInspector]
        public Canvas imageCanvas;
        [HideInInspector]
        public RectTransform imageRectTransform;
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
                button.outlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            }
            
            // Configure the material for transparency
            button.outlineMaterial.SetFloat("_Surface", 1);
            button.outlineMaterial.SetFloat("_Blend", 0);
            button.outlineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            button.outlineMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            button.outlineMaterial.renderQueue = 3000;
            
            // Set material properties
            button.outlineMaterial.SetFloat("_Smoothness", 0.5f);
            button.outlineMaterial.SetFloat("_Metallic", 0.0f);

            // Initialize UI image if icon is assigned
            if (button.useImage && button.buttonIcon != null)
            {
                // Create Canvas
                GameObject canvasObj = new GameObject($"{button.buttonName}_Canvas");
                canvasObj.transform.SetParent(button.buttonMeshRenderer.transform);
                button.imageCanvas = canvasObj.AddComponent<Canvas>();
                button.imageCanvas.renderMode = RenderMode.WorldSpace;
                
                // Set canvas initial transform
                canvasObj.transform.localPosition = Vector3.zero;
                canvasObj.transform.localRotation = Quaternion.identity;
                canvasObj.transform.localScale = Vector3.one * 0.001f; // Small base scale for better control

                // Add Canvas Scaler
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 1000;

                // Create Image GameObject
                GameObject imageObj = new GameObject($"{button.buttonName}_Image");
                imageObj.transform.SetParent(canvasObj.transform, false);
                button.imageComponent = imageObj.AddComponent<Image>();
                button.imageRectTransform = imageObj.GetComponent<RectTransform>();
                
                // Configure image
                button.imageComponent.sprite = button.buttonIcon;
                button.imageComponent.preserveAspect = true;
                float baseSize = 100f; // Base size in UI units
                button.imageRectTransform.sizeDelta = new Vector2(baseSize, baseSize);
                button.imageComponent.color = button.normalColor;
                
                // Center the image
                button.imageRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                button.imageRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                button.imageRectTransform.pivot = new Vector2(0.5f, 0.5f);
                
                // Apply transform values
                UpdateImageTransform(button);
            }
        }
    }

    private void UpdateImageTransform(ButtonVisuals button)
    {
        if (button.imageCanvas != null && button.imageRectTransform != null)
        {
            // Position
            button.imageRectTransform.localPosition = button.imagePosition * 1000f;
            
            // Rotation
            button.imageRectTransform.localRotation = Quaternion.Euler(button.imageRotation);
            
            // Scale - Apply both to transform and size
            button.imageRectTransform.localScale = button.imageScale;
            
            // Base size
            float scaledSize = button.imageSize * 100f;
            button.imageRectTransform.sizeDelta = new Vector2(scaledSize, scaledSize);
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
        
        // Update image transforms in case values changed in inspector
        foreach (var button in buttons)
        {
            if (button.useImage && button.buttonIcon != null)
            {
                UpdateImageTransform(button);
            }
        }
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
                    Debug.LogWarning($"Button name '{button.buttonName}' not recognized.");
                    break;
            }

            // Update button visuals
            if (isPressed)
            {
                ApplyButtonEffect(button, button.pressedColor, true);
            }
            else if (wasPressed)
            {
                ApplyButtonEffect(button, button.hoveredColor, false);
            }
            else
            {
                ApplyButtonEffect(button, button.normalColor, false);
            }

            previousButtonStates[button.buttonName] = isPressed;
        }
    }

    private void ApplyButtonEffect(ButtonVisuals button, Color color, bool isPressed)
    {
        // Update button material
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

        // Update image if present
        if (button.useImage && button.imageComponent != null)
        {
            button.imageComponent.color = color;
        }
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