using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

/// <summary>
/// Logs cycling data from Unity (speed, power, cadence, gradient, HR, etc.)
/// Integrates with experimental trial system
/// Saves data in CSV format for analysis and plotting
/// </summary>
public class UnityDataLogger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private WebSocketClient webSocketClient;
    [SerializeField] private ProceduralRouteGenerator routeGenerator;
    
    [Header("Trial Information")]
    [SerializeField] private string participantID = "P001";
    [SerializeField] private int trialNumber = 1;
    [SerializeField] private string sessionDate = "";
    
    [Header("Logging Settings")]
    [SerializeField] private bool isLogging = false;
    [SerializeField] private float loggingInterval = 0.1f; // 10 Hz (every 100ms)
    [SerializeField] private string dataFolder = "CyclingData";
    
    [Header("Current Data")]
    [SerializeField] private float currentSpeed = 0f;
    [SerializeField] private float currentPower = 0f;
    [SerializeField] private float currentCadence = 0f;
    [SerializeField] private float currentHeartRate = 0f;
    [SerializeField] private float currentGradient = 0f;
    [SerializeField] private float currentDistance = 0f;
    [SerializeField] private float currentElevation = 0f;
    
    [Header("Statistics")]
    [SerializeField] private float averageGradient = 0f;
    [SerializeField] private float totalElevationGain = 0f;
    [SerializeField] private float totalElevationLoss = 0f;
    [SerializeField] private int dataPointsCollected = 0;
    
    private List<DataPoint> dataPoints = new List<DataPoint>();
    private float lastLogTime = 0f;
    private float trialStartTime = 0f;
    private float lastElevation = 0f;
    private string currentLogFilePath = "";
    private StreamWriter logWriter = null;

    [System.Serializable]
    public class DataPoint
    {
        public float timestamp;
        public float elapsedTime;
        public float distance;
        public float speed;
        public float power;
        public float cadence;
        public float heartRate;
        public float gradient;
        public float elevation;
    }

    private void Start()
    {
        sessionDate = DateTime.Now.ToString("yyyyMMdd");
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (webSocketClient == null)
            webSocketClient = FindObjectOfType<WebSocketClient>();
        
        if (routeGenerator == null)
            routeGenerator = FindObjectOfType<ProceduralRouteGenerator>();
        
        Debug.Log("[UnityDataLogger] Ready. Press 'L' to start/stop logging");
        Debug.Log("[UnityDataLogger] Press 'S' to show statistics");
    }

    private void Update()
    {
        // Keyboard controls
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (isLogging)
                StopLogging();
            else
                StartLogging();
        }
        
        if (Input.GetKeyDown(KeyCode.S))
        {
            ShowStatistics();
        }
        
        // Collect data if logging
        if (isLogging && Time.time - lastLogTime >= loggingInterval)
        {
            CollectDataPoint();
            lastLogTime = Time.time;
        }
    }

    /// <summary>
    /// Start logging data
    /// </summary>
    [ContextMenu("Start Logging")]
    public void StartLogging()
    {
        if (isLogging)
        {
            Debug.LogWarning("[UnityDataLogger] Already logging!");
            return;
        }
        
        // Create data folder
        string folderPath = Path.Combine(Application.dataPath, "..", dataFolder);
        Directory.CreateDirectory(folderPath);
        
        // Create participant folder
        string participantFolder = Path.Combine(folderPath, participantID);
        Directory.CreateDirectory(participantFolder);
        
        // Create session folder (by date)
        string sessionFolder = Path.Combine(participantFolder, sessionDate);
        Directory.CreateDirectory(sessionFolder);
        
        // Create log file
        string fileName = $"Trial_{trialNumber:D3}_{DateTime.Now:HHmmss}.csv";
        currentLogFilePath = Path.Combine(sessionFolder, fileName);
        
        // Open file for writing
        logWriter = new StreamWriter(currentLogFilePath, false);
        
        // Write header
        logWriter.WriteLine("Timestamp,ElapsedTime,Distance,Speed,Power,Cadence,HeartRate,Gradient,Elevation");
        logWriter.Flush();
        
        // Reset data
        dataPoints.Clear();
        trialStartTime = Time.time;
        lastElevation = GetCurrentElevation();
        totalElevationGain = 0f;
        totalElevationLoss = 0f;
        dataPointsCollected = 0;
        
        isLogging = true;
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"[UnityDataLogger] LOGGING STARTED");
        Debug.Log($"{'='*60}");
        Debug.Log($"Participant: {participantID}");
        Debug.Log($"Trial: {trialNumber}");
        Debug.Log($"Date: {sessionDate}");
        Debug.Log($"File: {fileName}");
        Debug.Log($"{'='*60}\n");
    }

    /// <summary>
    /// Stop logging and save data
    /// </summary>
    [ContextMenu("Stop Logging")]
    public void StopLogging()
    {
        if (!isLogging)
        {
            Debug.LogWarning("[UnityDataLogger] Not currently logging!");
            return;
        }
        
        isLogging = false;
        
        // Close file
        if (logWriter != null)
        {
            logWriter.Close();
            logWriter = null;
        }
        
        // Calculate statistics
        CalculateStatistics();
        
        // Save metadata
        SaveMetadata();
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"[UnityDataLogger] LOGGING STOPPED");
        Debug.Log($"{'='*60}");
        Debug.Log($"Data points collected: {dataPointsCollected}");
        Debug.Log($"Duration: {Time.time - trialStartTime:F1}s");
        Debug.Log($"File saved: {currentLogFilePath}");
        Debug.Log($"{'='*60}\n");
        
        ShowStatistics();
    }

    /// <summary>
    /// Collect a single data point
    /// </summary>
    private void CollectDataPoint()
    {
        // Get current values
        currentDistance = bikeController != null ? bikeController.GetDistance() : 0f;
        currentSpeed = bikeController != null ? bikeController.GetSpeed() : 0f;
        currentPower = bikeController != null ? bikeController.GetPower() : 0f;
        currentCadence = bikeController != null ? bikeController.GetCadence() : 0f;
        currentGradient = bikeController != null ? bikeController.GetGradient() : 0f;
        currentHeartRate = webSocketClient != null ? webSocketClient.GetHeartRateBpm() : 0f;
        currentElevation = GetCurrentElevation();
        
        // Track elevation changes
        float elevationChange = currentElevation - lastElevation;
        if (elevationChange > 0)
            totalElevationGain += elevationChange;
        else if (elevationChange < 0)
            totalElevationLoss += Mathf.Abs(elevationChange);
        lastElevation = currentElevation;
        
        // Create data point
        DataPoint point = new DataPoint
        {
            timestamp = Time.time,
            elapsedTime = Time.time - trialStartTime,
            distance = currentDistance,
            speed = currentSpeed,
            power = currentPower,
            cadence = currentCadence,
            heartRate = currentHeartRate,
            gradient = currentGradient,
            elevation = currentElevation
        };
        
        dataPoints.Add(point);
        dataPointsCollected++;
        
        // Write to file
        if (logWriter != null)
        {
            logWriter.WriteLine($"{point.timestamp:F3},{point.elapsedTime:F3},{point.distance:F2},{point.speed:F2},{point.power:F1},{point.cadence:F1},{point.heartRate:F0},{point.gradient:F2},{point.elevation:F2}");
            
            // Flush every 10 points to ensure data is saved
            if (dataPointsCollected % 10 == 0)
            {
                logWriter.Flush();
            }
        }
    }

    /// <summary>
    /// Get current elevation from bike position
    /// </summary>
    private float GetCurrentElevation()
    {
        if (bikeController == null) return 0f;
        
        return bikeController.transform.position.y;
    }

    /// <summary>
    /// Calculate trial statistics
    /// </summary>
    private void CalculateStatistics()
    {
        if (dataPoints.Count == 0) return;
        
        // Calculate average gradient
        float totalGradient = 0f;
        int gradientCount = 0;
        
        foreach (var point in dataPoints)
        {
            if (point.speed > 0.1f) // Only count when moving
            {
                totalGradient += point.gradient;
                gradientCount++;
            }
        }
        
        averageGradient = gradientCount > 0 ? totalGradient / gradientCount : 0f;
        
        // Alternative: Calculate from elevation change
        if (dataPoints.Count > 1)
        {
            float startElevation = dataPoints[0].elevation;
            float endElevation = dataPoints[dataPoints.Count - 1].elevation;
            float totalDistance = dataPoints[dataPoints.Count - 1].distance - dataPoints[0].distance;
            
            if (totalDistance > 0)
            {
                float elevationChange = endElevation - startElevation;
                float calculatedGradient = (elevationChange / totalDistance) * 100f;
                
                Debug.Log($"[Statistics] Average gradient (measured): {averageGradient:F2}%");
                Debug.Log($"[Statistics] Average gradient (calculated): {calculatedGradient:F2}%");
            }
        }
    }

    /// <summary>
    /// Save trial metadata
    /// </summary>
    private void SaveMetadata()
    {
        if (string.IsNullOrEmpty(currentLogFilePath)) return;
        
        string metadataPath = currentLogFilePath.Replace(".csv", "_metadata.txt");
        
        using (StreamWriter writer = new StreamWriter(metadataPath, false))
        {
            writer.WriteLine("TRIAL METADATA");
            writer.WriteLine($"{'='*60}");
            writer.WriteLine($"Participant ID: {participantID}");
            writer.WriteLine($"Trial Number: {trialNumber}");
            writer.WriteLine($"Date: {sessionDate}");
            writer.WriteLine($"Start Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine();
            
            writer.WriteLine("ROUTE INFORMATION");
            writer.WriteLine($"{'='*60}");
            if (routeGenerator != null)
            {
                writer.WriteLine($"Target Gradient: {routeGenerator.GetCurrentGradient():F2}%");
                writer.WriteLine($"Actual Gradient (terrain): {routeGenerator.GetActualGradient():F2}%");
                writer.WriteLine($"Route Length: {routeGenerator.GetRouteLength():F1}m");
            }
            writer.WriteLine($"Average Gradient (measured): {averageGradient:F2}%");
            writer.WriteLine($"Total Elevation Gain: {totalElevationGain:F1}m");
            writer.WriteLine($"Total Elevation Loss: {totalElevationLoss:F1}m");
            writer.WriteLine();
            
            writer.WriteLine("TRIAL STATISTICS");
            writer.WriteLine($"{'='*60}");
            writer.WriteLine($"Duration: {Time.time - trialStartTime:F1}s");
            writer.WriteLine($"Data Points: {dataPointsCollected}");
            writer.WriteLine($"Sampling Rate: {1f / loggingInterval:F1} Hz");
            
            if (dataPoints.Count > 0)
            {
                float avgSpeed = 0f, avgPower = 0f, avgCadence = 0f, avgHR = 0f;
                float maxSpeed = 0f, maxPower = 0f, maxCadence = 0f, maxHR = 0f;
                int movingCount = 0;
                
                foreach (var point in dataPoints)
                {
                    if (point.speed > 0.1f)
                    {
                        avgSpeed += point.speed;
                        avgPower += point.power;
                        avgCadence += point.cadence;
                        avgHR += point.heartRate;
                        movingCount++;
                        
                        if (point.speed > maxSpeed) maxSpeed = point.speed;
                        if (point.power > maxPower) maxPower = point.power;
                        if (point.cadence > maxCadence) maxCadence = point.cadence;
                        if (point.heartRate > maxHR) maxHR = point.heartRate;
                    }
                }
                
                if (movingCount > 0)
                {
                    avgSpeed /= movingCount;
                    avgPower /= movingCount;
                    avgCadence /= movingCount;
                    avgHR /= movingCount;
                }
                
                writer.WriteLine($"Average Speed: {avgSpeed:F2} m/s ({avgSpeed * 3.6f:F1} km/h)");
                writer.WriteLine($"Average Power: {avgPower:F1} W");
                writer.WriteLine($"Average Cadence: {avgCadence:F1} RPM");
                writer.WriteLine($"Average Heart Rate: {avgHR:F0} BPM");
                writer.WriteLine();
                writer.WriteLine($"Max Speed: {maxSpeed:F2} m/s ({maxSpeed * 3.6f:F1} km/h)");
                writer.WriteLine($"Max Power: {maxPower:F1} W");
                writer.WriteLine($"Max Cadence: {maxCadence:F1} RPM");
                writer.WriteLine($"Max Heart Rate: {maxHR:F0} BPM");
            }
        }
        
        Debug.Log($"[UnityDataLogger] Metadata saved: {metadataPath}");
    }

    /// <summary>
    /// Show current statistics
    /// </summary>
    [ContextMenu("Show Statistics")]
    public void ShowStatistics()
    {
        Debug.Log($"\n{'='*60}");
        Debug.Log($"CURRENT TRIAL STATISTICS");
        Debug.Log($"{'='*60}");
        Debug.Log($"Participant: {participantID} | Trial: {trialNumber}");
        Debug.Log($"Logging: {(isLogging ? "ACTIVE" : "STOPPED")}");
        Debug.Log($"Data Points: {dataPointsCollected}");
        
        if (routeGenerator != null)
        {
            Debug.Log($"\nRoute:");
            Debug.Log($"  Target Gradient: {routeGenerator.GetCurrentGradient():F2}%");
            Debug.Log($"  Actual Gradient: {routeGenerator.GetActualGradient():F2}%");
            Debug.Log($"  Measured Gradient: {averageGradient:F2}%");
        }
        
        Debug.Log($"\nElevation:");
        Debug.Log($"  Total Gain: {totalElevationGain:F1}m");
        Debug.Log($"  Total Loss: {totalElevationLoss:F1}m");
        Debug.Log($"  Net Change: {totalElevationGain - totalElevationLoss:F1}m");
        
        Debug.Log($"\nCurrent Values:");
        Debug.Log($"  Speed: {currentSpeed:F2} m/s ({currentSpeed * 3.6f:F1} km/h)");
        Debug.Log($"  Power: {currentPower:F1} W");
        Debug.Log($"  Cadence: {currentCadence:F1} RPM");
        Debug.Log($"  Heart Rate: {currentHeartRate:F0} BPM");
        Debug.Log($"  Gradient: {currentGradient:F2}%");
        Debug.Log($"{'='*60}\n");
    }

    /// <summary>
    /// Set trial information
    /// </summary>
    public void SetTrialInfo(string participantId, int trial)
    {
        participantID = participantId;
        trialNumber = trial;
        sessionDate = DateTime.Now.ToString("yyyyMMdd");
        
        Debug.Log($"[UnityDataLogger] Trial info set: {participantID}, Trial {trialNumber}");
    }

    /// <summary>
    /// Get current log file path
    /// </summary>
    public string GetLogFilePath()
    {
        return currentLogFilePath;
    }

    /// <summary>
    /// Check if currently logging
    /// </summary>
    public bool IsLogging()
    {
        return isLogging;
    }

    private void OnApplicationQuit()
    {
        if (isLogging)
        {
            StopLogging();
        }
    }
}
