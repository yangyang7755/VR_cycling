using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Randomizes placement of assets (trees, rocks, grass) on terrain
/// Assets are grounded to terrain height and avoid the road
/// </summary>
public class TerrainAssetRandomizer : MonoBehaviour
{
    [Header("Terrain")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private float roadWidth = 10f; // Width of road to keep clear
    [SerializeField] private float roadCenterZ = 0f; // Z position of road center (auto-detected from bike)
    
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
    
    [Header("Rock Settings")]
    [SerializeField] private bool spawnRocks = true;
    [SerializeField] private int rockCount = 30;
    [SerializeField] private float rockMinScale = 0.5f;
    [SerializeField] private float rockMaxScale = 2f;
    [SerializeField] private float rockMinDistanceFromRoad = 8f;
    
    [Header("Grass Settings")]
    [SerializeField] private bool spawnGrass = true;
    [SerializeField] private int grassCount = 100;
    [SerializeField] private float grassMinScale = 0.8f;
    [SerializeField] private float grassMaxScale = 1.2f;
    [SerializeField] private float grassMinDistanceFromRoad = 5f;
    
    [Header("Placement Area")]
    [SerializeField] private float placementStartX = 0f; // Start of placement area
    [SerializeField] private float placementEndX = 500f; // End of placement area
    [SerializeField] private float placementWidthZ = 100f; // Width on each side of road
    
    [Header("Advanced Settings")]
    [SerializeField] private bool randomRotation = true;
    [SerializeField] private bool alignToTerrainNormal = false;
    [SerializeField] private float maxSlopeAngle = 45f; // Don't place on slopes steeper than this
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Organization")]
    [SerializeField] private bool organizeInHierarchy = true;
    [SerializeField] private string containerName = "TerrainAssets";
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showPlacementDebug = false; // Show each placement attempt
    [SerializeField] private bool useRaycastForHeight = true; // Use raycast instead of SampleHeight
    
    private GameObject assetsContainer;
    private GameObject treesContainer;
    private GameObject rocksContainer;
    private GameObject grassContainer;
    private List<GameObject> spawnedAssets = new List<GameObject>();

    private void Start()
    {
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        // Auto-detect road center from bike position
        BikeController bike = FindObjectOfType<BikeController>();
        if (bike != null)
        {
            roadCenterZ = bike.transform.position.z;
        }
        
        // Don't auto-spawn on start - wait for manual trigger or route generation
    }

    /// <summary>
    /// Spawn all assets
    /// </summary>
    [ContextMenu("Spawn All Assets")]
    public void SpawnAllAssets()
    {
        ClearAllAssets();
        CreateContainers();
        
        if (showDebugInfo)
            Debug.Log($"[AssetRandomizer] Starting asset placement...");
        
        int totalSpawned = 0;
        
        if (spawnTrees && treePrefabs != null && treePrefabs.Length > 0)
        {
            int spawned = SpawnTrees();
            totalSpawned += spawned;
            if (showDebugInfo)
                Debug.Log($"[AssetRandomizer] ✓ Spawned {spawned} trees");
        }
        
        if (spawnRocks && rockPrefabs != null && rockPrefabs.Length > 0)
        {
            int spawned = SpawnRocks();
            totalSpawned += spawned;
            if (showDebugInfo)
                Debug.Log($"[AssetRandomizer] ✓ Spawned {spawned} rocks");
        }
        
        if (spawnGrass && grassPrefabs != null && grassPrefabs.Length > 0)
        {
            int spawned = SpawnGrass();
            totalSpawned += spawned;
            if (showDebugInfo)
                Debug.Log($"[AssetRandomizer] ✓ Spawned {spawned} grass patches");
        }
        
        if (showDebugInfo)
            Debug.Log($"[AssetRandomizer] ✓ Total assets spawned: {totalSpawned}");
    }

    /// <summary>
    /// Clear all spawned assets
    /// </summary>
    [ContextMenu("Clear All Assets")]
    public void ClearAllAssets()
    {
        // Destroy all tracked assets
        foreach (GameObject asset in spawnedAssets)
        {
            if (asset != null)
                DestroyImmediate(asset);
        }
        spawnedAssets.Clear();
        
        // Destroy containers
        if (assetsContainer != null)
            DestroyImmediate(assetsContainer);
        
        if (showDebugInfo)
            Debug.Log("[AssetRandomizer] ✓ Cleared all assets");
    }

    /// <summary>
    /// Create hierarchy containers
    /// </summary>
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
    /// Spawn trees
    /// </summary>
    private int SpawnTrees()
    {
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = treeCount * 10;
        
        while (spawned < treeCount && attempts < maxAttempts)
        {
            attempts++;
            
            Vector3 position = GetRandomPosition(treeMinDistanceFromRoad);
            
            if (IsValidPlacement(position, treeMinDistanceFromRoad))
            {
                GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
                GameObject tree = Instantiate(prefab, position, Quaternion.identity);
                
                // Random scale
                float scale = Random.Range(treeMinScale, treeMaxScale);
                tree.transform.localScale = Vector3.one * scale;
                
                // Random rotation
                if (randomRotation)
                {
                    tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
                
                // Align to terrain normal
                if (alignToTerrainNormal)
                {
                    AlignToTerrain(tree.transform, position);
                }
                
                // Organize
                if (organizeInHierarchy && treesContainer != null)
                {
                    tree.transform.SetParent(treesContainer.transform);
                }
                
                // Debug visualization
                if (showPlacementDebug)
                {
                    Debug.DrawLine(position, position + Vector3.up * 5f, Color.green, 5f);
                    Debug.Log($"[AssetRandomizer] Tree placed at ({position.x:F1}, {position.y:F1}, {position.z:F1})");
                }
                
                spawnedAssets.Add(tree);
                spawned++;
            }
        }
        
        return spawned;
    }

    /// <summary>
    /// Spawn rocks
    /// </summary>
    private int SpawnRocks()
    {
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = rockCount * 10;
        
        while (spawned < rockCount && attempts < maxAttempts)
        {
            attempts++;
            
            Vector3 position = GetRandomPosition(rockMinDistanceFromRoad);
            
            if (IsValidPlacement(position, rockMinDistanceFromRoad))
            {
                GameObject prefab = rockPrefabs[Random.Range(0, rockPrefabs.Length)];
                GameObject rock = Instantiate(prefab, position, Quaternion.identity);
                
                // Random scale
                float scale = Random.Range(rockMinScale, rockMaxScale);
                rock.transform.localScale = Vector3.one * scale;
                
                // Random rotation
                if (randomRotation)
                {
                    rock.transform.rotation = Quaternion.Euler(
                        Random.Range(-10f, 10f),
                        Random.Range(0f, 360f),
                        Random.Range(-10f, 10f)
                    );
                }
                
                // Align to terrain normal
                if (alignToTerrainNormal)
                {
                    AlignToTerrain(rock.transform, position);
                }
                
                // Organize
                if (organizeInHierarchy && rocksContainer != null)
                {
                    rock.transform.SetParent(rocksContainer.transform);
                }
                
                spawnedAssets.Add(rock);
                spawned++;
            }
        }
        
        return spawned;
    }

    /// <summary>
    /// Spawn grass
    /// </summary>
    private int SpawnGrass()
    {
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = grassCount * 10;
        
        while (spawned < grassCount && attempts < maxAttempts)
        {
            attempts++;
            
            Vector3 position = GetRandomPosition(grassMinDistanceFromRoad);
            
            if (IsValidPlacement(position, grassMinDistanceFromRoad))
            {
                GameObject prefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
                GameObject grass = Instantiate(prefab, position, Quaternion.identity);
                
                // Random scale
                float scale = Random.Range(grassMinScale, grassMaxScale);
                grass.transform.localScale = Vector3.one * scale;
                
                // Random rotation
                if (randomRotation)
                {
                    grass.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
                
                // Always align grass to terrain
                AlignToTerrain(grass.transform, position);
                
                // Organize
                if (organizeInHierarchy && grassContainer != null)
                {
                    grass.transform.SetParent(grassContainer.transform);
                }
                
                spawnedAssets.Add(grass);
                spawned++;
            }
        }
        
        return spawned;
    }

    /// <summary>
    /// Get random position within placement area
    /// </summary>
    private Vector3 GetRandomPosition(float minDistanceFromRoad)
    {
        // Random X along the route
        float x = Random.Range(placementStartX, placementEndX);
        
        // Random Z on either side of road (avoiding road)
        float side = Random.value > 0.5f ? 1f : -1f;
        float zOffset = Random.Range(minDistanceFromRoad, placementWidthZ);
        float z = roadCenterZ + (side * zOffset);
        
        // Get terrain height at this position (world coordinates)
        Vector3 worldPos = new Vector3(x, 0f, z);
        float height = GetTerrainHeight(worldPos);
        
        return new Vector3(x, height, z);
    }
    
    /// <summary>
    /// Get terrain height at world position
    /// </summary>
    private float GetTerrainHeight(Vector3 worldPosition)
    {
        if (terrain == null)
            return 0f;
        
        // Method 1: Use raycast for most accurate height (recommended)
        if (useRaycastForHeight)
        {
            RaycastHit hit;
            Vector3 rayStart = new Vector3(worldPosition.x, 1000f, worldPosition.z);
            
            if (Physics.Raycast(rayStart, Vector3.down, out hit, 2000f))
            {
                if (showPlacementDebug)
                {
                    Debug.DrawLine(rayStart, hit.point, Color.green, 2f);
                }
                return hit.point.y;
            }
            else if (showPlacementDebug)
            {
                Debug.LogWarning($"[AssetRandomizer] Raycast missed at ({worldPosition.x:F1}, {worldPosition.z:F1})");
            }
        }
        
        // Method 2: Fallback to SampleHeight
        // SampleHeight returns height relative to terrain base
        // We need to add terrain's Y position to get world height
        float terrainHeight = terrain.SampleHeight(worldPosition);
        float worldHeight = terrainHeight + terrain.transform.position.y;
        
        if (showPlacementDebug)
        {
            Debug.Log($"[AssetRandomizer] SampleHeight at ({worldPosition.x:F1}, {worldPosition.z:F1}): terrain={terrainHeight:F2}, world={worldHeight:F2}");
        }
        
        return worldHeight;
    }

    /// <summary>
    /// Check if position is valid for placement
    /// </summary>
    private bool IsValidPlacement(Vector3 position, float minDistanceFromRoad)
    {
        // Check distance from road
        float distanceFromRoad = Mathf.Abs(position.z - roadCenterZ);
        if (distanceFromRoad < minDistanceFromRoad)
            return false;
        
        // Check if within road width (double check)
        if (distanceFromRoad < roadWidth / 2f)
            return false;
        
        // Check terrain slope
        if (terrain != null)
        {
            Vector3 normal = terrain.terrainData.GetInterpolatedNormal(
                (position.x - terrain.transform.position.x) / terrain.terrainData.size.x,
                (position.z - terrain.transform.position.z) / terrain.terrainData.size.z
            );
            
            float slope = Vector3.Angle(normal, Vector3.up);
            if (slope > maxSlopeAngle)
                return false;
        }
        
        return true;
    }

    /// <summary>
    /// Align object to terrain normal
    /// </summary>
    private void AlignToTerrain(Transform obj, Vector3 position)
    {
        if (terrain == null)
            return;
        
        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(
            (position.x - terrain.transform.position.x) / terrain.terrainData.size.x,
            (position.z - terrain.transform.position.z) / terrain.terrainData.size.z
        );
        
        obj.up = normal;
    }

    /// <summary>
    /// Set placement area from route
    /// </summary>
    public void SetPlacementArea(float startX, float endX)
    {
        placementStartX = startX;
        placementEndX = endX;
        
        if (showDebugInfo)
            Debug.Log($"[AssetRandomizer] Placement area set: {startX}m to {endX}m");
    }

    /// <summary>
    /// Set road center Z position
    /// </summary>
    public void SetRoadCenterZ(float z)
    {
        roadCenterZ = z;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || terrain == null)
            return;
        
        // Draw road area (red)
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Vector3 roadStart = new Vector3(placementStartX, 0f, roadCenterZ - roadWidth / 2f);
        Vector3 roadEnd = new Vector3(placementEndX, 0f, roadCenterZ + roadWidth / 2f);
        Vector3 roadSize = new Vector3(placementEndX - placementStartX, 1f, roadWidth);
        Vector3 roadCenter = new Vector3((placementStartX + placementEndX) / 2f, 0f, roadCenterZ);
        Gizmos.DrawCube(roadCenter, roadSize);
        
        // Draw placement area (green)
        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
        Vector3 placementSize = new Vector3(placementEndX - placementStartX, 1f, placementWidthZ * 2f);
        Gizmos.DrawCube(roadCenter, placementSize);
    }
}
