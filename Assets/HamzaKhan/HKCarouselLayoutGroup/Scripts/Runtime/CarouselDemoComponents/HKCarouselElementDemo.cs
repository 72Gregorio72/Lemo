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

        // 🚀 ULTRA OPTIMIZED LOADING SYSTEM
        private static Dictionary<int, CarouselElementDemo> allElements = new Dictionary<int, CarouselElementDemo>();
        private static HashSet<int> loadedThumbnails = new HashSet<int>();
        private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private static Queue<ThumbnailLoadJob> loadQueue = new Queue<ThumbnailLoadJob>();
        private static bool isProcessingQueue = false;
        private static int maxConcurrentLoads = 2;
        private static int currentConcurrentLoads = 0;
        
        // 🚀 PERFORMANCE SETTINGS
        private static MonoBehaviour staticCoroutineRunner;
        private static float debounceDelay = 0.1f; // Very fast response
        private static Coroutine debounceCoroutine;
        private static bool isCurrentlyLoading = false;
        private const int MAX_THUMBNAIL_SIZE = 256;

        // 🚀 FRAME BUDGET SYSTEM
        private static float frameStartTime;
        private static float maxFrameTime = 0.012f; // 12ms budget for 60fps with headroom

        private class ThumbnailLoadJob
        {
            public string filePath;
            public CarouselElementDemo element;
            public int priority;
            public bool isUrgent;
            
            public ThumbnailLoadJob(string path, CarouselElementDemo elem, int prio, bool urgent)
            {
                filePath = path;
                element = elem;
                priority = prio;
                isUrgent = urgent;
            }
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
            
            // 🚀 LAZY THUMBNAIL RESOLUTION: Build thumbnail path dynamically (no file I/O during init)
            if (string.IsNullOrEmpty(data.ThumbnailPath))
            {
                // Construct thumbnail path without file system checks
                thumbnailPath = BuildThumbnailPath(data.Name, data.Category);
            }
            else
            {
                thumbnailPath = data.ThumbnailPath;
            }

            if (nameText != null) nameText.text = data.Name;
            if (categoryText != null) categoryText.text = data.Category ?? "UNKNOWN";

            // Register element for optimized loading
            allElements[index] = this;

            // 🚀 ULTRA-FAST SETUP: Defer rating and description loading to avoid blocking carousel creation
            StartCoroutine(LoadAdditionalDataDeferred());
        }

        /// <summary>
        /// 🚀 DEFERRED LOADING: Load rating and description data after carousel setup is complete
        /// </summary>
        private IEnumerator LoadAdditionalDataDeferred()
        {
            // Wait for carousel to finish initial setup
            yield return new WaitForSeconds(0.5f);
            
            // Load additional data without blocking
            LoadRatingDataFromNameText();
            
            // Small delay between operations
            yield return new WaitForEndOfFrame();
            
            LoadDescriptionFromDataFile();
        }

        /// <summary>
        /// 🚀 LAZY PATH BUILDING: Construct thumbnail path without file system access
        /// </summary>
        private string BuildThumbnailPath(string elementName, string category)
        {
            // Build path based on expected structure - no file existence check
            return Path.Combine(Application.persistentDataPath, "Thumbnails", category, elementName + ".png");
        }

        /// <summary>
        /// 🚀 ASYNC THUMBNAIL PATH RESOLUTION: Verify thumbnail exists and find alternatives
        /// </summary>
        private static IEnumerator ResolveThumbnailPathAsync(string elementName, string category, System.Action<string> onComplete)
        {
            string[] extensions = { ".png", ".jpg" };
            string thumbnailBasePath = Path.Combine(Application.persistentDataPath, "Thumbnails");
            string categoryPath = Path.Combine(thumbnailBasePath, category);
            
            // Check if we can resolve this on a background thread
            bool pathResolved = false;
            string resolvedPath = null;
            
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (Directory.Exists(categoryPath))
                    {
                        foreach (string ext in extensions)
                        {
                            string fullPath = Path.Combine(categoryPath, elementName + ext);
                            if (File.Exists(fullPath))
                            {
                                resolvedPath = fullPath;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[THUMBNAIL_RESOLVE] Error resolving path for {elementName}: {ex.Message}");
                }
                finally
                {
                    pathResolved = true;
                }
            });
            
            // Wait for background resolution
            while (!pathResolved)
            {
                yield return null;
            }
            
            onComplete?.Invoke(resolvedPath);
        }

        private string thumbnailPath;

        /// <summary>
        /// 🚀 ULTRA FAST LOADING: Optimized thumbnail loading system
        /// </summary>
        public static void UpdateVisibleThumbnails(int centerIndex, int totalElements)
        {
            // Cancel previous debounce
            if (staticCoroutineRunner != null && debounceCoroutine != null)
            {
                staticCoroutineRunner.StopCoroutine(debounceCoroutine);
            }

            if (staticCoroutineRunner != null)
            {
                debounceCoroutine = staticCoroutineRunner.StartCoroutine(DebouncedThumbnailUpdate(centerIndex, totalElements));
            }
        }

        private static IEnumerator DebouncedThumbnailUpdate(int centerIndex, int totalElements)
        {
            if (isCurrentlyLoading)
            {
                yield break;
            }
            
            yield return new WaitForSeconds(debounceDelay);
            
            if (isCurrentlyLoading) yield break;
            
            isCurrentlyLoading = true;

            // 🚀 PRIORITY LOADING: Load only center + immediate neighbors
            var urgentJobs = new List<ThumbnailLoadJob>();
            
            for (int offset = -1; offset <= 1; offset++)
            {
                int targetIndex = centerIndex + offset;
                if (targetIndex < 0) targetIndex = totalElements - 1;
                if (targetIndex >= totalElements) targetIndex = 0;
                
                if (allElements.TryGetValue(targetIndex, out CarouselElementDemo element))
                {
                    if (!string.IsNullOrEmpty(element.thumbnailPath) && !loadedThumbnails.Contains(targetIndex))
                    {
                        int priority = (offset == 0) ? 1 : 2;
                        bool isUrgent = (offset == 0); // Only center is urgent
                        urgentJobs.Add(new ThumbnailLoadJob(element.thumbnailPath, element, priority, isUrgent));
                        loadedThumbnails.Add(targetIndex);
                    }
                }
            }

            // 🚀 IMMEDIATE PROCESSING: Process urgent jobs right away
            foreach (var job in urgentJobs)
            {
                if (job.isUrgent)
                {
                    yield return staticCoroutineRunner.StartCoroutine(ProcessThumbnailJob(job));
                }
                else
                {
                    QueueThumbnailJob(job);
                }
            }

            isCurrentlyLoading = false;
        }

        /// <summary>
        /// 🚀 JOB QUEUE SYSTEM: Add thumbnail job to queue
        /// </summary>
        private static void QueueThumbnailJob(ThumbnailLoadJob job)
        {
            loadQueue.Enqueue(job);
            
            if (!isProcessingQueue)
            {
                staticCoroutineRunner.StartCoroutine(ProcessLoadQueue());
            }
        }

        /// <summary>
        /// 🚀 QUEUE PROCESSOR: Process thumbnail jobs with frame budget
        /// </summary>
        private static IEnumerator ProcessLoadQueue()
        {
            isProcessingQueue = true;
            
            while (loadQueue.Count > 0)
            {
                frameStartTime = Time.realtimeSinceStartup;
                
                // Process jobs within frame budget
                while (loadQueue.Count > 0 && currentConcurrentLoads < maxConcurrentLoads)
                {
                    if ((Time.realtimeSinceStartup - frameStartTime) > maxFrameTime)
                    {
                        yield return null; // Frame break
                        frameStartTime = Time.realtimeSinceStartup;
                    }
                    
                    var job = loadQueue.Dequeue();
                    staticCoroutineRunner.StartCoroutine(ProcessThumbnailJob(job));
                    currentConcurrentLoads++;
                }
                
                yield return null; // Frame break between batches
            }
            
            isProcessingQueue = false;
        }

        /// <summary>
        /// 🚀 ULTRA-OPTIMIZED THUMBNAIL PROCESSING: Enhanced with async path resolution
        /// </summary>
        private static IEnumerator ProcessThumbnailJob(ThumbnailLoadJob job)
        {
            // Check cache first
            if (textureCache.ContainsKey(job.filePath))
            {
                job.element.ApplyThumbnail(textureCache[job.filePath]);
                currentConcurrentLoads--;
                yield break;
            }

            // 🚀 ASYNC PATH VERIFICATION: Verify thumbnail path exists or find alternative
            bool pathVerified = false;
            string verifiedPath = job.filePath;
            
            // Extract element info for path resolution
            string elementName = Path.GetFileNameWithoutExtension(job.filePath);
            string category = job.element.categoryText?.text ?? "Unknown";
            
            // Quick existence check first
            if (!File.Exists(job.filePath))
            {
                // Try async path resolution if direct path fails
                yield return staticCoroutineRunner.StartCoroutine(ResolveThumbnailPathAsync(elementName, category, (resolvedPath) =>
                {
                    verifiedPath = resolvedPath;
                    pathVerified = true;
                }));
                
                if (string.IsNullOrEmpty(verifiedPath))
                {
                    Debug.LogWarning($"[OPTIMIZED_THUMB] 📸 Could not find thumbnail for {elementName} in {category}");
                    currentConcurrentLoads--;
                    yield break;
                }
            }
            else
            {
                pathVerified = true;
            }

            // 🚀 ASYNC FILE LOADING: Load file without blocking
            var loadFileCoroutine = LoadFileAsync(verifiedPath);
            yield return staticCoroutineRunner.StartCoroutine(loadFileCoroutine);
            
            if (loadFileCoroutine.Current == null)
            {
                currentConcurrentLoads--;
                yield break;
            }

            byte[] fileData = (byte[])loadFileCoroutine.Current;
            yield return null; // Frame break after file load

            // 🚀 TEXTURE CREATION: Create texture with error handling
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(fileData))
            {
                DestroyImmediate(texture);
                currentConcurrentLoads--;
                yield break;
            }

            yield return null; // Frame break after image load

            // 🚀 RESIZE IF NEEDED: Optimize memory usage
            if (texture.width > MAX_THUMBNAIL_SIZE || texture.height > MAX_THUMBNAIL_SIZE)
            {
                var resizedTexture = ResizeTexture(texture, MAX_THUMBNAIL_SIZE);
                DestroyImmediate(texture);
                texture = resizedTexture;
                yield return null; // Frame break after resize
            }

            // 🚀 APPLY SETTINGS: Optimize texture settings
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();

            // 🚀 CACHE TEXTURE: Store in cache for reuse (use verified path as key)
            textureCache[verifiedPath] = texture;
            
            // Also cache with original path if different
            if (verifiedPath != job.filePath)
            {
                textureCache[job.filePath] = texture;
            }

            // 🚀 APPLY TO UI: Set the thumbnail
            job.element.ApplyThumbnail(texture);

            currentConcurrentLoads--;
            yield return null; // Final frame break
        }

        /// <summary>
        /// 🚀 ASYNC FILE LOADER: Load file data without blocking main thread
        /// </summary>
        private static IEnumerator LoadFileAsync(string filePath)
        {
            byte[] fileData = null;
            bool isLoading = true;
            
            // Start file loading on background thread
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    fileData = File.ReadAllBytes(filePath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[OPTIMIZED_THUMB] Error loading file {filePath}: {ex.Message}");
                }
                finally
                {
                    isLoading = false;
                }
            });
            
            // Wait for file loading to complete
            while (isLoading)
            {
                yield return null;
            }
            
            yield return fileData;
        }

        /// <summary>
        /// 🚀 TEXTURE RESIZER: Efficient texture resizing
        /// </summary>
        private static Texture2D ResizeTexture(Texture2D original, int maxSize)
        {
            // Calculate new dimensions
            float aspectRatio = (float)original.width / original.height;
            int newWidth, newHeight;
            
            if (aspectRatio > 1.0f)
            {
                newWidth = maxSize;
                newHeight = Mathf.RoundToInt(maxSize / aspectRatio);
            }
            else
            {
                newWidth = Mathf.RoundToInt(maxSize * aspectRatio);
                newHeight = maxSize;
            }
            
            // Create resized texture using RenderTexture for quality
            var resized = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            
            Graphics.Blit(original, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resized.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            
            return resized;
        }

        /// <summary>
        /// 🚀 APPLY THUMBNAIL: Set thumbnail sprite efficiently
        /// </summary>
        private void ApplyThumbnail(Texture2D texture)
        {
            if (texture == null || thumbnailImage == null) return;
            
            // Clean up old sprite
            if (thumbnailImage.sprite != null)
            {
                DestroyImmediate(thumbnailImage.sprite);
            }
            
            // Create and apply new sprite
            thumbnailImage.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
        }

        /// <summary>
        /// 🚀 INITIALIZE OPTIMIZED LOADING: Start the optimized system
        /// </summary>
        public static void InitializeSmartLoading(HKCarouselLayoutGroup3D<HKCarouselElementData> carousel)
        {
            if (carousel == null) return;

            staticCoroutineRunner = carousel;
            
            // Subscribe to carousel changes
            carousel.OnValueChanged.RemoveAllListeners();
            carousel.OnValueChanged.AddListener(OnCarouselSelectionChanged);

            // 🚀 START PROGRESSIVE LOADING: Load from center outward
            int currentIndex = carousel.GetTrueSelectedIndex();
            int totalElements = allElements.Count;
            
            if (totalElements > 0)
            {
                staticCoroutineRunner.StartCoroutine(ProgressiveLoadAllThumbnails(currentIndex, totalElements));
            }
        }

        /// <summary>
        /// 🚀 PROGRESSIVE LOADING: Load thumbnails efficiently from center outward
        /// </summary>
        private static IEnumerator ProgressiveLoadAllThumbnails(int centerIndex, int totalElements)
        {
            var loadedIndices = new HashSet<int>();
            
            // 🚀 PRIORITY 1: Load center immediately
            yield return staticCoroutineRunner.StartCoroutine(LoadSingleThumbnailImmediate(centerIndex, loadedIndices));
            
            // 🚀 PRIORITY 2: Load immediate neighbors
            yield return new WaitForSeconds(0.1f);
            yield return staticCoroutineRunner.StartCoroutine(LoadSingleThumbnailImmediate(GetWrappedIndex(centerIndex - 1, totalElements), loadedIndices));
            yield return staticCoroutineRunner.StartCoroutine(LoadSingleThumbnailImmediate(GetWrappedIndex(centerIndex + 1, totalElements), loadedIndices));
            
            // 🚀 PRIORITY 3+: Load remaining thumbnails in background
            int maxRadius = Mathf.CeilToInt(totalElements / 2f);
            
            for (int radius = 2; radius <= maxRadius; radius++)
            {
                yield return new WaitForSeconds(0.2f);
                
                int leftIndex = GetWrappedIndex(centerIndex - radius, totalElements);
                int rightIndex = GetWrappedIndex(centerIndex + radius, totalElements);
                
                if (!loadedIndices.Contains(leftIndex))
                {
                    QueueThumbnailForBackground(leftIndex, radius + 5);
                }
                
                if (!loadedIndices.Contains(rightIndex))
                {
                    QueueThumbnailForBackground(rightIndex, radius + 5);
                }
                
                if (loadedIndices.Count >= totalElements) break;
            }
        }

        /// <summary>
        /// 🚀 IMMEDIATE LOADING: Load single thumbnail immediately
        /// </summary>
        private static IEnumerator LoadSingleThumbnailImmediate(int index, HashSet<int> loadedIndices)
        {
            if (loadedIndices.Contains(index)) yield break;
            
            if (allElements.TryGetValue(index, out CarouselElementDemo element))
            {
                if (!string.IsNullOrEmpty(element.thumbnailPath))
                {
                    var job = new ThumbnailLoadJob(element.thumbnailPath, element, 1, true);
                    yield return staticCoroutineRunner.StartCoroutine(ProcessThumbnailJob(job));
                    loadedThumbnails.Add(index);
                    loadedIndices.Add(index);
                }
            }
        }

        /// <summary>
        /// 🚀 BACKGROUND LOADING: Queue thumbnail for background loading
        /// </summary>
        private static void QueueThumbnailForBackground(int index, int priority)
        {
            if (allElements.TryGetValue(index, out CarouselElementDemo element))
            {
                if (!string.IsNullOrEmpty(element.thumbnailPath) && !loadedThumbnails.Contains(index))
                {
                    var job = new ThumbnailLoadJob(element.thumbnailPath, element, priority, false);
                    QueueThumbnailJob(job);
                    loadedThumbnails.Add(index);
                }
            }
        }

        private static int GetWrappedIndex(int index, int totalElements)
        {
            if (index < 0) return totalElements + index;
            if (index >= totalElements) return index - totalElements;
            return index;
        }

        private static void OnCarouselSelectionChanged(int newCenterIndex)
        {
            if (allElements.Count > 0)
            {
                UpdateVisibleThumbnails(newCenterIndex, allElements.Count);
            }
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
                
                // If we get here, no Description line found
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
        /// 🚀 OPTIMIZED CLEANUP: Clean up resources efficiently
        /// </summary>
        private void OnDestroy()
        {
            // Remove from collections
            if (allElements.ContainsKey(id))
            {
                allElements.Remove(id);
            }
            
            if (loadedThumbnails.Contains(id))
            {
                loadedThumbnails.Remove(id);
            }
            
            // Clean up sprite
            if (thumbnailImage != null && thumbnailImage.sprite != null)
            {
                DestroyImmediate(thumbnailImage.sprite);
            }
        }

        /// <summary>
        /// 🚀 CACHE CLEANUP: Clean up texture cache when needed
        /// </summary>
        public static void ClearTextureCache()
        {
            foreach (var texture in textureCache.Values)
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
            textureCache.Clear();
        }
    }
}
