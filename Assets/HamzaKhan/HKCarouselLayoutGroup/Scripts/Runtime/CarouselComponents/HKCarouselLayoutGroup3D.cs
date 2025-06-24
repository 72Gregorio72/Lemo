using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine.Video;
using System.Linq;
using System.Collections;
using TMPro;
#if UNITY_ANDROID
using UnityEngine.Networking;
#endif

namespace HKCarouselLayoutGroup
{
    [RequireComponent(typeof(RectTransform))]
    public class HKCarouselLayoutGroup3D<T> : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler where T : HKBaseCarouselData
    {
        [Header("Main Settings")]
        [SerializeField] private int _defaultSelectedIndex = 0;
        [SerializeField, Min(0)] private float _scroll = 2;
        [SerializeField] private float _spacing = 245;
        [SerializeField] private float _zOffset = 9.5f;
        [SerializeField] private float _yOffset = 0;
        [SerializeField] private float _rotationY = 10;
        [SerializeField] private float _scaleFactor = 0.9f;
        [SerializeField] private float _spacingModMulti = -0.008f;
        [SerializeField] private float _depthModMulti = 0.2f;
        private bool _poolCreated = false;


        [Header("Auto Snapping to nearest element")]
        [SerializeField] private bool _enableSnapping = true;
        [SerializeField] private float _snapSpeed = 15f;
        [SerializeField] private bool _smoothClamp = true;

        [Header("Re Usable Elements Pooling System")]
        [SerializeField] private RectTransform _content;
        [SerializeField] private GameObject _item;
        [SerializeField] private List<T> _carouselElements;
        [SerializeField] private int _fixedCount = 5;
        [SerializeField] private bool _useRecycling = true;
        [SerializeField] private int _edge = 2;

        [Header("Callbacks")]
        public UnityEvent<int> OnValueChanged;

        [Header("Progress Display")]
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private bool _debugLogProgress = true;

        private List<RectTransform> _items = new();
        private Vector2 _dragStartPos;
        private bool _isDragging = false;
        private int _lastActiveCount = -1;
        private float _lastScroll = -1;
        private bool _needsUpdate = true;

        public int CurrentSelectedIndex { get; private set; }
        private int PoolRelativeSelectedIndex;
        private int IndexOffset;
        private int _lastSelectedIndex;

        private bool _isSimulatingScroll = false;
        private float _targetScroll;
        private float _scrollLerpSpeed = 10f; // Tweak for desired smoothness


        private List<PoolElement> _poolElements;

        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        public class PoolElement
        {
            public RectTransform RectTransform;
            public int Index;
        }

        void OnEnable()
        {
            if (!_isInitialized)
            {
                StartCoroutine(InitializeCarousel());
            }
            else
            {
            RefreshItems();
            UpdateCarousel();
            }
        }

        void OnValidate()
        {
            if (_isInitialized)
        {
            RefreshItems();
            UpdateCarousel();
            }
        }

        private void Awake()
        {
            if (!_isInitialized)
            {
                StartCoroutine(InitializeCarousel());
            }
        }

