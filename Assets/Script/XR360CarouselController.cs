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

/// <summary>
/// 🎯 OPTIMIZED ON-DEMAND MEDIA LOADING SYSTEM
/// 
/// How it works:
/// 1. 📄 Load only .txt metadata files into carousel (super fast!)
/// 2. 🎮 When trigger pressed, check metadata Type field
/// 3. 🎬 If Type="video" → search Videos/{category}/{name}.mp4
/// 4. 🖼️ If Type="image" → search 360 Images/{category}/{name}.png  
/// 5. 🚀 Load only the specific file needed RIGHT NOW!
/// 
/// Benefits:
/// ✅ Fast startup (no media pre-loading)
/// ✅ Low memory usage 
/// ✅ Scalable to hundreds of experiences
/// ✅ Dynamic content discovery
/// </summary>
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
        Debug.Log("[XR360CarouselController] Initialize() called");

        if (!isSetup)
        {
            InitializeDevices();
        isSetup = true;
        }

        Debug.Log("[XR360CarouselController] Starting asset loading...");
        
        // Load metadata directly since files are manually added via PC
        Debug.Log("[XR360CarouselController] Files are manually added via PC, skipping copy operation");
        Debug.Log("[XR360CarouselController] Expected file structure:");
        Debug.Log($"[XR360CarouselController] - Images: {Path.Combine(Application.persistentDataPath, "360 Images", "Category", "ElementName.png/.jpg")}");
        Debug.Log($"[XR360CarouselController] - Videos: {Path.Combine(Application.persistentDataPath, "Videos", "Category", "ElementName.mp4")}");
        Debug.Log($"[XR360CarouselController] - Metadata: {Path.Combine(Application.persistentDataPath, "Data", "Category", "ElementName.txt")}");
        
        LoadAssetsFromPersistentDataPath();

        Debug.Log("[XR360CarouselController] Initialize() completed");
    }

    private void InitializeDevices()
    {
        // Initialize input devices
        InputDevices.GetDevices(devices);
        
        // Initialize black texture
        blackTexture = new Texture2D(1, 1);
        blackTexture.SetPixel(0, 0, Color.black);
        blackTexture.Apply();

        // Initialize sphere material
        if (sphereMaterial != null)
        {
            // Always keep the videoRenderTexture assigned to the material
            sphereMaterial.SetTexture("_MainTex", videoRenderTexture);
        }

        // Initialize rating system
        InitializeRatingSystem();
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
        Debug.Log($"[MEDIA] Using ON-DEMAND loading - no pre-loaded media path");

        // Check if this is a scene link
        if (elementData.IsSceneLink && elementData.SceneIndex >= 0)
        {
            Debug.Log($"[SCENE] Loading scene index: {elementData.SceneIndex}");
            StartCoroutine(LoadSceneAsync(elementData.SceneIndex));
            return;
        }

        // Handle regular media with ON-DEMAND loading (no pre-loaded VideoPath needed)
        // Always treat as new selection when carousel is visible
        if (isCarouselVisible)
        {
            lastSelectedIndex = selectedIndex;
            // NOTE: currentMediaPath will be resolved on-demand in Handle methods

            // Hide carousel UI
            HideCarousel();

            // 🎯 ON-DEMAND LOADING: Check metadata type and load only what's needed!
            if (elementData.IsImage360)
            {
                Debug.Log($"[TRIGGER] 🖼️ Metadata says IMAGE - will search 360 Images/{elementData.Category}/{elementData.Name}");
                HandleImage360Media(elementData, false, true);
            }
            else
            {
                Debug.Log($"[TRIGGER] 🎬 Metadata says VIDEO - will search Videos/{elementData.Category}/{elementData.Name}");
                HandleVideoMedia(elementData, false, true);
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

        // Pause audio (only if it's image audio, not video audio)
        if (audioSource != null && audioSource.isPlaying && !string.IsNullOrEmpty(currentAudioPath))
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

        // Resume audio if needed (only for image audio)
        if (audioSource != null && isAudioPaused && !string.IsNullOrEmpty(currentAudioPath))
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
            // 🎯 ON-DEMAND IMAGE LOADING: Find the image file NOW (not pre-loaded!)
            Debug.Log($"[ON-DEMAND IMAGE] Loading '{data.Name}' from category '{data.Category}'");
            string resolvedMediaPath = ResolveMediaPath(data.Name, data.Category, true);
            if (string.IsNullOrEmpty(resolvedMediaPath))
            {
                Debug.LogError($"[ON-DEMAND IMAGE] ❌ Could not resolve image path for {data.Name} in category {data.Category}");
                return;
            }
            
            Debug.Log($"[ON-DEMAND IMAGE] ✅ Found image: {resolvedMediaPath}");
            currentMediaPath = resolvedMediaPath;
            triggerPressCount = 1;
            isAudioPaused = false;

            // 🎵 CLEAN AUDIO: Stop any video audio and set up image audio
            CleanupAudio();

            // Get audio path from metadata for images only
            string audioPath = GetAudioPathFromMetadata(data.Name, data.Category);
            if (!string.IsNullOrEmpty(audioPath))
            {
                Debug.Log($"[ON-DEMAND AUDIO] Setting up audio for image: {audioPath}");
                HandleAudioPlayback(audioPath);
            }
            else
            {
                Debug.Log("[ON-DEMAND AUDIO] No audio specified for this image");
                currentAudioPath = null;
            }

            FadeOutCanvas();
            DisplayImageOnSphere(resolvedMediaPath);
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
            // 🎯 ON-DEMAND VIDEO LOADING: Find the video file NOW (not pre-loaded!)
            Debug.Log($"[ON-DEMAND VIDEO] Loading '{data.Name}' from category '{data.Category}'");
            string resolvedMediaPath = ResolveMediaPath(data.Name, data.Category, false);
            if (string.IsNullOrEmpty(resolvedMediaPath))
            {
                Debug.LogError($"[ON-DEMAND VIDEO] ❌ Could not resolve video path for {data.Name} in category {data.Category}");
                return;
            }
            
            Debug.Log($"[ON-DEMAND VIDEO] ✅ Found video: {resolvedMediaPath}");
            currentMediaPath = resolvedMediaPath;
            triggerPressCount = 1;
            isVideoPaused = false;
            isAudioPaused = false;

            // 🎵 CLEAN AUDIO: Stop any playing audio from images - videos should use their own audio
            CleanupAudio();

            FadeOutCanvas();
            PlayVideo(resolvedMediaPath, vp);
            return;
        }

        if (shouldFadeOut)
        {
            if (isVideoPaused)
            {
                vp.Play();
                isVideoPaused = false;
            }

            // 🎵 VIDEOS: Don't resume image audio for videos
            FadeOutCanvas();
        }
        else
        {
            vp.Pause();
            isVideoPaused = true;

            // 🎵 VIDEOS: Don't pause image audio for videos (it should already be stopped)
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

            // Clean up any existing audio before playing new media
            CleanupAudio();

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
        Debug.Log($"[VIDEO] Playing video from full path: {videoPath}");
        
        // videoPath is now a full path from ResolveMediaPath, no conversion needed
        string fullPath = videoPath;
        
        videoPlayer.enabled = true;
        string fileName = Path.GetFileNameWithoutExtension(videoPath);
        LoadTransformData(fileName, null, false); // false = not image360 (it's a video)

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[VIDEO] Video file not found at path: {fullPath}");
            return;
        }

        // Configure VideoPlayer using the original working approach - MaterialOverride
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
        videoPlayer.targetMaterialRenderer = videoSphere.GetComponent<Renderer>();
        videoPlayer.targetMaterialProperty = "_MainTex";
        videoPlayer.aspectRatio = VideoAspectRatio.FitVertically;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);
        
        Debug.Log($"[VIDEO] Configured using MaterialOverride to render directly to videoSphere");

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

        // Clear any old image texture before video playback (video will override the material directly)
        Texture currentTexture = sphereMaterial.GetTexture("_MainTex");
        if (currentTexture != null && currentTexture != blackTexture && currentTexture != videoRenderTexture)
        {
            Debug.Log("[VIDEO] Clearing old image texture before video playback");
            DestroyImmediate(currentTexture, true);
        }
        
        Debug.Log("[VIDEO] Video player will override material directly using MaterialOverride mode");

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

        // 🎵 VIDEOS: Don't play image audio - videos use their own audio track
        Debug.Log("[VIDEO] Video playback started - using video's own audio track");
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
            LoadTransformData(fileName, category, true); // true = isImage360
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
    private void CleanupAudio()
    {
        if (audioSource != null)
        {
            Debug.Log("[AUDIO_CLEANUP] Stopping and clearing audio source");
            audioSource.Stop();
            audioSource.clip = null;
            currentAudioPath = null;
            isAudioPaused = false;
        }
    }

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
    private void LoadTransformData(string fileName, string category = null, bool isImage360 = true)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[TRANSFORM] fileName is null or empty");
            return;
        }

        Debug.Log($"[TRANSFORM] Looking for data file: {fileName} in category: {category} (isImage360: {isImage360})");

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
                    // 🎵 AUDIO: Only handle audio for images, not videos
                    if (isImage360)
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
                        
                        Debug.Log($"[TRANSFORM] Set base audio path to: {currentAudioPath} (for image)");
                    }
                    else
                    {
                        Debug.Log("[TRANSFORM] Ignoring audio data for video - videos use their own audio track");
                    }
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

    private IEnumerator CopyAndroidStreamingAssetsAndThenLoad()
    {
        Debug.Log("[CAROUSEL] Starting copy operation before loading assets...");
        
        // First, copy all the streaming assets
        yield return StartCoroutine(CopyAndroidStreamingAssets());
        
        Debug.Log("[CAROUSEL] Copy operation completed, now loading assets...");
        
        // Then load the assets from the copied files
        LoadAssetsFromPersistentDataPath();
    }

    void LoadAssetsFromPersistentDataPath()
    {
        Debug.Log($"[CAROUSEL] LoadAssetsFromPersistentDataPath called, cachedElements = {(cachedElements == null ? "null" : cachedElements.Count.ToString())}");
        
        if (cachedElements != null)
        {
            Debug.Log($"[CAROUSEL] Using cached elements: {cachedElements.Count}");
            ApplyElementsToCarousel(cachedElements);
            return;
        }

        Debug.Log("[CAROUSEL] Starting LoadAssetsAsync coroutine...");
        StartCoroutine(LoadAssetsAsync());
    }

    private IEnumerator LoadAssetsAsync()
    {
        Debug.Log("[CAROUSEL] 🚀 LoadAssetsAsync ULTRA-OPTIMIZED STARTED");
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
        Debug.Log("[CAROUSEL] Added Home card");

        yield return null; // Frame break
        
        Debug.Log("[CAROUSEL] Starting ultra-fast metadata loading...");

        string dataRoot = Path.Combine(Application.persistentDataPath, "Data");
        
        if (!Directory.Exists(dataRoot))
        {
            Debug.LogWarning($"[METADATA] Data directory not found: {dataRoot}");
            cachedElements = loadedElements;
            ApplyElementsToCarousel(loadedElements);
            yield break;
        }

        // 🚀 ULTRA-FAST BATCH PROCESSING: Load metadata files with frame time management
        var allMetadataFiles = new List<string>();
        
        // Collect all metadata files first (this is fast)
        try
        {
            foreach (var categoryDir in Directory.GetDirectories(dataRoot))
            {
                var txtFiles = Directory.GetFiles(categoryDir, "*.txt")
                    .Where(f => {
                        string name = Path.GetFileNameWithoutExtension(f).ToLower();
                        return !name.StartsWith("files") && !name.StartsWith("home");
                    })
                    .ToArray();
                
                allMetadataFiles.AddRange(txtFiles);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[METADATA] Error collecting metadata files: {e.Message}");
        }

        Debug.Log($"[METADATA] Found {allMetadataFiles.Count} metadata files to process");
        yield return null; // Frame break after collection

        // 🚀 FRAME-BUDGET PROCESSING: Process files with strict frame time limits
        const float maxFrameTime = 0.008f; // 8ms budget for ultra-smooth loading
        const int minBatchSize = 1; // Minimum files per frame
        const int maxBatchSize = 5; // Maximum files per frame
        
        int processedCount = 0;
        float frameStartTime;
        
        for (int i = 0; i < allMetadataFiles.Count; i += maxBatchSize)
        {
            frameStartTime = Time.realtimeSinceStartup;
            int batchSize = 0;
            
            // Process files within frame budget
            for (int j = i; j < allMetadataFiles.Count && j < i + maxBatchSize; j++)
            {
                // Check frame time before processing each file
                if (batchSize >= minBatchSize && (Time.realtimeSinceStartup - frameStartTime) > maxFrameTime)
                {
                    break; // Frame budget exceeded, yield
                }
                
                try
                {
                    var elementData = ParseMetadataFile(allMetadataFiles[j]);
                    if (elementData != null)
                    {
                        loadedElements.Add(elementData);
                        processedCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[METADATA] Error processing {allMetadataFiles[j]}: {e.Message}");
                }
                
                batchSize++;
            }
            
            // Update progress and yield every batch
            float progress = (float)processedCount / allMetadataFiles.Count;
            Debug.Log($"[METADATA] 🚀 Processed {processedCount}/{allMetadataFiles.Count} files ({progress:P0})");
            
            yield return null; // Frame break between batches
        }

        Debug.Log($"[METADATA] 🚀 ULTRA-FAST loading completed: {loadedElements.Count} total elements");
        
        cachedElements = loadedElements;
        ApplyElementsToCarousel(loadedElements);
    }

    /// <summary>
    /// 🚀 ULTRA-FAST METADATA PARSING: NO blocking file operations during initialization
    /// Thumbnail paths are resolved lazily when actually needed
    /// </summary>
    private HKCarouselElementData ParseMetadataFile(string filePath)
    {
        var data = new HKCarouselElementData
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            Category = Path.GetFileName(Path.GetDirectoryName(filePath)),
            VideoPath = null, // ✅ NO PRE-LOADING: Path resolved dynamically when needed
            ThumbnailPath = null // 🚀 OPTIMIZED: NO immediate resolution - will be resolved lazily by CarouselElementDemo
        };

        try
        {
            string[] lines = File.ReadAllLines(filePath);
            string audioPath = null;
            string description = null;
            
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                // Split by first colon to handle descriptions that may contain colons
                int colonIndex = trimmedLine.IndexOf(':');
                if (colonIndex <= 0) continue;

                string key = trimmedLine.Substring(0, colonIndex).Trim();
                string value = trimmedLine.Substring(colonIndex + 1).Trim();

                switch (key.ToLower())
                {
                    case "type":
                        data.IsImage360 = string.Equals(value.Trim(), "image", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "audio":
                        audioPath = value;
                        break;
                    case "description":
                        description = value;
                        break;
                }
            }

            Debug.Log($"[METADATA] 🚀 Fast-parsed: {data.Name} (Category: {data.Category}, Type: {(data.IsImage360 ? "Image" : "Video")})");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[METADATA] Error parsing {filePath}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 🎵 ON-DEMAND AUDIO: Extracts audio path from metadata when needed
    /// </summary>
    private string GetAudioPathFromMetadata(string elementName, string category)
    {
        try
        {
            string metadataPath = Path.Combine(Application.persistentDataPath, "Data", category, elementName + ".txt");
            Debug.Log($"[ON-DEMAND AUDIO] Reading metadata for audio: {metadataPath}");
            
            if (File.Exists(metadataPath))
            {
                string[] lines = File.ReadAllLines(metadataPath);
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;

                    int colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex <= 0) continue;

                    string key = trimmedLine.Substring(0, colonIndex).Trim();
                    string value = trimmedLine.Substring(colonIndex + 1).Trim();

                    if (key.ToLower() == "audio")
                    {
                        Debug.Log($"[ON-DEMAND AUDIO] ✅ Found audio reference: {value}");
                        return value;
                    }
                }
                Debug.Log($"[ON-DEMAND AUDIO] No audio reference found in metadata");
            }
            else
            {
                Debug.LogWarning($"[ON-DEMAND AUDIO] Metadata file not found: {metadataPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ON-DEMAND AUDIO] Error extracting audio path for {elementName}: {e.Message}");
        }
        
        return null;
    }

    /// <summary>
    /// 🎯 ON-DEMAND MEDIA LOADING: Only called when trigger is pressed!
    /// Dynamically finds the correct media file and returns FULL path (like images)
    /// </summary>
    private string ResolveMediaPath(string elementName, string category, bool isImage360)
    {
        string mediaType = isImage360 ? "IMAGE" : "VIDEO";
        string mediaFolder = isImage360 ? "360 Images" : "Videos";
        string[] extensions = isImage360 ? new[] { ".png", ".jpg" } : new[] { ".mp4" };
        
        Debug.Log($"[ON-DEMAND] 🎯 Looking for {mediaType}: '{elementName}' in category '{category}'");
        
        // Build the full category path like we do for images
        string categoryPath = Path.Combine(Application.persistentDataPath, mediaFolder, category);
        Debug.Log($"[ON-DEMAND] Category path: {categoryPath}");
        
        if (!Directory.Exists(categoryPath))
        {
            Debug.LogError($"[ON-DEMAND] ❌ Category directory not found: {categoryPath}");
            return null;
        }
        
        // Get all files in the category directory  
        var allFiles = Directory.GetFiles(categoryPath);
        Debug.Log($"[ON-DEMAND] Files found: [{string.Join(", ", allFiles.Select(Path.GetFileName))}]");
        
        // Try to find exact match first
        foreach (string ext in extensions)
        {
            string exactFileName = elementName + ext;
            string exactPath = Path.Combine(categoryPath, exactFileName);
            
            Debug.Log($"[ON-DEMAND] Checking exact: {exactPath}");
            
            if (File.Exists(exactPath))
            {
                Debug.Log($"[ON-DEMAND] ✅ FOUND {mediaType} (exact): {exactPath}");
                return exactPath; // Return FULL PATH like images do
            }
        }
        
        // If exact match fails, try fuzzy matching with cleaned names
        Debug.Log($"[ON-DEMAND] Exact match failed, trying fuzzy matching...");
        string targetClean = elementName.Trim().ToLowerInvariant();
        
        foreach (string filePath in allFiles)
        {
            string fileName = Path.GetFileName(filePath);
            string fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);
            string fileExt = Path.GetExtension(fileName).ToLowerInvariant();
            
            // Clean the actual filename (remove special chars, normalize whitespace)
            string actualClean = fileNameNoExt.Trim().ToLowerInvariant();
            actualClean = System.Text.RegularExpressions.Regex.Replace(actualClean, @"\s+", " ");
            actualClean = actualClean.Replace("\n", "").Replace("\r", "");
            
            Debug.Log($"[ON-DEMAND] Comparing target:'{targetClean}' vs actual:'{actualClean}' (ext:{fileExt})");
            
            if (extensions.Contains(fileExt) && actualClean.Equals(targetClean))
            {
                Debug.Log($"[ON-DEMAND] ✅ FOUND {mediaType} (fuzzy): {filePath}");
                Debug.Log($"[ON-DEMAND] Matched '{fileName}' with cleaned target '{targetClean}'");
                return filePath; // Return FULL PATH like images do
            }
        }
        
        Debug.LogError($"[ON-DEMAND] ❌ Could not find {mediaType} for '{elementName}' in '{category}'");
        Debug.LogError($"[ON-DEMAND] Available: {string.Join(", ", allFiles.Select(f => Path.GetFileName(f)))}");
        return null;
    }

    /// <summary>
    /// 🖼️ THUMBNAIL LOADING: Called during carousel creation to find thumbnail files
    /// Dynamically finds the correct thumbnail file for carousel display
    /// </summary>
    private string ResolveThumbnailPath(string elementName, string category)
    {
        string[] extensions = { ".png", ".jpg" };
        
        Debug.Log($"[THUMBNAIL] 🖼️ Looking for thumbnail: '{elementName}' in category '{category}'");
        
        string thumbnailBasePath = Path.Combine(Application.persistentDataPath, "Thumbnails");
        string categoryPath = Path.Combine(thumbnailBasePath, category);
        
        Debug.Log($"[THUMBNAIL] Category path: {categoryPath}");
        Debug.Log($"[THUMBNAIL] Category path exists: {Directory.Exists(categoryPath)}");
        
        if (Directory.Exists(categoryPath))
        {
            var allFiles = Directory.GetFiles(categoryPath);
            Debug.Log($"[THUMBNAIL] All files in {category} thumbnails: [{string.Join(", ", allFiles.Select(Path.GetFileName))}]");
        }
        
        foreach (string ext in extensions)
        {
            string fullPath = Path.Combine(categoryPath, elementName + ext);
            
            Debug.Log($"[THUMBNAIL] Checking: {fullPath}");
            Debug.Log($"[THUMBNAIL] File exists: {File.Exists(fullPath)}");
            
            if (File.Exists(fullPath))
            {
                Debug.Log($"[THUMBNAIL] ✅ FOUND thumbnail: {fullPath}");
                return fullPath; // Return full path for thumbnail loading
            }
        }
        
        Debug.LogWarning($"[THUMBNAIL] ⚠️ Could not find thumbnail for '{elementName}' in category '{category}'");
        Debug.LogWarning($"[THUMBNAIL] Searched for files: {string.Join(", ", extensions.Select(ext => elementName + ext))}");
        Debug.LogWarning($"[THUMBNAIL] In directory: {categoryPath}");
        return null;
    }

    private void ApplyElementsToCarousel(List<HKCarouselElementData> elements)
    {
        if (carousel360 == null) return;

        Debug.Log($"[XR360CarouselController] 🚀 Applying {elements.Count} elements to carousel with ULTRA-FAST setup");

        var carouselType = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>);
        var elementsField = carouselType.GetField("_carouselElements", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var defaultIndexField = carouselType.GetField("_defaultSelectedIndex", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (elementsField != null)
        {
            elementsField.SetValue(carousel360, elements);
            Debug.Log($"[XR360CarouselController] ✅ Set {elements.Count} elements on carousel");

            if (elements.Count > 0 && defaultIndexField != null)
            {
                int middleIndex = Mathf.FloorToInt(elements.Count / 2f);
                defaultIndexField.SetValue(carousel360, middleIndex);
                Debug.Log($"[XR360CarouselController] ✅ Set default index to {middleIndex}");
            }

            // Trigger CreatePool if it hasn't been created yet
            var poolCreatedField = carouselType.GetField("_poolCreated", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (poolCreatedField != null)
            {
                bool poolCreated = (bool)poolCreatedField.GetValue(carousel360);
                if (!poolCreated)
                {
                    Debug.Log("[XR360CarouselController] 🚀 Creating carousel pool...");
                    var createPoolMethod = carouselType.GetMethod("CreatePool", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (createPoolMethod != null)
                    {
                        createPoolMethod.Invoke(carousel360, null);
                        Debug.Log("[XR360CarouselController] ✅ Carousel pool created");
                    }
                }
            }

            var refreshMethod = carouselType.GetMethod("RefreshItems", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var updateMethod = carouselType.GetMethod("UpdateCarousel", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (refreshMethod != null && updateMethod != null)
            {
                refreshMethod.Invoke(carousel360, null);
                updateMethod.Invoke(carousel360, null);
                Debug.Log("[XR360CarouselController] ✅ Carousel refreshed and updated");
            }

            // 🚀 ULTRA-FAST THUMBNAIL SYSTEM: Initialize optimized loading
            Debug.Log("[XR360CarouselController] 🚀 Initializing ULTRA-FAST thumbnail loading...");
            CarouselElementDemo.InitializeSmartLoading(carousel360);
            
            // 🚀 BACKGROUND OPTIMIZATION: Pre-scan thumbnail directories in background
            StartCoroutine(PreScanThumbnailDirectories());
            
            Debug.Log("[XR360CarouselController] 🎉 ULTRA-FAST carousel setup completed!");
        }
    }

    /// <summary>
    /// 🚀 BACKGROUND OPTIMIZATION: Pre-scan thumbnail directories for faster future access
    /// </summary>
    private IEnumerator PreScanThumbnailDirectories()
    {
        // Wait a bit to let carousel finish setup
        yield return new WaitForSeconds(2f);
        
        Debug.Log("[THUMBNAIL_PRESCAN] 🔍 Starting background thumbnail directory scan...");
        
        string thumbnailBasePath = Path.Combine(Application.persistentDataPath, "Thumbnails");
        
        if (!Directory.Exists(thumbnailBasePath))
        {
            Debug.Log("[THUMBNAIL_PRESCAN] 📁 No thumbnails directory found");
            yield break;
        }
        
        // Pre-scan directories on background thread to warm up file system cache
        bool scanComplete = false;
        int totalDirectories = 0;
        int scannedDirectories = 0;
        
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var categoryDirs = Directory.GetDirectories(thumbnailBasePath);
                totalDirectories = categoryDirs.Length;
                
                foreach (var categoryDir in categoryDirs)
                {
                    // Pre-scan each category directory
                    var files = Directory.GetFiles(categoryDir, "*.png");
                    var jpgFiles = Directory.GetFiles(categoryDir, "*.jpg");
                    
                    string categoryName = Path.GetFileName(categoryDir);
                    Debug.Log($"[THUMBNAIL_PRESCAN] 📂 {categoryName}: {files.Length + jpgFiles.Length} thumbnails");
                    
                    scannedDirectories++;
                    
                    // Small delay to prevent overwhelming the file system
                    System.Threading.Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[THUMBNAIL_PRESCAN] Error during background scan: {ex.Message}");
            }
            finally
            {
                scanComplete = true;
            }
        });
        
        // Wait for background scan to complete
        while (!scanComplete)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        Debug.Log($"[THUMBNAIL_PRESCAN] ✅ Background scan completed: {scannedDirectories}/{totalDirectories} directories");
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