using UnityEngine;

/// <summary>
/// Creates curvature illusion effects:
/// 1. Adds subtle upward bend to road mesh (even if terrain is flat)
/// 2. Reduces horizon visibility on steeper grades
/// </summary>
public class CurvatureIllusion : MonoBehaviour
{
    [Header("Terrain Reference")]
    [Tooltip("Terrain to modify")]
    [SerializeField] private Terrain terrain;
    
    [Tooltip("SimpleTerrainBuilder reference")]
    [SerializeField] private SimpleTerrainBuilder terrainBuilder;

    [Header("Road Curvature Settings")]
    [Tooltip("Enable upward road curvature illusion")]
    [SerializeField] private bool enableRoadCurvature = true;
    
    [Tooltip("Curvature strength (how much the road bends upward). Higher = more pronounced")]
    [Range(0f, 0.05f)]
    [SerializeField] private float curvatureStrength = 0.01f;
    
    [Tooltip("Curvature distance (how far ahead the curvature starts, in meters)")]
    [Range(10f, 200f)]
    [SerializeField] private float curvatureDistance = 100f;
    
    [Tooltip("Apply curvature based on current gradient (more curvature on steeper grades)")]
    [SerializeField] private bool gradientBasedCurvature = true;
    
    [Tooltip("Curvature multiplier per 1% grade")]
    [Range(0.5f, 2f)]
    [SerializeField] private float curvaturePerPercentGrade = 1f;

    [Header("Horizon Visibility Settings")]
    [Tooltip("Enable horizon visibility reduction on steeper grades")]
    [SerializeField] private bool enableHorizonReduction = true;
    
    [Tooltip("Horizon line component to adjust")]
    [SerializeField] private HorizonLine horizonLine;
    
    [Tooltip("Base horizon visibility (0 = invisible, 1 = fully visible)")]
    [Range(0f, 1f)]
    [SerializeField] private float baseHorizonVisibility = 1f;
    
    [Tooltip("Visibility reduction per 1% grade (0.1 = 10% reduction per 1% grade)")]
    [Range(0f, 0.2f)]
    [SerializeField] private float visibilityReductionPerGrade = 0.05f;
    
