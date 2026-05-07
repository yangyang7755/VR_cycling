using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor script to reimport all terrain assets and automatically load them into the scene
/// </summary>
public class ReimportAndLoadAssets : EditorWindow
{
    [MenuItem("Tools/Reimport and Load Terrain Assets")]
    public static void ShowWindow()
    {
        GetWindow<ReimportAndLoadAssets>("Reimport & Load Assets");
    }

    private void OnGUI()
    {
        GUILayout.Label("Terrain Asset Reimport & Loader", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("This tool will:\n1. Reimport all terrain textures and layers\n2. Auto-assign them to SimpleTerrainBuilder in the scene", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("Reimport All Terrain Assets", GUILayout.Height(30)))
        {
            ReimportAllTerrainAssets();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Load Assets to Scene", GUILayout.Height(30)))
        {
            LoadAssetsToScene();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Do Both (Reimport + Load)", GUILayout.Height(40)))
        {
            ReimportAllTerrainAssets();
            EditorUtility.DisplayProgressBar("Loading Assets", "Waiting for reimport to complete...", 0.5f);
            System.Threading.Thread.Sleep(500); // Give Unity time to process reimports
            EditorUtility.ClearProgressBar();
            LoadAssetsToScene();
        }
    }

    /// <summary>
    /// Reimport all terrain textures and layers
    /// </summary>
    private static void ReimportAllTerrainAssets()
    {
        Debug.Log("[ReimportAndLoadAssets] Starting asset reimport...");

        string terrainPath = "Assets/Textures/Terrain";
        
        // Find all textures in Terrain folder
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { terrainPath });
        int textureCount = 0;
        
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            textureCount++;
            Debug.Log($"  Reimported texture: {path}");
        }

        // Find all TerrainLayer assets
        string[] layerGuids = AssetDatabase.FindAssets("t:TerrainLayer", new[] { terrainPath });
        int layerCount = 0;
        
        foreach (string guid in layerGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            layerCount++;
            Debug.Log($"  Reimported TerrainLayer: {path}");
        }

        // Also check for textures that might need TerrainLayer creation
        CheckAndCreateMissingTerrainLayers(terrainPath);

        AssetDatabase.Refresh();
        Debug.Log($"[ReimportAndLoadAssets] ✓ Reimport complete! Textures: {textureCount}, Layers: {layerCount}");
    }

    /// <summary>
    /// Check for texture files that don't have corresponding TerrainLayers and create them
    /// </summary>
    private static void CheckAndCreateMissingTerrainLayers(string terrainPath)
    {
        // Find all texture files
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { terrainPath });
        
        foreach (string guid in textureGuids)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(guid);
            string textureName = System.IO.Path.GetFileNameWithoutExtension(texturePath);
            
            // Skip if it's a normal map (ends with _ or _n)
            if (textureName.EndsWith("_") || textureName.EndsWith("_n") || textureName.EndsWith("_normal"))
            {
                continue;
            }

            // Check if TerrainLayer already exists
            string layerPath = terrainPath + "/" + textureName + ".terrainlayer";
            TerrainLayer existingLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            
            if (existingLayer == null)
            {
                // Create new TerrainLayer
                TerrainLayer newLayer = new TerrainLayer();
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                
                if (texture != null)
                {
                    newLayer.diffuseTexture = texture;
                    
                    // Try to find corresponding normal map
                    string normalMapPath = terrainPath + "/" + textureName + "_.jpg";
                    if (!System.IO.File.Exists(Application.dataPath.Replace("Assets", "") + normalMapPath))
                    {
                        normalMapPath = terrainPath + "/" + textureName + "_n.jpg";
                    }
                    if (!System.IO.File.Exists(Application.dataPath.Replace("Assets", "") + normalMapPath))
                    {
                        normalMapPath = terrainPath + "/" + textureName + "_normal.jpg";
                    }
                    
                    if (System.IO.File.Exists(Application.dataPath.Replace("Assets", "") + normalMapPath))
                    {
                        Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);
                        if (normalMap != null)
                        {
                            newLayer.normalMapTexture = normalMap;
                            Debug.Log($"  Found normal map for {textureName}: {normalMapPath}");
                        }
                    }

                    // Set default tile size
                    newLayer.tileSize = new Vector2(15, 15);
                    
                    // Save the TerrainLayer
                    AssetDatabase.CreateAsset(newLayer, layerPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"  ✓ Created TerrainLayer: {layerPath}");
                }
            }
        }
    }

    /// <summary>
    /// Load all terrain assets to SimpleTerrainBuilder components in the scene
    /// </summary>
    private static void LoadAssetsToScene()
    {
        Debug.Log("[ReimportAndLoadAssets] Loading assets to scene...");

        // Find all SimpleTerrainBuilder components in the scene
        SimpleTerrainBuilder[] builders = FindObjectsOfType<SimpleTerrainBuilder>();
        
        if (builders.Length == 0)
        {
            Debug.LogWarning("[ReimportAndLoadAssets] No SimpleTerrainBuilder components found in scene!");
            EditorUtility.DisplayDialog("No Components Found", 
                "No SimpleTerrainBuilder components found in the scene.\n\nPlease add the SimpleTerrainBuilder script to a GameObject first.", 
                "OK");
            return;
        }

        string terrainPath = "Assets/Textures/Terrain";
        int assignedCount = 0;

        foreach (SimpleTerrainBuilder builder in builders)
        {
            bool builderModified = false;

            // Load Grass layer
            TerrainLayer grassLayer = LoadTerrainLayer(terrainPath, "Grass");
            if (grassLayer != null)
            {
                SerializedObject so = new SerializedObject(builder);
                SerializedProperty grassProp = so.FindProperty("grassLayer");
                if (grassProp != null)
                {
                    grassProp.objectReferenceValue = grassLayer;
                    so.ApplyModifiedProperties();
                    builderModified = true;
                    Debug.Log($"  ✓ Assigned Grass layer to {builder.name}");
                }
            }

            // Load Road layer (try both "Road" and "road")
            TerrainLayer roadLayer = LoadTerrainLayer(terrainPath, "Road");
            if (roadLayer == null)
            {
                roadLayer = LoadTerrainLayer(terrainPath, "road");
            }
            if (roadLayer != null)
            {
                SerializedObject so = new SerializedObject(builder);
                SerializedProperty roadProp = so.FindProperty("roadLayer");
                if (roadProp != null)
                {
                    roadProp.objectReferenceValue = roadLayer;
                    so.ApplyModifiedProperties();
                    builderModified = true;
                    Debug.Log($"  ✓ Assigned Road layer to {builder.name}");
                }
            }

            // Load Mountain layer
            TerrainLayer mountainLayer = LoadTerrainLayer(terrainPath, "Mountain");
            if (mountainLayer != null)
            {
                SerializedObject so = new SerializedObject(builder);
                SerializedProperty mountainProp = so.FindProperty("mountainLayer");
                if (mountainProp != null)
                {
                    mountainProp.objectReferenceValue = mountainLayer;
                    so.ApplyModifiedProperties();
                    builderModified = true;
                    Debug.Log($"  ✓ Assigned Mountain layer to {builder.name}");
                }
            }

            if (builderModified)
            {
                EditorUtility.SetDirty(builder);
                assignedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ReimportAndLoadAssets] ✓ Load complete! Updated {assignedCount} SimpleTerrainBuilder component(s)");
        
        EditorUtility.DisplayDialog("Load Complete", 
            $"Successfully loaded terrain assets to {assignedCount} SimpleTerrainBuilder component(s) in the scene.", 
            "OK");
    }

    /// <summary>
    /// Load a TerrainLayer by name from the terrain path
    /// </summary>
    private static TerrainLayer LoadTerrainLayer(string terrainPath, string layerName)
    {
        // Try exact name first
        string layerPath = terrainPath + "/" + layerName + ".terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        
        if (layer != null)
        {
            return layer;
        }

        // Try searching
        string[] guids = AssetDatabase.FindAssets($"{layerName} t:TerrainLayer", new[] { terrainPath });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer != null)
            {
                Debug.Log($"  Found {layerName} layer at: {path}");
                return layer;
            }
        }

        Debug.LogWarning($"  Could not find TerrainLayer: {layerName}");
        return null;
    }
}
