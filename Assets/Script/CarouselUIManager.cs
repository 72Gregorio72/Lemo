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
    
    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private Ease easeType = Ease.OutQuad;
    [SerializeField] private float scaleMultiplier = 0.8f;

    private InputDevice leftController;
    private InputDevice rightController;
    private bool isSceneUIActive = false;
    private bool wasSecondaryButtonPressed = false;
    private bool isTransitioning = false;

    private CanvasGroup ui360CanvasGroup;
    private CanvasGroup uiSceneCanvasGroup;

    private void Start()
    {
        // Set initial UI state
        ui360.SetActive(true);
        uiScene.SetActive(true); // Keep both active but control visibility with CanvasGroup

        // Get or add CanvasGroup components
        ui360CanvasGroup = ui360.GetComponent<CanvasGroup>();
        if (ui360CanvasGroup == null)
            ui360CanvasGroup = ui360.AddComponent<CanvasGroup>();

        uiSceneCanvasGroup = uiScene.GetComponent<CanvasGroup>();
        if (uiSceneCanvasGroup == null)
            uiSceneCanvasGroup = uiScene.AddComponent<CanvasGroup>();

        // Set initial states
        ui360CanvasGroup.alpha = 1f;
        ui360CanvasGroup.interactable = true;
        ui360CanvasGroup.blocksRaycasts = true;

        uiSceneCanvasGroup.alpha = 0f;
        uiSceneCanvasGroup.interactable = false;
        uiSceneCanvasGroup.blocksRaycasts = false;

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

        // Ensure the carousel components are properly referenced
        var sceneCarousel = uiScene.GetComponentInChildren<HKSceneCarouselLayoutGroup3D>();
        var ui360Carousel = ui360.GetComponentInChildren<HKCarouselLayoutGroup3D<HKCarouselElementData>>();

        if (sceneCarousel == null || ui360Carousel == null)
        {
            Debug.LogError("Carousel components not found in UI objects!");
        }
    }

    private void Update()
    {
        // Initialize devices if needed
        if (!leftController.isValid || !rightController.isValid)
        {
            InitializeDevices();
        }

        // Even if not all devices are initialized, we can still handle input from valid ones
        HandleUISwitch();
    }

    private void InitializeDevices()
    {
        var leftDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, leftDevices);
        if (leftDevices.Count > 0)
        {
            leftController = leftDevices[0];
        }

        var rightDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, rightDevices);
        if (rightDevices.Count > 0)
        {
            rightController = rightDevices[0];
        }
    }

    private void HandleUISwitch()
    {
        if (isTransitioning) return; // Don't handle input during transition

        bool leftSecondaryPressed = false;
        bool rightSecondaryPressed = false;

        // Check each controller independently
        if (leftController.isValid)
        {
            leftController.TryGetFeatureValue(CommonUsages.secondaryButton, out leftSecondaryPressed);
        }
        
        if (rightController.isValid)
        {
            rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out rightSecondaryPressed);
        }

        bool secondaryButtonPressed = leftSecondaryPressed || rightSecondaryPressed;

        if (secondaryButtonPressed && !wasSecondaryButtonPressed)
        {
            // Toggle UI state
            isSceneUIActive = !isSceneUIActive;
            TransitionUI();
        }
        wasSecondaryButtonPressed = secondaryButtonPressed;
    }

    private void TransitionUI()
    {
        isTransitioning = true;

        if (isSceneUIActive)
        {
            // Transition from 360 UI to Scene UI
            // Fade out and scale down 360 UI
            Sequence seq360 = DOTween.Sequence();
            seq360.Join(ui360.transform.DOScale(Vector3.one * scaleMultiplier, transitionDuration).SetEase(easeType))
                  .Join(ui360CanvasGroup.DOFade(0f, transitionDuration).SetEase(easeType))
                  .OnStart(() => {
                      ui360CanvasGroup.interactable = false;
                      ui360CanvasGroup.blocksRaycasts = false;
                  });

            // Fade in and scale up Scene UI
            Sequence seqScene = DOTween.Sequence();
            seqScene.Join(uiScene.transform.DOScale(Vector3.one, transitionDuration).SetEase(easeType))
                   .Join(uiSceneCanvasGroup.DOFade(1f, transitionDuration).SetEase(easeType))
                   .OnComplete(() => {
                       uiSceneCanvasGroup.interactable = true;
                       uiSceneCanvasGroup.blocksRaycasts = true;
                       isTransitioning = false;
                   });
        }
        else
        {
            // Transition from Scene UI to 360 UI
            // Fade out and scale down Scene UI
            Sequence seqScene = DOTween.Sequence();
            seqScene.Join(uiScene.transform.DOScale(Vector3.one * scaleMultiplier, transitionDuration).SetEase(easeType))
                   .Join(uiSceneCanvasGroup.DOFade(0f, transitionDuration).SetEase(easeType))
                   .OnStart(() => {
                       uiSceneCanvasGroup.interactable = false;
                       uiSceneCanvasGroup.blocksRaycasts = false;
                   });

            // Fade in and scale up 360 UI
            Sequence seq360 = DOTween.Sequence();
            seq360.Join(ui360.transform.DOScale(Vector3.one, transitionDuration).SetEase(easeType))
                 .Join(ui360CanvasGroup.DOFade(1f, transitionDuration).SetEase(easeType))
                 .OnComplete(() => {
                     ui360CanvasGroup.interactable = true;
                     ui360CanvasGroup.blocksRaycasts = true;
                     isTransitioning = false;
                 });
        }

        Debug.Log($"Switched to {(isSceneUIActive ? "Scene UI" : "360 UI")}");
    }
} 