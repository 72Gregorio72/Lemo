using UnityEngine;
using UnityEngine.UI;

public class HueSliderControll : MonoBehaviour
{
    [SerializeField] private Transform trackingTarget;               // Oggetto VR (es. sfera)
    [SerializeField] private RectTransform sliderRect;               // RectTransform dello Slider
    [SerializeField] private Slider hueSlider;                       // Lo Slider
    [SerializeField] private ColorPickerControll colorPicker;        // Il ColorPicker

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (trackingTarget == null || sliderRect == null || hueSlider == null || colorPicker == null)
            return;

        // 1. Converti la posizione del tracker in coordinate schermo
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(mainCamera, trackingTarget.position);

        // 2. Converti in coordinate locali del RectTransform
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            sliderRect,
            screenPoint,
            mainCamera,
            out Vector2 localPoint))
        {
            float height = sliderRect.rect.height;
            float deltaY = height * 0.5f;

            // 3. Clamp posizione verticale dentro lo slider
            localPoint.y = Mathf.Clamp(localPoint.y, -deltaY, deltaY);
            localPoint.x = 0f; // blocchiamo X per mantenerlo centrato

            // 4. Applica la nuova posizione "bloccata"
            Vector3 worldPoint = sliderRect.TransformPoint(localPoint);
            trackingTarget.position = worldPoint;

            // 5. Calcola valore normalizzato e aggiorna slider
            float normalizedY = (localPoint.y + deltaY) / height;
            hueSlider.value = normalizedY;

            // 6. Aggiorna le texture colore
            //Debug.Log("Hue value updated: " + hueSlider.value);
        }
    }

    public void UpdateImage()
    {
        colorPicker.UpdateSVImage();
    }
}
