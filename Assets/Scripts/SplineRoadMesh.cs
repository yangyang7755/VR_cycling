using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates a visible road mesh along a SplinePath.
/// Creates a flat ribbon mesh that sits slightly above the terrain surface.
/// Much more reliable than terrain alphamap painting for curved roads.
/// 
/// SETUP: Add to same GameObject as CurvedRouteGenerator. It auto-finds the SplinePath.
/// Assign a road material (dark grey/asphalt). A default material is created if none assigned.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SplineRoadMesh : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplinePath splinePath;
    [SerializeField] private Terrain terrain;
    
    [Header("Road Settings")]
    [Tooltip("Road width in meters")]
    [SerializeField] private float roadWidth = 10f;
    
    [Tooltip("Height offset above terrain surface")]
    [SerializeField] private float heightOffset = 0.05f;
    
    [Tooltip("Sample interval along spline (meters). Lower = smoother but more triangles.")]
    [SerializeField] private float sampleInterval = 2f;
    
    [Header("Road Appearance")]
    [SerializeField] private Material roadMaterial;
    
    [Tooltip("Road texture (auto-loads Road_2lane_dark02 if available)")]
    [SerializeField] private Texture2D roadTexture;
    
    [Tooltip("Road color (used if no texture)")]
    [SerializeField] private Color roadColor = new Color(0.22f, 0.22f, 0.24f);
    
    [Tooltip("UV tiling — how often the texture repeats along the road")]
    [SerializeField] private float textureTilingAlongRoad = 0.05f;
    
    [Header("Center Line")]
    [SerializeField] private bool drawCenterLine = false;
    [SerializeField] private float centerLineWidth = 0.3f;
    [SerializeField] private float centerLineDashLength = 3f;
    [SerializeField] private float centerLineGapLength = 3f;
    
    [Header("Edge Lines")]
    [SerializeField] private bool drawEdgeLines = false;
    [SerializeField] private float edgeLineWidth = 0.2f;
    
    [Header("Auto-Generate")]
    [SerializeField] private bool generateOnStart = false; // CurvedRouteGenerator calls us
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private GameObject centerLineObj;
    private GameObject leftEdgeObj;
    private GameObject rightEdgeObj;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        if (splinePath == null) splinePath = GetComponent<SplinePath>();
        if (splinePath == null) splinePath = FindObjectOfType<SplinePath>();
        if (terrain == null) terrain = FindObjectOfType<Terrain>();
    }
    
    private void Start()
    {
        if (generateOnStart && splinePath != null && splinePath.GetTotalLength() > 0f)
        {
            GenerateRoadMesh();
        }
    }
    
    /// <summary>
    /// Generate the road mesh along the spline. Call after spline is built.
    /// </summary>
    public void GenerateRoadMesh()
    {
        if (splinePath == null || splinePath.GetTotalLength() < 1f)
        {
            Debug.LogWarning("[SplineRoadMesh] No valid spline path.");
            return;
        }
        
        // Ensure material
        if (roadMaterial == null)
        {
            roadMaterial = CreateRoadMaterial();
        }
        meshRenderer.material = roadMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        
        // Build road mesh
        Mesh mesh = BuildRoadStrip(roadWidth, heightOffset);
        meshFilter.mesh = mesh;
        
        // Build line markings
        if (drawCenterLine) BuildCenterLine();
        if (drawEdgeLines) BuildEdgeLines();
        
        Debug.Log($"[SplineRoadMesh] Road mesh generated: {mesh.vertexCount} verts, {mesh.triangles.Length / 3} tris");
    }
    
    private Material CreateRoadMaterial()
    {
        // Create material by copying default primitive material (guaranteed correct URP shader)
        GameObject tempPrim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Material mat = new Material(tempPrim.GetComponent<Renderer>().sharedMaterial);
        Destroy(tempPrim);

        if (roadTexture == null)
            roadTexture = Resources.Load<Texture2D>("Road_2lane_dark02");

        #if UNITY_EDITOR
        if (roadTexture == null)
        {
            roadTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Road_2lane_dark02.png");
        }
        #endif

        if (roadTexture == null)
            roadTexture = GenerateAsphaltTexture(256, 512);

        if (roadTexture != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", roadTexture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", roadTexture);
            mat.mainTextureScale = new Vector2(1f, textureTilingAlongRoad);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
        mat.SetInt("_Cull", 0);

        return mat;
    }
    
    /// <summary>
    /// Generate a procedural asphalt texture with noise grain and lane markings.
    /// </summary>
    private Texture2D GenerateAsphaltTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, true);
        tex.name = "ProceduralAsphalt";
        
        Color baseAsphalt = new Color(0.2f, 0.2f, 0.22f);
        Color darkGrain = new Color(0.15f, 0.15f, 0.17f);
        Color lightGrain = new Color(0.28f, 0.28f, 0.3f);
        Color lineColor = new Color(0.9f, 0.9f, 0.85f);
        
        float centerX = width * 0.5f;
        float edgeLineX1 = width * 0.08f;
        float edgeLineX2 = width * 0.92f;
        float lineWidth = width * 0.015f;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Base asphalt with Perlin noise grain
                float noise1 = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                float noise2 = Mathf.PerlinNoise(x * 0.3f + 50f, y * 0.3f + 50f);
                float grain = noise1 * 0.6f + noise2 * 0.4f;
                
                Color pixel = Color.Lerp(darkGrain, lightGrain, grain);
                pixel = Color.Lerp(baseAsphalt, pixel, 0.5f);
                
                // Center dashed line
                bool isCenterLine = Mathf.Abs(x - centerX) < lineWidth;
                bool isDash = (y % 40) < 24; // dashed pattern
                if (isCenterLine && isDash)
                {
                    pixel = lineColor;
                }
                
                // Edge lines (solid)
                bool isLeftEdge = Mathf.Abs(x - edgeLineX1) < lineWidth;
                bool isRightEdge = Mathf.Abs(x - edgeLineX2) < lineWidth;
                if (isLeftEdge || isRightEdge)
                {
                    pixel = lineColor;
                }
                
                tex.SetPixel(x, y, pixel);
            }
        }
        
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        
        return tex;
    }
    
    private Material CreateCompatibleMaterial(Color color)
    {
        return RuntimeMaterialHelper.CreateMaterial(color);
    }
    
    private Mesh BuildRoadStrip(float width, float yOffset)
    {
        float totalLen = splinePath.GetTotalLength();
        int sampleCount = Mathf.CeilToInt(totalLen / sampleInterval) + 1;
        float halfWidth = width * 0.5f;
        
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        
        for (int i = 0; i < sampleCount; i++)
        {
            float dist = Mathf.Min(i * sampleInterval, totalLen);
            float t = dist / totalLen;
            
            SplinePath.SplineSample sample = splinePath.SampleAtDistance(dist);
            Vector3 pos = sample.position;
            Vector3 right = sample.right;
            
            // Get terrain height at left and right edges
            Vector3 leftPos = pos - right * halfWidth;
            Vector3 rightPos = pos + right * halfWidth;
            
            if (terrain != null)
            {
                leftPos.y = terrain.SampleHeight(leftPos) + yOffset;
                rightPos.y = terrain.SampleHeight(rightPos) + yOffset;
            }
            else
            {
                leftPos.y = pos.y + yOffset;
                rightPos.y = pos.y + yOffset;
            }
            
            // Convert to local space
            leftPos = transform.InverseTransformPoint(leftPos);
            rightPos = transform.InverseTransformPoint(rightPos);
            
            verts.Add(leftPos);
            verts.Add(rightPos);
            
            uvs.Add(new Vector2(0f, t * (totalLen / width)));
            uvs.Add(new Vector2(1f, t * (totalLen / width)));
            
            if (i > 0)
            {
                int idx = (i - 1) * 2;
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
                tris.Add(idx + 1); tris.Add(idx + 2); tris.Add(idx + 3);
            }
        }
        
        Mesh mesh = new Mesh();
        mesh.name = "RoadMesh";
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void BuildCenterLine()
    {
        if (centerLineObj != null) Destroy(centerLineObj);
        
        centerLineObj = new GameObject("CenterLine");
        centerLineObj.transform.SetParent(transform, false);
        
        MeshFilter mf = centerLineObj.AddComponent<MeshFilter>();
        MeshRenderer mr = centerLineObj.AddComponent<MeshRenderer>();
        
        Material lineMat = CreateCompatibleMaterial(Color.white);
        mr.material = lineMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        
        // Build dashed center line
        float totalLen = splinePath.GetTotalLength();
        float dashCycle = centerLineDashLength + centerLineGapLength;
        
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        float halfW = centerLineWidth * 0.5f;
        float lineY = heightOffset + 0.02f;
        
        for (float dist = 0f; dist < totalLen; dist += sampleInterval * 0.5f)
        {
            float posInCycle = dist % dashCycle;
            if (posInCycle > centerLineDashLength) continue; // gap
            
            SplinePath.SplineSample sample = splinePath.SampleAtDistance(dist);
            Vector3 pos = sample.position;
            Vector3 right = sample.right;
            
            if (terrain != null) pos.y = terrain.SampleHeight(pos) + lineY;
            else pos.y += lineY;
            
            Vector3 left = transform.InverseTransformPoint(pos - right * halfW);
            Vector3 rt = transform.InverseTransformPoint(pos + right * halfW);
            
            int idx = verts.Count;
            verts.Add(left);
            verts.Add(rt);
            
            if (idx >= 2 && posInCycle > sampleInterval * 0.3f)
            {
                tris.Add(idx - 2); tris.Add(idx); tris.Add(idx - 1);
                tris.Add(idx - 1); tris.Add(idx); tris.Add(idx + 1);
            }
        }
        
        Mesh mesh = new Mesh();
        mesh.name = "CenterLine";
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mf.mesh = mesh;
    }
    
    private void BuildEdgeLines()
    {
        BuildEdgeLine("LeftEdge", -roadWidth * 0.5f + edgeLineWidth, ref leftEdgeObj);
        BuildEdgeLine("RightEdge", roadWidth * 0.5f - edgeLineWidth, ref rightEdgeObj);
    }
    
    private void BuildEdgeLine(string name, float lateralOffset, ref GameObject lineObj)
    {
        if (lineObj != null) Destroy(lineObj);
        
        lineObj = new GameObject(name);
        lineObj.transform.SetParent(transform, false);
        
        MeshFilter mf = lineObj.AddComponent<MeshFilter>();
        MeshRenderer mr = lineObj.AddComponent<MeshRenderer>();
        
        Material lineMat = CreateCompatibleMaterial(Color.white);
        mr.material = lineMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        
        float totalLen = splinePath.GetTotalLength();
        float halfW = edgeLineWidth * 0.5f;
        float lineY = heightOffset + 0.02f;
        
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        
        for (float dist = 0f; dist <= totalLen; dist += sampleInterval)
        {
            SplinePath.SplineSample sample = splinePath.SampleAtDistance(dist);
            Vector3 center = sample.position + sample.right * lateralOffset;
            
            if (terrain != null) center.y = terrain.SampleHeight(center) + lineY;
            else center.y += lineY;
            
            Vector3 left = transform.InverseTransformPoint(center - sample.right * halfW);
            Vector3 right = transform.InverseTransformPoint(center + sample.right * halfW);
            
            int idx = verts.Count;
            verts.Add(left);
            verts.Add(right);
            
            if (idx >= 2)
            {
                tris.Add(idx - 2); tris.Add(idx); tris.Add(idx - 1);
                tris.Add(idx - 1); tris.Add(idx); tris.Add(idx + 1);
            }
        }
        
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mf.mesh = mesh;
    }
    
    private void OnDestroy()
    {
        if (centerLineObj != null) Destroy(centerLineObj);
        if (leftEdgeObj != null) Destroy(leftEdgeObj);
        if (rightEdgeObj != null) Destroy(rightEdgeObj);
    }
}
