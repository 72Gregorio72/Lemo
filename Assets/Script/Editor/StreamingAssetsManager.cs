using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class StreamingAssetsManager : EditorWindow
{
    private Vector2 scrollPosition;
    private bool showVideos = true;
    private bool show360Images = true;
    private bool showThumbnails = true;
    private bool showAudio = true;
    private bool showData = true;

    [MenuItem("Tools/Streaming Assets Manager")]
    public static void ShowWindow()
    {
        GetWindow<StreamingAssetsManager>("Streaming Assets");
    }

    private void OnGUI()
    {
        GUILayout.Label("Streaming Assets Manager", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh Asset Database"))
        {
            AssetDatabase.Refresh();
        }

        if (GUILayout.Button("Create Required Folders"))
        {
            CreateRequiredFolders();
        }

        if (GUILayout.Button("Generate All Manifests"))
        {
            StreamingAssetsHelper.GenerateManifests();
        }

        EditorGUILayout.Space();
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Show all folders with same category structure
        showVideos = EditorGUILayout.Foldout(showVideos, "Videos");
        if (showVideos) ShowFolderContents("Videos");

        show360Images = EditorGUILayout.Foldout(show360Images, "360 Images");
        if (show360Images) ShowFolderContents("360 Images");

        showThumbnails = EditorGUILayout.Foldout(showThumbnails, "Thumbnails");
        if (showThumbnails) ShowFolderContents("Thumbnails");

        showAudio = EditorGUILayout.Foldout(showAudio, "Audio");
        if (showAudio) ShowFolderContents("Audio");

        showData = EditorGUILayout.Foldout(showData, "Data");
        if (showData) ShowFolderContents("Data");

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        
        if (GUILayout.Button("Add Files..."))
        {
            AddFiles();
        }

        EditorGUILayout.HelpBox(
            "Folder Structure:\n" +
            "StreamingAssets/\n" +
            "  ├── Videos/\n" +
            "  │   ├── Nature/\n" +
            "  │   └── Space/\n" +
            "  ├── 360 Images/\n" +
            "  │   ├── Nature/\n" +
            "  │   └── Space/\n" +
            "  ├── Thumbnails/\n" +
            "  │   ├── Nature/\n" +
            "  │   └── Space/\n" +
            "  ├── Audio/\n" +
            "  │   ├── Nature/\n" +
            "  │   └── Space/\n" +
            "  └── Data/\n" +
            "      ├── Nature/\n" +
            "      └── Space/", MessageType.Info);
    }

    private void ShowFolderContents(string folderName)
    {
        string basePath = Path.Combine(Application.streamingAssetsPath, folderName);
        if (!Directory.Exists(basePath))
        {
            EditorGUILayout.HelpBox($"{folderName} folder not found!", MessageType.Warning);
            return;
        }

        string[] categories = { "Nature", "Space" };
        foreach (var category in categories)
        {
            string categoryPath = Path.Combine(basePath, category);
            if (Directory.Exists(categoryPath))
            {
                EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                var files = Directory.GetFiles(categoryPath)
                    .Where(f => !f.EndsWith(".meta") && Path.GetFileName(f) != "files.txt")
                    .ToArray();

                foreach (var file in files)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(Path.GetFileName(file));
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Remove File",
                            $"Are you sure you want to remove {Path.GetFileName(file)}?",
                            "Yes", "No"))
                        {
                            File.Delete(file);
                            // Also delete the .meta file if it exists
                            string metaFile = file + ".meta";
                            if (File.Exists(metaFile))
                            {
                                File.Delete(metaFile);
                            }
                            AssetDatabase.Refresh();
                            StreamingAssetsHelper.GenerateManifests();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }
        }
    }

    private void CreateRequiredFolders()
    {
        string[] folders = { "Videos", "360 Images", "Thumbnails", "Audio", "Data" };
        string[] categories = { "Nature", "Space" };

        foreach (var folder in folders)
        {
            string folderPath = Path.Combine(Application.streamingAssetsPath, folder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            foreach (var category in categories)
            {
                string categoryPath = Path.Combine(folderPath, category);
                if (!Directory.Exists(categoryPath))
                {
                    Directory.CreateDirectory(categoryPath);
                }
            }
        }

        AssetDatabase.Refresh();
        StreamingAssetsHelper.GenerateManifests();
    }

    private void AddFiles()
    {
        string[] filters = new string[] {
            "Video files", "mp4",
            "360 Images", "png",
            "Thumbnails", "png",
            "Audio files", "wav,mp3",
            "Data files", "txt",
            "All Files", "*"
        };

        string path = EditorUtility.OpenFilePanelWithFilters(
            "Select Files",
            "",
            filters
        );

        if (!string.IsNullOrEmpty(path))
        {
            string ext = Path.GetExtension(path).ToLower();
            string targetFolder;

            // Determine target folder based on file extension and name
            if (ext == ".mp4") targetFolder = "Videos";
            else if (ext == ".wav" || ext == ".mp3") targetFolder = "Audio";
            else if (ext == ".txt") targetFolder = "Data";
            else if (Path.GetFileName(path).Contains("360")) targetFolder = "360 Images";
            else targetFolder = "Thumbnails";

            int categoryChoice = EditorUtility.DisplayDialogComplex(
                "Select Category",
                $"Choose a category for {Path.GetFileName(path)}",
                "Nature",
                "Cancel",
                "Space"
            );

            if (categoryChoice == 1) // Cancel
                return;

            string category = categoryChoice == 0 ? "Nature" : "Space";
            string targetPath = Path.Combine(Application.streamingAssetsPath, targetFolder, category, Path.GetFileName(path));

            File.Copy(path, targetPath, true);
            AssetDatabase.Refresh();
            StreamingAssetsHelper.GenerateManifests();
        }
    }
} 