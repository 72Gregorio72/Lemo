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