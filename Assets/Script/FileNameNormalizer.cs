using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Centralized file name normalization utility to handle case sensitivity, 
/// whitespace, and spacing inconsistencies across different file types.
/// 
/// This solves the problem where:
/// - "Video Name.txt" doesn't match "video name.mp4" (case difference)
/// - "Video Name.txt" doesn't match "Video  Name.mp4" (extra spaces)
/// - "Video Name .txt" doesn't match "Video Name.mp4" (trailing space)
/// </summary>
public static class FileNameNormalizer
{
    /// <summary>
    /// Normalizes a file name for consistent matching by:
    /// - Converting to lowercase
    /// - Trimming leading/trailing whitespace
    /// - Normalizing multiple spaces to single spaces
    /// - Removing problematic characters
    /// </summary>
    public static string NormalizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        // Trim whitespace
        string normalized = fileName.Trim();
        
        // Convert to lowercase for case-insensitive matching
        normalized = normalized.ToLowerInvariant();
        
        // Normalize multiple spaces to single spaces
        normalized = Regex.Replace(normalized, @"\s+", " ");
        
        // Remove other problematic characters that might cause issues
        normalized = normalized.Replace("\t", " ").Replace("\r", "").Replace("\n", "");
        
        // Final trim after all replacements
        normalized = normalized.Trim();
        
        return normalized;
    }

    /// <summary>
    /// Attempts to find a file with robust matching across different extensions.
    /// Handles case sensitivity and spacing issues by trying multiple variations.
    /// </summary>
    public static string FindFileWithNormalization(string basePath, string fileName, string[] extensions)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fileName) || extensions == null)
            return null;

        string normalizedFileName = NormalizeFileName(fileName);
        
        // First try: exact match (fastest path)
        foreach (string ext in extensions)
        {
            string exactPath = Path.Combine(basePath, fileName + ext);
            if (File.Exists(exactPath))
            {
                Debug.Log($"[FileNormalizer] Exact match found: {exactPath}");
                return exactPath;
            }
        }

        // Second try: normalized filename matching
        if (!Directory.Exists(basePath))
            return null;

        try
        {
            string[] allFiles = Directory.GetFiles(basePath);
            
            foreach (string filePath in allFiles)
            {
                string fileNameOnly = Path.GetFileNameWithoutExtension(filePath);
                string fileExtension = Path.GetExtension(filePath);
                
                // Check if this extension is one we're looking for
                bool isTargetExtension = false;
                foreach (string ext in extensions)
                {
                    if (string.Equals(fileExtension, ext, StringComparison.OrdinalIgnoreCase))
                    {
                        isTargetExtension = true;
                        break;
                    }
                }
                
                if (!isTargetExtension)
                    continue;
                
                string normalizedFileNameOnly = NormalizeFileName(fileNameOnly);
                
                if (normalizedFileNameOnly == normalizedFileName)
                {
                    Debug.Log($"[FileNormalizer] Normalized match found: '{fileName}' -> '{Path.GetFileName(filePath)}'");
                    return filePath;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FileNormalizer] Error scanning directory {basePath}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Specialized method for finding media files (videos/images) with normalization
    /// </summary>
    public static string FindMediaFile(string mediaPath, string fileName, bool isImage)
    {
        string[] extensions = isImage ? new[] { ".png", ".jpg", ".jpeg" } : new[] { ".mp4", ".mov", ".avi" };
        return FindFileWithNormalization(mediaPath, fileName, extensions);
    }

    /// <summary>
    /// Specialized method for finding audio files with normalization
    /// </summary>
    public static string FindAudioFile(string audioPath, string fileName)
    {
        string[] extensions = { ".mp3", ".wav", ".ogg" };
        return FindFileWithNormalization(audioPath, fileName, extensions);
    }

    /// <summary>
    /// Specialized method for finding thumbnail files with normalization
    /// </summary>
    public static string FindThumbnailFile(string thumbnailPath, string fileName)
    {
        string[] extensions = { ".png", ".jpg", ".jpeg" };
        return FindFileWithNormalization(thumbnailPath, fileName, extensions);
    }

    /// <summary>
    /// Specialized method for finding data/metadata files with normalization
    /// </summary>
    public static string FindDataFile(string dataPath, string fileName)
    {
        string[] extensions = { ".txt" };
        return FindFileWithNormalization(dataPath, fileName, extensions);
    }

    /// <summary>
    /// Diagnostic method to show normalization results for debugging
    /// </summary>
    public static void LogNormalizationComparison(string original, string normalized)
    {
        if (original != normalized)
        {
            Debug.Log($"[FileNormalizer] Normalized: '{original}' -> '{normalized}'");
        }
    }

    /// <summary>
    /// Checks if two file names would match after normalization
    /// </summary>
    public static bool AreNamesEquivalent(string name1, string name2)
    {
        return NormalizeFileName(name1) == NormalizeFileName(name2);
    }
} 