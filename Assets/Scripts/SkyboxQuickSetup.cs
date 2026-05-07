using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quick setup helper for skybox - specifically for setting skybox_back on horizon
/// </summary>
public class SkyboxQuickSetup : MonoBehaviour
{
    [Header("Skybox Material")]
    [Tooltip("Skybox material to apply (Free_skybox.mat)")]
    [SerializeField] private Material skyboxMaterial;

    [Header("Texture Assignment")]
    [Tooltip("Road direction: X-axis (right) or Z-axis (forward)")]
    [SerializeField] private RoadDirection roadDirection = RoadDirection.XAxis;

    public enum RoadDirection
    {
        XAxis,  // Road goes right (X+), looking forward sees +Z
        ZAxis   // Road goes forward (Z+), looking forward sees +Z
    }

    [Header("Auto Setup")]
    [Tooltip("Automatically apply skybox when scene starts")]
    [SerializeField] private bool applyOnStart = true;

    private void Start()
    {
        if (applyOnStart)
        {
            SetupSkybox();
        }
    }

    /// <summary>
    /// Setup skybox with skybox_back on horizon
    /// </summary>
    [ContextMenu("Setup Skybox")]
    public void SetupSkybox()
    {
        // Try to find skybox material if not assigned
        if (skyboxMaterial == null)
        {
            FindSkyboxMaterial();
        }

        if (skyboxMaterial == null)
        {
            Debug.LogError("[SkyboxQuickSetup] Skybox material not found! Please assign Free_skybox.mat manually.");
            return;
        }

        // Apply skybox to scene
        RenderSettings.skybox = skyboxMaterial;

        // Ensure camera uses skybox
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.Skybox;
        }

        Debug.Log($"[SkyboxQuickSetup] ✓ Skybox applied: {skyboxMaterial.name}");
        Debug.Log($"[SkyboxQuickSetup] Road direction: {roadDirection}");
        Debug.Log($"[SkyboxQuickSetup] skybox_back should appear on horizon when looking forward");

        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneView.RepaintAll();
        }
        #endif
    }

    /// <summary>
    /// Auto-find Free_skybox material
    /// </summary>
    [ContextMenu("Find Skybox Material")]
    private void FindSkyboxMaterial()
    {
        #if UNITY_EDITOR
        // Search for Free_skybox material
        string[] guids = AssetDatabase.FindAssets("Free_skybox t:Material", new[] { "Assets" });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            skyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (skyboxMaterial != null)
            {
                Debug.Log($"[SkyboxQuickSetup] ✓ Found skybox material: {path}");
            }
        }
        else
        {
            Debug.LogWarning("[SkyboxQuickSetup] Could not find Free_skybox material. Please assign manually.");
        }
        #endif
    }

    /// <summary>
    /// Create a simple skybox material with only skybox_back
    /// </summary>
    [ContextMenu("Create Simple Skybox with skybox_back")]
    public void CreateSimpleSkybox()
    {
        #if UNITY_EDITOR
        // Find skybox_back texture
        string[] guids = AssetDatabase.FindAssets("skybox_back t:Texture2D", new[] { "Assets" });
        if (guids.Length == 0)
        {
            Debug.LogError("[SkyboxQuickSetup] Could not find skybox_back texture!");
            return;
        }

        string texturePath = AssetDatabase.GUIDToAssetPath(guids[0]);
        Texture2D backTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        if (backTexture == null)
        {
            Debug.LogError($"[SkyboxQuickSetup] Could not load texture: {texturePath}");
            return;
        }

        // Create new material
        Material newSkybox = new Material(Shader.Find("Skybox/6 Sided"));
        newSkybox.name = "SimpleSkybox_BackOnly";

        // Assign skybox_back to all 6 faces (simple background)
        newSkybox.SetTexture("_FrontTex", backTexture);
        newSkybox.SetTexture("_BackTex", backTexture);
        newSkybox.SetTexture("_LeftTex", backTexture);
        newSkybox.SetTexture("_RightTex", backTexture);
        newSkybox.SetTexture("_UpTex", backTexture);
        newSkybox.SetTexture("_DownTex", backTexture);

        // Save material
        string materialPath = "Assets/Textures/FreeMountain/Skybox/SimpleSkybox_BackOnly.mat";
        AssetDatabase.CreateAsset(newSkybox, materialPath);
        AssetDatabase.SaveAssets();

        skyboxMaterial = newSkybox;
        Debug.Log($"[SkyboxQuickSetup] ✓ Created simple skybox material: {materialPath}");
        Debug.Log("[SkyboxQuickSetup] skybox_back is now assigned to all faces");

        SetupSkybox();
        #endif
    }

    /// <summary>
    /// Get current skybox material
    /// </summary>
    public Material GetCurrentSkybox()
    {
        return RenderSettings.skybox;
    }

    /// <summary>
    /// Remove skybox
    /// </summary>
    [ContextMenu("Remove Skybox")]
    public void RemoveSkybox()
    {
        RenderSettings.skybox = null;
        Debug.Log("[SkyboxQuickSetup] Skybox removed.");

        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneView.RepaintAll();
        }
        #endif
    }
}
