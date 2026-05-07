using UnityEngine;
using TMPro;

/// <summary>
/// Simple UI display for bike data (Speed, Power, Gradient)
/// Fixed to top-left corner of screen
/// </summary>
public class BikeDataUI : MonoBehaviour
{
    [Header("Bike Controller")]
    [Tooltip("Bike controller to get data from")]
    [SerializeField] private BikeController bikeController;

    [Header("UI Text References")]
    [Tooltip("Text to display speed")]
    [SerializeField] private TextMeshProUGUI speedText;
    
    [Tooltip("Text to display power")]
    [SerializeField] private TextMeshProUGUI powerText;
    
    [Tooltip("Text to display gradient")]
    [SerializeField] private TextMeshProUGUI gradientText;
    
    [Tooltip("Text to display heart rate (optional)")]
    [SerializeField] private TextMeshProUGUI heartRateText;

    [Header("Course Progress Bar")]
    [Tooltip("Progress bar showing course/climb progress")]
    [SerializeField] private UnityEngine.UI.Slider courseProgressBar;
    
    [Tooltip("Progress bar fill image (for color coding based on gradient)")]
    [SerializeField] private UnityEngine.UI.Image progressBarFill;
    
    [Tooltip("Current position marker on progress bar")]
    [SerializeField] private RectTransform positionMarker;
    
    [Tooltip("Text showing current gradient on progress bar")]
    [SerializeField] private TextMeshProUGUI progressGradientText;
    
    [Tooltip("Text showing progress percentage or distance")]
    [SerializeField] private TextMeshProUGUI progressText;
    
