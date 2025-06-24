using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.Video;
using DG.Tweening;
using System.IO;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Collections;
using HKCarouselLayoutGroup;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public class XR360CarouselController : MonoBehaviour
{
    [Header("Carousel Control")]
    private HKCarouselLayoutGroup3DDemo carousel360;
    [SerializeField] private float inputThreshold = 0.5f;
    [SerializeField] private float scrollCooldown = 0.25f;
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("Materials & Sphere")]
    [SerializeField] private Material sphereMaterial;
    [SerializeField] private Transform videoSphere;
    [SerializeField] private GameObject additionalToggleObject;
    [SerializeField] private AudioSource audioSource;

    [Header("Rating System")]
    [SerializeField] private GameObject ratingPrefab;
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private float ratingPopupDistance = 2f;
    [SerializeField] private float ratingPopupHeight = 0f;

    [Header("Video Render Texture")]
    [SerializeField]
    private RenderTexture videoRenderTexture;  // Reference to the VideoRenderTexture asset

    [Header("Transition Effects")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float blackScreenDuration = 0.2f; // How long to stay black

    private float cooldownTimer = 0f;
    private List<InputDevice> devices = new();
    private Dictionary<InputDevice, bool> previousTriggerStates = new();
    private int triggerPressCount = 0;
    private string currentMediaPath = null;
    private bool isVideoPaused = false;
    private string currentAudioPath = null;
    private bool isAudioPaused = false;
    private bool isRatingPending = false;
    private GameObject activeRatingInstance;
    private string pendingMediaPath = null;
    private HKCarouselElementData pendingElementData = null;
    private bool isCarouselInteractive = true;
    private Texture2D blackTexture;

    private bool isSetup = false;

    // New simple state flags (rating flow removed)
    private bool isCarouselVisible = true;   // Carousel UI starts visible
    private int lastSelectedIndex = -1;      // Track last played media index

    private ColorAdjustments colorAdjustments;

    private string RatingFilePath
    {
        get
        {
            string directory = Path.Combine(Application.persistentDataPath, "Rating");
            Directory.CreateDirectory(directory); // Ensure directory exists
            return Path.Combine(directory, "ratings.json");
        }
    }

    private void CopyInitialRatingFile()
    {
        try
        {
            // Check if the source file exists in StreamingAssets
            string sourceFile = Path.Combine(Application.streamingAssetsPath, "Rating", "ratings.json");
            if (!File.Exists(sourceFile))
            {
                // Create an empty ratings file if source doesn't exist
                File.WriteAllText(RatingFilePath, "{}");
                return;
            }

            // Copy the file from StreamingAssets to persistentDataPath
            File.Copy(sourceFile, RatingFilePath, true);
            Debug.Log("[Rating] Successfully copied initial rating file to: " + RatingFilePath);
        }
        catch (Exception e)
        {
            Debug.Log("[Rating] Error copying initial rating file: " + e.Message);
            // Create an empty ratings file as fallback
            try
            {
                File.WriteAllText(RatingFilePath, "{}");
            }
            catch (Exception writeEx)
            {
                Debug.Log("[Rating] Failed to create empty rating file: " + writeEx.Message);
            }
        }
    }

    public void Initialize(HKCarouselLayoutGroup3DDemo carouselComponent)
    {
        carousel360 = carouselComponent;
        isSetup = true;
        
        // Initialize other components
        InputDevices.GetDevices(devices);
        
        blackTexture = new Texture2D(1, 1);
        blackTexture.SetPixel(0, 0, Color.black);
        blackTexture.Apply();

        if (sphereMaterial != null)
        {
            // Always keep the videoRenderTexture assigned to the material
            sphereMaterial.SetTexture("_MainTex", videoRenderTexture);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(CopyAndroidStreamingAssets());
#endif

        InitializeRatingSystem();
        
        // Load all assets into the carousel
        LoadAssetsFromPersistentDataPath();
    }

    private void Start()
    {
        // Only do minimal setup if not initialized externally
        if (!isSetup)
        {
            InputDevices.GetDevices(devices);
            
            // Try to find carousel in children if not set
            carousel360 = GetComponentInChildren<HKCarouselLayoutGroup3DDemo>();
            if (carousel360 == null)
            {
                Debug.Log("No HKCarouselLayoutGroup3DDemo found in children. Waiting for external initialization...");
            }

            // Ensure the videoRenderTexture is assigned to the material
            if (sphereMaterial != null)
            {
                sphereMaterial.SetTexture("_MainTex", videoRenderTexture);
            }

            // Get color adjustments component from volume
            if (postProcessVolume != null)
            {
                postProcessVolume.profile.TryGet(out colorAdjustments);
                if (colorAdjustments == null)
                {
                    Debug.LogWarning("[POST] No ColorAdjustments found in post-process volume!");
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (blackTexture != null)
        {
            DestroyImmediate(blackTexture, true);
        }

        // Clean up any texture that might be set on the material
        if (sphereMaterial != null)
        {
            var currentTexture = sphereMaterial.GetTexture("_MainTex");
            if (currentTexture != null && currentTexture != blackTexture && currentTexture != videoRenderTexture)
            {
                DestroyImmediate(currentTexture, true);
            }
            sphereMaterial.SetTexture("_MainTex", null);
        }
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy || !isSetup || carousel360 == null) return;

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
                    PlaySelectedMedia();
                }

                previousTriggerStates[device] = isTriggerHeld;
            }

            // THUMBSTICK
            if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                if (cooldownTimer <= 0f && isCarouselInteractive && carousel360 != null)
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
            }
        }
    }

    private void PlaySelectedMedia()
    {
        if (carousel360 == null) return;

        // CASE 1: Carousel is hidden –> show it again and pause media
        if (!isCarouselVisible)
        {
            ShowCarouselAndPauseMedia();
            return;
        }

        // CASE 2: Carousel visible –> attempt to play the selected media
        int index = carousel360.GetTrueSelectedIndex();
        var elementData = carousel360.GetElementDataFromIndex(index) as HKCarouselElementData;
        if (elementData == null) return;

        // Check if this is a scene link
        if (elementData.IsSceneLink && elementData.SceneIndex >= 0)
        {
            Debug.Log($"[SCENE] Loading scene index: {elementData.SceneIndex}");
            StartCoroutine(LoadSceneAsync(elementData.SceneIndex));
            return;
        }

        // Handle regular media
        if (string.IsNullOrEmpty(elementData.VideoPath)) return;

        // Always treat as new selection when carousel is visible
        if (isCarouselVisible)
        {
            lastSelectedIndex = index;
            currentMediaPath = elementData.VideoPath;

            // Hide carousel UI
            HideCarousel();

            // Display image or video depending on flag
            if (elementData.IsImage360)
            {
                HandleImage360Media(elementData, false, true);
            }
            else
            {
                GameObject videoObj = GameObject.FindGameObjectWithTag("VideoPlayer");
                if (videoObj != null)
                {
                    var vp = videoObj.GetComponent<VideoPlayer>();
                    if (vp != null)
                    {
                        PlayVideo(elementData.VideoPath, vp);
                    }
                }
            }
            return;
        }

        // If we get here, we're resuming the current media
        ResumeCurrentMedia();
    }

    // Helper – hide carousel (fade canvas out) and set flag
    private void HideCarousel()
    {
        FadeOutCanvas();
        isCarouselVisible = false;
    }

    // Helper – show carousel again and pause any playing media
    private void ShowCarouselAndPauseMedia()
    {
        // Pause video if any
        GameObject videoObj = GameObject.FindGameObjectWithTag("VideoPlayer");
        if (videoObj != null)
        {
            var vp = videoObj.GetComponent<VideoPlayer>();
            if (vp != null && vp.isPlaying)
            {
                vp.Pause();
                isVideoPaused = true;
            }
        }

        // Pause audio
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
            isAudioPaused = true;
        }

        FadeInCanvas(false);
        isCarouselVisible = true;
    }

    // Helper – resume media if it was paused
    private void ResumeCurrentMedia()
    {
        // If last media was video, resume it
        GameObject videoObj = GameObject.FindGameObjectWithTag("VideoPlayer");
        if (videoObj != null)
        {
            var vp = videoObj.GetComponent<VideoPlayer>();
            if (vp != null && isVideoPaused)
            {
                vp.Play();
                isVideoPaused = false;
            }
        }

        // Resume audio if needed
        if (audioSource != null && isAudioPaused)
        {
            audioSource.Play();
            isAudioPaused = false;
        }

        // Hide carousel again
        HideCarousel();
    }

    private void HandleImage360Media(HKCarouselElementData data, bool isSameMedia, bool shouldFadeOut)
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
    }

    private void HandleVideoMedia(HKCarouselElementData data, bool isSameMedia, bool shouldFadeOut)
    {
        GameObject videoObj = GameObject.FindGameObjectWithTag("VideoPlayer");
        if (videoObj == null)
        {
            Debug.LogError("No object tagged 'VideoPlayer' found.");
            return;
        }

        var vp = videoObj.GetComponent<VideoPlayer>();
        if (vp == null)
        {
            Debug.LogError("No VideoPlayer component found.");
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

    // Rating System Methods
    private void InitializeRatingSystem()
    {
#if !UNITY_EDITOR
        if (!File.Exists(RatingFilePath))
        {
            CopyInitialRatingFile();
        }
#endif
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

        var mainCamera = xrOrigin.GetComponentInChildren<Camera>();
        if (mainCamera == null)
        {
            Debug.LogError("[ShowRatingPopup] Cannot find camera in XR Origin!");
            return;
        }

        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 spawnPosition = mainCamera.transform.position + forward * ratingPopupDistance;
        spawnPosition.y += ratingPopupHeight;

        activeRatingInstance = Instantiate(ratingPrefab);
        activeRatingInstance.transform.position = spawnPosition;
        activeRatingInstance.transform.rotation = Quaternion.identity;

        var ratingSystem = activeRatingInstance.GetComponent<RatingSystem>();
        if (ratingSystem != null)
        {
            string experienceName = Path.GetFileNameWithoutExtension(currentMediaPath ?? "Unknown");
            ratingSystem.experienceName = experienceName;
            StartCoroutine(WaitForRatingComplete(ratingSystem));
        }
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

        pendingMediaPath = null;
        pendingElementData = null;
    }

    // Media Playback Methods
    private void PlayVideo(string videoPath, VideoPlayer videoPlayer)
    {
        Debug.Log($"[VIDEO] Original path: {videoPath}");
        
        string fullPath = GetCorrectPath(videoPath);
        Debug.Log($"[VIDEO] Using path: {fullPath}");
        
        videoPlayer.enabled = true;
        string fileName = Path.GetFileNameWithoutExtension(videoPath);
        LoadTransformData(fileName);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[VIDEO] Video file not found at path: {fullPath}");
            return;
        }

        // Configure VideoPlayer for best quality
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
        videoPlayer.targetMaterialRenderer = videoSphere.GetComponent<Renderer>();
        videoPlayer.targetMaterialProperty = "_MainTex";
        videoPlayer.aspectRatio = VideoAspectRatio.FitVertically;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);

#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android, we need the file:// protocol
        videoPlayer.url = "file://" + fullPath;
        Debug.Log($"[VIDEO] Set video URL for Android: {videoPlayer.url}");
#else
        videoPlayer.url = fullPath;
        Debug.Log($"[VIDEO] Set video URL for non-Android: {videoPlayer.url}");
#endif

        StartCoroutine(FadeToBlackAndPlayVideo(videoPlayer));
    }

    private IEnumerator FadeToBlackAndPlayVideo(VideoPlayer videoPlayer)
    {
        // Fade to black using both material and post-processing
        var fadeSequence = DOTween.Sequence();
        
        // Material fade
        fadeSequence.Join(sphereMaterial.DOFloat(0f, "_Visibility", fadeOutDuration)
            .SetEase(Ease.InOutSine));
            
        // Post-processing fade (if available)
        if (colorAdjustments != null)
        {
            fadeSequence.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                x => colorAdjustments.postExposure.value = x,
                -10f, fadeOutDuration)
                .SetEase(Ease.InOutSine));
        }

        yield return fadeSequence.WaitForCompletion();
        yield return new WaitForSeconds(blackScreenDuration);

        // Prepare video player
        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
        {
            Debug.Log("[VIDEO] Preparing video...");
            yield return null;
        }

        Debug.Log("[VIDEO] Video preparation completed, starting playback");
        videoPlayer.Play();

        // Fade back in
        fadeSequence = DOTween.Sequence();
        
        // Material fade
        fadeSequence.Join(sphereMaterial.DOFloat(1f, "_Visibility", fadeInDuration)
            .SetEase(Ease.InOutSine));
            
        // Post-processing fade (if available)
        if (colorAdjustments != null)
        {
            fadeSequence.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                x => colorAdjustments.postExposure.value = x,
                0f, fadeInDuration)
                .SetEase(Ease.InOutSine));
        }

        yield return fadeSequence.WaitForCompletion();

        // Start audio playback
        TryPlayAudio();
    }

    private void DisplayImageOnSphere(string imagePath)
    {
        Debug.Log($"[IMAGE] Original path: {imagePath}");
        
        string fullPath = GetCorrectPath(imagePath);
        Debug.Log($"[IMAGE] Converted path: {fullPath}");

        if (sphereMaterial == null)
        {
            Debug.LogError("[IMAGE] Sphere material is null!");
            return;
        }

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[IMAGE] Image file not found at path: {fullPath}");
            return;
        }

        StartCoroutine(FadeToBlackAndDisplayImage(fullPath));
    }

    private IEnumerator FadeToBlackAndDisplayImage(string fullPath)
    {
        // Fade to black using both material and post-processing
        var fadeSequence = DOTween.Sequence();
        
        // Material fade
        fadeSequence.Join(sphereMaterial.DOFloat(0f, "_Visibility", fadeOutDuration)
            .SetEase(Ease.InOutSine));
            
        // Post-processing fade (if available)
        if (colorAdjustments != null)
        {
            fadeSequence.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                x => colorAdjustments.postExposure.value = x,
                -10f, fadeOutDuration)
                .SetEase(Ease.InOutSine));
        }

        yield return fadeSequence.WaitForCompletion();
        yield return new WaitForSeconds(blackScreenDuration);

        // Load and process image outside try-catch
        byte[] fileData;
        try
        {
            fileData = File.ReadAllBytes(fullPath);
        }
        catch (Exception e)
        {
            Debug.LogError("[IMAGE] Error reading image file: " + e.Message);
            yield break;
        }

        Debug.Log($"[IMAGE] Successfully read {fileData.Length} bytes from file");

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(fileData))
        {
            Debug.LogError($"[IMAGE] Failed to load image data from: {fullPath}");
            DestroyImmediate(texture, true);
            yield break;
        }

        Debug.Log($"[IMAGE] Successfully loaded image: {fullPath} (Size: {texture.width}x{texture.height})");

        string fileName = Path.GetFileNameWithoutExtension(fullPath);
        LoadTransformData(fileName);

        texture.wrapMode = TextureWrapMode.Clamp;

        // Clear any existing texture first
        Texture currentTexture = sphereMaterial.GetTexture("_MainTex");
        if (currentTexture != null && currentTexture != blackTexture && currentTexture != videoRenderTexture)
        {
            DestroyImmediate(currentTexture, true);
        }
        sphereMaterial.SetTexture("_MainTex", null);

        // Set the new texture
        sphereMaterial.SetTexture("_MainTex", texture);

        // Fade back in
        fadeSequence = DOTween.Sequence();
        
        // Material fade
        fadeSequence.Join(sphereMaterial.DOFloat(1f, "_Visibility", fadeInDuration)
            .SetEase(Ease.InOutSine));
            
        // Post-processing fade (if available)
        if (colorAdjustments != null)
        {
            fadeSequence.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                x => colorAdjustments.postExposure.value = x,
                0f, fadeInDuration)
                .SetEase(Ease.InOutSine));
        }

        yield return fadeSequence.WaitForCompletion();

        // Start audio playback
        TryPlayAudio();
    }

    private string GetCorrectPath(string originalPath)
    {
        try
        {
            Debug.Log($"[PATH] Processing path: {originalPath}");

            // Remove file:/// prefix if present
            if (originalPath.StartsWith("file:///"))
            {
                originalPath = originalPath.Substring("file:///".Length);
                Debug.Log($"[PATH] Removed file:/// prefix, path is now: {originalPath}");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // Extract category and filename from the original path
            string[] pathParts = originalPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string mediaType = string.Empty;
            string category = string.Empty;
            string fileName = string.Empty;

            // Parse path parts to get mediaType, category and filename
            for (int i = 0; i < pathParts.Length; i++)
            {
                if (pathParts[i].Equals("Videos", StringComparison.OrdinalIgnoreCase) || 
                    pathParts[i].Equals("360 Images", StringComparison.OrdinalIgnoreCase) ||
                    pathParts[i].Equals("Audio", StringComparison.OrdinalIgnoreCase))
                {
                    mediaType = pathParts[i];
                    if (i + 2 < pathParts.Length) // Make sure we have category and filename
                    {
                        category = pathParts[i + 1];
                        fileName = pathParts[i + 2];
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(mediaType) || string.IsNullOrEmpty(category) || string.IsNullOrEmpty(fileName))
            {
                Debug.LogError($"[PATH] Failed to parse path parts from: {originalPath}");
                return originalPath;
            }

            string fullPath = $"/sdcard/Android/data/{Application.identifier}/files/{mediaType}/{category}/{fileName}";
            Debug.Log($"[PATH] Constructed Android path: {fullPath}");
            return fullPath;
#else
            // For other platforms or editor, use persistentDataPath
            if (originalPath.Contains("/Android/data/" + Application.identifier + "/files/"))
            {
                Debug.Log($"[PATH] Using direct path: {originalPath}");
                return originalPath;
            }

            string basePath = Application.persistentDataPath;
            string relativePath = originalPath;
            string[] prefixesToRemove = new[] { "Videos/", "360 Images/", "Audio/", "Videos\\", "360 Images\\", "Audio\\" };
            foreach (var prefix in prefixesToRemove)
            {
                if (relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = relativePath.Substring(prefix.Length);
                    break;
                }
            }

            // Determine media type from original path
            string mediaType;
            if (originalPath.ToLower().Contains("video"))
                mediaType = "Videos";
            else if (originalPath.ToLower().Contains("audio"))
                mediaType = "Audio";
            else
                mediaType = "360 Images";

            string fullPath = Path.Combine(basePath, mediaType, relativePath);
            Debug.Log($"[PATH] Constructed non-Android path: {fullPath}");
            return fullPath;
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"[PATH] Error in GetCorrectPath: {e.Message}");
            return originalPath;
        }
    }

    // Audio Methods
    private void HandleAudioPlayback(string audioFileName)
    {
        Debug.Log($"[AUDIO] Starting HandleAudioPlayback with file: '{audioFileName}'");

        if (audioSource == null)
        {
            Debug.LogError("[AUDIO] AudioSource component is null! Please assign it in the inspector!");
            return;
        }

        // Clean up the audio file name
        string cleanFileName = audioFileName;
        Debug.Log($"[AUDIO] Looking for audio file: '{cleanFileName}'");

        // Try to load the audio clip from Resources/Audio with different extensions
        AudioClip audioClip = null;
        string[] extensions = new[] { "", ".wav", ".mp3" };
        
        foreach (var ext in extensions)
        {
            string path = $"Audio/{cleanFileName}{ext}";
            Debug.Log($"[AUDIO] Attempting to load from Resources path: '{path}'");
            audioClip = Resources.Load<AudioClip>(path);
            if (audioClip != null)
            {
                Debug.Log($"[AUDIO] Successfully loaded audio from: '{path}'");
                break;
            }
        }

        if (audioClip == null)
        {
            Debug.LogError($"[AUDIO] Failed to load audio clip for: '{cleanFileName}'. Make sure the file exists in Resources/Audio folder!");
            return;
        }

        // Stop any currently playing audio
        if (audioSource.isPlaying)
        {
            Debug.Log("[AUDIO] Stopping current audio");
            audioSource.Stop();
        }

        // Set up the audio source
        audioSource.clip = audioClip;
        currentAudioPath = $"Audio/{cleanFileName}";
        isAudioPaused = false;

        Debug.Log($"[AUDIO] Setup complete - Clip: {audioClip.name}, Duration: {audioClip.length}s");
        Debug.Log($"[AUDIO] AudioSource state - HasClip: {audioSource.clip != null}, Volume: {audioSource.volume}, Mute: {audioSource.mute}");
    }

    private void TryPlayAudio()
    {
        Debug.Log("[AUDIO] TryPlayAudio called");

        if (audioSource == null)
        {
            Debug.LogError("[AUDIO] AudioSource is null!");
            return;
        }

        if (string.IsNullOrEmpty(currentAudioPath))
        {
            Debug.LogError("[AUDIO] No audio path set!");
            return;
        }

        Debug.Log($"[AUDIO] Current state - HasClip: {audioSource.clip != null}, IsPlaying: {audioSource.isPlaying}, Path: {currentAudioPath}");

        if (audioSource.clip == null)
        {
            Debug.Log($"[AUDIO] Loading clip from path: {currentAudioPath}");
            AudioClip audioClip = Resources.Load<AudioClip>(currentAudioPath);
            if (audioClip == null)
            {
                // Try with extensions if direct path fails
                string[] extensions = new[] { ".wav", ".mp3" };
                foreach (var ext in extensions)
                {
                    string fullPath = $"{currentAudioPath}{ext}";
                    Debug.Log($"[AUDIO] Trying path with extension: {fullPath}");
                    audioClip = Resources.Load<AudioClip>(fullPath);
                    if (audioClip != null)
                    {
                        Debug.Log($"[AUDIO] Found audio with extension: {ext}");
                        break;
                    }
                }

                if (audioClip == null)
                {
                    Debug.LogError($"[AUDIO] Failed to load audio clip from: {currentAudioPath}");
                    return;
                }
            }
            audioSource.clip = audioClip;
        }

        Debug.Log($"[AUDIO] About to play - Clip: {audioSource.clip.name}, Volume: {audioSource.volume}, Mute: {audioSource.mute}");
        audioSource.Play();
        Debug.Log($"[AUDIO] Started playing: {currentAudioPath}, IsPlaying: {audioSource.isPlaying}");
    }

    // Transform Data Methods
    private void LoadTransformData(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[TRANSFORM] fileName is null or empty");
            return;
        }

        Debug.Log($"[TRANSFORM] Looking for data file: {fileName}");

        // Load transform data from Resources/Data folder
        TextAsset transformData = Resources.Load<TextAsset>($"Data/{fileName}");
        if (transformData == null)
        {
            // Try with .txt extension if not found
            transformData = Resources.Load<TextAsset>($"Data/{fileName}.txt");
            if (transformData == null)
            {
                Debug.LogError($"[TRANSFORM] No data file found in Resources/Data for: {fileName}");
                return;
            }
        }

        Debug.Log($"[TRANSFORM] Successfully loaded data file for: {fileName}");
        Debug.Log($"[TRANSFORM] File contents:\n{transformData.text}");

        Vector3 position = Vector3.zero;
        Vector3 rotation = Vector3.zero;
        string audioFileName = null;

        string[] lines = transformData.text.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            Debug.Log($"[TRANSFORM] Processing line: '{trimmed}'");

            if (trimmed.StartsWith("Position"))
            {
                ParsePosition(line, ref position);
                Debug.Log($"[TRANSFORM] Parsed position: {position}");
            }
            else if (trimmed.StartsWith("Rotation"))
            {
                ParseRotation(line, ref rotation);
                Debug.Log($"[TRANSFORM] Parsed rotation: {rotation}");
            }
            else if (trimmed.StartsWith("Audio:"))
            {
                audioFileName = trimmed.Substring("Audio:".Length).Trim();
                Debug.Log($"[TRANSFORM] Found audio file name: '{audioFileName}'");
            }
        }

        // Apply transform data
        if (videoSphere != null)
        {
            videoSphere.localPosition = position;
            videoSphere.localEulerAngles = rotation;
            Debug.Log($"[TRANSFORM] Applied transform - Position: {position}, Rotation: {rotation}");
        }

        // Handle audio if specified
        if (!string.IsNullOrEmpty(audioFileName))
        {
            Debug.Log($"[TRANSFORM] Calling HandleAudioPlayback with: '{audioFileName}'");
            HandleAudioPlayback(audioFileName);
        }
        else
        {
            // Clear audio state if no audio specified
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
                currentAudioPath = null;
                isAudioPaused = false;
                Debug.Log("[TRANSFORM] No audio specified, cleared audio state");
            }
        }
    }

    private void ParsePosition(string line, ref Vector3 position)
    {
        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4 &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            position = new Vector3(x, y, z);
        }
    }

    private void ParseRotation(string line, ref Vector3 rotation)
    {
        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4 &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            rotation = new Vector3(x, y, z);
        }
    }

    // UI Methods
    private void FadeOutCanvas()
    {
        if (additionalToggleObject != null)
        {
            additionalToggleObject.SetActive(false);
        }

        var canvasGroup = GetOrAddCanvasGroup();
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            isCarouselInteractive = false;

            canvasGroup.DOFade(0f, fadeOutDuration)
                .SetEase(Ease.OutQuad);
        }
    }

    private void FadeInCanvas(bool clearTexture = true)
    {
        if (additionalToggleObject != null)
        {
            additionalToggleObject.SetActive(true);
        }

        if (clearTexture && sphereMaterial != null)
        {
            sphereMaterial.SetTexture("_MainTex", null);
        }

        var canvasGroup = GetOrAddCanvasGroup();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            isCarouselInteractive = true;

            canvasGroup.DOFade(1f, fadeOutDuration)
                .SetEase(Ease.OutQuad);
        }
    }

    private CanvasGroup GetOrAddCanvasGroup()
    {
        var canvasRect = carousel360?.GetComponent<RectTransform>();
        if (canvasRect == null) return null;

        var canvasGroup = canvasRect.GetComponent<CanvasGroup>();
        return canvasGroup ?? canvasRect.gameObject.AddComponent<CanvasGroup>();
    }

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

    private void UpdateProgress(string status, float progress)
    {
        Debug.Log($"Progress: {status} ({progress:P0})");
        // If you have a UI element to show progress, update it here
    }

    private IEnumerator HideProgressAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // If you have a UI element showing progress, hide it here
    }

    private IEnumerator CopyAndroidStreamingAssets()
    {
        string[] folders = new[] { "Videos", "360 Images", "Audio", "Data" };
        float progressPerFolder = 1f / folders.Length;
        float currentProgress = 0f;

        foreach (string folder in folders)
        {
            UpdateProgress($"Copying {folder}...", currentProgress);
            
            string sourcePath = Path.Combine(Application.streamingAssetsPath, folder);
            string targetPath = Path.Combine(Application.persistentDataPath, folder);
            
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            // Copy all subfolders and files
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                string newDirPath = dirPath.Replace(sourcePath, targetPath);
                if (!Directory.Exists(newDirPath))
                {
                    Directory.CreateDirectory(newDirPath);
                }
            }

            foreach (string filePath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                string newFilePath = filePath.Replace(sourcePath, targetPath);
                File.Copy(filePath, newFilePath, true);
                
                Debug.Log($"Copied file: {Path.GetFileName(filePath)} to {newFilePath}");
            }

            currentProgress += progressPerFolder;
            yield return null;
        }

        UpdateProgress("All files copied successfully!", 1f);
        yield return StartCoroutine(HideProgressAfterDelay(2f));
    }

    void LoadAssetsFromPersistentDataPath()
    {
        var loadedElements = new List<HKCarouselElementData>();

        // Add Home card at the beginning
        loadedElements.Add(new HKCarouselElementData
        {
            Name = "Home",
            Category = "Navigation",
            IsSceneLink = true,
            SceneIndex = 1,
            ThumbnailPath = "Thumbnails/home"
        });

        // Videos
        string videosRoot = Path.Combine(Application.persistentDataPath, "Videos");
        if (Directory.Exists(videosRoot))
        {
            foreach (var categoryDir in Directory.GetDirectories(videosRoot))
            {
                string category = Path.GetFileName(categoryDir);
                foreach (var videoFile in Directory.GetFiles(categoryDir, "*.mp4"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(videoFile);
                    string relativePath = Path.Combine("Videos", category, Path.GetFileName(videoFile));
                    
                    loadedElements.Add(new HKCarouselElementData
                    {
                        Name = fileName,
                        Category = category,
                        VideoPath = relativePath,
                        ThumbnailPath = $"Thumbnails/Videos/{category}/{fileName}",
                        IsImage360 = false
                    });
                    
                    Debug.Log($"[CAROUSEL] Added video: {fileName} from category: {category}");
                }
            }
        }

        // 360 Images
        string imagesRoot = Path.Combine(Application.persistentDataPath, "360 Images");
        if (Directory.Exists(imagesRoot))
        {
            foreach (var categoryDir in Directory.GetDirectories(imagesRoot))
            {
                string category = Path.GetFileName(categoryDir);
                foreach (var imageFile in Directory.GetFiles(categoryDir, "*.*"))
                {
                    string ext = Path.GetExtension(imageFile).ToLower();
                    if (ext == ".jpg" || ext == ".png")
                    {
                        string fileName = Path.GetFileNameWithoutExtension(imageFile);
                        string relativePath = Path.Combine("360 Images", category, Path.GetFileName(imageFile));
                        
                        loadedElements.Add(new HKCarouselElementData
                        {
                            Name = fileName,
                            Category = category,
                            VideoPath = relativePath,
                            ThumbnailPath = $"Thumbnails/360 Images/{category}/{fileName}",
                            IsImage360 = true
                        });
                        
                        Debug.Log($"[CAROUSEL] Added 360 image: {fileName} from category: {category}");
                    }
                }
            }
        }

        // Sort elements by category and name, keeping Home first
        loadedElements.Sort((a, b) => {
            // Always keep Home first
            if (a.IsSceneLink) return -1;
            if (b.IsSceneLink) return 1;

            int catCompare = string.Compare(a.Category, b.Category, System.StringComparison.OrdinalIgnoreCase);
            if (catCompare != 0) return catCompare;
            return string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
        });

        Debug.Log($"[CAROUSEL] Total elements loaded: {loadedElements.Count}");

        // Use reflection to set the carousel elements
        var carouselType = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>);
        var elementsField = carouselType.GetField("_carouselElements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var defaultIndexField = carouselType.GetField("_defaultSelectedIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (elementsField != null && carousel360 != null)
        {
            elementsField.SetValue(carousel360, loadedElements);

            // Set default selected index to middle if there are elements
            if (loadedElements.Count > 0 && defaultIndexField != null)
            {
                int middleIndex = Mathf.FloorToInt(loadedElements.Count / 2f);
                defaultIndexField.SetValue(carousel360, middleIndex);
            }

            // Force refresh
            var refreshMethod = carouselType.GetMethod("RefreshItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var updateMethod = carouselType.GetMethod("UpdateCarousel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (refreshMethod != null && updateMethod != null)
            {
                refreshMethod.Invoke(carousel360, null);
                updateMethod.Invoke(carousel360, null);
                Debug.Log("[CAROUSEL] Carousel refreshed and updated");
            }
        }
        else
        {
            Debug.LogError("[CAROUSEL] Failed to set carousel elements - missing field or carousel reference");
        }
    }

    private IEnumerator LoadSceneAsync(int sceneIndex)
    {
        // Start loading the scene asynchronously
        AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneIndex);
        asyncLoad.allowSceneActivation = false;

        // Optional: Show loading progress
        float targetProgress = 0.9f; // AsyncOperation goes to 0.9 until allowSceneActivation is true
        
        while (asyncLoad.progress < targetProgress)
        {
            float progress = asyncLoad.progress / targetProgress;
            Debug.Log($"[SCENE] Loading progress: {progress:P0}");
            yield return null;
        }

        // Optional: Add a small delay to ensure everything is ready
        yield return new WaitForSeconds(0.5f);

        // Activate the scene
        asyncLoad.allowSceneActivation = true;
    }
} 