using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;

namespace HKCarouselLayoutGroup
{
    public class CarouselElementDemo : MonoBehaviour, ICarouselElement<HKCarouselElementData>
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image thumbnailImage;

        [Header("Rating Display")]
        [SerializeField] private TMP_Text ratingAverageText;
        [SerializeField] private TMP_Text ratingCountText;

        private int id;

        public void ConfigureElement(HKCarouselElementData data, int index)
        {
            id = index;

            if (nameText != null) nameText.text = data.Name;
            if (categoryText != null) categoryText.text = data.Category ?? "UNKNOWN";

            if (thumbnailImage != null && !string.IsNullOrEmpty(data.ThumbnailPath))
            {
                // First try loading as Texture2D
                Texture2D thumbnailTexture = Resources.Load<Texture2D>(data.ThumbnailPath);
                if (thumbnailTexture != null)
                {
                    // Convert Texture2D to Sprite
                    Sprite thumbnailSprite = Sprite.Create(thumbnailTexture, 
                        new Rect(0, 0, thumbnailTexture.width, thumbnailTexture.height), 
                        new Vector2(0.5f, 0.5f));
                    thumbnailImage.sprite = thumbnailSprite;
                }
                else
                {
                    // Fallback: try loading as Sprite directly
                    Sprite thumbnailSprite = Resources.Load<Sprite>(data.ThumbnailPath);
                    if (thumbnailSprite != null)
                    {
                        thumbnailImage.sprite = thumbnailSprite;
                    }
                    else
                    {
                        Debug.LogWarning($"[CarouselElementDemo] Failed to load thumbnail from path: {data.ThumbnailPath}");
                    }
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

            // Get reference to the XRCarouselInputController
            var carouselController = FindObjectOfType<XRCarouselInputController>();
            if (carouselController == null)
            {
                Debug.LogError("[CarouselElementDemo] Could not find XRCarouselInputController!");
                SetRatingDisplay(0f, 0);
                return;
            }

            // Load rating data using the controller's method
            var ratingData = carouselController.LoadRatingData();
            
            if (ratingData.TryGetValue(key, out XRCarouselInputController.RatingData data))
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

        [System.Serializable]
        public class RatingData
        {
            public float average;
            public int count;
        }

        public static class JsonUtilityWrapper
        {
            [System.Serializable]
            private class Wrapper<T>
            {
                public List<string> keys = new();
                public List<T> values = new();
            }

            public static Dictionary<string, T> FromJson<T>(string json)
            {
                var wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
                Dictionary<string, T> dict = new();
                for (int i = 0; i < wrapper.keys.Count; i++)
                    dict[wrapper.keys[i]] = wrapper.values[i];
                return dict;
            }
        }
    }
}
