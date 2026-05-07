using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple position marker for existing progress bars
/// Just shows where you are on the route with a visible marker
/// Attach this to your existing progress bar
/// </summary>
public class SimpleProgressMarker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private RectTransform progressBarRect; // Your existing progress bar
    
    [Header("Marker Settings")]
    [SerializeField] private GameObject markerPrefab; // Optional: use your own marker
    [SerializeField] private float markerSize = 20f;
    [SerializeField] private Color markerColor = Color.white;
    [SerializeField] private bool showGlow = true;
    [SerializeField] private Color glowColor = Color.yellow;
    
    [Header("Route Settings")]
    [SerializeField] private float totalRouteLength = 500f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    private GameObject marker;
    private RectTransform markerRect;
    private Image markerImage;
    private float courseStartDistance = 0f;

    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (progressBarRect == null)
        {
            // Try to find progress bar
            var progressBar = FindObjectOfType<DistanceProgressBar>();
            if (progressBar != null)
            {
                progressBarRect = progressBar.GetComponent<RectTransform>();
            }
        }
        
        if (bikeController == null || progressBarRect == null)
        {
            Debug.LogError("[SimpleProgressMarker] BikeController or Progress Bar not found!");
            enabled = false;
            return;
        }
        
        courseStartDistance = bikeController.GetDistance();
        CreateMarker();
    }

    private void CreateMarker()
    {
        if (markerPrefab != null)
        {
            // Use provided prefab
            marker = Instantiate(markerPrefab, progressBarRect);
            markerRect = marker.GetComponent<RectTransform>();
            markerImage = marker.GetComponent<Image>();
        }
        else
        {
            // Create simple marker
            marker = new GameObject("PositionMarker");
            marker.transform.SetParent(progressBarRect, false);
            
            markerRect = marker.AddComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0f, 0.5f);
            markerRect.anchorMax = new Vector2(0f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(markerSize, markerSize);
            markerRect.anchoredPosition = Vector2.zero;
            
            markerImage = marker.AddComponent<Image>();
            markerImage.color = markerColor;
            markerImage.raycastTarget = false;
            
            // Try to use a circular sprite
            markerImage.sprite = CreateCircleSprite();
            
            // Add outline for visibility
            Outline outline = marker.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, 2f);
            
            // Add glow
            if (showGlow)
            {
                GameObject glow = new GameObject("Glow");
                glow.transform.SetParent(marker.transform, false);
                
                RectTransform glowRect = glow.AddComponent<RectTransform>();
                glowRect.anchorMin = Vector2.zero;
                glowRect.anchorMax = Vector2.one;
                glowRect.offsetMin = new Vector2(-8f, -8f);
                glowRect.offsetMax = new Vector2(8f, 8f);
                
                Image glowImage = glow.AddComponent<Image>();
                glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.6f);
                glowImage.sprite = markerImage.sprite;
                glowImage.raycastTarget = false;
            }
        }
        
        Debug.Log("[SimpleProgressMarker] Marker created successfully!");
    }

    private void Update()
    {
        if (marker == null || markerRect == null || bikeController == null)
            return;
        
        UpdateMarkerPosition();
    }

    private void UpdateMarkerPosition()
    {
        // Get current distance
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - courseStartDistance;
        distanceFromStart = Mathf.Clamp(distanceFromStart, 0f, totalRouteLength);
        
        // Calculate progress (0 to 1)
        float progress = totalRouteLength > 0f ? distanceFromStart / totalRouteLength : 0f;
        
        // Get progress bar width
        float barWidth = progressBarRect.rect.width;
        
        // Calculate marker X position
        float xPos = progress * barWidth;
        
        // Update marker position
        markerRect.anchoredPosition = new Vector2(xPos, 0f);
        
        // Debug
        if (debugMode && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[SimpleProgressMarker] Progress: {progress:P0} | Position: {xPos:F1} | Distance: {distanceFromStart:F0}/{totalRouteLength:F0}m");
        }
        
        // Pulse effect for visibility
        if (debugMode)
        {
            float pulse = 0.9f + 0.1f * Mathf.Sin(Time.time * 4f);
            marker.transform.localScale = Vector3.one * pulse;
        }
    }

    private Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                
                if (distance <= radius)
                {
                    // Smooth edges
                    float alpha = 1f - Mathf.Clamp01((distance - radius + 2f) / 2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Reset marker to start position
    /// </summary>
    public void ResetMarker()
    {
        if (bikeController != null)
        {
            courseStartDistance = bikeController.GetDistance();
        }
        
        if (markerRect != null)
        {
            markerRect.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// Set route length
    /// </summary>
    public void SetRouteLength(float length)
    {
        totalRouteLength = length;
    }
}
