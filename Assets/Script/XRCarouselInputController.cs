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
        [SerializeField] private HKCarouselLayoutGroup3DDemo carousel360;  // For 360 UI
        [SerializeField] private HKSceneCarouselLayoutGroup3D carouselScene;  // For Scene UI
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
        [SerializeField] private Transform xrOrigin; // Reference to XR Origin transform
        [SerializeField] private float ratingPopupDistance = 2f; // Distance in meters from XR Origin
        [SerializeField] private float ratingPopupHeight = 0f; // Vertical offset from camera forward

        private bool isRatingPending = false;
        private GameObject activeRatingInstance;
        private string pendingMediaPath = null;
        private HKCarouselElementData pendingElementData = null;

        private Texture2D blackTexture;

        // Add new field to track if carousel is interactive
        private bool isCarouselInteractive = true;

        private string RatingFilePath => 
#if UNITY_EDITOR
            "Rating/Rating";  // Resources path for editor
#else
            Path.Combine(Application.persistentDataPath, "Rating.json");  // File path for builds
#endif

        [System.Serializable]
        public class RatingDataWrapper
        {
            public List<RatingEntry> entries = new List<RatingEntry>();
        }

        [System.Serializable]
        public class RatingEntry
        {
            public string key;
            public RatingData value;
        }

        [System.Serializable]
        public class RatingData
        {
            public float average;
            public int count;
        }

        void Start()
        {
            InputDevices.GetDevices(devices);
            
            // Create a black texture to use as default/transition
            blackTexture = new Texture2D(1, 1);
            blackTexture.SetPixel(0, 0, Color.black);
            blackTexture.Apply();

            // Set initial state of sphere material to have no texture
            if (sphereMaterial != null)
            {
                sphereMaterial.SetTexture("_MainTex", null);
            }

            // Initialize rating system
            InitializeRatingSystem();
        }

        private void InitializeRatingSystem()
        {
#if !UNITY_EDITOR
            // In builds, check if we need to copy the initial rating file
            if (!File.Exists(RatingFilePath))
            {
                CopyInitialRatingFile();
            }
#endif
        }

        private void CopyInitialRatingFile()
        {
            try
            {
                // Load the initial rating data from Resources
                TextAsset ratingJson = Resources.Load<TextAsset>("Rating/Rating");
                if (ratingJson != null)
                {
                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(RatingFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Write the initial rating data to persistent path
                    File.WriteAllText(RatingFilePath, ratingJson.text);
                    Debug.Log($"[Rating] Initial rating file created at: {RatingFilePath}");
                }
                else
                {
                    Debug.LogWarning("[Rating] No initial rating file found in Resources.");
                    // Create empty rating file
                    SaveRatingData(new Dictionary<string, RatingData>());
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rating] Failed to copy initial rating file: {e.Message}");
                // Create empty rating file as fallback
                SaveRatingData(new Dictionary<string, RatingData>());
            }
        }

        public Dictionary<string, RatingData> LoadRatingData()
        {
#if UNITY_EDITOR
            // In editor, load from Resources
            TextAsset ratingJson = Resources.Load<TextAsset>("Rating/Rating");
            if (ratingJson == null)
            {
                Debug.LogWarning("[Rating] No rating file found in Resources.");
                return new Dictionary<string, RatingData>();
            }
            var wrapper = JsonUtility.FromJson<RatingDataWrapper>(ratingJson.text);
            return ConvertFromWrapper(wrapper);
#else
            // In builds, load from persistent data path
            if (!File.Exists(RatingFilePath))
            {
                Debug.LogWarning($"[Rating] No rating file found at: {RatingFilePath}");
                return new Dictionary<string, RatingData>();
            }
            string json = File.ReadAllText(RatingFilePath);
            var wrapper = JsonUtility.FromJson<RatingDataWrapper>(json);
            return ConvertFromWrapper(wrapper);
#endif
        }

        private Dictionary<string, RatingData> ConvertFromWrapper(RatingDataWrapper wrapper)
        {
            var dict = new Dictionary<string, RatingData>();
            if (wrapper?.entries != null)
            {
                foreach (var entry in wrapper.entries)
                {
                    dict[entry.key] = entry.value;
                }
            }
            return dict;
        }

        private RatingDataWrapper ConvertToWrapper(Dictionary<string, RatingData> dict)
        {
            var wrapper = new RatingDataWrapper();
            foreach (var kvp in dict)
            {
                wrapper.entries.Add(new RatingEntry { key = kvp.Key, value = kvp.Value });
            }
            return wrapper;
        }

        private void SaveRatingData(Dictionary<string, RatingData> ratingData)
        {
            var wrapper = ConvertToWrapper(ratingData);
            string json = JsonUtility.ToJson(wrapper, true); // true for pretty print

#if UNITY_EDITOR
            // In editor, save to Resources
            string resourcesPath = Path.Combine(Application.dataPath, "Resources", "Rating");
            if (!Directory.Exists(resourcesPath))
            {
                Directory.CreateDirectory(resourcesPath);
            }
            string filePath = Path.Combine(resourcesPath, "Rating.json");
            File.WriteAllText(filePath, json);
            Debug.Log($"[Rating] Saved rating data to Resources: {filePath}");
#else
            // In builds, save to persistent data path
            File.WriteAllText(RatingFilePath, json);
            Debug.Log($"[Rating] Saved rating data to: {RatingFilePath}");
#endif
        }

        public void UpdateRating(string experienceName, float rating)
        {
            var ratingData = LoadRatingData();
            
            if (!ratingData.ContainsKey(experienceName))
            {
                ratingData[experienceName] = new RatingData { average = rating, count = 1 };
            }
            else
            {
                var current = ratingData[experienceName];
                float totalRating = current.average * current.count;
                current.count++;
                current.average = (totalRating + rating) / current.count;
                ratingData[experienceName] = current;
            }

            SaveRatingData(ratingData);
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
            // Remove the active check since we want to process input for both UIs
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
                        PlaySelectedMedia();
                    }

                    previousTriggerStates[device] = isTriggerHeld;
                }

                // THUMBSTICK - Process for both carousels based on which UI is active
                if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
                {
                    if (cooldownTimer <= 0f)
                    {
                        GameObject ui360 = GameObject.FindGameObjectWithTag("360UI");
                        GameObject sceneUI = GameObject.FindGameObjectWithTag("SceneUI");

                        // Handle 360 UI Carousel
                        if (ui360 != null && ui360.activeInHierarchy && isCarouselInteractive && carousel360 != null)
                        {
                            if (axis.x > inputThreshold)
                            {
                                carousel360.SimulateScroll(-1);
                                cooldownTimer = scrollCooldown;
                            }
                            else if (axis.x < -inputThreshold)
                            {
                                carousel360.SimulateScroll(1);
                                cooldownTimer = scrollCooldown;
                            }
                        }
                        // Handle Scene UI Carousel
                        else if (sceneUI != null && sceneUI.activeInHierarchy && carouselScene != null)
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
        }

        private void PlaySelectedMedia()
        {
            Debug.Log("PlaySelectedMedia called");
            
            // First check which UI is active
            GameObject sceneUI = GameObject.FindGameObjectWithTag("SceneUI");
            GameObject ui360 = GameObject.FindGameObjectWithTag("360UI");

            Debug.Log($"UI Status - SceneUI: {(sceneUI != null ? (sceneUI.activeInHierarchy ? "Active" : "Inactive") : "Not Found")}, " +
                     $"360UI: {(ui360 != null ? (ui360.activeInHierarchy ? "Active" : "Inactive") : "Not Found")}");

            // If Scene UI is active, handle scene loading
            if (sceneUI != null && sceneUI.activeInHierarchy)
            {
                Debug.Log("Scene UI is active, attempting to load scene...");
                
                // Get the carousel directly from the SceneUI object with the correct type
                var sceneCarousel = sceneUI.GetComponent<HKSceneCarouselLayoutGroup3D>();
                Debug.Log($"Scene Carousel component found: {sceneCarousel != null}");
                
                if (sceneCarousel == null)
                {
                    Debug.LogError("Could not find scene carousel component on SceneUI object!");
                    return;
                }

                int currentIndex = sceneCarousel.GetTrueSelectedIndex();
                Debug.Log($"Current carousel index: {currentIndex}");
                
                var sceneData = sceneCarousel.GetElementDataFromIndex(currentIndex);
                Debug.Log($"Scene data retrieved: {(sceneData != null ? "Yes" : "No")}");
                
                if (sceneData == null)
                {
                    Debug.LogError($"No scene data found for index: {currentIndex}");
                    return;
                }

                Debug.Log($"Scene Data - Name: '{sceneData.sceneName}', Index: {sceneData.sceneIndex}, Description: '{sceneData.description}'");

                // Try loading by name first
                if (!string.IsNullOrEmpty(sceneData.sceneName))
                {
                    try
                    {
                        Debug.Log($"Attempting to load scene by name: {sceneData.sceneName}");
                        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneData.sceneName);
                        Debug.Log($"Successfully initiated scene load by name: {sceneData.sceneName}");
                        return;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to load scene by name: {sceneData.sceneName}. Error: {e.Message}. Trying index instead...");
                    }
                }

                // If name loading failed or wasn't possible, try loading by index
                if (sceneData.sceneIndex >= 0)
                {
                    try
                    {
                        Debug.Log($"Attempting to load scene by index: {sceneData.sceneIndex}");
                        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneData.sceneIndex);
                        Debug.Log($"Successfully initiated scene load by index: {sceneData.sceneIndex}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to load scene by index: {sceneData.sceneIndex}. Error: {e.Message}");
                        Debug.LogError("Make sure the scene is added to Build Settings and the build index matches!");
                    }
                }
                else
                {
                    Debug.LogError($"Invalid scene index: {sceneData.sceneIndex} for scene: {sceneData.sceneName}");
                }
                return;
            }
            // If 360 UI is active, handle 360 content
            else if (ui360 != null && ui360.activeInHierarchy)
            {
                if (carousel360 == null || isRatingPending) return;

                int index = carousel360.GetTrueSelectedIndex();
                var elementData = carousel360.GetElementDataFromIndex(index);

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

                // Handle 360 image
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

                        FadeInCanvas(false);
                    }
                    return;
                }

                // Handle video
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

                    FadeInCanvas(false);
                }
            }
        }

        private void ShowRatingPopup()
        {
            if (ratingPrefab == null || isRatingPending) return;

            if (xrOrigin == null)
            {
                Debug.LogError("[ShowRatingPopup] XR Origin reference is missing!");
                return;
            }

            isRatingPending = true;

            // Find the camera (assuming it's a child of XR Origin)
            var mainCamera = xrOrigin.GetComponentInChildren<Camera>();
            if (mainCamera == null)
            {
                Debug.LogError("[ShowRatingPopup] Cannot find camera in XR Origin!");
                return;
            }

            // Calculate spawn position
            Vector3 forward = mainCamera.transform.forward;
            forward.y = 0; // Zero out vertical component for consistent height
            forward.Normalize();

            // Calculate position: camera position + forward direction * distance + height offset
            Vector3 spawnPosition = mainCamera.transform.position + forward * ratingPopupDistance;
            spawnPosition.y += ratingPopupHeight; // Add height offset

            // Instantiate the rating popup with identity rotation (0,0,0)
            activeRatingInstance = Instantiate(ratingPrefab);
            
            // Set position and keep original rotation
            activeRatingInstance.transform.position = spawnPosition;
            activeRatingInstance.transform.rotation = Quaternion.identity;

            var ratingSystem = activeRatingInstance.GetComponent<RatingSystem>();
            if (ratingSystem != null)
            {
                string experienceName = "Unknown";
                if (!string.IsNullOrEmpty(currentMediaPath))
                {
                    string fileName = Path.GetFileNameWithoutExtension(currentMediaPath);
                    experienceName = fileName;
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
                sphereMaterial.SetTexture("_MainTex", null);
                return;
            }
            Debug.Log($"[PlayVideo] Successfully loaded video: {videoPath}");

            // Create sequence: fade out -> clear old content -> update content -> fade in -> play audio
            sphereMaterial.DOFloat(0f, "_Visibility", half)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    // Only clear texture when switching to new content
                    if (videoPlayer.clip != videoClip)
                    {
                        sphereMaterial.SetTexture("_MainTex", null);
                    }
                    
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

            // Create sequence: fade out -> clear old content -> update content -> fade in -> play audio
            sphereMaterial.DOFloat(0f, "_Visibility", half)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    // Only clear if we're switching to a new texture
                    Texture currentTexture = sphereMaterial.GetTexture("_MainTex");
                    if (currentTexture != texture)
                    {
                        sphereMaterial.SetTexture("_MainTex", null);
                    }
                    
                    // Small delay before setting new texture
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

        private void FadeOutCanvas()
        {
            GameObject ui360 = GameObject.FindGameObjectWithTag("360UI");
            GameObject sceneUI = GameObject.FindGameObjectWithTag("SceneUI");
            RectTransform canvasRect = null;

            // Get the active carousel's RectTransform
            if (ui360 != null && ui360.activeInHierarchy && carousel360 != null)
            {
                canvasRect = carousel360.GetComponent<RectTransform>();
            }
            else if (sceneUI != null && sceneUI.activeInHierarchy && carouselScene != null)
            {
                canvasRect = carouselScene.GetComponent<RectTransform>();
            }

            if (canvasRect == null) return;

            CanvasGroup canvasGroup = canvasRect.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = canvasRect.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            isCarouselInteractive = false;

            canvasGroup.DOFade(0f, fadeOutDuration)
                .SetEase(Ease.OutQuad);
        }

        private void FadeInCanvas(bool clearTexture = true)
        {
            GameObject ui360 = GameObject.FindGameObjectWithTag("360UI");
            GameObject sceneUI = GameObject.FindGameObjectWithTag("SceneUI");
            RectTransform canvasRect = null;

            // Get the active carousel's RectTransform
            if (ui360 != null && ui360.activeInHierarchy && carousel360 != null)
            {
                canvasRect = carousel360.GetComponent<RectTransform>();
            }
            else if (sceneUI != null && sceneUI.activeInHierarchy && carouselScene != null)
            {
                canvasRect = carouselScene.GetComponent<RectTransform>();
            }

            if (canvasRect == null) return;

            if (clearTexture && sphereMaterial != null)
            {
                sphereMaterial.SetTexture("_MainTex", null);
            }

            CanvasGroup canvasGroup = canvasRect.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = canvasRect.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            isCarouselInteractive = true;

            canvasGroup.DOFade(1f, fadeOutDuration)
                .SetEase(Ease.OutQuad);
        }

        // Optional: Add method to visualize the spawn position in the editor
        private void OnDrawGizmosSelected()
        {
            if (xrOrigin != null)
            {
                var mainCamera = xrOrigin.GetComponentInChildren<Camera>();
                if (mainCamera != null)
                {
                    Vector3 forward = mainCamera.transform.forward;
                    forward.y = 0;
                    forward.Normalize();
                    
                    Vector3 spawnPosition = mainCamera.transform.position + forward * ratingPopupDistance;
                    spawnPosition.y += ratingPopupHeight;
                    
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(spawnPosition, 0.2f);
                    Gizmos.DrawLine(mainCamera.transform.position, spawnPosition);
                }
            }
        }
    }
}
