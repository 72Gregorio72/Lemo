using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using System;
using System.Collections;

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

        private int id;

        // 🎯 SMART LOADING: Only load visible thumbnails (center + neighbors)
        private static Dictionary<int, CarouselElementDemo> allElements = new Dictionary<int, CarouselElementDemo>();
        private static HashSet<int> loadedThumbnails = new HashSet<int>();
        private static int currentCenterIndex = -1;
        private static bool isUpdatingThumbnails = false;
        
        // 🚀 PERFORMANCE: Debouncing and async loading to prevent frame drops
        private static Coroutine debounceCoroutine;
        private static MonoBehaviour staticCoroutineRunner;
        private static float debounceDelay = 0.5f; // Increased to 500ms - wait longer before loading
        private static bool isCurrentlyLoading = false; // Prevent overlapping loads

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

        public void ConfigureElement(HKCarouselElementData data, int index)
        {
            id = index;
            thumbnailPath = data.ThumbnailPath; // Store for smart loading

            if (nameText != null) nameText.text = data.Name;
            if (categoryText != null) categoryText.text = data.Category ?? "UNKNOWN";

            // 🎯 SMART LOADING: Register element and load only if visible
            allElements[index] = this;
            Debug.Log($"[SMART_LOAD] 📋 Registered element {index}: {data.Name}");

            LoadRatingDataFromNameText();
            LoadDescriptionFromDataFile();
        }

        private string thumbnailPath; // Store thumbnail path for smart loading

        /// <summary>
        /// 🚀 PERSISTENT SCROLLING: Call this when carousel selection changes to load missing thumbnails
        /// Uses debouncing to prevent frame drops during rapid scrolling
        /// Works together with progressive loading - only loads missing thumbnails (no unloading)
        /// All thumbnails remain in memory once loaded for instant browsing
        /// </summary>
        public static void UpdateVisibleThumbnails(int centerIndex, int totalElements)
        {
            // 🚀 DEBOUNCING: Cancel previous loading and wait before starting new one
            if (staticCoroutineRunner != null && debounceCoroutine != null)
            {
                staticCoroutineRunner.StopCoroutine(debounceCoroutine);
            }

            // Start debounced loading (will only load missing thumbnails)
            if (staticCoroutineRunner != null)
            {
                debounceCoroutine = staticCoroutineRunner.StartCoroutine(DebouncedThumbnailUpdate(centerIndex, totalElements));
            }
        }

        /// <summary>
        /// 🚀 DEBOUNCED LOADING: Wait before updating thumbnails to prevent frame drops
        /// </summary>
        private static IEnumerator DebouncedThumbnailUpdate(int centerIndex, int totalElements)
        {
            Debug.Log($"[SMART_LOAD] 🚀 Debouncing thumbnail update for center index: {centerIndex}");
            
            // 🚀 PREVENT OVERLAPPING: Don't start if already loading
            if (isCurrentlyLoading || isUpdatingThumbnails)
            {
                Debug.Log($"[SMART_LOAD] ⏸️ Skipping update - already loading");
                yield break;
            }
            
            // Wait for user to stop scrolling rapidly
            yield return new WaitForSeconds(debounceDelay);
            
            // 🚀 DOUBLE CHECK: Still not loading after delay?
            if (isCurrentlyLoading || isUpdatingThumbnails) yield break;
            
            isCurrentlyLoading = true;
            isUpdatingThumbnails = true;

            Debug.Log($"[SMART_LOAD] 🎯 Executing debounced thumbnail update for center index: {centerIndex}");

            // 🎯 PERSISTENT THUMBNAILS: Keep all loaded thumbnails in memory (no unloading)
            Debug.Log($"[SMART_LOAD] 💾 Keeping all {loadedThumbnails.Count} loaded thumbnails persistent");

            // 🚀 PRIORITY LOADING: Load immediate neighbors if not already loaded by progressive system
            HashSet<int> priorityIndices = new HashSet<int>();
            for (int offset = -1; offset <= 1; offset++)
            {
                int targetIndex = centerIndex + offset;
                // Handle wrapping for circular carousel
                if (targetIndex < 0) targetIndex = totalElements - 1;
                if (targetIndex >= totalElements) targetIndex = 0;
                priorityIndices.Add(targetIndex);
            }

            Debug.Log($"[SMART_LOAD] Checking priority indices: [{string.Join(", ", priorityIndices)}] (center + neighbors)");

            // Load any missing priority thumbnails (center + immediate neighbors)
            foreach (int priorityIndex in priorityIndices)
            {
                if (!loadedThumbnails.Contains(priorityIndex) && allElements.TryGetValue(priorityIndex, out CarouselElementDemo element))
                {
                    if (!string.IsNullOrEmpty(element.thumbnailPath))
                    {
                        element.StartCoroutine(element.LoadThumbnailOptimizedAsync(element.thumbnailPath));
                        loadedThumbnails.Add(priorityIndex);
                        Debug.Log($"[SMART_LOAD] 📸 Loading missing priority thumbnail for index {priorityIndex}");
                    }
                }
                else if (loadedThumbnails.Contains(priorityIndex))
                {
                    Debug.Log($"[SMART_LOAD] ✅ Priority thumbnail for index {priorityIndex} already loaded");
                }
                // 🚀 ULTRA SMOOTH: Multiple frame breaks between each operation
                yield return null;
                yield return null;
            }

            currentCenterIndex = centerIndex;
            isUpdatingThumbnails = false;
            isCurrentlyLoading = false; // 🚀 RESET LOADING FLAG
            
            Debug.Log($"[SMART_LOAD] ✅ Debounced thumbnail update completed for center index: {centerIndex}");
        }

        /// <summary>
        /// 💾 PERSISTENT THUMBNAILS: Thumbnails remain loaded once created (no unloading)
        /// This method removed as we now keep all thumbnails in memory permanently
        /// </summary>
        // private void UnloadThumbnail() - REMOVED: Thumbnails are now persistent

        /// <summary>
        /// 🎯 SMART LOADING: Initialize smart thumbnail loading system
        /// Call this once after all carousel elements are configured
        /// </summary>
        public static void InitializeSmartLoading(HKCarouselLayoutGroup3D<HKCarouselElementData> carousel)
        {
            if (carousel == null) return;

            Debug.Log($"[SMART_LOAD] 🚀 Initializing smart loading for {allElements.Count} elements");

            // Set up coroutine runner for debouncing
            staticCoroutineRunner = carousel;
            
            // Subscribe to carousel selection changes
            carousel.OnValueChanged.RemoveAllListeners(); // Clear any existing listeners
            carousel.OnValueChanged.AddListener(OnCarouselSelectionChanged);

            // 🎯 PROGRESSIVE LOADING: Start loading from center and expand outward
            int currentIndex = carousel.GetTrueSelectedIndex();
            int totalElements = allElements.Count;
            
            Debug.Log($"[SMART_LOAD] Starting progressive loading from center index: {currentIndex}, total elements: {totalElements}");
            
            if (totalElements > 0)
            {
                // Start progressive loading from center outward
                staticCoroutineRunner.StartCoroutine(ProgressiveLoadAllThumbnails(currentIndex, totalElements));
            }
        }

        /// <summary>
        /// 🎯 PROGRESSIVE LOADING: Load thumbnails from center outward for smooth startup with full coverage
        /// Priority: Center first, then adjacent (-1,+1), then next ring (-2,+2), etc.
        /// </summary>
        private static IEnumerator ProgressiveLoadAllThumbnails(int centerIndex, int totalElements)
        {
            Debug.Log($"[PROGRESSIVE] 🎯 Starting progressive loading from center {centerIndex}");
            
            // Track what we've loaded to avoid duplicates
            HashSet<int> alreadyLoaded = new HashSet<int>();
            
            // 🎯 PRIORITY 1: Load center immediately (most important)
            yield return staticCoroutineRunner.StartCoroutine(LoadSingleThumbnail(centerIndex, totalElements, alreadyLoaded, "CENTER"));
            
            // 🎯 PRIORITY 2: Load immediate neighbors (-1, +1)
            yield return new WaitForSeconds(0.3f); // Short delay before neighbors
            yield return staticCoroutineRunner.StartCoroutine(LoadSingleThumbnail(GetWrappedIndex(centerIndex - 1, totalElements), totalElements, alreadyLoaded, "LEFT"));
            yield return new WaitForSeconds(0.2f);
            yield return staticCoroutineRunner.StartCoroutine(LoadSingleThumbnail(GetWrappedIndex(centerIndex + 1, totalElements), totalElements, alreadyLoaded, "RIGHT"));
            
            // 🎯 PRIORITY 3+: Load remaining thumbnails in expanding rings
            int maxRadius = Mathf.CeilToInt(totalElements / 2f);
            
            for (int radius = 2; radius <= maxRadius; radius++)
            {
                yield return new WaitForSeconds(0.4f); // Longer delay for non-priority thumbnails
                
                Debug.Log($"[PROGRESSIVE] 📍 Loading ring {radius} around center {centerIndex}");
                
                // Load left side of ring (-radius)
                int leftIndex = GetWrappedIndex(centerIndex - radius, totalElements);
                if (!alreadyLoaded.Contains(leftIndex))
                {
                    yield return staticCoroutineRunner.StartCoroutine(LoadSingleThumbnail(leftIndex, totalElements, alreadyLoaded, $"RING-{radius}-LEFT"));
                    yield return new WaitForSeconds(0.2f);
                }
                
                // Load right side of ring (+radius)
                int rightIndex = GetWrappedIndex(centerIndex + radius, totalElements);
                if (!alreadyLoaded.Contains(rightIndex))
                {
                    yield return staticCoroutineRunner.StartCoroutine(LoadSingleThumbnail(rightIndex, totalElements, alreadyLoaded, $"RING-{radius}-RIGHT"));
                    yield return new WaitForSeconds(0.2f);
                }
                
                // If we've loaded everything, break early
                if (alreadyLoaded.Count >= totalElements)
                {
                    break;
                }
            }
            
            Debug.Log($"[PROGRESSIVE] ✅ Progressive loading completed! Loaded {alreadyLoaded.Count}/{totalElements} thumbnails");
            Debug.Log($"[PROGRESSIVE] 🎉 All thumbnails are now available for instant browsing!");
        }

        /// <summary>
        /// 🎯 PROGRESSIVE LOADING: Load a single thumbnail with tracking
        /// </summary>
        private static IEnumerator LoadSingleThumbnail(int index, int totalElements, HashSet<int> alreadyLoaded, string priority)
        {
            if (alreadyLoaded.Contains(index)) yield break;
            
            if (allElements.TryGetValue(index, out CarouselElementDemo element))
            {
                if (!string.IsNullOrEmpty(element.thumbnailPath))
                {
                    Debug.Log($"[PROGRESSIVE] 📸 Loading {priority} thumbnail for index {index}");
                    element.StartCoroutine(element.LoadThumbnailOptimizedAsync(element.thumbnailPath));
                    loadedThumbnails.Add(index);
                    alreadyLoaded.Add(index);
                    
                    // Small delay to let the thumbnail start loading
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        /// <summary>
        /// 🎯 PROGRESSIVE LOADING: Handle circular array wrapping
        /// </summary>
        private static int GetWrappedIndex(int index, int totalElements)
        {
            if (index < 0) return totalElements + index;
            if (index >= totalElements) return index - totalElements;
            return index;
        }

        /// <summary>
        /// 🔄 SMART SCROLLING: Handle carousel selection changes 
        /// Works as backup to progressive loading - ensures visible thumbnails are available
        /// </summary>
        private static void OnCarouselSelectionChanged(int newCenterIndex)
        {
            Debug.Log($"[SMART_LOAD] 🔄 Carousel selection changed to index: {newCenterIndex}");
            
            if (allElements.Count > 0)
            {
                // Check if visible thumbnails need loading (progressive system may have already loaded them)
                UpdateVisibleThumbnails(newCenterIndex, allElements.Count);
            }
        }

        /// <summary>
        /// 🚀 PERFORMANCE OPTIMIZED: Loads thumbnails async with ultra compression to prevent frame drops
        /// </summary>
        private IEnumerator LoadThumbnailOptimizedAsync(string thumbnailPath)
        {
            const int MAX_THUMBNAIL_SIZE = 256; // Good quality for thumbnails (256x256 max)
            
            Debug.Log($"[THUMBNAIL_OPT] 🧠 Loading optimized thumbnail: {Path.GetFileName(thumbnailPath)}");
            
            // 🚀 ULTRA SMOOTH: Multiple frame breaks for zero frame drops
            yield return null;
            yield return null;
            
            // Clean up any existing sprite to free memory
            if (thumbnailImage.sprite != null)
            {
                var oldTexture = thumbnailImage.sprite.texture;
                DestroyImmediate(thumbnailImage.sprite);
                if (oldTexture != null) DestroyImmediate(oldTexture);
            }
            
            // 🚀 CLEANUP FRAME BREAK
            yield return null;
            
            byte[] fileData = null;
            Texture2D originalTexture = null;
            Texture2D finalTexture = null;
            
            // 🚀 FRAME 1: Load file data with error handling
            try
            {
                fileData = File.ReadAllBytes(thumbnailPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[THUMBNAIL_OPT] ❌ Error reading thumbnail file {thumbnailPath}: {ex.Message}");
                yield break;
            }
            
            yield return null; // 🚀 FRAME BREAK 1: Don't block the main thread
            yield return null; // 🚀 EXTRA FRAME BREAK 1A
            
            // 🚀 FRAME 2: Create texture with error handling
            try
            {
                originalTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!originalTexture.LoadImage(fileData))
                {
                    Debug.LogWarning($"[THUMBNAIL_OPT] ⚠️ Failed to load thumbnail: {thumbnailPath}");
                    if (originalTexture != null) DestroyImmediate(originalTexture);
                    yield break;
                    }
                }
                catch (Exception ex)
            {
                Debug.LogError($"[THUMBNAIL_OPT] ❌ Error creating texture for {thumbnailPath}: {ex.Message}");
                if (originalTexture != null) DestroyImmediate(originalTexture);
                yield break;
            }
            
            yield return null; // 🚀 FRAME BREAK 2: Don't block the main thread
            yield return null; // 🚀 EXTRA FRAME BREAK 2A
            yield return null; // 🚀 EXTRA FRAME BREAK 2B
            
            // 🚀 FRAME 3: Resize if too large (memory optimization)
            bool needsResize = originalTexture.width > MAX_THUMBNAIL_SIZE || originalTexture.height > MAX_THUMBNAIL_SIZE;
            int newWidth = originalTexture.width;
            int newHeight = originalTexture.height;
            
            if (needsResize)
            {
                Debug.Log($"[THUMBNAIL_OPT] 📏 Resizing thumbnail from {originalTexture.width}x{originalTexture.height} to max {MAX_THUMBNAIL_SIZE}");
                
                // Calculate new size maintaining aspect ratio
                float aspectRatio = (float)originalTexture.width / originalTexture.height;
                if (aspectRatio > 1.0f)
                {
                    newWidth = MAX_THUMBNAIL_SIZE;
                    newHeight = Mathf.RoundToInt(MAX_THUMBNAIL_SIZE / aspectRatio);
                }
                else
                {
                    newWidth = Mathf.RoundToInt(MAX_THUMBNAIL_SIZE * aspectRatio);
                    newHeight = MAX_THUMBNAIL_SIZE;
                }
            }
            
            yield return null; // 🚀 FRAME BREAK 3: Don't block during calculations
            
            try
            {
                if (needsResize)
                {
                    // Create resized texture
                    finalTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
                    
                    // Simple resize using RenderTexture (more memory efficient than other methods)
                    RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
                    Graphics.Blit(originalTexture, rt);
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    finalTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
                    finalTexture.Apply();
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(rt);
                    
                    // Clean up original texture
                    DestroyImmediate(originalTexture);
                    
                    Debug.Log($"[THUMBNAIL_OPT] ✅ Resized to {newWidth}x{newHeight}, estimated memory: {(newWidth * newHeight * 4 / 1024)}KB");
                }
                else
                {
                    finalTexture = originalTexture;
                    Debug.Log($"[THUMBNAIL_OPT] ✅ Using original size {finalTexture.width}x{finalTexture.height}, estimated memory: {(finalTexture.width * finalTexture.height * 4 / 1024)}KB");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[THUMBNAIL_OPT] ❌ Error processing texture resize {thumbnailPath}: {ex.Message}");
                if (originalTexture != null) DestroyImmediate(originalTexture);
                if (finalTexture != null) DestroyImmediate(finalTexture);
                yield break;
            }
            
            yield return null; // 🚀 FRAME BREAK 4: Don't block before sprite creation
            yield return null; // 🚀 EXTRA FRAME BREAK 4A
            yield return null; // 🚀 EXTRA FRAME BREAK 4B
            
            // 🚀 FRAME 4: Create sprite and apply to UI
            Sprite thumbnailSprite = null;
            try
            {
                thumbnailSprite = Sprite.Create(finalTexture, 
                    new Rect(0, 0, finalTexture.width, finalTexture.height), 
                    new Vector2(0.5f, 0.5f));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[THUMBNAIL_OPT] ❌ Error creating sprite {thumbnailPath}: {ex.Message}");
                if (finalTexture != null) DestroyImmediate(finalTexture);
            }
            
            yield return null; // 🚀 SPRITE CREATION FRAME BREAK
            
            // Apply sprite to UI if creation was successful
            if (thumbnailSprite != null)
            {
                thumbnailImage.sprite = thumbnailSprite;
                Debug.Log($"[THUMBNAIL_OPT] 🎉 Successfully loaded optimized thumbnail for {Path.GetFileName(thumbnailPath)}");
            }
            
            yield return null; // 🚀 FINAL FRAME BREAK 1: Ensure smooth operation
            yield return null; // 🚀 FINAL FRAME BREAK 2: Extra smooth
            yield return null; // 🚀 FINAL FRAME BREAK 3: Ultra smooth
        }

        public int GetID() => id;

        private void LoadRatingDataFromNameText()
        {
            if (nameText == null || string.IsNullOrWhiteSpace(nameText.text))
            {
                SetRatingDisplay(0f, 0);
                return;
            }

            string key = nameText.text.Trim();

            // Try to get reference to either controller type
            var carouselController = FindFirstObjectByType<XRCarouselInputController>();
            var carousel360Controller = FindFirstObjectByType<XR360CarouselController>();

            if (carouselController == null && carousel360Controller == null)
            {
                Debug.LogWarning("[CarouselElementDemo] Could not find either XRCarouselInputController or XR360CarouselController!");
                SetRatingDisplay(0f, 0);
                return;
            }

            // Load rating data using the available controller
            Dictionary<string, RatingData> ratingData;
            if (carouselController != null)
            {
                var inputControllerData = carouselController.LoadRatingData();
                ratingData = new Dictionary<string, RatingData>();
                foreach (var kvp in inputControllerData)
                {
                    ratingData[kvp.Key] = new RatingData { average = kvp.Value.average, count = kvp.Value.count };
                }
            }
            else
            {
                // Use the same rating file path and loading logic as XR360CarouselController
                string ratingPath = Path.Combine(Application.persistentDataPath, "Rating", "ratings.json");
                if (!File.Exists(ratingPath))
                {
                    SetRatingDisplay(0f, 0);
                    return;
                }

                try
                {
                    string json = File.ReadAllText(ratingPath);
                    var wrapper = JsonUtility.FromJson<RatingDataWrapper>(json);
                    ratingData = ConvertFromWrapper(wrapper);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[CarouselElementDemo] Error loading rating data: {e.Message}");
                    ratingData = new Dictionary<string, RatingData>();
                }
            }
            
            if (ratingData.TryGetValue(key, out RatingData data))
            {
                SetRatingDisplay(data.average, data.count);
            }
            else
            {
                SetRatingDisplay(0f, 0);
            }
        }

        public void SetCategoryAlpha(float alpha)
        {
            if (categoryText != null)
            {
                var c = categoryText.color;
                c.a = alpha;
                categoryText.color = c;
            }

            // Handle mask height transition
            if (maskRect != null)
            {
                // alpha is 1 when centered/selected, 0 when far away
                float targetHeight = Mathf.Lerp(normalHeight, selectedHeight, alpha);
                Vector2 sizeDelta = maskRect.sizeDelta;
                sizeDelta.y = Mathf.Lerp(sizeDelta.y, targetHeight, Time.deltaTime * transitionSpeed);
                maskRect.sizeDelta = sizeDelta;
            }
        }

        private void LoadDescriptionFromDataFile()
        {
            if (descriptionText == null || nameText == null || string.IsNullOrWhiteSpace(nameText.text))
                return;

            string fileName = nameText.text.Trim();
            TextAsset descriptionFile = Resources.Load<TextAsset>($"Data/{fileName}");
            
            if (descriptionFile != null)
            {
                // Split the text file into lines and find the Description line
                string[] lines = descriptionFile.text.Split('\n');
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("Description:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        string description = trimmedLine.Substring("Description:".Length).Trim();
                        descriptionText.text = description;
                        return;
                    }
                }
                
                // If we get here, no Description line was found
                Debug.LogWarning($"[Description] No Description line found in {fileName}.txt");
                descriptionText.text = string.Empty;
            }
            else
            {
                Debug.LogWarning($"[Description] Data file not found: {fileName}.txt");
                descriptionText.text = string.Empty;
            }
        }

        private void SetRatingDisplay(float average, int count)
        {
            if (ratingAverageText != null)
                ratingAverageText.text = average.ToString("F1");
            
            if (ratingCountText != null)
                ratingCountText.text = $"({count})";
        }

        /// <summary>
        /// 🧹 MEMORY CLEANUP: Free thumbnail memory when element is destroyed
        /// </summary>
        private void OnDestroy()
        {
            CleanupThumbnail();
        }

        /// <summary>
        /// 🧹 MEMORY CLEANUP: Properly dispose of thumbnail textures to free memory
        /// </summary>
        private void CleanupThumbnail()
        {
            if (thumbnailImage != null && thumbnailImage.sprite != null)
            {
                var texture = thumbnailImage.sprite.texture;
                DestroyImmediate(thumbnailImage.sprite);
                if (texture != null) 
                {
                    DestroyImmediate(texture);
                    Debug.Log("[THUMBNAIL_OPT] 🧹 Cleaned up thumbnail texture memory");
                }
                thumbnailImage.sprite = null;
            }
        }
    }
}
