using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Places mountains in the background (behind the horizon) to add depth and visual reference
/// Mountains are placed at various distances to create a layered effect
/// </summary>
public class MountainBackgroundGenerator : MonoBehaviour
{
    [Header("Mountain Prefab")]
    [Tooltip("Mountain prefab to instantiate (Free_Mountain.prefab)")]
    [SerializeField] private GameObject mountainPrefab;

    [Header("Placement Settings")]
    [Tooltip("Number of mountain layers (different distances)")]
    [Range(1, 5)]
    [SerializeField] private int mountainLayers = 3;

    [Tooltip("Base distance from road start (meters) - first layer")]
    [SerializeField] private float baseDistance = 800f;

    [Tooltip("Distance between layers (meters)")]
    [SerializeField] private float layerSpacing = 200f;

    [Tooltip("Number of mountains per layer")]
    [Range(1, 10)]
    [SerializeField] private int mountainsPerLayer = 3;

    [Tooltip("Spread width across terrain (meters) - how wide the mountain range is")]
    [SerializeField] private float spreadWidth = 400f;

    [Header("Position Settings")]
    [Tooltip("Road center Z position (road runs along X axis)")]
    [SerializeField] private float roadCenterZ = 0f;

    [Tooltip("Mountain base height offset (meters above terrain)")]
    [SerializeField] private float heightOffset = 0f;

    [Tooltip("Random height variation (meters)")]
    [SerializeField] private float heightVariation = 10f;

    [Header("Rotation Settings")]
    [Tooltip("Random rotation variation (degrees)")]
    [Range(0f, 360f)]
    [SerializeField] private float rotationVariation = 45f;

    [Tooltip("Base rotation around Y axis (degrees)")]
    [SerializeField] private float baseRotationY = 0f;

    [Header("Scale Settings")]
    [Tooltip("Base scale multiplier")]
    [Range(0.5f, 3f)]
    [SerializeField] private float baseScale = 1f;

