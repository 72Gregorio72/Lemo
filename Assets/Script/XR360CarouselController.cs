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
using UnityEngine.SceneManagement;

/// <summary>
/// Ultra-optimized async on-demand media loading system
/// 
/// How it works:
/// 1. Load only .txt metadata files into carousel (super fast!)
/// 2. When trigger pressed, check metadata Type field
/// 3. If Type="video" → search Videos/{category}/{name}.mp4 ASYNC
/// 4. If Type="image" → search 360 Images/{category}/{name}.png ASYNC


/// </summary>
public class XR360CarouselController : MonoBehaviour
{
    [Header("Carousel Control")]
    private HKCarouselLayoutGroup3DDemo carousel360;
    [SerializeField] private float inputThreshold = 0.5f;
    [SerializeField] private float scrollCooldown = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.3f;  // Reduced from 1f to 0.3f

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
    [SerializeField] private float fadeInDuration = 0.3f;   // Reduced from 1f to 0.3f
    [SerializeField] private float blackScreenDuration = 0f; // Removed wait time

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

    // Async loading system - removed (back to simple sync approach)
    // private static Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();
    // private static Dictionary<string, Texture2D> imageCache = new Dictionary<string, Texture2D>();
    // private static Dictionary<string, string> transformCache = new Dictionary<string, string>();
    // private static int currentAsyncOperations = 0;
    // private static Queue<System.Action> mainThreadQueue = new Queue<System.Action>();
    // [SerializeField] private int maxConcurrentLoads = 2;
    
    private HKCarouselElementData pendingElementData = null;
    private bool isCarouselInteractive = true;
    private Texture2D blackTexture;

    private bool isSetup = false;

    // New simple state flags (rating flow removed)
    private bool isCarouselVisible = true;   // Carousel UI starts visible
    private int lastSelectedIndex = -1;      // Track last played media index

    private ColorAdjustments colorAdjustments;
    private const float LOADING_EXPOSURE = -200f;
    
