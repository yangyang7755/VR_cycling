using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Incline indicator showing current incline (circular indicator) and upcoming incline preview (undulating bar)
/// Positioned at bottom right corner of screen
/// Design inspired by cycling apps with circular current incline and undulating preview bar
/// </summary>
public class InclineIndicator : MonoBehaviour
{
    [Header("Bike Controller")]
    [Tooltip("Bike controller to get gradient from")]
    [SerializeField] private BikeController bikeController;

    [Tooltip("Terrain builder to sample upcoming gradients")]
    [SerializeField] private SimpleTerrainBuilder terrainBuilder;

    [Tooltip("Terrain for height sampling")]
    [SerializeField] private Terrain terrain;

    [Header("UI References - Current Incline (Circular Indicator)")]
    [Tooltip("Container for circular current incline indicator")]
    [SerializeField] private RectTransform currentInclineContainer;

    [Tooltip("Circular background image (yellow/black split)")]
    [SerializeField] private Image currentInclineCircle;

    [Tooltip("Text showing current incline percentage (e.g., '+6.6') - Secondary info")]
    [SerializeField] private TextMeshProUGUI currentInclineValueText;

    [Tooltip("Text showing percentage symbol ('%') - Secondary info")]
    [SerializeField] private TextMeshProUGUI currentInclinePercentText;

    [Header("UI References - Continuous Incline Bar (Current + Upcoming)")]
    [Tooltip("Main container for continuous incline bar showing current and upcoming gradient")]
    [SerializeField] private RectTransform continuousInclineBarContainer;

    [Tooltip("Background for continuous incline bar")]
    [SerializeField] private Image continuousInclineBarBackground;

    [Tooltip("Parent for incline segments (current + upcoming preview)")]
    [SerializeField] private RectTransform inclineSegmentsParent;

    [Tooltip("Current position indicator (vertical line or marker)")]
    [SerializeField] private RectTransform currentPositionIndicator;

    [Tooltip("Image for current position indicator")]
    [SerializeField] private Image currentPositionIndicatorImage;

    [Header("UI References - Upcoming Incline Preview (Undulating Bar)")]
    [Tooltip("Container for upcoming incline preview bar")]
    [SerializeField] private RectTransform previewBarContainer;

    [Tooltip("Parent for preview bar segments")]
    [SerializeField] private RectTransform previewBarParent;

    [Tooltip("Background image for preview bar (optional)")]
    [SerializeField] private Image previewBarBackground;

    [Tooltip("Current position marker (blue pin icon)")]
    [SerializeField] private RectTransform positionMarker;

    [Tooltip("Image for position marker (blue pin)")]
    [SerializeField] private Image positionMarkerImage;

    [Header("Settings")]
    [Tooltip("Total course distance for preview (meters)")]
    [SerializeField] private float totalCourseDistance = 500f;

    [Tooltip("Distance ahead to preview (meters)")]
    [SerializeField] private float previewDistance = 200f;

    [Tooltip("Number of segments for undulating preview bar")]
    [Range(50, 200)]
    [SerializeField] private int previewBarSegmentCount = 100;

    [Tooltip("Update frequency (times per second)")]
    [SerializeField] private float updateRate = 10f;

    [Tooltip("Height of preview bar (pixels)")]
    [SerializeField] private float previewBarHeight = 40f;

    [Tooltip("Width of preview bar (pixels)")]
    [SerializeField] private float previewBarWidth = 400f;

    [Tooltip("Vertical amplitude of undulation (how much up/down movement)")]
    [Range(10f, 50f)]
    [SerializeField] private float undulationAmplitude = 20f;

    [Header("Visual Settings")]
    [Tooltip("Color for preview bar (yellow)")]
    [SerializeField] private Color previewBarColor = Color.yellow;

    [Tooltip("Color for current incline circle (yellow half)")]
    [SerializeField] private Color circleYellowColor = Color.yellow;

