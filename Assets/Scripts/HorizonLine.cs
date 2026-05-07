using UnityEngine;

/// <summary>
/// Creates a visible horizon line to help visualize road incline
/// Can be displayed as a 3D line or UI element
/// </summary>
public class HorizonLine : MonoBehaviour
{
    [Header("Display Mode")]
    [Tooltip("How to display the horizon line")]
    [SerializeField] private HorizonDisplayMode displayMode = HorizonDisplayMode.LineRenderer3D;

    public enum HorizonDisplayMode
    {
        LineRenderer3D,  // 3D line in the scene
        UIOverlay,       // UI line on screen
        Both             // Both 3D and UI
    }

    [Header("3D Line Renderer Settings")]
    [Tooltip("Line renderer component (auto-created if null)")]
    [SerializeField] private LineRenderer lineRenderer;
    
    [Tooltip("Material for the horizon line")]
    [SerializeField] private Material lineMaterial;
    
    [Tooltip("Line color")]
    [SerializeField] private Color lineColor = Color.white;
    
    [Tooltip("Line width")]
    [SerializeField] private float lineWidth = 0.1f;
    
    [Tooltip("Distance from camera to draw horizon line (meters)")]
    [SerializeField] private float horizonDistance = 500f;
    
    [Tooltip("Height of horizon line above terrain (meters)")]
    [SerializeField] private float horizonHeight = 0f;

    [Header("UI Overlay Settings")]
    [Tooltip("UI Canvas to add horizon line to (auto-created if null)")]
    [SerializeField] private Canvas uiCanvas;
    
    [Tooltip("UI line GameObject (auto-created if null)")]
    [SerializeField] private GameObject uiLineObject;
    
    [Tooltip("UI line color")]
    [SerializeField] private Color uiLineColor = Color.yellow;
    
    [Tooltip("UI line thickness (pixels)")]
    [SerializeField] private float uiLineThickness = 2f;
    
    [Tooltip("UI line offset from center (0 = center, positive = above, negative = below)")]
    [Range(-0.5f, 0.5f)]
    [SerializeField] private float uiLineOffset = 0f;

    [Header("Camera Reference")]
    [Tooltip("Camera to calculate horizon position (auto-found if null)")]
    [SerializeField] private Camera targetCamera;

    [Header("Terrain Reference")]
    [Tooltip("Terrain to sample height for horizon line")]
    [SerializeField] private Terrain terrain;

    [Header("Background Image (Skybox Back)")]
    [Tooltip("Enable background image at horizon")]
    [SerializeField] private bool showBackgroundImage = true;
    
    [Tooltip("Background texture (e.g., skybox_back.psd)")]
    [SerializeField] private Texture2D backgroundTexture;
    
    [Tooltip("Background image GameObject (auto-created if null)")]
    [SerializeField] private GameObject backgroundImageObject;
    
    [Tooltip("Distance from camera to background (meters)")]
    [SerializeField] private float backgroundDistance = 1000f;
    
    [Tooltip("Background height above terrain (meters)")]
    [SerializeField] private float backgroundHeight = 0f;
    
    [Tooltip("Background width (meters)")]
    [SerializeField] private float backgroundWidth = 800f;
    
    [Tooltip("Background height size (meters)")]
    [SerializeField] private float backgroundHeightSize = 400f;
    
    [Tooltip("Auto-find skybox_back texture on start")]
    [SerializeField] private bool autoFindSkyboxBack = true;
    
    [Tooltip("Rotation offset for background image (degrees) - adjust if image appears tilted. Positive = rotate clockwise, Negative = rotate counter-clockwise")]
    [Range(-180f, 180f)]
    [SerializeField] private float backgroundRotationOffset = 0f;

    [Header("Auto Setup")]
    [Tooltip("Automatically setup on start")]
    [SerializeField] private bool setupOnStart = true;

    private void Start()
    {
        // Auto-find skybox_back texture if enabled
        if (autoFindSkyboxBack && backgroundTexture == null)
        {
            FindSkyboxBackTexture();
        }

        if (setupOnStart)
        {
            SetupHorizonLine();
        }
    }

