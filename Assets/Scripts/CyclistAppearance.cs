using UnityEngine;

/// <summary>
/// Resets the cyclist model to plain white on Start.
/// Ensures no leftover colored materials from previous runs.
/// </summary>
public class CyclistAppearance : MonoBehaviour
{
    private void Start()
    {
        // Reset all renderers on the cyclist to plain white
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Material whiteMat = new Material(temp.GetComponent<Renderer>().sharedMaterial);
        Destroy(temp);

        Color white = Color.white;
        if (whiteMat.HasProperty("_BaseColor")) whiteMat.SetColor("_BaseColor", white);
        if (whiteMat.HasProperty("_Color"))     whiteMat.SetColor("_Color", white);
        if (whiteMat.HasProperty("_Smoothness")) whiteMat.SetFloat("_Smoothness", 0.1f);
        if (whiteMat.HasProperty("_Metallic"))  whiteMat.SetFloat("_Metallic", 0f);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            // Apply white to all materials on this renderer
            Material[] mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = new Material(whiteMat);
            }
            rend.materials = mats;
        }

        Debug.Log($"[CyclistAppearance] Reset {renderers.Length} renderers to white");
    }
}
