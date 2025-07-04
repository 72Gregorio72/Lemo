using UnityEngine;

namespace HKCarouselLayoutGroup
{
    [System.Serializable]
    public class ExtendedCarouselElementData : HKCarouselElementData
    {
        public string AudioPath { get; set; }
        public string Description { get; set; }

        public static ExtendedCarouselElementData FromMetadataFile(string filePath)
        {
            var data = new ExtendedCarouselElementData
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(filePath),
                Category = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(filePath))
            };

            try
            {
                string[] lines = System.IO.File.ReadAllLines(filePath);
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
                            data.IsImage360 = value.Trim().ToLower() == "image";
                            break;
                        case "audio":
                            data.AudioPath = value;
                            break;
                        case "description":
                            data.Description = value;
                            break;
                    }
                }

                // Set thumbnail path based on name and category
                data.ThumbnailPath = $"Thumbnails/{data.Category}/{data.Name}";

                // Log successful parsing
                Debug.Log($"[METADATA] Successfully parsed file {filePath}: Type={data.IsImage360}, Audio={data.AudioPath}, Description={data.Description}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[METADATA] Error parsing file {filePath}: {e.Message}");
            }

            return data;
        }
    }
} 