using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using Oculus.Platform;

public class LogoFadeAndSpawn : MonoBehaviour
{
    public RawImage logoImage;             // Assign your UI RawImage
    public float logoAnimationTime = 4f;   // Total time for fade in/out
    public GameObject canvasPrefab;        // Prefab with scale (0.01, 0.01, 0.01)
    public AudioSource audioSource;

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

        // Play audio at exactly half time
        PlayAudio();
        
        // Fade Out: 1 -> 0
        for (float t = 0f; t < halfTime; t += Time.deltaTime)
        {
            SetAlpha(1f - (t / halfTime));
            yield return null;
        }
        SetAlpha(0f); // Ensure it's fully hidden

        GoHomeScene(3f);
    }

    private void PlayAudio()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("AudioSource is not assigned!");
        }
    }

    private void GoHomeScene(float delay)
    {
        StartCoroutine(LoadSceneWithDelay(delay));
    }

    private IEnumerator LoadSceneWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(1);
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            // Wait until the scene is fully loaded
            if (asyncLoad.progress >= 0.9f)
            {
                // Activate the scene when ready
                asyncLoad.allowSceneActivation = true;
            }
            yield return null;
        }
    }

    private void SetAlpha(float alpha)
    {
        Color color = logoImage.color;
        color.a = Mathf.Clamp01(alpha);
        logoImage.color = color;
    }
}
