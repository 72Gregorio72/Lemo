using UnityEngine;
using UnityEngine.UI;

public class SVImageControll : MonoBehaviour
{
    [SerializeField] private Image pickerImage;
    [SerializeField] private Transform trackingTarget; // Il target (es. sfera)

    private RawImage SVimage;
    private ColorPickerControll CC;
    private RectTransform rectTransform, pickerTransform;
    private Camera mainCamera;

    private void Awake()
    {
        SVimage = GetComponent<RawImage>();
        CC = GetComponentInParent<ColorPickerControll>();
        rectTransform = GetComponent<RectTransform>();
        pickerTransform = pickerImage.GetComponent<RectTransform>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (trackingTarget == null) return;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(mainCamera, trackingTarget.position);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            screenPoint,
            mainCamera,
            out Vector2 localPoint))
        {
            // Limita i valori dentro l’area del picker
            float width = rectTransform.sizeDelta.x;
            float height = rectTransform.sizeDelta.y;

            float deltaX = width * 0.5f;
            float deltaY = height * 0.5f;

            localPoint.x = Mathf.Clamp(localPoint.x, -deltaX, deltaX);
            localPoint.y = Mathf.Clamp(localPoint.y, -deltaY, deltaY);

            // 🔁 Converto il punto locale limitato di nuovo in posizione mondo
            Vector3 worldPoint = rectTransform.TransformPoint(localPoint);

            // 🛑 Forzo il trackingTarget a rimanere dentro l’area
            trackingTarget.position = worldPoint;

            // ✅ Ora aggiorno il picker e il colore
            MovePickerAndUpdateColor(localPoint);
        }
    }

    private void MovePickerAndUpdateColor(Vector2 localPoint)
    {
        float width = rectTransform.sizeDelta.x;
        float height = rectTransform.sizeDelta.y;

        float deltaX = width * 0.5f;
        float deltaY = height * 0.5f;

        localPoint.x = Mathf.Clamp(localPoint.x, -deltaX, deltaX);
        localPoint.y = Mathf.Clamp(localPoint.y, -deltaY, deltaY);

        pickerTransform.localPosition = localPoint;

        float xNorm = (localPoint.x + deltaX) / width;
        float yNorm = (localPoint.y + deltaY) / height;

        // Update colore
        pickerImage.color = Color.HSVToRGB(0, 0, 1 - yNorm);
        CC.SetSV(xNorm, yNorm);
    }
}
