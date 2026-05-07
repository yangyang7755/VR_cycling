using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Heart Rate display for VR environment
/// Shows real-time heart rate data from WebSocket connection
/// Can be positioned in 3D space for VR visualization
/// </summary>
public class HeartRateDisplay : MonoBehaviour
{
    [Header("WebSocket Client")]
    [Tooltip("WebSocket client to receive HR data from")]
    [SerializeField] private WebSocketClient webSocketClient;

    [Header("Display Mode")]
    [Tooltip("Display mode: 2D UI or 3D World Space")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.WorldSpace3D;
    
    public enum DisplayMode
    {
        ScreenSpace2D,   // 2D UI overlay on screen
        WorldSpace3D     // 3D text in world space (for VR)
    }

    [Header("UI Elements (for 2D mode)")]
    [Tooltip("Text component to display heart rate")]
    [SerializeField] private TextMeshProUGUI heartRateText;
    
    [Tooltip("Text component to display HR status/zone")]
    [SerializeField] private TextMeshProUGUI heartRateZoneText;

    [Header("3D Text (for World Space mode)")]
    [Tooltip("3D TextMeshPro component for world space display")]
    [SerializeField] private TextMeshPro heartRateText3D;
    
    [Tooltip("3D TextMeshPro component for zone display")]
    [SerializeField] private TextMeshPro heartRateZoneText3D;

    [Header("Visual Settings")]
    [Tooltip("Show heart rate zone (Resting, Fat Burn, Cardio, Peak)")]
    [SerializeField] private bool showHeartRateZone = true;
    
    [Tooltip("Color for resting zone (60-100 BPM)")]
    [SerializeField] private Color restingZoneColor = Color.blue;
    
    [Tooltip("Color for fat burn zone (100-140 BPM)")]
    [SerializeField] private Color fatBurnZoneColor = Color.green;
    
    [Tooltip("Color for cardio zone (140-170 BPM)")]
    [SerializeField] private Color cardioZoneColor = Color.yellow;
    
    [Tooltip("Color for peak zone (170+ BPM)")]
    [SerializeField] private Color peakZoneColor = Color.red;

    [Header("Animation")]
    [Tooltip("Pulse animation when HR updates")]
    [SerializeField] private bool enablePulseAnimation = true;
    
    [Tooltip("Pulse scale multiplier")]
    [SerializeField] private float pulseScale = 1.2f;
    
    [Tooltip("Pulse duration in seconds")]
    [SerializeField] private float pulseDuration = 0.3f;

    [Header("Update Settings")]
    [Tooltip("Update frequency (times per second)")]
    [SerializeField] private float updateRate = 10f;

    private float currentHeartRate = 0f;
    private float lastHeartRate = 0f;
    private float updateInterval;
    private float lastUpdateTime;
    private Vector3 originalScale;
    private float pulseTimer = 0f;

    private void Start()
    {
        // Auto-find WebSocket client if not assigned
        if (webSocketClient == null)
        {
            webSocketClient = FindObjectOfType<WebSocketClient>();
            if (webSocketClient == null)
            {
                Debug.LogWarning("[HeartRateDisplay] WebSocketClient not found! Please assign it in Inspector or add WebSocketClient component to scene.");
            }
        }

        // Subscribe to WebSocket events
        if (webSocketClient != null)
        {
            webSocketClient.OnTelemetryReceived += OnTelemetryReceived;
            webSocketClient.OnConnected += OnWebSocketConnected;
            webSocketClient.OnDisconnected += OnWebSocketDisconnected;
        }

        // Store original scale for pulse animation
        if (heartRateText3D != null)
        {
            originalScale = heartRateText3D.transform.localScale;
        }
        else if (heartRateText != null)
        {
            originalScale = heartRateText.transform.localScale;
        }

        // Initialize display
        UpdateDisplay();

        updateInterval = 1f / updateRate;
        lastUpdateTime = Time.time;
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
            lastUpdateTime = Time.time;
        }

        // Handle pulse animation
        if (enablePulseAnimation && pulseTimer > 0f)
        {
            pulseTimer -= Time.deltaTime;
            float pulseProgress = 1f - (pulseTimer / pulseDuration);
            float scale = Mathf.Lerp(pulseScale, 1f, pulseProgress);
            
            if (displayMode == DisplayMode.WorldSpace3D && heartRateText3D != null)
            {
                heartRateText3D.transform.localScale = originalScale * scale;
            }
            else if (displayMode == DisplayMode.ScreenSpace2D && heartRateText != null)
            {
                heartRateText.transform.localScale = originalScale * scale;
            }
        }
    }

