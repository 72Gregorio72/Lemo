using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HKCarouselLayoutGroup;

public class SceneCarouselElementDemo : MonoBehaviour, ICarouselElement<HKSceneCarouselData>
{
    [Header("UI References")]
    [SerializeField] private Image thumbnail;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private CanvasGroup categoryGroup;

    private int elementIndex;

    public void ConfigureElement(HKSceneCarouselData data, int index)
    {
        elementIndex = index;

        // Set title
        if (titleText != null)
        {
            titleText.text = data.sceneName;
        }

        // Set description
        if (descriptionText != null)
        {
            descriptionText.text = data.description;
        }

        // Load and set thumbnail
        if (thumbnail != null && !string.IsNullOrEmpty(data.thumbnailPath))
        {
            var thumbnailTexture = Resources.Load<Texture2D>(data.thumbnailPath);
            if (thumbnailTexture != null)
            {
                thumbnail.sprite = Sprite.Create(
                    thumbnailTexture,
                    new Rect(0, 0, thumbnailTexture.width, thumbnailTexture.height),
                    new Vector2(0.5f, 0.5f)
                );
            }
        }
    }

    public void SetCategoryAlpha(float alpha)
    {
        if (categoryGroup != null)
        {
            categoryGroup.alpha = alpha;
        }
    }

    public int GetID()
    {
        return elementIndex;
    }
} 