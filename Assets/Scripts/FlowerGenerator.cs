using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generates flowers randomly across the terrain to enrich the environment
/// Loads flower textures from Assets/Textures/GrassFlowers and places them as quads
/// </summary>
public class FlowerGenerator : MonoBehaviour
{
    [Header("Flower Settings")]
    [Tooltip("Number of flowers to spawn")]
    [Range(50, 500)]
    [SerializeField] private int flowerCount = 200;
    
    [Tooltip("Minimum distance between flowers (meters)")]
    [Range(0.5f, 5f)]
    [SerializeField] private float minFlowerSpacing = 1.5f;
    
    [Tooltip("Flower spawn area width from road edge (meters)")]
    [Range(5f, 100f)]
    [SerializeField] private float flowerSpawnWidth = 50f;
    
    [Tooltip("Safe distance from road (meters)")]
    [Range(1f, 10f)]
    [SerializeField] private float safeDistanceFromRoad = 3f;

    [Header("Flower Appearance")]
    [Tooltip("Flower size range (meters)")]
    [SerializeField] private Vector2 flowerSizeRange = new Vector2(0.3f, 0.8f);
    
    [Tooltip("Random rotation for flowers")]
    [SerializeField] private bool randomRotation = true;
    
    [Tooltip("Random scale variation")]
    [SerializeField] private bool randomScale = true;
    
    [Tooltip("Scale variation range (0.7 = 70%, 1.3 = 130%)")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.7f, 1.3f);

    [Header("Flower Textures")]
    [Tooltip("Flower textures to use (auto-loaded if empty)")]
    [SerializeField] private Texture2D[] flowerTextures;

    [Header("References")]
    [Tooltip("Terrain to place flowers on")]
    [SerializeField] private Terrain terrain;
    
    [Tooltip("SimpleTerrainBuilder for road information")]
    [SerializeField] private SimpleTerrainBuilder terrainBuilder;
    
    [Tooltip("CurvedRoadGenerator for curved road information (optional)")]
    [SerializeField] private CurvedRoadGenerator curvedRoadGenerator;

    [Header("Random Seed")]
    [Tooltip("Random seed for reproducible generation")]
    [SerializeField] private int randomSeed = 0;
    
    [Tooltip("Use random seed each time")]
    [SerializeField] private bool useRandomSeed = true;

    [Header("Height Detection")]
    [Tooltip("Use Raycast for more accurate height detection (recommended)")]
    [SerializeField] private bool useRaycastForHeight = true;
    
    [Tooltip("Raycast distance (meters)")]
    [SerializeField] private float raycastDistance = 200f;

    private Transform flowersParent;
    private List<GameObject> spawnedFlowers = new List<GameObject>();
    private float roadCenterZ;
    private float roadWidth;

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

        if (curvedRoadGenerator == null)
        {
            curvedRoadGenerator = FindObjectOfType<CurvedRoadGenerator>();
        }

        // Get road information
        if (terrainBuilder != null)
        {
            roadWidth = terrainBuilder.GetRoadWidth();
            roadCenterZ = terrainBuilder.GetRoadCenterZ();
        }
        else if (terrain != null)
        {
            // Fallback: assume road is in center
            roadCenterZ = terrain.terrainData.size.z / 2f;
            roadWidth = 15f; // Default road width
        }

        // Load flower textures if not assigned
        if (flowerTextures == null || flowerTextures.Length == 0)
        {
            LoadFlowerTextures();
        }
    }

    /// <summary>
    /// Load flower textures from Assets/Textures/GrassFlowers
    /// </summary>
    private void LoadFlowerTextures()
    {
        List<Texture2D> textures = new List<Texture2D>();

#if UNITY_EDITOR
        // Load textures from the folder
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Textures/GrassFlowers" });
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            
            // Only load flower textures (not grass textures)
            if (texture != null && path.Contains("grassFlower"))
            {
                textures.Add(texture);
            }
        }