    private void OnTelemetryReceived(WebSocketClient.TelemetryData data)
    {
        if (data.heart_rate_bpm.HasValue)
        {
            lastHeartRate = currentHeartRate;
            currentHeartRate = data.heart_rate_bpm.Value;
            
            // Trigger pulse animation if HR changed significantly
            if (Mathf.Abs(currentHeartRate - lastHeartRate) > 1f && enablePulseAnimation)
            {
                pulseTimer = pulseDuration;
            }
        }
    }

    private void OnWebSocketConnected()
    {
        Debug.Log("[HeartRateDisplay] WebSocket connected - HR data will be displayed");
    }

    private void OnWebSocketDisconnected()
    {
        Debug.LogWarning("[HeartRateDisplay] WebSocket disconnected - HR data unavailable");
        // Optionally show "No Data" or gray out the display
    }

    private void UpdateDisplay()
    {
        // Get current HR from WebSocket client
        if (webSocketClient != null && webSocketClient.IsConnected())
        {
            currentHeartRate = webSocketClient.GetHeartRateBpm();
        }

        // Update text based on display mode
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
        if (heartRateText != null)
        {
            if (currentHeartRate > 0f)
            {
                heartRateText.text = $"HR: {currentHeartRate:F0} BPM";
                heartRateText.color = GetHeartRateZoneColor(currentHeartRate);
            }
            else
            {
                heartRateText.text = "HR: -- BPM";
                heartRateText.color = Color.gray;
            }
        }

        if (heartRateZoneText != null && showHeartRateZone)
        {
            if (currentHeartRate > 0f)
            {
                heartRateZoneText.text = GetHeartRateZone(currentHeartRate);
                heartRateZoneText.color = GetHeartRateZoneColor(currentHeartRate);
            }
            else
            {
                heartRateZoneText.text = "No Data";
                heartRateZoneText.color = Color.gray;
            }
        }
    }

    private void Update3DDisplay()
    {
        if (heartRateText3D != null)
        {
            if (currentHeartRate > 0f)
            {
                heartRateText3D.text = $"HR: {currentHeartRate:F0} BPM";
                heartRateText3D.color = GetHeartRateZoneColor(currentHeartRate);
            }
            else
            {
                heartRateText3D.text = "HR: -- BPM";
                heartRateText3D.color = Color.gray;
            }
        }

        if (heartRateZoneText3D != null && showHeartRateZone)
        {
            if (currentHeartRate > 0f)
            {
                heartRateZoneText3D.text = GetHeartRateZone(currentHeartRate);
                heartRateZoneText3D.color = GetHeartRateZoneColor(currentHeartRate);
            }
            else
            {
                heartRateZoneText3D.text = "No Data";
                heartRateZoneText3D.color = Color.gray;
            }
        }
    }

    private string GetHeartRateZone(float hr)
    {
        if (hr < 60)
            return "Resting";
        else if (hr < 100)
            return "Resting";
        else if (hr < 140)
            return "Fat Burn";
        else if (hr < 170)
            return "Cardio";
        else
            return "Peak";
    }

    private Color GetHeartRateZoneColor(float hr)
    {
        if (hr < 60)
            return restingZoneColor;
        else if (hr < 100)
            return restingZoneColor;
        else if (hr < 140)
            return fatBurnZoneColor;
        else if (hr < 170)
            return cardioZoneColor;
        else
            return peakZoneColor;
    }

    /// <summary>
    /// Set display mode (2D or 3D)
    /// </summary>
    public void SetDisplayMode(DisplayMode mode)
    {
        displayMode = mode;
    }

    /// <summary>
    /// Get current heart rate value
    /// </summary>
    public float GetCurrentHeartRate()
    {
        return currentHeartRate;
    }
}
