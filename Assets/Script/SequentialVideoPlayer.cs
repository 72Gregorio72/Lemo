using UnityEngine;
using UnityEngine.Video;
using System.Collections;

public class SequentialVideoPlayer : MonoBehaviour
{
    [SerializeField]
    private VideoPlayer videoPlayer;

    [SerializeField]
    private string resourcesFolderPath = "Videos/";  // Path inside Resources folder

    private int currentVideoIndex = 0;
    private const int TOTAL_VIDEOS = 14;

    private void Start()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        // Set up video player settings
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
        videoPlayer.aspectRatio = VideoAspectRatio.FitVertically;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

        // Add listener for when video finishes
        videoPlayer.loopPointReached += OnVideoFinished;

        // Start playing the first video
        PlayCurrentVideo();
    }

    private void PlayCurrentVideo()
    {
        if (currentVideoIndex >= TOTAL_VIDEOS)
        {
            Debug.Log("All videos have been played!");
            return;
        }

        string videoName = $"T{currentVideoIndex + 1}";
        VideoClip videoClip = Resources.Load<VideoClip>(resourcesFolderPath + videoName);

        Debug.Log($"Attempting to play video: {videoName}");

        if (videoClip != null)
        {
            videoPlayer.clip = videoClip;
            videoPlayer.Prepare();
            StartCoroutine(WaitForVideoToLoad());
        }
        else
        {
            Debug.LogError($"Video not found in Resources folder: {videoName}. Make sure video is placed in Assets/Resources/{resourcesFolderPath}");
            // Skip to next video if current one is not found
            currentVideoIndex++;
            PlayCurrentVideo();
        }
    }

    private IEnumerator WaitForVideoToLoad()
    {
        while (!videoPlayer.isPrepared)
        {
            Debug.Log("Preparing video...");
            yield return null;
        }

        Debug.Log("Video prepared, starting playback...");
        videoPlayer.Play();
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log($"Video {currentVideoIndex + 1} finished playing");
        currentVideoIndex++;
        PlayCurrentVideo();
    }

    public void RestartSequence()
    {
        currentVideoIndex = 0;
        PlayCurrentVideo();
    }

    // Helper method to check if all required videos are present
    public void CheckVideosExistence()
    {
        for (int i = 1; i <= TOTAL_VIDEOS; i++)
        {
            string videoName = $"T{i}";
            VideoClip videoClip = Resources.Load<VideoClip>(resourcesFolderPath + videoName);
            if (videoClip == null)
            {
                Debug.LogWarning($"Missing video: {videoName} in Resources/{resourcesFolderPath}");
            }
        }
    }
} 