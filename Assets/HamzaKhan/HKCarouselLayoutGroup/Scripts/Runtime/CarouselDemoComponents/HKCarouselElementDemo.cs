using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.Networking; // Added for UnityWebRequestTexture

namespace HKCarouselLayoutGroup
{
    public class CarouselElementDemo : MonoBehaviour, ICarouselElement<HKCarouselElementData>
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private RectTransform maskRect;

        [Header("Mask Animation")]
        [SerializeField] private float normalHeight = 389.18f;
        [SerializeField] private float selectedHeight = 300f;
        [SerializeField] private float transitionSpeed = 10f;

        [Header("Rating Display")]
        [SerializeField] private TMP_Text ratingAverageText;
        [SerializeField] private TMP_Text ratingCountText;

        [Header("Loading Feedback")]
        [SerializeField] private GameObject loadingIndicator;

        private int id;
        private string thumbnailPath;
        private HKCarouselElementData elementData;
        private bool isLoading = false;
        private bool hasLoadedThumbnail = false;

        // ASYNC THUMBNAIL LOADING SYSTEM
        private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private static Dictionary<string, List<CarouselElementDemo>> loadingQueue = new Dictionary<string, List<CarouselElementDemo>>();
        private static MonoBehaviour staticCoroutineRunner;
        private static UnityEngine.Rendering.Universal.ColorAdjustments colorAdjustments;
        private const int MAX_THUMBNAIL_SIZE = 256;
        private const float LOADING_EXPOSURE = -200f; // Match video fade exposure
        private const float NORMAL_EXPOSURE = 0f;   // Normal brightness
        private const float FADE_DURATION = 0.5f;   // Faster fade
        private const int MAX_CONCURRENT_LOADS = 5; // Allow more concurrent loading for faster startup
        private static int currentLoadingCount = 0;
        private static List<string> priorityLoadingQueue = new List<string>();
        private static float lastScrollTime = 0f; // Track when user last scrolled

        private void Awake()
        {
            // Ensure we set exposure to deep black immediately on Awake
            if (staticCoroutineRunner == null)
            {
                staticCoroutineRunner = this;
                SetupPostProcessing();
            }
        }

        private static void SetupPostProcessing()
        {
            // Find the global volume and get ColorAdjustments - do this immediately
            var globalVolume = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            if (globalVolume != null && globalVolume.profile.TryGet(out colorAdjustments))
            {
                // Force immediate update to deep black
                colorAdjustments.active = true;
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = LOADING_EXPOSURE;
            }
        }

        private void Start()
        {
            if (staticCoroutineRunner == null)
            {
                staticCoroutineRunner = this;
                SetupPostProcessing();
            }

            // Initialization is now handled by InitializeSmartLoading called from carousel controller
            // This ensures proper timing and prevents multiple initializations
        }

        [System.Serializable]
        private class RatingDataWrapper
        {
            public List<RatingEntry> entries = new List<RatingEntry>();
        }

        [System.Serializable]
        private class RatingEntry
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

        public void ConfigureElement(HKCarouselElementData elementData, int index)
        {
            this.elementData = elementData;
            this.id = index;

            // Essential setup only - no heavy operations during initialization
            if (nameText != null)
                nameText.text = elementData.Name;

            // Set category text if available (needed for description loading)
            if (categoryText != null && !string.IsNullOrEmpty(elementData.Category))
                categoryText.text = elementData.Category;

            // Build thumbnail path but don't load yet
            thumbnailPath = BuildThumbnailPath(elementData.Name, elementData.Category ?? "");

            // Show loading indicator initially
            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);

