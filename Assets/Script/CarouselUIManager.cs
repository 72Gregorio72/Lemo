using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using HKCarouselLayoutGroup;
using DG.Tweening;

public class CarouselUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject ui360;
    [SerializeField] private GameObject uiScene;
    
    [Header("Input Settings")]
    [SerializeField] private bool isLeftController = false;

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private Ease easeType = Ease.OutBack;
    [SerializeField] private float scaleMultiplier = 0.8f;
    [SerializeField] private float transitionRadius = 2f; // Distance from center for the circular motion
    [SerializeField] private float rotationDegrees = 90f; // How many degrees to rotate during transition
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // Axis around which UIs rotate
    
    private InputDevice targetDevice;
    private bool isSceneUIActive = false;
    private bool wasSecondaryButtonPressed = false;
    private bool isTransitioning = false;

    private void Start()
    {
        // Set initial UI state
        ui360.SetActive(true);
        uiScene.SetActive(false);

        // Set initial scales
        ui360.transform.localScale = Vector3.one;
        uiScene.transform.localScale = Vector3.one * scaleMultiplier;
        
        // Verify tags
        if (ui360.tag != "360UI")
        {
            Debug.LogError("360 UI object must be tagged as '360UI'");
        }
        if (uiScene.tag != "SceneUI")
        {
            Debug.LogError("Scene UI object must be tagged as 'SceneUI'");
        }
    }

    private void Update()
    {
        if (!targetDevice.isValid)
        {
            InitializeDevice();
            return;
        }

        HandleUISwitch();
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
        }
    }

    private void HandleUISwitch()
    {
        if (isTransitioning) return; // Don't handle input during transition

        if (targetDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryButtonPressed))
        {
            if (secondaryButtonPressed && !wasSecondaryButtonPressed)
            {
                // Toggle UI state
                isSceneUIActive = !isSceneUIActive;
                TransitionUI();
            }
            wasSecondaryButtonPressed = secondaryButtonPressed;
        }
    }

    private void TransitionUI()
    {
        isTransitioning = true;

        if (isSceneUIActive)
        {
            // Transition from 360 UI to Scene UI
            uiScene.SetActive(true);
            uiScene.transform.localScale = Vector3.one * scaleMultiplier;

            // Fade out and scale down 360 UI
            Sequence seq360 = DOTween.Sequence();
            seq360.Join(ui360.transform.DOScale(Vector3.one * scaleMultiplier, transitionDuration).SetEase(easeType))
                  .OnComplete(() => ui360.SetActive(false));

            // Fade in and scale up Scene UI
            Sequence seqScene = DOTween.Sequence();
            seqScene.Join(uiScene.transform.DOScale(Vector3.one, transitionDuration).SetEase(easeType))
                   .OnComplete(() => isTransitioning = false);
        }
        else
        {
            // Transition from Scene UI to 360 UI
            ui360.SetActive(true);
            ui360.transform.localScale = Vector3.one * scaleMultiplier;

            // Fade out and scale down Scene UI
            Sequence seqScene = DOTween.Sequence();
            seqScene.Join(uiScene.transform.DOScale(Vector3.one * scaleMultiplier, transitionDuration).SetEase(easeType))
                   .OnComplete(() => uiScene.SetActive(false));

            // Fade in and scale up 360 UI
            Sequence seq360 = DOTween.Sequence();
            seq360.Join(ui360.transform.DOScale(Vector3.one, transitionDuration).SetEase(easeType))
                 .OnComplete(() => isTransitioning = false);
        }

        Debug.Log($"Switched to {(isSceneUIActive ? "Scene UI" : "360 UI")}");
    }
} 