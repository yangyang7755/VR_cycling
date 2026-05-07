using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper script to easily apply skybox material to the scene
/// </summary>
public class SkyboxSetup : MonoBehaviour
{
    [Header("Skybox Settings")]
    [Tooltip("Skybox material to apply to the scene")]
    [SerializeField] private Material skyboxMaterial;

    [Header("Auto Setup")]
    [Tooltip("Automatically apply skybox when scene starts")]
    [SerializeField] private bool applyOnStart = true;

    private void Start()
    {
        if (applyOnStart && skyboxMaterial != null)
        {
            ApplySkybox();
        }
    }

    /// <summary>
    /// Apply the skybox material to the scene
    /// </summary>
    [ContextMenu("Apply Skybox")]
    public void ApplySkybox()
    {
        if (skyboxMaterial == null)
        {
            Debug.LogError("[SkyboxSetup] Skybox material not assigned! Please assign it in Inspector.");
            return;
        }

        // Set skybox material
        RenderSettings.skybox = skyboxMaterial;

        Debug.Log($"[SkyboxSetup] ✓ Skybox applied: {skyboxMaterial.name}");

        // Force update lighting if in editor
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Update scene view
            UnityEditor.SceneView.RepaintAll();
            Debug.Log("[SkyboxSetup] Scene view updated. Skybox should be visible now.");
        }
        #endif
    }

    /// <summary>
    /// Auto-find skybox material from FreeMountain assets
    /// </summary>
    [ContextMenu("Auto-Find Skybox Material")]
    public void AutoFindSkyboxMaterial()
    {
        #if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("Free_skybox t:Material", new[] { "Assets/Textures" });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            skyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (skyboxMaterial != null)
            {
                Debug.Log($"[SkyboxSetup] ✓ Found skybox material: {path}");
                ApplySkybox();
            }
        }
        else
        {
            Debug.LogWarning("[SkyboxSetup] Could not find Free_skybox material. Please assign manually.");
        }
        #endif
    }

    /// <summary>
    /// Remove skybox (set to null)
    /// </summary>
    [ContextMenu("Remove Skybox")]
    public void RemoveSkybox()
    {
        RenderSettings.skybox = null;
        Debug.Log("[SkyboxSetup] Skybox removed.");
        
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneView.RepaintAll();
        }
        #endif
    }

    /// <summary>
    /// Get current skybox material
    /// </summary>
    public Material GetCurrentSkybox()
    {
        return RenderSettings.skybox;
    }
}
