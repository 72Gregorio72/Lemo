using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using DG.Tweening;
using HKCarouselLayoutGroup;
using UnityEngine.SceneManagement;

namespace HKCarouselLayoutGroup
{
    public class XRSceneInputManager : MonoBehaviour
    {
        [Header("Target GameObject")]
        [SerializeField] private GameObject targetObject;
        [SerializeField] private float animationDuration = 0.3f;

        [Header("Carousel Reference")]
        [SerializeField] private HKSceneCarouselLayoutGroup3D carousel;

        [Header("Input Settings")]
        [SerializeField] private float inputThreshold = 0.5f;
        [SerializeField] private float scrollCooldown = 0.25f;

        [Header("Scene Loading")]
        [SerializeField] private float fadeDuration = 1.5f;
        [SerializeField] private float targetPostExposure = -200f;

        [Header("Left Controller Events")]
        public UnityEvent OnLeftPrimaryButtonPressed;
        public UnityEvent OnLeftPrimaryButtonReleased;
        public UnityEvent OnLeftSecondaryButtonPressed;
        public UnityEvent OnLeftSecondaryButtonReleased;
        public UnityEvent OnLeftGripButtonPressed;
        public UnityEvent OnLeftGripButtonReleased;

        [Header("Right Controller Events")]
        public UnityEvent OnRightPrimaryButtonPressed;
        public UnityEvent OnRightPrimaryButtonReleased;
        public UnityEvent OnRightSecondaryButtonPressed;
        public UnityEvent OnRightSecondaryButtonReleased;
        public UnityEvent OnRightGripButtonPressed;
        public UnityEvent OnRightGripButtonReleased;

        private List<InputDevice> devices = new();
        private Dictionary<InputDevice, bool> previousPrimaryStates = new();
        private Dictionary<InputDevice, bool> previousSecondaryStates = new();
        private Dictionary<InputDevice, bool> previousGripStates = new();
        private Dictionary<InputDevice, bool> previousTriggerStates = new();

        private bool isLeftPrimaryOn;
        private bool isRightPrimaryOn;
        private bool isLeftSecondaryOn;
        private bool isRightSecondaryOn;
        private bool isLeftGripOn;
        private bool isRightGripOn;
        private bool isLeftTriggerOn;
        private bool isRightTriggerOn;
        private float cooldownTimer = 0f;
        private Vector3 originalScale;
        private bool isFading = false;
        private bool isObjectShown = false;

        private void Start()
        {
            if (targetObject != null)
            {
                originalScale = targetObject.transform.localScale;
                targetObject.SetActive(false);
            }

            if (carousel == null)
            {
                Debug.LogWarning("Carousel reference not set in XRSceneInputManager!");
            }
        }