    [Tooltip("Color for current incline circle (black half)")]
    [SerializeField] private Color circleBlackColor = Color.black;

    [Tooltip("Color for position marker (blue pin)")]
    [SerializeField] private Color positionMarkerColor = new Color(0.2f, 0.6f, 1f); // Light blue

    [Tooltip("Color for position marker icon (white)")]
    [SerializeField] private Color positionMarkerIconColor = Color.white;

    [Tooltip("Size of circular current incline indicator (pixels)")]
    [SerializeField] private float circleIndicatorSize = 80f;

    [Header("Continuous Incline Bar Settings")]
    [Tooltip("Width of continuous incline bar (pixels)")]
    [SerializeField] private float continuousBarWidth = 300f;

    [Tooltip("Height of continuous incline bar container (pixels)")]
    [SerializeField] private float continuousBarHeight = 60f;

    [Tooltip("Number of segments for continuous preview (more = smoother but more expensive)")]
    [Range(20, 100)]
    [SerializeField] private int continuousBarSegmentCount = 50;

    [Tooltip("Distance ahead to preview (meters)")]
    [SerializeField] private float previewDistanceAhead = 100f;

    [Tooltip("Maximum gradient for visual encoding (percentage)")]
    [SerializeField] private float maxGradientForEncoding = 15f;

    [Tooltip("Base color for flat terrain (0% gradient)")]
    [SerializeField] private Color flatColor = Color.cyan;

    [Tooltip("Color for moderate incline (5% gradient)")]
    [SerializeField] private Color moderateColor = Color.yellow;

    [Tooltip("Color for steep incline (10%+ gradient)")]
    [SerializeField] private Color steepColor = Color.red;

    [Tooltip("Minimum segment height (pixels) - for flat terrain")]
    [SerializeField] private float minSegmentHeight = 8f;

    [Tooltip("Maximum segment height (pixels) - for steep terrain")]
    [SerializeField] private float maxSegmentHeight = 50f;

    [Tooltip("Smoothing speed for visual changes (higher = faster response)")]
    [Range(1f, 20f)]
    [SerializeField] private float visualSmoothingSpeed = 10f;

    [Tooltip("Emphasize current position with visual highlight")]
    [SerializeField] private bool emphasizeCurrentPosition = true;

    [Tooltip("Show upcoming preview with reduced opacity (for predictability)")]
    [SerializeField] private bool showUpcomingPreview = true;

