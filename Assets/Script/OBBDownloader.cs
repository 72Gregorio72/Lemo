using UnityEngine;
using System.IO;
using System;

public class OBBDownloader : MonoBehaviour
{
    private static string mainOBBPath;
    private static string patchOBBPath;

    void Awake()
    {
        #if UNITY_ANDROID
        SetupOBBPaths();
        #endif
    }

    private void SetupOBBPaths()
    {
        try
        {
            string packageName = Application.identifier;
            int version = Application.version.Split('.')[0].Length > 0 ? 
                         int.Parse(Application.version.Split('.')[0]) : 1;

            // Format: /storage/emulated/0/Android/obb/[package-name]/[main|patch].[version].[package-name].obb
            string obbPath = Path.Combine(AndroidOBBPath(), packageName);
            mainOBBPath = Path.Combine(obbPath, $"main.{version}.{packageName}.obb");
            patchOBBPath = Path.Combine(obbPath, $"patch.{version}.{packageName}.obb");

            Debug.Log($"OBB Paths set - Main: {mainOBBPath}, Patch: {patchOBBPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error setting up OBB paths: {e.Message}");
        }
    }

    private string AndroidOBBPath()
    {
        // This is the standard path for OBB files on Android
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal)
            .Replace("/files", ""), "obb");
    }

    public static string GetMainOBBPath()
    {
        return mainOBBPath;
    }

    public static string GetPatchOBBPath()
    {
        return patchOBBPath;
    }

    public static bool AreOBBFilesPresent()
    {
        return File.Exists(mainOBBPath) || File.Exists(patchOBBPath);
    }
} 