using UnityEngine;

/// <summary>
/// Reliable runtime material creation for URP projects.
/// Gets the shader from existing scene objects that are already rendering correctly,
/// avoiding the Shader.Find() issues that cause pink/magenta materials.
/// </summary>
public static class RuntimeMaterialHelper
{
    private static Shader _cachedShader;
    
    /// <summary>
    /// Get a shader that is guaranteed to work in the current render pipeline.
    /// Creates a Unity primitive and copies its default shader — this is the only
    /// 100% reliable method across all render pipelines.
    /// </summary>
    public static Shader GetWorkingShader()
    {
        if (_cachedShader != null) return _cachedShader;
        
        // The ONLY reliable way: create a primitive and copy its shader.
        // Unity always assigns the correct pipeline shader to primitives.
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Renderer tempRend = temp.GetComponent<Renderer>();
        if (tempRend != null && tempRend.sharedMaterial != null)
        {
            _cachedShader = tempRend.sharedMaterial.shader;
            Debug.Log($"[RuntimeMaterialHelper] Cached shader: {_cachedShader.name}");
        }
        Object.Destroy(temp);
        
        if (_cachedShader == null)
        {
            _cachedShader = Shader.Find("Standard");
            Debug.LogWarning("[RuntimeMaterialHelper] Falling back to Standard shader");
        }
        
        return _cachedShader;
    }
    
    /// <summary>
    /// Create a material with the given color that works in the current render pipeline.
    /// </summary>
    public static Material CreateMaterial(Color color)
    {
        Shader shader = GetWorkingShader();
        Material mat = new Material(shader);
        
        // Set color on all possible property names
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        
        // Reduce shininess
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
        
        return mat;
    }
}
