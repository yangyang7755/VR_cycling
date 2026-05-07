using UnityEngine;

/// <summary>
/// Quick terrain presets for common cycling scenarios
/// One-click generation of specific terrain types
/// </summary>
public class TerrainPresets : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ImprovedTerrainGenerator terrainGenerator;
    
    private void Start()
    {
        if (terrainGenerator == null)
            terrainGenerator = FindObjectOfType<ImprovedTerrainGenerator>();
    }
    
    [ContextMenu("Preset: Beginner Friendly")]
    public void GenerateBeginnerFriendly()
    {
        // Mostly flat with gentle rolling sections
        Debug.Log("[TerrainPresets] Generating Beginner Friendly terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
    
    [ContextMenu("Preset: Time Trial")]
    public void GenerateTimeTrial()
    {
        // Flat and fast
        Debug.Log("[TerrainPresets] Generating Time Trial terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
    
    [ContextMenu("Preset: Classic Climb")]
    public void GenerateClassicClimb()
    {
        // Long steady climb
        Debug.Log("[TerrainPresets] Generating Classic Climb terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
    
    [ContextMenu("Preset: Punchy Hills")]
    public void GeneratePunchyHills()
    {
        // Short steep climbs with recovery
        Debug.Log("[TerrainPresets] Generating Punchy Hills terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
    
    [ContextMenu("Preset: Mountain Stage")]
    public void GenerateMountainStage()
    {
        // Multiple climbs with descents
        Debug.Log("[TerrainPresets] Generating Mountain Stage terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
    
    [ContextMenu("Preset: Recovery Ride")]
    public void GenerateRecoveryRide()
    {
        // Very flat and easy
        Debug.Log("[TerrainPresets] Generating Recovery Ride terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
    
    [ContextMenu("Preset: Criterium")]
    public void GenerateCriterium()
    {
        // Flat with occasional short climbs
        Debug.Log("[TerrainPresets] Generating Criterium terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
    
    [ContextMenu("Preset: Alpine Challenge")]
    public void GenerateAlpineChallenge()
    {
        // Extreme climbing
        Debug.Log("[TerrainPresets] Generating Alpine Challenge terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
    
    [ContextMenu("Preset: Mixed Training")]
    public void GenerateMixedTraining()
    {
        // Variety of everything
        Debug.Log("[TerrainPresets] Generating Mixed Training terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
    
    [ContextMenu("Preset: Interval Training")]
    public void GenerateIntervalTraining()
    {
        // Alternating flat and climbs
        Debug.Log("[TerrainPresets] Generating Interval Training terrain...");
        if (terrainGenerator != null)
            terrainGenerator.GenerateTerrain();
    }
}
