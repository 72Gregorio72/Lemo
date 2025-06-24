using UnityEngine;
using UnityEngine.Video;
using System.IO;

public class OBBVideoLoader : MonoBehaviour
{
    [SerializeField]
    private VideoPlayer videoPlayer;
    
    [SerializeField]
    private string videoPathInOBB = "Videos/Nature/Teatro.mp4";

    void Start()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        LoadVideoFromOBB();
    }

    private void LoadVideoFromOBB()
    {
        #if UNITY_ANDROID
        if (OBBDownloader.AreOBBFilesPresent())
        {
            string mainOBBPath = OBBDownloader.GetMainOBBPath();
            if (File.Exists(mainOBBPath))
            {
                // Construct the full path to the video within the OBB file
                string videoPath = Path.Combine(mainOBBPath, videoPathInOBB);
                videoPlayer.url = videoPath;
                Debug.Log($"Loading video from OBB: {videoPath}");
            }
            else
            {
                Debug.LogError("Main OBB file not found!");
            }
        }
        else
        {
            Debug.LogError("OBB files not present!");
        }
        #endif
    }
} 