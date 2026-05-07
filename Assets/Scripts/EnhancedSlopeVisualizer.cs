using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhances slope visibility with visual markers and better height transitions
/// Makes inclines more obvious to the user
/// </summary>
public class EnhancedSlopeVisualizer : MonoBehaviour
{
    [Header("Slope Visualization")]
    [Tooltip("Add visual markers along the road to show gradient")]
    [SerializeField] private bool addSlopeMarkers = true;
    
    [Tooltip("Distance between slope markers (meters)")]
    [Range(10f, 50f)]
    [SerializeField] private float markerSpacing = 20f;
    
    [Tooltip("Slope marker prefab (optional - will create simple markers if not set)")]
    [SerializeField] private GameObject slopeMarkerPrefab;
    
    [Header("Height Transitions")]
    [Tooltip("Add smooth height transitions for better visibility")]
    [SerializeField] private bool enhanceHeightTransitions = true;
    
    [Tooltip("Transition smoothness (higher = smoother)")]
    [Range(0.1f, 2f)]
    [SerializeField] private float transitionSmoothness = 1f;
    
    [Header("References")]
    [Tooltip("Terrain to enhance")]
    [SerializeField] private Terrain terrain;
    
    [Tooltip("SimpleTerrainBuilder reference")]
    [SerializeField] private SimpleTerrainBuilder terrainBuilder;

    private GameObject markersParent;
    private List<GameObject> slopeMarkers = new List<GameObject>();

    private void Start()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }
        
        if (terrainBuilder == null)
        {
            terrainBuilder = FindObjectOfType<SimpleTerrainBuilder>();
        }
    }

    /// <summary>
    /// Enhance slope visibility
    /// </summary>
    [ContextMenu("Enhance Slope Visibility")]
    public void EnhanceSlopeVisibility()
    {
        if (terrain == null || terrainBuilder == null)
        {
            Debug.LogError("[EnhancedSlopeVisualizer] Terrain or TerrainBuilder not found!");
            return;
        }

        if (enhanceHeightTransitions)
        {
            EnhanceHeightTransitions();
        }

        if (addSlopeMarkers)
        {
            CreateSlopeMarkers();
        }

        Debug.Log("[EnhancedSlopeVisualizer] ✓ Slope visibility enhanced");
    }

    private void EnhanceHeightTransitions()
    {
        TerrainData data = terrain.terrainData;
        int width = data.heightmapResolution;
        int height = data.heightmapResolution;
        Vector3 size = data.size;
        
        float[,] heights = data.GetHeights(0, 0, width, height);
        float currentGradient = terrainBuilder.GetGradient() / 100f; // Convert to decimal
        float visualMultiplier = 2.5f; // Default, should match SimpleTerrainBuilder
        
        // Get visual multiplier if possible
        var visualMultiplierField = typeof(SimpleTerrainBuilder).GetField("visualGradientMultiplier", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (visualMultiplierField != null)
        {
            visualMultiplier = (float)visualMultiplierField.GetValue(terrainBuilder);
        }
        
        float visualGradient = currentGradient * visualMultiplier;
        float visualHeightGain = size.x * visualGradient;
        
        // Apply smooth transitions
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalizedX = (float)x / (width - 1);
                float baseHeight = heights[x, z];
                
                // Calculate expected height at this position
                float expectedHeight = 0.1f + (normalizedX * visualHeightGain / size.y);
                
                // Smooth transition towards expected height
                float transitionFactor = Mathf.Pow(normalizedX, transitionSmoothness);
                heights[x, z] = Mathf.Lerp(baseHeight, expectedHeight, transitionFactor * 0.3f); // 30% influence
                heights[x, z] = Mathf.Clamp01(heights[x, z]);
            }
        }
        
        data.SetHeights(0, 0, heights);
        Debug.Log("[EnhancedSlopeVisualizer] ✓ Height transitions enhanced");
    }

    private void CreateSlopeMarkers()
    {
        // Clear existing markers
        ClearSlopeMarkers();

        if (terrain == null) return;

        TerrainData data = terrain.terrainData;
        Vector3 size = data.size;
        float roadCenterZ = size.z / 2f;

        // Create parent object
        if (markersParent == null)
        {
            markersParent = new GameObject("SlopeMarkers");
        }

        // Create markers along the road
        int markerCount = Mathf.RoundToInt(size.x / markerSpacing);
        
        for (int i = 0; i < markerCount; i++)
        {
            float x = i * markerSpacing;
            float normalizedX = x / size.x;
            
            // Get terrain height at this position
            Vector3 position = new Vector3(x, 0, roadCenterZ);
            float terrainHeight = terrain.SampleHeight(position);
            position.y = terrainHeight + 0.5f; // Slightly above ground
            
            // Create marker
            GameObject marker;
            if (slopeMarkerPrefab != null)
            {
                marker = Instantiate(slopeMarkerPrefab, position, Quaternion.identity, markersParent.transform);
            }
            else
            {
                // Create simple marker
                marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"SlopeMarker_{i}";
                marker.transform.position = position;
                marker.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
                marker.transform.parent = markersParent.transform;
                
                // Color based on gradient
                Renderer renderer = marker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    float gradient = terrainBuilder.GetGradient();
                    Color color = gradient > 5f ? Color.red : (gradient > 0f ? Color.yellow : Color.green);
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = color;
                    renderer.material = mat;
                }
            }
            
            slopeMarkers.Add(marker);
        }

        Debug.Log($"[EnhancedSlopeVisualizer] ✓ Created {markerCount} slope markers");
    }

    private void ClearSlopeMarkers()
    {
        foreach (GameObject marker in slopeMarkers)
        {
            if (marker != null)
            {
                DestroyImmediate(marker);
            }
        }
        slopeMarkers.Clear();

        if (markersParent != null)
        {
            DestroyImmediate(markersParent);
            markersParent = null;
        }
    }

    [ContextMenu("Clear Slope Markers")]
    public void ClearSlopeMarkersManual()
    {
        ClearSlopeMarkers();
    }
}
