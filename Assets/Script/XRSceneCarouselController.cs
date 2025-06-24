using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using DG.Tweening;
using HKCarouselLayoutGroup;

public class XRSceneCarouselController : MonoBehaviour
{
    [Header("Carousel Control")]
    [SerializeField] private HKSceneCarouselLayoutGroup3D carouselScene;
    [SerializeField] private float inputThreshold = 0.5f;
    [SerializeField] private float scrollCooldown = 0.25f;

    [Header("Scene Loading")]
    [SerializeField] private float targetPostExposure = -200f;
    [SerializeField] private float fadeDuration = 1.5f;
    private const float INITIAL_POST_EXPOSURE = 0f;

    private float cooldownTimer = 0f;
    private List<InputDevice> devices = new();
    private Dictionary<InputDevice, bool> previousTriggerStates = new();
    private bool isCarouselInteractive = true;
    private bool isFading = false;

    private Volume globalVolume;
    private ColorAdjustments colorAdjustments;

    private void Start()
    {
        InputDevices.GetDevices(devices);
        
        // Find the global volume in the scene
        globalVolume = FindFirstObjectByType<Volume>();

        if (globalVolume == null)
        {
            Debug.LogWarning("Global Volume not found in scene.");
            return;
        }

        // Try to get Color Adjustments override
        if (!globalVolume.profile.TryGet(out colorAdjustments))
        {
            Debug.LogError("Color Adjustments not found in Global Volume profile.");
        }
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        InputDevices.GetDevices(devices);
        cooldownTimer -= Time.deltaTime;

        foreach (var device in devices)
        {
            // TRIGGER BUTTON
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool isTriggerHeld))
            {
                previousTriggerStates.TryGetValue(device, out bool wasTriggerHeld);

                if (isTriggerHeld && !wasTriggerHeld)
                {
                    LoadSelectedScene();
                }

                previousTriggerStates[device] = isTriggerHeld;
            }

            // THUMBSTICK
            if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                if (cooldownTimer <= 0f && isCarouselInteractive && carouselScene != null)
                {
                    if (axis.x > inputThreshold)
                    {
                        carouselScene.SimulateScroll(-1);
                        cooldownTimer = scrollCooldown;
                    }
                    else if (axis.x < -inputThreshold)
                    {
                        carouselScene.SimulateScroll(1);
                        cooldownTimer = scrollCooldown;
                    }
                }
            }
        }
    }

    private void LoadSelectedScene()
    {
        if (carouselScene == null || isFading) return;

        int currentIndex = carouselScene.GetTrueSelectedIndex();
        var sceneData = carouselScene.GetElementDataFromIndex(currentIndex);

        if (sceneData == null)
        {
            Debug.LogError($"No scene data found for index: {currentIndex}");
            return;
        }

        Debug.Log($"Loading Scene - Name: '{sceneData.sceneName}', Index: {sceneData.sceneIndex}");
        StartCoroutine(FadeOutAndLoadScene(sceneData.sceneName, sceneData.sceneIndex));
    }

    private float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    private IEnumerator FadeOutAndLoadScene(string sceneName, int sceneIndex)
    {
        isFading = true;
        isCarouselInteractive = false;

        float elapsedTime = 0f;
        float startValue = colorAdjustments.postExposure.value;
        float targetValue = targetPostExposure;

        Debug.Log($"Starting fade from {startValue} to {targetValue}");

        // Fade to black
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;
            
            float easedT = EaseInOutCubic(t);
            colorAdjustments.postExposure.value = Mathf.Lerp(startValue, targetValue, easedT);

            yield return null;
        }

        colorAdjustments.postExposure.value = targetValue;
        Debug.Log("Fade complete, loading scene...");

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
            isCarouselInteractive = true;
            yield break;
        }

        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            Debug.Log($"Loading scene: {progress * 100}%");
            yield return null;
        }

        Debug.Log("Scene load complete");
        isFading = false;
        isCarouselInteractive = true;
    }
} 