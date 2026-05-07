using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Editor script to quickly create simple tree prefabs from textures
/// </summary>
public class CreateSimpleTreePrefab : EditorWindow
{
    private Texture2D treeTexture;
    private string treeName = "SimpleTree";
    private string prefabPath = "Assets/Prefabs/Trees";
    private Material existingMaterial;
    private bool useExistingMaterial = false;

    [MenuItem("Tools/Create Simple Tree Prefab")]
    public static void ShowWindow()
    {
        GetWindow<CreateSimpleTreePrefab>("Create Tree Prefab");
    }

    private void OnGUI()
    {
        GUILayout.Label("Create Simple Tree from Texture", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Tree name
        treeName = EditorGUILayout.TextField("Tree Name", treeName);

        // Texture selection
        EditorGUILayout.LabelField("Tree Texture", EditorStyles.boldLabel);
        treeTexture = (Texture2D)EditorGUILayout.ObjectField("Texture", treeTexture, typeof(Texture2D), false);

        // Material options
        EditorGUILayout.Space();
        useExistingMaterial = EditorGUILayout.Toggle("Use Existing Material", useExistingMaterial);
        if (useExistingMaterial)
        {
            existingMaterial = (Material)EditorGUILayout.ObjectField("Material", existingMaterial, typeof(Material), false);
        }

        // Prefab path
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefab Save Path", EditorStyles.boldLabel);
        prefabPath = EditorGUILayout.TextField("Path", prefabPath);

        EditorGUILayout.Space();

        // Create button
        GUI.enabled = treeTexture != null && !string.IsNullOrEmpty(treeName);
        if (GUILayout.Button("Create Tree Prefab", GUILayout.Height(30)))
        {
            CreateTree();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("This will create a simple tree using a Quad with your texture. " +
            "Make sure the texture has transparency if needed.", MessageType.Info);
    }

    private void CreateTree()
    {
        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder(prefabPath))
        {
            string[] folders = prefabPath.Split('/');
            string currentPath = folders[0]; // "Assets"
            
            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = newPath;
            }
        }

        // Create Quad GameObject
        GameObject tree = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tree.name = treeName;

        // Create or use material
        Material material;
        if (useExistingMaterial && existingMaterial != null)
        {
            material = existingMaterial;
        }
        else
        {
            material = new Material(Shader.Find("Standard"));
            material.name = treeName + "_Material";
            material.mainTexture = treeTexture;
            
            // Set to Cutout for transparency
            material.SetFloat("_Mode", 1); // Cutout mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        // Assign material
        tree.GetComponent<Renderer>().material = material;

        // Scale tree appropriately (adjust as needed)
        tree.transform.localScale = new Vector3(2f, 3f, 1f); // Width x Height

        // Create prefab
        string prefabFullPath = prefabPath + "/" + treeName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(tree, prefabFullPath);
        
        // Clean up scene object
        DestroyImmediate(tree);

        Debug.Log($"[CreateSimpleTreePrefab] ✓ Created tree prefab: {prefabFullPath}");
        
        // Select the created prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFullPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        EditorUtility.DisplayDialog("Success", $"Tree prefab created at:\n{prefabFullPath}", "OK");
    }
}

#endif