    [Tooltip("Minimum horizon visibility (prevents complete disappearance)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float minHorizonVisibility = 0.3f;
    
    [Tooltip("Maximum horizon visibility (at 0% grade)")]
    [Range(0.5f, 1f)]
    [SerializeField] private float maxHorizonVisibility = 1f;

    [Header("Fog Settings (Alternative Horizon Reduction)")]
    [Tooltip("Use fog to reduce horizon visibility")]
    [SerializeField] private bool useFogForHorizon = false;
    
    [Tooltip("Fog density multiplier per 1% grade")]
    [Range(0f, 0.1f)]
    [SerializeField] private float fogDensityPerGrade = 0.02f;
    
    [Tooltip("Base fog density")]
    [Range(0f, 0.1f)]
    [SerializeField] private float baseFogDensity = 0f;

    [Header("References")]
    [Tooltip("Bike controller to get current gradient")]
    [SerializeField] private BikeController bikeController;

    private float[,] originalHeights; // Store original terrain heights (2D array)
    private bool curvatureApplied = false;
    private float currentGradient = 0f;

    private void Start()
    {
        // Auto-find components
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (terrainBuilder == null)
        {
            terrainBuilder = FindObjectOfType<SimpleTerrainBuilder>();
        }

        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        if (horizonLine == null)
        {
            horizonLine = FindObjectOfType<HorizonLine>();
        }

        // Store original terrain heights
        StoreOriginalHeights();

        // Apply curvature if enabled
        if (enableRoadCurvature)
        {
            ApplyRoadCurvature();
        }
    }

    private void Update()
    {
        // Update current gradient
        if (bikeController != null)
        {
            currentGradient = bikeController.GetGradient();
        }

        // Update horizon visibility based on gradient
        if (enableHorizonReduction)
        {
            UpdateHorizonVisibility();
        }

        // Update fog if enabled
        if (useFogForHorizon)
        {
            UpdateFogDensity();
        }
    }

    /// <summary>
    /// Store original terrain heights before applying curvature
    /// </summary>
    private void StoreOriginalHeights()
    {
        if (terrain == null || terrain.terrainData == null) return;

        TerrainData data = terrain.terrainData;
        int width = data.heightmapResolution;
        int height = data.heightmapResolution;
        
        // GetHeights returns float[,] (2D array)
        originalHeights = data.GetHeights(0, 0, width, height);
    }

    /// <summary>
    /// Apply subtle upward curvature to the road
    /// </summary>
    public void ApplyRoadCurvature()
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogWarning("[CurvatureIllusion] Cannot apply road curvature: Terrain is null!");
            return;
        }

        if (originalHeights == null)
        {
            StoreOriginalHeights();
        }

        TerrainData data = terrain.terrainData;
        int width = data.heightmapResolution;
        int height = data.heightmapResolution;
        Vector3 size = data.size;

        // Get road information
        float roadCenterZ = size.z / 2f;
        float roadWidth = 15f;
        if (terrainBuilder != null)
        {
            roadCenterZ = terrainBuilder.GetRoadCenterZ();
            roadWidth = terrainBuilder.GetRoadWidth();
        }

        // Calculate effective curvature strength
        float effectiveCurvature = curvatureStrength;
        if (gradientBasedCurvature && bikeController != null)
        {
            float gradient = bikeController.GetGradient();
            effectiveCurvature = curvatureStrength * (1f + gradient * curvaturePerPercentGrade / 100f);
        }

        // Get current heights
        float[,] heights = data.GetHeights(0, 0, width, height);

        // Apply curvature to road area
        int centerZ = Mathf.RoundToInt((roadCenterZ / size.z) * height);
        float roadWidthInPixels = (roadWidth / size.z) * height;
        int halfWidth = Mathf.RoundToInt(roadWidthInPixels / 2f);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                // Check if this point is on the road
                int distFromCenter = Mathf.Abs(z - centerZ);
                if (distFromCenter <= halfWidth)
                {
                    // Calculate distance along road (normalized 0-1)
                    float normalizedX = (float)x / (width - 1);
                    
                    // Apply upward curvature: road bends upward as it goes forward
                    // Use a smooth curve (sine or quadratic) that increases with distance
                    float curvatureFactor = 0f;
                    if (normalizedX > 0.1f) // Start curvature after 10% of the road
                    {
                        // Normalize distance for curvature (0 to 1 from curvature start to end)
                        float curvatureProgress = Mathf.Clamp01((normalizedX - 0.1f) / 0.9f);
                        
                        // Use quadratic curve for smooth upward bend
                        curvatureFactor = curvatureProgress * curvatureProgress;
                        
                        // Scale by curvature strength and distance
                        float distanceMeters = normalizedX * size.x;
                        float curvatureHeight = curvatureFactor * effectiveCurvature * (distanceMeters / curvatureDistance);
                        
                        // Apply to height (convert to normalized height)
                        float heightIncrease = curvatureHeight / size.y;
                        heights[x, z] = Mathf.Min(1f, heights[x, z] + heightIncrease);
                    }
                }
            }
        }

        // Apply modified heights
        data.SetHeights(0, 0, heights);
        curvatureApplied = true;

        Debug.Log($"[CurvatureIllusion] Applied road curvature: Strength={effectiveCurvature:F4}, Distance={curvatureDistance}m");
    }

    /// <summary>
    /// Remove road curvature and restore original heights
    /// </summary>
    public void RemoveRoadCurvature()
    {
        if (terrain == null || terrain.terrainData == null || originalHeights == null)
        {
            return;
        }

        TerrainData data = terrain.terrainData;
        int width = data.heightmapResolution;
        int height = data.heightmapResolution;

        // Restore original heights
        data.SetHeights(0, 0, originalHeights);
        curvatureApplied = false;

        Debug.Log("[CurvatureIllusion] Removed road curvature");
    }

    /// <summary>
    /// Update horizon visibility based on current gradient
    /// </summary>
    private void UpdateHorizonVisibility()
    {
        if (horizonLine == null) return;

        // Calculate visibility based on gradient
        // Higher gradient = lower visibility
        float visibility = baseHorizonVisibility;
        
        if (currentGradient > 0.1f) // Only reduce if there's a significant gradient
        {
            float reduction = currentGradient * visibilityReductionPerGrade;
            visibility = baseHorizonVisibility - reduction;
        }

        // Clamp visibility
        visibility = Mathf.Clamp(visibility, minHorizonVisibility, maxHorizonVisibility);

        // Apply to horizon line (if it has visibility/alpha control)
        // This would require adding a SetVisibility method to HorizonLine
        ApplyHorizonVisibility(visibility);
    }

    /// <summary>
    /// Apply visibility to horizon line
    /// </summary>
    private void ApplyHorizonVisibility(float visibility)
    {
        if (horizonLine == null) return;

        // Use the SetVisibility method if available
        var setVisibilityMethod = horizonLine.GetType().GetMethod("SetVisibility");
        if (setVisibilityMethod != null)
        {
            setVisibilityMethod.Invoke(horizonLine, new object[] { visibility });
        }
        else
        {
            // Fallback: manually adjust alpha
            var lineRenderer = horizonLine.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                Color startColor = lineRenderer.startColor;
                Color endColor = lineRenderer.endColor;
                startColor.a = visibility;
                endColor.a = visibility;
                lineRenderer.startColor = startColor;
                lineRenderer.endColor = endColor;
            }
        }
    }

    /// <summary>
    /// Update fog density based on gradient
    /// </summary>
    private void UpdateFogDensity()
    {
        if (!RenderSettings.fog) return;

        // Calculate fog density based on gradient
        float fogDensity = baseFogDensity;
        
        if (currentGradient > 0.1f)
        {
            fogDensity += currentGradient * fogDensityPerGrade;
        }

        // Clamp fog density
        fogDensity = Mathf.Clamp(fogDensity, 0f, 0.1f);

        RenderSettings.fogDensity = fogDensity;
    }

    /// <summary>
    /// Get current gradient for external use
    /// </summary>
    public float GetCurrentGradient()
    {
        return currentGradient;
    }

    /// <summary>
    /// Reapply curvature (useful after terrain regeneration)
    /// </summary>
    [ContextMenu("Reapply Road Curvature")]
    public void ReapplyCurvature()
    {
        StoreOriginalHeights();
        ApplyRoadCurvature();
    }

    /// <summary>
    /// Remove curvature
    /// </summary>
    [ContextMenu("Remove Road Curvature")]
    public void RemoveCurvature()
    {
        RemoveRoadCurvature();
    }
}
