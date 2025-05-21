using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;

public class LogoFadeAndSpawn : MonoBehaviour
{
    public RawImage logoImage;             // Assign your UI RawImage
    public float logoAnimationTime = 4f;   // Total time for fade in/out
    public GameObject canvasPrefab;        // Prefab with scale (0.01, 0.01, 0.01)

    private void Start()
    {
        if (logoImage == null || canvasPrefab == null)
        {
            Debug.LogError("Missing references: Assign logoImage and canvasPrefab.");
            return;
        }

        SetAlpha(0f); // Hide logo initially
        StartCoroutine(AnimateLogo());
    }

    private IEnumerator AnimateLogo()
    {
        yield return new WaitForSeconds(2f); // Delay before fade starts

        float halfTime = logoAnimationTime / 2f;

        // Fade In: 0 -> 1
        for (float t = 0f; t < halfTime; t += Time.deltaTime)
        {
            SetAlpha(t / halfTime);
            yield return null;
        }
        SetAlpha(1f); // Ensure it's fully visible

        // Fade Out: 1 -> 0
        for (float t = 0f; t < halfTime; t += Time.deltaTime)
        {
            SetAlpha(1f - (t / halfTime));
            yield return null;
        }
        SetAlpha(0f); // Ensure it's fully hidden

        GameObject newCanvas = Instantiate(canvasPrefab);

        // Ensure prefab starts at correct scale
        newCanvas.transform.localScale = Vector3.one * 0.01f;

        // Punch scale around current size
        newCanvas.transform.DOPunchScale(Vector3.one * 0.005f, 0.5f, 5, 1);

    }

    private void SetAlpha(float alpha)
    {
        Color color = logoImage.color;
        color.a = Mathf.Clamp01(alpha);
        logoImage.color = color;
    }
}