        private IEnumerator InitializeCarousel()
        {
            if (_isInitialized)
            {
                yield break;
            }

            if (_progressText != null)
            {
                _progressText.gameObject.SetActive(true);
                _progressText.text = "Starting initialization...";
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, we need to use UnityWebRequest to access files in StreamingAssets
            yield return StartCoroutine(CopyAndroidStreamingAssets());
#else
            // On other platforms, we can copy directly
            yield return StartCoroutine(CopyStreamingAssets());
#endif

            // Now load the assets
            LoadAssetsFromPersistentDataPath();

            // Create the pool after loading assets
            CreatePool();

            if (_progressText != null)
            {
                _progressText.text = "Initialization complete!";
                StartCoroutine(HideProgressAfterDelay(2f));
            }

            _isInitialized = true;
            
            // Initial refresh and update
            RefreshItems();
            UpdateCarousel();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private IEnumerator CopyAndroidStreamingAssets()
        {
            // All folders use the same category structure and manifest system
            string[] allFolders = { "Videos", "360 Images", "Thumbnails", "Audio", "Data" };
            string[] categories = { "Nature", "Space", "Relaxation" };
            
            UpdateProgress("Preparing to copy files...", 0);
            yield return null;

            int totalOperations = allFolders.Length * categories.Length;
            int currentOperation = 0;

            // Handle all folders with categories using manifests
            foreach (var folder in allFolders)
            {
                string persistentFolder = Path.Combine(Application.persistentDataPath, folder);
                Directory.CreateDirectory(persistentFolder);

                foreach (var category in categories)
                {
                    string categoryFolder = Path.Combine(persistentFolder, category);
                    Directory.CreateDirectory(categoryFolder);

                    string manifestPath = Path.Combine(Application.streamingAssetsPath, folder, category, "files.txt");
                    using (UnityWebRequest listRequest = UnityWebRequest.Get(manifestPath))
                    {
                        yield return listRequest.SendWebRequest();

                        if (listRequest.result == UnityWebRequest.Result.Success)
                        {
                            string[] files = listRequest.downloadHandler.text.Split('\n');
                            List<string> filesToCopy = new List<string>();

                            foreach (string file in files)
                            {
                                string trimmedFile = file.Trim();
                                if (string.IsNullOrEmpty(trimmedFile)) continue;

                                string targetPath = Path.Combine(categoryFolder, trimmedFile);
                                if (!File.Exists(targetPath))
                                {
                                    filesToCopy.Add(trimmedFile);
                                }
                            }

                            yield return StartCoroutine(CopyFilesInParallel(folder, category, filesToCopy));
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to read manifest for {folder}/{category}: {listRequest.error}");
                        }
                    }

                    currentOperation++;
                    UpdateProgress($"Processed {folder}/{category}", (float)currentOperation / totalOperations);
                    yield return null;
                }
            }
        }

        private IEnumerator CopyFilesInParallel(string folder, string category, List<string> filesToCopy)
        {
            List<UnityWebRequest> activeRequests = new List<UnityWebRequest>();
            int maxConcurrentRequests = 3;

            for (int i = 0; i < filesToCopy.Count; i++)
            {
                string file = filesToCopy[i];
                string sourceUrl = Path.Combine(Application.streamingAssetsPath, folder, category, file);
                string targetPath = Path.Combine(Application.persistentDataPath, folder, category, file);

                UnityWebRequest www = UnityWebRequest.Get(sourceUrl);
                activeRequests.Add(www);
                www.SendWebRequest();

                if (activeRequests.Count >= maxConcurrentRequests || i == filesToCopy.Count - 1)
                {
                    bool allRequestsDone;
                    do
                    {
                        allRequestsDone = true;
                        foreach (var request in activeRequests)
                        {
                            if (!request.isDone)
                            {
                                allRequestsDone = false;
                                break;
                            }
                        }
                        yield return null;
                    } while (!allRequestsDone);

                    for (int j = 0; j < activeRequests.Count; j++)
                    {
                        var request = activeRequests[j];
                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            string fileName = filesToCopy[i - (activeRequests.Count - 1) + j];
                            string filePath = Path.Combine(Application.persistentDataPath, folder, category, fileName);
                            File.WriteAllBytes(filePath, request.downloadHandler.data);
                            LogManager.Instance?.LogCustomMessage($"Copied {fileName} to {filePath}", "AssetCopy");
                        }
                        request.Dispose();
                    }

                    activeRequests.Clear();
                }

                UpdateProgress($"Copying {folder}/{category}", (float)(i + 1) / filesToCopy.Count);
                yield return null;
            }
        }
#else
        private IEnumerator CopyStreamingAssets()
        {
            // Media folders that use categories
            string[] mediaFolders = { "Videos", "360 Images", "Thumbnails" };
            // Support folders that don't use categories
            string[] supportFolders = { "Audio", "Data" };
            
            UpdateProgress("Preparing to copy files...", 0);
            yield return null;

            // First, scan all files that need to be copied
            Dictionary<string, string> filesToCopy = new Dictionary<string, string>();
            int totalFiles = 0;

            // Handle media folders with categories
            foreach (var folder in mediaFolders)
            {
                string srcPath = Path.Combine(Application.streamingAssetsPath, folder);
                string dstPath = Path.Combine(Application.persistentDataPath, folder);

                if (!Directory.Exists(srcPath)) continue;

                Directory.CreateDirectory(dstPath);

                string[] files = Directory.GetFiles(srcPath, "*.*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string relativePath = file.Substring(srcPath.Length + 1);
                    string targetPath = Path.Combine(dstPath, relativePath);
                    string targetDir = Path.GetDirectoryName(targetPath);

                    if (!File.Exists(targetPath))
                    {
                        filesToCopy[file] = targetPath;
                        totalFiles++;
                    }
                }
            }

            // Handle support folders
            foreach (var folder in supportFolders)
            {
                string srcPath = Path.Combine(Application.streamingAssetsPath, folder);
                string dstPath = Path.Combine(Application.persistentDataPath, folder);

                if (!Directory.Exists(srcPath)) continue;

                Directory.CreateDirectory(dstPath);

                // Copy all .txt and .wav files directly
                foreach (string file in Directory.GetFiles(srcPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".txt" || ext == ".wav")
                    {
                        string fileName = Path.GetFileName(file);
                        string targetPath = Path.Combine(dstPath, fileName);

                        if (!File.Exists(targetPath))
                        {
                            filesToCopy[file] = targetPath;
                            totalFiles++;
                        }
                    }
                }
            }

            if (totalFiles == 0)
            {
                yield break;
            }

            // Now copy files in batches
            int currentFile = 0;
            int batchSize = 10;
            List<string> currentBatch = new List<string>();

            foreach (var kvp in filesToCopy)
            {
                currentBatch.Add(kvp.Key);
                
                if (currentBatch.Count >= batchSize || currentFile == totalFiles - 1)
                {
                    foreach (string file in currentBatch)
                    {
                        string targetPath = filesToCopy[file];
                        string targetDir = Path.GetDirectoryName(targetPath);
                        
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(file, targetPath, true);
                        currentFile++;
                        
                        UpdateProgress($"Copying files", (float)currentFile / totalFiles);
                    }
                    
                    currentBatch.Clear();
                    yield return null;
                }
            }
        }
#endif

        private void UpdateProgress(string status, float progress)
        {
            if (_progressText != null)
            {
                _progressText.text = $"{status}\n{(progress * 100):F0}%";
            }
            
            if (_debugLogProgress)
            {
                Debug.Log($"[Carousel] {status} - {(progress * 100):F0}%");
            }
        }

        private IEnumerator HideProgressAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_progressText != null)
            {
                _progressText.gameObject.SetActive(false);
            }
        }