    private void LateUpdate()
    {
        // Update horizon line position based on camera
        if (targetCamera != null)
        {
            UpdateHorizonPosition();
            
            // Update background image position
            if (showBackgroundImage && backgroundImageObject != null)
            {
                UpdateBackgroundPosition();
            }
        }
    }

    /// <summary>
    /// Setup horizon line based on display mode
    /// </summary>
    [ContextMenu("Setup Horizon Line")]
    public void SetupHorizonLine()
    {
        // Auto-find camera
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindObjectOfType<Camera>();
            }
        }

        // Auto-find terrain
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        // Setup based on display mode
        if (displayMode == HorizonDisplayMode.LineRenderer3D || displayMode == HorizonDisplayMode.Both)
        {
            Setup3DLineRenderer();
        }

        if (displayMode == HorizonDisplayMode.UIOverlay || displayMode == HorizonDisplayMode.Both)
        {
            SetupUIOverlay();
        }

        // Setup background image if enabled
        if (showBackgroundImage)
        {
            SetupBackgroundImage();
        }

        Debug.Log($"[HorizonLine] Horizon line setup complete. Mode: {displayMode}");
    }

    /// <summary>
    /// Auto-find skybox_back texture
    /// </summary>
    private void FindSkyboxBackTexture()
    {
        #if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("skybox_back t:Texture2D", new[] { "Assets" });
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            backgroundTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (backgroundTexture != null)
            {
                Debug.Log($"[HorizonLine] ✓ Found skybox_back texture: {path}");
            }
        }
        else
        {
            Debug.LogWarning("[HorizonLine] Could not find skybox_back texture. Please assign manually in Inspector.");
        }
        #endif
    }

    /// <summary>
    /// Setup background image at horizon
    /// </summary>
    private void SetupBackgroundImage()
    {
        if (backgroundTexture == null)
        {
            Debug.LogWarning("[HorizonLine] Background texture not assigned. Background image will not be created.");
            return;
        }

        // Create or get background object
        if (backgroundImageObject == null)
        {
            backgroundImageObject = new GameObject("HorizonBackground");
            backgroundImageObject.transform.SetParent(transform);
        }

        // Get or add MeshRenderer and MeshFilter
        MeshFilter meshFilter = backgroundImageObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = backgroundImageObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = backgroundImageObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = backgroundImageObject.AddComponent<MeshRenderer>();
        }

        // Create quad mesh
        Mesh quadMesh = CreateQuadMesh(backgroundWidth, backgroundHeightSize);
        meshFilter.mesh = quadMesh;

        // Create material with background texture
        Material backgroundMaterial = new Material(Shader.Find("Unlit/Texture"));
        backgroundMaterial.mainTexture = backgroundTexture;
        meshRenderer.material = backgroundMaterial;

        // Position background
        UpdateBackgroundPosition();

        Debug.Log("[HorizonLine] ✓ Background image setup complete");
    }

    /// <summary>
    /// Create a quad mesh for background
    /// </summary>
    private Mesh CreateQuadMesh(float width, float height)
    {
        Mesh mesh = new Mesh();
        mesh.name = "HorizonBackgroundQuad";

        // Vertices
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-width / 2f, -height / 2f, 0),  // Bottom-left
            new Vector3(width / 2f, -height / 2f, 0),   // Bottom-right
            new Vector3(-width / 2f, height / 2f, 0),   // Top-left
            new Vector3(width / 2f, height / 2f, 0)    // Top-right
        };

        // Triangles
        int[] triangles = new int[6]
        {
            0, 2, 1,  // First triangle
            2, 3, 1   // Second triangle
        };

        // UVs
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),  // Bottom-left
            new Vector2(1, 0),  // Bottom-right
            new Vector2(0, 1),  // Top-left
            new Vector2(1, 1)   // Top-right
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        return mesh;
    }

    /// <summary>
    /// Update background image position based on camera
    /// </summary>
    private void UpdateBackgroundPosition()
    {
        if (backgroundImageObject == null || targetCamera == null) return;

        // Calculate position in front of camera at horizon distance
        Vector3 cameraPos = targetCamera.transform.position;
        Vector3 cameraForward = targetCamera.transform.forward;
        Vector3 cameraRight = targetCamera.transform.right;

        // Position background at horizon distance
        Vector3 backgroundPos = cameraPos + cameraForward * backgroundDistance;

        // Sample terrain height if available
        if (terrain != null)
        {
            float terrainHeight = terrain.SampleHeight(backgroundPos);
            backgroundPos.y = terrainHeight + backgroundHeight;
        }
        else
        {
            backgroundPos.y = cameraPos.y + backgroundHeight;
        }

        // Set position
        backgroundImageObject.transform.position = backgroundPos;

        // Rotate to face camera, but keep it level (horizontal)
        Vector3 directionToCamera = (cameraPos - backgroundPos);
        directionToCamera.y = 0; // Keep horizontal (no vertical tilt)
        directionToCamera.Normalize();
        
        if (directionToCamera != Vector3.zero)
        {
            // Look at camera but keep upright (no roll or pitch)
            Quaternion baseRotation = Quaternion.LookRotation(-directionToCamera, Vector3.up);
            
            // Apply rotation offset if needed
            if (Mathf.Abs(backgroundRotationOffset) > 0.01f)
            {
                baseRotation *= Quaternion.Euler(0, 0, backgroundRotationOffset);
            }
            
            backgroundImageObject.transform.rotation = baseRotation;
        }
        else
        {
            // Fallback: face forward, level
            Quaternion baseRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            if (Mathf.Abs(backgroundRotationOffset) > 0.01f)
            {
                baseRotation *= Quaternion.Euler(0, 0, backgroundRotationOffset);
            }
            backgroundImageObject.transform.rotation = baseRotation;
        }
    }

    /// <summary>
    /// Setup 3D line renderer for horizon
    /// </summary>
    private void Setup3DLineRenderer()
    {
        // Get or create line renderer
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        // Configure line renderer
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.positionCount = 2;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        // Set material
        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
        }
        else
        {
            // Create default material
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.material.color = lineColor;
        }

        // Set initial positions
        UpdateHorizonPosition();
    }

    /// <summary>
    /// Setup UI overlay for horizon
    /// </summary>
    private void SetupUIOverlay()
    {
        // Find or create canvas
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas == null)
            {
                // Create canvas
                GameObject canvasObj = new GameObject("HorizonCanvas");
                uiCanvas = canvasObj.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }

        // Create UI line if needed
        if (uiLineObject == null)
        {
            uiLineObject = new GameObject("HorizonLineUI");
            uiLineObject.transform.SetParent(uiCanvas.transform, false);

            // Add RectTransform
            RectTransform rectTransform = uiLineObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0.5f + uiLineOffset);
            rectTransform.anchorMax = new Vector2(1, 0.5f + uiLineOffset);
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;

            // Add Image component for line
            UnityEngine.UI.Image lineImage = uiLineObject.AddComponent<UnityEngine.UI.Image>();
            lineImage.color = uiLineColor;
            rectTransform.sizeDelta = new Vector2(Screen.width, uiLineThickness);
        }
    }

    /// <summary>
    /// Update horizon line position based on camera
    /// </summary>
    private void UpdateHorizonPosition()
    {
        if (targetCamera == null) return;

        // Calculate horizon position in world space
        Vector3 cameraPos = targetCamera.transform.position;
        Vector3 cameraForward = targetCamera.transform.forward;
        Vector3 cameraRight = targetCamera.transform.right;

        // Calculate horizon point (straight ahead at horizon distance)
        Vector3 horizonCenter = cameraPos + cameraForward * horizonDistance;

        // Sample terrain height if available
        if (terrain != null)
        {
            float terrainHeight = terrain.SampleHeight(horizonCenter);
            horizonCenter.y = terrainHeight + horizonHeight;
        }
        else
        {
            horizonCenter.y = cameraPos.y + horizonHeight;
        }

        // Update 3D line renderer
        if (lineRenderer != null && (displayMode == HorizonDisplayMode.LineRenderer3D || displayMode == HorizonDisplayMode.Both))
        {
            // Create line spanning across view
            float lineLength = horizonDistance * 0.5f; // Half the horizon distance for line length
            Vector3 lineStart = horizonCenter - cameraRight * lineLength;
            Vector3 lineEnd = horizonCenter + cameraRight * lineLength;

            lineRenderer.SetPosition(0, lineStart);
            lineRenderer.SetPosition(1, lineEnd);
        }

        // UI overlay position is handled by RectTransform anchors, no update needed
    }

    /// <summary>
    /// Show horizon line
    /// </summary>
    public void ShowHorizon()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
        }
        if (uiLineObject != null)
        {
            uiLineObject.SetActive(true);
        }
    }

    /// <summary>
    /// Hide horizon line
    /// </summary>
    public void HideHorizon()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        if (uiLineObject != null)
        {
            uiLineObject.SetActive(false);
        }
    }

    /// <summary>
    /// Toggle horizon line visibility
    /// </summary>
    [ContextMenu("Toggle Horizon")]
    public void ToggleHorizon()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = !lineRenderer.enabled;
        }
        if (uiLineObject != null)
        {
            uiLineObject.SetActive(!uiLineObject.activeSelf);
        }
    }

    /// <summary>
    /// Set horizon line color
    /// </summary>
    public void SetLineColor(Color color)
    {
        lineColor = color;
        if (lineRenderer != null)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }
        if (uiLineObject != null)
        {
            UnityEngine.UI.Image image = uiLineObject.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.color = color;
            }
        }
    }

    /// <summary>
    /// Set horizon line distance
    /// </summary>
    public void SetHorizonDistance(float distance)
    {
        horizonDistance = distance;
        UpdateHorizonPosition();
    }

    /// <summary>
    /// Set horizon visibility (0 = invisible, 1 = fully visible)
    /// </summary>
    public void SetVisibility(float visibility)
    {
        visibility = Mathf.Clamp01(visibility);
        
        // Update line renderer alpha
        if (lineRenderer != null)
        {
            Color startColor = lineRenderer.startColor;
            Color endColor = lineRenderer.endColor;
            startColor.a = visibility;
            endColor.a = visibility;
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
        }
        
        // Update UI line alpha
        if (uiLineObject != null)
        {
            var image = uiLineObject.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                Color color = image.color;
                color.a = visibility;
                image.color = color;
            }
        }
        
        // Update background image alpha
        if (backgroundImageObject != null)
        {
            var renderer = backgroundImageObject.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                Color color = renderer.material.color;
                color.a = visibility;
                renderer.material.color = color;
                
                // Enable transparency if needed
                if (visibility < 1f)
                {
                    var mat = renderer.material;
                    if (mat.shader.name.Contains("Standard") || mat.shader.name.Contains("Unlit"))
                    {
                        // Switch to transparent shader if needed
                        if (mat.GetFloat("_Mode") != 3) // Transparent mode
                        {
                            mat.SetFloat("_Mode", 3);
                            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            mat.SetInt("_ZWrite", 0);
                            mat.DisableKeyword("_ALPHATEST_ON");
                            mat.EnableKeyword("_ALPHABLEND_ON");
                            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            mat.renderQueue = 3000;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Set background texture
    /// </summary>
    public void SetBackgroundTexture(Texture2D texture)
    {
        backgroundTexture = texture;
        if (backgroundImageObject != null)
        {
            MeshRenderer renderer = backgroundImageObject.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.mainTexture = texture;
            }
        }
    }

    /// <summary>
    /// Show/hide background image
    /// </summary>
    public void SetBackgroundVisible(bool visible)
    {
        showBackgroundImage = visible;
        if (backgroundImageObject != null)
        {
            backgroundImageObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Get background image object
    /// </summary>
    public GameObject GetBackgroundObject()
    {
        return backgroundImageObject;
    }
}
