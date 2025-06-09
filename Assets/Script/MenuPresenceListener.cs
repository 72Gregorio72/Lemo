using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

public class MenuPresenceListener : MonoBehaviour
{
    [SerializeField] private GameObject menuPrefabToWatch;
    [SerializeField] private float menuFocusDistance = 0.1f;
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private Ease transitionEase = Ease.InOutQuad;

    private GameObject currentMenuInstance;
    private Volume globalVolume;
    private DepthOfField depthOfField;
    private float initialFocusDistance;
    private DepthOfFieldMode initialFocusMode;
    private Sequence dofSequence;

    private void Start()
    {
        // Get Global Volume and Depth of Field
        globalVolume = FindAnyObjectByType<Volume>();
        if (globalVolume != null && globalVolume.profile.TryGet(out depthOfField))
        {
            // Store initial values
            initialFocusDistance = depthOfField.focusDistance.value;
            initialFocusMode = depthOfField.mode.value;
            Debug.Log($"[MenuPresenceListener] Stored initial focus distance: {initialFocusDistance}");
        }
        else
        {
            Debug.LogWarning("[MenuPresenceListener] Depth of Field component not found in the global volume profile!");
        }
    }

    private void Update()
    {
        // Find any instance of our prefab in the scene
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        bool foundNewInstance = false;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains(menuPrefabToWatch.name) && obj != currentMenuInstance)
            {
                currentMenuInstance = obj;
                Debug.Log($"[MenuPresenceListener] Found menu instance: {obj.name}");
                foundNewInstance = true;
                break;
            }
        }

        // If we found a new instance, transition to menu DOF settings
        if (foundNewInstance && depthOfField != null)
        {
            TransitionToMenuFocus();
        }
        // If our tracked instance was destroyed, transition back to initial settings
        else if (currentMenuInstance == null && depthOfField != null)
        {
            Debug.Log("[MenuPresenceListener] Menu instance is no longer in the scene");
            TransitionToInitialFocus();
        }
    }

    private void TransitionToMenuFocus()
    {
        // Kill any existing transition
        dofSequence?.Kill();

        // Create new transition sequence
        dofSequence = DOTween.Sequence()
            .Append(DOTween.To(() => depthOfField.focusDistance.value, x => depthOfField.focusDistance.value = x, menuFocusDistance, transitionDuration).SetEase(transitionEase))
            .OnStart(() => {
                depthOfField.mode.value = DepthOfFieldMode.Bokeh;
            });
    }

    private void TransitionToInitialFocus()
    {
        // Kill any existing transition
        dofSequence?.Kill();

        // Create new transition sequence
        dofSequence = DOTween.Sequence()
            .Append(DOTween.To(() => depthOfField.focusDistance.value, x => depthOfField.focusDistance.value = x, initialFocusDistance, transitionDuration).SetEase(transitionEase))
            .OnComplete(() => {
                depthOfField.mode.value = initialFocusMode;
            });
    }

    private void OnDestroy()
    {
        dofSequence?.Kill();
        // Reset to initial values
        if (depthOfField != null)
        {
            depthOfField.focusDistance.value = initialFocusDistance;
            depthOfField.mode.value = initialFocusMode;
        }
    }

    private void OnValidate()
    {
        if (menuPrefabToWatch == null)
        {
            Debug.LogWarning("[MenuPresenceListener] Please assign a menu prefab to watch!");
        }
    }
} 