        private void CreatePool()
        {
            if (_poolCreated) return; // ⛔ Already created

            _poolCreated = true; // ✅ Set flag

            if (_poolElements == null)
                _poolElements = new List<PoolElement>();
            _poolElements.Clear();

            _items.Clear();

            _defaultSelectedIndex = Mathf.Clamp(_defaultSelectedIndex, 0, _carouselElements.Count - 1);

            IndexOffset = 0;
            _scroll = _defaultSelectedIndex;

            int count = _carouselElements.Count;

            if (count == 0)
            {
                Debug.LogWarning("[CreatePool] No carousel elements found.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                RectTransform rectTransform = Instantiate(_item).GetComponent<RectTransform>();

                PoolElement poolElement = new PoolElement
                {
                    RectTransform = rectTransform,
                    Index = i
                };

                _poolElements.Add(poolElement);
                _items.Add(rectTransform);

                rectTransform.SetParent(_content, false);

                var demo = rectTransform.GetComponent<CarouselElementDemo>();
                if (demo != null)
                {
                    SetCarouselElement((ICarouselElement<T>)demo, i);
                    Debug.Log($"[CreatePool] SetCarouselElement called on index {i}");
                }
                else
                {
                    Debug.LogWarning("[CreatePool] CarouselElementDemo not found on prefab.");
                }
            }

            _lastActiveCount = _items.Count;
            _lastSelectedIndex = -1;
            _needsUpdate = true;
        }

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RefreshItemsIfChanged();
                _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, _items.Count - 1));

                if (Mathf.Abs(_scroll - _lastScroll) > 0.001f || _needsUpdate)
                {
                    UpdateCarousel();
                    _lastScroll = _scroll;
                    _needsUpdate = false;
                }

                return;
            }