    [Tooltip("Text showing distance information (remaining or total)")]
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("Climb Visualization")]
    [Tooltip("Show climb fluctuations on progress bar")]
    [SerializeField] private bool showClimbFluctuations = true;
    
    [Tooltip("Total course distance in meters (default: 500m for experiment)")]
    [SerializeField] private float totalCourseDistance = 500f;
    
    [Tooltip("Distance display mode: Travelled, Remaining, or Both")]
    [SerializeField] private DistanceDisplayMode distanceDisplayMode = DistanceDisplayMode.Travelled;

    public enum DistanceDisplayMode
    {
        Travelled,      // Show distance already travelled (e.g., "50m")
        Remaining,      // Show remaining distance (e.g., "450m")
        Both            // Show both (e.g., "50m / 500m")
    }
    
    [Tooltip("Gradient markers on progress bar (show where climbs are)")]
    [SerializeField] private bool showGradientMarkers = true;
    
    [Tooltip("Number of gradient sample points along the course")]
    [Range(10, 100)]
    [SerializeField] private int gradientSamplePoints = 50;

    [Header("Heart Rate Display")]
    [Tooltip("WebSocket client to get heart rate data (optional)")]
    [SerializeField] private WebSocketClient webSocketClient;
    
    [Tooltip("Show heart rate in UI")]
    [SerializeField] private bool showHeartRate = true;

    [Header("Update Settings")]
    [Tooltip("Update frequency (times per second)")]
    [SerializeField] private float updateRate = 10f;

    private float updateInterval;
    private float lastUpdateTime;
    private float[] courseGradients; // Store gradients along the course for visualization
    private float courseStartDistance = 0f;

    private void Start()
    {
        // Auto-find bike controller
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        if (bikeController == null)
        {
            Debug.LogError("[BikeDataUI] BikeController not found!");
        }

        // Auto-find WebSocket client for heart rate data
        if (webSocketClient == null && showHeartRate)
        {
            webSocketClient = FindObjectOfType<WebSocketClient>();
            if (webSocketClient == null)
            {
                Debug.LogWarning("[BikeDataUI] WebSocketClient not found. Heart rate will not be displayed.");
            }
        }

        // Ensure Canvas is in Screen Space mode
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogWarning("[BikeDataUI] Canvas is not in Screen Space - Overlay mode! Changing it now.");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // Fix UI position to top-left
        FixUIPosition();

        // Initialize course distance (default: 500m for experiment)
        if (totalCourseDistance <= 0f)
        {
            totalCourseDistance = 500f; // Default experiment distance
        }

        // Initialize gradient samples for climb visualization
        if (showClimbFluctuations)
        {
            InitializeCourseGradients();
        }

        updateInterval = 1f / updateRate;
        lastUpdateTime = Time.time;
    }

    private void InitializeCourseGradients()
    {
        if (totalCourseDistance <= 0f) return;

        courseGradients = new float[gradientSamplePoints];
        Terrain terrain = FindObjectOfType<Terrain>();
        // OLD SYSTEM - Commented out
        // TrialTerrainManager trialManager = FindObjectOfType<TrialTerrainManager>();
        
        if (terrain == null) return;

        TerrainData data = terrain.terrainData;
        Vector3 size = data.size;
        int heightmapRes = data.heightmapResolution;

        // Sample gradients along the course
        for (int i = 0; i < gradientSamplePoints; i++)
        {
            float normalizedX = (float)i / (gradientSamplePoints - 1);
            int x = Mathf.RoundToInt(normalizedX * (heightmapRes - 1));
            int z = heightmapRes / 2; // Road center

            // Ensure we don't go out of bounds
            if (x >= heightmapRes - 1)
            {
                // Use previous gradient for last point
                courseGradients[i] = i > 0 ? courseGradients[i - 1] : 0f;
                continue;
            }

            // Get height at this point and next point
            try
            {
                float[,] heights = data.GetHeights(x, z, 2, 1);
                float height1 = heights[0, 0] * size.y;
                float height2 = heights[1, 0] * size.y;
                float distance = size.x / gradientSamplePoints;
                float gradient = ((height2 - height1) / distance) * 100f; // Convert to percentage
                courseGradients[i] = gradient;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BikeDataUI] Error sampling gradient at index {i}: {e.Message}");
                courseGradients[i] = i > 0 ? courseGradients[i - 1] : 0f;
            }
        }
    }

    private void FixUIPosition()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) return;

        // Anchor to top-left corner
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(20, -20);

        Debug.Log("[BikeDataUI] UI fixed to top-left corner");
    }

    private void Update()
    {
        if (bikeController == null) return;

        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDisplay();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateDisplay()
    {
        // Update Speed
        if (speedText != null)
        {
            float speedKmh = bikeController.GetSpeedKmh();
            speedText.text = $"Speed: {speedKmh:F1} km/h";
        }

        // Update Power
        if (powerText != null)
        {
            float power = bikeController.GetPower();
            powerText.text = $"Power: {power:F0} W";
        }

        // Update Gradient
        if (gradientText != null)
        {
            float gradient = bikeController.GetGradient();
            gradientText.text = $"Gradient: {gradient:F1}%";
        }

        // Update Heart Rate (if enabled and WebSocket client available)
        if (showHeartRate && heartRateText != null && webSocketClient != null && webSocketClient.IsConnected())
        {
            float heartRate = webSocketClient.GetHeartRateBpm();
            if (heartRate > 0f)
            {
                heartRateText.text = $"HR: {heartRate:F0} BPM";
            }
            else
            {
                heartRateText.text = "HR: -- BPM";
            }
        }
        else if (showHeartRate && heartRateText != null)
        {
            heartRateText.text = "HR: -- BPM";
        }

        // Update Course Progress
        UpdateCourseProgress();
    }

    private void UpdateCourseProgress()
    {
        if (courseProgressBar == null) return;

        // Get current distance
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - courseStartDistance;

        // Calculate progress (0-1)
        float progress = 0f;
        if (totalCourseDistance > 0f)
        {
            progress = Mathf.Clamp01(distanceFromStart / totalCourseDistance);
        }

        // Update progress bar value
        courseProgressBar.value = progress;

        // Update progress text
        if (progressText != null)
        {
            progressText.text = $"{progress * 100f:F0}%";
        }

        // Update distance text
        if (distanceText != null)
        {
            switch (distanceDisplayMode)
            {
                case DistanceDisplayMode.Travelled:
                    // Show distance already travelled
                    distanceText.text = $"{distanceFromStart:F0}m";
                    break;
                    
                case DistanceDisplayMode.Remaining:
                    // Show remaining distance
                    float remaining = totalCourseDistance - distanceFromStart;
                    if (remaining < 0) remaining = 0;
                    distanceText.text = $"{remaining:F0}m";
                    break;
                    
                case DistanceDisplayMode.Both:
                    // Show both travelled and total
                    distanceText.text = $"{distanceFromStart:F0}m / {totalCourseDistance:F0}m";
                    break;
            }
        }

        // Get current gradient
        float currentGradient = bikeController.GetGradient();

        // Update gradient text on progress bar
        if (progressGradientText != null)
        {
            progressGradientText.text = $"{currentGradient:F1}%";
        }

        // Update progress bar color based on current gradient
        if (progressBarFill != null)
        {
            // Color coding: blue (flat) -> yellow (moderate) -> red (steep)
            float gradientNormalized = Mathf.Clamp01(Mathf.Abs(currentGradient) / 20f);
            Color color;
            if (currentGradient < 0)
            {
                // Downhill - use blue/cyan
                color = Color.Lerp(Color.blue, Color.cyan, gradientNormalized);
            }
            else
            {
                // Uphill - use yellow to red
                color = Color.Lerp(Color.yellow, Color.red, gradientNormalized);
            }
            progressBarFill.color = color;
        }

        // Update position marker
        if (positionMarker != null && courseProgressBar != null)
        {
            RectTransform barRect = courseProgressBar.GetComponent<RectTransform>();
            if (barRect != null)
            {
                float barWidth = barRect.rect.width;
                float markerX = (progress * barWidth) - (barWidth * 0.5f);
                positionMarker.anchoredPosition = new Vector2(markerX, 0);
            }
        }

        // Show climb fluctuations if enabled
        if (showClimbFluctuations && courseGradients != null)
        {
            VisualizeClimbFluctuations(progress);
        }
    }

    private void VisualizeClimbFluctuations(float currentProgress)
    {
        // This method can be used to draw gradient markers or color segments on the progress bar
        // For now, we'll update the fill color based on the gradient at current position
        
        if (courseGradients == null || courseGradients.Length == 0) return;

        // Get gradient at current position
        int sampleIndex = Mathf.RoundToInt(currentProgress * (gradientSamplePoints - 1));
        sampleIndex = Mathf.Clamp(sampleIndex, 0, gradientSamplePoints - 1);
        float gradientAtPosition = courseGradients[sampleIndex];

        // Update fill color based on gradient at this position
        if (progressBarFill != null)
        {
            float gradientNormalized = Mathf.Clamp01(Mathf.Abs(gradientAtPosition) / 20f);
            if (gradientAtPosition < 0)
            {
                progressBarFill.color = Color.Lerp(Color.blue, Color.cyan, gradientNormalized);
            }
            else
            {
                progressBarFill.color = Color.Lerp(Color.yellow, Color.red, gradientNormalized);
            }
        }
    }

    /// <summary>
    /// Reset course progress (call when starting a new trial)
    /// </summary>
    public void ResetCourseProgress()
    {
        courseStartDistance = bikeController != null ? bikeController.GetDistance() : 0f;
        
        // Re-initialize gradients if needed
        if (showClimbFluctuations)
        {
            InitializeCourseGradients();
        }
    }

    /// <summary>
    /// Set total course distance
    /// </summary>
    public void SetTotalCourseDistance(float distance)
    {
        totalCourseDistance = distance;
        if (showClimbFluctuations)
        {
            InitializeCourseGradients();
        }
    }

    [ContextMenu("Fix UI Position")]
    public void FixUIPositionManual()
    {
        FixUIPosition();
    }
}
