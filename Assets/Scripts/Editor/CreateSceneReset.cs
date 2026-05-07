using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor script to quickly create a CompleteSceneReset GameObject
/// </summary>
public class CreateSceneReset : EditorWindow
{
    [MenuItem("Tools/Create Complete Scene Reset")]
    public static void CreateSceneResetObject()
    {
        // Check if one already exists
        CompleteSceneReset existing = FindObjectOfType<CompleteSceneReset>();
        if (existing != null)
        {
            if (EditorUtility.DisplayDialog("Scene Reset Exists", 
                "A CompleteSceneReset already exists in the scene. Do you want to select it?", 
                "Select", "Cancel"))
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
            }
            return;
        }

        // Create new GameObject
        GameObject resetObj = new GameObject("SceneReset");
        CompleteSceneReset sceneReset = resetObj.AddComponent<CompleteSceneReset>();

        // Try to find terrain in scene
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain != null)
        {
            SerializedObject so = new SerializedObject(sceneReset);
            so.FindProperty("terrain").objectReferenceValue = terrain;
            so.ApplyModifiedProperties();
            
            Debug.Log("✓ SceneReset created and connected to existing Terrain");
        }
        else
        {
            Debug.LogWarning("No Terrain found in scene. Please assign it manually in the Inspector.");
        }

        // Select the new object
        Selection.activeGameObject = resetObj;
        EditorGUIUtility.PingObject(resetObj);

        Debug.Log("✓ CompleteSceneReset GameObject created!");
        Debug.Log("  This will automatically reset the scene when the game starts.");
        Debug.Log("  You can also right-click the component and select 'Reset Complete Scene' to test it.");
    }

    [MenuItem("Tools/Create Complete Scene Reset", true)]
    public static bool ValidateCreateSceneReset()
    {
        return true;
    }
}
