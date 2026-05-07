using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// Custom editor for InclineIndicator to show all references and connections
/// Helps debug and visualize how the InclineIndicator is coupled to other objects
/// 
/// Usage: Select a GameObject with InclineIndicator component, Inspector will show:
/// - All component references (BikeController, TerrainBuilder, Terrain)
/// - All UI element references with quick "Select" buttons
/// - GameObject hierarchy path
/// </summary>
[CustomEditor(typeof(InclineIndicator))]
public class InclineIndicatorDebugger : Editor
{
    private bool showReferences = true;
    private bool showUIReferences = true;
    private SerializedProperty bikeControllerProp;
    private SerializedProperty terrainBuilderProp;
    private SerializedProperty terrainProp;

    private void OnEnable()
    {
        // Get serialized properties for main references
        bikeControllerProp = serializedObject.FindProperty("bikeController");
        terrainBuilderProp = serializedObject.FindProperty("terrainBuilder");
        terrainProp = serializedObject.FindProperty("terrain");
    }

    public override void OnInspectorGUI()
    {
        InclineIndicator indicator = (InclineIndicator)target;
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("=== InclineIndicator Reference Debugger ===", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This debugger shows all objects that InclineIndicator is connected to. " +
            "Use 'Select' buttons to quickly navigate to referenced objects.", MessageType.Info);
        EditorGUILayout.Space();

        // Show main references
        showReferences = EditorGUILayout.Foldout(showReferences, "Main Component References", true);
        if (showReferences)
        {
            EditorGUI.indentLevel++;

            // Bike Controller
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Bike Controller:", GUILayout.Width(150));
            if (bikeControllerProp != null && bikeControllerProp.objectReferenceValue != null)
            {
                EditorGUILayout.ObjectField(bikeControllerProp.objectReferenceValue, typeof(BikeController), true, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = (bikeControllerProp.objectReferenceValue as BikeController).gameObject;
                    EditorGUIUtility.PingObject(bikeControllerProp.objectReferenceValue);
                }
            }
            else
            {
                EditorGUILayout.LabelField("(Not Assigned - Auto-finds in Start())", EditorStyles.helpBox, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Find", GUILayout.Width(60)))
                {
                    BikeController found = FindObjectOfType<BikeController>();
                    if (found != null)
                    {
                        bikeControllerProp.objectReferenceValue = found;
                        Selection.activeGameObject = found.gameObject;
                        EditorGUIUtility.PingObject(found);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Not Found", "BikeController not found in scene.", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // Terrain Builder
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Terrain Builder:", GUILayout.Width(150));
            if (terrainBuilderProp != null && terrainBuilderProp.objectReferenceValue != null)
            {
                EditorGUILayout.ObjectField(terrainBuilderProp.objectReferenceValue, typeof(SimpleTerrainBuilder), true, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = (terrainBuilderProp.objectReferenceValue as SimpleTerrainBuilder).gameObject;
                    EditorGUIUtility.PingObject(terrainBuilderProp.objectReferenceValue);
                }
            }
            else
            {
                EditorGUILayout.LabelField("(Not Assigned - Auto-finds in Start())", EditorStyles.helpBox, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Find", GUILayout.Width(60)))
                {
                    SimpleTerrainBuilder found = FindObjectOfType<SimpleTerrainBuilder>();
                    if (found != null)
                    {
                        terrainBuilderProp.objectReferenceValue = found;
                        Selection.activeGameObject = found.gameObject;
                        EditorGUIUtility.PingObject(found);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Not Found", "SimpleTerrainBuilder not found in scene.", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // Terrain
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Terrain:", GUILayout.Width(150));
            if (terrainProp != null && terrainProp.objectReferenceValue != null)
            {
                EditorGUILayout.ObjectField(terrainProp.objectReferenceValue, typeof(Terrain), true, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = (terrainProp.objectReferenceValue as Terrain).gameObject;
                    EditorGUIUtility.PingObject(terrainProp.objectReferenceValue);
                }
            }
            else
            {
                EditorGUILayout.LabelField("(Not Assigned - Auto-finds in Start())", EditorStyles.helpBox, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Find", GUILayout.Width(60)))
                {
                    Terrain found = FindObjectOfType<Terrain>();
                    if (found != null)
                    {
                        terrainProp.objectReferenceValue = found;
                        Selection.activeGameObject = found.gameObject;
                        EditorGUIUtility.PingObject(found);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Not Found", "Terrain not found in scene.", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // Show UI References
        showUIReferences = EditorGUILayout.Foldout(showUIReferences, "UI Element References", true);
        if (showUIReferences)
        {
            EditorGUI.indentLevel++;

            // Get UI reference properties
            SerializedProperty currentInclineContainerProp = serializedObject.FindProperty("currentInclineContainer");
            SerializedProperty currentInclineCircleProp = serializedObject.FindProperty("currentInclineCircle");
            SerializedProperty currentInclineValueTextProp = serializedObject.FindProperty("currentInclineValueText");
            SerializedProperty currentInclinePercentTextProp = serializedObject.FindProperty("currentInclinePercentText");
            SerializedProperty continuousBarContainerProp = serializedObject.FindProperty("continuousInclineBarContainer");
            SerializedProperty continuousBarBackgroundProp = serializedObject.FindProperty("continuousInclineBarBackground");
            SerializedProperty inclineSegmentsParentProp = serializedObject.FindProperty("inclineSegmentsParent");
            SerializedProperty currentPositionIndicatorProp = serializedObject.FindProperty("currentPositionIndicator");
            SerializedProperty previewBarContainerProp = serializedObject.FindProperty("previewBarContainer");
            SerializedProperty previewBarParentProp = serializedObject.FindProperty("previewBarParent");
            SerializedProperty positionMarkerProp = serializedObject.FindProperty("positionMarker");

            // Current Incline UI
            EditorGUILayout.LabelField("Current Incline UI:", EditorStyles.boldLabel);
            ShowUIReferenceProp("Current Incline Container", currentInclineContainerProp);
            ShowUIReferenceProp("Current Incline Circle", currentInclineCircleProp);
            ShowUIReferenceProp("Current Incline Value Text", currentInclineValueTextProp);
            ShowUIReferenceProp("Current Incline Percent Text", currentInclinePercentTextProp);

            EditorGUILayout.Space();

            // Continuous Incline Bar UI
            EditorGUILayout.LabelField("Continuous Incline Bar UI:", EditorStyles.boldLabel);
            ShowUIReferenceProp("Continuous Bar Container", continuousBarContainerProp);
            ShowUIReferenceProp("Continuous Bar Background", continuousBarBackgroundProp);
            ShowUIReferenceProp("Incline Segments Parent", inclineSegmentsParentProp);
            ShowUIReferenceProp("Current Position Indicator", currentPositionIndicatorProp);

            EditorGUILayout.Space();

            // Preview Bar UI
            EditorGUILayout.LabelField("Preview Bar UI:", EditorStyles.boldLabel);
            ShowUIReferenceProp("Preview Bar Container", previewBarContainerProp);
            ShowUIReferenceProp("Preview Bar Parent", previewBarParentProp);
            ShowUIReferenceProp("Position Marker", positionMarkerProp);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // Show GameObject hierarchy path
        EditorGUILayout.LabelField("=== GameObject Hierarchy ===", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        string path = GetGameObjectPath(indicator.gameObject);
        EditorGUILayout.LabelField("Path:", path, EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Show in Hierarchy"))
        {
            Selection.activeGameObject = indicator.gameObject;
            EditorGUIUtility.PingObject(indicator.gameObject);
        }
        if (GUILayout.Button("Find Canvas"))
        {
            Canvas canvas = indicator.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Selection.activeGameObject = canvas.gameObject;
                EditorGUIUtility.PingObject(canvas.gameObject);
            }
            else
            {
                EditorUtility.DisplayDialog("Canvas Not Found", 
                    "No Canvas found in parent hierarchy. InclineIndicator should be a child of a Canvas.", "OK");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        serializedObject.ApplyModifiedProperties();

        // Draw default inspector below debugger
        EditorGUILayout.LabelField("=== Default Inspector ===", EditorStyles.boldLabel);
        DrawDefaultInspector();
    }

    private void ShowUIReferenceProp(string label, SerializedProperty prop)
    {
        if (prop == null) return;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label + ":", GUILayout.Width(200));
        EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.ExpandWidth(true));
        if (prop.objectReferenceValue != null)
        {
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                if (prop.objectReferenceValue is Component component)
                {
                    Selection.activeGameObject = component.gameObject;
                    EditorGUIUtility.PingObject(component.gameObject);
                }
                else if (prop.objectReferenceValue is GameObject go)
                {
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                }
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
