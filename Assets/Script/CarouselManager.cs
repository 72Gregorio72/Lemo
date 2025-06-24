using UnityEngine;
using TMPro;
using System.Collections;
using HKCarouselLayoutGroup;
using UnityEngine.UI;
using System.IO;

public class CarouselManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject carouselPrefab;
    [SerializeField] private Transform carouselParent;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private XR360CarouselController carouselController; // Reference to the controller

    [Header("Settings")]
    [SerializeField] private bool checkForNewContent = true;
    [SerializeField] private float delayBeforeHidingLoading = 1f;

    private HKCarouselLayoutGroup3D<HKCarouselElementData> activeCarousel;
    private bool isInitialized = false;

    private void Start()
    {
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        StartCoroutine(InitializeCarousel());
    }

    private IEnumerator InitializeCarousel()
    {
        if (progressText != null)
            progressText.text = "Checking for content...";

        // Create the carousel directly
        GameObject carouselGO = Instantiate(carouselPrefab, Vector3.zero, Quaternion.identity);
        if (carouselParent != null)
            carouselGO.transform.SetParent(carouselParent, false);

        activeCarousel = carouselGO.GetComponent<HKCarouselLayoutGroup3D<HKCarouselElementData>>();
        
        if (activeCarousel == null)
        {
            Debug.LogError("Carousel prefab does not have HKCarouselLayoutGroup3D component!");
            Destroy(carouselGO);
            yield break;
        }

        // Wait for initialization to complete
        while (!activeCarousel.IsInitialized)
        {
            yield return null;
        }

        // Check if we have any content
        if (!HasContent())
        {
            Debug.LogWarning("No content found in persistent data path!");
            if (progressText != null)
                progressText.text = "No content available";
            Destroy(carouselGO);
            yield break;
        }

        // Initialize the carousel controller
        if (carouselController != null)
        {
            var demo = carouselGO.GetComponent<HKCarouselLayoutGroup3DDemo>();
            if (demo != null)
            {
                carouselController.Initialize(demo);
                Debug.Log("Successfully initialized carousel controller");
            }
            else
            {
                Debug.LogError("Could not find HKCarouselLayoutGroup3DDemo component on instantiated carousel!");
            }
        }
        else
        {
            Debug.LogWarning("No XR360CarouselController reference set in CarouselManager!");
        }

        isInitialized = true;

        // Hide loading screen with delay
        if (loadingScreen != null)
        {
            yield return new WaitForSeconds(delayBeforeHidingLoading);
            loadingScreen.SetActive(false);
        }

        if (progressText != null)
            progressText.gameObject.SetActive(false);
    }

    private bool HasContent()
    {
        string[] folders = { "Videos", "360 Images" };
        string persistentPath = Application.persistentDataPath;

        foreach (var folder in folders)
        {
            string folderPath = Path.Combine(persistentPath, folder);
            if (Directory.Exists(folderPath))
            {
                // Check each category folder
                foreach (var categoryDir in Directory.GetDirectories(folderPath))
                {
                    if (Directory.GetFiles(categoryDir, "*.*").Length > 0)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public void RefreshCarousel()
    {
        if (isInitialized && activeCarousel != null)
        {
            StartCoroutine(InitializeCarousel());
        }
    }

    // Optional: Method to check if new content is available
    private bool HasNewContent()
    {
        // Implementation depends on how you want to track new content
        // Could compare file counts, check timestamps, etc.
        return false;
    }
} 