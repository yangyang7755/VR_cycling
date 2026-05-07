using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates random trees and landscape elements along the sides of the road
/// Regenerates on each trial start for variety
/// </summary>
public class LandscapeGenerator : MonoBehaviour
{
    [Header("Terrain Reference")]
    [Tooltip("Terrain to place objects on")]
    [SerializeField] private Terrain terrain;

    [Header("Road Settings")]
    [Tooltip("Road center position (Z coordinate - road is horizontal, center is in Z direction)")]
    [SerializeField] private float roadCenterZ = 0f;
    
    [Tooltip("Road width in meters")]
    [SerializeField] private float roadWidth = 15f;
    
    [Tooltip("Safe distance from road edge (meters) - objects won't spawn closer than this")]
    [SerializeField] private float safeDistanceFromRoad = 2f;

    [Header("Tree Settings")]
    [Tooltip("Tree prefabs to randomly spawn")]
    [SerializeField] private GameObject[] treePrefabs;
    
    [Tooltip("Number of trees to spawn per side of road")]
    [Range(10, 200)]
    [SerializeField] private int treesPerSide = 50;
    
    [Tooltip("Minimum distance between trees (meters)")]
    [Range(2f, 20f)]
    [SerializeField] private float minTreeSpacing = 4f;
    
    [Tooltip("Tree spawn area width from road edge (meters)")]
    [Range(10f, 100f)]
    [SerializeField] private float treeSpawnWidth = 60f;
    
    [Tooltip("Tree density variation (0 = uniform, 1 = very varied)")]
    [Range(0f, 1f)]
    [SerializeField] private float treeDensityVariation = 0.5f;
    
    [Tooltip("Create tree clusters (groups of trees)")]
    [SerializeField] private bool createTreeClusters = true;
    
    [Tooltip("Number of tree clusters per side")]
    [Range(3, 20)]
    [SerializeField] private int treeClustersPerSide = 8;
    
    [Tooltip("Trees per cluster")]
    [Range(2, 10)]
    [SerializeField] private int treesPerCluster = 4;

    [Header("Landscape Settings")]
    [Tooltip("Landscape element prefabs (rocks, bushes, flowers, etc.)")]
    [SerializeField] private GameObject[] landscapePrefabs;
    
    [Tooltip("Number of landscape elements to spawn per side")]
    [Range(10, 100)]
    [SerializeField] private int landscapeElementsPerSide = 40;
    
    [Tooltip("Minimum distance between landscape elements (meters)")]
    [Range(1f, 10f)]
    [SerializeField] private float minLandscapeSpacing = 2f;
    
    [Tooltip("Landscape spawn area width from road edge (meters)")]
    [Range(10f, 80f)]
    [SerializeField] private float landscapeSpawnWidth = 40f;
    
    [Tooltip("Add variety to landscape element sizes")]
    [SerializeField] private bool varyLandscapeSizes = true;
    
    [Tooltip("Minimum scale for landscape elements")]
    [Range(0.3f, 1f)]
    [SerializeField] private float minLandscapeScale = 0.5f;
    
    [Tooltip("Maximum scale for landscape elements")]
    [Range(1f, 3f)]
    [SerializeField] private float maxLandscapeScale = 1.5f;

    [Header("Spawn Area")]
    [Tooltip("Terrain size to cover (X axis - road direction)")]
    [SerializeField] private float terrainLength = 500f;
    
    [Tooltip("Terrain width (Z axis - perpendicular to road)")]
    [SerializeField] private float terrainWidth = 200f;

    [Header("Parent Objects")]
    [Tooltip("Parent object for trees (created automatically if not set)")]
    [SerializeField] private Transform treesParent;
    
    [Tooltip("Parent object for landscape elements (created automatically if not set)")]
    [SerializeField] private Transform landscapeParent;

    private List<GameObject> spawnedObjects = new List<GameObject>();
    private int randomSeed = 0;

    /// <summary>
    /// Set random seed for reproducible generation
    /// </summary>
    public void SetRandomSeed(int seed)
    {
        randomSeed = seed;
        Random.InitState(seed);
    }

    private void Start()
    {
        // Auto-find terrain if not assigned
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        // Get terrain dimensions and road settings if available
        if (terrain != null)
        {
            TerrainData data = terrain.terrainData;
            terrainLength = data.size.x; // Road direction (left to right)
            terrainWidth = data.size.z;  // Perpendicular to road (front to back)
            roadCenterZ = data.size.z / 2f; // Road is in middle of Z axis (horizontal road)
        }

        // Get road settings from SimpleTerrainBuilder if available
        SimpleTerrainBuilder terrainBuilder = FindObjectOfType<SimpleTerrainBuilder>();
        if (terrainBuilder != null)
        {
            roadWidth = terrainBuilder.GetRoadWidth();
            roadCenterZ = terrainBuilder.GetRoadCenterZ();
            Debug.Log($"[LandscapeGenerator] Got road settings: width={roadWidth}m, center Z={roadCenterZ}");
        }
    }

