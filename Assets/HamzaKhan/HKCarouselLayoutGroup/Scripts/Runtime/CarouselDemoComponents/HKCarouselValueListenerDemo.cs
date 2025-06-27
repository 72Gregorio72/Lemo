using UnityEngine;

namespace HKCarouselLayoutGroup
{
    public class HKCarouselValueListenerDemo : MonoBehaviour
    {

        [Header("This Component debugs the selected items name")]
        [SerializeField] private HKCarouselLayoutGroup3DDemo _carouselLayoutGroup;

        private void Start()
        {
            _carouselLayoutGroup.OnValueChanged.AddListener(OnNewItemSelected);

            OnNewItemSelected(_carouselLayoutGroup.GetTrueSelectedIndex());
        }

        private void OnNewItemSelected(int index)
        {
            if (_carouselLayoutGroup == null)
            {
                Debug.LogWarning("[CarouselValueListener] Carousel layout group reference is null");
                return;
            }

            if (index < 0)
            {
                Debug.LogWarning("[CarouselValueListener] Invalid index: " + index);
                return;
            }

            HKCarouselElementData carouselElementData = _carouselLayoutGroup.GetElementDataFromIndex(index);

            if (carouselElementData != null)
            {
#if UNITY_EDITOR
                Debug.Log($"Current Selected Item Name {carouselElementData.Name}");
#endif
            }
        }
    }
}