#endif

            HandleScroll();

            if (_useRecycling)
            {
                HandleRecycling();
            }

            if (_isSimulatingScroll)
            {
                _scroll = Mathf.Lerp(_scroll, _targetScroll, Time.deltaTime * _scrollLerpSpeed);
                _needsUpdate = true;

                if (Mathf.Abs(_scroll - _targetScroll) < 0.001f)
                {
                    _scroll = _targetScroll;
                    _isSimulatingScroll = false;
                    OnEndSimulatedDrag(); // Custom method to match OnEndDrag
                }
            }

        }

        private void HandleScroll()
        {
            if (!_isDragging && _enableSnapping)
            {
                float target = Mathf.Round(_scroll);
                if (Mathf.Abs(target - _scroll) > 0.001f)
                {
                    _scroll = Mathf.Lerp(_scroll, target, Time.deltaTime * _snapSpeed);
                    _needsUpdate = true;
                }
            }

            if (_smoothClamp)
            {
                _scroll = Mathf.Lerp(_scroll, Mathf.Clamp(_scroll, 0, Mathf.Max(0, _items.Count - 1)), Time.deltaTime * _snapSpeed);
            }
            else
            {
                _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, _items.Count - 1));
            }

            RefreshItemsIfChanged();

            if (Mathf.Abs(_scroll - _lastScroll) > 0.001f || _needsUpdate)
            {
                UpdateCarousel();
                _lastScroll = _scroll;
                _needsUpdate = false;
            }
        }

        private void HandleRecycling()
        {
            int totalItems = _items.Count;
            int maxIndex = _carouselElements.Count - 1;
            int predictedTrueIndex = Mathf.RoundToInt(_scroll) + IndexOffset;

            if (PoolRelativeSelectedIndex >= totalItems - _edge && predictedTrueIndex < maxIndex)
            {
                if (CurrentSelectedIndex + _edge >= _carouselElements.Count)
                {
                    return;
                }

                RectTransform first = _items[0];
                _items.RemoveAt(0);
                _items.Add(first);

                int newDataIndex = _items[_items.Count - 2].GetComponent<ICarouselElement<T>>().GetID() + 1;

                if (newDataIndex <= maxIndex)
                {
                    SetCarouselElement(first.GetComponent<ICarouselElement<T>>(), newDataIndex);
                    _scroll -= 1;
                    IndexOffset += 1;
                    _needsUpdate = true;
                }
            }
            else if (PoolRelativeSelectedIndex <= _edge && predictedTrueIndex > 0)
            {
                if (CurrentSelectedIndex - _edge <= 0)
                {
                    return;
                }

                RectTransform last = _items[totalItems - 1];
                _items.RemoveAt(totalItems - 1);
                _items.Insert(0, last);

                int newDataIndex = _items[1].GetComponent<ICarouselElement<T>>().GetID() - 1;

                if (newDataIndex >= 0)
                {
                    SetCarouselElement(last.GetComponent<ICarouselElement<T>>(), newDataIndex);
                    _scroll += 1;
                    IndexOffset -= 1;
                    _needsUpdate = true;
                }
            }
        }

        private void RefreshItems()
        {
            _items.Clear();
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeInHierarchy)
                    continue;

                if (child is RectTransform rt)
                {
                    var canvas = rt.GetComponent<Canvas>();
                    if (!canvas)
                        canvas = rt.gameObject.AddComponent<Canvas>();

                    canvas.overrideSorting = true;

                    if (!rt.GetComponent<GraphicRaycaster>())
                        rt.gameObject.AddComponent<GraphicRaycaster>();

                    _items.Add(rt);
                }
            }

            _lastActiveCount = _items.Count;
            _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, _items.Count - 1));
            _needsUpdate = true;
        }

        private void RefreshItemsIfChanged()
        {
            int currentCount = 0;
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeInHierarchy)
                    currentCount++;
            }

            if (currentCount != _lastActiveCount)
            {
                RefreshItems();
            }
        }

        private void UpdateCarousel()
        {
            if (_items.Count == 0) return;

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null) continue;

                float offset = i - _scroll;
                float rotY = -offset * _rotationY;

                float spacingMod = Mathf.Abs(rotY) * _spacingModMulti + 1;
                float depthMod = Mathf.Abs(rotY) * _depthModMulti + 1;

                float x = offset * _spacing * spacingMod;
                float z = Mathf.Abs(offset) * _zOffset * depthMod;
                float y = Mathf.Abs(offset) * _yOffset;

                float scale = Mathf.Pow(_scaleFactor, Mathf.Abs(offset));

                var item = _items[i];
                item.localPosition = new Vector3(x, y, z);
                item.localRotation = Quaternion.Euler(0, rotY, 0);
                item.localScale = Vector3.one * scale;

                float distance = Mathf.Abs(offset);
                float alpha = Mathf.Clamp01(1f - (distance * 0.7f)); // More distance = lower alpha

                var demo = item.GetComponent<CarouselElementDemo>();
                if (demo != null)
                {
                    demo.SetCategoryAlpha(alpha);
                }


            }

            List<(RectTransform item, float absOffset)> sorted = new();

            for (int i = 0; i < _items.Count; i++)
            {
                sorted.Add((_items[i], Mathf.Abs(i - _scroll)));
            }

            sorted.Sort((a, b) => b.absOffset.CompareTo(a.absOffset));

            for (int i = 0; i < sorted.Count; i++)
            {
                var canvas = sorted[i].item.GetComponent<Canvas>();
                if (canvas)
                    canvas.sortingOrder = i;
            }

            PoolRelativeSelectedIndex = Mathf.Clamp(Mathf.RoundToInt(_scroll), 0, _fixedCount - 1);
            CurrentSelectedIndex = PoolRelativeSelectedIndex + IndexOffset;

            if (_lastSelectedIndex != CurrentSelectedIndex)
            {
                OnValueChanged.Invoke(CurrentSelectedIndex);
                _lastSelectedIndex = CurrentSelectedIndex;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _dragStartPos = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            float dragDelta = (eventData.position.x - _dragStartPos.x) / _spacing;
            _scroll -= dragDelta;
            _dragStartPos = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }

        public void SetCarouselElement(ICarouselElement<T> cElement, int index)
        {
            cElement.ConfigureElement(_carouselElements[index], index);
        }

        public T GetElementDataFromIndex(int index)
        {
            return _carouselElements[index];
        }

        public int GetTrueSelectedIndex()
        {
            return CurrentSelectedIndex;
        }


        public void SimulateScroll(float direction)
        {
            if (direction == 0 || _items.Count == 0 || _isSimulatingScroll)
                return;

            int nextIndex = Mathf.Clamp(Mathf.RoundToInt(_scroll - direction), 0, Mathf.Max(0, _items.Count - 1));

            _targetScroll = nextIndex;
            _isSimulatingScroll = true;

            OnBeginSimulatedDrag(); // Custom method to match OnBeginDrag behavior
        }

        private void OnBeginSimulatedDrag()
        {
            _isDragging = true;
            // You can set _dragStartPos if needed, but not required here
        }

        private void OnEndSimulatedDrag()
        {
            _isDragging = false;
        }

        private string GetPlayableVideoPath(string originalPath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, we need to use the copied file in persistentDataPath
            string fileName = Path.GetFileName(originalPath);
            string category = Path.GetFileName(Path.GetDirectoryName(originalPath));
            string persistentPath = Path.Combine(Application.persistentDataPath, "Videos", category, fileName);
            
            // Ensure the file exists in persistentDataPath
            if (!File.Exists(persistentPath))
            {
                Debug.LogError($"[GetPlayableVideoPath] Video file not found in persistentDataPath: {persistentPath}");
                return string.Empty;
            }

            // Use file:// protocol for local files on Android
            return "file://" + persistentPath;
#else
            // On other platforms, we can use the original path
            return originalPath;
#endif
        }

        private string GetPlayable360ImagePath(string originalPath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, we need to use the copied file in persistentDataPath
            string fileName = Path.GetFileName(originalPath);
            string category = Path.GetFileName(Path.GetDirectoryName(originalPath));
            string persistentPath = Path.Combine(Application.persistentDataPath, "360 Images", category, fileName);
            
            // Ensure the file exists in persistentDataPath
            if (!File.Exists(persistentPath))
            {
                Debug.LogError($"[GetPlayable360ImagePath] 360° image file not found in persistentDataPath: {persistentPath}");
                return string.Empty;
            }

            // Use file:// protocol for local files on Android
            return "file://" + persistentPath;
#else
            // On other platforms, we can use the original path
            return originalPath;
#endif
        }

        void LoadAssetsFromPersistentDataPath()
        {
            var loadedElements = new List<HKCarouselElementData>();

            Debug.Log("[CAROUSEL] Starting to load assets...");

            // Add Home card at the beginning
            loadedElements.Add(new HKCarouselElementData
            {
                Name = "Home",
                Category = "Navigation",
                IsSceneLink = true,
                SceneIndex = 1,
                ThumbnailPath = "Thumbnails/home"
            });

            Debug.Log("[CAROUSEL] Added Home card");

            // Videos
            string videosRoot = Path.Combine(Application.persistentDataPath, "Videos");
            if (Directory.Exists(videosRoot))
            {
                foreach (var categoryDir in Directory.GetDirectories(videosRoot))
                {
                    string category = Path.GetFileName(categoryDir);
                    foreach (var videoFile in Directory.GetFiles(categoryDir, "*.mp4"))
                    {
                        string name = Path.GetFileNameWithoutExtension(videoFile);
                        Debug.Log($"[LoadAssetsFromPersistentDataPath] Found video: {name} in category: {category}");

                        // Check for thumbnail in both PersistentDataPath and StreamingAssets
                        string thumbnailPath = Path.Combine(Application.persistentDataPath, "Thumbnails", category, name + ".png");
                        if (!File.Exists(thumbnailPath))
                        {
                            thumbnailPath = Path.Combine(Application.streamingAssetsPath, "Thumbnails", category, name + ".png");
                        }

                        // Get the playable path for the video
                        string playablePath = GetPlayableVideoPath(videoFile);
                        if (string.IsNullOrEmpty(playablePath))
                        {
                            Debug.LogError($"[LoadAssetsFromPersistentDataPath] Failed to get playable path for video: {videoFile}");
                            continue;
                        }

                        loadedElements.Add(new HKCarouselElementData
                        {
                            Name = name,
                            Category = category,
                            VideoPath = playablePath,
                            ThumbnailPath = File.Exists(thumbnailPath) ? thumbnailPath : null,
                            IsImage360 = false
                        });
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
                    foreach (var imageFile in Directory.GetFiles(categoryDir, "*.png"))
                    {
                        string name = Path.GetFileNameWithoutExtension(imageFile);
                        Debug.Log($"[LoadAssetsFromPersistentDataPath] Found 360° image: {name} in category: {category}");

                        // Check for thumbnail in both PersistentDataPath and StreamingAssets
                        string thumbnailPath = Path.Combine(Application.persistentDataPath, "Thumbnails", category, name + ".png");
                        if (!File.Exists(thumbnailPath))
                        {
                            thumbnailPath = Path.Combine(Application.streamingAssetsPath, "Thumbnails", category, name + ".png");
                        }

                        // Get the playable path for the 360° image
                        string playablePath = GetPlayable360ImagePath(imageFile);
                        if (string.IsNullOrEmpty(playablePath))
                        {
                            Debug.LogError($"[LoadAssetsFromPersistentDataPath] Failed to get playable path for 360° image: {imageFile}");
                            continue;
                        }

                        loadedElements.Add(new HKCarouselElementData
                        {
                            Name = name,
                            Category = category,
                            VideoPath = playablePath,
                            ThumbnailPath = File.Exists(thumbnailPath) ? thumbnailPath : null,
                            IsImage360 = true
                        });
                    }
                }
            }

            // Sort elements by Category, then Name
            loadedElements.Sort((a, b) =>
            {
                // Always keep Home first
                if (a.IsSceneLink) return -1;
                if (b.IsSceneLink) return 1;

                int catCompare = string.Compare(a.Category, b.Category, System.StringComparison.OrdinalIgnoreCase);
                if (catCompare != 0) return catCompare;
                return string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
            });

            Debug.Log($"[CAROUSEL] Total elements loaded: {loadedElements.Count}");

            // Assign to carousel
            var fi = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>)
                .GetField("_carouselElements", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fi != null)
            {
                fi.SetValue(this, loadedElements);

                // Set default selected index to middle
                if (loadedElements.Count > 0)
                {
                    var defaultIndexField = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>)
                        .GetField("_defaultSelectedIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (defaultIndexField != null)
                    {
                        int middleIndex = Mathf.FloorToInt(loadedElements.Count / 2);
                        defaultIndexField.SetValue(this, middleIndex);
                    }
                }
            }

            // Refresh carousel UI
            RefreshItems();
            UpdateCarousel();
        }
    }



    public interface ICarouselElement<T> where T : HKBaseCarouselData
    {
        void ConfigureElement(T data, int index);
        int GetID();
    }

    [System.Serializable]
    public class HKBaseCarouselData { }


    public class FolderVideoDataToCarousel : MonoBehaviour
    {
        public HKCarouselLayoutGroup3DDemo carousel;

        public string VideoRoot
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Application.streamingAssetsPath, "Videos");
#else
        return Path.Combine(Application.persistentDataPath, "Videos");