    // Scene-aware content loading
    private string activeSceneName;
    


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
        }
        catch (Exception)
        {
            // Create an empty ratings file as fallback
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RatingFilePath));
                File.WriteAllText(RatingFilePath, "{}");
            }
            catch (Exception)
            {
                // Silent fallback
            }
        }
    }

    public void Initialize(HKCarouselLayoutGroup3DDemo carouselComponent)
    {
        carousel360 = carouselComponent;

        // Initialize scene name for scene-aware content loading
        activeSceneName = SceneManager.GetActiveScene().name;

        if (!isSetup)
        {
            InitializeDevices();
        isSetup = true;
        }
        
        // Load metadata directly since files are manually added via PC
        LoadAssetsFromPersistentDataPath();
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

    private void Awake()
    {
        // Set exposure to deep black immediately on Awake
        var globalVolume = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
        if (globalVolume != null && globalVolume.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments.active = true;
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = LOADING_EXPOSURE;
        }

        // Set target framerate for smooth VR
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        // CRITICAL FIX: Unity defaults to 30fps on mobile causing scrolling lag
        Application.targetFrameRate = 60;
        
        // Disable conflicting input controllers to prevent conflicts
        var conflictingController = FindFirstObjectByType<HKCarouselLayoutGroup.XRCarouselInputController>();
        if (conflictingController != null)
        {
            conflictingController.enabled = false;
        }

        // Also disable scene controllers if present to avoid conflicts
        var sceneController = FindFirstObjectByType<XRSceneCarouselController>();
        if (sceneController != null)
        {
            sceneController.enabled = false;
        }

            InputDevices.GetDevices(devices);
            
        // Initialize scene name for scene-aware content loading if not already set
        if (string.IsNullOrEmpty(activeSceneName))
        {
            activeSceneName = SceneManager.GetActiveScene().name;
        }
        
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

        // Find the global volume in the scene and get ColorAdjustments
            if (postProcessVolume != null)
            {
            if (!postProcessVolume.profile.TryGet(out colorAdjustments))
                {
                colorAdjustments = null;
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

    private void Update()
    {
        if (!gameObject.activeInHierarchy || !isSetup || carousel360 == null) return;

        InputDevices.GetDevices(devices);
        cooldownTimer -= Time.deltaTime;

        foreach (var device in devices)
        {
            // TRIGGER BUTTON - Only detect press events, not continuous
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool isTriggerHeld))
            {
                previousTriggerStates.TryGetValue(device, out bool wasTriggerHeld);

                // Only call PlaySelectedMedia on trigger PRESS (not hold)
                if (isTriggerHeld && !wasTriggerHeld)
                {
                    PlaySelectedMedia();
                }

                previousTriggerStates[device] = isTriggerHeld;
            }

            // THUMBSTICK - Handle carousel navigation
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

    private void CheckAndUpdateDevices()
    {
        // Remove this method - it's not needed, Update handles devices directly
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
        int selectedIndex = carousel360.GetTrueSelectedIndex(); // Get the true index
        
        // Get the element data and log its details
        var elementData = carousel360.GetElementDataFromIndex(selectedIndex) as HKCarouselElementData;
        if (elementData == null)
        {
            return;
        }

        // Check if this is a scene link
        if (elementData.IsSceneLink && elementData.SceneIndex >= 0)
        {
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

            // ON-DEMAND LOADING: Check metadata type and load only what's needed!
            if (elementData.IsImage360)
            {
                HandleImage360Media(elementData, false, true);
            }
            else
            {
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

    /// <summary>
    /// Ultra-fast async image handler: Zero-lag image loading with instant feedback
    /// </summary>
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
            // Find the image file with same name as data txt
            string resolvedMediaPath = ResolveMediaPath(data.Name, data.Category, true);
            if (string.IsNullOrEmpty(resolvedMediaPath))
            {
                return;
            }
            
            currentMediaPath = resolvedMediaPath;
            triggerPressCount = 1;
            isAudioPaused = false;

            // Clean up any existing audio first
            CleanupAudio();

            // Read audio filename from metadata, then resolve path
            string audioFilename = GetAudioFilenameFromMetadata(data.Name, data.Category);
            if (!string.IsNullOrEmpty(audioFilename))
            {
                string audioPath = ResolveAudioPath(audioFilename, data.Category);
            if (!string.IsNullOrEmpty(audioPath))
            {
                    currentAudioPath = audioPath;
                    // Audio will be loaded in background during image display
            }
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

    /// <summary>
    /// Ultra-fast async video handler: Zero-lag video loading with instant feedback
    /// </summary>
    private void HandleVideoMedia(HKCarouselElementData data, bool isSameMedia, bool shouldFadeOut)
    {
        GameObject videoObj = GameObject.FindGameObjectWithTag("VideoPlayer");
        if (videoObj == null)
        {
            return;
        }

        var vp = videoObj.GetComponent<VideoPlayer>();
        if (vp == null)
        {
            return;
        }

        if (!isSameMedia)
        {
            // Find the video file with same name as data txt
            string resolvedMediaPath = ResolveMediaPath(data.Name, data.Category, false);
            if (string.IsNullOrEmpty(resolvedMediaPath))
            {
                return;
            }
            
            currentMediaPath = resolvedMediaPath;
            triggerPressCount = 1;
            isVideoPaused = false;
            isAudioPaused = false;

            // Clean up any audio from images - videos use their own audio
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

            FadeOutCanvas();
        }
        else
        {
            vp.Pause();
            isVideoPaused = true;

            FadeInCanvas(false);
        }
    }

    // All async methods removed - back to simple synchronous approach

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

    private void ShowRatingPopup(string experienceName, HKCarouselElementData elementData, string mediaPath)
    {
        if (xrOrigin == null)
        {
            return;
        }

        Camera xrCamera = xrOrigin.GetComponentInChildren<Camera>();
        if (xrCamera == null)
        {
            return;
        }

        Vector3 forwardDirection = xrCamera.transform.forward;
        forwardDirection.y = 0;
        forwardDirection.Normalize();

        Vector3 popupPosition = xrCamera.transform.position + forwardDirection * ratingPopupDistance;
        popupPosition.y = xrCamera.transform.position.y + ratingPopupHeight;

        if (ratingPrefab != null)
        {
            activeRatingInstance = Instantiate(ratingPrefab, popupPosition, Quaternion.LookRotation(forwardDirection));

        var ratingSystem = activeRatingInstance.GetComponent<RatingSystem>();
        if (ratingSystem != null)
        {
                isRatingPending = true;
                pendingElementData = elementData;
                pendingMediaPath = mediaPath;
                
            ratingSystem.experienceName = experienceName;
            StartCoroutine(WaitForRatingComplete(ratingSystem));
            }
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
        string fullPath = videoPath;
        
        videoPlayer.enabled = true;
        string fileName = Path.GetFileNameWithoutExtension(videoPath);
        LoadTransformData(fileName, null, false); // false = not image360 (it's a video)

        if (!File.Exists(fullPath))
        {
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

#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android, we need the file:// protocol
        videoPlayer.url = "file://" + fullPath;
#else
        videoPlayer.url = fullPath;
#endif

        // Start video preparation IMMEDIATELY (don't wait for anything)
        videoPlayer.Prepare();

        // Start ultra-fast video loading with no waiting
        StartCoroutine(UltraFastVideoLoad(videoPlayer));
    }

    private IEnumerator UltraFastVideoLoad(VideoPlayer videoPlayer)
    {
        // Clear old texture immediately 
        Texture currentTexture = sphereMaterial.GetTexture("_MainTex");
        if (currentTexture != null && currentTexture != blackTexture && currentTexture != videoRenderTexture)
        {
            DestroyImmediate(currentTexture, true);
        }

        // Set the video render texture immediately (even if video isn't ready yet)
        sphereMaterial.SetTexture("_MainTex", videoRenderTexture);

        // Do a super quick fade (just enough to feel smooth)
        var quickFade = DOTween.Sequence();
        
        // Very short fade out
        quickFade.Join(sphereMaterial.DOFloat(0f, "_Visibility", 0.15f)
            .SetEase(Ease.InOutSine));
            
        if (colorAdjustments != null)
        {
            quickFade.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                x => colorAdjustments.postExposure.value = x,
                -200f, 0.15f)  // Less extreme exposure change for speed
                .SetEase(Ease.InOutSine));
        }

        yield return quickFade.WaitForCompletion();

        // Start playing video as soon as possible (even if not fully prepared)
        if (videoPlayer.isPrepared)
        {
            videoPlayer.Play();
        }
        else
        {
            // Start playing as soon as it's ready, don't wait
            StartCoroutine(PlayVideoWhenReady(videoPlayer));
        }

        // Fade in immediately - don't wait for video to be ready
        var quickFadeIn = DOTween.Sequence();
        
        quickFadeIn.Join(sphereMaterial.DOFloat(1f, "_Visibility", 0.15f)
            .SetEase(Ease.InOutSine));
            
        if (colorAdjustments != null)
        {
            quickFadeIn.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                x => colorAdjustments.postExposure.value = x,
                0f, 0.15f)
                .SetEase(Ease.InOutSine));
        }

        yield return quickFadeIn.WaitForCompletion();
    }

    private IEnumerator PlayVideoWhenReady(VideoPlayer videoPlayer)
    {
        // Wait for preparation in background while content is visible
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }
        
        // Start playing immediately when ready
        videoPlayer.Play();
    }

    private void DisplayImageOnSphere(string imagePath)
    {
        string fullPath = imagePath;

        if (sphereMaterial == null)
        {
            return;
        }

        if (!File.Exists(fullPath))
        {
            return;
        }

        // Extract category from the original path
        string category = ExtractCategoryFromPath(imagePath);
        string fileName = Path.GetFileNameWithoutExtension(imagePath);
        
        // Start loading immediately (parallel to fading) for faster response
        StartCoroutine(FastLoadAndDisplayImage(fullPath, fileName, category));
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
                        return category;
                    }
                }
            }
            
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private IEnumerator FastLoadAndDisplayImage(string fullPath, string fileName = null, string category = null)
    {
        // Start fading and loading in parallel for speed
        var fadeTask = StartCoroutine(FadeToBlack());
        var loadTask = LoadFileAsync(fullPath);

        // Load transform data immediately 
        if (!string.IsNullOrEmpty(fileName))
        {
            LoadTransformData(fileName, category, true); // true = isImage360
        }

        // Wait for both fade and file loading to complete
        yield return fadeTask;
        
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }

        byte[] fileData = loadTask.Result;
        
        // Create texture quickly
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(fileData))
        {
            DestroyImmediate(texture, true);
            yield break;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();

        // Clear old texture and set new one
        Texture currentTexture = sphereMaterial.GetTexture("_MainTex");
        if (currentTexture != null && currentTexture != blackTexture && currentTexture != videoRenderTexture)
        {
            DestroyImmediate(currentTexture, true);
        }

        sphereMaterial.SetTexture("_MainTex", texture);

        // Start audio loading in background (don't wait for it)
        if (!string.IsNullOrEmpty(currentAudioPath))
        {
            StartCoroutine(LoadAudioInBackground());
        }

        // Fade in immediately - don't wait for audio
        yield return StartCoroutine(FadeIn());
    }

    private IEnumerator FadeToBlack()
    {
        var fadeSequence = DOTween.Sequence();
        
        // Material fade down
        fadeSequence.Join(sphereMaterial.DOFloat(0f, "_Visibility", fadeOutDuration)
            .SetEase(Ease.InOutSine));
            
        // Post-processing fade down (if available)
        if (colorAdjustments != null)
        {
            fadeSequence.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                x => colorAdjustments.postExposure.value = x,
                -10f, fadeOutDuration)
                .SetEase(Ease.InOutSine));
        }

        yield return fadeSequence.WaitForCompletion();
    }

    private IEnumerator FadeIn()
    {
        var fadeSequence = DOTween.Sequence();
         
         // Material fade up
        fadeSequence.Join(sphereMaterial.DOFloat(1f, "_Visibility", fadeInDuration)
             .SetEase(Ease.InOutSine));
             
         // Post-processing fade up (if available)
         if (colorAdjustments != null)
         {
            fadeSequence.Join(DOTween.To(() => colorAdjustments.postExposure.value,
                 x => colorAdjustments.postExposure.value = x,
                 0f, fadeInDuration)
                 .SetEase(Ease.InOutSine));
         }

        yield return fadeSequence.WaitForCompletion();
    }

    private IEnumerator LoadAudioInBackground()
    {
        // Load audio directly from resolved path (don't block the main loading)
        if (!string.IsNullOrEmpty(currentAudioPath) && audioSource != null)
        {
            // Determine audio type from extension
            string ext = Path.GetExtension(currentAudioPath).ToLowerInvariant();
            AudioType audioType = ext == ".mp3" ? AudioType.MPEG : AudioType.WAV;
            
            // Load audio directly
            StartCoroutine(PlayAudioFromPath(currentAudioPath, audioType));
        }
        yield return null; // Just ensure it starts on next frame
     }

    private string GetCorrectPath(string originalPath)
    {
        try
        {
            // Remove file:/// prefix if present
            if (originalPath.StartsWith("file:///"))
            {
                originalPath = originalPath.Substring("file:///".Length);
            }

            // Extract media type, category and filename
            string[] pathParts = originalPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

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

            if (string.IsNullOrEmpty(mediaType) || string.IsNullOrEmpty(category) || string.IsNullOrEmpty(fileName))
            {
                fileExistsCache[originalPath] = false;
                return null;
            }

            string fullPath = Path.Combine(Application.persistentDataPath, mediaType, category, fileName);
            
            bool exists = File.Exists(fullPath);
            fileExistsCache[originalPath] = exists;
            
            return exists ? fullPath : null;
        }
        catch (Exception )
        {
            return null;
        }
    }

    // Helper function specifically for Data and Audio files
    private string GetDataOrAudioPath(string relativePath)
    {
        try
        {
            string fullPath = Path.Combine(Application.persistentDataPath, relativePath);
            
            bool fileExists = File.Exists(fullPath);
            
            return fileExists ? fullPath : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Audio Methods
    /// <summary>
    /// Clean up any playing audio
    /// </summary>
    private void CleanupAudio()
    {
        if (audioSource != null)
        {
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }
            audioSource.clip = null;
        }
        currentAudioPath = null;
        isAudioPaused = false;
    }

    private IEnumerator PlayAudioFromPath(string audioPath, AudioType audioType)
    {
        using (var www = UnityWebRequestMultimedia.GetAudioClip("file://" + audioPath, audioType))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }
            }
        }
    }

    // Transform Data Methods
    private void LoadTransformData(string fileName, string category = null, bool isImage360 = false)
    {
        try
        {
            string metadataPath;

        if (!string.IsNullOrEmpty(category))
        {
                // Scene-aware path with category
                metadataPath = Path.Combine(Application.persistentDataPath, activeSceneName, "Data", category, fileName + ".txt");
        }
        else
        {
                // Try to find metadata file in any category subfolder
                string dataRoot = Path.Combine(Application.persistentDataPath, activeSceneName, "Data");
                
                if (Directory.Exists(dataRoot))
                {
                    var files = Directory.GetFiles(dataRoot, fileName + ".txt", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        metadataPath = files[0];
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

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

                    switch (key.ToLower())
                    {
                        case "rotation":
                            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float rotation))
                            {
                                if (videoSphere != null)
                                {
                                    videoSphere.rotation = Quaternion.Euler(0, rotation, 0);
                                }
                            }
                            break;

                        case "scale":
                            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale))
                            {
                                if (videoSphere != null)
                                {
                                    videoSphere.localScale = Vector3.one * scale;
                                }
                            }
                            break;
                    }
                }
            }
        }
        catch (Exception )
        {
            // Silently handle errors
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
        // Progress updates handled silently
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
                }
            }
            catch (Exception)
            {
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
        // Copy streaming assets first
        yield return StartCoroutine(CopyAndroidStreamingAssets());
        
        // Then load the assets from the copied files
        LoadAssetsFromPersistentDataPath();
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
            ThumbnailPath = null // Will be resolved by the same system as other thumbnails
        });

        yield return null; // Frame break
        
        string dataRoot = Path.Combine(Application.persistentDataPath, activeSceneName, "Data");
        
        if (!Directory.Exists(dataRoot))
        {
            cachedElements = loadedElements;
            ApplyElementsToCarousel(loadedElements);
            yield break;
        }

        // ULTRA-FAST METADATA COLLECTION: Collect all files in one go
        var allMetadataFiles = new List<string>();
        
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
        catch (Exception)
        {
            // Silent error handling
        }

        // IMMEDIATE CAROUSEL SETUP: Process all files as fast as possible
        const float maxFrameTime = 0.008f; // 8ms budget for fast loading while maintaining smoothness
        const int maxBatchSize = 10; // Larger batches for faster loading
        
        float frameStartTime = Time.realtimeSinceStartup;
        int processedCount = 0;
        
        for (int i = 0; i < allMetadataFiles.Count; i++)
        {
            // Check frame time only after processing several files
            if (processedCount > 0 && processedCount % 5 == 0 && (Time.realtimeSinceStartup - frameStartTime) > maxFrameTime)
            {
                yield return null; // Frame break only when needed
                frameStartTime = Time.realtimeSinceStartup;
                }
                
                try
                {
                var elementData = ParseMetadataFile(allMetadataFiles[i]);
                    if (elementData != null)
                    {
                        loadedElements.Add(elementData);
                }
            }
            catch (Exception)
            {
                // Silent error handling for maximum performance
            }
            
            processedCount++;
        }
        
        // Apply immediately after loading all metadata
        cachedElements = loadedElements;
        ApplyElementsToCarousel(loadedElements);
    }

    /// <summary>
    /// Ultra-fast metadata parsing: No blocking file operations during initialization
    /// Thumbnail paths are resolved lazily when actually needed
    /// </summary>
    private HKCarouselElementData ParseMetadataFile(string filePath)
    {
        // Create ExtendedCarouselElementData to properly store description and audio data
        var data = new ExtendedCarouselElementData
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            Category = Path.GetFileName(Path.GetDirectoryName(filePath)),
            VideoPath = null, // NO PRE-LOADING: Path resolved dynamically when needed
            ThumbnailPath = null // ZERO-LAG: NO thumbnail resolution during setup - pure lazy loading
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

                if (trimmedLine.StartsWith("Type:", StringComparison.OrdinalIgnoreCase))
                {
                    string type = trimmedLine.Substring("Type:".Length).Trim().ToLower();
                    data.IsImage360 = type == "image";
                }
                else if (trimmedLine.StartsWith("Audio:", StringComparison.OrdinalIgnoreCase))
                {
                    audioPath = trimmedLine.Substring("Audio:".Length).Trim();
                }
                else if (trimmedLine.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
                {
                    description = trimmedLine.Substring("Description:".Length).Trim();
                }
            }

            // Store additional data (now guaranteed to work since data is ExtendedCarouselElementData)
            data.AudioPath = audioPath;
            data.Description = description;
            
            Debug.Log($"[ParseMetadata] {data.Name} ({data.Category}) - Type: {(data.IsImage360 ? "Image" : "Video")}, Description: {description}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ParseMetadata] Failed to parse {filePath}: {ex.Message}");
            return null;
        }

            return data;
        }

    /// <summary>
    /// Simple path resolution: if data is "filename.txt", media is "filename.mp4" or "filename.png" in same category
    /// ENHANCED: Now uses normalization for robust matching (handles case, spacing, whitespace issues)
    /// </summary>
    private string ResolveMediaPath(string elementName, string category, bool isImage)
    {
        string mediaFolder = isImage ? "360 Images" : "Videos";
        string categoryPath = Path.Combine(Application.persistentDataPath, activeSceneName, mediaFolder, category);
        
        // First try: exact match (fastest path)
        string[] extensions = isImage ? new[] { ".png", ".jpg", ".jpeg" } : new[] { ".mp4", ".mov" };
        
        foreach (string ext in extensions)
        {
            string exactPath = Path.Combine(categoryPath, elementName + ext);
            if (File.Exists(exactPath))
            {
                return exactPath;
            }
        }
        
        // Second try: normalized filename matching for case/spacing issues
        if (!Directory.Exists(categoryPath))
        {
            Debug.LogWarning($"[ResolveMediaPath] Category directory not found: {categoryPath}");
            return null;
        }
        
        try
        {
            // Normalize the target filename for comparison
            string normalizedElementName = NormalizeFileNameForMatching(elementName);
            
            string[] allFiles = Directory.GetFiles(categoryPath);
            
            foreach (string filePath in allFiles)
            {
                string fileNameOnly = Path.GetFileNameWithoutExtension(filePath);
                string fileExtension = Path.GetExtension(filePath);
                
                // Check if this extension is one we're looking for
                bool isTargetExtension = false;
                foreach (string ext in extensions)
                {
                    if (string.Equals(fileExtension, ext, StringComparison.OrdinalIgnoreCase))
                    {
                        isTargetExtension = true;
                        break;
                    }
                }
                
                if (!isTargetExtension)
                    continue;
                
                string normalizedFileNameOnly = NormalizeFileNameForMatching(fileNameOnly);
                
                if (normalizedFileNameOnly == normalizedElementName)
                {
                    Debug.Log($"[ResolveMediaPath] Used normalization to find: '{elementName}' -> '{Path.GetFileName(filePath)}'");
                    return filePath;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ResolveMediaPath] Error scanning directory {categoryPath}: {ex.Message}");
        }
        
        Debug.LogWarning($"[ResolveMediaPath] No {(isImage ? "image" : "video")} file found for '{elementName}' in category '{category}'");
        return null;
    }
    
    /// <summary>
    /// Normalizes a filename for matching by handling case sensitivity, spaces, and whitespace
    /// </summary>
    private string NormalizeFileNameForMatching(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        // Trim whitespace and convert to lowercase
        string normalized = fileName.Trim().ToLowerInvariant();
        
        // Normalize multiple spaces to single spaces
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
        
        // Remove problematic characters
        normalized = normalized.Replace("\t", " ").Replace("\r", "").Replace("\n", "");
        
        // Final trim
        normalized = normalized.Trim();
        
        return normalized;
    }

    /// <summary>
    /// Read the Audio field from metadata txt file
    /// ENHANCED: Now uses normalization for robust matching (handles case, spacing, whitespace issues)
    /// </summary>
    private string GetAudioFilenameFromMetadata(string elementName, string category)
    {
        try
        {
            string dataPath = Path.Combine(Application.persistentDataPath, activeSceneName, "Data", category);
            
            // First try: exact match
            string exactMetadataPath = Path.Combine(dataPath, elementName + ".txt");
            if (File.Exists(exactMetadataPath))
            {
                return ExtractAudioFilenameFromFile(exactMetadataPath);
            }
            
            // Second try: normalized filename matching for case/spacing issues
            if (!Directory.Exists(dataPath))
            {
                Debug.LogWarning($"[GetAudioFilenameFromMetadata] Data directory not found: {dataPath}");
                return null;
            }
            
            try
            {
                // Normalize the target filename for comparison
                string normalizedElementName = NormalizeFileNameForMatching(elementName);
                
                string[] allFiles = Directory.GetFiles(dataPath, "*.txt");
                
                foreach (string filePath in allFiles)
                {
                    string fileNameOnly = Path.GetFileNameWithoutExtension(filePath);
                    string normalizedFileNameOnly = NormalizeFileNameForMatching(fileNameOnly);
                    
                    if (normalizedFileNameOnly == normalizedElementName)
                    {
                        Debug.Log($"[GetAudioFilenameFromMetadata] Used normalization to find metadata: '{elementName}' -> '{Path.GetFileName(filePath)}'");
                        return ExtractAudioFilenameFromFile(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GetAudioFilenameFromMetadata] Error scanning directory {dataPath}: {ex.Message}");
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GetAudioFilenameFromMetadata] Error processing metadata for '{elementName}': {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Helper method to extract audio filename from a metadata file
    /// </summary>
    private string ExtractAudioFilenameFromFile(string metadataPath)
    {
        try
        {
            string[] lines = File.ReadAllLines(metadataPath);
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                if (trimmedLine.StartsWith("Audio:", StringComparison.OrdinalIgnoreCase))
                {
                    string audioFilename = trimmedLine.Substring("Audio:".Length).Trim();
                    
                    // Normalize the audio filename as well to handle spacing issues
                    audioFilename = audioFilename.Trim();
                    
                    return audioFilename;
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ExtractAudioFilenameFromFile] Error reading metadata file {metadataPath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Simple audio path resolution using the filename from metadata
    /// ENHANCED: Now uses normalization for robust matching (handles case, spacing, whitespace issues)
    /// </summary>
    private string ResolveAudioPath(string audioFilename, string category)
    {
        if (string.IsNullOrEmpty(audioFilename))
            return null;
        
        string categoryPath = Path.Combine(Application.persistentDataPath, activeSceneName, "Audio", category);
        
        // First try: exact match (fastest path)
        string[] extensions = { ".mp3", ".wav", ".ogg" };
        
        foreach (string ext in extensions)
        {
            string exactPath = Path.Combine(categoryPath, audioFilename + ext);
            if (File.Exists(exactPath))
            {
                return exactPath;
            }
        }
        
        // Second try: normalized filename matching for case/spacing issues
        if (!Directory.Exists(categoryPath))
        {
            Debug.LogWarning($"[ResolveAudioPath] Category directory not found: {categoryPath}");
            return null;
        }
        
        try
        {
            // Normalize the target filename for comparison
            string normalizedAudioFilename = NormalizeFileNameForMatching(audioFilename);
            
            string[] allFiles = Directory.GetFiles(categoryPath);
            
            foreach (string filePath in allFiles)
            {
                string fileNameOnly = Path.GetFileNameWithoutExtension(filePath);
                string fileExtension = Path.GetExtension(filePath);
                
                // Check if this extension is one we're looking for
                bool isTargetExtension = false;
                foreach (string ext in extensions)
                {
                    if (string.Equals(fileExtension, ext, StringComparison.OrdinalIgnoreCase))
                    {
                        isTargetExtension = true;
                        break;
                    }
                }
                
                if (!isTargetExtension)
                    continue;
                
                string normalizedFileNameOnly = NormalizeFileNameForMatching(fileNameOnly);
                
                if (normalizedFileNameOnly == normalizedAudioFilename)
                {
                    Debug.Log($"[ResolveAudioPath] Used normalization to find: '{audioFilename}' -> '{Path.GetFileName(filePath)}'");
                    return filePath;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ResolveAudioPath] Error scanning directory {categoryPath}: {ex.Message}");
        }
        
        Debug.LogWarning($"[ResolveAudioPath] No audio file found for '{audioFilename}' in category '{category}'");
        return null;
    }

    /// <summary>
    /// Ultra-fast thumbnail resolution: Zero-lag thumbnail path building
    /// ENHANCED: Now uses normalization for robust matching (handles case, spacing, whitespace issues)
    /// </summary>
    private string ResolveThumbnailPath(string elementName, string category)
    {
        string thumbnailBasePath = Path.Combine(Application.persistentDataPath, activeSceneName, "Thumbnails");
        string categoryPath = Path.Combine(thumbnailBasePath, category);
        
        // First try: exact match (fastest path)
        string[] extensions = { ".png", ".jpg", ".jpeg" };
        
        foreach (string ext in extensions)
        {
            string exactPath = Path.Combine(categoryPath, elementName + ext);
            if (File.Exists(exactPath))
            {
                return exactPath;
            }
        }
        
        // Second try: normalized filename matching for case/spacing issues
        if (!Directory.Exists(categoryPath))
        {
            return null; // Keep silent for missing thumbnails as they're optional
        }
        
        try
        {
            // Normalize the target filename for comparison
            string normalizedElementName = NormalizeFileNameForMatching(elementName);
            
            string[] allFiles = Directory.GetFiles(categoryPath);
            
            foreach (string filePath in allFiles)
            {
                string fileNameOnly = Path.GetFileNameWithoutExtension(filePath);
                string fileExtension = Path.GetExtension(filePath);
                
                // Check if this extension is one we're looking for
                bool isTargetExtension = false;
                foreach (string ext in extensions)
                {
                    if (string.Equals(fileExtension, ext, StringComparison.OrdinalIgnoreCase))
                    {
                        isTargetExtension = true;
                        break;
                    }
                }
                
                if (!isTargetExtension)
                    continue;
                
                string normalizedFileNameOnly = NormalizeFileNameForMatching(fileNameOnly);
                
                if (normalizedFileNameOnly == normalizedElementName)
                {
                    Debug.Log($"[ResolveThumbnailPath] Used normalization to find: '{elementName}' -> '{Path.GetFileName(filePath)}'");
                    return filePath;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ResolveThumbnailPath] Error scanning directory {categoryPath}: {ex.Message}");
        }
        
        return null; // No logging for missing thumbnails - keep it silent and fast
    }

    private void ApplyElementsToCarousel(List<HKCarouselElementData> elements)
    {
        if (carousel360 == null) return;

        // ULTRA-FAST INITIALIZATION: Do only essential setup immediately
        var carouselType = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>);
        var elementsField = carouselType.GetField("_carouselElements", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var defaultIndexField = carouselType.GetField("_defaultSelectedIndex", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (elementsField != null)
        {
            // Set elements immediately
            elementsField.SetValue(carousel360, elements);

            if (elements.Count > 0 && defaultIndexField != null)
            {
                int middleIndex = Mathf.FloorToInt(elements.Count / 2f);
                defaultIndexField.SetValue(carousel360, middleIndex);
            }

            // Do essential carousel setup immediately (but lightweight)
            StartCoroutine(SetupCarouselLightweight());
        }
    }

    private IEnumerator SetupCarouselLightweight()
    {
        // Wait one frame for elements to be set
        yield return null;

        var carouselType = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>);
        
        // Create pool if needed (essential for display)
            var poolCreatedField = carouselType.GetField("_poolCreated", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (poolCreatedField != null)
            {
                bool poolCreated = (bool)poolCreatedField.GetValue(carousel360);
                if (!poolCreated)
                {
                    var createPoolMethod = carouselType.GetMethod("CreatePool", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (createPoolMethod != null)
                    {
                        createPoolMethod.Invoke(carousel360, null);
                    }
                }
            }

        // Refresh and update carousel (essential for display)
            var refreshMethod = carouselType.GetMethod("RefreshItems", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var updateMethod = carouselType.GetMethod("UpdateCarousel", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (refreshMethod != null && updateMethod != null)
            {
                refreshMethod.Invoke(carousel360, null);
                updateMethod.Invoke(carousel360, null);
        }
        
        // Ensure carousel is visible immediately (even during dimmed loading)
        yield return null;
        
        // Initialize thumbnail loading with exposure control
        CarouselElementDemo.InitializeSmartLoading(carousel360);
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
            yield return null;
        }

        // Optional: Add a small delay to ensure everything is ready
        yield return new WaitForSeconds(0.5f);

        // Activate the scene
        asyncLoad.allowSceneActivation = true;
    }
} 