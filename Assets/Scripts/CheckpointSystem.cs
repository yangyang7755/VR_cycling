using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

/// <summary>
/// Manages checkpoints along the route and emits timestamped markers
/// Handles checkpoint triggers, visual indicators, and event logging
/// </summary>
public class CheckpointSystem : MonoBehaviour
{
    [Header("Route Configuration")]
    [Tooltip("Route configuration asset")]
    [SerializeField] private RouteConfiguration routeConfig;

    [Tooltip("Bike controller to track distance")]
    [SerializeField] private BikeController bikeController;

    [Header("Checkpoint Visualization")]
    [Tooltip("Checkpoint prefab (will be instantiated at each checkpoint location)")]
    [SerializeField] private GameObject checkpointPrefab;

    [Tooltip("Parent object for checkpoints")]
    [SerializeField] private Transform checkpointsParent;

    [Tooltip("Show checkpoint markers in world")]
    [SerializeField] private bool showCheckpointMarkers = true;

    [Header("Checkpoint UI")]
    [Tooltip("UI panel to display checkpoint information")]
    [SerializeField] private GameObject checkpointUIPanel;

    [Tooltip("Text to display checkpoint name")]
    [SerializeField] private TextMeshProUGUI checkpointNameText;

    [Tooltip("Text to display checkpoint distance")]
    [SerializeField] private TextMeshProUGUI checkpointDistanceText;

    [Tooltip("Text to display checkpoint timestamp")]
    [SerializeField] private TextMeshProUGUI checkpointTimestampText;

    [Tooltip("Display duration for checkpoint UI (seconds)")]
    [Range(2f, 10f)]
    [SerializeField] private float uiDisplayDuration = 5f;

    [Header("Event Logging")]
    [Tooltip("Log checkpoint events to console")]
    [SerializeField] private bool logToConsole = true;

    [Tooltip("Log checkpoint events to file")]
    [SerializeField] private bool logToFile = false;

    [Tooltip("File path for checkpoint log (relative to Application.persistentDataPath)")]
    [SerializeField] private string logFilePath = "checkpoint_log.txt";

    private List<CheckpointData> checkpoints = new List<CheckpointData>();
    private List<CheckpointMarker> checkpointMarkers = new List<CheckpointMarker>();
    private int nextCheckpointIndex = 0;
    private float routeStartTime = 0f;
    private bool isActive = false;

