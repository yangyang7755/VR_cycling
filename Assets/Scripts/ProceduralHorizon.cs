using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Creates a procedural mountain horizon around the terrain.
/// Generates a ring of mountain silhouettes at the terrain edges,
/// randomized each time for variety. Gives depth and a sense of place.
/// 
/// SETUP: Add to any GameObject. Auto-finds terrain. Runs on Start.
/// </summary>
public class ProceduralHorizon : MonoBehaviour
{
    [Header("Mountain Ring")]
    [Tooltip("Distance from terrain center to mountain ring")]
    [SerializeField] private float ringRadius = 800f;
    
    [Tooltip("Number of mountain peaks around the ring")]
    [SerializeField] private int peakCount = 36;
    
    [Tooltip("Minimum mountain height")]
    [SerializeField] private float minHeight = 120f;
    
    [Tooltip("Maximum mountain height")]
    [SerializeField] private float maxHeight = 400f;
    
    [Tooltip("Mountain base width")]
    [SerializeField] private float baseWidth = 200f;
    
    [Header("Appearance")]
    [SerializeField] private Color nearMountainColor = new Color(0.3f, 0.4f, 0.3f);
    [SerializeField] private Color farMountainColor = new Color(0.55f, 0.6f, 0.7f);
    [SerializeField] private Color snowCapColor = new Color(0.85f, 0.88f, 0.92f);
    [SerializeField] private float snowLineRatio = 0.7f; // snow above 70% of peak height
    
    [Header("Layers")]
    [Tooltip("Number of mountain layers (more = more depth)")]
    [SerializeField] private int layerCount = 4;
    
    [Tooltip("Layer spacing multiplier")]
    [SerializeField] private float layerSpacing = 1.2f;
    
    [Header("Randomization")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int seed = 42;
    
    private List<GameObject> mountainObjects = new List<GameObject>();
    private Transform mountainParent;

    private void Start()
    {
        // Delay to run after terrain is sculpted
        StartCoroutine(GenerateDelayed());
    }
    
    private System.Collections.IEnumerator GenerateDelayed()
    {
        yield return null;
        yield return null; // wait 2 frames for terrain to be ready
        GenerateHorizon();
    }
    
    [ContextMenu("Generate Horizon")]
    public void GenerateHorizon()
    {
        ClearHorizon();
        
        int currentSeed = useRandomSeed ? UnityEngine.Random.Range(0, 999999) : seed;
        UnityEngine.Random.InitState(currentSeed);
        
        mountainParent = new GameObject("MountainHorizon").transform;
        mountainParent.SetParent(transform);
        
        // Find terrain center
        Terrain terrain = FindObjectOfType<Terrain>();
        Vector3 center = Vector3.zero;
        float baseY = 0f;
        
        if (terrain != null)
        {
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = terrain.terrainData.size;
            center = tPos + new Vector3(tSize.x * 0.5f, 0f, tSize.z * 0.5f);
            baseY = terrain.transform.position.y; // use terrain base, not road elevation
        }
        
        // Generate mountain layers (back to front)
        for (int layer = layerCount - 1; layer >= 0; layer--)
        {
            float layerRadius = ringRadius * Mathf.Pow(layerSpacing, layer);
            float layerScale = 1f + layer * 0.4f; // farther layers are taller
            float layerAlpha = 1f - (float)layer / layerCount * 0.3f;
            
            Color layerColor = Color.Lerp(nearMountainColor, farMountainColor, (float)layer / layerCount);
            
            int layerPeaks = peakCount + layer * 4; // more peaks in distant layers
            
            GenerateMountainLayer(center, baseY, layerRadius, layerScale, layerColor, layerAlpha, layerPeaks, layer);
        }
        
        Debug.Log($"[ProceduralHorizon] Generated {layerCount} mountain layers, {mountainObjects.Count} peaks, seed={currentSeed}");
    }
    
    private void GenerateMountainLayer(Vector3 center, float baseY, float radius, float heightScale, Color color, float alpha, int peaks, int layerIndex)
    {
        // Get working shader from primitive
        GameObject tempPrim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Material baseMat = new Material(tempPrim.GetComponent<Renderer>().sharedMaterial);
        Destroy(tempPrim);
        
        for (int i = 0; i < peaks; i++)
        {
            float angle = ((float)i / peaks) * Mathf.PI * 2f;
            // Add randomness to angle
            angle += UnityEngine.Random.Range(-0.15f, 0.15f);
            
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            
            float peakHeight = UnityEngine.Random.Range(minHeight, maxHeight) * heightScale;
            float peakWidth = baseWidth * UnityEngine.Random.Range(0.6f, 1.4f) * heightScale;
            
            // Create mountain mesh
            GameObject mountain = CreateMountainMesh(peakHeight, peakWidth, color, baseMat);
            mountain.transform.SetParent(mountainParent);
            mountain.transform.position = new Vector3(x, baseY, z);
            
            // Face toward center
            Vector3 toCenter = center - mountain.transform.position;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.1f)
            {
                mountain.transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
            }
            
            // Random Y rotation variation
            mountain.transform.Rotate(0f, UnityEngine.Random.Range(-30f, 30f), 0f);
            
            mountainObjects.Add(mountain);
        }
    }
    