#endif
            }
        }

        public string ThumbnailRoot
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Application.streamingAssetsPath, "Thumbnails");
#else
        return Path.Combine(Application.persistentDataPath, "Thumbnails");
#endif
            }
        }

        void Start()

        {
            var elements = LoadCarouselElementsFromNatureFolder();
            SetCarouselData(elements);
        }



        List<HKCarouselElementData> LoadCarouselElementsFromNatureFolder()
        {
            var list = new List<HKCarouselElementData>();

            string natureFolder = Path.Combine(VideoRoot, "Nature");

            if (!Directory.Exists(natureFolder))
                return list;

            foreach (var videoPath in Directory.GetFiles(natureFolder, "*.mp4"))
            {
                string videoName = Path.GetFileNameWithoutExtension(videoPath);
                string relativeVideoPath = $"Videos/Nature/{videoName}.mp4";
                string relativeThumbnailPath = $"Thumbnails/Nature/{videoName}.png";

                list.Add(new HKCarouselElementData
                {
                    Name = videoName,
                    VideoPath = relativeVideoPath,
                    ThumbnailPath = File.Exists(Path.Combine(ThumbnailRoot, "Nature", videoName + ".png"))
                        ? relativeThumbnailPath
                        : null
                });
            }

            return list;
        }


        void SetCarouselData(List<HKCarouselElementData> data)
        {
            var field = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>)
                .GetField("_carouselElements", BindingFlags.NonPublic | BindingFlags.Instance);

            field?.SetValue(carousel, data);

            var refresh = carousel.GetType().GetMethod("RefreshItems", BindingFlags.NonPublic | BindingFlags.Instance);
            refresh?.Invoke(carousel, null);
        }


    }
}

