using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to flatten the terrain heightmap.
/// Use this BEFORE pressing Play to remove old saved terrain data.
/// Menu: Tools > Flatten Terrain
/// </summary>
public class FlattenTerrain
{
    [MenuItem("Tools/Flatten Terrain (Reset Heights)")]
    public static void Flatten()
    {
        Terrain terrain = Object.FindObjectOfType<Terrain>();
        if (terrain == null)
        {
            EditorUtility.DisplayDialog("Flatten Terrain", "No terrain found in scene!", "OK");
            return;
        }
        
        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;
        float[,] heights = new float[res, res];
        
        // Set to a low flat height (5% of terrain height)
        float flatHeight = 0.05f;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                heights[z, x] = flatHeight;
            }
        }
        
        td.SetHeights(0, 0, heights);
        
        // Also clear alphamap to first layer only
        int alphaRes = td.alphamapWidth;
        int layerCount = td.terrainLayers != null ? td.terrainLayers.Length : 0;
        if (layerCount > 0)
        {
            float[,,] alphas = new float[alphaRes, alphaRes, layerCount];
            for (int z = 0; z < alphaRes; z++)
            {
                for (int x = 0; x < alphaRes; x++)
                {
                    alphas[z, x, 0] = 1f;
                    for (int l = 1; l < layerCount; l++)
                        alphas[z, x, l] = 0f;
                }
            }
            td.SetAlphamaps(0, 0, alphas);
        }
        
        Debug.Log($"[FlattenTerrain] Terrain flattened to {flatHeight * td.size.y:F1}m. Alphamap cleared.");
        EditorUtility.DisplayDialog("Flatten Terrain", 
            $"Terrain flattened!\nHeight: {flatHeight * td.size.y:F1}m\nAlphamap: cleared to grass\n\nNow press Play — CurvedRouteGenerator will sculpt the terrain.", "OK");
    }
}
