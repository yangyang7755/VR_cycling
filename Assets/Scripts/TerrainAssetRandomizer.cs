using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Randomizes placement of assets (trees, rocks, grass) on terrain.
/// Spline-aware: places objects along the curved road at proper distance
/// from the road center, grounded to the sculpted terrain height.
/// 
/// Integration:
///   CurvedRouteGenerator.GenerateRoute() → sculpts terrain → calls SpawnAlongRoute()
///   Objects are placed at correct ground elevation and never on the road surface.
/// </summary>
public class TerrainAssetRandomizer : MonoBehaviour
{
    [Header("Terrain")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private float roadWidth = 12f;
    [SerializeField] private float shoulderWidth = 4f;
    
    [Header("Spline Reference (auto-detected)")]
    [SerializeField] private SplinePath splinePath;
    [SerializeField] private CurvedRouteGenerator routeGenerator;
    
    [Header("Asset Prefabs")]
    [SerializeField] private GameObject[] treePrefabs;
    [SerializeField] private GameObject[] rockPrefabs;
    [SerializeField] private GameObject[] grassPrefabs;
    
    [Header("Tree Settings")]
    [SerializeField] private bool spawnTrees = true;
    [SerializeField] private int treeCount = 50;
    [SerializeField] private float treeMinScale = 0.8f;
    [SerializeField] private float treeMaxScale = 1.5f;
    [SerializeField] private float treeMinDistanceFromRoad = 15f;
    [SerializeField] private float treeMaxDistanceFromRoad = 60f;
    
    [Header("Rock Settings")]
    [SerializeField] private bool spawnRocks = true;
    [SerializeField] private int rockCount = 30;
    [SerializeField] private float rockMinScale = 0.5f;
    [SerializeField] private float rockMaxScale = 2f;
    [SerializeField] private float rockMinDistanceFromRoad = 8f;
    [SerializeField] private float rockMaxDistanceFromRoad = 40f;
    
    [Header("Grass Settings")]
    [SerializeField] private bool spawnGrass = true;
    [SerializeField] private int grassCount = 100;
    [SerializeField] private float grassMinScale = 0.8f;
    [SerializeField] private float grassMaxScale = 1.2f;
    [SerializeField] private float grassMinDistanceFromRoad = 5f;
    [SerializeField] private float grassMaxDistanceFromRoad = 30f;
    
    [Header("Placement Settings")]
    [SerializeField] private float placementStepAlongRoute = 10f;
    [SerializeField] private bool randomRotation = true;
    [SerializeField] private bool alignToTerrainNormal = false;
    [SerializeField] private float maxSlopeAngle = 45f;
    
    [Header("Fallback (straight road mode)")]
    [SerializeField] private float roadCenterZ = 0f;
    [SerializeField] private float placementStartX = 0f;
    [SerializeField] private float placementEndX = 500f;
    [SerializeField] private float placementWidthZ = 100f;
    
    [Header("Organization")]
    [SerializeField] private bool organizeInHierarchy = true;
    [SerializeField] private string containerName = "TerrainAssets";
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showGizmos = true;
    
    private GameObject assetsContainer;
    private GameObject treesContainer;
    private GameObject rocksContainer;
    private GameObject grassContainer;
    private List<GameObject> spawnedAssets = new List<GameObject>();

    private void Start()
    {
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (routeGenerator == null)
            routeGenerator = FindObjectOfType<CurvedRouteGenerator>();
        
        if (splinePath == null && routeGenerator != null)
            splinePath = routeGenerator.GetSplinePath();
        
        // Auto-detect road center from bike position (fallback for straight roads)
        BikeController bike = FindObjectOfType<BikeController>();
        if (bike != null)
            roadCenterZ = bike.transform.position.z;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUBLIC API — called after route generation
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spawn roadside assets along the curved spline route.
    /// Call this AFTER CurvedRouteGenerator.GenerateRoute() has sculpted terrain.
    /// Objects are placed at proper distance from the road and grounded to terrain height.
    /// </summary>
    [ContextMenu("Spawn Along Route")]
    public void SpawnAlongRoute()
    {
        // Refresh references
        if (routeGenerator == null)
            routeGenerator = FindObjectOfType<CurvedRouteGenerator>();
        if (routeGenerator != null)
            splinePath = routeGenerator.GetSplinePath();
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (splinePath == null || splinePath.GetTotalLength() < 10f)
        {
            Debug.LogWarning("[AssetRandomizer] No valid spline path found. Falling back to straight-line placement.");
            SpawnAllAssets();
            return;
        }

        ClearAllAssets();
        CreateContainers();
        
        float routeLength = splinePath.GetTotalLength();
        
        if (showDebugInfo)
            Debug.Log($"[AssetRandomizer] Spawning along curved route ({routeLength:F0}m)...");
        
        int totalSpawned = 0;
        
        if (spawnTrees && HasValidPrefabs(treePrefabs))
        {
            int spawned = SpawnAlongSpline(treePrefabs, treeCount, treeMinDistanceFromRoad, treeMaxDistanceFromRoad,
                                           treeMinScale, treeMaxScale, treesContainer, "tree");
            totalSpawned += spawned;
            if (showDebugInfo) Debug.Log($"[AssetRandomizer] Spawned {spawned} trees along route");
        }
        
        if (spawnRocks && HasValidPrefabs(rockPrefabs))
        {
            int spawned = SpawnAlongSpline(rockPrefabs, rockCount, rockMinDistanceFromRoad, rockMaxDistanceFromRoad,
                                           rockMinScale, rockMaxScale, rocksContainer, "rock");
            totalSpawned += spawned;
            if (showDebugInfo) Debug.Log($"[AssetRandomizer] Spawned {spawned} rocks along route");
        }
        
        if (spawnGrass && HasValidPrefabs(grassPrefabs))
        {
            int spawned = SpawnAlongSpline(grassPrefabs, grassCount, grassMinDistanceFromRoad, grassMaxDistanceFromRoad,
                                           grassMinScale, grassMaxScale, grassContainer, "grass");
            totalSpawned += spawned;
            if (showDebugInfo) Debug.Log($"[AssetRandomizer] Spawned {spawned} grass along route");
        }
        
        if (showDebugInfo)
            Debug.Log($"[AssetRandomizer] Total roadside assets: {totalSpawned}");
    }

    /// <summary>
    /// Spawn assets distributed along the spline, offset to either side of the road.
    /// Each asset is grounded to the sculpted terrain height at its final position.
    /// </summary>
    private int SpawnAlongSpline(GameObject[] prefabs, int count, float minDist, float maxDist,
                                  float minScale, float maxScale, GameObject container, string label)
    {
        float routeLength = splinePath.GetTotalLength();
        int spawned = 0;
        int maxAttempts = count * 5;
        int attempts = 0;
        
        // Minimum clearance: half road width + shoulder
        float roadClearance = (roadWidth * 0.5f) + shoulderWidth;
        float effectiveMinDist = Mathf.Max(minDist, roadClearance + 1f);
        
        while (spawned < count && attempts < maxAttempts)
        {
            attempts++;
            
            // Pick a random distance along the route
            float dist = Random.Range(0f, routeLength);
            SplinePath.SplineSample sample = splinePath.SampleAtDistance(dist);
            
            // Pick a random side (left or right) and distance from road
            float side = Random.value > 0.5f ? 1f : -1f;
            float lateralOffset = Random.Range(effectiveMinDist, maxDist);
            
            // Position = road center + lateral offset perpendicular to road direction
            Vector3 position = sample.position + sample.right * side * lateralOffset;
            
            // Ground the object to terrain height
            float groundHeight = GetTerrainHeight(position);
            position.y = groundHeight;
            
            // Validate: within terrain bounds and not on road
            if (!IsWithinTerrainBounds(position))
                continue;
            
            // Double-check: measure actual distance to nearest road point to catch tight curves
            if (!IsSafeFromRoad(position, roadClearance))
                continue;
            
            // Check slope
            if (!IsValidSlope(position))
                continue;
            
            // Spawn the asset
            GameObject prefab = null;
            try { prefab = prefabs[Random.Range(0, prefabs.Length)]; } catch { continue; }
            if (prefab == null) continue;
            GameObject obj = Instantiate(prefab, position, Quaternion.identity);
            
            // Scale
            float scale = Random.Range(minScale, maxScale);
            obj.transform.localScale = Vector3.one * scale;
            
            // Rotation
            if (randomRotation)
            {
                if (label == "rock")
                    obj.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                else
                    obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            
            // Terrain normal alignment
            if (alignToTerrainNormal || label == "grass")
                AlignToTerrain(obj.transform, position);
            
            // Parent
            if (organizeInHierarchy && container != null)
                obj.transform.SetParent(container.transform);
            
            spawnedAssets.Add(obj);
            spawned++;
        }
        
        return spawned;
    }

    /// <summary>
    /// Check that a world position is safe distance from the road surface.
    /// Samples nearby spline points to handle tight curves where a simple
    /// perpendicular offset might still land on the road.
    /// </summary>
    private bool IsSafeFromRoad(Vector3 worldPos, float minClearance)
    {
        float routeLength = splinePath.GetTotalLength();
        float checkStep = 5f; // check every 5m along route
        float minSqDist = float.MaxValue;
        
        // Only check within a reasonable range — full route scan is expensive
        // Quick broad-phase: find approximate nearest distance using the spline
        // Sample at coarse intervals to find closest section
        float bestDist = 0f;
        float coarseStep = 20f;
        float bestSqDist = float.MaxValue;
        
        for (float d = 0f; d <= routeLength; d += coarseStep)
        {
            SplinePath.SplineSample s = splinePath.SampleAtDistance(d);
            float sq = (new Vector3(worldPos.x, 0f, worldPos.z) - new Vector3(s.position.x, 0f, s.position.z)).sqrMagnitude;
            if (sq < bestSqDist) { bestSqDist = sq; bestDist = d; }
        }
        
        // Fine-pass around the closest section
        float searchStart = Mathf.Max(0f, bestDist - coarseStep);
        float searchEnd = Mathf.Min(routeLength, bestDist + coarseStep);
        
        for (float d = searchStart; d <= searchEnd; d += checkStep)
        {
            SplinePath.SplineSample s = splinePath.SampleAtDistance(d);
            float sq = (new Vector3(worldPos.x, 0f, worldPos.z) - new Vector3(s.position.x, 0f, s.position.z)).sqrMagnitude;
            if (sq < minSqDist) minSqDist = sq;
        }
        
        return Mathf.Sqrt(minSqDist) >= minClearance;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FALLBACK: Original straight-road placement (for non-spline routes)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Legacy spawn method for straight roads (no spline).
    /// </summary>
    [ContextMenu("Spawn All Assets (Straight Road)")]
    public void SpawnAllAssets()
    {
        ClearAllAssets();
        CreateContainers();
        
        if (showDebugInfo)
            Debug.Log($"[AssetRandomizer] Starting straight-road asset placement...");
        
        int totalSpawned = 0;
        
        if (spawnTrees && treePrefabs != null && treePrefabs.Length > 0)
        {
            int spawned = SpawnAssetsStraight(treePrefabs, treeCount, treeMinDistanceFromRoad,
                                              treeMinScale, treeMaxScale, treesContainer);
            totalSpawned += spawned;
        }
        
        if (spawnRocks && rockPrefabs != null && rockPrefabs.Length > 0)
        {
            int spawned = SpawnAssetsStraight(rockPrefabs, rockCount, rockMinDistanceFromRoad,
                                              rockMinScale, rockMaxScale, rocksContainer);
            totalSpawned += spawned;
        }
        
        if (spawnGrass && grassPrefabs != null && grassPrefabs.Length > 0)
        {
            int spawned = SpawnAssetsStraight(grassPrefabs, grassCount, grassMinDistanceFromRoad,
                                              grassMinScale, grassMaxScale, grassContainer);
            totalSpawned += spawned;
        }
        
        if (showDebugInfo)
            Debug.Log($"[AssetRandomizer] Total assets spawned (straight): {totalSpawned}");
    }

    private int SpawnAssetsStraight(GameObject[] prefabs, int count, float minDistFromRoad,
                                     float minScale, float maxScale, GameObject container)
    {
        int spawned = 0;
        int maxAttempts = count * 10;
        int attempts = 0;
        
        while (spawned < count && attempts < maxAttempts)
        {
            attempts++;
            
            float x = Random.Range(placementStartX, placementEndX);
            float side = Random.value > 0.5f ? 1f : -1f;
            float zOffset = Random.Range(minDistFromRoad, placementWidthZ);
            float z = roadCenterZ + (side * zOffset);
            
            Vector3 position = new Vector3(x, 0f, z);
            float height = GetTerrainHeight(position);
            position.y = height;
            
            // Distance from road check
            float distFromRoad = Mathf.Abs(position.z - roadCenterZ);
            if (distFromRoad < minDistFromRoad || distFromRoad < roadWidth * 0.5f)
                continue;
            
            if (!IsValidSlope(position))
                continue;
            
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            GameObject obj = Instantiate(prefab, position, Quaternion.identity);
            
            float scale = Random.Range(minScale, maxScale);
            obj.transform.localScale = Vector3.one * scale;
            
            if (randomRotation)
                obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            
            if (organizeInHierarchy && container != null)
                obj.transform.SetParent(container.transform);
            
            spawnedAssets.Add(obj);
            spawned++;
        }
        
        return spawned;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Clear all spawned assets
    /// </summary>
    [ContextMenu("Clear All Assets")]
    public void ClearAllAssets()
    {
        foreach (GameObject asset in spawnedAssets)
        {
            if (asset != null)
                DestroyImmediate(asset);
        }
        spawnedAssets.Clear();
        
        if (assetsContainer != null)
            DestroyImmediate(assetsContainer);
        
        if (showDebugInfo)
            Debug.Log("[AssetRandomizer] Cleared all assets");
    }

    private void CreateContainers()
    {
        if (organizeInHierarchy)
        {
            assetsContainer = new GameObject(containerName);
            assetsContainer.transform.SetParent(transform);
            
            treesContainer = new GameObject("Trees");
            treesContainer.transform.SetParent(assetsContainer.transform);
            
            rocksContainer = new GameObject("Rocks");
            rocksContainer.transform.SetParent(assetsContainer.transform);
            
            grassContainer = new GameObject("Grass");
            grassContainer.transform.SetParent(assetsContainer.transform);
        }
    }

    /// <summary>
    /// Safely check if a prefab array has at least one valid (non-null) entry.
    /// Handles Unity's serialized unassigned references without throwing exceptions.
    /// </summary>
    private bool HasValidPrefabs(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0) return false;
        try
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null) return true;
            }
        }
        catch (System.Exception)
        {
            return false;
        }
        return false;
    }

    private float GetTerrainHeight(Vector3 worldPosition)
    {
        if (terrain == null)
            return 0f;
        
        // Always use terrain.SampleHeight for reliable grounding.
        // Raycasting can fail when the terrain collider hasn't rebuilt yet
        // (e.g. when called immediately after SculptTerrainAlongPath).
        // SampleHeight reads directly from heightmap data which is always current.
        float terrainHeight = terrain.SampleHeight(worldPosition);
        return terrainHeight + terrain.transform.position.y;
    }

    private bool IsWithinTerrainBounds(Vector3 position)
    {
        if (terrain == null) return true;
        
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = terrain.terrainData.size;
        
        return position.x >= tPos.x && position.x <= tPos.x + tSize.x &&
               position.z >= tPos.z && position.z <= tPos.z + tSize.z;
    }

    private bool IsValidSlope(Vector3 position)
    {
        if (terrain == null) return true;
        
        float nx = (position.x - terrain.transform.position.x) / terrain.terrainData.size.x;
        float nz = (position.z - terrain.transform.position.z) / terrain.terrainData.size.z;
        
        if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) return false;
        
        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(nx, nz);
        float slope = Vector3.Angle(normal, Vector3.up);
        return slope <= maxSlopeAngle;
    }