public static class AssetCopyUtility
{
    public static IEnumerator CopyStreamingAssetsToPersistent(string relativePath)
    {
        string sourcePath = Path.Combine(Application.streamingAssetsPath, relativePath);
        string destPath = Path.Combine(Application.persistentDataPath, relativePath);

        if (File.Exists(destPath))
            yield break; // Already copied

        string destDir = Path.GetDirectoryName(destPath);
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

#if UNITY_ANDROID && !UNITY_EDITOR
        using (UnityWebRequest www = UnityWebRequest.Get(sourcePath))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(destPath, www.downloadHandler.data);
            }
            else
            {
                Debug.LogError("Failed to copy " + sourcePath + " to " + destPath);
            }
        }
#else
        File.Copy(sourcePath, destPath, true);
        yield return null;
#endif
    }

    public static IEnumerator CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(destDir, fileName);
#if UNITY_ANDROID && !UNITY_EDITOR
            string androidPath = Path.Combine(sourceDir, fileName);
            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(androidPath))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(destFile, www.downloadHandler.data);
                }
                else
                {
                    Debug.LogError("Failed to copy " + androidPath + " to " + destFile);
                }
            }
#else
            File.Copy(file, destFile, true);
            yield return null;
#endif
        }

        // Copy all subdirectories recursively
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(dir);
            yield return CopyDirectory(dir, Path.Combine(destDir, dirName));
        }
    }
}