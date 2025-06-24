using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public class BuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    private const string STREAMING_ASSETS_PATH = "Assets/StreamingAssets";
    private static readonly string[] REQUIRED_FOLDERS = { "Videos", "360 Images", "Thumbnails" };

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("[BuildProcessor] Starting build preprocessing...");

        // Ensure StreamingAssets folder exists
        if (!Directory.Exists(STREAMING_ASSETS_PATH))
        {
            Debug.Log("[BuildProcessor] Creating StreamingAssets folder");
            Directory.CreateDirectory(STREAMING_ASSETS_PATH);
        }

        // Create required subfolders if they don't exist
        foreach (var folder in REQUIRED_FOLDERS)
        {
            string folderPath = Path.Combine(STREAMING_ASSETS_PATH, folder);
            if (!Directory.Exists(folderPath))
            {
                Debug.Log($"[BuildProcessor] Creating {folder} folder");
                Directory.CreateDirectory(folderPath);
            }

            // Create category subfolders
            string[] categories = { "Nature", "Space" };
            foreach (var category in categories)
            {
                string categoryPath = Path.Combine(folderPath, category);
                if (!Directory.Exists(categoryPath))
                {
                    Debug.Log($"[BuildProcessor] Creating {folder}/{category} folder");
                    Directory.CreateDirectory(categoryPath);
                }
            }
        }

        // Verify content exists
        bool hasContent = false;
        foreach (var folder in REQUIRED_FOLDERS)
        {
            string folderPath = Path.Combine(STREAMING_ASSETS_PATH, folder);
            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                hasContent = true;
                Debug.Log($"[BuildProcessor] Found {files.Length} files in {folder}");
                foreach (var file in files)
                {
                    Debug.Log($"[BuildProcessor] - {Path.GetFileName(file)}");
                }
            }
        }

        if (!hasContent)
        {
            Debug.LogWarning("[BuildProcessor] WARNING: No content found in StreamingAssets folders!");
            if (EditorUtility.DisplayDialog("No Content Found",
                "No content was found in the StreamingAssets folders. The app will have no videos or images to display. Do you want to continue with the build?",
                "Continue", "Cancel"))
            {
                Debug.Log("[BuildProcessor] User chose to continue build despite no content");
            }
            else
            {
                throw new BuildFailedException("Build cancelled due to no content in StreamingAssets");
            }
        }

        // Force Unity to include StreamingAssets in the build
        AssetDatabase.Refresh();
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
        {
            string apkPath = report.summary.outputPath;
            Debug.Log($"[BuildProcessor] Build completed. APK path: {apkPath}");

            // Verify StreamingAssets were included
            if (File.Exists(apkPath))
            {
                Debug.Log("[BuildProcessor] APK file exists. Size: " + new FileInfo(apkPath).Length / 1024 / 1024 + "MB");
                
                // Optional: You could add additional verification here using
                // System.IO.Compression.ZipFile to check APK contents
            }
        }
    }
}

[InitializeOnLoad]
public class StreamingAssetsValidator
{
    static StreamingAssetsValidator()
    {
        EditorApplication.playModeStateChanged += ValidateOnPlayMode;
    }

    private static void ValidateOnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            ValidateStreamingAssets();
        }
    }

    private static void ValidateStreamingAssets()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        if (!Directory.Exists(streamingAssetsPath))
        {
            Debug.LogError("StreamingAssets folder is missing! Create it before entering play mode.");
            EditorApplication.isPlaying = false;
            return;
        }

        string[] requiredFolders = { "Videos", "360 Images", "Thumbnails" };
        foreach (var folder in requiredFolders)
        {
            string folderPath = Path.Combine(streamingAssetsPath, folder);
            if (!Directory.Exists(folderPath))
            {
                Debug.LogError($"Required folder '{folder}' is missing in StreamingAssets!");
                EditorApplication.isPlaying = false;
                return;
            }

            // Check for content
            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                Debug.LogWarning($"No files found in {folder}. The carousel will be empty.");
            }
            else
            {
                Debug.Log($"Found {files.Length} files in {folder}");
            }
        }
    }
} 