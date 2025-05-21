using System.IO;
using UnityEngine;

public class PersistentAssetInitializer : MonoBehaviour
{
    void Awake()
    {
        string sourcePath = Application.streamingAssetsPath;
        string destinationPath = Application.persistentDataPath;

        string markerFile = Path.Combine(destinationPath, "init_done.txt");
        if (!File.Exists(markerFile))
        {
            CopyDirectory(sourcePath, destinationPath);
            File.WriteAllText(markerFile, "done");
        }
    }

    void CopyDirectory(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(destinationDir))
            Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string dest = Path.Combine(destinationDir, Path.GetFileName(file));
            if (!File.Exists(dest))
                File.Copy(file, dest);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string newDestDir = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, newDestDir);
        }
    }
}
