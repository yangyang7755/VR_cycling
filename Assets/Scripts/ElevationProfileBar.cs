using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Zwift-style elevation profile bar showing upcoming terrain
/// Color-coded segments: Yellow (flat) -> Orange (moderate) -> Red (steep)
/// Current position marker moves along the bar
/// </summary>
public class ElevationProfileBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Terrain terrain;
    
    [Header("UI Elements")]
    [SerializeField] private RectTransform barContainer;
    [SerializeField] private RectTransform segmentsParent;
    [SerializeField] private RectTransform currentPositionMarker;
    [SerializeField] private Image markerImage;
    [SerializeField] private TextMeshProUGUI currentGradientText;
    
    [Header("Bar Settings")]
    [SerializeField] private float barWidth = 400f;
    [SerializeField] private float barHeight = 60f;
    [SerializeField] private int segmentCount = 100;
    [SerializeField] private float previewDistance = 200f; // meters ahead
    
    [Header("Visual Settings")]
    [SerializeField] private float minSegmentHeight = 10f;
    [SerializeField] private float maxSegmentHeight = 50f;
    [SerializeField] private Color flatColor = new Color(1f, 0.9f, 0.2f); // Yellow
    [SerializeField] private Color moderateColor = new Color(1f, 0.6f, 0.1f); // Orange
    [SerializeField] private Color steepColor = new Color(1f, 0.2f, 0.1f); // Red
    [SerializeField] private Color markerColor = Color.white;
    
    [Header("Gradient Thresholds")]
    [SerializeField] private float moderateThreshold = 4f; // 4% = moderate
    [SerializeField] private float steepThreshold = 8f; // 8% = steep
    
    private List<Image> segments = new List<Image>();
    private float[] gradients;
    private float[] heights;
    private float updateInterval = 0.5f;
    private float lastUpdateTime = 0f;
    private bool isVisible = false;

    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null || terrain == null)
        {
            Debug.LogError("[ElevationProfileBar] BikeController or Terrain not found!");
            enabled = false;
            return;
        }
        
        InitializeBar();
        
        // Hidden by default — shown when trial starts (HillClimb phase)
        Hide();
    }

    /// <summary>
    /// Show the progress bar (call when HillClimb phase starts)
    /// </summary>
    public void Show()
    {
        isVisible = true;
        gameObject.SetActive(true);
        if (barContainer != null)
            barContainer.gameObject.SetActive(true);
        
        // Also enable all children in case they were hidden
        foreach (Transform child in transform)
            child.gameObject.SetActive(true);
        
        SampleTerrain();
        Debug.Log("[ElevationProfileBar] SHOWN");
    }

    /// <summary>
    /// Hide the progress bar (call during menus, cue screens, etc.)
    /// </summary>
    public void Hide()
    {
        isVisible = false;
        if (barContainer != null)
            barContainer.gameObject.SetActive(false);
        else
        {
            foreach (Transform child in transform)
                child.gameObject.SetActive(false);
        }
    }

    private void InitializeBar()
    {
        if (segmentsParent == null)
        {
            Debug.LogError("[ElevationProfileBar] Segments parent not assigned!");
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
    }

    private void SampleTerrain()
    {
        gradients = new float[segmentCount];
        heights = new float[segmentCount];
        
        float currentDistance = bikeController.GetDistance();
        
        // Priority: Use CurvedRouteGenerator's elevation profile
        CurvedRouteGenerator routeGen = FindObjectOfType<CurvedRouteGenerator>();
        if (routeGen != null && routeGen.GetElevationProfile() != null)
        {
            float routeLength = routeGen.GetRouteLength();
            float sampleInterval = routeLength / segmentCount;
            
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            
            float[] profile = routeGen.GetElevationProfile();
            
            for (int i = 0; i < segmentCount; i++)
            {
                float distance = i * sampleInterval;
                int idx = Mathf.Clamp(Mathf.RoundToInt(distance), 0, profile.Length - 1);
                heights[i] = profile[idx];
                
                if (heights[i] < minHeight) minHeight = heights[i];
                if (heights[i] > maxHeight) maxHeight = heights[i];
                
                // Gradient from profile
                int gradIdx = Mathf.Clamp(Mathf.RoundToInt(distance), 1, profile.Length - 1);
                gradients[i] = (profile[gradIdx] - profile[gradIdx - 1]) * 100f;
            }
            
            // Normalize heights for visual display
            float heightRange = maxHeight - minHeight;
            if (heightRange < 0.1f) heightRange = 10f;
            
            float segmentWidth = barWidth / segmentCount;
            
            for (int i = 0; i < segments.Count && i < segmentCount; i++)
            {
                Image seg = segments[i];
                RectTransform rect = seg.GetComponent<RectTransform>();
                
                float normalizedHeight = (heights[i] - minHeight) / heightRange;
                float segHeight = Mathf.Lerp(minSegmentHeight, maxSegmentHeight, normalizedHeight);
                
                Color color = GetGradientColor(gradients[i]);
                
                // Darken segments the bike has passed
                float segDist = (float)i / segmentCount * routeLength;
                if (segDist < currentDistance)
                    color *= new Color(0.6f, 0.6f, 0.6f, 1f);
                
                rect.sizeDelta = new Vector2(segmentWidth, segHeight);
                seg.color = color;
            }
            
            // Update marker position along the bar
            if (currentPositionMarker != null)
            {
                float progress = Mathf.Clamp01(currentDistance / routeLength);
                float xPos = progress * barWidth;
                
                int segIdx = Mathf.Clamp(Mathf.FloorToInt(progress * segmentCount), 0, segmentCount - 1);
                float normH = (heights[segIdx] - minHeight) / heightRange;
                float yPos = Mathf.Lerp(minSegmentHeight, maxSegmentHeight, normH);
                
                currentPositionMarker.anchoredPosition = new Vector2(xPos, yPos);
            }
            
            return;
        }
        
        // Fallback: sample terrain along X axis (old behavior)
        if (terrain == null) return;
        
        float sampleInt = previewDistance / segmentCount;
        float roadZ = terrain.transform.position.z;
        
        float fallbackMinHeight = float.MaxValue;
        float fallbackMaxHeight = float.MinValue;
        
        for (int i = 0; i < segmentCount; i++)
        {
            float distance = currentDistance + (i * sampleInt);
            Vector3 pos = new Vector3(distance, 0f, roadZ);
            float height = terrain.SampleHeight(pos);
            
            heights[i] = height;
            if (height < fallbackMinHeight) fallbackMinHeight = height;
            if (height > fallbackMaxHeight) fallbackMaxHeight = height;
            
            Vector3 posAhead = new Vector3(distance + 5f, 0f, roadZ);
            float heightAhead = terrain.SampleHeight(posAhead);
            float gradient = ((heightAhead - height) / 5f) * 100f;
            gradients[i] = Mathf.Clamp(gradient, -12f, 12f);
        }
        
        float fallbackHeightRange = fallbackMaxHeight - fallbackMinHeight;
        if (fallbackHeightRange < 0.1f) fallbackHeightRange = 10f;
        
        float fallbackSegWidth = barWidth / segmentCount;
        
        for (int i = 0; i < segments.Count && i < segmentCount; i++)
        {
            Image seg = segments[i];
            RectTransform rect = seg.GetComponent<RectTransform>();
            
            float normalizedHeight = (heights[i] - fallbackMinHeight) / fallbackHeightRange;
            float segHeight = Mathf.Lerp(minSegmentHeight, maxSegmentHeight, normalizedHeight);
            
            Color color = GetGradientColor(gradients[i]);
            
            rect.sizeDelta = new Vector2(fallbackSegWidth, segHeight);
            seg.color = color;
        }
        
        if (currentPositionMarker != null)
        {
            currentPositionMarker.anchoredPosition = new Vector2(0f, barHeight * 0.5f);
        }
    }

    private void Update()
    {
        if (!isVisible) return;
        
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        
        // Resample terrain as cyclist moves
        SampleTerrain();
        
        // Update current gradient text
        if (currentGradientText != null && bikeController != null)
        {
            float gradient = bikeController.GetGradient();
            string sign = gradient >= 0 ? "+" : "";
            currentGradientText.text = $"{sign}{gradient:F1}%";
            
            // Color code the gradient text
            if (Mathf.Abs(gradient) < 2f)
                currentGradientText.color = new Color(0.3f, 0.9f, 0.3f); // green
            else if (Mathf.Abs(gradient) < 5f)
                currentGradientText.color = new Color(1f, 0.85f, 0.1f); // yellow
            else if (Mathf.Abs(gradient) < 8f)
                currentGradientText.color = new Color(1f, 0.5f, 0.1f); // orange
            else
                currentGradientText.color = new Color(1f, 0.2f, 0.1f); // red
        }
    }

    private Color GetGradientColor(float gradient)
    {
        float absGradient = Mathf.Abs(gradient);
        
        if (gradient < -0.5f)
        {
            // Downhill - lighter yellow
            return Color.Lerp(flatColor, new Color(0.8f, 1f, 0.5f), 0.5f);
        }
        else if (absGradient < moderateThreshold)
        {
            // Flat to moderate - yellow to orange
            float t = absGradient / moderateThreshold;
            return Color.Lerp(flatColor, moderateColor, t);
        }
        else if (absGradient < steepThreshold)
        {
            // Moderate to steep - orange to red
            float t = (absGradient - moderateThreshold) / (steepThreshold - moderateThreshold);
            return Color.Lerp(moderateColor, steepColor, t);
        }
        else
        {
            // Very steep - red
            return steepColor;
        }
    }
}
