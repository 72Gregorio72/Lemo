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
using System.Linq;
using System.Threading.Tasks;


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

    private List<HKCarouselElementData> cachedElements;
    private Dictionary<string, bool> fileExistsCache = new Dictionary<string, bool>();
    private const int BATCH_SIZE = 10; // Number of items to load per frame

    private async Task<byte[]> LoadFileAsync(string path)
    {
        using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            byte[] data = new byte[fs.Length];
            await fs.ReadAsync(data, 0, (int)fs.Length);
            return data;
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

        // Add debug logging for carousel state
        Debug.Log($"[MEDIA] Carousel visible: {isCarouselVisible}");
        Debug.Log($"[MEDIA] Last selected index: {lastSelectedIndex}");

        // CASE 1: Carousel is hidden –> show it again and pause media
        if (!isCarouselVisible)
        {
            ShowCarouselAndPauseMedia();
            return;
        }

        // CASE 2: Carousel visible –> attempt to play the selected media
        int selectedIndex = carousel360.GetTrueSelectedIndex(); // Get the true index
        Debug.Log($"[MEDIA] Selected Index: {selectedIndex}");
        
        // Get the element data and log its details
        var elementData = carousel360.GetElementDataFromIndex(selectedIndex) as HKCarouselElementData;
        if (elementData == null)
        {
            Debug.LogError("[MEDIA] Failed to get element data");
            return;
        }

        Debug.Log($"[MEDIA] Selected Element - Name: {elementData.Name}, Category: {elementData.Category}, IsSceneLink: {elementData.IsSceneLink}");
        Debug.Log($"[MEDIA] Selected media path: {elementData.VideoPath}");

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
            lastSelectedIndex = selectedIndex;
            currentMediaPath = elementData.VideoPath;

            // Hide carousel UI
            HideCarousel();

            // Display image or video depending on flag
            if (elementData.IsImage360)
            {
                Debug.Log($"[MEDIA] Playing 360 Image at index {selectedIndex}: {elementData.Name}");
                HandleImage360Media(elementData, false, true);
            }
            else
            {
                Debug.Log($"[MEDIA] Playing Video at index {selectedIndex}: {elementData.Name}");
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

        // Extract category from the original path
        string category = ExtractCategoryFromPath(imagePath);
        string fileName = Path.GetFileNameWithoutExtension(imagePath);
        
        // Start the fade and display coroutine, passing transform data
        StartCoroutine(FadeToBlackAndDisplayImage(fullPath, fileName, category));
    }

    private string ExtractCategoryFromPath(string path)
    {
        try
        {
            // Extract category from path like "360 Images/Nature/filename.png"
            string[] pathParts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                if (pathParts[i].Equals("360 Images", StringComparison.OrdinalIgnoreCase) || 
                    pathParts[i].Equals("Videos", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < pathParts.Length)
                    {
                        string category = pathParts[i + 1];
                        Debug.Log($"[CATEGORY] Extracted category: {category} from path: {path}");
                        return category;
                    }
                }
            }
            
            Debug.LogWarning($"[CATEGORY] Could not extract category from path: {path}");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CATEGORY] Error extracting category: {e.Message}");
            return null;
        }
    }

    private IEnumerator FadeToBlackAndDisplayImage(string fullPath, string fileName = null, string category = null)
    {
        Debug.Log("[IMAGE] Starting fade to black and load sequence");

        // STEP 1: Fade to black (wait for completion)
        var fadeDownSequence = DOTween.Sequence();
        
        // Material fade down
        fadeDownSequence.Join(sphereMaterial.DOFloat(0f, "_Visibility", fadeOutDuration)
            .SetEase(Ease.InOutSine));
            
        // Post-processing fade down (if available)
        if (colorAdjustments != null)
        {
            fadeDownSequence.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                x => colorAdjustments.postExposure.value = x,
                -10f, fadeOutDuration)
                .SetEase(Ease.InOutSine));
        }

        yield return fadeDownSequence.WaitForCompletion();
        Debug.Log("[IMAGE] Fade to black completed, now loading everything...");

        // STEP 2: Load image data
        Debug.Log("[IMAGE] Loading image file...");
        var loadTask = LoadFileAsync(fullPath);
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }

        byte[] fileData = loadTask.Result;
        Debug.Log("[IMAGE] Image file loaded, creating texture...");
        
        // STEP 3: Create and load texture
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(fileData))
        {
            Debug.LogError($"[IMAGE] Failed to load image data from: {fullPath}");
            DestroyImmediate(texture, true);
            yield break;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        Debug.Log("[IMAGE] Texture created and applied");

        // STEP 4: Clear old texture and set new one
        Texture currentTexture = sphereMaterial.GetTexture("_MainTex");
        if (currentTexture != null && currentTexture != blackTexture && currentTexture != videoRenderTexture)
        {
            DestroyImmediate(currentTexture, true);
        }

        // STEP 5: Load and apply transform data BEFORE setting texture
        if (!string.IsNullOrEmpty(fileName))
        {
            Debug.Log("[IMAGE] Loading transform data...");
            LoadTransformData(fileName, category);
            Debug.Log("[IMAGE] Transform data applied");
            // Give a frame for transform to settle
            yield return null;
        }

        sphereMaterial.SetTexture("_MainTex", texture);
        Debug.Log("[IMAGE] New texture set on material with correct transforms");

        // STEP 6: Start audio loading and wait for it to ACTUALLY be ready
        Debug.Log("[IMAGE] Starting audio loading while still invisible...");
        StartCoroutine(LoadAudioAndThenFadeIn());
    }

    private IEnumerator LoadAudioAndThenFadeIn()
    {
        Debug.Log("[AUDIO_LOAD] Starting audio loading process...");
        
        // Start audio loading
        if (!string.IsNullOrEmpty(currentAudioPath))
        {
            Debug.Log("[AUDIO_LOAD] Audio path exists, starting TryPlayAudio...");
            
            // Call TryPlayAudio which will load and start the audio
            TryPlayAudio();
            
            // Wait until audio is actually playing or confirmed loaded
            float timeout = 5f; // Maximum wait time
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                if (audioSource != null && audioSource.isPlaying)
                {
                    Debug.Log("[AUDIO_LOAD] Audio is now playing! Ready to fade in.");
                    break;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (elapsed >= timeout)
            {
                Debug.LogWarning("[AUDIO_LOAD] Audio loading timed out, proceeding with fade in anyway");
            }
        }
        else
        {
            Debug.Log("[AUDIO_LOAD] No audio path set, proceeding directly to fade in");
        }
        
        // Give one frame for everything to settle
        yield return null;
        
        Debug.Log("[AUDIO_LOAD] EVERYTHING is now loaded and ready! Calling fade in...");
        
                 // NOW call the fade in function
         StartCoroutine(FadeInVisibility());
     }

     private IEnumerator FadeInVisibility()
     {
         Debug.Log("[FADE_IN] Starting fade in - everything is loaded and ready!");
         
         // Fade back in (wait for completion)
         var fadeUpSequence = DOTween.Sequence();
         
         // Material fade up
         fadeUpSequence.Join(sphereMaterial.DOFloat(1f, "_Visibility", fadeInDuration)
             .SetEase(Ease.InOutSine));
             
         // Post-processing fade up (if available)
         if (colorAdjustments != null)
         {
             fadeUpSequence.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                 x => colorAdjustments.postExposure.value = x,
                 0f, fadeInDuration)
                 .SetEase(Ease.InOutSine));
         }

         yield return fadeUpSequence.WaitForCompletion();
         Debug.Log("[FADE_IN] Fade in completed! Audio and visuals are both active!");
         Debug.Log("[FADE_IN] Complete sequence finished - user can see and hear everything!");
     }

    private string GetCorrectPath(string originalPath)
    {
        try
        {
            Debug.Log($"[PATH] Getting correct path for: {originalPath}");

            // Remove file:/// prefix if present
            if (originalPath.StartsWith("file:///"))
            {
                originalPath = originalPath.Substring("file:///".Length);
            }

            // Extract media type, category and filename
            string[] pathParts = originalPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Debug path parts
            Debug.Log($"[PATH] Path parts: {string.Join(", ", pathParts)}");

            string mediaType = string.Empty;
            string category = string.Empty;
            string fileName = string.Empty;

            for (int i = 0; i < pathParts.Length; i++)
            {
                if (pathParts[i].Equals("Videos", StringComparison.OrdinalIgnoreCase) || 
                    pathParts[i].Equals("360 Images", StringComparison.OrdinalIgnoreCase) ||
                    pathParts[i].Equals("Audio", StringComparison.OrdinalIgnoreCase))
                {
                    mediaType = pathParts[i];
                    if (i + 2 < pathParts.Length)
                    {
                        category = pathParts[i + 1];
                        fileName = pathParts[i + 2];
                    }
                    break;
                }
            }

            Debug.Log($"[PATH] Parsed - MediaType: {mediaType}, Category: {category}, FileName: {fileName}");

            if (string.IsNullOrEmpty(mediaType) || string.IsNullOrEmpty(category) || string.IsNullOrEmpty(fileName))
            {
                Debug.LogError("[PATH] Failed to parse path components");
                fileExistsCache[originalPath] = false;
                return null;
            }

            string fullPath = Path.Combine(Application.persistentDataPath, mediaType, category, fileName);
            Debug.Log($"[PATH] Final path: {fullPath}");
            
            bool fileExists = File.Exists(fullPath);
            Debug.Log($"[PATH] File exists: {fileExists}");
            
            fileExistsCache[originalPath] = fileExists;
            return fileExists ? fullPath : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PATH] Error in GetCorrectPath: {e.Message}");
            fileExistsCache[originalPath] = false;
            return null;
        }
    }

    // Helper function specifically for Data and Audio files
    private string GetDataOrAudioPath(string relativePath)
    {
        try
        {
            Debug.Log($"[DATA/AUDIO PATH] Getting path for: {relativePath}");
            
            string fullPath = Path.Combine(Application.persistentDataPath, relativePath);
            Debug.Log($"[DATA/AUDIO PATH] Full path: {fullPath}");
            
            bool fileExists = File.Exists(fullPath);
            Debug.Log($"[DATA/AUDIO PATH] File exists: {fileExists}");
            
            return fileExists ? fullPath : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DATA/AUDIO PATH] Error getting path: {e.Message}");
            return null;
        }
    }

    // Audio Methods
    private void HandleAudioPlayback(string audioFileName)
    {
        Debug.Log($"[AUDIO] Starting HandleAudioPlayback with file: '{audioFileName}'");

        if (audioSource == null)
        {
            Debug.LogError("[AUDIO] AudioSource component is null!");
            return;
        }

        // Try both underscore and space versions of the filename
        string[] fileNameVariations = new[] {
            audioFileName,                              // Original
            audioFileName.Replace("_", " "),           // Replace underscores with spaces
            audioFileName.Replace(" ", "_")            // Replace spaces with underscores
        };

        AudioClip audioClip = null;
        string successfulPath = null;

        foreach (var fileName in fileNameVariations)
        {
            Debug.Log($"[AUDIO] Trying filename variation: '{fileName}'");
            
            // Try direct load
            audioClip = Resources.Load<AudioClip>($"Audio/{fileName}");
            if (audioClip != null)
            {
                successfulPath = fileName;
                Debug.Log($"[AUDIO] Found audio with exact name: '{fileName}'");
                break;
            }

            // Try with different extensions
            string[] extensions = new[] { ".wav", ".mp3" };
            foreach (var ext in extensions)
            {
                string path = $"Audio/{fileName}{ext}";
                Debug.Log($"[AUDIO] Trying path: '{path}'");
                audioClip = Resources.Load<AudioClip>(path);
                if (audioClip != null)
                {
                    successfulPath = fileName;
                    Debug.Log($"[AUDIO] Found audio at path: '{path}'");
                    break;
                }
            }

            if (audioClip != null) break;
        }

        if (audioClip == null)
        {
            Debug.LogError($"[AUDIO] Failed to load audio clip for any variation of: '{audioFileName}'");
            return;
        }

        // Stop any currently playing audio
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        // Set up the audio source
        audioSource.clip = audioClip;
        currentAudioPath = $"Audio/{successfulPath}"; // Store the successful path
        isAudioPaused = false;

        Debug.Log($"[AUDIO] Setup complete - Clip: {audioClip.name}, Duration: {audioClip.length}s");
        Debug.Log($"[AUDIO] Current audio path set to: {currentAudioPath}");
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

        // Try both .wav and .mp3 extensions
        string[] extensions = new[] { ".wav", ".mp3" };
        foreach (string ext in extensions)
        {
            string fullPath = (currentAudioPath + ext).Replace('\\', '/');
            Debug.Log($"[AUDIO] Checking for audio at: {fullPath}");
            
            if (File.Exists(fullPath))
            {
                Debug.Log($"[AUDIO] Found audio file at: {fullPath}");
                StartCoroutine(PlayAudioFromPath(fullPath, ext == ".mp3" ? AudioType.MPEG : AudioType.WAV));
                return;
            }
        }

        // If not found in category folder, try root Audio folder as fallback
        string audioFileName = Path.GetFileName(currentAudioPath);
        string rootAudioRelativePath = Path.Combine("Audio", audioFileName);
        string rootAudioPath = GetCorrectPath(rootAudioRelativePath);
        
        if (!string.IsNullOrEmpty(rootAudioPath))
        {
            foreach (string ext in extensions)
            {
                string fullPath = rootAudioPath + ext;
                Debug.Log($"[AUDIO] Checking fallback path: {fullPath}");
                
                if (File.Exists(fullPath))
                {
                    Debug.Log($"[AUDIO] Found audio file at fallback path: {fullPath}");
                    StartCoroutine(PlayAudioFromPath(fullPath, ext == ".mp3" ? AudioType.MPEG : AudioType.WAV));
                    return;
                }
            }
        }

        Debug.LogError($"[AUDIO] No audio file found with either .wav or .mp3 extension at: {currentAudioPath} or fallback location");
    }

    private IEnumerator PlayAudioFromPath(string audioPath, AudioType audioType)
    {
        Debug.Log($"[AUDIO] Loading audio from: {audioPath} as {audioType}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + audioPath, audioType))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log("[AUDIO] Successfully started playing audio");
                }
                else
                {
                    Debug.LogError("[AUDIO] Downloaded clip is null");
                }
            }
            else
            {
                Debug.LogError($"[AUDIO] Error loading audio: {www.error}");
            }
        }
    }

    // Transform Data Methods
    private void LoadTransformData(string fileName, string category = null)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[TRANSFORM] fileName is null or empty");
            return;
        }

        Debug.Log($"[TRANSFORM] Looking for data file: {fileName} in category: {category}");

        // Use helper function for Data files
        string dataPath;
        if (!string.IsNullOrEmpty(category))
        {
            string relativePath = Path.Combine("Data", category, fileName + ".txt");
            dataPath = GetDataOrAudioPath(relativePath);
        }
        else
        {
            string relativePath = Path.Combine("Data", fileName + ".txt");
            dataPath = GetDataOrAudioPath(relativePath);
        }

        if (string.IsNullOrEmpty(dataPath) || !File.Exists(dataPath))
        {
            // Try fallback to root Data folder if category didn't work
            if (!string.IsNullOrEmpty(category))
            {
                string fallbackRelativePath = Path.Combine("Data", fileName + ".txt");
                string fallbackPath = GetCorrectPath(fallbackRelativePath);
                if (!string.IsNullOrEmpty(fallbackPath) && File.Exists(fallbackPath))
                {
                    dataPath = fallbackPath;
                    Debug.Log($"[TRANSFORM] Found data file in root folder: {dataPath}");
                }
                else
                {
                    Debug.LogError($"[TRANSFORM] Data file not found at: {dataPath} or {fallbackPath}");
                    return;
                }
            }
            else
            {
                Debug.LogError($"[TRANSFORM] Data file not found at: {dataPath}");
                return;
            }
        }

        try
        {
            string[] lines = File.ReadAllLines(dataPath);
            Vector3 position = Vector3.zero;
            Vector3 rotation = Vector3.zero;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("Audio:"))
                {
                    string audioFileName = trimmedLine.Substring("Audio:".Length).Trim();
                    audioFileName = Path.GetFileNameWithoutExtension(audioFileName);
                    
                    // Store audio path base (without extension) for later extension checking
                    if (!string.IsNullOrEmpty(category))
                    {
                        currentAudioPath = Path.Combine(Application.persistentDataPath, "Audio", category, audioFileName);
                    }
                    else
                    {
                        currentAudioPath = Path.Combine(Application.persistentDataPath, "Audio", audioFileName);
                    }
                    
                    Debug.Log($"[TRANSFORM] Set base audio path to: {currentAudioPath}");
                }
                else if (trimmedLine.StartsWith("Position"))
                {
                    ParsePosition(trimmedLine, ref position);
                }
                else if (trimmedLine.StartsWith("Rotation"))
                {
                    ParseRotation(trimmedLine, ref rotation);
                }
            }

            // Apply transform data
            if (videoSphere != null)
            {
                videoSphere.position = position;
                videoSphere.rotation = Quaternion.Euler(rotation);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TRANSFORM] Error reading transform data: {e.Message}");
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
            
            try
            {
                // Create target directory if it doesn't exist
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                    Debug.Log($"[COPY] Created directory: {targetPath}");
                }

                // Copy all subfolders and files
                foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                {
                    string newDirPath = dirPath.Replace(sourcePath, targetPath);
                    if (!Directory.Exists(newDirPath))
                    {
                        Directory.CreateDirectory(newDirPath);
                        Debug.Log($"[COPY] Created subdirectory: {newDirPath}");
                    }
                }

                foreach (string filePath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                {
                    string newFilePath = filePath.Replace(sourcePath, targetPath);
                    File.Copy(filePath, newFilePath, true);
                    Debug.Log($"[COPY] Copied file: {Path.GetFileName(filePath)} to {newFilePath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[COPY] Error copying {folder}: {e.Message}");
                // Continue with next folder instead of breaking
            }

            currentProgress += progressPerFolder;
            yield return null;
        }

        UpdateProgress("All files copied successfully!", 1f);
        yield return StartCoroutine(HideProgressAfterDelay(2f));
    }

    void LoadAssetsFromPersistentDataPath()
    {
        if (cachedElements != null)
        {
            ApplyElementsToCarousel(cachedElements);
            return;
        }

        StartCoroutine(LoadAssetsAsync());
    }

    private IEnumerator LoadAssetsAsync()
    {
        var loadedElements = new List<HKCarouselElementData>();

        // Add Home card at the beginning
        loadedElements.Add(new HKCarouselElementData
        {
            Name = "Home",
            Category = "Navigation",
            IsSceneLink = true,
            SceneIndex = 1,
            ThumbnailPath = "Scene Thumbnails/Home thumbnail"
        });

        // 360 Images first (since that's what we see in the UI)
        string imagesRoot = Path.Combine(Application.persistentDataPath, "360 Images");
        if (Directory.Exists(imagesRoot))
        {
            var categoryDirs = Directory.GetDirectories(imagesRoot);
            int processedItems = 0;

            foreach (var categoryDir in categoryDirs)
            {
                string category = Path.GetFileName(categoryDir);
                // Get files and sort them by name to ensure consistent order
                var imageFiles = Directory.GetFiles(categoryDir, "*.*")
                    .Where(f => {
                        string ext = Path.GetExtension(f).ToLower();
                        return ext == ".jpg" || ext == ".png";
                    })
                    .OrderBy(f => Path.GetFileNameWithoutExtension(f)) // Sort by filename
                    .ToList();

                foreach (var imageFile in imageFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(imageFile);
                    string relativePath = Path.Combine("360 Images", category, Path.GetFileName(imageFile));
                    
                    loadedElements.Add(new HKCarouselElementData
                    {
                        Name = fileName,
                        Category = category,
                        VideoPath = relativePath,
                        ThumbnailPath = $"Thumbnails/{category}/{fileName}",
                        IsImage360 = true
                    });

                    processedItems++;
                    if (processedItems % BATCH_SIZE == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        // Videos second
        string videosRoot = Path.Combine(Application.persistentDataPath, "Videos");
        if (Directory.Exists(videosRoot))
        {
            var categoryDirs = Directory.GetDirectories(videosRoot);
            int processedItems = 0;

            foreach (var categoryDir in categoryDirs)
            {
                string category = Path.GetFileName(categoryDir);
                // Get files and sort them by name to ensure consistent order
                var videoFiles = Directory.GetFiles(categoryDir, "*.mp4")
                    .OrderBy(f => Path.GetFileNameWithoutExtension(f)) // Sort by filename
                    .ToList();

                foreach (var videoFile in videoFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(videoFile);
                    string relativePath = Path.Combine("Videos", category, Path.GetFileName(videoFile));
                    
                    loadedElements.Add(new HKCarouselElementData
                    {
                        Name = fileName,
                        Category = category,
                        VideoPath = relativePath,
                        ThumbnailPath = $"Thumbnails/{category}/{fileName}",
                        IsImage360 = false
                    });

                    processedItems++;
                    if (processedItems % BATCH_SIZE == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        // Remove the final sorting that was messing up the order
        cachedElements = loadedElements;
        ApplyElementsToCarousel(loadedElements);
    }

    private void ApplyElementsToCarousel(List<HKCarouselElementData> elements)
    {
        if (carousel360 == null) return;

        var carouselType = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>);
        var elementsField = carouselType.GetField("_carouselElements", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var defaultIndexField = carouselType.GetField("_defaultSelectedIndex", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (elementsField != null)
        {
            elementsField.SetValue(carousel360, elements);

            if (elements.Count > 0 && defaultIndexField != null)
            {
                int middleIndex = Mathf.FloorToInt(elements.Count / 2f);
                defaultIndexField.SetValue(carousel360, middleIndex);
            }

            var refreshMethod = carouselType.GetMethod("RefreshItems", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var updateMethod = carouselType.GetMethod("UpdateCarousel", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (refreshMethod != null && updateMethod != null)
            {
                refreshMethod.Invoke(carousel360, null);
                updateMethod.Invoke(carousel360, null);
            }
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