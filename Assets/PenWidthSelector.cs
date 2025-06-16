using UnityEngine;
using UnityEngine.UI;

public class PenWidthSelector : MonoBehaviour
{
    [SerializeField] private Transform trackingTarget;       // Cubo grabbabile
    [SerializeField] private RectTransform sliderRect;       // UI slider visivo
    [SerializeField] private Slider widthSlider;             // Slider Unity
    [SerializeField] private AirDrawing airDrawing;          // Script della matita

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (trackingTarget == null || sliderRect == null || widthSlider == null || airDrawing == null)
            return;

        // 1. Coordinate schermo
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(mainCamera, trackingTarget.position);

        // 2. Coordinate locali nel RectTransform
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            sliderRect,
            screenPoint,
            mainCamera,
            out Vector2 localPoint))
        {
            float height = sliderRect.rect.height;
            float deltaY = height * 0.5f;

            localPoint.y = Mathf.Clamp(localPoint.y, -deltaY, deltaY);
            localPoint.x = 0f;

            Vector3 worldPoint = sliderRect.TransformPoint(localPoint);
            trackingTarget.position = worldPoint;

            // 3. Valore normalizzato
            float normalizedY = (localPoint.y + deltaY) / height;
            widthSlider.value = normalizedY;

            // 4. Applica al disegno
            airDrawing.lineWidth = Mathf.Lerp(0.001f, 0.02f, normalizedY); // esempio da 0.001 a 0.02
            Debug.Log("Line width updated: " + airDrawing.lineWidth);
        }
    }
}