    [Tooltip("Opacity for upcoming preview segments (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float upcomingPreviewOpacity = 0.6f;

    [Header("Position Settings")]
    [Tooltip("Anchor position: Bottom Right")]
    [SerializeField] private bool autoPosition = true;

    [Tooltip("Offset from bottom right corner (pixels)")]
    [SerializeField] private Vector2 bottomRightOffset = new Vector2(50f, 50f);

    private float updateInterval;
    private float lastUpdateTime;
    private float courseStartDistance = 0f;

    // Preview bar data (for undulating preview bar)
    private List<Image> previewBarSegments = new List<Image>();
    private float[] courseGradients;
    private float[] courseHeights;
    private float maxGradientRange = 20f;

    // Continuous incline bar data
    private List<Image> continuousBarSegments = new List<Image>();
    private float[] continuousBarGradients; // Gradients for continuous bar
    private float[] continuousBarHeights; // Heights for continuous bar
    private int currentPositionSegmentIndex = 0;

    private void Start()
    {
        // Auto-find references
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        if (terrainBuilder == null)
        {
            terrainBuilder = FindObjectOfType<SimpleTerrainBuilder>();
        }

        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        // Calculate update interval
        updateInterval = 1f / updateRate;

        // Auto-position container at bottom right
        if (autoPosition)
        {
            SetupBottomRightPosition();
        }

        // Initialize course start distance
        if (bikeController != null)
        {
            courseStartDistance = bikeController.GetDistance();
        }

        // Initialize preview bar (undulating preview)
        InitializePreviewBar();

        // Initialize continuous incline bar (current + upcoming)
        InitializeContinuousInclineBar();

        // Initialize UI
        UpdateInclineDisplay();
    }

    /// <summary>
    /// Initialize continuous incline bar showing current and upcoming gradient
    /// This bar visually encodes effort and predictability with minimal cognitive load
    /// </summary>
    private void InitializeContinuousInclineBar()
    {
        if (inclineSegmentsParent == null) return;

        // Clear existing segments
        foreach (Image segment in continuousBarSegments)
        {
            if (segment != null) Destroy(segment.gameObject);
        }
        continuousBarSegments.Clear();

        // Create segments for continuous bar
        float segmentWidth = continuousBarWidth / continuousBarSegmentCount;
        for (int i = 0; i < continuousBarSegmentCount; i++)
        {
            GameObject segmentObj = new GameObject($"ContinuousSegment_{i}");
            segmentObj.transform.SetParent(inclineSegmentsParent, false);

            RectTransform segmentRect = segmentObj.AddComponent<RectTransform>();
            segmentRect.anchorMin = new Vector2(0f, 0f);
            segmentRect.anchorMax = new Vector2(0f, 0f);
            segmentRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom
            segmentRect.sizeDelta = new Vector2(segmentWidth, minSegmentHeight);
            segmentRect.anchoredPosition = new Vector2(i * segmentWidth, 0f);

            Image segmentImage = segmentObj.AddComponent<Image>();
            segmentImage.color = flatColor;
            continuousBarSegments.Add(segmentImage);
        }

        // Sample continuous bar gradients and heights
        SampleContinuousBarProfile();
    }

    /// <summary>
    /// Sample terrain to get continuous bar gradient and height profile
    /// Samples from current position to preview distance ahead
    /// </summary>
    private void SampleContinuousBarProfile()
    {
        if (terrain == null || terrainBuilder == null) return;

        continuousBarGradients = new float[continuousBarSegmentCount];
        continuousBarHeights = new float[continuousBarSegmentCount];

        float roadCenterZ = terrainBuilder.GetRoadCenterZ();
        float sampleInterval = previewDistanceAhead / continuousBarSegmentCount;

        // Get current distance
        float currentDistance = bikeController != null ? bikeController.GetDistance() - courseStartDistance : 0f;
        float startHeight = 0f;
        bool hasStartHeight = false;

        for (int i = 0; i < continuousBarSegmentCount; i++)
        {
            // Sample from current position forward
            float distance = currentDistance + (i * sampleInterval);
            float worldX = distance;
            float worldZ = roadCenterZ;

            Vector3 pos = new Vector3(worldX, 0f, worldZ);
            float height = terrain.SampleHeight(pos);

            if (!hasStartHeight && i == 0)
            {
                startHeight = height;
                hasStartHeight = true;
            }

            // Calculate gradient using multiple samples for more robust slope calculation
            // Use longer sampling distance and average multiple samples to reduce impact of local undulations
            float sampleDistance = 15f; // Sample 15m ahead (longer distance for smoother gradient)
            int numSamples = 3; // Sample at current, mid-point, and ahead
            float totalRise = 0f;

            for (int s = 0; s < numSamples; s++)
            {
                float currentSampleDistance = (float)s / (numSamples - 1) * sampleDistance;
                Vector3 samplePosCurrent = new Vector3(worldX + currentSampleDistance, 0f, worldZ);
                Vector3 samplePosAhead = new Vector3(worldX + currentSampleDistance + 5f, 0f, worldZ); // 5m step for rise

                float heightCurrent = terrain.SampleHeight(samplePosCurrent);
                float heightAhead = terrain.SampleHeight(samplePosAhead);
                totalRise += (heightAhead - heightCurrent);
            }
            
            float averageRise = totalRise / numSamples;
            float run = 5f; // The 'run' for each individual rise calculation is 5m
            float gradient = (averageRise / run) * 100f; // Convert to percentage

            // Clamp gradient to reasonable range
            gradient = Mathf.Clamp(gradient, -12f, 12f);

            continuousBarGradients[i] = gradient;
            continuousBarHeights[i] = height - startHeight; // Relative to start
        }
    }

    /// <summary>
    /// Initialize the undulating preview bar with segments
    /// </summary>
    private void InitializePreviewBar()
    {
        if (previewBarParent == null) return;

        // Clear existing segments
        foreach (Image segment in previewBarSegments)
        {
            if (segment != null) Destroy(segment.gameObject);
        }
        previewBarSegments.Clear();

        // Create segments for preview bar
        float segmentWidth = previewBarWidth / previewBarSegmentCount;
        for (int i = 0; i < previewBarSegmentCount; i++)
        {
            GameObject segmentObj = new GameObject($"PreviewSegment_{i}");
            segmentObj.transform.SetParent(previewBarParent, false);

            RectTransform segmentRect = segmentObj.AddComponent<RectTransform>();
            segmentRect.anchorMin = new Vector2(0f, 0.5f);
            segmentRect.anchorMax = new Vector2(0f, 0.5f);
            segmentRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom
            segmentRect.sizeDelta = new Vector2(segmentWidth, previewBarHeight);
            segmentRect.anchoredPosition = new Vector2(i * segmentWidth, 0f);

            Image segmentImage = segmentObj.AddComponent<Image>();
            segmentImage.color = previewBarColor;
            previewBarSegments.Add(segmentImage);
        }

        // Sample course gradients and heights
        SampleCourseProfile();
    }

    /// <summary>
    /// Sample terrain to get course gradient and height profile
    /// </summary>
    private void SampleCourseProfile()
    {
        if (terrain == null || terrainBuilder == null) return;

        courseGradients = new float[previewBarSegmentCount];
        courseHeights = new float[previewBarSegmentCount];

        float roadCenterZ = terrainBuilder.GetRoadCenterZ();
        float sampleInterval = totalCourseDistance / previewBarSegmentCount;

        // Get start height from bike controller's current position (if available)
        // This ensures elevation is relative to the actual starting point
        float startHeight = 0f;
        bool hasStartHeight = false;
        
        if (bikeController != null)
        {
            // Use bike's starting position height as reference
            Vector3 bikeStartPos = bikeController.transform.position;
            startHeight = terrain.SampleHeight(bikeStartPos);
            hasStartHeight = true;
        }

        for (int i = 0; i < previewBarSegmentCount; i++)
        {
            float distance = i * sampleInterval;
            float worldX = distance;
            float worldZ = roadCenterZ;

            Vector3 pos = new Vector3(worldX, 0f, worldZ);
            float height = terrain.SampleHeight(pos);

            if (!hasStartHeight)
            {
                startHeight = height;
                hasStartHeight = true;
            }

            // Calculate gradient using multiple samples for more robust slope calculation
            // Use longer sampling distance and average multiple samples to reduce impact of local undulations
            float sampleDistance = 15f; // Sample 15m ahead (longer distance for smoother gradient)
            int numSamples = 3; // Sample at current, mid-point, and ahead
            float totalRise = 0f;

            for (int s = 0; s < numSamples; s++)
            {
                float currentSampleDistance = (float)s / (numSamples - 1) * sampleDistance;
                Vector3 samplePosCurrent = new Vector3(worldX + currentSampleDistance, 0f, worldZ);
                Vector3 samplePosAhead = new Vector3(worldX + currentSampleDistance + 5f, 0f, worldZ); // 5m step for rise

                float heightCurrent = terrain.SampleHeight(samplePosCurrent);
                float heightAhead = terrain.SampleHeight(samplePosAhead);
                totalRise += (heightAhead - heightCurrent);
            }
            
            float averageRise = totalRise / numSamples;
            float run = 5f; // The 'run' for each individual rise calculation is 5m
            float gradient = (averageRise / run) * 100f; // Convert to percentage

            // Clamp gradient to reasonable range
            gradient = Mathf.Clamp(gradient, -12f, 12f);

            courseGradients[i] = gradient;
            courseHeights[i] = height - startHeight; // Relative to start (elevation gain from start)
            
            // #region agent log - Debug elevation calculation
            if (i == 0 || (i % 20 == 0 && Time.frameCount % 60 == 0)) // Log first and every 20th segment
            {
                System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log", 
                    $"{{\"id\":\"incline_elevation_sample\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"InclineIndicator.cs:385\",\"message\":\"Sampling course elevation profile\",\"data\":{{\"segmentIndex\":{i},\"distance\":{distance:F2},\"worldX\":{worldX:F2},\"worldZ\":{worldZ:F2},\"height\":{height:F2},\"startHeight\":{startHeight:F2},\"relativeHeight\":{height - startHeight:F2},\"gradient\":{gradient:F2}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\"}}\n");
            }
            // #endregion
        }

        // Find max gradient range for normalization
        maxGradientRange = 0f;
        for (int i = 0; i < courseGradients.Length; i++)
        {
            float absGradient = Mathf.Abs(courseGradients[i]);
            if (absGradient > maxGradientRange)
            {
                maxGradientRange = absGradient;
            }
        }
        if (maxGradientRange < 1f) maxGradientRange = 20f;
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateInclineDisplay();
            lastUpdateTime = Time.time;
        }
    }

