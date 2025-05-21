namespace HKCarouselLayoutGroup
{
    public class HKCarouselLayoutGroup3DDemo : HKCarouselLayoutGroup3D<HKCarouselElementData> { }

    [System.Serializable]
    public class HKCarouselElementData : HKBaseCarouselData
    {
        public string Name;
        public string ThumbnailPath;
        public string Category;
        public bool IsImage360;

        private string _videoPath;
        public string VideoPath
        {
            get => _videoPath;
            internal set
            {
                _videoPath = value;
                // Extract category from path if not already set
                if (string.IsNullOrEmpty(Category) && !string.IsNullOrEmpty(_videoPath))
                {
                    string[] parts = _videoPath.Split('/');
                    if (parts.Length >= 2)
                    {
                        // The category should be the second part of the path (after Videos/360 Images)
                        Category = parts[1];
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"[HKCarouselElementData] Name: {Name}, Category: {Category}, Path: {VideoPath}, IsImage360: {IsImage360}";
        }
    }
}