        private void Update()
        {
            InputDevices.GetDevices(devices);
            cooldownTimer -= Time.deltaTime;

            foreach (var device in devices)
            {
                // PRIMARY BUTTON
                if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool isPrimaryHeld))
                {
                    previousPrimaryStates.TryGetValue(device, out bool wasPrimaryHeld);

                    if (isPrimaryHeld && !wasPrimaryHeld)
                    {
                        if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left))
                        {
                            isLeftPrimaryOn = !isLeftPrimaryOn;
                            if (isLeftPrimaryOn) OnLeftPrimaryButtonPressed?.Invoke();
                            else OnLeftPrimaryButtonReleased?.Invoke();
                        }
                        else if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right))
                        {
                            isRightPrimaryOn = !isRightPrimaryOn;
                            if (isRightPrimaryOn) OnRightPrimaryButtonPressed?.Invoke();
                            else OnRightPrimaryButtonReleased?.Invoke();
                        }
                    }

                    previousPrimaryStates[device] = isPrimaryHeld;
                }

                // SECONDARY BUTTON
                if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool isSecondaryHeld))
                {
                    previousSecondaryStates.TryGetValue(device, out bool wasSecondaryHeld);

                    if (isSecondaryHeld && !wasSecondaryHeld)
                    {
                        if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left))
                        {
                            isLeftSecondaryOn = !isLeftSecondaryOn;
                            if (isLeftSecondaryOn) OnLeftSecondaryButtonPressed?.Invoke();
                            else OnLeftSecondaryButtonReleased?.Invoke();
                        }
                        else if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right))
                        {
                            isRightSecondaryOn = !isRightSecondaryOn;
                            if (isRightSecondaryOn) OnRightSecondaryButtonPressed?.Invoke();
                            else OnRightSecondaryButtonReleased?.Invoke();
                        }
                    }

                    previousSecondaryStates[device] = isSecondaryHeld;
                }

                // GRIP BUTTON
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool isGripHeld))
                {
                    previousGripStates.TryGetValue(device, out bool wasGripHeld);

                    if (isGripHeld && !wasGripHeld)
                    {
                        if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left))
                        {
                            isLeftGripOn = !isLeftGripOn;
                            if (isLeftGripOn) OnLeftGripButtonPressed?.Invoke();
                            else OnLeftGripButtonReleased?.Invoke();
                        }
                        else if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right))
                        {
                            isRightGripOn = !isRightGripOn;
                            if (isRightGripOn) OnRightGripButtonPressed?.Invoke();
                            else OnRightGripButtonReleased?.Invoke();
                        }
                    }

                    previousGripStates[device] = isGripHeld;
                }

                // TRIGGER BUTTON - Handle object spawning or scene loading
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool isTriggerHeld))
                {
                    previousTriggerStates.TryGetValue(device, out bool wasTriggerHeld);

                    if (isTriggerHeld && !wasTriggerHeld)
                    {
                        if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left) || 
                            device.characteristics.HasFlag(InputDeviceCharacteristics.Right))
                        {
                            HandleTriggerPress();
                        }
                    }

                    previousTriggerStates[device] = isTriggerHeld;
                }

                // THUMBSTICK - Handle carousel navigation
                if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
                {
                    if (cooldownTimer <= 0f && carousel != null)
                    {
                        if (axis.x > inputThreshold)
                        {
                            carousel.SimulateScroll(-1);
                            cooldownTimer = scrollCooldown;
                        }
                        else if (axis.x < -inputThreshold)
                        {
                            carousel.SimulateScroll(1);
                            cooldownTimer = scrollCooldown;
                        }
                    }
                }
            }
        }

        private void HandleTriggerPress()
        {
            if (carousel == null) return;

            // If object is not shown yet, show it
            if (!isObjectShown)
            {
                ShowObjectWithAnimation();
                isObjectShown = true;
                return;
            }

            // Object is shown, check current index
            int currentIndex = carousel.GetTrueSelectedIndex();
            var sceneData = carousel.GetElementDataFromIndex(currentIndex);

            if (currentIndex == 0)
            {
                // Hide the object
                HideObjectWithAnimation();
                isObjectShown = false;
            }
            else if (sceneData != null)
            {
                // Load the scene
                StartCoroutine(FadeOutAndLoadScene(sceneData.sceneName, sceneData.sceneIndex));
            }
        }

        private System.Collections.IEnumerator FadeOutAndLoadScene(string sceneName, int sceneIndex)
        {
            if (isFading) yield break;
            isFading = true;

            // Find the global volume
            var globalVolume = FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            if (globalVolume == null)
            {
                Debug.LogError("Global Volume not found in scene!");
                isFading = false;
                yield break;
            }

            // Get color adjustments
            UnityEngine.Rendering.Universal.ColorAdjustments colorAdjustments;
            if (!globalVolume.profile.TryGet(out colorAdjustments))
            {
                Debug.LogError("Color Adjustments not found in Global Volume profile!");
                isFading = false;
                yield break;
            }

            float elapsedTime = 0f;
            float startValue = colorAdjustments.postExposure.value;

            // Fade to black
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / fadeDuration;
                float easedT = EaseInOutCubic(t);
                colorAdjustments.postExposure.value = Mathf.Lerp(startValue, targetPostExposure, easedT);
                yield return null;
            }

            // Ensure we reach the target value
            colorAdjustments.postExposure.value = targetPostExposure;

            // Load the scene
            AsyncOperation asyncLoad;
            if (!string.IsNullOrEmpty(sceneName))
            {
                asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            }
            else if (sceneIndex >= 0)
            {
                asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
            }
            else
            {
                isFading = false;
                yield break;
            }

            // Wait for the scene to finish loading
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            isFading = false;
        }

        private float EaseInOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        private void ShowObjectWithAnimation()
        {
            if (targetObject == null) return;

            targetObject.SetActive(true);
            targetObject.transform.localScale = Vector3.zero;
            targetObject.transform.DOScale(originalScale, animationDuration)
                .SetEase(Ease.OutBack);
        }

        private void HideObjectWithAnimation()
        {
            if (targetObject == null) return;

            targetObject.transform.DOScale(Vector3.zero, animationDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => targetObject.SetActive(false));
        }
    }
}
