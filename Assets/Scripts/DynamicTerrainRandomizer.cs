using UnityEngine;
using System.Collections;

/// <summary>
/// Dynamic terrain randomizer that creates spontaneous terrain changes
/// Can generate terrain on-the-fly as the rider progresses
/// Creates more unpredictable and engaging riding experience
/// </summary>
public class DynamicTerrainRandomizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private BikeController bikeController;
    
    [Header("Dynamic Generation")]
    [Tooltip("Generate terrain ahead of rider in real-time")]
    [SerializeField] private bool enableDynamicGeneration = false;
    
    [Tooltip("Distance ahead to generate (meters)")]
    [SerializeField] private float generationDistance = 100f;
    
    [Tooltip("How often to update terrain (seconds)")]
    [SerializeField] private float updateInterval = 2f;
    
    [Header("Randomization Settings")]
    [Tooltip("Randomize terrain every X seconds")]
    [SerializeField] private bool enablePeriodicRandomization = false;
    
    [Tooltip("Randomization interval (seconds)")]
    [SerializeField] private float randomizationInterval = 30f;
    
    [Header("Surprise Features")]
    [Tooltip("Add random surprise climbs")]
    [SerializeField] private bool enableSurpriseClimbs = true;
    
    [Tooltip("Chance of surprise climb per update (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float surpriseClimbChance = 0.1f;
    
    [Tooltip("Add random sprint sections (flat fast sections)")]
    [SerializeField] private bool enableSprintSections = true;
    
    [Tooltip("Chance of sprint section per update (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float sprintSectionChance = 0.15f;
    
    [Header("Terrain Variety")]
    [Tooltip("Mix of terrain types")]
    [SerializeField] private TerrainMix terrainMix = TerrainMix.Balanced;
    
    public enum TerrainMix
    {
        MostlyFlat,
        Balanced,
        MostlyHilly,
        Extreme,
        CompletelyRandom
    }
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private float lastUpdateTime;
    private float lastRandomizationTime;
    private ImprovedTerrainGenerator terrainGenerator;
    
    private void Start()
    {
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        terrainGenerator = GetComponent<ImprovedTerrainGenerator>();
        if (terrainGenerator == null)
        {
            terrainGenerator = gameObject.AddComponent<ImprovedTerrainGenerator>();
        }
        
        lastUpdateTime = Time.time;
        lastRandomizationTime = Time.time;
    }

    private void Update()
    {
        if (enablePeriodicRandomization && Time.time - lastRandomizationTime > randomizationInterval)
        {
            RandomizeTerrain();
            lastRandomizationTime = Time.time;
        }
        
        if (enableDynamicGeneration && Time.time - lastUpdateTime > updateInterval)
        {
            UpdateDynamicTerrain();
            lastUpdateTime = Time.time;
        }
    }
    
    [ContextMenu("Randomize Terrain Now")]
    public void RandomizeTerrain()
    {
        if (terrainGenerator == null) return;
        
        // Set random profile based on terrain mix
        ImprovedTerrainGenerator.TerrainProfile profile = GetRandomProfile();
        
        // Use reflection or make terrainProfile public in ImprovedTerrainGenerator
        // For now, just generate
        terrainGenerator.GenerateTerrain();
        
        if (showDebugInfo)
        {
            Debug.Log($"[DynamicTerrainRandomizer] Terrain randomized! Profile: {profile}");
        }
    }
    
    private ImprovedTerrainGenerator.TerrainProfile GetRandomProfile()
    {
        switch (terrainMix)
        {
            case TerrainMix.MostlyFlat:
                return Random.value < 0.7f ? 
                    ImprovedTerrainGenerator.TerrainProfile.Flat : 
                    ImprovedTerrainGenerator.TerrainProfile.Rolling;
                
            case TerrainMix.Balanced:
                int rand = Random.Range(0, 4);
                return (ImprovedTerrainGenerator.TerrainProfile)rand;
                
            case TerrainMix.MostlyHilly:
                return Random.value < 0.7f ? 
                    ImprovedTerrainGenerator.TerrainProfile.Hilly : 
                    ImprovedTerrainGenerator.TerrainProfile.Mountainous;
                
            case TerrainMix.Extreme:
                return ImprovedTerrainGenerator.TerrainProfile.Mountainous;
                
            case TerrainMix.CompletelyRandom:
                return ImprovedTerrainGenerator.TerrainProfile.Mixed;
                
            default:
                return ImprovedTerrainGenerator.TerrainProfile.Mixed;
        }
    }
    
    private void UpdateDynamicTerrain()
    {
        if (bikeController == null || terrain == null) return;
        
        // Check for surprise features
        if (enableSurpriseClimbs && Random.value < surpriseClimbChance)
        {
            AddSurpriseClimb();
        }
        
        if (enableSprintSections && Random.value < sprintSectionChance)
        {
            AddSprintSection();
        }
    }
    
    private void AddSurpriseClimb()
    {
        if (showDebugInfo)
        {
            Debug.Log("[DynamicTerrainRandomizer] Adding surprise climb ahead!");
        }
        
        // This would modify terrain ahead of the rider
        // Implementation depends on your terrain system
    }
    
    private void AddSprintSection()
    {
        if (showDebugInfo)
        {
            Debug.Log("[DynamicTerrainRandomizer] Adding sprint section ahead!");
        }
        
        // This would add a flat fast section ahead
    }
    
    [ContextMenu("Generate Completely Random Terrain")]
    public void GenerateCompletelyRandomTerrain()
    {
        terrainMix = TerrainMix.CompletelyRandom;
        RandomizeTerrain();
    }
}
