using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

public class SceneLoadManager : MonoBehaviour
{
    [Header("Color Adjustment Settings")]
    [Tooltip("Target post exposure value to lerp to")]
    public float targetPostExposure = 0f;
    [Tooltip("Initial post exposure value (dark)")]
    private const float INITIAL_POST_EXPOSURE = -200f;
    [Tooltip("How long the fade should take")]
    public float fadeDuration = 2f;

    [Header("Scene Loading")]
    [Tooltip("List of GameObjects to activate after fade")]
    public List<GameObject> objectsToActivate;
    [Tooltip("Delay between activating each object")]
    public float objectActivationDelay = 0.1f;
    [Tooltip("At what percentage of the fade to start activating objects (0-1)")]
    [Range(0f, 1f)]
    public float startActivatingAt = 0.3f;

    private Volume globalVolume;
    private ColorAdjustments colorAdjustments;

    private void Awake()
    {
        // Get the Global Volume component
        globalVolume = GetComponent<Volume>();
        if (globalVolume == null)
        {
            Debug.LogError("No Volume component found on this GameObject!");
            return;
        }

        // Try to get Color Adjustments
        if (!globalVolume.profile.TryGet(out colorAdjustments))
        {
            Debug.LogError("No Color Adjustments found in the Global Volume profile!");
            return;
        }

        // Set initial dark value
        colorAdjustments.postExposure.value = INITIAL_POST_EXPOSURE;

        // Deactivate all objects in the list
        if (objectsToActivate != null)
        {
            foreach (GameObject obj in objectsToActivate)
            {
                if (obj != null && obj != gameObject)
                {
                    obj.SetActive(false);
                }
            }
        }

        // Start the fade coroutine
        StartCoroutine(FadeInScene());
    }

    private float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    private IEnumerator ActivateObjectsSequentially()
    {
        if (objectsToActivate != null)
        {
            for (int i = 0; i < objectsToActivate.Count; i++)
            {
                if (objectsToActivate[i] != null && !objectsToActivate[i].activeSelf)
                {
                    objectsToActivate[i].SetActive(true);
                    yield return new WaitForSeconds(objectActivationDelay);
                }
            }
        }
    }

    private IEnumerator FadeInScene()
    {
        float elapsedTime = 0f;
        float startValue = INITIAL_POST_EXPOSURE;
        bool startedActivating = false;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;
            
            // Apply easing to the fade
            float easedT = EaseInOutCubic(t);
            colorAdjustments.postExposure.value = Mathf.Lerp(startValue, targetPostExposure, easedT);

            // Start activating objects at the specified percentage
            if (!startedActivating && t >= startActivatingAt)
            {
                startedActivating = true;
                StartCoroutine(ActivateObjectsSequentially());
            }

            yield return null;
        }

        // Ensure we reach the target value
        colorAdjustments.postExposure.value = targetPostExposure;
    }
} 