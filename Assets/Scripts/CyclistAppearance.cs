using UnityEngine;

/// <summary>
/// Colors the cyclist model with jersey, shorts, helmet, skin, and bike colors.
/// Auto-finds body parts by name in the hierarchy and assigns colored materials.
/// 
/// SETUP: Add to the biker root GameObject (the one with the Animator).
/// Adjust colors in the Inspector. Press Play.
/// </summary>
public class CyclistAppearance : MonoBehaviour
{
    [Header("Jersey")]
    [SerializeField] private Color jerseyColor = new Color(0.0f, 0.45f, 0.8f); // Blue
    [SerializeField] private Color jerseyAccent = new Color(1f, 0.3f, 0.1f); // Orange accent
    
    [Header("Shorts")]
    [SerializeField] private Color shortsColor = new Color(0.08f, 0.08f, 0.1f); // Black bib shorts
    
    [Header("Helmet")]
    [SerializeField] private Color helmetColor = Color.white;
    
    [Header("Skin")]
    [SerializeField] private Color skinColor = new Color(0.9f, 0.75f, 0.6f); // Light skin tone
    
    [Header("Shoes & Gloves")]
    [SerializeField] private Color shoeColor = new Color(0.15f, 0.15f, 0.15f); // Dark
    
    [Header("Bike")]
    [SerializeField] private Color bikeFrameColor = new Color(0.1f, 0.1f, 0.12f); // Dark frame
    [SerializeField] private Color bikeWheelColor = new Color(0.2f, 0.2f, 0.22f); // Dark wheels
    [SerializeField] private float bikeMetallic = 0.6f;
    
    [Header("Options")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool colorBike = true;
    
    private Material cachedBaseMaterial;

    private void Start()
    {
        if (applyOnStart) ApplyAppearance();
    }
    
    [ContextMenu("Apply Appearance")]
    public void ApplyAppearance()
    {
        // Cache a working material from a primitive
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cachedBaseMaterial = new Material(temp.GetComponent<Renderer>().sharedMaterial);
        Destroy(temp);
        
        // Find and color all renderers in the hierarchy
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        
        int colored = 0;
        foreach (Renderer rend in renderers)
        {
            string name = rend.gameObject.name.ToLower();
            
            if (name.Contains("head"))
            {
                // Head mesh — could be helmet or face
                // Use a split: white helmet on top, skin below
                SetColor(rend, helmetColor, 0.3f);
                colored++;
            }
            else if (name.Contains("rider") || name.Contains("biker") || name.Contains("body") || name.Contains("character"))
            {
                // Single body mesh — create a gradient texture: jersey top, shorts bottom
                ApplyBodyTexture(rend);
                colored++;
            }
            else if (name.Contains("frame") || name.Contains("handlebar") || name.Contains("seat") || name.Contains("saddle"))
            {
                if (colorBike) SetColor(rend, bikeFrameColor, bikeMetallic);
                colored++;
            }
            else if (name.Contains("wheel") || name.Contains("tire") || name.Contains("spoke"))
            {
                if (colorBike) SetColor(rend, bikeWheelColor, 0.3f);
                colored++;
            }
            else if (name.Contains("pedal"))
            {
                if (colorBike) SetColor(rend, new Color(0.15f, 0.15f, 0.15f), 0.4f);
                colored++;
            }
        }
        
        Debug.Log($"[CyclistAppearance] Applied colors to {colored}/{renderers.Length} renderers");
    }
    
    /// <summary>
    /// Apply a procedural texture to the body mesh with jersey on top, shorts on bottom, skin for limbs.
    /// </summary>
    private void ApplyBodyTexture(Renderer rend)
    {
        // Generate a simple body texture
        int texSize = 128;
        Texture2D bodyTex = new Texture2D(texSize, texSize, TextureFormat.RGB24, false);
        bodyTex.name = "CyclistBody";
        
        for (int y = 0; y < texSize; y++)
        {
            float t = (float)y / texSize;
            Color rowColor;
            
            if (t < 0.15f)
            {
                // Bottom: shoes
                rowColor = shoeColor;
            }
            else if (t < 0.45f)
            {
                // Lower body: bib shorts
                rowColor = shortsColor;
            }
            else if (t < 0.5f)
            {
                // Transition
                rowColor = Color.Lerp(shortsColor, jerseyColor, (t - 0.45f) / 0.05f);
            }
            else if (t < 0.85f)
            {
                // Upper body: jersey
                rowColor = jerseyColor;
            }
            else
            {
                // Neck/skin
                rowColor = skinColor;
            }
            
            for (int x = 0; x < texSize; x++)
            {
                // Add slight horizontal variation for arms (skin on edges)
                float xNorm = (float)x / texSize;
                if (t > 0.55f && t < 0.85f && (xNorm < 0.15f || xNorm > 0.85f))
                {
                    rowColor = skinColor; // Arms
                }
                
                bodyTex.SetPixel(x, y, rowColor);
            }
        }
        
        bodyTex.Apply();
        bodyTex.filterMode = FilterMode.Bilinear;
        
        Material mat = new Material(cachedBaseMaterial);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", bodyTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", bodyTex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
        
        rend.material = mat;
    }

    private void SetColor(Renderer rend, Color color, float smoothness)
    {
        // Create a new material instance based on the working shader
        Material mat = new Material(cachedBaseMaterial);
        
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        
        rend.material = mat;
    }
    
    // Body part detection kept for reference but no longer used for the single-mesh model
    
    /// <summary>
    /// List all renderers and their names for debugging.
    /// Use this to see what names your model uses.
    /// </summary>
    [ContextMenu("List All Renderers")]
    public void ListAllRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Debug.Log($"[CyclistAppearance] Found {renderers.Length} renderers:");
        foreach (Renderer r in renderers)
        {
            string path = GetHierarchyPath(r.transform);
            Debug.Log($"  {path} — Material: {r.sharedMaterial?.name}, Shader: {r.sharedMaterial?.shader?.name}");
        }
    }
    
    private string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        Transform parent = t.parent;
        int depth = 0;
        while (parent != null && parent != transform && depth < 5)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
            depth++;
        }
        return path;
    }
}
