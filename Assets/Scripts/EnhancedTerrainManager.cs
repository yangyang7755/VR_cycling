using UnityEngine;

/// <summary>
/// Manages all terrain enhancements: curved roads, rich landscape, better slopes
/// Integrates all visual improvements in one place
/// </summary>
public class EnhancedTerrainManager : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("SimpleTerrainBuilder component")]
    [SerializeField] private SimpleTerrainBuilder terrainBuilder;
    
    [Tooltip("CurvedRoadGenerator component")]
    [SerializeField] private CurvedRoadGenerator curvedRoadGenerator;
    
    [Tooltip("LandscapeGenerator component")]
    [SerializeField] private LandscapeGenerator landscapeGenerator;
    
    [Tooltip("EnhancedSlopeVisualizer component")]
    [SerializeField] private EnhancedSlopeVisualizer slopeVisualizer;

    [Header("Auto Setup")]
    [Tooltip("Automatically find components if not assigned")]
    [SerializeField] private bool autoFindComponents = true;
    
    [Tooltip("Apply all enhancements automatically on Start")]
    [SerializeField] private bool autoApplyOnStart = false;

    private void Start()
    {
        if (autoFindComponents)
        {
            FindComponents();
        }

        if (autoApplyOnStart)
        {
            ApplyAllEnhancements();
        }
    }

    private void FindComponents()
    {
        if (terrainBuilder == null)
        {
            terrainBuilder = FindObjectOfType<SimpleTerrainBuilder>();
        }

        if (curvedRoadGenerator == null)
        {
            curvedRoadGenerator = FindObjectOfType<CurvedRoadGenerator>();
        }

        if (landscapeGenerator == null)
        {
            landscapeGenerator = FindObjectOfType<LandscapeGenerator>();
        }

        if (slopeVisualizer == null)
        {
            slopeVisualizer = FindObjectOfType<EnhancedSlopeVisualizer>();
        }
    }

    /// <summary>
    /// Apply all terrain enhancements
    /// </summary>
    [ContextMenu("Apply All Enhancements")]
    public void ApplyAllEnhancements()
    {
        Debug.Log("[EnhancedTerrainManager] Applying all terrain enhancements...");

        if (terrainBuilder == null)
        {
            Debug.LogError("[EnhancedTerrainManager] SimpleTerrainBuilder not found!");
            return;
        }

        // Step 1: Initialize base terrain
        terrainBuilder.InitializeTerrain();
        
        // Step 2: Add elevation profile
        terrainBuilder.AddElevationProfile();
        
        // Step 3: Setup textures
        terrainBuilder.SetupTextures();
        
        // Step 4: Paint road (curved or straight)
        if (curvedRoadGenerator != null && curvedRoadGenerator.enableCurvedRoad)
        {
            curvedRoadGenerator.PaintCurvedRoad();
        }
        else
        {
            terrainBuilder.PaintRoad();
        }
        
        // Step 5: Enhance slope visibility
        if (slopeVisualizer != null)
        {
            slopeVisualizer.EnhanceSlopeVisibility();
        }
        
        // Step 6: Generate landscape
        if (landscapeGenerator != null)
        {
            landscapeGenerator.GenerateLandscape();
        }

        Debug.Log("[EnhancedTerrainManager] ✓ All enhancements applied!");
    }

    /// <summary>
    /// Regenerate landscape only (for trial changes)
    /// </summary>
    [ContextMenu("Regenerate Landscape")]
    public void RegenerateLandscape()
    {
        if (landscapeGenerator != null)
        {
            landscapeGenerator.GenerateLandscape();
        }
        else
        {
            Debug.LogWarning("[EnhancedTerrainManager] LandscapeGenerator not found!");
        }
    }

    /// <summary>
    /// Regenerate curved road only
    /// </summary>
    [ContextMenu("Regenerate Curved Road")]
    public void RegenerateCurvedRoad()
    {
        if (curvedRoadGenerator != null)
        {
            curvedRoadGenerator.GenerateCurvePoints();
            curvedRoadGenerator.PaintCurvedRoad();
        }
        else
        {
            Debug.LogWarning("[EnhancedTerrainManager] CurvedRoadGenerator not found!");
        }
    }
}
