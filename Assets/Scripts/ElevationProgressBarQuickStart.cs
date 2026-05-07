using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quick setup helper - attach this to an empty GameObject to auto-create the elevation progress bar UI
/// This will create all necessary UI elements programmatically
/// After running once, you can remove this component and customize the created UI
/// </summary>
public class ElevationProgressBarQuickStart : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Terrain terrain;
    
    [Header("Settings")]
    [SerializeField] private float totalRouteLength = 500f;
    [SerializeField] private bool createOnStart = true;
    
    [Header("Position")]
    [SerializeField] private float bottomOffset = 100f;
    [SerializeField] private float barWidth = 600f;
    [SerializeField] private float barHeight = 100f;
    
    [Header("Debug")]
    [SerializeField] private bool alreadyCreated = false;

    private void Start()
    {
        if (createOnStart && !alreadyCreated)
        {
            CreateElevationProgressBar();
            alreadyCreated = true;
        }
    }

    [ContextMenu("Create Elevation Progress Bar")]
    public void CreateElevationProgressBar()
    {
        // Find Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas found! Please create a Canvas first.");
            return;
        }
        
        // Auto-find references if not assigned
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null || terrain == null)
        {
            Debug.LogError("BikeController or Terrain not found!");
            return;
        }
        
        // Create main container
        GameObject mainObj = new GameObject("ElevationProgressBar");
        mainObj.transform.SetParent(canvas.transform, false);
        
        RectTransform mainRect = mainObj.AddComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0.5f, 0f);
        mainRect.anchorMax = new Vector2(0.5f, 0f);
        mainRect.pivot = new Vector2(0.5f, 0f);
        mainRect.anchoredPosition = new Vector2(0f, bottomOffset);
        mainRect.sizeDelta = new Vector2(barWidth, barHeight);
        
        // Add background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(mainObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.6f);
        
        // Create segments parent
        GameObject segmentsObj = new GameObject("SegmentsParent");
        segmentsObj.transform.SetParent(mainObj.transform, false);
        RectTransform segmentsRect = segmentsObj.AddComponent<RectTransform>();
        segmentsRect.anchorMin = new Vector2(0f, 0f);
        segmentsRect.anchorMax = new Vector2(0f, 0f);
        segmentsRect.pivot = new Vector2(0f, 0f);
        segmentsRect.anchoredPosition = new Vector2(10f, 10f);
        segmentsRect.sizeDelta = new Vector2(barWidth - 20f, barHeight - 30f);
        
        // Create markers parent
        GameObject markersObj = new GameObject("MarkersParent");
        markersObj.transform.SetParent(mainObj.transform, false);
        RectTransform markersRect = markersObj.AddComponent<RectTransform>();
        markersRect.anchorMin = new Vector2(0f, 0f);
        markersRect.anchorMax = new Vector2(0f, 0f);
        markersRect.pivot = new Vector2(0f, 0f);
        markersRect.anchoredPosition = new Vector2(10f, 10f);
        markersRect.sizeDelta = new Vector2(barWidth - 20f, barHeight - 30f);
        
        // Create current position marker
        GameObject markerObj = new GameObject("CurrentPositionMarker");
        markerObj.transform.SetParent(segmentsObj.transform, false);
        RectTransform markerRect = markerObj.AddComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0f, 0f);
        markerRect.anchorMax = new Vector2(0f, 0f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(16f, 16f);
        markerRect.anchoredPosition = Vector2.zero;
        
        Image markerImage = markerObj.AddComponent<Image>();
        markerImage.color = Color.white;
        // Create a simple circle sprite
        markerImage.sprite = CreateCircleSprite();
        
        // Add outline to marker for visibility
        Outline markerOutline = markerObj.AddComponent<Outline>();
        markerOutline.effectColor = Color.black;
        markerOutline.effectDistance = new Vector2(2f, 2f);
        
        // Add glow effect to marker
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(markerObj.transform, false);
        RectTransform glowRect = glowObj.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-6f, -6f);
        glowRect.offsetMax = new Vector2(6f, 6f);
        Image glowImage = glowObj.AddComponent<Image>();
        glowImage.color = new Color(1f, 1f, 0f, 0.6f); // Yellow glow
        glowImage.sprite = CreateCircleSprite();
        glowImage.raycastTarget = false;
        
        // Create info panel
        GameObject infoObj = new GameObject("InfoPanel");
        infoObj.transform.SetParent(mainObj.transform, false);
        RectTransform infoRect = infoObj.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 1f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.pivot = new Vector2(0.5f, 0f);
        infoRect.anchoredPosition = new Vector2(0f, 5f);
        infoRect.sizeDelta = new Vector2(0f, 20f);
        
        // Distance text (left)
        GameObject distTextObj = new GameObject("DistanceText");
        distTextObj.transform.SetParent(infoObj.transform, false);
        RectTransform distRect = distTextObj.AddComponent<RectTransform>();
        distRect.anchorMin = new Vector2(0f, 0.5f);
        distRect.anchorMax = new Vector2(0f, 0.5f);
        distRect.pivot = new Vector2(0f, 0.5f);
        distRect.anchoredPosition = new Vector2(10f, 0f);
        distRect.sizeDelta = new Vector2(200f, 20f);
        TextMeshProUGUI distText = distTextObj.AddComponent<TextMeshProUGUI>();
        distText.text = "0m / 500m";
        distText.fontSize = 14;
        distText.color = Color.white;
        distText.alignment = TextAlignmentOptions.Left;
        
        // Gradient text (center)
        GameObject gradTextObj = new GameObject("GradientText");
        gradTextObj.transform.SetParent(infoObj.transform, false);
        RectTransform gradRect = gradTextObj.AddComponent<RectTransform>();
        gradRect.anchorMin = new Vector2(0.5f, 0.5f);
        gradRect.anchorMax = new Vector2(0.5f, 0.5f);
        gradRect.pivot = new Vector2(0.5f, 0.5f);
        gradRect.anchoredPosition = Vector2.zero;
        gradRect.sizeDelta = new Vector2(100f, 20f);
        TextMeshProUGUI gradText = gradTextObj.AddComponent<TextMeshProUGUI>();
        gradText.text = "0.0%";
        gradText.fontSize = 16;
        gradText.fontStyle = FontStyles.Bold;
        gradText.color = Color.yellow;
        gradText.alignment = TextAlignmentOptions.Center;
        
        // Elevation text (right)
        GameObject elevTextObj = new GameObject("ElevationText");
        elevTextObj.transform.SetParent(infoObj.transform, false);
        RectTransform elevRect = elevTextObj.AddComponent<RectTransform>();
        elevRect.anchorMin = new Vector2(1f, 0.5f);
        elevRect.anchorMax = new Vector2(1f, 0.5f);
        elevRect.pivot = new Vector2(1f, 0.5f);
        elevRect.anchoredPosition = new Vector2(-10f, 0f);
        elevRect.sizeDelta = new Vector2(100f, 20f);
        TextMeshProUGUI elevText = elevTextObj.AddComponent<TextMeshProUGUI>();
        elevText.text = "0m";
        elevText.fontSize = 14;
        elevText.color = Color.white;
        elevText.alignment = TextAlignmentOptions.Right;
        
        // Add the ElevationProgressBar component
        ElevationProgressBar progressBar = mainObj.AddComponent<ElevationProgressBar>();
        
        // Use reflection to set private fields (since they're SerializeField)
        var type = typeof(ElevationProgressBar);
        
        SetPrivateField(progressBar, "bikeController", bikeController);
        SetPrivateField(progressBar, "terrain", terrain);
        SetPrivateField(progressBar, "barContainer", mainRect);
        SetPrivateField(progressBar, "segmentsParent", segmentsRect);
        SetPrivateField(progressBar, "currentPositionMarker", markerRect);
        SetPrivateField(progressBar, "markerImage", markerImage);
        SetPrivateField(progressBar, "distanceText", distText);
        SetPrivateField(progressBar, "currentGradientText", gradText);
        SetPrivateField(progressBar, "elevationText", elevText);
        SetPrivateField(progressBar, "markersParent", markersRect);
        SetPrivateField(progressBar, "barWidth", barWidth - 20f);
        SetPrivateField(progressBar, "barHeight", barHeight - 30f);
        SetPrivateField(progressBar, "totalRouteLength", totalRouteLength);
        
        Debug.Log("✓ Elevation Progress Bar created successfully!");
        Debug.Log("You can now customize the appearance in the Inspector.");
        Debug.Log("Consider removing the QuickStart component after setup.");
    }
    
    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            field.SetValue(obj, value);
        }
    }
    
    private Sprite CreateCircleSprite()
    {
        // Create a simple circle texture
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
                    pixels[y * size + x] = Color.white;
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
}
