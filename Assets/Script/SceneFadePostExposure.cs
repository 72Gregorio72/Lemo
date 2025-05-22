using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SceneFadePostExposure : MonoBehaviour
{
    private Volume globalVolume;
    private ColorAdjustments colorAdjustments;

    private bool isFading = false;

    private void Start()
    {
        // Find the global volume in the scene
        globalVolume = FindFirstObjectByType<Volume>();

        if (globalVolume == null)
        {
            //Debug.LogError("Global Volume not found in scene.");
            return;
        }

        // Try to get Color Adjustments override
        if (!globalVolume.profile.TryGet(out colorAdjustments))
        {
            Debug.LogError("Color Adjustments not found in Global Volume profile.");
        }
    }

    public void HomeScene()
    {
        if (isFading || colorAdjustments == null)
            return;

        isFading = true;

        float currentExposure = colorAdjustments.postExposure.value;

        // Animate post-exposure from current to -20
        DOTween.To(
            () => currentExposure,
            x => {
                currentExposure = x;
                colorAdjustments.postExposure.value = x;
            },
            -20f,
            1.5f // duration of fade
        )
        .SetEase(Ease.InQuad)
        .OnComplete(() => SceneManager.LoadScene(1));
    }
}
