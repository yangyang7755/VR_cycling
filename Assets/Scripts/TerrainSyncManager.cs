using UnityEngine;
using System.Collections;

/// <summary>
/// Ensures all terrain-dependent components stay synchronized
/// Handles terrain generation -> elevation bar update -> progress reset
/// </summary>
public class TerrainSyncManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProceduralRouteGenerator routeGenerator;
    [SerializeField] private ElevationProgressBar elevationBar;
    [SerializeField] private BikeController bikeController;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private void Start()
    {
        // Auto-find references
        if (routeGenerator == null)
            routeGenerator = FindObjectOfType<ProceduralRouteGenerator>();
        
        if (elevationBar == null)
            elevationBar = FindObjectOfType<ElevationProgressBar>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
    }

    /// <summary>
    /// Synchronize all components after terrain generation
    /// Call this after generating new terrain
    /// </summary>
    public void SynchronizeAfterTerrainGeneration()
    {
        StartCoroutine(SyncCoroutine());
    }

    private IEnumerator SyncCoroutine()
    {
        if (debugMode)
            Debug.Log("[TerrainSync] Starting synchronization...");
        
        // Wait for terrain to be fully applied
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        // Update elevation bar
        if (elevationBar != null && routeGenerator != null)
        {
            float routeLength = routeGenerator.GetRouteLength();
            elevationBar.SetTotalRouteLength(routeLength);
            elevationBar.ResetProgress();
            
            if (debugMode)
                Debug.Log($"[TerrainSync] ✓ Elevation bar updated (route: {routeLength}m)");
        }
        
        // Reset bike position if needed
        if (bikeController != null)
        {
            // You may want to add a reset method to BikeController
            // bikeController.ResetPosition();
            
            if (debugMode)
                Debug.Log("[TerrainSync] ✓ Bike controller ready");
        }
        
        if (debugMode)
            Debug.Log("[TerrainSync] ✓ Synchronization complete!");
    }

    /// <summary>
    /// Manual sync trigger (for testing)
    /// </summary>
    [ContextMenu("Synchronize Now")]
    public void SynchronizeNow()
    {
        SynchronizeAfterTerrainGeneration();
    }
}
