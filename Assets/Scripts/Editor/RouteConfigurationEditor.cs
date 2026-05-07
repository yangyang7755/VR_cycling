using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Custom editor window for creating and editing route configurations
/// Provides visual interface for adding climbs, checkpoints, and route settings
/// </summary>
public class RouteConfigurationEditor : EditorWindow
{
    private RouteConfiguration routeConfig;
    private Vector2 scrollPosition;
    private int selectedClimbIndex = -1;

    [MenuItem("Tools/Cycling/Route Configuration Editor")]
    public static void ShowWindow()
    {
        GetWindow<RouteConfigurationEditor>("Route Configuration Editor");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Route Configuration Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Load/Create route config
        routeConfig = (RouteConfiguration)EditorGUILayout.ObjectField("Route Configuration", routeConfig, typeof(RouteConfiguration), false);

        if (routeConfig == null)
        {
            EditorGUILayout.HelpBox("No route configuration selected. Create or load one to begin.", MessageType.Info);
            
            if (GUILayout.Button("Create New Route Configuration"))
            {
                CreateNewRouteConfig();
            }
            
            EditorGUILayout.EndScrollView();
            return;
        }

        // Route settings
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Route Settings", EditorStyles.boldLabel);
        
        routeConfig.routeName = EditorGUILayout.TextField("Route Name", routeConfig.routeName);
        routeConfig.totalRouteLength = EditorGUILayout.Slider("Total Length (m)", routeConfig.totalRouteLength, 5000f, 50000f);
        EditorGUILayout.LabelField("Length (km)", (routeConfig.totalRouteLength / 1000f).ToString("F2"));

        // Base terrain settings
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Base Rolling Terrain", EditorStyles.boldLabel);
        
        routeConfig.baseTerrainGradient = EditorGUILayout.Slider("Base Gradient (%)", routeConfig.baseTerrainGradient, -2f, 2f);
        routeConfig.rollingAmplitude = EditorGUILayout.Slider("Rolling Amplitude", routeConfig.rollingAmplitude, 0f, 5f);
        routeConfig.rollingFrequency = EditorGUILayout.Slider("Rolling Frequency", routeConfig.rollingFrequency, 0f, 0.1f);
        routeConfig.baseVisualGradient = EditorGUILayout.Slider("Base Visual Gradient", routeConfig.baseVisualGradient, 0f, 3f);

        // Climb segments
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Climb Segments", EditorStyles.boldLabel);
        
        if (routeConfig.climbSegments == null)
        {
            routeConfig.climbSegments = new List<ClimbSegment>();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Climb Segment"))
        {
            AddNewClimbSegment();
        }
        
        if (GUILayout.Button("Validate Route"))
        {
            ValidateRoute();
        }
        EditorGUILayout.EndHorizontal();

        // List climbs
        EditorGUILayout.Space();
        for (int i = 0; i < routeConfig.climbSegments.Count; i++)
        {
            DrawClimbSegment(i, routeConfig.climbSegments[i]);
            EditorGUILayout.Space();
        }

        // Checkpoint settings
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Checkpoint Settings", EditorStyles.boldLabel);
        
        routeConfig.checkpointAtStart = EditorGUILayout.Toggle("Checkpoint at Start", routeConfig.checkpointAtStart);
        routeConfig.checkpointAtEnd = EditorGUILayout.Toggle("Checkpoint at End", routeConfig.checkpointAtEnd);
        routeConfig.regularCheckpointInterval = EditorGUILayout.FloatField("Regular Interval (m, 0=disable)", routeConfig.regularCheckpointInterval);

        // Summary
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Route Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Total Climbs: {routeConfig.climbSegments.Count}");
        
        List<CheckpointData> allCheckpoints = routeConfig.GetAllCheckpoints();
        EditorGUILayout.LabelField($"Total Checkpoints: {allCheckpoints.Count}");

        EditorGUILayout.EndScrollView();

        // Save changes
        if (GUI.changed && routeConfig != null)
        {
            EditorUtility.SetDirty(routeConfig);
        }
    }

    private void CreateNewRouteConfig()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Route Configuration", "NewRoute", "asset", "Create route configuration");
        if (!string.IsNullOrEmpty(path))
        {
            routeConfig = ScriptableObject.CreateInstance<RouteConfiguration>();
            AssetDatabase.CreateAsset(routeConfig, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = routeConfig;
        }
    }

    private void AddNewClimbSegment()
    {
        if (routeConfig.climbSegments == null)
        {
            routeConfig.climbSegments = new List<ClimbSegment>();
        }

        ClimbSegment newClimb = new ClimbSegment
        {
            climbName = $"Climb {routeConfig.climbSegments.Count + 1}",
            difficulty = DifficultyLevel.Moderate,
            startLocation = routeConfig.climbSegments.Count > 0 ? 
                routeConfig.climbSegments.Last().EndLocation + 200f : 1000f,
            length = 500f,
            averageGrade = 5f,
            visualGradientMultiplier = 1.0f,
            undulationFrequency = 0.2f,
            undulationAmplitude = 0.5f,
            startTransition = 50f,
            endTransition = 50f,
            checkpointAtStart = true,
            checkpointAtEnd = true,
            checkpointMidClimb = false,
            midClimbPosition = 0.5f
        };

        routeConfig.climbSegments.Add(newClimb);
        EditorUtility.SetDirty(routeConfig);
    }

    private void DrawClimbSegment(int index, ClimbSegment climb)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        bool isExpanded = EditorGUILayout.Foldout(selectedClimbIndex == index, $"Climb {index + 1}: {climb.climbName}", true);
        if (isExpanded) selectedClimbIndex = index;
        else if (selectedClimbIndex == index) selectedClimbIndex = -1;

        if (GUILayout.Button("Remove", GUILayout.Width(60)))
        {
            routeConfig.climbSegments.RemoveAt(index);
            EditorUtility.SetDirty(routeConfig);
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (isExpanded)
        {
            climb.climbName = EditorGUILayout.TextField("Name", climb.climbName);
            climb.difficulty = (DifficultyLevel)EditorGUILayout.EnumPopup("Difficulty", climb.difficulty);
            
            EditorGUILayout.Space();
            climb.startLocation = EditorGUILayout.Slider("Start Location (m)", climb.startLocation, 0f, routeConfig.totalRouteLength);
            climb.length = EditorGUILayout.Slider("Length (m)", climb.length, 100f, 5000f);
            EditorGUILayout.LabelField($"End Location: {climb.EndLocation:F0}m");

            EditorGUILayout.Space();
            climb.averageGrade = EditorGUILayout.Slider("Average Grade (%)", climb.averageGrade, 0f, 20f);
            climb.visualGradientMultiplier = EditorGUILayout.Slider("Visual Multiplier", climb.visualGradientMultiplier, 0.5f, 5f);
            
            EditorGUILayout.Space();
            climb.undulationFrequency = EditorGUILayout.Slider("Undulation Frequency", climb.undulationFrequency, 0f, 1f);
            climb.undulationAmplitude = EditorGUILayout.Slider("Undulation Amplitude", climb.undulationAmplitude, 0f, 2f);
            
            EditorGUILayout.Space();
            climb.startTransition = EditorGUILayout.Slider("Start Transition (m)", climb.startTransition, 0f, 200f);
            climb.endTransition = EditorGUILayout.Slider("End Transition (m)", climb.endTransition, 0f, 200f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Checkpoints", EditorStyles.boldLabel);
            climb.checkpointAtStart = EditorGUILayout.Toggle("Checkpoint at Start", climb.checkpointAtStart);
            climb.checkpointAtEnd = EditorGUILayout.Toggle("Checkpoint at End", climb.checkpointAtEnd);
            climb.checkpointMidClimb = EditorGUILayout.Toggle("Checkpoint Mid-Climb", climb.checkpointMidClimb);
            if (climb.checkpointMidClimb)
            {
                climb.midClimbPosition = EditorGUILayout.Slider("Mid Position (0-1)", climb.midClimbPosition, 0f, 1f);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void ValidateRoute()
    {
        if (routeConfig == null) return;

        string errorMessage;
        if (routeConfig.ValidateRoute(out errorMessage))
        {
            EditorUtility.DisplayDialog("Validation", "Route configuration is valid!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Validation Error", errorMessage, "OK");
        }
    }
}
