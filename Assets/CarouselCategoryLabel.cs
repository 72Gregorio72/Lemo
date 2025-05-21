using TMPro;
using UnityEngine;

namespace HKCarouselLayoutGroup
{
    public class CarouselCategoryLabel : MonoBehaviour
    {
        [SerializeField] private HKCarouselLayoutGroup3DDemo carousel;
        [SerializeField] private TextMeshProUGUI categoryLabel;

        private void Start()
        {
            if (carousel != null)
            {
                // Subscribe to index change
                carousel.OnValueChanged.AddListener(UpdateLabel);
                UpdateLabel(carousel.GetTrueSelectedIndex());
            }
        }

        private void UpdateLabel(int index)
        {
            var element = carousel.GetElementDataFromIndex(index);
            categoryLabel.text = element.Category?.ToUpperInvariant() ?? "CATEGORY";
        }
    }
}
