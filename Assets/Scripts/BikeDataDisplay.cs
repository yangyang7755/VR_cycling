using UnityEngine;
using TMPro;

/// <summary>
/// Displays bike trainer data (speed, power, cadence, distance) from WebSocket
/// Can be used as standalone display or integrated with BikeController
/// </summary>
public class BikeDataDisplay : MonoBehaviour
{
    [Header("WebSocket Client")]
    [Tooltip("WebSocket client to receive bike data from")]
    [SerializeField] private WebSocketClient webSocketClient;

    [Header("Display Mode")]
    [Tooltip("Display mode: 2D UI or 3D World Space")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.WorldSpace3D;
    
    public enum DisplayMode
    {
        ScreenSpace2D,
        WorldSpace3D
    }

    [Header("UI Text Elements (for 2D mode)")]
    [Tooltip("Text to display speed")]
    [SerializeField] private TextMeshProUGUI speedText;
    
    [Tooltip("Text to display power")]
    [SerializeField] private TextMeshProUGUI powerText;
    
    [Tooltip("Text to display cadence")]
    [SerializeField] private TextMeshProUGUI cadenceText;
    
    [Tooltip("Text to display distance (optional)")]
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("3D Text Elements (for World Space mode)")]
    [Tooltip("3D TextMeshPro for speed")]
    [SerializeField] private TextMeshPro speedText3D;
    
    [Tooltip("3D TextMeshPro for power")]
    [SerializeField] private TextMeshPro powerText3D;
    
    [Tooltip("3D TextMeshPro for cadence")]
    [SerializeField] private TextMeshPro cadenceText3D;
    
    [Tooltip("3D TextMeshPro for distance (optional)")]
    [SerializeField] private TextMeshPro distanceText3D;

    [Header("Display Options")]
    [Tooltip("Show speed")]
    [SerializeField] private bool showSpeed = true;
    
    [Tooltip("Show power")]
    [SerializeField] private bool showPower = true;
    
    [Tooltip("Show cadence")]
    [SerializeField] private bool showCadence = true;
    
    [Tooltip("Show distance (calculated from speed)")]
    [SerializeField] private bool showDistance = true;

    [Header("Distance Tracking")]
    [Tooltip("Calculate distance from speed (meters)")]
    [SerializeField] private bool calculateDistance = true;
    
    [Tooltip("Reset distance when speed is zero for more than this duration (seconds)")]
    [SerializeField] private float resetDistanceAfterIdle = 5f;

    [Header("Update Settings")]
    [Tooltip("Update frequency (times per second)")]
    [SerializeField] private float updateRate = 10f;

    private float currentSpeed = 0f;
    private float currentPower = 0f;
    private float currentCadence = 0f;
    private float totalDistance = 0f; // meters
    private float lastUpdateTime;
    private float updateInterval;
    private float lastSpeedTime;
    private float idleTime = 0f;

    private void Start()
    {
        // Auto-find WebSocket client if not assigned
        if (webSocketClient == null)
        {
            webSocketClient = FindObjectOfType<WebSocketClient>();
            if (webSocketClient == null)
            {
                Debug.LogWarning("[BikeDataDisplay] WebSocketClient not found! Please assign it in Inspector.");
            }
        }

        // Subscribe to WebSocket events
        if (webSocketClient != null)
        {
            webSocketClient.OnTelemetryReceived += OnTelemetryReceived;
            webSocketClient.OnConnected += OnWebSocketConnected;
            webSocketClient.OnDisconnected += OnWebSocketDisconnected;
        }

        updateInterval = 1f / updateRate;
        lastUpdateTime = Time.time;
        lastSpeedTime = Time.time;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (webSocketClient != null)
        {
            webSocketClient.OnTelemetryReceived -= OnTelemetryReceived;
            webSocketClient.OnConnected -= OnWebSocketConnected;
            webSocketClient.OnDisconnected -= OnWebSocketDisconnected;
        }
    }