    /// <summary>
    /// Generate landscape for current trial
    /// </summary>
    public void GenerateLandscape()
    {
        // Initialize random seed if not set
        if (randomSeed == 0)
        {
            randomSeed = Random.Range(1, 999999);
            Random.InitState(randomSeed);
        }
        else
        {
            Random.InitState(randomSeed);
        }

        // Clear existing landscape
        ClearLandscape();

        // Create parent objects if needed
        if (treesParent == null)
        {
            GameObject treesParentObj = new GameObject("Trees");
            treesParent = treesParentObj.transform;
        }

        if (landscapeParent == null)
        {
            GameObject landscapeParentObj = new GameObject("Landscape");
            landscapeParent = landscapeParentObj.transform;
        }

        // Generate trees on both sides of road
        if (createTreeClusters)
        {
            GenerateTreeClustersOnSide(true);  // Left side
            GenerateTreeClustersOnSide(false); // Right side
        }
        else
        {
            GenerateTreesOnSide(true);  // Left side
            GenerateTreesOnSide(false); // Right side
        }

        // Generate landscape elements on both sides
        GenerateLandscapeOnSide(true);  // Left side
        GenerateLandscapeOnSide(false); // Right side

        int totalTrees = spawnedObjects.Count - (landscapeElementsPerSide * 2);
        Debug.Log($"[LandscapeGenerator] ✓ Generated landscape: {totalTrees} trees, {landscapeElementsPerSide * 2} landscape elements");
    }

    private void GenerateTreesOnSide(bool leftSide)
    {
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogWarning("[LandscapeGenerator] No tree prefabs assigned!");
            return;
        }

        List<Vector3> placedPositions = new List<Vector3>();

