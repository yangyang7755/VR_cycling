using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generates near-field motion cues by placing roadside elements every 5-15m
/// Elements include: grass, flowers, rocks, and road cracks
/// This creates visual motion parallax to enhance the sense of forward movement
/// </summary>
public class NearFieldMotionGenerator : MonoBehaviour
{
    [Header("Terrain Reference")]
    [Tooltip("Terrain to place elements on")]
    [SerializeField] private Terrain terrain;

    [Header("Road Settings")]
    [Tooltip("Road center position (Z coordinate)")]
    [SerializeField] private float roadCenterZ = 0f;
    
    [Tooltip("Road width in meters")]
    [SerializeField] private float roadWidth = 15f;
    
    [Tooltip("Safe distance from road edge (meters)")]
    [SerializeField] private float safeDistanceFromRoad = 1.5f;

    [Header("Spacing Settings")]
    [Tooltip("Minimum spacing between elements (meters)")]
    [Range(5f, 15f)]
    [SerializeField] private float minSpacing = 5f;
    
    [Tooltip("Maximum spacing between elements (meters)")]
    [Range(5f, 15f)]
    [SerializeField] private float maxSpacing = 15f;
    
    [Tooltip("Distance from road edge to place elements (meters)")]
    [Range(1f, 5f)]
    [SerializeField] private float distanceFromRoad = 2f;

    [Header("Element Types")]
    [Tooltip("Enable grass elements")]
    [SerializeField] private bool enableGrass = true;
    
    [Tooltip("Enable flower elements")]
    [SerializeField] private bool enableFlowers = true;
    
    [Tooltip("Enable rock elements")]
    [SerializeField] private bool enableRocks = true;
    
    [Tooltip("Enable road crack elements")]
    [SerializeField] private bool enableRoadCracks = true;

    [Header("Grass Settings")]
    [Tooltip("Grass prefabs or textures")]
    [SerializeField] private GameObject[] grassPrefabs;
    
    [Tooltip("Grass textures (auto-loaded if empty)")]
    [SerializeField] private Texture2D[] grassTextures;
    
    [Tooltip("Grass size range (meters)")]
    [SerializeField] private Vector2 grassSizeRange = new Vector2(0.4f, 0.8f);
    
