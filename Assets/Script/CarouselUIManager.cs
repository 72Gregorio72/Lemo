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
    [SerializeField] private Ease easeType = Ease.OutBack;
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
        uiScene.SetActive(false);

        // Set initial scales and alpha
        ui360.transform.localScale = Vector3.one;
        uiScene.transform.localScale = Vector3.one * scaleMultiplier;
        
        // Get or add CanvasGroups
        ui360CanvasGroup = ui360.GetComponent<CanvasGroup>() ?? ui360.AddComponent<CanvasGroup>();
        uiSceneCanvasGroup = uiScene.GetComponent<CanvasGroup>() ?? uiScene.AddComponent<CanvasGroup>();

        ui360CanvasGroup.alpha = 1f;
        uiSceneCanvasGroup.alpha = 0f;
        
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
        if (!leftController.isValid || !rightController.isValid)
        {
            InitializeDevices();
            return;
        }

        HandleUISwitch();
    }

    private void InitializeDevices()
    {
        var devices = new List<InputDevice>();
        
        // Get left controller
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left,
            devices);
        if (devices.Count > 0)
        {
            leftController = devices[0];
            Debug.Log($"Left controller found: {leftController.name}");
        }

        // Get right controller
        devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right,
            devices);
        if (devices.Count > 0)
        {
            rightController = devices[0];
            Debug.Log($"Right controller found: {rightController.name}");
        }
    }

    private void HandleUISwitch()
    {
        if (isTransitioning) return;

        bool leftSecondaryPressed = false;
        bool rightSecondaryPressed = false;

        leftController.TryGetFeatureValue(CommonUsages.secondaryButton, out leftSecondaryPressed);
        rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out rightSecondaryPressed);

        bool anySecondaryPressed = leftSecondaryPressed || rightSecondaryPressed;

        if (anySecondaryPressed && !wasSecondaryButtonPressed)
        {
            isSceneUIActive = !isSceneUIActive;
            TransitionUI();
        }
        wasSecondaryButtonPressed = anySecondaryPressed;
    }

    private void TransitionUI()
    {
        isTransitioning = true;

        if (isSceneUIActive)
        {
            // Transition from 360 UI to Scene UI
            uiScene.SetActive(true);
            uiScene.transform.localScale = Vector3.one * scaleMultiplier;
            uiSceneCanvasGroup.alpha = 0f;

            // Fade out and scale down 360 UI
            Sequence seq360 = DOTween.Sequence();
            seq360.Join(ui360.transform.DOScale(Vector3.one * scaleMultiplier, transitionDuration).SetEase(easeType))
                  .Join(ui360CanvasGroup.DOFade(0f, transitionDuration).SetEase(Ease.InOutSine))
                  .OnComplete(() => ui360.SetActive(false));

            // Fade in and scale up Scene UI
            Sequence seqScene = DOTween.Sequence();
            seqScene.Join(uiScene.transform.DOScale(Vector3.one, transitionDuration).SetEase(easeType))
                   .Join(uiSceneCanvasGroup.DOFade(1f, transitionDuration).SetEase(Ease.InOutSine))
                   .OnComplete(() => isTransitioning = false);
        }
        else
        {
            // Transition from Scene UI to 360 UI
            ui360.SetActive(true);
            ui360.transform.localScale = Vector3.one * scaleMultiplier;
            ui360CanvasGroup.alpha = 0f;

            // Fade out and scale down Scene UI
            Sequence seqScene = DOTween.Sequence();
            seqScene.Join(uiScene.transform.DOScale(Vector3.one * scaleMultiplier, transitionDuration).SetEase(easeType))
                   .Join(uiSceneCanvasGroup.DOFade(0f, transitionDuration).SetEase(Ease.InOutSine))
                   .OnComplete(() => uiScene.SetActive(false));

            // Fade in and scale up 360 UI
            Sequence seq360 = DOTween.Sequence();
            seq360.Join(ui360.transform.DOScale(Vector3.one, transitionDuration).SetEase(easeType))
                 .Join(ui360CanvasGroup.DOFade(1f, transitionDuration).SetEase(Ease.InOutSine))
                 .OnComplete(() => isTransitioning = false);
        }

        Debug.Log($"Switched to {(isSceneUIActive ? "Scene UI" : "360 UI")}");
    }
} 