        for (int i = 0; i < treesPerSide; i++)
        {
            // Try to find a valid position
            Vector3 position = FindValidPosition(leftSide, treeSpawnWidth, minTreeSpacing, placedPositions);
            
            if (position != Vector3.zero)
            {
                // Randomly select a tree prefab
                GameObject treePrefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
                
                // Spawn tree
                GameObject tree = Instantiate(treePrefab, position, Quaternion.identity, treesParent);
                
                // Random rotation around Y axis
                tree.transform.Rotate(0, Random.Range(0f, 360f), 0);
                
                // Random scale variation (80% to 120%)
                float scale = Random.Range(0.8f, 1.2f);
                tree.transform.localScale = Vector3.one * scale;
                
                // Adjust Y position to terrain height
                if (terrain != null)
                {
                    float terrainHeight = terrain.SampleHeight(position);
                    position.y = terrainHeight;
                    tree.transform.position = position;
                }
                
                placedPositions.Add(position);
                spawnedObjects.Add(tree);
            }
        }
    }

    private void GenerateTreeClustersOnSide(bool leftSide)
    {
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogWarning("[LandscapeGenerator] No tree prefabs assigned!");
            return;
        }

        List<Vector3> clusterCenters = new List<Vector3>();

        // Generate cluster centers
        for (int cluster = 0; cluster < treeClustersPerSide; cluster++)
        {
            Vector3 clusterCenter = FindValidPosition(leftSide, treeSpawnWidth, minTreeSpacing * 3f, clusterCenters);
            
            if (clusterCenter != Vector3.zero)
            {
                clusterCenters.Add(clusterCenter);
                
                // Generate trees in this cluster
                for (int treeIndex = 0; treeIndex < treesPerCluster; treeIndex++)
                {
                    // Random offset from cluster center
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float distance = Random.Range(2f, minTreeSpacing * 1.5f);
                    
                    Vector3 treePosition = clusterCenter + new Vector3(
                        Mathf.Cos(angle) * distance,
                        0,
                        Mathf.Sin(angle) * distance
                    );
                    
                    // Check if position is valid
                    float distanceFromRoad = Mathf.Abs(treePosition.z - roadCenterZ);
                    if (distanceFromRoad < (roadWidth / 2f) + safeDistanceFromRoad)
                    {
                        continue; // Too close to road
                    }
                    
                    // Randomly select a tree prefab
                    GameObject treePrefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
                    
                    // Spawn tree
                    GameObject tree = Instantiate(treePrefab, treePosition, Quaternion.identity, treesParent);
                    
                    // Random rotation around Y axis
                    tree.transform.Rotate(0, Random.Range(0f, 360f), 0);
                    
                    // Random scale variation (80% to 120%)
                    float scale = Random.Range(0.8f, 1.2f);
                    tree.transform.localScale = Vector3.one * scale;
                    
                    // Adjust Y position to terrain height
                    if (terrain != null)
                    {
                        float terrainHeight = terrain.SampleHeight(treePosition);
                        treePosition.y = terrainHeight;
                        tree.transform.position = treePosition;
                    }
                    
                    spawnedObjects.Add(tree);
                }
            }
        }
    }

    private void GenerateLandscapeOnSide(bool leftSide)
    {
        if (landscapePrefabs == null || landscapePrefabs.Length == 0)
        {
            // Landscape elements are optional
            return;
        }

        List<Vector3> placedPositions = new List<Vector3>();

        for (int i = 0; i < landscapeElementsPerSide; i++)
        {
            // Try to find a valid position
            Vector3 position = FindValidPosition(leftSide, landscapeSpawnWidth, minLandscapeSpacing, placedPositions);
            
            if (position != Vector3.zero)
            {
                // Randomly select a landscape prefab
                GameObject landscapePrefab = landscapePrefabs[Random.Range(0, landscapePrefabs.Length)];
                
                // Spawn landscape element
                GameObject element = Instantiate(landscapePrefab, position, Quaternion.identity, landscapeParent);
                
                // Random rotation around Y axis
                element.transform.Rotate(0, Random.Range(0f, 360f), 0);
                
                // Random scale variation
                float scale = varyLandscapeSizes 
                    ? Random.Range(minLandscapeScale, maxLandscapeScale)
                    : Random.Range(0.7f, 1.3f);
                element.transform.localScale = Vector3.one * scale;
                
                // Adjust Y position to terrain height
                if (terrain != null)
                {
                    float terrainHeight = terrain.SampleHeight(position);
                    position.y = terrainHeight;
                    element.transform.position = position;
                }
                
                placedPositions.Add(position);
                spawnedObjects.Add(element);
            }
        }
    }

    private Vector3 FindValidPosition(bool leftSide, float spawnWidth, float minSpacing, List<Vector3> existingPositions)
    {
        int maxAttempts = 50;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Random X position along road (terrain length - road goes left to right)
            float x = Random.Range(0f, terrainLength);
            
            // Random Z position on the specified side (road is horizontal, center is in Z)
            // Road center is at roadCenterZ, road extends roadWidth/2 on each side
            float roadTopEdge = roadCenterZ + (roadWidth / 2f);    // "Top" = positive Z (back)
            float roadBottomEdge = roadCenterZ - (roadWidth / 2f);  // "Bottom" = negative Z (front)
            
            float z;
            if (leftSide)
            {
                // Left side (front, negative Z): spawn between road bottom edge - safe distance and road bottom edge - safe distance - spawn width
                float minZ = roadBottomEdge - safeDistanceFromRoad - spawnWidth;
                float maxZ = roadBottomEdge - safeDistanceFromRoad;
                z = Random.Range(minZ, maxZ);
            }
            else
            {
                // Right side (back, positive Z): spawn between road top edge + safe distance and road top edge + safe distance + spawn width
                float minZ = roadTopEdge + safeDistanceFromRoad;
                float maxZ = roadTopEdge + safeDistanceFromRoad + spawnWidth;
                z = Random.Range(minZ, maxZ);
            }

            Vector3 position = new Vector3(x, 0, z);

            // Check if position is valid (not too close to existing objects)
            bool isValid = true;
            foreach (Vector3 existingPos in existingPositions)
            {
                float distance = Vector3.Distance(new Vector3(position.x, 0, position.z), new Vector3(existingPos.x, 0, existingPos.z));
                if (distance < minSpacing)
                {
                    isValid = false;
                    break;
                }
            }

            // Check if position is not on road
            if (isValid)
            {
                float distanceFromRoadCenter = Mathf.Abs(z - roadCenterZ);
                if (distanceFromRoadCenter < (roadWidth / 2f) + safeDistanceFromRoad)
                {
                    isValid = false; // Too close to road
                }
            }

            if (isValid)
            {
                return position;
            }
        }

        // Could not find valid position after max attempts
        return Vector3.zero;
    }

    /// <summary>
    /// Clear all spawned landscape objects
    /// </summary>
    public void ClearLandscape()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        spawnedObjects.Clear();

        // Also destroy parent objects if they exist
        if (treesParent != null)
        {
            DestroyImmediate(treesParent.gameObject);
            treesParent = null;
        }

        if (landscapeParent != null)
        {
            DestroyImmediate(landscapeParent.gameObject);
            landscapeParent = null;
        }

        Debug.Log("[LandscapeGenerator] ✓ Cleared all landscape objects");
    }

    [ContextMenu("Generate Landscape")]
    public void GenerateLandscapeManual()
    {
        GenerateLandscape();
    }

    [ContextMenu("Clear Landscape")]
    public void ClearLandscapeManual()
    {
        ClearLandscape();
    }
}
