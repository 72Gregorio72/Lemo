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

        public class PoolElement
        {
            public RectTransform RectTransform;
            public int Index;
        }

        void OnEnable()
        {
            TryAutoLoadCarouselElements();
            RefreshItems();
            UpdateCarousel();
            CreatePool();
        }

        void OnValidate()
        {
            RefreshItems();
            UpdateCarousel();
        }

        private void Awake()
        {
            //CreatePool();
        }

        private void TryAutoLoadCarouselElements()
        {
            if (typeof(T) != typeof(HKCarouselElementData)) 
            {
                Debug.LogWarning($"[TryAutoLoadCarouselElements] Wrong type: {typeof(T)}, expected: {typeof(HKCarouselElementData)}");
                return;
            }

            var loadedElements = new List<HKCarouselElementData>();

            // Load all video files from Resources
            Debug.Log("[TryAutoLoadCarouselElements] Attempting to load from Resources folder...");
            
            var videoClips = Resources.LoadAll<VideoClip>("Videos");
            Debug.Log($"[TryAutoLoadCarouselElements] Found {videoClips.Length} video clips");
            foreach (var clip in videoClips)
            {
                Debug.Log($"[TryAutoLoadCarouselElements] Video clip found: {clip.name}");
            }
            
            var images360 = Resources.LoadAll<Texture2D>("360 Images");
            Debug.Log($"[TryAutoLoadCarouselElements] Found {images360.Length} 360° images");
            foreach (var img in images360)
            {
                Debug.Log($"[TryAutoLoadCarouselElements] 360 image found: {img.name}");
            }
            
            var thumbnails = Resources.LoadAll<Texture2D>("Thumbnails");
            Debug.Log($"[TryAutoLoadCarouselElements] Found {thumbnails.Length} thumbnails");
            foreach (var thumb in thumbnails)
            {
                Debug.Log($"[TryAutoLoadCarouselElements] Thumbnail found: {thumb.name}");
            }

            // Process video clips
            foreach (var video in videoClips)
            {
                string name = video.name;
                string category = GetCategoryFromResourcePath(video);
                Debug.Log($"[TryAutoLoadCarouselElements] Processing video: {name} in category: {category}");
                
                // Find matching thumbnail
                string thumbnailPath = null;
                var thumbnail = thumbnails.FirstOrDefault(t => t.name == name);
                if (thumbnail != null)
                {
                    // Remove .png extension if present since Resources.Load doesn't need it
                    thumbnailPath = $"Thumbnails/{category}/{name}".Replace(".png", "");
                }

                // Store the full path including category
                string fullVideoPath = $"Videos/{category}/{name}";
                
                loadedElements.Add(new HKCarouselElementData
                {
                    Name = name,
                    Category = category,
                    VideoPath = fullVideoPath,
                    ThumbnailPath = thumbnailPath,
                    IsImage360 = false
                });
            }

            // Process 360 images
            foreach (var image in images360)
            {
                string name = image.name;
                string category = GetCategoryFromResourcePath(image);
                Debug.Log($"[TryAutoLoadCarouselElements] Processing 360° image: {name} in category: {category}");
                
                // Find matching thumbnail
                string thumbnailPath = null;
                var thumbnail = thumbnails.FirstOrDefault(t => t.name == name);
                if (thumbnail != null)
                {
                    thumbnailPath = $"Thumbnails/{category}/{name}".Replace(".png", "");
                }

                // Store the full path including category
                string fullImagePath = $"360 Images/{category}/{name}";

                loadedElements.Add(new HKCarouselElementData
                {
                    Name = name,
                    Category = category,
                    VideoPath = fullImagePath,
                    ThumbnailPath = thumbnailPath,
                    IsImage360 = true
                });
            }

            Debug.Log($"[TryAutoLoadCarouselElements] Total elements loaded: {loadedElements.Count}");

            if (loadedElements.Count == 0)
            {
                Debug.LogError("[TryAutoLoadCarouselElements] No elements found in Resources folder. Please ensure you have the following structure:\n" +
                    "Assets/Resources/\n" +
                    "├── Videos/\n" +
                    "│   └── [CategoryName]/\n" +
                    "│       └── video files\n" +
                    "├── 360 Images/\n" +
                    "│   └── [CategoryName]/\n" +
                    "│       └── image files\n" +
                    "└── Thumbnails/\n" +
                    "    └── [CategoryName]/\n" +
                    "        └── thumbnail files");
                return;
            }

            // Group elements by Category, then Name
            loadedElements.Sort((a, b) =>
            {
                int catCompare = string.Compare(a.Category, b.Category, System.StringComparison.OrdinalIgnoreCase);
                if (catCompare != 0) return catCompare;

                return string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
            });

            // Assign the loaded list to the carousel
            FieldInfo fi = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>)
                .GetField("_carouselElements", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fi != null)
            {
                fi.SetValue(this, loadedElements);

                FieldInfo defaultIndexField = typeof(HKCarouselLayoutGroup3D<HKCarouselElementData>)
                    .GetField("_defaultSelectedIndex", BindingFlags.NonPublic | BindingFlags.Instance);

                if (defaultIndexField != null && loadedElements.Count > 0)
                {
                    int middleIndex = Mathf.FloorToInt(loadedElements.Count / 2);
                    defaultIndexField.SetValue(this, middleIndex);
                }
            }
        }

        private string GetCategoryFromResourcePath(UnityEngine.Object resource)
        {
            string resourceName = resource.name;
            Debug.Log($"[GetCategoryFromResourcePath] Processing resource: {resourceName}");

            // Try to extract category from the resource name
            string[] nameParts = resourceName.Split('_');
            if (nameParts.Length > 1)
            {
                string category = nameParts[0];
                Debug.Log($"[GetCategoryFromResourcePath] Found category from name: {category}");
                return category;
            }

            // If no category found in name, check if it's in a subfolder
            if (resource is VideoClip)
            {
                var allVideos = Resources.LoadAll<VideoClip>("Videos");
                foreach (var video in allVideos)
                {
                    if (video == resource)
                    {
                        // Get all subfolders in the Videos directory
                        string[] subfolders = Directory.GetDirectories(Path.Combine(Application.dataPath, "Resources", "Videos"));
                        foreach (var subfolder in subfolders)
                        {
                            string categoryName = Path.GetFileName(subfolder);
                            string fullPath = $"Videos/{categoryName}/{resourceName}";
                            if (Resources.Load<VideoClip>(fullPath) != null)
                            {
                                Debug.Log($"[GetCategoryFromResourcePath] Found category from path: {categoryName}");
                                return categoryName;
                            }
                        }
                    }
                }
            }
            else if (resource is Texture2D)
            {
                var allImages = Resources.LoadAll<Texture2D>("360 Images");
                foreach (var image in allImages)
                {
                    if (image == resource)
                    {
                        // Get all subfolders in the 360 Images directory
                        string[] subfolders = Directory.GetDirectories(Path.Combine(Application.dataPath, "Resources", "360 Images"));
                        foreach (var subfolder in subfolders)
                        {
                            string categoryName = Path.GetFileName(subfolder);
                            string fullPath = $"360 Images/{categoryName}/{resourceName}";
                            if (Resources.Load<Texture2D>(fullPath) != null)
                            {
                                Debug.Log($"[GetCategoryFromResourcePath] Found category from path: {categoryName}");
                                return categoryName;
                            }
                        }
                    }
                }
            }

            Debug.LogWarning($"[GetCategoryFromResourcePath] Could not determine category for resource: {resourceName}");
            return "Unknown";
        }

        private string GetFullResourcePath(UnityEngine.Object resource)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetAssetPath(resource);
#else
            return $"Path in build: Resources/{resource.name}";
#endif
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