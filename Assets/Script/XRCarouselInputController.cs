using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.Video;
using DG.Tweening;
using System.IO;
using System.Globalization;
using System;
using UnityEngine.Networking;
using System.Collections;
namespace HKCarouselLayoutGroup
{
    public class XRCarouselInputController : MonoBehaviour
    {
        [Header("Carousel Control")]
        [SerializeField] private HKCarouselLayoutGroup3DDemo carousel;
        [SerializeField] private float inputThreshold = 0.5f;
        [SerializeField] private float scrollCooldown = 0.25f;
        [SerializeField] private float fadeOutDuration = 1f;

        [Header("Materials & Sphere")]
        [SerializeField] private Material sphereMaterial;
        [SerializeField] private Transform videoSphere;

        [Header("XR Button Events")]
        public UnityEvent OnLeftPrimaryPressed;
        public UnityEvent OnLeftPrimaryReleased;
        public UnityEvent OnRightPrimaryPressed;
        public UnityEvent OnRightPrimaryReleased;

        private float cooldownTimer = 0f;
        private List<InputDevice> devices = new();
        private Dictionary<InputDevice, bool> previousPrimaryStates = new();
        private Dictionary<InputDevice, bool> previousTriggerStates = new();

        private int triggerPressCount = 0;
        private string currentMediaPath = null;
        private bool isVideoPaused = false;

        private bool isLeftObjectOn = false;
        private bool isRightObjectOn = false;
        [SerializeField] private AudioSource audioSource;
        private string currentAudioPath = null;

        private bool isAudioPaused = false;
        [Header("Rating System")]
        [SerializeField] private GameObject ratingPrefab;
        [SerializeField] private Transform ratingParent; // optional: canvas or world-space anchor

        private bool isRatingPending = false;
        private GameObject activeRatingInstance;
        private string pendingMediaPath = null;
        private HKCarouselElementData pendingElementData = null;

        private Texture2D blackTexture;

        void Start()
        {
            InputDevices.GetDevices(devices);
            
            // Create a black texture to use as default/transition
            blackTexture = new Texture2D(1, 1);
            blackTexture.SetPixel(0, 0, Color.black);
            blackTexture.Apply();
        }

        void OnDestroy()
        {
            if (blackTexture != null)
            {
                Destroy(blackTexture);
            }
        }

        void Update()
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
                        bool isLeft = device.characteristics.HasFlag(InputDeviceCharacteristics.Left);
                        bool isRight = device.characteristics.HasFlag(InputDeviceCharacteristics.Right);

                        if (isLeft)
                        {
                            isLeftObjectOn = !isLeftObjectOn;
                            if (isLeftObjectOn) OnLeftPrimaryPressed?.Invoke();
                            else OnLeftPrimaryReleased?.Invoke();
                        }
                        else if (isRight)
                        {
                            isRightObjectOn = !isRightObjectOn;
                            if (isRightObjectOn) OnRightPrimaryPressed?.Invoke();
                            else OnRightPrimaryReleased?.Invoke();
                        }
                    }

