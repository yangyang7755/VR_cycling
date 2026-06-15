using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Zwift-style unified elevation progress bar
/// Shows elevation profile with current position marker
/// Progress moves along the actual terrain elevation
/// Color-coded by gradient: Green (downhill) -> Yellow (flat) -> Orange (moderate) -> Red (steep)
/// </summary>
public class ElevationProgressBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Terrain terrain;
    
    [Header("UI Elements")]
    [SerializeField] private RectTransform barContainer;
    [SerializeField] private RectTransform segmentsParent;
    [SerializeField] private RectTransform currentPositionMarker;
    [SerializeField] private Image markerImage;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private TextMeshProUGUI currentGradientText;
    [SerializeField] private TextMeshProUGUI elevationText;
    
    [Header("Bar Settings")]
    [SerializeField] private float barWidth = 600f;
    [SerializeField] private float barHeight = 80f;
    [SerializeField] private int segmentCount = 150;
    [SerializeField] private float totalRouteLength = 500f; // Total route in meters
    
    [Header("Visual Settings")]
    [SerializeField] private float minSegmentHeight = 15f;
    [SerializeField] private float maxSegmentHeight = 70f;
    [SerializeField] private float elevationExaggeration = 2f; // Exaggerate elevation changes for visibility
    [SerializeField] private float markerSize = 16f; // Size of position marker
    [SerializeField] private bool showMarkerGlow = true;
    
    [Header("Colors")]
    [SerializeField] private Color downhillColor = new Color(0.2f, 0.7f, 0.9f); // Blue
    [SerializeField] private Color flatColor = new Color(0.3f, 0.8f, 0.3f); // Green
    [SerializeField] private Color moderateColor = new Color(1f, 0.85f, 0.1f); // Yellow
    [SerializeField] private Color steepColor = new Color(1f, 0.4f, 0.1f); // Orange
    [SerializeField] private Color verysteepColor = new Color(0.9f, 0.1f, 0.1f); // Red
    [SerializeField] private Color markerColor = Color.white;
    [SerializeField] private Color completedTint = new Color(0.5f, 0.5f, 0.5f, 0.6f); // Darken completed sections
    
    [Header("Gradient Thresholds")]
    [SerializeField] private float moderateThreshold = 3f; // 3% = moderate
    [SerializeField] private float steepThreshold = 6f; // 6% = steep
    [SerializeField] private float verySteepThreshold = 9f; // 9% = very steep
    
    [Header("Distance Markers")]
    [SerializeField] private bool showDistanceMarkers = true;
    [SerializeField] private float distanceMarkerInterval = 100f; // Every 100m
    [SerializeField] private GameObject distanceMarkerPrefab;
    [SerializeField] private RectTransform markersParent;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool logMarkerPosition = false;
    
    private List<Image> segments = new List<Image>();
    private List<GameObject> distanceMarkers = new List<GameObject>();
    private float[] gradients;
    private float[] heights;
    private float[] distances; // Distance for each segment
    private float updateInterval = 0.1f;
    private float lastUpdateTime = 0f;
    private float courseStartDistance = 0f;
    private float minHeight = 0f;
    private float maxHeight = 0f;

    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null || terrain == null)
        {
            Debug.LogError("[ElevationProgressBar] BikeController or Terrain not found!");
            enabled = false;
            return;
        }
        
        courseStartDistance = bikeController.GetDistance();
        
        InitializeBar();
        SampleEntireRoute();
        CreateDistanceMarkers();
        
        // Hidden by default — only shown during HillClimb phase
        if (barContainer != null)
            barContainer.gameObject.SetActive(false);
    }

    private void InitializeBar()
    {
        if (segmentsParent == null)
        {
            Debug.LogError("[ElevationProgressBar] Segments parent not assigned!");
            return;
        }
        
        // Clear existing segments
        foreach (Image seg in segments)
        {
            if (seg != null) Destroy(seg.gameObject);
        }
        segments.Clear();
        
        // Create segments
        float segmentWidth = barWidth / segmentCount;
        
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject segObj = new GameObject($"Segment_{i}");
            segObj.transform.SetParent(segmentsParent, false);
            
            RectTransform rect = segObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(segmentWidth, minSegmentHeight);
            rect.anchoredPosition = new Vector2(i * segmentWidth + segmentWidth * 0.5f, 0f);
            
            Image img = segObj.AddComponent<Image>();
            img.color = flatColor;
            segments.Add(img);
        }
        
        // Setup marker
        if (currentPositionMarker != null && markerImage != null)
        {
            markerImage.color = markerColor;
        }
    }

    private void SampleEntireRoute()
    {
        gradients = new float[segmentCount];
        heights = new float[segmentCount];
        distances = new float[segmentCount];
        
        float sampleInterval = totalRouteLength / segmentCount;
        
        // Priority 1: Use CurvedRouteGenerator's elevation profile (most accurate)
        CurvedRouteGenerator routeGen = FindObjectOfType<CurvedRouteGenerator>();
        if (routeGen != null && routeGen.GetElevationProfile() != null)
        {
            SampleFromElevationProfile(routeGen, sampleInterval);
        }
        else if (terrain != null)
        {
            // Priority 2: Sample along spline path
            SplinePath splinePath = FindObjectOfType<SplinePath>();
            if (splinePath != null && splinePath.GetTotalLength() > 0f)
            {
                SampleAlongSpline(splinePath, sampleInterval);
            }
            else
            {
                // Fallback: sample along X axis (straight road)
                SampleAlongXAxis(sampleInterval);
            }
        }
        else
        {
            // No terrain and no profile — set flat defaults
            for (int i = 0; i < segmentCount; i++)
            {
                heights[i] = 0f;
                gradients[i] = 0f;
                distances[i] = i * sampleInterval;
            }
            minHeight = 0f;
            maxHeight = 1f;
            return;
        }
        
        // Ensure we have some height variation for display
        float heightRange = maxHeight - minHeight;
        if (heightRange < 0.1f)
        {
            minHeight -= 5f;
            maxHeight += 5f;
        }
        
        // Update segment visuals
        UpdateSegmentVisuals();
        
        if (debugMode)
        {
            float minG = float.MaxValue, maxG = float.MinValue;
            for (int i = 0; i < segmentCount; i++)
            {
                if (gradients[i] < minG) minG = gradients[i];
                if (gradients[i] > maxG) maxG = gradients[i];
            }
            Debug.Log($"[ElevationProgressBar] Route: {courseStartDistance:F0}m to {courseStartDistance + totalRouteLength:F0}m");
            Debug.Log($"[ElevationProgressBar] Height: {minHeight:F1}m to {maxHeight:F1}m, Gradient: {minG:F1}% to {maxG:F1}%");
        }
    }
    
    private void SampleFromElevationProfile(CurvedRouteGenerator routeGen, float sampleInterval)
    {
        float[] profile = routeGen.GetElevationProfile();
        if (profile == null || profile.Length < 2)
        {
            Debug.LogWarning("[ElevationProgressBar] Elevation profile is null or too short, falling back to terrain sampling");
            SampleAlongXAxis(sampleInterval);
            return;
        }
        
        minHeight = float.MaxValue;
        maxHeight = float.MinValue;
        
        float profileLength = profile.Length - 1; // max valid index
        
        for (int i = 0; i < segmentCount; i++)
        {
            float distance = courseStartDistance + (i * sampleInterval);
            distances[i] = distance;
            
            // Sample height directly from profile array for accuracy
            int idx = Mathf.Clamp(Mathf.RoundToInt(distance), 0, (int)profileLength);
            heights[i] = profile[idx];
            
            if (heights[i] < minHeight) minHeight = heights[i];
            if (heights[i] > maxHeight) maxHeight = heights[i];
            
            // Gradient: difference between adjacent profile samples * 100 for percentage
            int gradIdx = Mathf.Clamp(Mathf.RoundToInt(distance), 1, (int)profileLength);
            gradients[i] = (profile[gradIdx] - profile[gradIdx - 1]) * 100f;
        }
        
        Debug.Log($"[ElevationProgressBar] Sampled from elevation profile: {segmentCount} segments, " +
                  $"height {minHeight:F1}m → {maxHeight:F1}m (rise={maxHeight-minHeight:F1}m), " +
                  $"courseStart={courseStartDistance:F0}m, routeLen={totalRouteLength:F0}m, " +
                  $"profileLen={profile.Length}, grad[50]={gradients[Mathf.Min(50, segmentCount-1)]:F2}%");
    }
    
    private void SampleAlongSpline(SplinePath splinePath, float sampleInterval)
    {
        minHeight = float.MaxValue;
        maxHeight = float.MinValue;
        
        for (int i = 0; i < segmentCount; i++)
        {
            float distance = courseStartDistance + (i * sampleInterval);
            distances[i] = distance;
            
            SplinePath.SplineSample sample = splinePath.SampleAtDistance(distance);
            float height = terrain.SampleHeight(sample.position);
            
            heights[i] = height;
            if (height < minHeight) minHeight = height;
            if (height > maxHeight) maxHeight = height;
            
            if (i > 0)
            {
                float heightDiff = height - heights[i - 1];
                float gradient = (heightDiff / sampleInterval) * 100f;
                gradients[i] = Mathf.Clamp(gradient, -15f, 15f);
            }
            else
            {
                gradients[i] = 0f;
            }
        }
    }
    
    private void SampleAlongXAxis(float sampleInterval)
    {
        Vector3 bikePos = Vector3.zero;
        if (bikeController != null && bikeController.gameObject != null)
        {
            bikePos = bikeController.transform.position;
        }
        float roadZ = bikePos.z;
        
        minHeight = float.MaxValue;
        maxHeight = float.MinValue;
        
        for (int i = 0; i < segmentCount; i++)
        {
            float distance = courseStartDistance + (i * sampleInterval);
            distances[i] = distance;
            
            Vector3 pos = new Vector3(distance, 0f, roadZ);
            float height = terrain.SampleHeight(pos);
            
            heights[i] = height;
            if (height < minHeight) minHeight = height;
            if (height > maxHeight) maxHeight = height;
            
            if (i > 0)
            {
                float heightDiff = height - heights[i - 1];
                float gradient = (heightDiff / sampleInterval) * 100f;
                gradients[i] = Mathf.Clamp(gradient, -15f, 15f);
            }
            else
            {
                gradients[i] = 0f;
            }
        }
    }

    private void UpdateSegmentVisuals()
    {
        float segmentWidth = barWidth / segmentCount;
        float heightRange = maxHeight - minHeight;
        if (heightRange < 0.1f) heightRange = 10f;
        
        // Debug: Log gradient range
        if (debugMode && gradients != null && gradients.Length > 0)
        {
            float minGrad = float.MaxValue;
            float maxGrad = float.MinValue;
            float avgGrad = 0f;
            for (int i = 0; i < gradients.Length; i++)
            {
                if (gradients[i] < minGrad) minGrad = gradients[i];
                if (gradients[i] > maxGrad) maxGrad = gradients[i];
                avgGrad += gradients[i];
            }
            avgGrad /= gradients.Length;
            Debug.Log($"[ElevationProgressBar] Gradient range: {minGrad:F2}% to {maxGrad:F2}%, Average: {avgGrad:F2}%");
        }
        
        for (int i = 0; i < segments.Count && i < segmentCount; i++)
        {
            Image seg = segments[i];
            RectTransform rect = seg.GetComponent<RectTransform>();
            
            // Calculate height based on elevation with exaggeration
            float normalizedHeight = (heights[i] - minHeight) / heightRange;
            normalizedHeight = Mathf.Pow(normalizedHeight, 1f / elevationExaggeration); // Apply exaggeration
            float segHeight = Mathf.Lerp(minSegmentHeight, maxSegmentHeight, normalizedHeight);
            
            // Set color based on gradient
            Color color = GetGradientColor(gradients[i]);
            
            rect.sizeDelta = new Vector2(segmentWidth, segHeight);
            seg.color = color;
        }
    }

    private void CreateDistanceMarkers()
    {
        if (!showDistanceMarkers || markersParent == null) return;
        
        // Clear existing markers
        foreach (GameObject marker in distanceMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        distanceMarkers.Clear();
        
        // Create markers at intervals
        int markerCount = Mathf.FloorToInt(totalRouteLength / distanceMarkerInterval);
        
        for (int i = 0; i <= markerCount; i++)
        {
            float markerDistance = i * distanceMarkerInterval;
            float normalizedPosition = markerDistance / totalRouteLength;
            float xPos = normalizedPosition * barWidth;
            
            GameObject markerObj;
            if (distanceMarkerPrefab != null)
            {
                markerObj = Instantiate(distanceMarkerPrefab, markersParent);
            }
            else
            {
                // Create simple marker
                markerObj = new GameObject($"Marker_{markerDistance}m");
                markerObj.transform.SetParent(markersParent, false);
                
                // Add vertical line
                GameObject line = new GameObject("Line");
                line.transform.SetParent(markerObj.transform, false);
                RectTransform lineRect = line.AddComponent<RectTransform>();
                lineRect.sizeDelta = new Vector2(2f, barHeight + 10f);
                lineRect.anchoredPosition = Vector2.zero;
                Image lineImg = line.AddComponent<Image>();
                lineImg.color = new Color(1f, 1f, 1f, 0.3f);
                
                // Add text label
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(markerObj.transform, false);
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.sizeDelta = new Vector2(60f, 20f);
                textRect.anchoredPosition = new Vector2(0f, -15f);
                TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
                text.text = $"{markerDistance:F0}m";
                text.fontSize = 12;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(1f, 1f, 1f, 0.7f);
            }
            
            RectTransform markerRect = markerObj.GetComponent<RectTransform>();
            if (markerRect == null)
                markerRect = markerObj.AddComponent<RectTransform>();
            
            markerRect.anchorMin = new Vector2(0f, 0f);
            markerRect.anchorMax = new Vector2(0f, 0f);
            markerRect.pivot = new Vector2(0.5f, 0f);
            markerRect.anchoredPosition = new Vector2(xPos, 0f);
            
            distanceMarkers.Add(markerObj);
        }
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (bikeController == null) return;
        
        // Get current distance
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - courseStartDistance;
        distanceFromStart = Mathf.Clamp(distanceFromStart, 0f, totalRouteLength);
        
        // Calculate progress (0 to 1)
        float progress = totalRouteLength > 0f ? distanceFromStart / totalRouteLength : 0f;
        
        // Update marker position
        if (currentPositionMarker != null)
        {
            float xPos = progress * barWidth;
            
            // Get current elevation for marker Y position
            int currentSegmentIndex = Mathf.FloorToInt(progress * segmentCount);
            currentSegmentIndex = Mathf.Clamp(currentSegmentIndex, 0, segmentCount - 1);
            
            float currentHeight = heights[currentSegmentIndex];
            float normalizedHeight = (currentHeight - minHeight) / (maxHeight - minHeight);
            normalizedHeight = Mathf.Pow(normalizedHeight, 1f / elevationExaggeration);
            float yPos = Mathf.Lerp(minSegmentHeight, maxSegmentHeight, normalizedHeight);
            
            currentPositionMarker.anchoredPosition = new Vector2(xPos, yPos);
            
            // Debug logging
            if (debugMode && logMarkerPosition && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[ElevationProgressBar] Marker: ({xPos:F1}, {yPos:F1}) | Progress: {progress:P0} | Distance: {distanceFromStart:F0}/{totalRouteLength:F0}m");
            }
            
            // Ensure marker is visible
            if (markerImage != null)
            {
                markerImage.enabled = true;
                // Pulse effect for visibility
                if (debugMode)
                {
                    float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 3f);
                    markerImage.transform.localScale = Vector3.one * pulse;
                }
            }
        }
        
        // Darken completed segments
        for (int i = 0; i < segments.Count; i++)
        {
            float segmentProgress = (float)i / segmentCount;
            if (segmentProgress <= progress)
            {
                // Completed - apply tint
                Color originalColor = GetGradientColor(gradients[i]);
                segments[i].color = originalColor * completedTint;
            }
            else
            {
                // Not yet reached - full color
                segments[i].color = GetGradientColor(gradients[i]);
            }
        }
        
        // Update distance text
        if (distanceText != null)
        {
            float remainingDistance = totalRouteLength - distanceFromStart;
            distanceText.text = $"{distanceFromStart:F0}m / {totalRouteLength:F0}m";
        }
        
        // Update current gradient text
        if (currentGradientText != null)
        {
            float gradient = bikeController.GetGradient();
            string sign = gradient >= 0 ? "+" : "";
            currentGradientText.text = $"{sign}{gradient:F1}%";
        }
        
        // Update elevation text
        if (elevationText != null)
        {
            int currentSegmentIndex = Mathf.FloorToInt(progress * segmentCount);
            currentSegmentIndex = Mathf.Clamp(currentSegmentIndex, 0, segmentCount - 1);
            float currentElevation = heights[currentSegmentIndex];
            elevationText.text = $"{currentElevation:F0}m";
        }
    }

    private Color GetGradientColor(float gradient)
    {
        float absGradient = Mathf.Abs(gradient);
        
        if (gradient < -1f)
        {
            // Downhill - blue, intensity by steepness
            float t = Mathf.Clamp01(Mathf.Abs(gradient) / 8f);
            return Color.Lerp(flatColor, downhillColor, t);
        }
        else if (absGradient < moderateThreshold)
        {
            // Flat to moderate - green to yellow
            float t = absGradient / moderateThreshold;
            return Color.Lerp(flatColor, moderateColor, t);
        }
        else if (absGradient < steepThreshold)
        {
            // Moderate to steep - yellow to orange
            float t = (absGradient - moderateThreshold) / (steepThreshold - moderateThreshold);
            return Color.Lerp(moderateColor, steepColor, t);
        }
        else if (absGradient < verySteepThreshold)
        {
            // Steep to very steep - orange to red
            float t = (absGradient - steepThreshold) / (verySteepThreshold - steepThreshold);
            return Color.Lerp(steepColor, verysteepColor, t);
        }
        else
        {
            // Very steep - solid red
            return verysteepColor;
        }
    }

    /// <summary>
    /// Reset progress bar (call when starting a new trial).
    /// Forces immediate re-sampling from the CurvedRouteGenerator elevation profile.
    /// </summary>
    public void ResetProgress()
    {
        if (bikeController != null)
        {
            courseStartDistance = bikeController.GetDistance();
        }
        else
        {
            courseStartDistance = 0f;
        }
        
        // Force-find the route generator and check its profile
        CurvedRouteGenerator routeGen = FindObjectOfType<CurvedRouteGenerator>();
        if (routeGen != null)
        {
            float[] profile = routeGen.GetElevationProfile();
            float routeLen = routeGen.GetRouteLength();
            
            if (profile != null && profile.Length > 1 && routeLen > 0f)
            {
                totalRouteLength = routeLen;
                Debug.Log($"[ElevationProgressBar] ResetProgress: route={routeLen:F0}m, profile={profile.Length} samples, " +
                          $"courseStart={courseStartDistance:F0}m");
            }
            else
            {
                Debug.LogWarning($"[ElevationProgressBar] ResetProgress: profile is null or empty! " +
                                 $"profile={(profile != null ? profile.Length.ToString() : "null")}, routeLen={routeLen:F0}");
            }
        }
        else
        {
            Debug.LogWarning("[ElevationProgressBar] ResetProgress: No CurvedRouteGenerator found!");
        }
        
        SampleEntireRoute();
        CreateDistanceMarkers();
        UpdateSegmentVisuals();
        UpdateProgress();
    }

    /// <summary>
    /// Set total route length and resample immediately.
    /// Called from CurvedRouteGenerator.NotifyTerrainChanged() right after
    /// the elevation profile has been generated.
    /// </summary>
    public void SetTotalRouteLength(float length)
    {
        totalRouteLength = length;
        
        // Reset course start distance — the bike will be reset to 0 for the new trial
        courseStartDistance = 0f;
        
        // Ensure segments exist (in case this is called before Start)
        if (segments == null || segments.Count == 0)
        {
            if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
            if (terrain == null) terrain = FindObjectOfType<Terrain>();
            InitializeBar();
        }
        
        // Sample immediately — the elevation profile exists right now
        SampleEntireRoute();
        CreateDistanceMarkers();
        UpdateSegmentVisuals();
        UpdateProgress();
        
        Debug.Log($"[ElevationProgressBar] SetTotalRouteLength({length:F0}m) — resampled, segments={segments.Count}");
    }
    
    /// <summary>
    /// Force resample of terrain (call after terrain generation)
    /// </summary>
    public void ResampleTerrain()
    {
        SampleEntireRoute();
        CreateDistanceMarkers();
        UpdateSegmentVisuals();
        UpdateProgress();
    }
}
