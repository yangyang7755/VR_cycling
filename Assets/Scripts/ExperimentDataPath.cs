using UnityEngine;
using System.IO;

/// <summary>
/// Central data path for all experiment output files.
/// 
/// Structure:
///   ~/Desktop/ExperimentData/
///   ├── P001/
///   │   ├── Block_1/
///   │   │   ├── hill_experiment_P001_B1_20260626_143020.csv
///   │   │   └── event_markers_P001_B1.csv
///   │   ├── Block_2/
///   │   ├── Block_3/
///   │   ├── Block_4/
///   │   ├── participant_earnings.csv
///   │   └── post_block_questionnaire.csv
///   ├── P002/
///   │   └── ...
///   └── session_log.csv  (cumulative across all participants)
/// </summary>
public static class ExperimentDataPath
{
    private static string _basePath = null;
    private static string _participantPath = null;
    private static string _blockPath = null;
    private static string _currentParticipant = null;
    private static int _currentBlock = 0;

    /// <summary>
    /// Returns the base experiment data folder (~/Desktop/ExperimentData/).
    /// </summary>
    public static string GetBasePath()
    {
        if (_basePath == null)
        {
            string desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            _basePath = Path.Combine(desktop, "ExperimentData");
            
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                Debug.Log($"[ExperimentDataPath] Created base: {_basePath}");
            }
        }
        return _basePath;
    }

    /// <summary>
    /// Set the current participant and block. Call this at the start of each block.
    /// Creates: ~/Desktop/ExperimentData/P001/Block_1/
    /// </summary>
    public static void SetSession(string participantID, int blockNumber)
    {
        _currentParticipant = participantID;
        _currentBlock = blockNumber;

        _participantPath = Path.Combine(GetBasePath(), participantID);
        if (!Directory.Exists(_participantPath))
            Directory.CreateDirectory(_participantPath);

        _blockPath = Path.Combine(_participantPath, $"Block_{blockNumber}");
        if (!Directory.Exists(_blockPath))
            Directory.CreateDirectory(_blockPath);

        Debug.Log($"[ExperimentDataPath] Session: {_blockPath}");
    }

    /// <summary>
    /// Returns the current block folder path (for trial-level data).
    /// e.g. ~/Desktop/ExperimentData/P001/Block_1/
    /// </summary>
    public static string GetPath()
    {
        if (_blockPath != null)
            return _blockPath;
        
        // Fallback if SetSession hasn't been called yet
        return GetBasePath();
    }

    /// <summary>
    /// Returns the participant folder (for per-participant cumulative files).
    /// e.g. ~/Desktop/ExperimentData/P001/
    /// </summary>
    public static string GetParticipantPath()
    {
        if (_participantPath != null)
            return _participantPath;
        
        return GetBasePath();
    }

    /// <summary>
    /// Returns the base path (for session-level cumulative files).
    /// </summary>
    public static string GetRootPath()
    {
        return GetBasePath();
    }
}