    private GameObject CreateMountainMesh(float height, float width, Color color, Material baseMat)
    {
        GameObject obj = new GameObject("Mountain");
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        float halfW = width * 0.5f;
        float depth = width * 0.5f; // deeper for more 3D look

        // Create a more detailed mountain with multiple ridges
        int segments = 8; // more segments = smoother silhouette
        List<Vector3> vertList = new List<Vector3>();
        List<int> triList = new List<int>();
        List<Color> colorList = new List<Color>();

        // Base ring
        for (int i = 0; i <= segments; i++)
        {
            float angle = ((float)i / segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * halfW;
            float z = Mathf.Sin(angle) * depth;
            vertList.Add(new Vector3(x, 0f, z));
            colorList.Add(color * 0.7f);
        }

        // Mid-ridge ring (60% height, slightly narrower)
        float midHeight = height * 0.55f;
        float midScale = 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float angle = ((float)i / segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * halfW * midScale;
            float z = Mathf.Sin(angle) * depth * midScale;
            // Add randomness for natural ridge
            x += UnityEngine.Random.Range(-halfW * 0.1f, halfW * 0.1f);
            z += UnityEngine.Random.Range(-depth * 0.1f, depth * 0.1f);
            float y = midHeight + UnityEngine.Random.Range(-height * 0.08f, height * 0.08f);
            vertList.Add(new Vector3(x, y, z));

            float hRatio = y / height;
            Color c = hRatio > snowLineRatio ? Color.Lerp(color, snowCapColor, (hRatio - snowLineRatio) / (1f - snowLineRatio)) : color * (0.8f + hRatio * 0.2f);
            c.a = 1f;
            colorList.Add(c);
        }

        // Peak (single point with slight offset)
        float peakX = UnityEngine.Random.Range(-halfW * 0.15f, halfW * 0.15f);
        float peakZ = UnityEngine.Random.Range(-depth * 0.15f, depth * 0.15f);
        vertList.Add(new Vector3(peakX, height, peakZ));
        colorList.Add(snowCapColor);

        int baseStart = 0;
        int midStart = segments + 1;
        int peakIdx = vertList.Count - 1;

        // Triangles: base to mid ring
        for (int i = 0; i < segments; i++)
        {
            triList.Add(baseStart + i);
            triList.Add(midStart + i);
            triList.Add(baseStart + i + 1);

            triList.Add(baseStart + i + 1);
            triList.Add(midStart + i);
            triList.Add(midStart + i + 1);
        }

        // Triangles: mid ring to peak
        for (int i = 0; i < segments; i++)
        {
            triList.Add(midStart + i);
            triList.Add(peakIdx);
            triList.Add(midStart + i + 1);
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertList.ToArray();
        mesh.triangles = triList.ToArray();
        mesh.colors = colorList.ToArray();
        mesh.RecalculateNormals();
        mf.mesh = mesh;

        Material mat = new Material(baseMat);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        mat.SetInt("_Cull", 0);
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return obj;
    }
    
    private void ClearHorizon()
    {
        foreach (GameObject obj in mountainObjects)
        {
            if (obj != null) Destroy(obj);
        }
        mountainObjects.Clear();
        
        if (mountainParent != null) Destroy(mountainParent.gameObject);
    }
    
    private void OnDestroy()
    {
        ClearHorizon();
    }
}
