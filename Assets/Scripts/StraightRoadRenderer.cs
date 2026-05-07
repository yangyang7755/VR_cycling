using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders a visible road along the bike's path on the terrain.
/// Works with ANY movement mode (straight, spline, looped).
/// Generates a road mesh strip ahead of and behind the bike,
/// following the terrain surface.
/// 
/// SETUP: Add to any GameObject. Auto-finds BikeController and Terrain.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class StraightRoadRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Terrain terrain;
    
    [Header("Road Dimensions")]
    [Tooltip("Road width in meters")]
    [SerializeField] private float roadWidth = 10f;
    
    [Tooltip("Road length ahead of bike (meters)")]
    [SerializeField] private float roadAhead = 500f;
    
    [Tooltip("Road length behind bike (meters)")]
    [SerializeField] private float roadBehind = 50f;
    
    [Tooltip("Height above terrain surface")]
    [SerializeField] private float heightOffset = 0.15f;
    
    [Tooltip("Sample interval (meters). Lower = smoother.")]
    [SerializeField] private float sampleInterval = 3f;
    
    [Header("Appearance")]
    [SerializeField] private Material roadMaterial;
    [SerializeField] private Color roadColor = new Color(0.22f, 0.22f, 0.24f);
    
    [Tooltip("Road texture (drag Road_2lane_dark02 here, or leave empty for procedural)")]
    [SerializeField] private Texture2D roadTexture;
    
    [Header("Line Markings")]
    [SerializeField] private bool showCenterLine = true;
    [SerializeField] private bool showEdgeLines = true;
    
    [Header("Update")]
    [Tooltip("Rebuild road mesh every N seconds")]
    [SerializeField] private float rebuildInterval = 0.5f;
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private float lastRebuildTime;
    private GameObject centerLineObj;
    private GameObject leftEdgeObj;
    private GameObject rightEdgeObj;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }
    
    private void Start()
    {
        if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
        if (terrain == null) terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null) Debug.LogError("[StraightRoadRenderer] BikeController not found!");
        if (terrain == null) Debug.LogError("[StraightRoadRenderer] Terrain not found!");
        
        // Reset transform — road mesh uses world-space vertices
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        
        // Force recreate material (clear any serialized stale material)
        roadMaterial = null;
        
        SetupMaterial();
        BuildRoad();
        
        Debug.Log($"[StraightRoadRenderer] Road initialized. Bike: {bikeController != null}, Terrain: {terrain != null}, Shader: {roadMaterial?.shader?.name}");
    }
    
    private void Update()
    {
        if (Time.time - lastRebuildTime > rebuildInterval)
        {
            BuildRoad();
            lastRebuildTime = Time.time;
        }
    }
    
    private void SetupMaterial()
    {
        // Always create fresh material to avoid stale serialized state
        
        // Try to load road texture
        if (roadTexture == null)
        {
            #if UNITY_EDITOR
            roadTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Road_2lane_dark02.png");
            #endif
        }
        
        // If still no texture, generate procedural asphalt
        if (roadTexture == null)
        {
            roadTexture = GenerateAsphaltTexture(256, 512);
        }
        
        // Create material by copying the default primitive material (guaranteed correct shader)
        GameObject tempPrim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        roadMaterial = new Material(tempPrim.GetComponent<Renderer>().sharedMaterial);
        Destroy(tempPrim);
        
        if (roadTexture != null)
        {
            if (roadMaterial.HasProperty("_BaseMap"))
                roadMaterial.SetTexture("_BaseMap", roadTexture);
            if (roadMaterial.HasProperty("_MainTex"))
                roadMaterial.SetTexture("_MainTex", roadTexture);
            
            roadMaterial.mainTextureScale = new Vector2(1f, 0.03f);
            
            // Set base color to white so texture shows at full brightness
            Color tint = Color.white;
            if (roadMaterial.HasProperty("_BaseColor")) roadMaterial.SetColor("_BaseColor", tint);
            if (roadMaterial.HasProperty("_Color")) roadMaterial.SetColor("_Color", tint);
        }
        
        // Disable culling so road is visible from both sides
        roadMaterial.SetInt("_Cull", 0); // 0 = Off (double-sided)
        
        meshRenderer.material = roadMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    
    private void BuildRoad()
    {
        if (bikeController == null || terrain == null) return;
        
        Transform bikeT = bikeController.transform;
        Vector3 bikePos = bikeT.position;
        Vector3 forward = bikeT.forward;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.right;
        forward.y = 0f;
        forward.Normalize();
        
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float halfWidth = roadWidth * 0.5f;
        
        // Build mesh from behind bike to ahead
        float startDist = -roadBehind;
        float endDist = roadAhead;
        int sampleCount = Mathf.CeilToInt((endDist - startDist) / sampleInterval) + 1;
        
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        
        for (int i = 0; i < sampleCount; i++)
        {
            float dist = startDist + i * sampleInterval;
            float t = (float)i / (sampleCount - 1);
            
            Vector3 center = bikePos + forward * dist;
            
            Vector3 leftPos = center - right * halfWidth;
            Vector3 rightPos = center + right * halfWidth;
            
            // Sample terrain height
            leftPos.y = terrain.SampleHeight(leftPos) + heightOffset;
            rightPos.y = terrain.SampleHeight(rightPos) + heightOffset;
            
            verts.Add(leftPos);
            verts.Add(rightPos);
            
            float uvY = t * ((endDist - startDist) / roadWidth);
            uvs.Add(new Vector2(0f, uvY));
            uvs.Add(new Vector2(1f, uvY));
            
            if (i > 0)
            {
                int idx = (i - 1) * 2;
                // Front face
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
                tris.Add(idx + 1); tris.Add(idx + 2); tris.Add(idx + 3);
                // Back face (so road is visible from any angle)
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
                tris.Add(idx + 1); tris.Add(idx + 3); tris.Add(idx + 2);
            }
        }
        
        Mesh mesh = meshFilter.mesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "RoadStrip";
        }
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.mesh = mesh;
        
        // Build line markings
        if (showCenterLine) BuildLine("CenterLine", ref centerLineObj, bikePos, forward, 0f, Color.white, 0.25f, true);
        if (showEdgeLines)
        {
            BuildLine("LeftEdge", ref leftEdgeObj, bikePos, forward, -halfWidth + 0.3f, Color.white, 0.2f, false);
            BuildLine("RightEdge", ref rightEdgeObj, bikePos, forward, halfWidth - 0.3f, Color.white, 0.2f, false);
        }
    }

    private void BuildLine(string name, ref GameObject lineObj, Vector3 bikePos, Vector3 forward, float lateralOffset, Color color, float lineWidth, bool dashed)
    {
        if (lineObj == null)
        {
            lineObj = new GameObject(name);
            lineObj.transform.SetParent(transform, false);
            lineObj.AddComponent<MeshFilter>();
            MeshRenderer mr = lineObj.AddComponent<MeshRenderer>();
            mr.material = CreateMaterial(color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float halfW = lineWidth * 0.5f;
        float lineY = heightOffset + 0.02f;
        
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        
        float startDist = -roadBehind;
        float endDist = roadAhead;
        
        for (float dist = startDist; dist <= endDist; dist += sampleInterval * 0.5f)
        {
            if (dashed)
            {
                float posInCycle = ((dist - startDist) % 6f);
                if (posInCycle > 3.5f) continue; // gap in dash
            }
            
            Vector3 center = bikePos + forward * dist + right * lateralOffset;
            center.y = terrain.SampleHeight(center) + lineY;
            
            Vector3 left = center - right * halfW;
            Vector3 rt = center + right * halfW;
            
            int idx = verts.Count;
            verts.Add(left);
            verts.Add(rt);
            
            if (idx >= 2)
            {
                tris.Add(idx - 2); tris.Add(idx); tris.Add(idx - 1);
                tris.Add(idx - 1); tris.Add(idx); tris.Add(idx + 1);
            }
        }
        
        MeshFilter mf = lineObj.GetComponent<MeshFilter>();
        Mesh mesh = mf.mesh;
        if (mesh == null) mesh = new Mesh();
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mf.mesh = mesh;
    }
    
    private Material CreateMaterial(Color color)
    {
        return RuntimeMaterialHelper.CreateMaterial(color);
    }
    
    private Texture2D GenerateAsphaltTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, true);
        tex.name = "ProceduralAsphalt";
        
        Color baseAsphalt = new Color(0.2f, 0.2f, 0.22f);
        Color darkGrain = new Color(0.15f, 0.15f, 0.17f);
        Color lightGrain = new Color(0.28f, 0.28f, 0.3f);
        Color lineColor = new Color(0.9f, 0.9f, 0.85f);
        
        float centerX = width * 0.5f;
        float edgeX1 = width * 0.06f;
        float edgeX2 = width * 0.94f;
        float lw = width * 0.012f;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                float n2 = Mathf.PerlinNoise(x * 0.3f + 50f, y * 0.3f + 50f);
                float grain = n1 * 0.6f + n2 * 0.4f;
                
                Color pixel = Color.Lerp(darkGrain, lightGrain, grain);
                pixel = Color.Lerp(baseAsphalt, pixel, 0.5f);
                
                // Center dashed line
                if (Mathf.Abs(x - centerX) < lw && (y % 40) < 24)
                    pixel = lineColor;
                
                // Edge lines
                if (Mathf.Abs(x - edgeX1) < lw || Mathf.Abs(x - edgeX2) < lw)
                    pixel = lineColor;
                
                tex.SetPixel(x, y, pixel);
            }
        }
        
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
    
    private void OnDestroy()
    {
        if (centerLineObj != null) Destroy(centerLineObj);
        if (leftEdgeObj != null) Destroy(leftEdgeObj);
        if (rightEdgeObj != null) Destroy(rightEdgeObj);
    }
}
