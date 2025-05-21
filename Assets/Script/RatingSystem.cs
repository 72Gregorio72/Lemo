using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using DG.Tweening;
using HKCarouselLayoutGroup;
using System.IO;

public class RatingSystem : MonoBehaviour
{
    [System.Serializable]
    public class RatingData
    {
        public float average;
        public int count;
    }

    public string rateUsTag = "RateUs";
    public float holdDuration = 5f;

    private List<Image> halves = new();
    private int currentIndex = 0;

    private GameObject rateUsObject;
    private RectTransform rateUsTransform;

    private List<InputDevice> devices = new();
    private Dictionary<InputDevice, bool> wasPressedLastFrame = new();
    public bool exitTriggered = false;
    [HideInInspector] public string experienceName = "Unknown";

    private HKCarouselLayoutGroup3D<HKCarouselElementData> carousel;

    void Start()
    {
        rateUsObject = GameObject.FindWithTag(rateUsTag);
        if (rateUsObject == null)
        {
            Debug.LogError("No object with tag 'RateUs' found.");
            enabled = false;
            return;
        }

        carousel = FindFirstObjectByType<HKCarouselLayoutGroup3D<HKCarouselElementData>>();
        if (carousel == null)
        {
            Debug.LogWarning("Carousel not found in the scene.");
        }

        rateUsTransform = rateUsObject.GetComponent<RectTransform>();

        for (int i = 1; i <= 5; i++)
        {
            var star = rateUsObject.transform.Find($"ImageBackground/VideoPanel/Shadow/Star {i}");
            if (star == null) continue;

            var left = star.Find("Left")?.GetComponent<Image>();
            var right = star.Find("Right")?.GetComponent<Image>();

            if (left != null) halves.Add(left);
            if (right != null) halves.Add(right);
        }

        InputDevices.GetDevices(devices);
        foreach (var device in devices)
        {
            wasPressedLastFrame[device] = false;
        }
    }

    void Update()
    {
        if (exitTriggered) return;

        InputDevices.GetDevices(devices);

        foreach (var device in devices)
        {
            if (!device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool isPressed))
                continue;

            bool wasPressed = wasPressedLastFrame.ContainsKey(device) && wasPressedLastFrame[device];

            if (isPressed && !wasPressed)
            {
                HandleRatingStep();
            }

            wasPressedLastFrame[device] = isPressed;
        }
    }

    void HandleRatingStep()
    {
        if (currentIndex >= halves.Count) return;

        var img = halves[currentIndex];
        if (img != null)
        {
            Color c = img.color;
            c.a = 1f;
            img.color = c;
        }

        currentIndex++;
    }

    public void SubmitRating()
    {
        int filledHalves = 0;
        foreach (var img in halves)
        {
            if (img != null && img.color.a >= 1f)
                filledHalves++;
        }

        float rating = filledHalves / 2f;

        string nameToUse = experienceName ?? "Unknown";
        Debug.Log($"[{nameToUse}] rating: {rating} stars");

        // Load existing ratings from Resources
        TextAsset ratingJson = Resources.Load<TextAsset>("Rating/Rating");
        Dictionary<string, RatingData> ratings = new();

        if (ratingJson != null)
        {
            ratings = JsonUtilityWrapper.FromJson<RatingData>(ratingJson.text);
        }

        // Update or add the new rating
        if (ratings.ContainsKey(nameToUse))
        {
            var existing = ratings[nameToUse];
            existing.average = ((existing.average * existing.count) + rating) / (existing.count + 1);
            existing.count++;
            ratings[nameToUse] = existing;
        }
        else
        {
            ratings[nameToUse] = new RatingData
            {
                average = rating,
                count = 1
            };
        }

        // Save the updated ratings
        string json = JsonUtilityWrapper.ToJson(ratings);
        
#if UNITY_EDITOR
        // In editor, we can write directly to the Resources folder
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        string ratingPath = Path.Combine(resourcesPath, "Rating");
        
        if (!Directory.Exists(ratingPath))
            Directory.CreateDirectory(ratingPath);
            
        File.WriteAllText(Path.Combine(ratingPath, "Rating.json"), json);
        UnityEditor.AssetDatabase.Refresh();
#else
        // In builds, we need to use PlayerPrefs or other persistence method
        PlayerPrefs.SetString("Ratings", json);
        PlayerPrefs.Save();
#endif

        exitTriggered = true;
    }

    public static class JsonUtilityWrapper
    {
        [System.Serializable]
        private class Wrapper<T>
        {
            public List<string> keys = new();
            public List<T> values = new();
        }

        public static Dictionary<string, T> FromJson<T>(string json)
        {
            var wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
            Dictionary<string, T> dict = new();
            for (int i = 0; i < wrapper.keys.Count; i++)
                dict[wrapper.keys[i]] = wrapper.values[i];
            return dict;
        }

        public static string ToJson<T>(Dictionary<string, T> dict)
        {
            var wrapper = new Wrapper<T>();
            foreach (var kv in dict)
            {
                wrapper.keys.Add(kv.Key);
                wrapper.values.Add(kv.Value);
            }
            return JsonUtility.ToJson(wrapper, true);
        }
    }

    public void Exit()
    {
        if (rateUsTransform != null)
        {
            rateUsTransform.DOScale(Vector3.zero, 0.5f)
                .SetEase(Ease.InBack)
                .OnComplete(() => rateUsObject.SetActive(false));
        }

        exitTriggered = true;
    }
}
