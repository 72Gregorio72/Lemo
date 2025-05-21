using UnityEngine;

namespace HKCarouselLayoutGroup
{
    public class HKSceneCarouselValueListener : MonoBehaviour
    {
        [Header("This Component debugs the selected scene's name")]
        [SerializeField] private HKSceneCarouselLayoutGroup3D sceneCarousel;

        private void Start()
        {
            if (sceneCarousel != null)
            {
                sceneCarousel.OnValueChanged.AddListener(OnNewSceneSelected);
                OnNewSceneSelected(sceneCarousel.GetTrueSelectedIndex());
            }
        }

        private void OnNewSceneSelected(int index)
        {
            HKSceneCarouselData sceneData = sceneCarousel.GetElementDataFromIndex(index);

            if (sceneData != null)
            {
#if UNITY_EDITOR
                Debug.Log($"Current Selected Scene: {sceneData.sceneName} (Build Index: {sceneData.sceneIndex})");
#endif
            }
        }

        private void OnDestroy()
        {
            if (sceneCarousel != null)
            {
                sceneCarousel.OnValueChanged.RemoveListener(OnNewSceneSelected);
            }
        }
    }
} 