                    previousPrimaryStates[device] = isPrimaryHeld;
                }

                // TRIGGER BUTTON
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool isTriggerHeld))
                {
                    previousTriggerStates.TryGetValue(device, out bool wasTriggerHeld);

                    if (isTriggerHeld && !wasTriggerHeld)
                    {
                        PlaySelectedMedia(); // ✅ NOW DEFINED
                    }

                    previousTriggerStates[device] = isTriggerHeld;
                }

                // THUMBSTICK
                if (carousel != null && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
                {
                    if (cooldownTimer <= 0f)
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

        private void PlaySelectedMedia()
        {
            if (carousel == null || isRatingPending) return;

            int index = carousel.GetTrueSelectedIndex();
            var elementData = carousel.GetElementDataFromIndex(index);

            if (elementData is not HKCarouselElementData data || string.IsNullOrEmpty(data.VideoPath))
                return;

            bool isSameMedia = data.VideoPath == currentMediaPath;

            // Check if switching to different media while a previous one was already played
            if (!isSameMedia && triggerPressCount > 1)
            {
                // Rating required before switching
                pendingMediaPath = data.VideoPath;
                pendingElementData = data;
                ShowRatingPopup();
                return;
            }

            triggerPressCount++;
            bool shouldFadeOut = triggerPressCount % 2 != 0;

            // ✅ IMAGE CASE
            if (data.IsImage360)
            {
                // Disable video player
                GameObject playerObject = GameObject.FindGameObjectWithTag("VideoPlayer");
                if (playerObject != null)
                {
                    var videoPlayer = playerObject.GetComponent<VideoPlayer>();
                    if (videoPlayer != null)
                    {
                        videoPlayer.Stop();
                        videoPlayer.enabled = false;
                    }
                }

                if (!isSameMedia)
                {
                    currentMediaPath = data.VideoPath;
                    triggerPressCount = 1;
                    isAudioPaused = false;

                    FadeOutCanvas();
                    DisplayImageOnSphere(data.VideoPath);
                    return;
                }

                if (shouldFadeOut)
                {
                    if (audioSource != null && isAudioPaused && !string.IsNullOrEmpty(currentAudioPath))
                    {
                        audioSource.Play();
                        isAudioPaused = false;
                    }

                    FadeOutCanvas();
                }
                else
                {
                    if (audioSource != null && audioSource.isPlaying)
                    {
                        audioSource.Pause();
                        isAudioPaused = true;
                    }

                    FadeInCanvas();
                }

                return;
            }

            // ✅ VIDEO CASE
            GameObject videoObj = GameObject.FindGameObjectWithTag("VideoPlayer");
            if (videoObj == null)
            {
                Debug.LogWarning("No object tagged 'VideoPlayer' found.");
                return;
            }

            var vp = videoObj.GetComponent<VideoPlayer>();
            if (vp == null)
            {
                Debug.LogWarning("No VideoPlayer component found.");
                return;
            }

            if (!isSameMedia)
            {
                currentMediaPath = data.VideoPath;
                triggerPressCount = 1;
                isVideoPaused = false;
                isAudioPaused = false;

                FadeOutCanvas();
                PlayVideo(data.VideoPath, vp);
                return;
            }

            if (shouldFadeOut)
            {
                if (isVideoPaused)
                {
                    vp.Play();
                    isVideoPaused = false;
                }

                if (audioSource != null && isAudioPaused && !string.IsNullOrEmpty(currentAudioPath))
                {
                    audioSource.Play();
                    isAudioPaused = false;
                }

                FadeOutCanvas();
            }
            else
            {
                vp.Pause();
                isVideoPaused = true;

                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.Pause();
                    isAudioPaused = true;
                }

                FadeInCanvas();
            }
        }

        private void ShowRatingPopup()
        {
            if (ratingPrefab == null || ratingParent == null || isRatingPending) return;

            isRatingPending = true;
            activeRatingInstance = Instantiate(ratingPrefab, ratingParent);

            var ratingSystem = activeRatingInstance.GetComponent<RatingSystem>();
            if (ratingSystem != null)
            {
                // Extract experience name from currentMediaPath (assumes it's from element data)
                string experienceName = "Unknown";
                if (!string.IsNullOrEmpty(currentMediaPath))
                {
                    string fileName = Path.GetFileNameWithoutExtension(currentMediaPath);
                    experienceName = fileName; // Or store and pass original elementData.Name
                }

                ratingSystem.experienceName = experienceName;

                StartCoroutine(WaitForRatingComplete(ratingSystem));
            }

            Debug.Log("[Rating] Rating popup shown before switching media.");
        }


        private IEnumerator WaitForRatingComplete(RatingSystem ratingSystem)
        {
            while (!ratingSystem || !ratingSystem.enabled || !ratingSystem.gameObject.activeSelf)
                yield return null;

            while (!ratingSystem.exitTriggered)
                yield return null;

            isRatingPending = false;

            if (activeRatingInstance != null)
                Destroy(activeRatingInstance);

            if (pendingElementData == null || string.IsNullOrEmpty(pendingMediaPath))
                yield break;

            currentMediaPath = pendingMediaPath;
            triggerPressCount = 1;
            isVideoPaused = false;
            isAudioPaused = false;

            FadeOutCanvas();

            // Play the stored media
            if (pendingElementData.IsImage360)
            {
                DisplayImageOnSphere(pendingMediaPath);
            }
            else
            {
                GameObject videoObj = GameObject.FindGameObjectWithTag("VideoPlayer");
                if (videoObj != null)
                {
                    var vp = videoObj.GetComponent<VideoPlayer>();
                    if (vp != null)
                        PlayVideo(pendingMediaPath, vp);
                }
            }

            // Clear pending media
            pendingMediaPath = null;
            pendingElementData = null;
        }


        private void PlayVideo(string videoPath, VideoPlayer videoPlayer)
        {
            Debug.Log($"[PlayVideo] Attempting to load video from path: {videoPath}");
            videoPlayer.enabled = true;
            string fileName = Path.GetFileNameWithoutExtension(videoPath);
            LoadTransformData(fileName);

            float half = fadeOutDuration / 2f;
            sphereMaterial.SetFloat("_Visibility", 1f);

            // Load video from Resources
            VideoClip videoClip = Resources.Load<VideoClip>(videoPath);
            if (videoClip == null)
            {
                Debug.LogError($"[PlayVideo] Failed to load video from Resources: {videoPath}. Make sure the file exists in the Resources folder and is included in the build.");
                return;
            }
            Debug.Log($"[PlayVideo] Successfully loaded video: {videoPath}");

            // Create sequence: fade out -> clear texture -> update content -> fade in -> play audio
            sphereMaterial.DOFloat(0f, "_Visibility", half)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    // First set to black texture
                    sphereMaterial.SetTexture("_MainTex", blackTexture);
                    
                    // Small delay before setting new video
                    DOVirtual.DelayedCall(0.05f, () => 
                    {
                        videoPlayer.clip = videoClip;
                        videoPlayer.Play();

                        // Start fade in and play audio when fade-in is halfway done
                        sphereMaterial.DOFloat(1f, "_Visibility", half)
                            .SetEase(Ease.InOutSine)
                            .OnUpdate(() => 
                            {
                                float currentVisibility = sphereMaterial.GetFloat("_Visibility");
                                // Start audio when we're halfway through the fade-in
                                if (currentVisibility >= 0.5f && !audioSource.isPlaying)
                                {
                                    TryPlayAudio();
                                }
                            });
                    });
                });
        }


        private void DisplayImageOnSphere(string imagePath)
        {
            if (sphereMaterial == null)
            {
                Debug.LogError("[DisplayImageOnSphere] sphereMaterial is null");
                return;
            }

            Debug.Log($"[DisplayImageOnSphere] Attempting to load image from path: {imagePath}");
            // Load image from Resources
            Texture2D texture = Resources.Load<Texture2D>(imagePath);
            if (texture == null)
            {
                Debug.LogError($"[DisplayImageOnSphere] Failed to load image from Resources: {imagePath}. Make sure the file exists in the Resources folder and is included in the build.");
                return;
            }
            Debug.Log($"[DisplayImageOnSphere] Successfully loaded image: {imagePath}");

            GameObject playerObject = GameObject.FindGameObjectWithTag("VideoPlayer");
            if (playerObject != null)
            {
                var videoPlayer = playerObject.GetComponent<VideoPlayer>();
                if (videoPlayer != null)
                {
                    videoPlayer.Stop();
                    videoPlayer.enabled = false;
                }
            }

            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            LoadTransformData(fileName);

            texture.wrapMode = TextureWrapMode.Clamp;

            float half = fadeOutDuration / 2f;
            sphereMaterial.SetFloat("_Visibility", 1f);

            // Create sequence: fade out -> clear texture -> update content -> fade in -> play audio
            sphereMaterial.DOFloat(0f, "_Visibility", half)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    // First set to black texture
                    sphereMaterial.SetTexture("_MainTex", blackTexture);
                    
                    // Small delay before setting new texture to ensure clean transition
                    DOVirtual.DelayedCall(0.05f, () => 
                    {
                        sphereMaterial.SetTexture("_MainTex", texture);

                        // Start fade in and play audio when fade-in is halfway done
                        sphereMaterial.DOFloat(1f, "_Visibility", half)
                            .SetEase(Ease.InOutSine)
                            .OnUpdate(() => 
                            {
                                float currentVisibility = sphereMaterial.GetFloat("_Visibility");
                                // Start audio when we're halfway through the fade-in
                                if (currentVisibility >= 0.5f && !audioSource.isPlaying)
                                {
                                    TryPlayAudio();
                                }
                            });
                    });
                });
        }

        private void TryPlayAudio()
        {
            if (audioSource == null || string.IsNullOrEmpty(currentAudioPath)) return;

            // Stop any currently playing audio first
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            StartCoroutine(LoadAndPlayAudio(currentAudioPath));
        }

        private void LoadTransformData(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            // Load transform data from Resources
            TextAsset transformData = Resources.Load<TextAsset>($"Data/{fileName}");
            if (transformData == null)
            {
                Debug.LogWarning("[TransformLoader] No transform file found: " + fileName);
                return;
            }

            Vector3 position = videoSphere.position;
            Vector3 rotation = videoSphere.eulerAngles;
            string audioFileName = null;

            string[] lines = transformData.text.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("Position"))
                {
                    string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 4 &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        position = new Vector3(x, y, z);
                        Debug.Log($"[TransformLoader] Parsed position: {position}");
                    }
                }
                else if (trimmed.StartsWith("Rotation"))
                {
                    string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 4 &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        rotation = new Vector3(x, y, z);
                        Debug.Log($"[TransformLoader] Parsed rotation: {rotation}");
                    }
                }
                else if (trimmed.StartsWith("Audio:"))
                {
                    audioFileName = trimmed.Substring(6).Trim().Trim('"', ' ');
                    if (audioFileName.Equals("No", StringComparison.OrdinalIgnoreCase))
                    {
                        audioFileName = null;
                    }
                    else if (!audioFileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    {
                        audioFileName += ".wav";
                    }
                    Debug.Log($"[TransformLoader] Parsed audio file: {audioFileName ?? "No audio"}");
                }
            }

            videoSphere.position = position;
            videoSphere.rotation = Quaternion.Euler(rotation);

            Debug.Log($"[TransformLoader] Applied position {position} and rotation {rotation} to video sphere.");

            if (audioFileName != null)
            {
                HandleAudioPlayback(audioFileName);
            }
            else
            {
                if (audioSource != null)
                {
                    audioSource.Stop();
                    audioSource.clip = null;
                }
                currentAudioPath = null;
                isAudioPaused = false;
            }
        }

        private void HandleAudioPlayback(string audioFileName)
        {
            if (audioSource == null || string.IsNullOrEmpty(audioFileName))
            {
                currentAudioPath = null;
                return;
            }

            // Just store the audio path, don't play yet
            // Clean up the audio file name by removing the .wav extension if it exists
            string cleanFileName = audioFileName;
            if (cleanFileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                cleanFileName = cleanFileName.Substring(0, cleanFileName.Length - 4);
            }

            // Store the path for later playback
            currentAudioPath = $"Audio/{cleanFileName}";
            Debug.Log($"[Audio] Audio path stored: {currentAudioPath}");
        }


        private IEnumerator LoadAndPlayAudio(string audioPath)
        {
            if (string.IsNullOrEmpty(audioPath))
            {
                Debug.LogWarning("[Audio] Audio path is null or empty");
                yield break;
            }

            // Load audio from Resources
            AudioClip audioClip = Resources.Load<AudioClip>(audioPath);
            if (audioClip == null)
            {
                Debug.LogError($"[Audio] Failed to load audio from Resources: {audioPath}");
                yield break;
            }

            audioSource.clip = audioClip;
            audioSource.Play();

            Debug.Log("[Audio] Now playing: " + audioPath);
        }


        private string GetCategoryFromPath(string path)
        {
            // Safely get parent folder of the file
            DirectoryInfo parent = Directory.GetParent(path);
            return parent != null ? parent.Name : "Unknown";
        }



        private void FadeOutCanvas(float durationOverride = -1f)
        {
            float duration = durationOverride > 0f ? durationOverride : fadeOutDuration;

            var canvasRect = carousel.GetComponent<RectTransform>();
            if (canvasRect == null) return;

            CanvasGroup canvasGroup = canvasRect.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = canvasRect.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            canvasGroup.DOFade(0f, duration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => canvasRect.gameObject.SetActive(false));
        }

        private void FadeInCanvas()
        {
            var canvasRect = carousel.GetComponent<RectTransform>();
            if (canvasRect == null) return;

            CanvasGroup canvasGroup = canvasRect.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = canvasRect.gameObject.AddComponent<CanvasGroup>();

            canvasRect.gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            canvasGroup.DOFade(1f, fadeOutDuration)
                .SetEase(Ease.OutQuad);
        }
    }
}