    /// <summary>
    /// Setup container position at bottom right of screen
    /// </summary>
    private void SetupBottomRightPosition()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        if (canvas != null)
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Set anchor to bottom right
                rectTransform.anchorMin = new Vector2(1f, 0f);
                rectTransform.anchorMax = new Vector2(1f, 0f);
                rectTransform.pivot = new Vector2(1f, 0f);

                // Position at bottom right with offset
                rectTransform.anchoredPosition = bottomRightOffset;
            }
        }
    }

    /// <summary>
    /// Update current and upcoming incline display
    /// </summary>
    private void UpdateInclineDisplay()
    {
        if (bikeController == null) return;

        // Get current incline
        float currentGradient = bikeController.GetGradient();
        UpdateCurrentIncline(currentGradient);

        // Update preview bar and position marker
        UpdatePreviewBar();
    }

    /// <summary>
    /// Update continuous incline bar showing current and upcoming gradient
    /// Visually encodes effort and predictability with minimal cognitive load
    /// </summary>
    private void UpdateCurrentIncline(float gradient)
    {
        // Update continuous incline bar (PRIMARY VISUAL ENCODING)
        UpdateContinuousInclineBar(gradient);

        // Update circular indicator (secondary visual element)
        if (currentInclineCircle != null)
        {
            if (gradient > 0f)
            {
                currentInclineCircle.color = circleYellowColor;
            }
            else
            {
                currentInclineCircle.color = circleBlackColor;
            }
        }

        // Update numeric value text (SECONDARY CUE - confirmation only)
        if (currentInclineValueText != null)
        {
            string sign = gradient >= 0f ? "+" : "";
            currentInclineValueText.text = $"{sign}{gradient:F1}";
            currentInclineValueText.fontSize = 18f;
        }

        // Update percent text
        if (currentInclinePercentText != null)
        {
            currentInclinePercentText.text = "%";
            currentInclinePercentText.fontSize = 14f;
        }
    }

    /// <summary>
    /// Update continuous incline bar with current and upcoming gradient preview
    /// This is the primary visual encoding mechanism
    /// </summary>
    private void UpdateContinuousInclineBar(float currentGradient)
    {
        if (continuousBarSegments.Count == 0 || continuousBarGradients == null) return;

        // Resample continuous bar profile (updates as cyclist moves)
        SampleContinuousBarProfile();

        // Calculate current position segment index (first segment = current position)
        currentPositionSegmentIndex = 0;

        float segmentWidth = continuousBarWidth / continuousBarSegmentCount;
        float baseY = 0f; // Bottom of bar

        // Use current gradient from BikeController for first segment (current position)
        // This ensures the indicator reflects the actual gradient, not just sampled terrain
        float currentSegmentGradient = currentGradient; // Use actual current gradient

        // #region agent log
        System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log", 
            $"{{\"id\":\"incline_indicator_update\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"InclineIndicator.cs:491\",\"message\":\"Updating continuous incline bar\",\"data\":{{\"currentGradient\":{currentGradient:F2},\"currentSegmentGradient\":{currentSegmentGradient:F2}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\"}}\n");
        // #endregion

        // Update each segment to visually encode gradient
        for (int i = 0; i < continuousBarSegments.Count && i < continuousBarGradients.Length; i++)
        {
            Image segment = continuousBarSegments[i];
            if (segment == null) continue;

            RectTransform segmentRect = segment.GetComponent<RectTransform>();
            if (segmentRect == null) continue;

            // Use actual current gradient for first segment, sampled gradient for upcoming preview
            float gradient = (i == 0) ? currentSegmentGradient : continuousBarGradients[i];
            float absGradient = Mathf.Abs(gradient);

            // CRITICAL: If current gradient is positive (uphill), don't show blue even if sampled gradient is slightly negative
            // This prevents false blue indication when there are local undulations
            if (i == 0 && currentGradient > 0.1f && gradient < 0f)
            {
                // Use current gradient instead of sampled gradient for current position
                gradient = currentGradient;
                absGradient = Mathf.Abs(gradient);
                
                // #region agent log
                System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log", 
                    $"{{\"id\":\"incline_indicator_fix\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"InclineIndicator.cs:520\",\"message\":\"Fixed blue indicator on uphill\",\"data\":{{\"sampledGradient\":{continuousBarGradients[i]:F2},\"currentGradient\":{currentGradient:F2},\"correctedGradient\":{gradient:F2}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\"}}\n");
                // #endregion
            }

            // Calculate visual encoding: height scales with gradient (monotonic)
            float normalizedGradient = Mathf.Clamp01(absGradient / maxGradientForEncoding);
            float segmentHeight = Mathf.Lerp(minSegmentHeight, maxSegmentHeight, normalizedGradient);

            // Calculate color based on gradient (monotonic color mapping)
            // CRITICAL: For current position, always use actual current gradient, not sampled
            Color segmentColor = GetGradientColor(gradient);

            // #region agent log - Compare gradient with color mapping
            if (i == 0 || (i < 5 && Time.frameCount % 30 == 0)) // Log current position and first few segments
            {
                System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log", 
                    $"{{\"id\":\"gradient_color_coupling\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"InclineIndicator.cs:564\",\"message\":\"Gradient to color mapping\",\"data\":{{\"segmentIndex\":{i},\"gradient\":{gradient:F2},\"absGradient\":{absGradient:F2},\"currentGradient\":{currentGradient:F2},\"sampledGradient\":{continuousBarGradients[i]:F2},\"color\":{{\"r\":{segmentColor.r:F2},\"g\":{segmentColor.g:F2},\"b\":{segmentColor.b:F2}}},\"isCurrentPosition\":{(i == currentPositionSegmentIndex)}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
            }
            // #endregion

            // Determine if this is current position or upcoming preview
            bool isCurrentPosition = (i == currentPositionSegmentIndex);
            bool isUpcoming = (i > currentPositionSegmentIndex);

            // Emphasize current position with full opacity and highlight
            if (isCurrentPosition && emphasizeCurrentPosition)
            {
                segmentColor.a = 1f; // Full opacity
                // Add slight brightness boost for emphasis
                segmentColor = Color.Lerp(segmentColor, Color.white, 0.1f);
            }
            else if (isUpcoming && showUpcomingPreview)
            {
                // Upcoming preview with reduced opacity (for predictability)
                segmentColor.a = upcomingPreviewOpacity;
            }
            else
            {
                segmentColor.a = 1f;
            }

            // Update segment position and size
            segmentRect.anchoredPosition = new Vector2(i * segmentWidth, baseY);
            segmentRect.sizeDelta = new Vector2(segmentWidth, segmentHeight);
            segment.color = segmentColor;
        }

        // Update current position indicator
        if (currentPositionIndicator != null)
        {
            float indicatorX = currentPositionSegmentIndex * segmentWidth;
            currentPositionIndicator.anchoredPosition = new Vector2(indicatorX, continuousBarHeight * 0.5f);
            
            if (currentPositionIndicatorImage != null)
            {
                currentPositionIndicatorImage.color = new Color(1f, 1f, 1f, 0.8f); // White, semi-transparent
            }
        }
    }

    /// <summary>
    /// Get color for gradient value (monotonic color mapping)
    /// Encodes subjective effort: flat = easy (cyan), steep = hard (red)
    /// Color mapping should match ElevationProgressBar:
    /// Green: 0-1% (flat)
    /// Yellow: 2-5%
    /// Orange: 5-8%
    /// Red: 8-12%
    /// Dark Red: 12%+
    /// </summary>
    private Color GetGradientColor(float gradient)
    {
        // Clamp gradient to 12% max (consistent with ElevationProgressBar)
        float clampedGradient = Mathf.Clamp(Mathf.Abs(gradient), 0f, 12f);
        float absGradient = clampedGradient;

        if (gradient < 0f)
        {
            // Downhill - blue (easier)
            float intensity = Mathf.Clamp01(Mathf.Abs(gradient) / 10f);
            return Color.Lerp(new Color(0.2f, 0.5f, 1f), new Color(0f, 0.3f, 1f), intensity);
        }
        else if (absGradient <= 1f)
        {
            // Flat: 0-1% - Green (matching ElevationProgressBar)
            float t = absGradient / 1f;
            return Color.Lerp(
                new Color(0.2f, 0.9f, 0.3f), // Bright green at 0%
                new Color(0.3f, 0.95f, 0.35f), // Slightly brighter green at 1%
                t
            );
        }
        else if (absGradient <= 5f)
        {
            // Moderate: 2-5% - Yellow (matching ElevationProgressBar)
            float t = (absGradient - 1f) / 4f;
            return Color.Lerp(
                new Color(0.9f, 1f, 0.2f),  // Bright yellow at 1%
                new Color(1f, 0.85f, 0.1f), // Slightly orange-yellow at 5%
                t
            );
        }
        else if (absGradient <= 8f)
        {
            // Steep: 5-8% - Orange (matching ElevationProgressBar)
            float t = (absGradient - 5f) / 3f;
            return Color.Lerp(
                new Color(1f, 0.7f, 0.1f),  // Orange at 5%
                new Color(1f, 0.5f, 0f),    // Darker orange at 8%
                t
            );
        }
        else if (absGradient <= 12f)
        {
            // Very steep: 8-12% - Red (matching ElevationProgressBar)
            float t = (absGradient - 8f) / 4f;
            return Color.Lerp(
                new Color(1f, 0.2f, 0.1f),  // Bright red at 8%
                new Color(0.8f, 0.1f, 0.1f), // Darker red at 12%
                t
            );
        }
        else
        {
            // Above 12% (should not happen) - Dark Red
            Debug.LogWarning($"[InclineIndicator] Gradient {absGradient:F2}% exceeds 12% limit! This should not happen.");
            return new Color(0.6f, 0f, 0f); // Dark red for values above 12%
        }
    }

    /// <summary>
    /// Update undulating preview bar and position marker
    /// </summary>
    private void UpdatePreviewBar()
    {
        if (bikeController == null || previewBarSegments.Count == 0 || courseGradients == null) return;

        // Get current distance
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - courseStartDistance;
        float progress = Mathf.Clamp01(distanceFromStart / totalCourseDistance);

        // Find min/max heights for normalization
        // CRITICAL: Use actual terrain heights, not just relative heights
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        for (int i = 0; i < courseHeights.Length; i++)
        {
            if (courseHeights[i] < minHeight) minHeight = courseHeights[i];
            if (courseHeights[i] > maxHeight) maxHeight = courseHeights[i];
        }
        float heightRange = maxHeight - minHeight;
        
        // #region agent log - Debug elevation display
        if (Time.frameCount % 60 == 0) // Log every 60 frames
        {
            System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log", 
                $"{{\"id\":\"incline_elevation_display\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"InclineIndicator.cs:733\",\"message\":\"Elevation display calculation\",\"data\":{{\"minHeight\":{minHeight:F2},\"maxHeight\":{maxHeight:F2},\"heightRange\":{heightRange:F2},\"progress\":{progress:F3},\"courseStartDistance\":{courseStartDistance:F2},\"currentDistance\":{currentDistance:F2}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
        }
        // #endregion
        
        if (heightRange < 0.1f) heightRange = 20f; // Default range

        // Update each segment to create undulating effect
        float segmentWidth = previewBarWidth / previewBarSegmentCount;
        float baseY = previewBarHeight * 0.5f; // Center of bar

        for (int i = 0; i < previewBarSegments.Count && i < courseHeights.Length; i++)
        {
            Image segment = previewBarSegments[i];
            if (segment == null) continue;

            RectTransform segmentRect = segment.GetComponent<RectTransform>();
            if (segmentRect == null) continue;

            // Calculate segment height based on elevation
            float normalizedHeight = 0f;
            if (heightRange > 0.01f)
            {
                normalizedHeight = (courseHeights[i] - minHeight) / heightRange;
            }

            // Calculate Y position (undulation)
            float segmentY = baseY + (normalizedHeight - 0.5f) * undulationAmplitude;

            // Update segment position and size
            segmentRect.anchoredPosition = new Vector2(i * segmentWidth, segmentY - (previewBarHeight * 0.5f));
            segmentRect.sizeDelta = new Vector2(segmentWidth, previewBarHeight);

            // Set color based on gradient (optional - can make it more vibrant)
            float gradient = courseGradients[i];
            Color segmentColor = previewBarColor;
            if (gradient > 5f)
            {
                segmentColor = Color.Lerp(previewBarColor, new Color(1f, 0.5f, 0f), 0.5f); // Orange for steep
            }
            else if (gradient > 10f)
            {
                segmentColor = Color.Lerp(previewBarColor, Color.red, 0.5f); // Red for very steep
            }
            segment.color = segmentColor;
        }

        // Update position marker
        UpdatePositionMarker(progress);
    }

    /// <summary>
    /// Update position marker (blue pin) on preview bar
    /// </summary>
    private void UpdatePositionMarker(float progress)
    {
        if (positionMarker == null) return;

        // Calculate X position based on progress
        float markerX = progress * previewBarWidth;
        positionMarker.anchoredPosition = new Vector2(markerX, previewBarHeight * 0.5f + 20f); // Above the bar

        // Set color
        if (positionMarkerImage != null)
        {
            positionMarkerImage.color = positionMarkerColor;
        }
    }


    /// <summary>
    /// Reset incline indicator (call when starting a new trial)
    /// </summary>
    public void ResetIndicator()
    {
        if (bikeController != null)
        {
            courseStartDistance = bikeController.GetDistance();
        }
        UpdateInclineDisplay();
    }
}