    private void Update()
    {
        // Update display at specified rate
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDisplay();
            UpdateDistance();
            lastUpdateTime = Time.time;
        }
    }

    private void OnTelemetryReceived(WebSocketClient.TelemetryData data)
    {
        // Update current values from WebSocket data
        if (data.speed_kmh.HasValue)
        {
            currentSpeed = data.speed_kmh.Value;
            lastSpeedTime = Time.time;
        }
        
        if (data.power_watts.HasValue)
        {
            currentPower = data.power_watts.Value;
        }
        
        if (data.cadence_rpm.HasValue)
        {
            currentCadence = data.cadence_rpm.Value;
        }
    }

    private void OnWebSocketConnected()
    {
        Debug.Log("[BikeDataDisplay] WebSocket connected - Bike data will be displayed");
    }

    private void OnWebSocketDisconnected()
    {
        Debug.LogWarning("[BikeDataDisplay] WebSocket disconnected - Bike data unavailable");
    }

    private void UpdateDistance()
    {
        if (!calculateDistance || !showDistance) return;

        // Calculate distance from speed
        float deltaTime = Time.deltaTime;
        float speedMs = currentSpeed / 3.6f; // Convert km/h to m/s
        totalDistance += speedMs * deltaTime;

        // Reset distance if idle for too long
        if (currentSpeed < 0.1f)
        {
            idleTime += deltaTime;
            if (idleTime >= resetDistanceAfterIdle)
            {
                totalDistance = 0f;
            }
        }
        else
        {
            idleTime = 0f;
        }
    }

    private void UpdateDisplay()
    {
        // Get latest values from WebSocket client
        if (webSocketClient != null && webSocketClient.IsConnected())
        {
            currentSpeed = webSocketClient.GetSpeedKmh();
            currentPower = webSocketClient.GetPowerWatts();
            currentCadence = webSocketClient.GetCadenceRpm();
        }

        if (displayMode == DisplayMode.WorldSpace3D)
        {
            Update3DDisplay();
        }
        else
        {
            Update2DDisplay();
        }
    }

    private void Update2DDisplay()
    {
        if (showSpeed && speedText != null)
        {
            speedText.text = currentSpeed > 0.1f ? $"Speed: {currentSpeed:F1} km/h" : "Speed: 0.0 km/h";
        }

        if (showPower && powerText != null)
        {
            powerText.text = currentPower > 0.1f ? $"Power: {currentPower:F0} W" : "Power: 0 W";
        }

        if (showCadence && cadenceText != null)
        {
            cadenceText.text = currentCadence > 0.1f ? $"Cadence: {currentCadence:F0} RPM" : "Cadence: 0 RPM";
        }

        if (showDistance && distanceText != null)
        {
            if (totalDistance >= 1000f)
            {
                distanceText.text = $"Distance: {totalDistance / 1000f:F2} km";
            }
            else
            {
                distanceText.text = $"Distance: {totalDistance:F0} m";
            }
        }
    }

    private void Update3DDisplay()
    {
        if (showSpeed && speedText3D != null)
        {
            speedText3D.text = currentSpeed > 0.1f ? $"Speed: {currentSpeed:F1} km/h" : "Speed: 0.0 km/h";
        }

        if (showPower && powerText3D != null)
        {
            powerText3D.text = currentPower > 0.1f ? $"Power: {currentPower:F0} W" : "Power: 0 W";
        }

        if (showCadence && cadenceText3D != null)
        {
            cadenceText3D.text = currentCadence > 0.1f ? $"Cadence: {currentCadence:F0} RPM" : "Cadence: 0 RPM";
        }

        if (showDistance && distanceText3D != null)
        {
            if (totalDistance >= 1000f)
            {
                distanceText3D.text = $"Distance: {totalDistance / 1000f:F2} km";
            }
            else
            {
                distanceText3D.text = $"Distance: {totalDistance:F0} m";
            }
        }
    }

    /// <summary>
    /// Reset distance counter
    /// </summary>
    public void ResetDistance()
    {
        totalDistance = 0f;
        idleTime = 0f;
    }

    /// <summary>
    /// Get current distance in meters
    /// </summary>
    public float GetDistance()
    {
        return totalDistance;
    }

    /// <summary>
    /// Get current speed in km/h
    /// </summary>
    public float GetSpeed()
    {
        return currentSpeed;
    }

    /// <summary>
    /// Get current power in watts
    /// </summary>
    public float GetPower()
    {
        return currentPower;
    }

    /// <summary>
    /// Get current cadence in RPM
    /// </summary>
    public float GetCadence()
    {
        return currentCadence;
    }
}