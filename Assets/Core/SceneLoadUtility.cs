using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using DG.Tweening;

namespace Core
{
    public class SceneLoadUtility : MonoBehaviour
    {
        private static SceneLoadUtility instance;
        public static SceneLoadUtility Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("SceneLoadUtility");
                    instance = go.AddComponent<SceneLoadUtility>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Fade Settings")]
        [SerializeField] private float fadeDuration = 1.5f;
        [SerializeField] private float targetPostExposure = -200f;
        [SerializeField] private float loadingDelay = 0.5f;

        private bool isLoading = false;

        public bool IsLoading => isLoading;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public IEnumerator LoadSceneWithFade(string sceneName = null, int sceneIndex = -1)
        {
            if (isLoading) yield break;
            isLoading = true;

            // Find the global volume
            var globalVolume = FindFirstObjectByType<Volume>();
            if (globalVolume == null)
            {
                Debug.LogError("Global Volume not found in scene!");
                isLoading = false;
                yield break;
            }

            // Get color adjustments
            ColorAdjustments colorAdjustments;
            if (!globalVolume.profile.TryGet(out colorAdjustments))
            {
                Debug.LogError("Color Adjustments not found in Global Volume profile!");
                isLoading = false;
                yield break;
            }

            // Start scene loading
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
                Debug.LogError("No valid scene name or index provided!");
                isLoading = false;
                yield break;
            }

            // Don't let the scene activate until we allow it
            asyncLoad.allowSceneActivation = false;

            // Create fade sequence
            float startValue = colorAdjustments.postExposure.value;
            Sequence fadeSequence = DOTween.Sequence();
            
            // Fade to black
            fadeSequence.Append(DOTween.To(
                () => startValue,
                x => colorAdjustments.postExposure.value = x,
                targetPostExposure,
                fadeDuration
            ).SetEase(Ease.InOutCubic));

            // Wait for both fade and scene load
            while (!fadeSequence.IsComplete() || asyncLoad.progress < 0.9f)
            {
                float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                Debug.Log($"Loading scene: {progress:P0}");
                yield return null;
            }

            // Optional delay
            yield return new WaitForSeconds(loadingDelay);

            // Activate the scene
            asyncLoad.allowSceneActivation = true;

            // Wait for scene activation
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            isLoading = false;
        }

        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSceneWithFade(sceneName: sceneName));
        }

        public void LoadScene(int sceneIndex)
        {
            StartCoroutine(LoadSceneWithFade(sceneIndex: sceneIndex));
        }
    }
} 