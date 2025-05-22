using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.XR;

namespace HKCarouselLayoutGroup
{
    [RequireComponent(typeof(RectTransform))]
    public class HKSceneCarouselLayoutGroup3D : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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

        [Header("Scene Data")]
        [SerializeField] private List<HKSceneCarouselData> sceneElements = new List<HKSceneCarouselData>();

        [Header("Auto Snapping")]
        [SerializeField] private bool _enableSnapping = true;
        [SerializeField] private float _snapSpeed = 15f;
        [SerializeField] private bool _smoothClamp = true;

        [Header("UI Elements")]
        [SerializeField] private RectTransform _content;
        [SerializeField] private GameObject _itemPrefab;
        
        [Header("XR Input")]
        [SerializeField] private bool isLeftController = false;
        [SerializeField] private float inputThreshold = 0.5f;
        [SerializeField] private float scrollCooldown = 0.25f;
        [SerializeField] private float scrollLerpSpeed = 10f;
        
        [Header("Callbacks")]
        public UnityEvent<int> OnValueChanged;

        private List<RectTransform> _items = new List<RectTransform>();
        private Vector2 _dragStartPos;
        private bool _isDragging = false;
        private float _lastScroll = -1;
        private bool _needsUpdate = true;
        private int _lastSelectedIndex = -1;

        private InputDevice targetDevice;
        private bool deviceInitialized = false;
        private float cooldownTimer = 0f;
        private bool isScrolling = false;
        private float targetScroll;

        public int CurrentSelectedIndex { get; private set; }

        private void Start()
        {
            CreateItems();
            UpdateCarousel();
        }

        private void CreateItems()
        {
            // Clear existing items
            foreach (Transform child in _content)
            {
                Destroy(child.gameObject);
            }
            _items.Clear();

            // Create new items
            for (int i = 0; i < sceneElements.Count; i++)
            {
                GameObject item = Instantiate(_itemPrefab, _content);
                RectTransform rt = item.GetComponent<RectTransform>();
                _items.Add(rt);

                var demo = item.GetComponent<SceneCarouselElementDemo>();
                if (demo != null)
                {
                    demo.ConfigureElement(sceneElements[i], i);
                }
            }

            _scroll = _defaultSelectedIndex;
        }

        private void InitializeDevice()
        {
            if (deviceInitialized) return;

            var characteristics = InputDeviceCharacteristics.Controller;
            characteristics |= isLeftController ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
            
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

            if (devices.Count > 0)
            {
                targetDevice = devices[0];
                deviceInitialized = true;
            }
        }

        private void Update()
        {
            if (!deviceInitialized)
            {
                InitializeDevice();
            }

            cooldownTimer -= Time.deltaTime;
            HandleThumbstickInput();

            // Handle smooth scrolling
            if (isScrolling)
            {
                _scroll = Mathf.Lerp(_scroll, targetScroll, Time.deltaTime * scrollLerpSpeed);
                _needsUpdate = true;

                // Check if we've reached (or very close to) the target
                if (Mathf.Abs(_scroll - targetScroll) < 0.01f)
                {
                    _scroll = targetScroll;
                    isScrolling = false;
                }
            }

            HandleScroll();
            
            if (Mathf.Abs(_scroll - _lastScroll) > 0.001f || _needsUpdate)
            {
                UpdateCarousel();
                _lastScroll = _scroll;
                _needsUpdate = false;
            }
        }

        private void HandleThumbstickInput()
        {
            if (!targetDevice.isValid) return;

            if (targetDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                if (cooldownTimer <= 0f && !isScrolling)
                {
                    if (axis.x > inputThreshold)
                    {
                        SimulateScroll(-1);
                        cooldownTimer = scrollCooldown;
                    }
                    else if (axis.x < -inputThreshold)
                    {
                        SimulateScroll(1);
                        cooldownTimer = scrollCooldown;
                    }
                }
            }
        }

        public void SimulateScroll(float direction)
        {
            if (direction == 0 || _items.Count == 0) return;

            int nextIndex = Mathf.Clamp(Mathf.RoundToInt(_scroll - direction), 0, Mathf.Max(0, _items.Count - 1));
            targetScroll = nextIndex;
            isScrolling = true;
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

                // Update alpha based on distance from center
                float distance = Mathf.Abs(offset);
                float alpha = Mathf.Clamp01(1f - (distance * 0.7f));
                var demo = item.GetComponent<SceneCarouselElementDemo>();
                if (demo != null)
                {
                    demo.SetCategoryAlpha(alpha);
                }
            }

            // Sort items by depth
            List<(RectTransform item, float absOffset)> sorted = new List<(RectTransform, float)>();
            for (int i = 0; i < _items.Count; i++)
            {
                sorted.Add((_items[i], Mathf.Abs(i - _scroll)));
            }
            sorted.Sort((a, b) => b.absOffset.CompareTo(a.absOffset));

            // Update sorting order
            for (int i = 0; i < sorted.Count; i++)
            {
                var canvas = sorted[i].item.GetComponent<Canvas>();
                if (canvas)
                    canvas.sortingOrder = i;
            }

            // Update selected index
            int newSelectedIndex = Mathf.RoundToInt(_scroll);
            if (_lastSelectedIndex != newSelectedIndex)
            {
                CurrentSelectedIndex = newSelectedIndex;
                OnValueChanged.Invoke(CurrentSelectedIndex);
                _lastSelectedIndex = newSelectedIndex;
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

        public HKSceneCarouselData GetElementDataFromIndex(int index)
        {
            if (index >= 0 && index < sceneElements.Count)
            {
                return sceneElements[index];
            }
            return null;
        }

        public int GetTrueSelectedIndex()
        {
            return CurrentSelectedIndex;
        }

        // Method to manually set scene data
        public void SetSceneElements(List<HKSceneCarouselData> newElements)
        {
            sceneElements = newElements;
            CreateItems();
            UpdateCarousel();
        }
    }
} 