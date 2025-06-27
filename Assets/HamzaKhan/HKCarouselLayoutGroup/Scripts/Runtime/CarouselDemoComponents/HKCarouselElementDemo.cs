using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using System;

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
        [SerializeField] private float normalHeight = 200f;
        [SerializeField] private float selectedHeight = 300f;
        [SerializeField] private float transitionSpeed = 10f;

        [Header("Rating Display")]
        [SerializeField] private TMP_Text ratingAverageText;
        [SerializeField] private TMP_Text ratingCountText;

        private int id;

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

            if (nameText != null) nameText.text = data.Name;
            if (categoryText != null) categoryText.text = data.Category ?? "UNKNOWN";

            if (thumbnailImage != null && !string.IsNullOrEmpty(data.ThumbnailPath))
            {
                try
                {
                    // Load image file directly
                    byte[] fileData = File.ReadAllBytes(data.ThumbnailPath);
                    Texture2D thumbnailTexture = new Texture2D(2, 2);
                    if (thumbnailTexture.LoadImage(fileData))
                    {
                        // Convert Texture2D to Sprite
                        Sprite thumbnailSprite = Sprite.Create(thumbnailTexture, 
                            new Rect(0, 0, thumbnailTexture.width, thumbnailTexture.height), 
                            new Vector2(0.5f, 0.5f));
                        thumbnailImage.sprite = thumbnailSprite;
                    }
                    else
                    {
                        Debug.LogWarning($"[CarouselElementDemo] Failed to load thumbnail image data from path: {data.ThumbnailPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CarouselElementDemo] Error loading thumbnail from path: {data.ThumbnailPath}\nError: {ex.Message}");
                }
            }

            LoadRatingDataFromNameText();
            LoadDescriptionFromDataFile();
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
    }
}
