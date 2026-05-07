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
        SampleTerrain();
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
        if (terrain == null) return;
        
        gradients = new float[segmentCount];
        heights = new float[segmentCount];
        
        float currentDistance = bikeController.GetDistance();
        float sampleInterval = previewDistance / segmentCount;
        
        // Get road center Z (assuming road is along X axis)
        float roadZ = terrain.transform.position.z;
        
        // Sample terrain ahead
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        
        for (int i = 0; i < segmentCount; i++)
        {
            float distance = currentDistance + (i * sampleInterval);
            Vector3 pos = new Vector3(distance, 0f, roadZ);
            float height = terrain.SampleHeight(pos);
            
            heights[i] = height;
            if (height < minHeight) minHeight = height;
            if (height > maxHeight) maxHeight = height;
            
            // Calculate gradient
            Vector3 posAhead = new Vector3(distance + 5f, 0f, roadZ);
            float heightAhead = terrain.SampleHeight(posAhead);
            float gradient = ((heightAhead - height) / 5f) * 100f;
            gradients[i] = Mathf.Clamp(gradient, -12f, 12f);
        }
        
        // Normalize heights for visual display
        float heightRange = maxHeight - minHeight;
        if (heightRange < 0.1f) heightRange = 10f;
        
        // Update segment visuals
        float segmentWidth = barWidth / segmentCount;
        
        for (int i = 0; i < segments.Count && i < segmentCount; i++)
        {
            Image seg = segments[i];
            RectTransform rect = seg.GetComponent<RectTransform>();
            
            // Calculate height based on elevation
            float normalizedHeight = (heights[i] - minHeight) / heightRange;
            float segHeight = Mathf.Lerp(minSegmentHeight, maxSegmentHeight, normalizedHeight);
            
            // Set color based on gradient
            Color color = GetGradientColor(gradients[i]);
            
            rect.sizeDelta = new Vector2(segmentWidth, segHeight);
            seg.color = color;
        }
    }

    private void Update()
    {
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
        }
        
        // Update marker position (always at start since we're showing "ahead")
        if (currentPositionMarker != null)
        {
            currentPositionMarker.anchoredPosition = new Vector2(0f, barHeight * 0.5f);
            if (markerImage != null)
                markerImage.color = markerColor;
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
