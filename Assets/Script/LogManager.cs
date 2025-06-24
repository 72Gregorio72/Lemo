using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class LogManager : MonoBehaviour
{
    private static LogManager instance;
    private string logFilePath;
    private StringBuilder logBuffer;
    private float lastWriteTime;
    private const float WRITE_INTERVAL = 5f; // Write to file every 5 seconds
    private const int MAX_BUFFER_SIZE = 1000; // Maximum number of log entries to keep in memory
    private Queue<string> logQueue;
    private Dictionary<string, int> errorCounts = new Dictionary<string, int>();
    private HashSet<string> checkedPaths = new HashSet<string>();

    public static LogManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("LogManager");
                instance = go.AddComponent<LogManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        logBuffer = new StringBuilder();
        logQueue = new Queue<string>();
        InitializeLogFile();
        
        // Subscribe to Unity's log callback
        Application.logMessageReceived += HandleLog;

        // Log system paths
        LogSystemPaths();
    }

    private void LogSystemPaths()
    {
        var pathsToLog = new List<(string name, string path)>
        {
            ("Application.dataPath", Application.dataPath),
            ("Application.persistentDataPath", Application.persistentDataPath),
            ("Application.streamingAssetsPath", Application.streamingAssetsPath),
            ("Application.temporaryCachePath", Application.temporaryCachePath),
            ("Application.consoleLogPath", Application.consoleLogPath),
            ("Working Directory", Environment.CurrentDirectory)
        };

        LogCustomMessage("=== System Paths ===", "SystemInfo");
        foreach (var pathInfo in pathsToLog)
        {
            LogCustomMessage($"{pathInfo.name}: {pathInfo.path}", "SystemInfo");
            if (Directory.Exists(pathInfo.path))
            {
                try
                {
                    var files = Directory.GetFiles(pathInfo.path, "*.*", SearchOption.TopDirectoryOnly);
                    var dirs = Directory.GetDirectories(pathInfo.path, "*", SearchOption.TopDirectoryOnly);
                    LogCustomMessage($"{pathInfo.name} contains {files.Length} files and {dirs.Length} directories", "SystemInfo");
                }
                catch (Exception e)
                {
                    LogCustomMessage($"Error accessing {pathInfo.name}: {e.Message}", "SystemInfo");
                }
            }
            else
            {
                LogCustomMessage($"{pathInfo.name} does not exist", "SystemInfo");
            }
        }
        LogCustomMessage("==================", "SystemInfo");
    }

    private void InitializeLogFile()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        string basePath = Path.Combine("/storage/emulated/0/Android/data", Application.identifier, "files");
        if (!Directory.Exists(basePath))
        {
            basePath = Path.Combine("/sdcard/Android/data", Application.identifier, "files");
        }
#else
        string basePath = Application.persistentDataPath;
