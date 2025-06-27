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
            if (carousel == null || categoryLabel == null)
            {
                return;
            }

            if (index < 0)
            {
                categoryLabel.text = "CATEGORY";
                return;
            }

            var element = carousel.GetElementDataFromIndex(index);
            if (element != null)
            {
                categoryLabel.text = element.Category?.ToUpperInvariant() ?? "CATEGORY";
            }
            else
            {
                categoryLabel.text = "CATEGORY";
            }
        }
    }
}
