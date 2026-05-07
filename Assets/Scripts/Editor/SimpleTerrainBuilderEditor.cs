using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SimpleTerrainBuilder))]
public class SimpleTerrainBuilderEditor : Editor
{
    private SerializedProperty grassLayerProp;
    private SerializedProperty roadLayerProp;
    private SerializedProperty mountainLayerProp;

    private void OnEnable()
    {
        // Cache serialized properties
        RefreshSerializedProperties();
    }

    private void RefreshSerializedProperties()
    {
        if (serializedObject != null && serializedObject.targetObject != null)
        {
            serializedObject.Update();
            grassLayerProp = serializedObject.FindProperty("grassLayer");
            roadLayerProp = serializedObject.FindProperty("roadLayer");
            mountainLayerProp = serializedObject.FindProperty("mountainLayer");
        }
    }

    public override void OnInspectorGUI()
    {
        // Check if serialized object is valid
        if (serializedObject == null || serializedObject.targetObject == null)
        {
            EditorGUILayout.HelpBox("Serialized object is null or target has been destroyed.", MessageType.Error);
            return;
        }

        // Refresh properties if they are null (object may have been recreated)
        if (grassLayerProp == null || roadLayerProp == null || mountainLayerProp == null)
        {
            RefreshSerializedProperties();
        }

        // Check again after refresh
        if (grassLayerProp == null || roadLayerProp == null || mountainLayerProp == null)
        {
            EditorGUILayout.HelpBox("Failed to find serialized properties. Please reselect the object.", MessageType.Error);
            return;
        }

        // Update serialized object
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

        // Auto-assign terrain layers button
        if (GUILayout.Button("Auto-Assign Terrain Layers", GUILayout.Height(30)))
        {
            AutoAssignTerrainLayers();
        }

        // Show status
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        
        // Safely access properties
        try
        {
            GUI.enabled = false;
            EditorGUILayout.PropertyField(grassLayerProp, new GUIContent("Grass Layer"));
            EditorGUILayout.PropertyField(roadLayerProp, new GUIContent("Road Layer"));
            EditorGUILayout.PropertyField(mountainLayerProp, new GUIContent("Mountain Layer"));
            GUI.enabled = true;

            TerrainLayer grassLayer = grassLayerProp != null ? grassLayerProp.objectReferenceValue as TerrainLayer : null;
            TerrainLayer roadLayer = roadLayerProp != null ? roadLayerProp.objectReferenceValue as TerrainLayer : null;
            TerrainLayer mountainLayer = mountainLayerProp != null ? mountainLayerProp.objectReferenceValue as TerrainLayer : null;

            if (grassLayer == null || roadLayer == null || mountainLayer == null)
            {
                EditorGUILayout.HelpBox("Some terrain layers are not assigned. Click 'Auto-Assign Terrain Layers' to automatically find and assign them.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("✓ All terrain layers are assigned correctly.", MessageType.Info);
            }
        }
        catch (System.Exception e)
        {
            EditorGUILayout.HelpBox($"Error accessing properties: {e.Message}", MessageType.Error);
            // Refresh properties on next frame
            RefreshSerializedProperties();
        }

        // Apply changes
        if (serializedObject != null && serializedObject.targetObject != null)
        {
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void AutoAssignTerrainLayers()
    {
        // Refresh properties before use
        RefreshSerializedProperties();
        
        if (serializedObject == null || serializedObject.targetObject == null)
        {
            Debug.LogError("[SimpleTerrainBuilderEditor] Cannot auto-assign: SerializedObject is null!");
            return;
        }

        if (grassLayerProp == null || roadLayerProp == null || mountainLayerProp == null)
        {
            Debug.LogError("[SimpleTerrainBuilderEditor] Cannot auto-assign: SerializedProperties are null!");
            return;
        }

        serializedObject.Update();
        bool assigned = false;

        // Try to load Grass layer
        if (grassLayerProp.objectReferenceValue == null)
        {
            string grassPath = "Assets/Textures/Terrain/Grass.terrainlayer";
            TerrainLayer grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(grassPath);
            if (grassLayer != null)
            {
                grassLayerProp.objectReferenceValue = grassLayer;
                assigned = true;
                Debug.Log($"✓ Auto-assigned Grass layer: {grassLayer.name}");
            }
            else
            {
                // Try searching
                string[] guids = AssetDatabase.FindAssets("Grass t:TerrainLayer");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                    if (grassLayer != null)
                    {
                        grassLayerProp.objectReferenceValue = grassLayer;
                        assigned = true;
                        Debug.Log($"✓ Auto-assigned Grass layer: {grassLayer.name} from {path}");
                    }
                }
            }
        }

        // Try to load Road layer
        if (roadLayerProp.objectReferenceValue == null)
        {
            string roadPath = "Assets/Textures/Terrain/Road.terrainlayer";
            TerrainLayer roadLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(roadPath);
            if (roadLayer != null)
            {
                roadLayerProp.objectReferenceValue = roadLayer;
                assigned = true;
                Debug.Log($"✓ Auto-assigned Road layer: {roadLayer.name}");
            }
            else
            {
                // Try searching
                string[] guids = AssetDatabase.FindAssets("Road t:TerrainLayer");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    roadLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                    if (roadLayer != null)
                    {
                        roadLayerProp.objectReferenceValue = roadLayer;
                        assigned = true;
                        Debug.Log($"✓ Auto-assigned Road layer: {roadLayer.name} from {path}");
                    }
                }
            }
        }

        // Try to load Mountain layer
        if (mountainLayerProp.objectReferenceValue == null)
        {
            string mountainPath = "Assets/Textures/Terrain/Mountain.terrainlayer";
            TerrainLayer mountainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(mountainPath);
            if (mountainLayer != null)
            {
                mountainLayerProp.objectReferenceValue = mountainLayer;
                assigned = true;
                Debug.Log($"✓ Auto-assigned Mountain layer: {mountainLayer.name}");
            }
            else
            {
                // Try searching
                string[] guids = AssetDatabase.FindAssets("Mountain t:TerrainLayer");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    mountainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                    if (mountainLayer != null)
                    {
                        mountainLayerProp.objectReferenceValue = mountainLayer;
                        assigned = true;
                        Debug.Log($"✓ Auto-assigned Mountain layer: {mountainLayer.name} from {path}");
                    }
                }
            }
        }

        if (assigned)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            Debug.Log("✓ Terrain layers auto-assigned successfully!");
        }
        else
        {
            Debug.LogWarning("Could not auto-assign terrain layers. Please assign them manually in the Inspector.");
        }
    }
}
