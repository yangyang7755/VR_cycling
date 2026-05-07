using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Fixes scene visual issues: terrain textures, skybox, and lighting
/// </summary>
public class SceneVisualFixer : MonoBehaviour
{
    [Header("Auto Fix on Start")]
    [Tooltip("Automatically fix scene visuals when game starts")]
    [SerializeField] private bool autoFixOnStart = true;

    private void Start()
    {
        if (autoFixOnStart)
        {
            FixSceneVisuals();
        }
    }

    [ContextMenu("Fix Scene Visuals")]
    public void FixSceneVisuals()
    {
        Debug.Log("[SceneVisualFixer] Starting scene visual fix...");

        // 1. Fix terrain textures
        FixTerrainTextures();

        // 2. Fix skybox
        FixSkybox();

        // 3. Fix lighting
        FixLighting();

        Debug.Log("[SceneVisualFixer] ✓ Scene visual fix completed!");
    }

    private void FixTerrainTextures()
    {
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("[SceneVisualFixer] No terrain found!");
            return;
        }

        TerrainData data = terrain.terrainData;
        
        // Check if terrain has layers
        if (data.terrainLayers == null || data.terrainLayers.Length == 0)
        {
            Debug.LogWarning("[SceneVisualFixer] Terrain has no layers! Please run SimpleTerrainBuilder.SetupTextures() first.");
            return;
        }

        // Check terrain material
        if (terrain.materialTemplate == null)
        {
            Debug.Log("[SceneVisualFixer] Terrain material is null - using default terrain shader.");
            // This is actually OK - Unity uses default terrain shader
        }

        // Force terrain to refresh
        terrain.Flush();
        
        Debug.Log($"[SceneVisualFixer] ✓ Terrain checked. Layers: {data.terrainLayers.Length}");
        foreach (var layer in data.terrainLayers)
        {
            if (layer != null)
            {
                Debug.Log($"  - Layer: {layer.name}, Diffuse: {(layer.diffuseTexture != null ? layer.diffuseTexture.name : "NULL")}");
            }
        }
    }

    private void FixSkybox()
    {
        #if UNITY_EDITOR
        // Try to find skybox material
        string[] guids = AssetDatabase.FindAssets("Free_skybox t:Material");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (skybox != null)
            {
                RenderSettings.skybox = skybox;
                Debug.Log($"[SceneVisualFixer] ✓ Skybox applied: {skybox.name}");
                
                // Force update
                if (!Application.isPlaying)
                {
                    SceneView.RepaintAll();
                }
                return;
            }
        }
        #endif

        // Check if skybox is already set
        if (RenderSettings.skybox != null)
        {
            Debug.Log($"[SceneVisualFixer] Skybox already set: {RenderSettings.skybox.name}");
        }
        else
        {
            Debug.LogWarning("[SceneVisualFixer] No skybox found! Please assign one manually.");
        }
    }

    private void FixLighting()
    {
        // Check main directional light
        Light mainLight = FindObjectOfType<Light>();
        if (mainLight != null)
        {
            if (mainLight.type == LightType.Directional)
            {
                // Ensure light is bright enough
                if (mainLight.intensity < 1.0f)
                {
                    mainLight.intensity = 1.0f;
                    Debug.Log("[SceneVisualFixer] ✓ Increased directional light intensity to 1.0");
                }

                // Ensure light color is not too dark
                if (mainLight.color.r < 0.8f || mainLight.color.g < 0.8f || mainLight.color.b < 0.8f)
                {
                    mainLight.color = Color.white;
                    Debug.Log("[SceneVisualFixer] ✓ Set light color to white");
                }
            }
        }

        // Check ambient light
        if (RenderSettings.ambientIntensity < 0.5f)
        {
            RenderSettings.ambientIntensity = 0.8f;
            Debug.Log("[SceneVisualFixer] ✓ Increased ambient intensity to 0.8");
        }

        Debug.Log("[SceneVisualFixer] ✓ Lighting checked and fixed");
    }

    [ContextMenu("Check Terrain Status")]
    public void CheckTerrainStatus()
    {
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("[SceneVisualFixer] No terrain found!");
            return;
        }

        TerrainData data = terrain.terrainData;
        
        Debug.Log("=== Terrain Status ===");
        Debug.Log($"Terrain Size: {data.size}");
        Debug.Log($"Heightmap Resolution: {data.heightmapResolution}");
        Debug.Log($"Alphamap Resolution: {data.alphamapWidth} x {data.alphamapHeight}");
        Debug.Log($"Terrain Layers: {data.terrainLayers?.Length ?? 0}");
        
        if (data.terrainLayers != null)
        {
            for (int i = 0; i < data.terrainLayers.Length; i++)
            {
                var layer = data.terrainLayers[i];
                if (layer != null)
                {
                    Debug.Log($"  Layer {i}: {layer.name}");
                    Debug.Log($"    Diffuse: {(layer.diffuseTexture != null ? layer.diffuseTexture.name : "NULL")}");
                    Debug.Log($"    Normal: {(layer.normalMapTexture != null ? layer.normalMapTexture.name : "NULL")}");
                }
                else
                {
                    Debug.LogWarning($"  Layer {i}: NULL!");
                }
            }
        }

        // Check alphamap
        float[,,] alphamap = data.GetAlphamaps(0, 0, Mathf.Min(10, data.alphamapWidth), Mathf.Min(10, data.alphamapHeight));
        Debug.Log($"Sample alphamap values (first 10x10):");
        for (int x = 0; x < Mathf.Min(10, data.alphamapWidth); x++)
        {
            for (int z = 0; z < Mathf.Min(10, data.alphamapHeight); z++)
            {
                string values = "";
                for (int layer = 0; layer < alphamap.GetLength(2); layer++)
                {
                    values += $"{alphamap[x, z, layer]:F2} ";
                }
                Debug.Log($"  [{x},{z}]: {values}");
            }
        }
    }
}
