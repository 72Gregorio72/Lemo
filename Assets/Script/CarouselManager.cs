using UnityEngine;
using TMPro;
using System.Collections;
using HKCarouselLayoutGroup;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;

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

        // Check if we have any content FIRST
        if (!HasContent())
        {
            Debug.LogWarning("No content found in persistent data path!");
            if (progressText != null)
                progressText.text = "No content available";
            yield break;
        }

        if (progressText != null)
            progressText.text = "Creating carousel...";

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

        if (progressText != null)
            progressText.text = "Loading metadata...";

        // Find XR360CarouselController if not assigned
        if (carouselController == null)
        {
            carouselController = FindObjectOfType<XR360CarouselController>();
            if (carouselController != null)
            {
                Debug.Log("[CarouselManager] Found XR360CarouselController automatically");
            }
        }

        // Initialize the carousel controller BEFORE carousel tries to initialize itself
        if (carouselController != null)
        {
            var demo = carouselGO.GetComponent<HKCarouselLayoutGroup3DDemo>();
            if (demo != null)
            {
                Debug.Log("[CarouselManager] Initializing XR360CarouselController with carousel...");
                carouselController.Initialize(demo);
                
                // Give it a few frames to load metadata and populate elements
                yield return new WaitForSeconds(1f);
                
                Debug.Log("[CarouselManager] XR360CarouselController initialization complete");
            }
            else
            {
                Debug.LogError("Could not find HKCarouselLayoutGroup3DDemo component on instantiated carousel!");
                Destroy(carouselGO);
                yield break;
            }
        }
        else
        {
            Debug.LogError("No XR360CarouselController reference set in CarouselManager!");
            Destroy(carouselGO);
            yield break;
        }

        if (progressText != null)
            progressText.text = "Finalizing carousel...";

        // Now wait for carousel initialization to complete (if it hasn't already)
        while (!activeCarousel.IsInitialized)
        {
            yield return null;
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

        Debug.Log("[CarouselManager] Carousel initialization complete!");
    }

    private bool HasContent()
    {
        // Get current scene name for scene-aware content checking
        string activeSceneName = SceneManager.GetActiveScene().name;
        
        // Check for metadata files in scene-specific Data/ folder
        string dataPath = Path.Combine(Application.persistentDataPath, activeSceneName, "Data");
        Debug.Log($"[CarouselManager] Checking for scene-aware content in: {dataPath}");
        
        if (!Directory.Exists(dataPath))
        {
            Debug.Log($"[CarouselManager] Scene-specific data directory does not exist for scene: {activeSceneName}");
            return false;
        }

        // Check each category folder for .txt files
        foreach (var categoryDir in Directory.GetDirectories(dataPath))
        {
            string category = Path.GetFileName(categoryDir);
            var txtFiles = Directory.GetFiles(categoryDir, "*.txt")
                .Where(f => {
                    string name = Path.GetFileNameWithoutExtension(f).ToLower();
                    return !name.StartsWith("files") && !name.StartsWith("home");
                })
                .ToArray();
                
            Debug.Log($"[CarouselManager] Scene '{activeSceneName}' - Category {category}: found {txtFiles.Length} metadata files");
            
            if (txtFiles.Length > 0)
            {
                return true;
            }
        }
        
        Debug.Log($"[CarouselManager] No metadata content found for scene: {activeSceneName}");
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