    private void AlignToTerrain(Transform obj, Vector3 position)
    {
        if (terrain == null) return;
        
        float nx = (position.x - terrain.transform.position.x) / terrain.terrainData.size.x;
        float nz = (position.z - terrain.transform.position.z) / terrain.terrainData.size.z;
        
        if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) return;
        
        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(nx, nz);
        obj.up = normal;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUBLIC SETTERS (for compatibility)
    // ═══════════════════════════════════════════════════════════════════════════

    public void SetPlacementArea(float startX, float endX)
    {
        placementStartX = startX;
        placementEndX = endX;
    }

    public void SetRoadCenterZ(float z)
    {
        roadCenterZ = z;
    }

    public void SetSplinePath(SplinePath path)
    {
        splinePath = path;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GIZMOS
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnDrawGizmos()
    {
        if (!showGizmos || terrain == null) return;
        
        // Draw road exclusion zone
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Vector3 roadCenter = new Vector3((placementStartX + placementEndX) / 2f, 0f, roadCenterZ);
        Vector3 roadSize = new Vector3(placementEndX - placementStartX, 1f, roadWidth);
        Gizmos.DrawCube(roadCenter, roadSize);
        
        // Draw placement area
        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
        Vector3 placementSize = new Vector3(placementEndX - placementStartX, 1f, placementWidthZ * 2f);
        Gizmos.DrawCube(roadCenter, placementSize);
    }
}