#endif

        flowerTextures = textures.ToArray();
        Debug.Log($"[FlowerGenerator] Loaded {flowerTextures.Length} flower textures");
    }

    /// <summary>
    /// Set random seed for reproducible generation
    /// </summary>
    public void SetRandomSeed(int seed)
    {
        randomSeed = seed;
        Random.InitState(seed);
    }

    /// <summary>
    /// Generate flowers across the terrain
    /// </summary>
    [ContextMenu("Generate Flowers")]
    public void GenerateFlowers()
    {
        // Clear existing flowers
        ClearFlowers();

        // Set random seed
        if (useRandomSeed && randomSeed == 0)
        {
            randomSeed = Random.Range(1, 999999);
        }
        Random.InitState(randomSeed);

        // Create parent object
        if (flowersParent == null)
        {
            GameObject parentObj = new GameObject("Flowers");
            flowersParent = parentObj.transform;
        }

        if (terrain == null)
        {
            Debug.LogError("[FlowerGenerator] No terrain found!");
            return;
        }

        if (flowerTextures == null || flowerTextures.Length == 0)
        {
            LoadFlowerTextures();
            if (flowerTextures == null || flowerTextures.Length == 0)
            {
                Debug.LogError("[FlowerGenerator] No flower textures found! Please assign textures or ensure they exist in Assets/Textures/GrassFlowers");
                return;
            }
        }

        TerrainData data = terrain.terrainData;
        Vector3 size = data.size;

        List<Vector3> placedPositions = new List<Vector3>();
        int flowersPlaced = 0;
        int attempts = 0;
        int maxAttempts = flowerCount * 10; // Prevent infinite loop

        while (flowersPlaced < flowerCount && attempts < maxAttempts)
        {
            attempts++;

            // Random position across terrain
            float x = Random.Range(0f, size.x);
            float z = Random.Range(0f, size.z);
            Vector3 position = new Vector3(x, 0, z);

            // Check if position is valid (not on road, not too close to other flowers)
            if (IsValidFlowerPosition(position, placedPositions))
            {
                // Get terrain height using the best method available
                Vector3 worldPos = new Vector3(position.x, 0, position.z);
                float terrainHeight = GetTerrainHeight(worldPos);
                worldPos.y = terrainHeight + 0.01f; // Slightly above terrain to prevent z-fighting

                // Spawn flower at correct height
                GameObject flower = CreateFlowerQuad(worldPos);
                
                // Ensure flower is at correct height after creation
                if (flower != null)
                {
                    Vector3 finalPos = flower.transform.position;
                    // Re-check terrain height to ensure accuracy
                    float finalHeight = GetTerrainHeight(new Vector3(finalPos.x, 0, finalPos.z));
                    finalPos.y = finalHeight + 0.01f;
                    flower.transform.position = finalPos;
                    
                    placedPositions.Add(worldPos);
                    spawnedFlowers.Add(flower);
                    flowersPlaced++;
                }
            }
        }

        Debug.Log($"[FlowerGenerator] ✓ Generated {flowersPlaced} flowers (Seed: {randomSeed})");
    }

    /// <summary>
    /// Check if a position is valid for placing a flower
    /// </summary>
    private bool IsValidFlowerPosition(Vector3 position, List<Vector3> placedPositions)
    {
        // Check distance from road
        float distanceFromRoad = GetDistanceFromRoad(position);
        if (distanceFromRoad < safeDistanceFromRoad)
        {
            return false; // Too close to road
        }

        // Check if within spawn area
        if (distanceFromRoad > (roadWidth / 2f) + flowerSpawnWidth)
        {
            return false; // Too far from road
        }

        // Check distance from other flowers
        foreach (Vector3 placedPos in placedPositions)
        {
            float distance = Vector3.Distance(new Vector3(position.x, 0, position.z), new Vector3(placedPos.x, 0, placedPos.z));
            if (distance < minFlowerSpacing)
            {
                return false; // Too close to another flower
            }
        }

        return true;
    }

    /// <summary>
    /// Get terrain height at a given X,Z position using the best available method
    /// </summary>
    private float GetTerrainHeight(Vector3 position)
    {
        if (useRaycastForHeight)
        {
            // Use Raycast for more accurate height detection
            RaycastHit hit;
            Vector3 rayStart = new Vector3(position.x, position.y + 100f, position.z); // Start ray high above
            Vector3 rayDirection = Vector3.down;
            
            // Raycast against terrain layer
            int terrainLayer = LayerMask.GetMask("Default");
            if (terrainLayer == 0)
            {
                // If no layer mask, raycast against everything
                if (Physics.Raycast(rayStart, rayDirection, out hit, raycastDistance))
                {
                    return hit.point.y;
                }
            }
            else
            {
                if (Physics.Raycast(rayStart, rayDirection, out hit, raycastDistance, terrainLayer))
                {
                    return hit.point.y;
                }
            }
        }
        
        // Fallback to SampleHeight if raycast fails or is disabled
        if (terrain != null)
        {
            return terrain.SampleHeight(position);
        }
        
        return 0f; // Default height if all else fails
    }

    /// <summary>
    /// Get distance from road center
    /// </summary>
    private float GetDistanceFromRoad(Vector3 position)
    {
        if (curvedRoadGenerator != null && curvedRoadGenerator.enableCurvedRoad)
        {
            // For curved roads, calculate distance from curved road center
            TerrainData data = terrain.terrainData;
            float normalizedX = position.x / data.size.x;
            float roadCenterZAtX = curvedRoadGenerator.GetRoadCenterZAtX(normalizedX);
            float baseRoadCenterZ = data.size.z / 2f;
            float actualRoadCenterZ = baseRoadCenterZ + roadCenterZAtX;
            
            return Mathf.Abs(position.z - actualRoadCenterZ);
        }
        else
        {
            // For straight roads
            return Mathf.Abs(position.z - roadCenterZ);
        }
    }

    /// <summary>
    /// Create a flower quad at the specified position
    /// </summary>
    private GameObject CreateFlowerQuad(Vector3 position)
    {
        if (flowerTextures == null || flowerTextures.Length == 0)
        {
            return null;
        }

        // Randomly select a flower texture
        Texture2D flowerTexture = flowerTextures[Random.Range(0, flowerTextures.Length)];

        // Create quad
        GameObject flower = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flower.name = $"Flower_{flowerTexture.name}";
        
        // Set parent first (important for world position)
        if (flowersParent != null)
        {
            flower.transform.SetParent(flowersParent);
        }
        
        // Set position AFTER setting parent to ensure correct world position
        flower.transform.position = position;
        
        // Verify position is correct using the same method as generation
        Vector3 correctedPos = position;
        float terrainHeight = GetTerrainHeight(new Vector3(position.x, 0, position.z));
        correctedPos.y = terrainHeight + 0.01f;
        flower.transform.position = correctedPos;

        // Set texture with proper material settings
        Renderer renderer = flower.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Use Unlit/Transparent shader for better appearance
            Material material = new Material(Shader.Find("Unlit/Transparent"));
            material.mainTexture = flowerTexture;
            
            // Enable alpha transparency
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000; // Transparent queue
            
            renderer.material = material;
        }
        
        // Remove collider (flowers don't need physics)
        Collider collider = flower.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }

        // Random rotation
        if (randomRotation)
        {
            flower.transform.Rotate(0, Random.Range(0f, 360f), 0);
        }

        // Random scale
        if (randomScale)
        {
            float scale = Random.Range(scaleRange.x, scaleRange.y);
            float size = Random.Range(flowerSizeRange.x, flowerSizeRange.y);
            flower.transform.localScale = Vector3.one * size * scale;
        }
        else
        {
            float size = Random.Range(flowerSizeRange.x, flowerSizeRange.y);
            flower.transform.localScale = Vector3.one * size;
        }

        // Make it face camera (billboard effect) - optional
        // You can add a script to make flowers always face camera if desired

        return flower;
    }

    /// <summary>
    /// Clear all spawned flowers
    /// </summary>
    [ContextMenu("Clear Flowers")]
    public void ClearFlowers()
    {
        foreach (GameObject flower in spawnedFlowers)
        {
            if (flower != null)
            {
                DestroyImmediate(flower);
            }
        }
        spawnedFlowers.Clear();

        if (flowersParent != null)
        {
            DestroyImmediate(flowersParent.gameObject);
            flowersParent = null;
        }

        Debug.Log("[FlowerGenerator] Cleared all flowers");
    }

    /// <summary>
    /// Regenerate flowers with new random seed
    /// </summary>
    [ContextMenu("Regenerate Flowers")]
    public void RegenerateFlowers()
    {
        if (useRandomSeed)
        {
            randomSeed = Random.Range(1, 999999);
        }
        GenerateFlowers();
    }
}