    [Tooltip("Grass spawn probability (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float grassProbability = 0.4f;

    [Header("Flower Settings")]
    [Tooltip("Flower prefabs or textures")]
    [SerializeField] private GameObject[] flowerPrefabs;
    
    [Tooltip("Flower textures (auto-loaded from GrassFlowers if empty)")]
    [SerializeField] private Texture2D[] flowerTextures;
    
    [Tooltip("Flower size range (meters)")]
    [SerializeField] private Vector2 flowerSizeRange = new Vector2(0.3f, 0.6f);
    
    [Tooltip("Flower spawn probability (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float flowerProbability = 0.3f;

    [Header("Rock Settings")]
    [Tooltip("Rock prefabs")]
    [SerializeField] private GameObject[] rockPrefabs;
    
    [Tooltip("Rock textures (for simple quads)")]
    [SerializeField] private Texture2D[] rockTextures;
    
    [Tooltip("Rock size range (meters)")]
    [SerializeField] private Vector2 rockSizeRange = new Vector2(0.2f, 0.5f);
    
    [Tooltip("Rock spawn probability (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float rockProbability = 0.2f;

    [Header("Road Crack Settings")]
    [Tooltip("Road crack prefabs or quads")]
    [SerializeField] private GameObject[] roadCrackPrefabs;
    
    [Tooltip("Road crack textures")]
    [SerializeField] private Texture2D[] roadCrackTextures;
    
    [Tooltip("Road crack size range (meters)")]
    [SerializeField] private Vector2 roadCrackSizeRange = new Vector2(0.5f, 1.5f);
    
    [Tooltip("Road crack spawn probability (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float roadCrackProbability = 0.15f;
    
    [Tooltip("Place cracks on road surface (not roadside)")]
    [SerializeField] private bool placeCracksOnRoad = true;

    [Header("Course Settings")]
    [Tooltip("Total course length (meters)")]
    [SerializeField] private float courseLength = 500f;
    
    [Tooltip("Start offset from beginning (meters)")]
    [SerializeField] private float startOffset = 10f;
    
    [Tooltip("End offset from end (meters)")]
    [SerializeField] private float endOffset = 10f;

    [Header("References")]
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
    [Tooltip("Use Raycast for accurate height detection")]
    [SerializeField] private bool useRaycastForHeight = true;
    
    [Tooltip("Raycast distance (meters)")]
    [SerializeField] private float raycastDistance = 200f;

    private Transform elementsParent;
    private List<GameObject> spawnedElements = new List<GameObject>();
    private System.Random random;

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
            roadCenterZ = terrain.terrainData.size.z / 2f;
        }

        // Load textures if not assigned
        LoadTextures();

        // Generate elements
        GenerateNearFieldElements();
    }

    /// <summary>
    /// Set random seed for reproducible generation
    /// </summary>
    public void SetRandomSeed(int seed)
    {
        randomSeed = seed;
        if (random != null)
        {
            random = new System.Random(seed);
        }
    }

    /// <summary>
    /// Clear all spawned elements
    /// </summary>
    public void ClearElements()
    {
        foreach (GameObject obj in spawnedElements)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        spawnedElements.Clear();

        if (elementsParent != null)
        {
            DestroyImmediate(elementsParent.gameObject);
            elementsParent = null;
        }
    }

    /// <summary>
    /// Regenerate elements with new seed
    /// </summary>
    public void Regenerate()
    {
        ClearElements();
        if (useRandomSeed)
        {
            randomSeed = Random.Range(0, 100000);
        }
        GenerateNearFieldElements();
    }

    private void LoadTextures()
    {
        // Load grass textures
        if (grassTextures == null || grassTextures.Length == 0)
        {
            #if UNITY_EDITOR
            string[] grassGuids = AssetDatabase.FindAssets("grass t:Texture2D");
            List<Texture2D> loadedGrass = new List<Texture2D>();
            foreach (string guid in grassGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) loadedGrass.Add(tex);
            }
            if (loadedGrass.Count > 0)
            {
                grassTextures = loadedGrass.ToArray();
                Debug.Log($"[NearFieldMotionGenerator] Auto-loaded {grassTextures.Length} grass textures");
            }
            #endif
        }

        // Load flower textures from GrassFlowers
        if (flowerTextures == null || flowerTextures.Length == 0)
        {
            #if UNITY_EDITOR
            string[] flowerGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/GrassFlowers" });
            List<Texture2D> loadedFlowers = new List<Texture2D>();
            foreach (string guid in flowerGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) loadedFlowers.Add(tex);
            }
            if (loadedFlowers.Count > 0)
            {
                flowerTextures = loadedFlowers.ToArray();
                Debug.Log($"[NearFieldMotionGenerator] Auto-loaded {flowerTextures.Length} flower textures");
            }
            #endif
        }

        // Load rock textures
        if (rockTextures == null || rockTextures.Length == 0)
        {
            #if UNITY_EDITOR
            string[] rockGuids = AssetDatabase.FindAssets("rock t:Texture2D");
            List<Texture2D> loadedRocks = new List<Texture2D>();
            foreach (string guid in rockGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) loadedRocks.Add(tex);
            }
            if (loadedRocks.Count > 0)
            {
                rockTextures = loadedRocks.ToArray();
                Debug.Log($"[NearFieldMotionGenerator] Auto-loaded {rockTextures.Length} rock textures");
            }
            #endif
        }
    }

    private void GenerateNearFieldElements()
    {
        if (terrain == null)
        {
            Debug.LogError("[NearFieldMotionGenerator] Terrain is null!");
            return;
        }

        // Initialize random
        if (useRandomSeed)
        {
            randomSeed = Random.Range(0, 100000);
        }
        random = new System.Random(randomSeed);

        // Create parent object
        if (elementsParent == null)
        {
            GameObject parentObj = new GameObject("NearFieldElements");
            elementsParent = parentObj.transform;
            elementsParent.SetParent(transform);
        }

        // Generate elements along the road
        float currentX = startOffset;
        int elementCount = 0;

        while (currentX < courseLength - endOffset)
        {
            // Determine spacing for this element
            float spacing = (float)(random.NextDouble() * (maxSpacing - minSpacing) + minSpacing);
            currentX += spacing;

            if (currentX >= courseLength - endOffset) break;

            // Determine which side to place element (left or right)
            bool placeOnLeft = random.NextDouble() < 0.5f;

            // Determine element type based on probabilities
            float rand = (float)random.NextDouble();
            ElementType elementType = ElementType.None;

            if (enableGrass && rand < grassProbability)
            {
                elementType = ElementType.Grass;
            }
            else if (enableFlowers && rand < grassProbability + flowerProbability)
            {
                elementType = ElementType.Flower;
            }
            else if (enableRocks && rand < grassProbability + flowerProbability + rockProbability)
            {
                elementType = ElementType.Rock;
            }
            else if (enableRoadCracks && rand < grassProbability + flowerProbability + rockProbability + roadCrackProbability)
            {
                elementType = ElementType.RoadCrack;
            }

            // Place element if type was determined
            if (elementType != ElementType.None)
            {
                PlaceElement(elementType, currentX, placeOnLeft);
                elementCount++;
            }
        }

        Debug.Log($"[NearFieldMotionGenerator] Generated {elementCount} near-field motion elements along {courseLength}m course");
    }

    private void PlaceElement(ElementType type, float xPosition, bool onLeft)
    {
        // Calculate Z position (distance from road center)
        float zOffset = (roadWidth / 2f) + distanceFromRoad;
        if (onLeft)
        {
            zOffset = -zOffset; // Left side (negative Z)
        }

        // Get road center Z at this X position (accounting for curves)
        float roadZ = roadCenterZ;
        if (curvedRoadGenerator != null && curvedRoadGenerator.enableCurvedRoad)
        {
            float normalizedX = xPosition / courseLength;
            float curveOffset = curvedRoadGenerator.GetRoadCenterZAtX(normalizedX);
            roadZ = roadCenterZ + curveOffset;
        }

        float worldZ = roadZ + zOffset;

        // Calculate world position
        Vector3 worldPos = new Vector3(xPosition, 0, worldZ);

        // Get height at this position
        float height = GetTerrainHeight(worldPos);
        worldPos.y = height;

        // For road cracks, place on road surface
        if (type == ElementType.RoadCrack && placeCracksOnRoad)
        {
            worldZ = roadZ + ((float)random.NextDouble() - 0.5f) * (roadWidth * 0.6f); // Random position on road
            worldPos.z = worldZ;
            worldPos.y = GetTerrainHeight(worldPos);
        }

        // Create element
        GameObject element = CreateElement(type, worldPos);
        if (element != null)
        {
            element.transform.SetParent(elementsParent);
            spawnedElements.Add(element);
        }
    }

    private GameObject CreateElement(ElementType type, Vector3 position)
    {
        GameObject element = null;
        Vector2 sizeRange = Vector2.zero;
        Texture2D[] textures = null;
        GameObject[] prefabs = null;

        switch (type)
        {
            case ElementType.Grass:
                sizeRange = grassSizeRange;
                textures = grassTextures;
                prefabs = grassPrefabs;
                break;
            case ElementType.Flower:
                sizeRange = flowerSizeRange;
                textures = flowerTextures;
                prefabs = flowerPrefabs;
                break;
            case ElementType.Rock:
                sizeRange = rockSizeRange;
                textures = rockTextures;
                prefabs = rockPrefabs;
                break;
            case ElementType.RoadCrack:
                sizeRange = roadCrackSizeRange;
                textures = roadCrackTextures;
                prefabs = roadCrackPrefabs;
                break;
        }

        // Try to use prefab first
        if (prefabs != null && prefabs.Length > 0)
        {
            GameObject prefab = prefabs[random.Next(prefabs.Length)];
            if (prefab != null)
            {
                element = Instantiate(prefab, position, Quaternion.identity);
                // Randomize scale
                float scale = (float)(random.NextDouble() * (sizeRange.y - sizeRange.x) + sizeRange.x);
                element.transform.localScale = Vector3.one * scale;
                // Random rotation
                element.transform.rotation = Quaternion.Euler(0, (float)(random.NextDouble() * 360), 0);
            }
        }

        // If no prefab, create quad from texture
        if (element == null && textures != null && textures.Length > 0)
        {
            Texture2D texture = textures[random.Next(textures.Length)];
            if (texture != null)
            {
                element = CreateQuadFromTexture(texture, position, sizeRange);
            }
        }

        // If still no element, create a simple colored quad
        if (element == null)
        {
            element = CreateSimpleQuad(type, position, sizeRange);
        }

        return element;
    }

    private GameObject CreateQuadFromTexture(Texture2D texture, Vector3 position, Vector2 sizeRange)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = $"NearField_{texture.name}";

        // Set material
        Material mat = new Material(Shader.Find("Unlit/Transparent"));
        mat.mainTexture = texture;
        mat.SetFloat("_CullMode", 0); // No culling
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000; // Transparent queue

        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.material = mat;

        // Set size
        float size = (float)(random.NextDouble() * (sizeRange.y - sizeRange.x) + sizeRange.x);
        quad.transform.localScale = new Vector3(size, size, 1f);

        // Position and orient
        quad.transform.position = position;
        quad.transform.rotation = Quaternion.LookRotation(Camera.main != null ? 
            (Camera.main.transform.position - position) : Vector3.forward, Vector3.up);
        
        // Random rotation around Y axis
        quad.transform.Rotate(0, (float)(random.NextDouble() * 360), 0);

        // Remove collider (not needed for visual elements)
        Collider col = quad.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        return quad;
    }

    private GameObject CreateSimpleQuad(ElementType type, Vector3 position, Vector2 sizeRange)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = $"NearField_{type}";

        // Set color based on type
        Color color = Color.green;
        switch (type)
        {
            case ElementType.Grass:
                color = new Color(0.2f, 0.6f, 0.2f); // Dark green
                break;
            case ElementType.Flower:
                color = new Color(1f, 0.5f, 0.8f); // Pink
                break;
            case ElementType.Rock:
                color = new Color(0.4f, 0.4f, 0.4f); // Gray
                break;
            case ElementType.RoadCrack:
                color = new Color(0.1f, 0.1f, 0.1f); // Dark gray/black
                break;
        }

        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = color;

        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.material = mat;

        // Set size
        float size = (float)(random.NextDouble() * (sizeRange.y - sizeRange.x) + sizeRange.x);
        quad.transform.localScale = new Vector3(size, size, 1f);

        // Position and orient
        quad.transform.position = position;
        quad.transform.rotation = Quaternion.LookRotation(Camera.main != null ? 
            (Camera.main.transform.position - position) : Vector3.forward, Vector3.up);
        
        // Random rotation
        quad.transform.Rotate(0, (float)(random.NextDouble() * 360), 0);

        // Remove collider
        Collider col = quad.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        return quad;
    }

    private float GetTerrainHeight(Vector3 position)
    {
        if (terrain == null) return 0f;

        if (useRaycastForHeight)
        {
            RaycastHit hit;
            Vector3 rayStart = position + Vector3.up * raycastDistance;
            if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance * 2f))
            {
                return hit.point.y + 0.01f; // Slightly above surface
            }
        }

        // Fallback to terrain sample height
        float height = terrain.SampleHeight(position);
        return height + 0.01f;
    }

    private void DestroyImmediate(Object obj)
    {
        if (obj == null) return;
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(obj);
        }
        else
        {
            Destroy(obj);
        }
        #else
        Destroy(obj);
        #endif
    }

    private enum ElementType
    {
        None,
        Grass,
        Flower,
        Rock,
        RoadCrack
    }
}