#endif
        logFilePath = Path.Combine(basePath, "app_log.txt");

        // Create initial log entry
        StringBuilder initialLog = new StringBuilder();
        initialLog.AppendLine($"=== Log Started at {DateTime.Now} ===");
        initialLog.AppendLine($"Application Version: {Application.version}");
        initialLog.AppendLine($"Platform: {Application.platform}");
        initialLog.AppendLine($"Device Model: {SystemInfo.deviceModel}");
        initialLog.AppendLine($"Device OS: {SystemInfo.operatingSystem}");
        initialLog.AppendLine($"Device Name: {SystemInfo.deviceName}");
        initialLog.AppendLine($"Device Type: {SystemInfo.deviceType}");
        initialLog.AppendLine($"Processor Type: {SystemInfo.processorType}");
        initialLog.AppendLine($"Processor Count: {SystemInfo.processorCount}");
        initialLog.AppendLine($"System Memory: {SystemInfo.systemMemorySize} MB");
        initialLog.AppendLine($"Graphics Device: {SystemInfo.graphicsDeviceName}");
        initialLog.AppendLine($"Graphics Memory: {SystemInfo.graphicsMemorySize} MB");
        initialLog.AppendLine($"Graphics API: {SystemInfo.graphicsDeviceType}");
        initialLog.AppendLine($"Base Path: {basePath}");
        initialLog.AppendLine("=====================================\n");

        try
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write initial log entry
            File.WriteAllText(logFilePath, initialLog.ToString());
            Debug.Log($"[LogManager] Initialized log file at: {logFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LogManager] Failed to initialize log file: {e.Message}");
        }
    }

    public void LogDirectoryContents(string path, string category = "FileSystem", bool recursive = false)
    {
        if (string.IsNullOrEmpty(path) || checkedPaths.Contains(path)) return;
        checkedPaths.Add(path);

        try
        {
            if (!Directory.Exists(path))
            {
                LogCustomMessage($"Directory does not exist: {path}", category);
                return;
            }

            LogCustomMessage($"=== Directory Contents: {path} ===", category);
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // Get all files
            var files = Directory.GetFiles(path, "*.*", searchOption);
            LogCustomMessage($"Found {files.Length} files:", category);
            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                LogCustomMessage($"  {fi.Name} ({fi.Length / 1024.0:F2} KB)", category);
            }

            // Get all directories
            var dirs = Directory.GetDirectories(path, "*", searchOption);
            LogCustomMessage($"Found {dirs.Length} directories:", category);
            foreach (var dir in dirs)
            {
                LogCustomMessage($"  {Path.GetFileName(dir)}/", category);
            }
            LogCustomMessage("===========================", category);
        }
        catch (Exception e)
        {
            LogCustomMessage($"Error accessing directory {path}: {e.Message}", category);
        }
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string logEntry = $"[{timestamp}] [{type}] {logString}";
        
        // Track error counts
        if (type == LogType.Error || type == LogType.Exception)
        {
            string errorKey = logString.GetHashCode().ToString();
            if (!errorCounts.ContainsKey(errorKey))
                errorCounts[errorKey] = 0;
            errorCounts[errorKey]++;

            logEntry += $"\nOccurrence: {errorCounts[errorKey]}";
            logEntry += $"\nStack Trace:\n{stackTrace}\n";
        }

        // Add to queue
        logQueue.Enqueue(logEntry);

        // Keep queue size in check
        while (logQueue.Count > MAX_BUFFER_SIZE)
        {
            logQueue.Dequeue();
        }
    }

    private void Update()
    {
        if (Time.time - lastWriteTime >= WRITE_INTERVAL || logQueue.Count >= MAX_BUFFER_SIZE)
        {
            WriteLogsToFile();
        }
    }

    private void WriteLogsToFile()
    {
        if (logQueue.Count == 0) return;

        try
        {
            logBuffer.Clear();
            while (logQueue.Count > 0)
            {
                logBuffer.AppendLine(logQueue.Dequeue());
            }

            File.AppendAllText(logFilePath, logBuffer.ToString());
            lastWriteTime = Time.time;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LogManager] Failed to write to log file: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        WriteLogsToFile();
        Application.logMessageReceived -= HandleLog;
    }

    public void LogCustomMessage(string message, string category = "Custom")
    {
        string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}";
        logQueue.Enqueue(logEntry);
    }

    public string GetLogFilePath()
    {
        return logFilePath;
    }

    public void LogFileDetails(string filePath, string category = "FileDetails")
    {
        try
        {
            if (!File.Exists(filePath))
            {
                LogCustomMessage($"File does not exist: {filePath}", category);
                return;
            }

            var fi = new FileInfo(filePath);
            LogCustomMessage($"=== File Details: {fi.Name} ===", category);
            LogCustomMessage($"Full Path: {fi.FullName}", category);
            LogCustomMessage($"Size: {fi.Length / 1024.0:F2} KB", category);
            LogCustomMessage($"Created: {fi.CreationTime}", category);
            LogCustomMessage($"Last Modified: {fi.LastWriteTime}", category);
            LogCustomMessage($"Last Accessed: {fi.LastAccessTime}", category);
            LogCustomMessage($"Attributes: {fi.Attributes}", category);
            LogCustomMessage("===========================", category);
        }
        catch (Exception e)
        {
            LogCustomMessage($"Error getting file details for {filePath}: {e.Message}", category);
        }
    }
} 