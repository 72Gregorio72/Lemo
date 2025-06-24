using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class StreamingAssetsHelper : EditorWindow
{
    [MenuItem("Tools/Generate Streaming Assets Manifests")]
    public static void GenerateManifests()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        
        // All folders use the same category structure
        string[] allFolders = { "Videos", "360 Images", "Thumbnails", "Audio", "Data" };
        string[] categories = { "Nature", "Space", "Relaxation" };

        foreach (var folder in allFolders)
        {
            string folderPath = Path.Combine(streamingAssetsPath, folder);
            if (!Directory.Exists(folderPath)) continue;

            foreach (var category in categories)
            {
                string categoryPath = Path.Combine(folderPath, category);
                if (!Directory.Exists(categoryPath)) continue;

                GenerateManifestForDirectory(categoryPath, folder);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("Generated all manifests successfully!");
    }

    private static void GenerateManifestForDirectory(string directoryPath, string folderType)
    {
        // Get all relevant files
        var files = new List<string>();
        
        // Get files based on folder type
        switch (folderType)
        {
            case "Videos":
                files.AddRange(Directory.GetFiles(directoryPath, "*.mp4", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                files.AddRange(Directory.GetFiles(directoryPath, "*.mp4.meta", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                break;
            case "360 Images":
            case "Thumbnails":
                files.AddRange(Directory.GetFiles(directoryPath, "*.png", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                files.AddRange(Directory.GetFiles(directoryPath, "*.png.meta", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                break;
            case "Audio":
                files.AddRange(Directory.GetFiles(directoryPath, "*.wav", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                files.AddRange(Directory.GetFiles(directoryPath, "*.wav.meta", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                files.AddRange(Directory.GetFiles(directoryPath, "*.mp3", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                files.AddRange(Directory.GetFiles(directoryPath, "*.mp3.meta", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                break;
            case "Data":
                files.AddRange(Directory.GetFiles(directoryPath, "*.txt", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                files.AddRange(Directory.GetFiles(directoryPath, "*.txt.meta", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName));
                break;
        }

        // Also include the manifest's own meta file if it exists
        string manifestMetaPath = Path.Combine(directoryPath, "files.txt.meta");
        if (File.Exists(manifestMetaPath))
        {
            files.Add("files.txt.meta");
        }

        if (files.Count == 0)
        {
            Debug.LogWarning($"No relevant files found in {directoryPath}");
            return;
        }

        // Sort files for consistency
        files.Sort();

        // Create manifest
        string manifestPath = Path.Combine(directoryPath, "files.txt");
        File.WriteAllLines(manifestPath, files);

        Debug.Log($"Generated manifest for {directoryPath} with {files.Count} files");
    }

    [MenuItem("Tools/List All Streaming Assets")]
    public static void ListAllFiles()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        var allFiles = Directory.GetFiles(streamingAssetsPath, "*.*", SearchOption.AllDirectories);

        Debug.Log("All files in StreamingAssets:");
        foreach (var file in allFiles)
        {
            string relativePath = file.Substring(streamingAssetsPath.Length + 1);
            Debug.Log(relativePath);
        }
    }

    [MenuItem("Tools/Verify Manifests")]
    public static void VerifyManifests()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        string[] allManifests = Directory.GetFiles(streamingAssetsPath, "files.txt", SearchOption.AllDirectories);

        foreach (var manifest in allManifests)
        {
            string directory = Path.GetDirectoryName(manifest);
            string[] listedFiles = File.ReadAllLines(manifest);
            
            foreach (var file in listedFiles)
            {
                string fullPath = Path.Combine(directory, file);
                if (!File.Exists(fullPath))
                {
                    Debug.LogError($"Missing file {file} listed in manifest: {manifest}");
                }
            }

            // Check for files not in manifest
            var actualFiles = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(f => f != "files.txt");

            var missingFromManifest = actualFiles.Except(listedFiles);
            foreach (var file in missingFromManifest)
            {
                Debug.LogWarning($"File exists but not in manifest: {Path.Combine(directory, file)}");
            }
        }

        Debug.Log("Manifest verification complete!");
    }
} 