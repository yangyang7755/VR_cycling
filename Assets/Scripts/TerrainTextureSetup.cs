using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper script to set up terrain textures from imported FreeMountain and GrassFlowers assets
/// Creates TerrainLayer assets from the imported textures
/// </summary>
public class TerrainTextureSetup : MonoBehaviour
{
    [Header("Texture References")]
    [Tooltip("Grass texture from GrassFlowers (e.g., Grass01_BigUV.png)")]
    [SerializeField] private Texture2D grassTexture;
    
    [Tooltip("Road texture (can use FreeMountain diff.psd or create custom)")]
    [SerializeField] private Texture2D roadTexture;
    
    [Tooltip("Normal map for grass (optional)")]
    [SerializeField] private Texture2D grassNormalMap;
    
    [Tooltip("Normal map for road (optional)")]
    [SerializeField] private Texture2D roadNormalMap;

    [Header("Terrain Layer Output")]
    [Tooltip("Created terrain layers will be saved here")]
    [SerializeField] private string terrainLayersPath = "Assets/Textures/Terrain";

    [Header("Texture Settings")]
    [Tooltip("Tile size for grass texture (meters)")]
    [SerializeField] private Vector2 grassTileSize = new Vector2(15f, 15f);
    
    [Tooltip("Tile size for road texture (meters)")]
    [SerializeField] private Vector2 roadTileSize = new Vector2(5f, 5f);

#if UNITY_EDITOR
    [ContextMenu("Create Terrain Layers")]
    public void CreateTerrainLayers()
    {
        if (grassTexture == null)
        {
            Debug.LogError("[TerrainTextureSetup] Grass texture not assigned! Please assign it in Inspector.");
            return;
        }

        if (roadTexture == null)
        {
            Debug.LogError("[TerrainTextureSetup] Road texture not assigned! Please assign it in Inspector.");
            return;
        }

        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder(terrainLayersPath))
        {
            string parentFolder = "Assets/Textures";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Textures");
            }
            AssetDatabase.CreateFolder(parentFolder, "Terrain");
        }

        // Create grass layer
        TerrainLayer grassLayer = CreateTerrainLayer(
            "Grass",
            grassTexture,
            grassNormalMap,
            grassTileSize,
            terrainLayersPath + "/Grass.terrainlayer"
        );

        // Create road layer
        TerrainLayer roadLayer = CreateTerrainLayer(
            "Road",
            roadTexture,
            roadNormalMap,
            roadTileSize,
            terrainLayersPath + "/Road.terrainlayer"
        );

        if (grassLayer != null && roadLayer != null)
        {
            Debug.Log($"[TerrainTextureSetup] ✓ Terrain layers created!");
            Debug.Log($"  Grass layer: {grassLayer.name}");
            Debug.Log($"  Road layer: {roadLayer.name}");
            Debug.Log($"  Location: {terrainLayersPath}");
            Debug.Log($"  Now assign these layers to SimpleTerrainBuilder!");
        }
    }

    private TerrainLayer CreateTerrainLayer(string name, Texture2D diffuse, Texture2D normal, Vector2 tileSize, string assetPath)
    {
        // Use constructor instead of CreateInstance for TerrainLayer
        TerrainLayer layer = new TerrainLayer();
        layer.diffuseTexture = diffuse;
        layer.normalMapTexture = normal;
        layer.tileSize = tileSize;
        layer.tileOffset = Vector2.zero;

        AssetDatabase.CreateAsset(layer, assetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TerrainTextureSetup] Created terrain layer: {name} at {assetPath}");
        return layer;
    }

    [ContextMenu("Auto-Find Grass Texture")]
    public void AutoFindGrassTexture()
    {
        string[] guids = AssetDatabase.FindAssets("Grass01_BigUV t:Texture2D", new[] { "Assets/Textures" });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (grassTexture != null)
            {
                Debug.Log($"[TerrainTextureSetup] ✓ Found grass texture: {path}");
            }
        }
        else
        {
            Debug.LogWarning("[TerrainTextureSetup] Could not find Grass01_BigUV.png. Please assign manually.");
        }
    }

    [ContextMenu("Auto-Find Road Texture")]
    public void AutoFindRoadTexture()
    {
        // Try to find FreeMountain diff texture
        string[] guids = AssetDatabase.FindAssets("diff t:Texture2D", new[] { "Assets/Textures" });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            roadTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (roadTexture != null)
            {
                Debug.Log($"[TerrainTextureSetup] ✓ Found road texture: {path}");
            }
        }
        else
        {
            Debug.LogWarning("[TerrainTextureSetup] Could not find road texture. Please assign manually or create a custom road texture.");
        }
    }

    [ContextMenu("Auto-Setup All")]
    public void AutoSetupAll()
    {
        AutoFindGrassTexture();
        AutoFindRoadTexture();
        if (grassTexture != null && roadTexture != null)
        {
            CreateTerrainLayers();
        }
        else
        {
            Debug.LogWarning("[TerrainTextureSetup] Could not auto-find all textures. Please assign manually and run Create Terrain Layers.");
        }
    }
#endif

    /// <summary>
    /// Get grass terrain layer (for use by SimpleTerrainBuilder)
    /// </summary>
    public TerrainLayer GetGrassLayer()
    {
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("Grass t:TerrainLayer", new[] { terrainLayersPath });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        }
#endif
        return null;
    }

    /// <summary>
    /// Get road terrain layer (for use by SimpleTerrainBuilder)
    /// </summary>
    public TerrainLayer GetRoadLayer()
    {
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("Road t:TerrainLayer", new[] { terrainLayersPath });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        }
#endif
        return null;
    }
}