    private void Start()
    {
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        if (routeConfig == null)
        {
            Debug.LogWarning("[CheckpointSystem] Route configuration not assigned. Will try to find from LongRouteTerrainBuilder.");
            LongRouteTerrainBuilder routeBuilder = FindObjectOfType<LongRouteTerrainBuilder>();
            if (routeBuilder != null)
            {
                routeConfig = routeBuilder.GetRouteConfiguration();
            }
        }

        if (routeConfig != null)
        {
            InitializeCheckpoints();
        }

        // Hide UI initially
        if (checkpointUIPanel != null)
        {
            checkpointUIPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isActive || bikeController == null || checkpoints.Count == 0) return;

        float currentDistance = bikeController.GetDistance();

        // Check if reached next checkpoint
        while (nextCheckpointIndex < checkpoints.Count)
        {
            CheckpointData checkpoint = checkpoints[nextCheckpointIndex];
            
            if (currentDistance >= checkpoint.location)
            {
                TriggerCheckpoint(checkpoint);
                nextCheckpointIndex++;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Initialize checkpoints from route configuration
    /// </summary>
    public void InitializeCheckpoints()
    {
        if (routeConfig == null)
        {
            Debug.LogError("[CheckpointSystem] Cannot initialize: route configuration is null");
            return;
        }

        // Get all checkpoints from route config
        checkpoints = routeConfig.GetAllCheckpoints();

        // Create visual markers if enabled
        if (showCheckpointMarkers && checkpointPrefab != null)
        {
            CreateCheckpointMarkers();
        }

        nextCheckpointIndex = 0;
        isActive = true;
        routeStartTime = Time.time;

        Debug.Log($"[CheckpointSystem] Initialized {checkpoints.Count} checkpoints for route: {routeConfig.routeName}");
    }

    /// <summary>
    /// Create visual markers for checkpoints in the world
    /// </summary>
    private void CreateCheckpointMarkers()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogWarning("[CheckpointSystem] Terrain not found. Checkpoints will be placed at Y=0.");
        }

        // Create parent if needed
        if (checkpointsParent == null)
        {
            GameObject parentObj = new GameObject("CheckpointMarkers");
            checkpointsParent = parentObj.transform;
        }

        foreach (CheckpointData checkpoint in checkpoints)
        {
            // Calculate world position
            Vector3 worldPos = GetWorldPositionAtDistance(checkpoint.location);

            // Instantiate checkpoint marker
            GameObject marker = Instantiate(checkpointPrefab, worldPos, Quaternion.identity, checkpointsParent);
            marker.name = $"Checkpoint_{checkpoint.name}";

            // Store marker reference
            CheckpointMarker markerComponent = marker.GetComponent<CheckpointMarker>();
            if (markerComponent == null)
            {
                markerComponent = marker.AddComponent<CheckpointMarker>();
            }

            markerComponent.Initialize(checkpoint);
            checkpointMarkers.Add(markerComponent);
        }
    }

    /// <summary>
    /// Get world position at a given distance along route
    /// </summary>
    private Vector3 GetWorldPositionAtDistance(float distance)
    {
        float roadCenterZ = 0f;
        float roadWidth = 15f;

        // Try to get road info from terrain builder
        SimpleTerrainBuilder terrainBuilder = FindObjectOfType<SimpleTerrainBuilder>();
        if (terrainBuilder != null)
        {
            roadCenterZ = terrainBuilder.GetRoadCenterZ();
            roadWidth = terrainBuilder.GetRoadWidth();
        }
        else if (terrain != null)
        {
            roadCenterZ = terrain.terrainData.size.z / 2f;
        }

        Vector3 position = new Vector3(distance, 0f, roadCenterZ);

        // Get terrain height
        if (terrain != null)
        {
            float height = terrain.SampleHeight(position);
            position.y = height + 0.5f; // Slightly above ground
        }

        return position;
    }

    /// <summary>
    /// Trigger a checkpoint (called when cyclist reaches it)
    /// </summary>
    private void TriggerCheckpoint(CheckpointData checkpoint)
    {
        if (checkpoint.isReached) return;

        checkpoint.isReached = true;
        checkpoint.timestamp = Time.time - routeStartTime;

        // Log checkpoint event
        LogCheckpointEvent(checkpoint);

        // Show UI notification
        ShowCheckpointUI(checkpoint);

        // Update visual marker
        CheckpointMarker marker = checkpointMarkers.FirstOrDefault(m => m.checkpointData == checkpoint);
        if (marker != null)
        {
            marker.OnReached();
        }

        // Emit timestamped marker event
        OnCheckpointReached?.Invoke(checkpoint);
    }

    /// <summary>
    /// Show checkpoint UI notification
    /// </summary>
    private void ShowCheckpointUI(CheckpointData checkpoint)
    {
        if (checkpointUIPanel == null) return;

        checkpointUIPanel.SetActive(true);

        if (checkpointNameText != null)
        {
            checkpointNameText.text = checkpoint.name;
        }

        if (checkpointDistanceText != null)
        {
            checkpointDistanceText.text = $"Distance: {checkpoint.location:F0}m";
        }

        if (checkpointTimestampText != null)
        {
            float minutes = checkpoint.timestamp / 60f;
            float seconds = checkpoint.timestamp % 60f;
            checkpointTimestampText.text = $"Time: {minutes:F0}:{seconds:00.0}";
        }

        // Hide UI after duration
        CancelInvoke(nameof(HideCheckpointUI));
        Invoke(nameof(HideCheckpointUI), uiDisplayDuration);
    }

    /// <summary>
    /// Hide checkpoint UI
    /// </summary>
    private void HideCheckpointUI()
    {
        if (checkpointUIPanel != null)
        {
            checkpointUIPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Log checkpoint event
    /// </summary>
    private void LogCheckpointEvent(CheckpointData checkpoint)
    {
        string logMessage = $"[{checkpoint.timestamp:F2}s] Checkpoint: {checkpoint.name} at {checkpoint.location:F0}m (Type: {checkpoint.type})";

        if (logToConsole)
        {
            Debug.Log($"[CheckpointSystem] {logMessage}");
        }

        if (logToFile)
        {
            string fullPath = System.IO.Path.Combine(Application.persistentDataPath, logFilePath);
            System.IO.File.AppendAllText(fullPath, logMessage + System.Environment.NewLine);
        }
    }

    /// <summary>
    /// Get all reached checkpoints with timestamps
    /// </summary>
    public List<CheckpointData> GetReachedCheckpoints()
    {
        return checkpoints.Where(c => c.isReached).ToList();
    }

    /// <summary>
    /// Get checkpoint data by location (within tolerance)
    /// </summary>
    public CheckpointData GetCheckpointAtDistance(float distance, float tolerance = 5f)
    {
        return checkpoints.FirstOrDefault(c => Mathf.Abs(c.location - distance) <= tolerance);
    }

    /// <summary>
    /// Reset checkpoints (for new route or restart)
    /// </summary>
    public void ResetCheckpoints()
    {
        foreach (CheckpointData checkpoint in checkpoints)
        {
            checkpoint.isReached = false;
            checkpoint.timestamp = 0f;
        }

        nextCheckpointIndex = 0;
        routeStartTime = Time.time;

        // Reset visual markers
        foreach (CheckpointMarker marker in checkpointMarkers)
        {
            if (marker != null)
            {
                marker.OnReset();
            }
        }
    }

    // Event delegate for checkpoint reached
    public System.Action<CheckpointData> OnCheckpointReached;

    // Helper reference
    [SerializeField] private Terrain terrain;
}

/// <summary>
/// Component attached to checkpoint marker GameObjects
/// </summary>
public class CheckpointMarker : MonoBehaviour
{
    public CheckpointData checkpointData;
    
    private bool isReached = false;
    private Renderer markerRenderer;

    private void Start()
    {
        markerRenderer = GetComponent<Renderer>();
    }

    public void Initialize(CheckpointData data)
    {
        checkpointData = data;
    }

    public void OnReached()
    {
        isReached = true;
        // Visual feedback (e.g., change color, play animation)
        if (markerRenderer != null)
        {
            markerRenderer.material.color = Color.green;
        }
    }

    public void OnReset()
    {
        isReached = false;
        if (markerRenderer != null)
        {
            markerRenderer.material.color = Color.yellow;
        }
    }
}