            // Defer all heavy operations to avoid initialization lag
            StartCoroutine(LoadAdditionalDataDeferred());
        }

        private IEnumerator LoadAdditionalDataDeferred()
        {
            // Wait one frame to avoid blocking initialization
            yield return null;

            // Load description (lightweight file operation)
            LoadDescriptionFromDataFile();
            
            // Wait another frame
            yield return null;
            
            // Load rating data (lightweight file operation)  
            LoadRatingDataFromNameText();
            
            // Note: Thumbnails are now handled by the smart loading system
        }

        private string BuildThumbnailPath(string elementName, string category)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return Path.Combine(Application.persistentDataPath, sceneName, "Thumbnails", category, elementName + ".png");
        }

        // Initialize async loading system
        public static void InitializeSmartLoading(HKCarouselLayoutGroup3D<HKCarouselElementData> carousel)
        {
            staticCoroutineRunner = carousel;
            
            // Find and setup post-processing
            SetupPostProcessing();
            
            // Add listener for scroll changes - now actually does something!
            carousel.OnValueChanged.AddListener(OnCarouselSelectionChanged);
            
            // Start with intelligent priority loading
            staticCoroutineRunner.StartCoroutine(InitializePriorityLoading());
        }

        private static IEnumerator InitializePriorityLoading()
        {
            float totalStartTime = Time.time; // Track total time for 4-second target
            
            // Wait for carousel to be fully initialized first
            var carousel = staticCoroutineRunner as HKCarouselLayoutGroup3D<HKCarouselElementData>;
            if (carousel != null)
            {
                // Wait until carousel is properly initialized
                while (!carousel.IsInitialized)
                {
                    yield return new WaitForSeconds(0.1f);
                }
                
                // Additional delay to ensure all carousel setup is complete
                yield return new WaitForSeconds(0.2f);
            }
            
            var allElements = FindObjectsOfType<CarouselElementDemo>();
            
            // Load essential 5 thumbnails: center, +1, +2, -1, -2
            int centerIndex = allElements.Length / 2;
            
            // Define essential loading indices (exactly 5 as requested)
            int[] essentialIndices = { 
                centerIndex,           // 0: center (most important)
                centerIndex + 1,       // 1: right neighbor
                centerIndex + 2,       // 2: extended right
                centerIndex - 1,       // -1: left neighbor  
                centerIndex - 2        // -2: extended left
            };
            
            var essentialElements = new List<CarouselElementDemo>();
            
            // Collect essential elements that need loading
            foreach (int index in essentialIndices)
            {
                if (index >= 0 && index < allElements.Length)
                {
                    var element = allElements[index];
                    if (!string.IsNullOrEmpty(element.thumbnailPath))
                    {
                        essentialElements.Add(element);
                    }
                }
            }
            
            // Start loading essential elements in parallel
            Debug.Log($"[Loading] Starting to load {essentialElements.Count} essential thumbnails...");
            foreach (var element in essentialElements)
            {
                staticCoroutineRunner.StartCoroutine(LoadThumbnailAsync(element, true));
                yield return new WaitForSeconds(0.02f); // Brief stagger
            }
            
            // Wait briefly for essential thumbnails, but respect 4-second total limit
            float maxEssentialWaitTime = 0.8f; // Max 0.8 seconds for essential thumbnails
            float essentialStartTime = Time.time;
            
            while (Time.time - essentialStartTime < maxEssentialWaitTime)
            {
                int loadedCount = 0;
                foreach (var element in essentialElements)
                {
                    if (element.hasLoadedThumbnail)
                        loadedCount++;
                }
                
                Debug.Log($"[Loading] Essential progress: {loadedCount}/{essentialElements.Count} loaded");
                
                // If we have at least 2 essential thumbnails, we can proceed early
                if (loadedCount >= 2)
                {
                    Debug.Log($"[Loading] {loadedCount} essential thumbnails ready, starting fade!");
                    break;
                }
                
                yield return new WaitForSeconds(0.1f);
            }
            
            // Calculate remaining time to hit exactly 4 seconds total
            float totalElapsed = Time.time - totalStartTime;
            float remainingTime = Mathf.Max(2.5f, 4.0f - totalElapsed); // At least 2.5 seconds for fade, max 4s total
            
            Debug.Log($"[Loading] Total elapsed: {totalElapsed:F1}s, Starting fade for {remainingTime:F1}s (total: 4s)");
            staticCoroutineRunner.StartCoroutine(TimedFadeUp(remainingTime));
            
            // Start background loading for remaining thumbnails
            staticCoroutineRunner.StartCoroutine(LoadRemainingThumbnailsWhenIdle(allElements, essentialIndices));
        }

        private static IEnumerator TimedFadeUp(float fadeDuration)
        {
            if (colorAdjustments == null) yield break;

            float elapsed = 0f;
            float startExposure = colorAdjustments.postExposure.value;
            
            Debug.Log($"[TimedFade] Starting fade from {startExposure} to {NORMAL_EXPOSURE} over {fadeDuration:F1}s");
            
            // Timed fade to normal brightness (takes exactly the specified time)
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                // Use smooth ease-in-out curve for natural feeling
                float smoothT = t * t * (3f - 2f * t); // Smoothstep function
                colorAdjustments.postExposure.value = Mathf.Lerp(startExposure, NORMAL_EXPOSURE, smoothT);
                yield return null;
            }

            // Ensure we end at exactly normal exposure
            colorAdjustments.postExposure.value = NORMAL_EXPOSURE;
            
            Debug.Log($"[TimedFade] Fade completed in {fadeDuration:F1}s");
        }

        private static IEnumerator LoadRemainingThumbnailsWhenIdle(CarouselElementDemo[] allElements, int[] alreadyLoadedIndices)
        {
            var loadedIndicesSet = new HashSet<int>(alreadyLoadedIndices);
            var remainingElements = new List<CarouselElementDemo>();
            
            // Collect elements that haven't been loaded yet
            for (int i = 0; i < allElements.Length; i++)
            {
                if (!loadedIndicesSet.Contains(i))
                {
                    var element = allElements[i];
                    if (!string.IsNullOrEmpty(element.thumbnailPath) && !element.hasLoadedThumbnail)
                    {
                        remainingElements.Add(element);
                    }
                }
            }
            
            Debug.Log($"[BackgroundLoading] {remainingElements.Count} remaining thumbnails to load when idle");
            
            // Wait a bit for user to potentially start scrolling
            yield return new WaitForSeconds(1.0f);
            
            // Load remaining thumbnails one by one during idle periods
            foreach (var element in remainingElements)
            {
                // Wait for idle period (no scrolling) before loading next thumbnail
                const float IDLE_THRESHOLD = 1.5f; // 1.5 seconds of no scrolling = idle
                
                while (Time.time - lastScrollTime < IDLE_THRESHOLD)
                {
                    // User is still scrolling, wait...
                    yield return new WaitForSeconds(0.2f);
                }
                
                // Load thumbnail if still needed and we're now idle
                if (!element.hasLoadedThumbnail && !element.isLoading)
                {
                    Debug.Log($"[BackgroundLoading] Loading {element.elementData?.Name} during idle time (last scroll: {Time.time - lastScrollTime:F1}s ago)");
                    staticCoroutineRunner.StartCoroutine(LoadThumbnailAsync(element, false));
                    
                    // Brief pause between background loads to be gentle on performance
                    yield return new WaitForSeconds(0.3f);
                }
                
                // If user started scrolling again while we were loading, respect that
                if (Time.time - lastScrollTime < IDLE_THRESHOLD)
                {
                    Debug.Log("[BackgroundLoading] User started scrolling again, pausing background loading");
                    yield return new WaitForSeconds(1.0f); // Wait a bit before resuming
                }
            }
            
            Debug.Log("[BackgroundLoading] All remaining thumbnails processed");
        }

        private static IEnumerator LoadThumbnailAsync(CarouselElementDemo element, bool isPriority)
        {
            if (element == null || string.IsNullOrEmpty(element.thumbnailPath)) yield break;
            
            // Check cache first
            if (textureCache.ContainsKey(element.thumbnailPath))
            {
                element.ApplyThumbnail(textureCache[element.thumbnailPath]);
                element.hasLoadedThumbnail = true; // Ensure flag is set when using cache
                yield break;
            }

            // Check if already loading this path
            if (loadingQueue.ContainsKey(element.thumbnailPath))
            {
                // Add to existing loading queue
                loadingQueue[element.thumbnailPath].Add(element);
                yield break;
            }

            // Wait for available loading slot if not priority
            if (!isPriority)
            {
                while (currentLoadingCount >= MAX_CONCURRENT_LOADS)
                {
                    yield return new WaitForSeconds(0.05f); // Faster check interval
                }
            }

            // Start loading
            currentLoadingCount++;
            loadingQueue[element.thumbnailPath] = new List<CarouselElementDemo> { element };
            
            // Show loading indicator
            if (element.loadingIndicator != null)
                element.loadingIndicator.SetActive(true);
            
            element.isLoading = true;

            string actualPath = element.FindThumbnailFile(element.thumbnailPath);
            if (string.IsNullOrEmpty(actualPath))
            {
                FinishLoading(element.thumbnailPath, null);
                yield break;
            }

            // Use UnityWebRequest for async loading
            string fileUrl = "file://" + actualPath;
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fileUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    
                    if (texture != null)
                    {
                        // Optimize texture
                        texture = OptimizeTexture(texture);
                        textureCache[element.thumbnailPath] = texture;
                        
                        // Apply to all elements waiting for this texture
                        var waitingElements = loadingQueue[element.thumbnailPath];
                        foreach (var waitingElement in waitingElements)
                        {
                            waitingElement.ApplyThumbnail(texture);
                            waitingElement.hasLoadedThumbnail = true; // Ensure flag is set
                        }
                        
                        Debug.Log($"[LoadThumbnail] Successfully loaded: {Path.GetFileName(actualPath)}");
                    }
                    else
                    {
                        Debug.LogWarning($"[LoadThumbnail] Failed to create texture from: {actualPath}");
                    }
                }
                else
                {
                    Debug.LogError($"[LoadThumbnail] Request failed for {actualPath}: {request.error}");
                }
                
                FinishLoading(element.thumbnailPath, null);
            }
        }

        private static void FinishLoading(string thumbnailPath, Texture2D texture)
        {
            currentLoadingCount--;
            
            if (loadingQueue.ContainsKey(thumbnailPath))
            {
                var elements = loadingQueue[thumbnailPath];
                foreach (var element in elements)
                {
                    element.isLoading = false;
                    element.hasLoadedThumbnail = true;
                    
                    // Hide loading indicator
                    if (element.loadingIndicator != null)
                        element.loadingIndicator.SetActive(false);
                }
                loadingQueue.Remove(thumbnailPath);
            }
        }

        private static Texture2D OptimizeTexture(Texture2D original)
        {
            // Always resize to optimal thumbnail size for consistency and performance
            Texture2D optimized = ResizeTextureSimple(original, MAX_THUMBNAIL_SIZE);
            
            // Apply optimal settings for UI thumbnails
            optimized.filterMode = FilterMode.Bilinear;
            optimized.wrapMode = TextureWrapMode.Clamp;
            optimized.Apply();
            
            return optimized;
        }

        private static Texture2D ResizeTextureSimple(Texture2D original, int maxSize)
        {
            int width = original.width;
            int height = original.height;
            
            // Calculate optimal size maintaining aspect ratio
            float scale = Mathf.Min((float)maxSize / width, (float)maxSize / height);
            int newWidth = Mathf.RoundToInt(width * scale);
            int newHeight = Mathf.RoundToInt(height * scale);
            
            // Use power-of-two dimensions for better GPU performance
            newWidth = Mathf.NextPowerOfTwo(newWidth);
            newHeight = Mathf.NextPowerOfTwo(newHeight);
            
            // Clamp to max size
            newWidth = Mathf.Min(newWidth, maxSize);
            newHeight = Mathf.Min(newHeight, maxSize);
            
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            
            Graphics.Blit(original, rt);
            
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            // Use RGB24 format for smaller memory footprint on thumbnails without alpha
            TextureFormat format = original.format == TextureFormat.RGB24 ? TextureFormat.RGB24 : TextureFormat.RGBA32;
            Texture2D resized = new Texture2D(newWidth, newHeight, format, false);
            resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resized.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            
            // Clean up original texture if it's different from the result
            if (original != resized)
            {
                UnityEngine.Object.DestroyImmediate(original);
            }
            
            return resized;
        }

        private static void OnCarouselSelectionChanged(int newIndex)
        {
            // Update last scroll time to track user activity
            lastScrollTime = Time.time;
            
            // Load thumbnails for current and nearby elements on demand
            var allElements = FindObjectsOfType<CarouselElementDemo>();
            
            // Load current element first (priority)
            if (newIndex >= 0 && newIndex < allElements.Length)
            {
                var currentElement = allElements[newIndex];
                if (!currentElement.hasLoadedThumbnail && !currentElement.isLoading)
                {
                    staticCoroutineRunner.StartCoroutine(LoadThumbnailAsync(currentElement, true));
                }
            }
            
            // Load immediate neighbors during scrolling (non-priority)
            int[] neighborIndices = { newIndex - 1, newIndex + 1 };
            foreach (int index in neighborIndices)
            {
                if (index >= 0 && index < allElements.Length)
                {
                    var element = allElements[index];
                    if (!element.hasLoadedThumbnail && !element.isLoading)
                    {
                        staticCoroutineRunner.StartCoroutine(LoadThumbnailAsync(element, false));
                    }
                }
            }
        }

        // Load thumbnail if needed (used for on-demand loading)
        public void LoadThumbnailIfNeeded()
        {
            if (thumbnailImage != null && !string.IsNullOrEmpty(thumbnailPath) && !hasLoadedThumbnail && !isLoading)
            {
                // Check cache first
                if (textureCache.ContainsKey(thumbnailPath))
                {
                    ApplyThumbnail(textureCache[thumbnailPath]);
                    hasLoadedThumbnail = true;
                    return;
                }
                
                // Start async loading if not already loading
                if (staticCoroutineRunner != null)
                {
                    staticCoroutineRunner.StartCoroutine(LoadThumbnailAsync(this, false));
                }
            }
        }

        public void OnElementBecameVisible()
        {
            // Trigger loading when element becomes visible
            LoadThumbnailIfNeeded();
        }

        public void OnElementBecameInvisible()
        {
            // Could be used for cleanup in the future
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

        private string FindThumbnailFile(string basePath)
        {
            string basePathWithoutExtension = Path.ChangeExtension(basePath, null);
            string directoryPath = Path.GetDirectoryName(basePathWithoutExtension);
            string fileName = Path.GetFileName(basePathWithoutExtension);
            
            // First try: exact match (fastest path)
            string[] extensions = { ".png", ".jpg", ".jpeg" };
            
            foreach (string ext in extensions)
            {
                string exactPath = Path.ChangeExtension(basePath, ext);
                if (File.Exists(exactPath))
                {
                    return exactPath;
                }
            }
            
            // Second try: normalized filename matching for case/spacing issues
            if (!Directory.Exists(directoryPath))
                return null;
            
            try
            {
                // Normalize the target filename for comparison
                string normalizedFileName = NormalizeFileNameForMatching(fileName);
                
                string[] allFiles = Directory.GetFiles(directoryPath);
                
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
                    
                    if (normalizedFileNameOnly == normalizedFileName)
                    {
                        Debug.Log($"[FindThumbnailFile] Used normalization to find: '{fileName}' -> '{Path.GetFileName(filePath)}'");
                        return filePath;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FindThumbnailFile] Error scanning directory {directoryPath}: {ex.Message}");
            }
            
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

        public void ApplyThumbnail(Texture2D texture)
        {
            if (thumbnailImage != null && texture != null)
            {
                // Clean up old sprite to prevent memory leaks
                if (thumbnailImage.sprite != null)
                {
                    var oldTexture = thumbnailImage.sprite.texture;
                    DestroyImmediate(thumbnailImage.sprite);
                    
                    // Only destroy texture if it's not in cache (to avoid destroying cached textures)
                    if (oldTexture != null && !textureCache.ContainsValue(oldTexture))
                    {
                        DestroyImmediate(oldTexture);
                    }
                }
                
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                thumbnailImage.sprite = sprite;
                
                // Hide loading indicator
                if (loadingIndicator != null)
                    loadingIndicator.SetActive(false);
                
                hasLoadedThumbnail = true;
            }
        }

        private void OnDestroy()
        {
            // Clean up sprite to prevent memory leaks
            if (thumbnailImage != null && thumbnailImage.sprite != null)
            {
                DestroyImmediate(thumbnailImage.sprite.texture);
                DestroyImmediate(thumbnailImage.sprite);
            }
        }

        public static void ClearTextureCache()
        {
            // Clean up all cached textures
            foreach (var texture in textureCache.Values)
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
            textureCache.Clear();
            loadingQueue.Clear();
            currentLoadingCount = 0;
        }

        public static void TrimCache(int maxCacheSize = 20)
        {
            // Remove oldest textures if cache grows too large
            if (textureCache.Count <= maxCacheSize) return;
            
            var sortedKeys = textureCache.Keys.ToList();
            int toRemove = textureCache.Count - maxCacheSize;
            
            for (int i = 0; i < toRemove; i++)
            {
                string key = sortedKeys[i];
                if (textureCache.ContainsKey(key))
                {
                    if (textureCache[key] != null)
                    {
                        DestroyImmediate(textureCache[key]);
                    }
                    textureCache.Remove(key);
                }
            }
        }

        // Periodically clean up cache (call this occasionally)
        public static void PeriodicCacheCleanup()
        {
            TrimCache();
        }

        private void LoadDescriptionFromDataFile()
        {
            if (descriptionText == null || elementData == null) return;

            try
            {
                // First, check if description is already available in ExtendedCarouselElementData
                if (elementData is ExtendedCarouselElementData extendedData && !string.IsNullOrEmpty(extendedData.Description))
                {
                    descriptionText.text = extendedData.Description;
                    Debug.Log($"[Description] Using cached description for {elementData.Name}: {extendedData.Description}");
                    return;
                }

                // Fallback: Load from file if not already cached
                string sceneName = SceneManager.GetActiveScene().name;
                
                // Use elementData.Category first, then fallback to categoryText, then "Unknown"
                string category = elementData.Category ?? categoryText?.text ?? "Unknown";
                
                // Try with the determined category - first exact match
                string dataPath = Path.Combine(Application.persistentDataPath, sceneName, "Data", category);
                string exactDataFilePath = Path.Combine(dataPath, elementData.Name + ".txt");

                if (File.Exists(exactDataFilePath))
                {
                    string description = ExtractDescriptionFromFile(exactDataFilePath);
                    if (!string.IsNullOrEmpty(description))
                    {
                        descriptionText.text = description;
                        
                        // Cache the description if we have ExtendedCarouselElementData
                        if (elementData is ExtendedCarouselElementData extData)
                        {
                            extData.Description = description;
                        }
                        
                        Debug.Log($"[Description] Loaded from file for {elementData.Name} ({category}): {description}");
                        return;
                    }
                }
                
                // Second try: normalized filename matching within the category
                if (Directory.Exists(dataPath))
                {
                    string description = FindDescriptionFileWithNormalization(dataPath, elementData.Name);
                    if (!string.IsNullOrEmpty(description))
                    {
                        descriptionText.text = description;
                        
                        // Cache the description if we have ExtendedCarouselElementData
                        if (elementData is ExtendedCarouselElementData extData)
                        {
                            extData.Description = description;
                        }
                        
                        return;
                    }
                }
                
                // If file not found, try searching all category folders as fallback with normalization
                string dataRoot = Path.Combine(Application.persistentDataPath, sceneName, "Data");
                if (Directory.Exists(dataRoot))
                {
                    foreach (var categoryDir in Directory.GetDirectories(dataRoot))
                    {
                        string description = FindDescriptionFileWithNormalization(categoryDir, elementData.Name);
                        if (!string.IsNullOrEmpty(description))
                        {
                            descriptionText.text = description;
                            
                            // Cache the description if we have ExtendedCarouselElementData
                            if (elementData is ExtendedCarouselElementData extData)
                            {
                                extData.Description = description;
                            }
                            
                            Debug.Log($"[Description] Found via fallback normalization for {elementData.Name}: {description}");
                            return;
                        }
                    }
                }
                
                Debug.LogWarning($"[Description] No description found for {elementData.Name} in any location");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Description] Failed to load for {elementData?.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Helper method to find description file using normalization
        /// </summary>
        private string FindDescriptionFileWithNormalization(string dataPath, string elementName)
        {
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
                        Debug.Log($"[Description] Used normalization to find data file: '{elementName}' -> '{Path.GetFileName(filePath)}'");
                        return ExtractDescriptionFromFile(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FindDescriptionFileWithNormalization] Error scanning directory {dataPath}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Helper method to extract description from a data file
        /// </summary>
        private string ExtractDescriptionFromFile(string dataFilePath)
        {
            try
            {
                string[] lines = File.ReadAllLines(dataFilePath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
                    {
                        string description = line.Substring("Description:".Length).Trim();
                        return description;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ExtractDescriptionFromFile] Error reading data file {dataFilePath}: {ex.Message}");
                return null;
            }
        }

        private void LoadRatingDataFromNameText()
        {
            if (nameText == null) return;

            try
            {
                MonoBehaviour controller = FindFirstObjectByType<XR360CarouselController>();
                if (controller == null)
                {
                    controller = FindFirstObjectByType<XRCarouselInputController>();
                }

                if (controller == null)
                {
                    return;
                }

                var ratingsProperty = controller.GetType().GetProperty("CurrentRatings", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                
                if (ratingsProperty != null)
                {
                    var ratingsDict = ratingsProperty.GetValue(controller) as Dictionary<string, RatingData>;
                    
                    if (ratingsDict != null && ratingsDict.TryGetValue(nameText.text, out RatingData ratingData))
                    {
                        if (ratingAverageText != null)
                        {
                            ratingAverageText.text = ratingData.average.ToString("F1");
                        }
                        
                        if (ratingCountText != null)
                        {
                            ratingCountText.text = ratingData.count.ToString();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silent error handling
            }
        }

        public void OnCardElementSelected() { }
        public void OnCardElementDeselected() { }
        public void SetCategoryAlpha(float alpha) { }
        public int GetID() => id;
    }
}

