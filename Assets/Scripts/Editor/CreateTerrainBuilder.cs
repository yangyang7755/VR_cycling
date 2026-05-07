using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor script to quickly create a SimpleTerrainBuilder GameObject
/// </summary>
public class CreateTerrainBuilder : EditorWindow
{
    [MenuItem("Tools/Create Terrain Builder")]
    public static void CreateTerrainBuilderObject()
    {
        // Check if one already exists
        SimpleTerrainBuilder existing = FindObjectOfType<SimpleTerrainBuilder>();
        if (existing != null)
        {
            if (EditorUtility.DisplayDialog("Terrain Builder Exists", 
                "A SimpleTerrainBuilder already exists in the scene. Do you want to select it?", 
                "Select", "Cancel"))
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
            }
            return;
        }

        // Create new GameObject
        GameObject terrainBuilderObj = new GameObject("TerrainBuilder");
        SimpleTerrainBuilder terrainBuilder = terrainBuilderObj.AddComponent<SimpleTerrainBuilder>();

        // Try to find terrain in scene
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain != null)
        {
            // Use SerializedObject to set the terrain reference
            SerializedObject so = new SerializedObject(terrainBuilder);
            so.FindProperty("terrain").objectReferenceValue = terrain;
            so.ApplyModifiedProperties();
            
            Debug.Log("✓ TerrainBuilder created and connected to existing Terrain");
        }
        else
        {
            Debug.LogWarning("No Terrain found in scene. Please assign it manually in the Inspector.");
        }

        // Select the new object
        Selection.activeGameObject = terrainBuilderObj;
        EditorGUIUtility.PingObject(terrainBuilderObj);

        Debug.Log("✓ TerrainBuilder GameObject created! Check the Inspector to configure settings.");
    }

    [MenuItem("Tools/Create Terrain Builder", true)]
    public static bool ValidateCreateTerrainBuilder()
    {
        return true;
    }
}