    [Tooltip("Scale variation per layer (further = smaller)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float scaleVariationPerLayer = 0.2f;

    [Tooltip("Random scale variation")]
    [Range(0f, 0.3f)]
    [SerializeField] private float randomScaleVariation = 0.1f;

    [Header("Terrain Reference")]
    [Tooltip("Terrain to sample height from")]
    [SerializeField] private Terrain terrain;

    [Header("Randomization")]
    [Tooltip("Random seed for mountain placement (0 = random each time)")]
    [SerializeField] private int randomSeed = 0;

    [Tooltip("Regenerate mountains on each trial")]
    [SerializeField] private bool regenerateOnTrial = true;

    [Header("Parent Object")]
    [Tooltip("Parent object for all mountains (auto-created if null)")]
    [SerializeField] private Transform mountainsParent;

    private List<GameObject> spawnedMountains = new List<GameObject>();
    private int currentSeed = 0;

    private void Start()
    {
        // Auto-find terrain if not assigned
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        // Auto-find mountain prefab if not assigned
        if (mountainPrefab == null)
        {
            LoadMountainPrefab();
        }

        // Create parent object
        if (mountainsParent == null)
        {
            GameObject parentObj = new GameObject("Mountains");
            mountainsParent = parentObj.transform;
        }

        // Generate mountains
        if (randomSeed == 0)
        {
            currentSeed = Random.Range(1, 1000000);
        }
        else
        {
            currentSeed = randomSeed;
        }

        GenerateMountains();
    }

    /// <summary>
    /// Try to auto-load mountain prefab from FreeMountain assets
    /// </summary>
    private void LoadMountainPrefab()
    {
        #if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("Free_Mountain t:Prefab", new[] { "Assets" });
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            mountainPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (mountainPrefab != null)
            {
                Debug.Log($"[MountainBackgroundGenerator] ✓ Found mountain prefab: {path}");
            }
        }
        else
        {
            Debug.LogWarning("[MountainBackgroundGenerator] Could not find Free_Mountain prefab. Please assign manually in Inspector.");
        }
        #endif
    }

    /// <summary>
    /// Generate mountains in background
    /// </summary>
    [ContextMenu("Generate Mountains")]
    public void GenerateMountains()
    {
        // Clear existing mountains
        ClearMountains();

        if (mountainPrefab == null)
        {
            Debug.LogError("[MountainBackgroundGenerator] Mountain prefab not assigned! Please assign Free_Mountain.prefab in Inspector.");
            return;
        }

        // Initialize random with seed
        Random.InitState(currentSeed);

        Debug.Log($"[MountainBackgroundGenerator] Generating {mountainLayers} layers of mountains...");

        // Generate each layer
        for (int layer = 0; layer < mountainLayers; layer++)
        {
            float layerDistance = baseDistance + (layer * layerSpacing);
            float layerScale = baseScale - (layer * scaleVariationPerLayer); // Further = smaller

            GenerateMountainLayer(layer, layerDistance, layerScale);
        }

        Debug.Log($"[MountainBackgroundGenerator] ✓ Generated {spawnedMountains.Count} mountains total");
    }

    /// <summary>
    /// Generate a single layer of mountains
    /// </summary>
    private void GenerateMountainLayer(int layerIndex, float distance, float scale)
    {
        for (int i = 0; i < mountainsPerLayer; i++)
        {
            // Calculate position
            float xPosition = Random.Range(-spreadWidth / 2f, spreadWidth / 2f);
            float zPosition = distance + Random.Range(-50f, 50f); // Slight variation in distance

            Vector3 position = new Vector3(xPosition, 0, zPosition);

            // Sample terrain height
            if (terrain != null)
            {
                float terrainHeight = terrain.SampleHeight(position);
                position.y = terrainHeight + heightOffset + Random.Range(-heightVariation, heightVariation);
            }
            else
            {
                position.y = heightOffset;
            }

            // Calculate rotation
            float rotationY = baseRotationY + Random.Range(-rotationVariation, rotationVariation);

            // Calculate scale with variation
            float finalScale = scale + Random.Range(-randomScaleVariation, randomScaleVariation);
            finalScale = Mathf.Max(0.3f, finalScale); // Minimum scale

            // Instantiate mountain
            GameObject mountain = Instantiate(mountainPrefab, position, Quaternion.Euler(0, rotationY, 0), mountainsParent);
            mountain.transform.localScale = Vector3.one * finalScale;
            mountain.name = $"Mountain_Layer{layerIndex + 1}_{i + 1}";

            spawnedMountains.Add(mountain);
        }
    }

    /// <summary>
    /// Clear all spawned mountains
    /// </summary>
    [ContextMenu("Clear Mountains")]
    public void ClearMountains()
    {
        foreach (GameObject mountain in spawnedMountains)
        {
            if (mountain != null)
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(mountain);
                }
                else
                {
                    Destroy(mountain);
                }
                #else
                Destroy(mountain);
                #endif
            }
        }
        spawnedMountains.Clear();
        Debug.Log("[MountainBackgroundGenerator] Cleared all mountains");
    }

    /// <summary>
    /// Set random seed for reproducible generation
    /// </summary>
    public void SetRandomSeed(int seed)
    {
        randomSeed = seed;
        currentSeed = seed;
        if (Application.isPlaying)
        {
            GenerateMountains();
        }
    }

    /// <summary>
    /// Regenerate mountains (useful for new trials)
    /// </summary>
    [ContextMenu("Regenerate Mountains")]
    public void RegenerateMountains()
    {
        if (randomSeed == 0)
        {
            currentSeed = Random.Range(1, 1000000);
        }
        GenerateMountains();
    }

    /// <summary>
    /// Get number of spawned mountains
    /// </summary>
    public int GetMountainCount()
    {
        return spawnedMountains.Count;
    }

    /// <summary>
    /// Adjust mountain positions based on road center
    /// </summary>
    public void SetRoadCenterZ(float centerZ)
    {
        roadCenterZ = centerZ;
        // Reposition mountains if needed
        if (spawnedMountains.Count > 0)
        {
            RegenerateMountains();
        }
    }
}
