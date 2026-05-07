using UnityEngine;

/// <summary>
/// Sets up a stable environment that doesn't break at terrain edges.
/// Creates a large ground plane beyond the terrain, configures fog for depth,
/// and adjusts camera far clip to prevent background pop-in.
/// 
/// Fixes the issue where the background appears/disappears on gradient changes
/// because the camera sees past the terrain edge.
/// 
/// SETUP: Add to any GameObject. Runs on Start.
/// </summary>
public class EnvironmentSetup : MonoBehaviour
{
    [Header("Ground Plane")]
    [Tooltip("Create a large ground plane extending beyond terrain")]
    [SerializeField] private bool createGroundPlane = true;
    
    [Tooltip("Ground plane size (meters). Should be much larger than terrain.")]
    [SerializeField] private float groundPlaneSize = 10000f;
    
    [Tooltip("Ground plane color")]
    [SerializeField] private Color groundColor = new Color(0.35f, 0.55f, 0.2f); // grass green
    
    [Tooltip("Ground plane Y offset below terrain")]
    [SerializeField] private float groundYOffset = -0.5f;
    
    [Header("Fog")]
    [Tooltip("Enable distance fog to hide terrain edges")]
    [SerializeField] private bool enableFog = true;
    
    [Tooltip("Fog color (should blend with skybox horizon)")]
    [SerializeField] private Color fogColor = new Color(0.7f, 0.8f, 0.9f);
    
    [Tooltip("Fog start distance")]
    [SerializeField] private float fogStart = 200f;
    
    [Tooltip("Fog end distance (objects fully fogged beyond this)")]
    [SerializeField] private float fogEnd = 800f;
    
    [Header("Camera")]
    [Tooltip("Set camera far clip plane")]
    [SerializeField] private bool adjustCamera = true;
    
    [Tooltip("Camera far clip distance")]
    [SerializeField] private float cameraFarClip = 2000f;
    
    [Header("Skybox")]
    [Tooltip("Ensure skybox is set (uses default procedural if none)")]
    [SerializeField] private bool ensureSkybox = true;
    
    [Tooltip("Ambient light intensity")]
    [Range(0.5f, 2f)]
    [SerializeField] private float ambientIntensity = 1.2f;
    
    private GameObject groundPlaneObj;

    private void Start()
    {
        if (createGroundPlane) CreateGroundPlane();
        if (enableFog) SetupFog();
        if (adjustCamera) SetupCamera();
        if (ensureSkybox) SetupSkybox();
        
        Debug.Log("[EnvironmentSetup] Environment configured");
    }
    
    private void CreateGroundPlane()
    {
        // Find terrain to position the ground plane
        Terrain terrain = FindObjectOfType<Terrain>();
        Vector3 center = Vector3.zero;
        float baseY = 0f;
        
        if (terrain != null)
        {
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            center = terrainPos + new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f);
            baseY = terrainPos.y + groundYOffset;
        }
        
        // Create a large flat quad
        groundPlaneObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundPlaneObj.name = "EnvironmentGroundPlane";
        groundPlaneObj.transform.position = new Vector3(center.x, baseY, center.z);
        
        // Scale: Unity plane is 10x10 by default, so scale = size/10
        float scale = groundPlaneSize / 10f;
        groundPlaneObj.transform.localScale = new Vector3(scale, 1f, scale);
        
        // Material — get from the primitive's own default (Unity assigns correct pipeline shader)
        Renderer rend = groundPlaneObj.GetComponent<Renderer>();
        Material mat = new Material(rend.sharedMaterial); // copy the default material
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", groundColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", groundColor);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = true;
        
        // Remove collider so it doesn't interfere with anything
        Collider col = groundPlaneObj.GetComponent<Collider>();
        if (col != null) Destroy(col);
        
        Debug.Log($"[EnvironmentSetup] Ground plane created: {groundPlaneSize}m at Y={baseY}, shader: {mat.shader.name}");
    }
    
    private void SetupFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        
        Debug.Log($"[EnvironmentSetup] Fog enabled: {fogStart}m to {fogEnd}m");
    }
    
    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.farClipPlane = cameraFarClip;
            cam.nearClipPlane = 0.3f;
            
            // Set clear flags to skybox
            cam.clearFlags = CameraClearFlags.Skybox;
        }
    }
    
    private void SetupSkybox()
    {
        // Boost ambient light
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        // Set up a nice procedural skybox
        Material skyMat = new Material(Shader.Find("Skybox/Procedural"));
        if (skyMat != null)
        {
            skyMat.SetFloat("_SunSize", 0.04f);
            skyMat.SetFloat("_SunSizeConvergence", 5f);
            skyMat.SetFloat("_AtmosphereThickness", 1.2f);
            skyMat.SetFloat("_Exposure", 1.3f);
            skyMat.SetColor("_SkyTint", new Color(0.4f, 0.55f, 0.75f));
            skyMat.SetColor("_GroundColor", new Color(0.35f, 0.45f, 0.3f));
            RenderSettings.skybox = skyMat;
        }

        // Set sun direction for nice lighting
        Light sun = FindObjectOfType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.96f, 0.88f); // warm sunlight
            sun.shadows = LightShadows.Soft;
        }
    }
    
    private void OnDestroy()
    {
        if (groundPlaneObj != null)
        {
            Destroy(groundPlaneObj);
        }
    }
}
