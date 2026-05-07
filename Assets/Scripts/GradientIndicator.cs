using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Zwift-style gradient indicator showing current slope with color coding
/// Includes a mini elevation profile showing upcoming undulations
/// Green (flat) -> Yellow (moderate) -> Red (steep)
/// </summary>
public class GradientIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Terrain terrain;
    
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI gradientText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image arrowImage;
    
    [Header("Undulation Preview")]
    [Tooltip("Show mini elevation profile of upcoming terrain")]
    [SerializeField] private bool showUndulationPreview = true;
    
    [Tooltip("Parent RectTransform for undulation bars")]
    [SerializeField] private RectTransform undulationParent;
    
    [Tooltip("Preview distance ahead (meters)")]
    [SerializeField] private float previewDistance = 150f;
    
    [Tooltip("Number of preview segments")]
    [SerializeField] private int previewSegments = 30;
    
    [Tooltip("Preview bar width")]
    [SerializeField] private float previewWidth = 200f;
    
    [Tooltip("Preview bar height")]
    [SerializeField] private float previewHeight = 40f;
    
    [Header("Color Settings")]
    [SerializeField] private Color flatColor = new Color(0.2f, 0.8f, 0.2f); // Green
    [SerializeField] private Color moderateColor = new Color(1f, 0.8f, 0f); // Yellow/Orange
    [SerializeField] private Color steepColor = new Color(1f, 0.2f, 0.2f); // Red
    [SerializeField] private Color downhillColor = new Color(0.3f, 0.6f, 1f); // Blue
    
    [Header("Gradient Thresholds")]
    [SerializeField] private float moderateThreshold = 3f; // 3% = moderate
    [SerializeField] private float steepThreshold = 7f; // 7% = steep
    
    [Header("Display Settings")]
    [SerializeField] private bool showPlusSign = true;
    [SerializeField] private bool animateOnChange = true;
    [SerializeField] private float updateInterval = 0.2f;
    
    private float lastGradient = 0f;
    private float lastUpdateTime = 0f;
    private float animationTime = 0f;
    private List<Image> undulationBars = new List<Image>();
    private bool undulationInitialized = false;

    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null)
        {
            Debug.LogError("[GradientIndicator] BikeController not found!");
            enabled = false;
            return;
        }
        
        if (showUndulationPreview)
        {
            InitializeUndulationPreview();
        }
    }
    
    private void InitializeUndulationPreview()
    {
        // Create undulation parent if not assigned
        if (undulationParent == null)
        {
            GameObject parentObj = new GameObject("UndulationPreview");
            parentObj.transform.SetParent(transform, false);
            undulationParent = parentObj.AddComponent<RectTransform>();
            undulationParent.anchorMin = new Vector2(0.5f, 0f);
            undulationParent.anchorMax = new Vector2(0.5f, 0f);
            undulationParent.pivot = new Vector2(0.5f, 1f);
            undulationParent.anchoredPosition = new Vector2(0f, -5f);
            undulationParent.sizeDelta = new Vector2(previewWidth, previewHeight);
            
            // Add background
            Image bg = parentObj.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.4f);
        }
        
        // Create segment bars
        float segWidth = previewWidth / previewSegments;
        
        for (int i = 0; i < previewSegments; i++)
        {
            GameObject barObj = new GameObject($"UndBar_{i}");
            barObj.transform.SetParent(undulationParent, false);
            
            RectTransform rect = barObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(segWidth - 1f, previewHeight * 0.5f);
            rect.anchoredPosition = new Vector2(i * segWidth + segWidth * 0.5f, 2f);
            
            Image img = barObj.AddComponent<Image>();
            img.color = flatColor;
            undulationBars.Add(img);
        }
        
        undulationInitialized = true;
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        
        float currentGradient = bikeController.GetGradient();
        UpdateDisplay(currentGradient);
        
        if (showUndulationPreview && undulationInitialized && terrain != null)
        {
            UpdateUndulationPreview();
        }
        
        if (Mathf.Abs(currentGradient - lastGradient) > 0.5f && animateOnChange)
        {
            animationTime = 0.3f;
        }
        
        lastGradient = currentGradient;
    }
    
    private void UpdateUndulationPreview()
    {
        if (terrain == null || bikeController == null) return;
        
        float sampleInterval = previewDistance / previewSegments;
        float[] heights = new float[previewSegments];
        float[] grads = new float[previewSegments];
        float minH = float.MaxValue, maxH = float.MinValue;
        
        // Check if a SplinePath is available
        SplinePath splinePath = FindObjectOfType<SplinePath>();
        
        if (splinePath != null && splinePath.GetTotalLength() > 0f)
        {
            // Sample along spline path
            float currentDist = bikeController.GetDistance();
            for (int i = 0; i < previewSegments; i++)
            {
                float dist = currentDist + i * sampleInterval;
                SplinePath.SplineSample sample = splinePath.SampleAtDistance(dist);
                heights[i] = terrain.SampleHeight(sample.position);
                if (heights[i] < minH) minH = heights[i];
                if (heights[i] > maxH) maxH = heights[i];
                
                if (i > 0)
                {
                    float heightDiff = heights[i] - heights[i - 1];
                    grads[i] = (heightDiff / sampleInterval) * 100f;
                }
            }
        }
        else
        {
            // Fallback: sample along bike's forward direction
            Vector3 bikePos = bikeController.transform.position;
            Vector3 forward = bikeController.transform.forward;
            if (forward.magnitude < 0.1f) forward = Vector3.right;
            
            for (int i = 0; i < previewSegments; i++)
            {
                Vector3 samplePos = bikePos + forward * (i * sampleInterval);
                heights[i] = terrain.SampleHeight(samplePos);
                if (heights[i] < minH) minH = heights[i];
                if (heights[i] > maxH) maxH = heights[i];
                
                if (i > 0)
                {
                    float heightDiff = heights[i] - heights[i - 1];
                    grads[i] = (heightDiff / sampleInterval) * 100f;
                }
            }
        }
        
        grads[0] = bikeController.GetGradient();
        
        float heightRange = maxH - minH;
        if (heightRange < 0.5f) heightRange = 5f;
        
        float segWidth = previewWidth / previewSegments;
        
        // Update bars
        for (int i = 0; i < previewSegments && i < undulationBars.Count; i++)
        {
            float normalizedH = (heights[i] - minH) / heightRange;
            float barHeight = Mathf.Lerp(previewHeight * 0.15f, previewHeight * 0.85f, normalizedH);
            
            RectTransform rect = undulationBars[i].GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(segWidth - 1f, barHeight);
            
            // Color by gradient
            undulationBars[i].color = GetGradientColor(grads[i]);
        }
    }

    private void UpdateDisplay(float gradient)
    {
        // Update text
        if (gradientText != null)
        {
            string sign = "";
            if (gradient > 0.1f && showPlusSign)
                sign = "+";
            else if (gradient < -0.1f)
                sign = "";
            
            gradientText.text = $"{sign}{gradient:F1}%";
            
            // Animate scale if needed
            if (animationTime > 0f)
            {
                float scale = 1f + (animationTime * 0.5f);
                gradientText.transform.localScale = Vector3.one * scale;
                animationTime -= Time.deltaTime;
            }
            else
            {
                gradientText.transform.localScale = Vector3.one;
            }
        }
        
        // Update colors based on gradient
        Color targetColor = GetGradientColor(gradient);
        
        if (backgroundImage != null)
        {
            backgroundImage.color = targetColor;
        }
        
        if (gradientText != null)
        {
            gradientText.color = Color.white;
        }
        
        // Update arrow rotation (if exists)
        if (arrowImage != null)
        {
            float angle = Mathf.Clamp(gradient * -5f, -60f, 60f);
            arrowImage.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private Color GetGradientColor(float gradient)
    {
        float absGradient = Mathf.Abs(gradient);
        
        // Downhill (blue)
        if (gradient < -0.5f)
        {
            return downhillColor;
        }
        // Flat to moderate (green to yellow)
        else if (absGradient < moderateThreshold)
        {
            float t = absGradient / moderateThreshold;
            return Color.Lerp(flatColor, moderateColor, t);
        }
        // Moderate to steep (yellow to red)
        else if (absGradient < steepThreshold)
        {
            float t = (absGradient - moderateThreshold) / (steepThreshold - moderateThreshold);
            return Color.Lerp(moderateColor, steepColor, t);
        }
        // Very steep (red)
        else
        {
            return steepColor;
        }
    }
    
    public string GetGradientCategory(float gradient)
    {
        float absGradient = Mathf.Abs(gradient);
        
        if (gradient < -0.5f)
            return "Downhill";
        else if (absGradient < 1f)
            return "Flat";
        else if (absGradient < moderateThreshold)
            return "Rolling";
        else if (absGradient < steepThreshold)
            return "Moderate Climb";
        else if (absGradient < 10f)
            return "Steep Climb";
        else
            return "Very Steep!";
    }
}
