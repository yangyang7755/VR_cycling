using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Complete scene reset - handles terrain, road, and biker setup
/// Call this at the start of each trial/game to reset everything
/// </summary>
public class CompleteSceneReset : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private GameObject bikeObject;
    [SerializeField] private GameObject bikerModel;
    [SerializeField] private GameObject wheelsModel;

    [Header("Biker Settings")]
    [Tooltip("Exact start position for biker")]
    [SerializeField] private Vector3 bikerStartPosition = new Vector3(247.7f, 5f, 0f);
    
    [Tooltip("Exact start rotation")]
    [SerializeField] private Vector3 bikerStartRotation = Vector3.zero;
    
    [Tooltip("Exact start scale")]
    [SerializeField] private Vector3 bikerStartScale = new Vector3(3f, 3f, 3f);
    
    [Tooltip("Wheel position offset relative to biker")]
    [SerializeField] private Vector3 wheelOffset = Vector3.zero;
    
    [Tooltip("Wheel rotation offset")]
    [SerializeField] private Vector3 wheelRotation = Vector3.zero;
    
    [Tooltip("Wheel scale")]
    [SerializeField] private Vector3 wheelScale = new Vector3(3f, 3f, 3f);

    [Header("Terrain Settings")]
    [Tooltip("Base height of terrain at start")]
    [SerializeField] private float baseHeight = 0.1f;
    
    [Tooltip("Gradient of the hill (5% = 0.05)")]
    [Range(0f, 0.2f)]
    [SerializeField] private float gradient = 0.05f;
    
    [Tooltip("Add visual hill variation")]
    [SerializeField] private bool addVisualVariation = true;
    
    [Tooltip("Hill variation amplitude")]
    [Range(0f, 0.1f)]
    [SerializeField] private float variationAmplitude = 0.02f;
    
    [Tooltip("Hill variation frequency")]
    [Range(0.5f, 5f)]
    [SerializeField] private float variationFrequency = 1f;
    
    [Tooltip("Add noise for natural variation")]
    [SerializeField] private bool addNoise = true;
    
    [Tooltip("Noise scale")]
    [SerializeField] private float noiseScale = 0.05f;
    
    [Tooltip("Noise amplitude")]
    [Range(0f, 0.05f)]
    [SerializeField] private float noiseAmplitude = 0.01f;

    [Header("Road Settings")]
    [Tooltip("Width of road in meters")]
    [SerializeField] private float roadWidth = 15f;
    
    [Tooltip("Road position offset")]
    [SerializeField] private float roadOffsetX = 0f;

    [Header("Textures")]
    [SerializeField] private TerrainLayer grassLayer;
    [SerializeField] private TerrainLayer roadLayer;

    [Header("Auto Reset")]
    [Tooltip("Automatically reset scene on Start")]
    [SerializeField] private bool resetOnStart = false; // Changed to false by default to prevent unwanted resets
    
    [Tooltip("Reset only terrain (not bike position)")]
    [SerializeField] private bool resetOnlyTerrain = false;

    private void Start()
    {
        if (resetOnStart)
        {
            if (resetOnlyTerrain)
            {
                ResetTerrain();
            }
            else
            {
                ResetCompleteScene();
            }
        }
    }

    [ContextMenu("Reset Complete Scene")]
    public void ResetCompleteScene()
    {
        Debug.Log("=== Starting Complete Scene Reset ===");
        
        // Step 1: Reset Terrain
        ResetTerrain();
        
        // Step 2: Reset Biker (only if not resetOnlyTerrain)
        if (!resetOnlyTerrain)
        {
            ResetBiker();
        }
        
        Debug.Log("=== Complete Scene Reset Finished ===");
    }
    
    [ContextMenu("Reset Only Terrain")]
    public void ResetOnlyTerrain()
    {
        Debug.Log("=== Resetting Terrain Only ===");
        ResetTerrain();
        Debug.Log("=== Terrain Reset Finished ===");
    }
    
    [ContextMenu("Reset Only Biker Position")]
    public void ResetOnlyBikerPosition()
    {
        Debug.Log("=== Resetting Biker Position Only ===");
        ResetBiker();
        Debug.Log("=== Biker Position Reset Finished ===");
    }

    private void ResetTerrain()
    {
        Debug.Log("--- Resetting Terrain ---");
        
        // Find terrain if not assigned
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogError("No terrain found! Cannot reset terrain.");
            return;
        }

        TerrainData data = terrain.terrainData;
        int width = data.heightmapResolution;
        int height = data.heightmapResolution;
        Vector3 size = data.size;

        // Step 1: Clear terrain
        float[,] heights = new float[width, height];
        data.SetHeights(0, 0, heights);
        
        // Clear textures
        int numLayers = data.terrainLayers.Length;
        if (numLayers > 0)
        {
            float[,,] alphamap = new float[data.alphamapWidth, data.alphamapHeight, numLayers];
            data.SetAlphamaps(0, 0, alphamap);
        }
        Debug.Log("✓ Terrain cleared");

        // Step 2: Setup textures
        SetupTextures(data);

        // Step 3: Add elevation profile with gradient
        AddElevationProfile(data, width, height, size);
        Debug.Log("✓ Elevation profile added");

        // Step 4: Paint road
        PaintRoad(data, width, height, size);
        Debug.Log("✓ Road painted");
    }

    private void SetupTextures(TerrainData data)
    {
        // Load textures if not assigned
        if (grassLayer == null)
        {
            LoadTexture("Grass", ref grassLayer);
        }

        if (roadLayer == null)
        {
            LoadTexture("road", ref roadLayer);
        }

        if (grassLayer == null || roadLayer == null)
        {
            Debug.LogWarning("Could not load textures! Terrain will be flat colored.");
            return;
        }

        // Set up terrain layers
        data.terrainLayers = new TerrainLayer[] { grassLayer, roadLayer };

        // Paint entire terrain with grass
        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        float[,,] alphamap = new float[width, height, 2];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                alphamap[x, z, 0] = 1f; // Grass
                alphamap[x, z, 1] = 0f; // Road
            }
        }

        data.SetAlphamaps(0, 0, alphamap);
        Debug.Log("✓ Textures set up");
    }

    private void AddElevationProfile(TerrainData data, int width, int height, Vector3 size)
    {
        float[,] heights = new float[width, height];

        // Calculate height increment
        float totalHeightGain = size.x * gradient;

        // Create elevation profile with gradient along X axis (road direction)
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Normalized position (0 to 1) along X axis
                float normalizedX = (float)x / (width - 1);
                
                // Base height with gradient (increases along X axis)
                float heightAtX = baseHeight + (normalizedX * totalHeightGain / size.y);
                
                // Add visual variation (undulation) if enabled
                if (addVisualVariation)
                {
                    float undulation = Mathf.Sin(normalizedX * Mathf.PI * 2f * variationFrequency) * variationAmplitude;
                    heightAtX += undulation;
                }
                
                // Add noise for natural variation if enabled
                if (addNoise)
                {
                    float noiseX = (float)x * noiseScale;
                    float noiseZ = (float)z * noiseScale;
                    float noise = Mathf.PerlinNoise(noiseX, noiseZ) * noiseAmplitude;
                    heightAtX += noise;
                }
                
                // Clamp to valid height range (0 to 1)
                heights[x, z] = Mathf.Clamp01(heightAtX);
            }
        }

        data.SetHeights(0, 0, heights);
    }

    private void PaintRoad(TerrainData data, int width, int height, Vector3 size)
    {
        if (roadLayer == null)
        {
            Debug.LogWarning("Road layer not set! Road will not be painted.");
            return;
        }

        float[,,] alphamap = data.GetAlphamaps(0, 0, width, height);

        // Find road layer index
        int roadIndex = -1;
        int grassIndex = 0;
        for (int i = 0; i < data.terrainLayers.Length; i++)
        {
            if (data.terrainLayers[i] == roadLayer)
            {
                roadIndex = i;
            }
            else
            {
                grassIndex = i;
            }
        }

        if (roadIndex == -1)
        {
            Debug.LogWarning("Road layer not found in terrain! Road will not be painted.");
            return;
        }

        // Calculate road center position (in Z direction, for horizontal road)
        int centerZ = height / 2;
        if (roadOffsetX != 0f)
        {
            float offsetInPixels = (roadOffsetX / size.z) * height;
            centerZ = Mathf.RoundToInt(centerZ + offsetInPixels);
        }

        // Calculate road width in pixels
        float roadWidthInPixels = (roadWidth / size.z) * height;
        int halfWidth = Mathf.RoundToInt(roadWidthInPixels / 2f);

        // Paint road from left to right (X axis direction)
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                int distFromCenter = Mathf.Abs(z - centerZ);
                
                if (distFromCenter <= halfWidth)
                {
                    alphamap[x, z, roadIndex] = 1f;
                    alphamap[x, z, grassIndex] = 0f;
                }
            }
        }

        data.SetAlphamaps(0, 0, alphamap);
    }

    private void ResetBiker()
    {
        Debug.Log("--- Resetting Biker ---");
        
        // Find or create bike object
        if (bikeObject == null)
        {
            bikeObject = GameObject.Find("Bike");
            if (bikeObject == null)
            {
                bikeObject = new GameObject("Bike");
            }
        }

        // Check if bikeObject is a prefab asset (not an instance in scene)
        #if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(bikeObject))
        {
            Debug.LogError("[CompleteSceneReset] Cannot modify prefab asset! Bike object must be an instance in the scene.");
            Debug.LogError("  Solution: Drag the Bike prefab into the scene to create an instance, then assign that instance.");
            return;
        }
        #endif

        // Set bike position and rotation (only in play mode or when explicitly called)
        if (Application.isPlaying || !Application.isPlaying)
        {
            bikeObject.transform.position = bikerStartPosition;
            bikeObject.transform.rotation = Quaternion.Euler(bikerStartRotation);
            bikeObject.transform.localScale = Vector3.one; // Bike object itself is scale 1
            bikeObject.SetActive(true);
            Debug.Log($"✓ Bike object set to position: {bikerStartPosition}, rotation: {bikerStartRotation}");
        }

        // Find or load biker model
        if (bikerModel == null)
        {
            bikerModel = FindModelInChildren("biker");
        }

        if (bikerModel == null)
        {
            bikerModel = LoadModel("biker");
        }

        // Find or load wheels model
        if (wheelsModel == null)
        {
            wheelsModel = FindModelInChildren("wheel");
        }

        if (wheelsModel == null)
        {
            wheelsModel = LoadModel("wheels");
        }

        // Reset biker model
        if (bikerModel != null)
        {
            // Check if bikerModel is a prefab asset
            #if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(bikerModel))
            {
                Debug.LogWarning("[CompleteSceneReset] Biker model is a prefab asset. Skipping transform modification.");
            }
            else
            {
            #endif
                // Make sure it's a child of bike object
                if (bikerModel.transform.parent != bikeObject.transform)
                {
                    bikerModel.transform.SetParent(bikeObject.transform);
                }

                bikerModel.SetActive(true);
                bikerModel.transform.localPosition = Vector3.zero;
                bikerModel.transform.localRotation = Quaternion.Euler(Vector3.zero);
                bikerModel.transform.localScale = bikerStartScale;
            #if UNITY_EDITOR
            }
            #endif
            
            // Enable renderer
            Renderer bikerRenderer = bikerModel.GetComponentInChildren<Renderer>();
            if (bikerRenderer != null)
            {
                bikerRenderer.enabled = true;
            }
            
            Debug.Log($"✓ Biker reset: Position={bikerModel.transform.localPosition}, Scale={bikerModel.transform.localScale}");
        }
        else
        {
            Debug.LogWarning("Biker model not found! Please assign it manually.");
        }

        // Reset wheels model
        if (wheelsModel != null)
        {
            // Check if wheelsModel is a prefab asset
            #if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(wheelsModel))
            {
                Debug.LogWarning("[CompleteSceneReset] Wheels model is a prefab asset. Skipping transform modification.");
            }
            else
            {
            #endif
                // Make wheels child of biker (or bike if no biker)
                if (bikerModel != null)
                {
                    if (wheelsModel.transform.parent != bikerModel.transform)
                    {
                        wheelsModel.transform.SetParent(bikerModel.transform);
                    }
                }
                else
                {
                    if (wheelsModel.transform.parent != bikeObject.transform)
                    {
                        wheelsModel.transform.SetParent(bikeObject.transform);
                    }
                }

                wheelsModel.SetActive(true);
                wheelsModel.transform.localPosition = wheelOffset;
                wheelsModel.transform.localRotation = Quaternion.Euler(wheelRotation);
                wheelsModel.transform.localScale = wheelScale;
            #if UNITY_EDITOR
            }
            #endif
            
            // Enable renderer
            Renderer wheelsRenderer = wheelsModel.GetComponentInChildren<Renderer>();
            if (wheelsRenderer != null)
            {
                wheelsRenderer.enabled = true;
            }
            
            Debug.Log($"✓ Wheels reset: Position={wheelsModel.transform.localPosition}, Scale={wheelsModel.transform.localScale}");
        }
        else
        {
            Debug.LogWarning("Wheels model not found! Please assign it manually.");
        }
    }

    private GameObject FindModelInChildren(string namePattern)
    {
        if (bikeObject == null) return null;

        foreach (Transform child in bikeObject.transform)
        {
            if (child.name.ToLower().Contains(namePattern.ToLower()))
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private GameObject LoadModel(string modelName)
    {
        #if UNITY_EDITOR
        string[] searchPaths = new string[] { "Assets/Models/Bike", "Assets" };
        
        foreach (string searchPath in searchPaths)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{modelName} t:GameObject", new[] { searchPath });
            
            if (guids.Length == 0)
            {
                guids = UnityEditor.AssetDatabase.FindAssets($"{modelName} t:Model", new[] { searchPath });
            }
            
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (model == null)
                {
                    UnityEngine.Object[] allObjects = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var obj in allObjects)
                    {
                        if (obj is GameObject)
                        {
                            model = obj as GameObject;
                            break;
                        }
                    }
                }
                
                if (model != null)
                {
                    GameObject instance = Instantiate(model, bikeObject.transform);
                    instance.name = modelName;
                    instance.SetActive(true);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    
                    return instance;
                }
            }
        }
        #endif
        return null;
    }

    private void LoadTexture(string name, ref TerrainLayer layer)
    {
        #if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{name} t:TerrainLayer", new[] { "Assets/Textures/Terrain" });
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            layer = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        }
        #endif
    }
}
