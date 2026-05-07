using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Improved terrain generator with more spontaneous and varied terrain
/// Uses Perlin noise, multiple octaves, and realistic terrain features
/// Creates more natural-looking routes with varied climbs, descents, and rolling sections
/// </summary>
public class ImprovedTerrainGenerator : MonoBehaviour
{
    [Header("Terrain References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private BikeController bikeController;
    
    [Header("Route Settings")]
    [SerializeField] private float routeLength = 500f;
    [SerializeField] private float roadWidth = 10f;
    
    [Header("Terrain Style")]
    [Tooltip("Terrain profile type")]
    [SerializeField] private TerrainProfile terrainProfile = TerrainProfile.Mixed;
    
    public enum TerrainProfile
    {
        Flat,           // Mostly flat with small undulations
        Rolling,        // Gentle rolling hills
        Hilly,          // Moderate hills with varied climbs
        Mountainous,    // Steep climbs and descents
        Mixed,          // Random mix of all types
        Custom          // Use custom settings below
    }
    
    [Header("Perlin Noise Settings")]
    [Tooltip("Base frequency of terrain features")]
    [SerializeField] private float baseFrequency = 0.01f;
    
    [Tooltip("Number of noise octaves (more = more detail)")]
    [Range(1, 6)]
    [SerializeField] private int octaves = 4;
    
    [Tooltip("Amplitude multiplier for each octave")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float persistence = 0.5f;
    
    [Tooltip("Frequency multiplier for each octave")]
    [Range(1.5f, 3f)]
    [SerializeField] private float lacunarity = 2f;
    
    [Header("Elevation Settings")]
    [Tooltip("Base elevation in meters")]
    [SerializeField] private float baseElevation = 50f;
    
    [Tooltip("Maximum elevation change from base")]
    [SerializeField] private float maxElevationChange = 40f;
    
    [Tooltip("Overall slope bias (-1 to 1, negative = downhill, positive = uphill)")]
    [Range(-1f, 1f)]
    [SerializeField] private float slopeBias = 0f;
    
    [Header("Feature Settings")]
    [Tooltip("Add random steep sections")]
    [SerializeField] private bool addSteepSections = true;
    
    [Tooltip("Number of steep sections")]
    [Range(0, 5)]
    [SerializeField] private int steepSectionCount = 2;
    
    [Tooltip("Add flat recovery sections")]
    [SerializeField] private bool addRecoverySections = true;
    
    [Tooltip("Number of recovery sections")]
    [Range(0, 5)]
    [SerializeField] private int recoverySectionCount = 2;
    
    [Header("Realism Settings")]
    [Tooltip("Smooth transitions between sections")]
    [SerializeField] private bool smoothTransitions = true;
    
    [Tooltip("Smoothing passes")]
    [Range(1, 10)]
    [SerializeField] private int smoothingPasses = 3;
    
    [Tooltip("Constrain maximum gradient")]
    [SerializeField] private bool constrainGradient = true;
    
    [Tooltip("Maximum allowed gradient (%)")]
    [Range(5f, 25f)]
    [SerializeField] private float maxGradient = 15f;
    
    [Header("Randomization")]
    [Tooltip("Use random seed")]
    [SerializeField] private bool useRandomSeed = true;
    
    [Tooltip("Fixed seed (if not random)")]
    [SerializeField] private int seed = 12345;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool logDetailedStats = true;
    
    private float[] elevationProfile;
    private float[] gradientProfile;
    private int currentSeed;

    private void Start()
    {
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
    }
    
    [ContextMenu("Generate Terrain")]
    public void GenerateTerrain()
    {
        if (terrain == null)
        {
            Debug.LogError("[ImprovedTerrainGenerator] Terrain not assigned!");
            return;
        }
        
        // Set seed
        currentSeed = useRandomSeed ? Random.Range(0, 999999) : seed;
        Random.InitState(currentSeed);
        
        // Apply terrain profile presets
        ApplyTerrainProfilePreset();
        
        // Generate elevation profile
        GenerateElevationProfile();
        
        // Apply to terrain
        ApplyToTerrain();
        
        // Notify other systems
        NotifyTerrainChanged();
        
        if (showDebugInfo)
        {
            Debug.Log($"[ImprovedTerrainGenerator] Terrain generated with seed: {currentSeed}");
            Debug.Log($"[ImprovedTerrainGenerator] Profile: {terrainProfile}");
        }
        
        if (logDetailedStats)
        {
            LogTerrainStatistics();
        }
    }
    
    private void ApplyTerrainProfilePreset()
    {
        switch (terrainProfile)
        {
            case TerrainProfile.Flat:
                baseFrequency = 0.005f;
                octaves = 2;
                maxElevationChange = 10f;
                steepSectionCount = 0;
                recoverySectionCount = 1;
                break;
                
            case TerrainProfile.Rolling:
                baseFrequency = 0.01f;
                octaves = 3;
                maxElevationChange = 20f;
                steepSectionCount = 1;
                recoverySectionCount = 2;
                break;
                
            case TerrainProfile.Hilly:
                baseFrequency = 0.015f;
                octaves = 4;
                maxElevationChange = 35f;
                steepSectionCount = 2;
                recoverySectionCount = 1;
                break;
                
            case TerrainProfile.Mountainous:
                baseFrequency = 0.02f;
                octaves = 5;
                maxElevationChange = 50f;
                steepSectionCount = 3;
                recoverySectionCount = 1;
                break;
                
            case TerrainProfile.Mixed:
                // Randomize settings
                baseFrequency = Random.Range(0.008f, 0.02f);
                octaves = Random.Range(3, 5);
                maxElevationChange = Random.Range(20f, 45f);
                steepSectionCount = Random.Range(1, 4);
                recoverySectionCount = Random.Range(1, 3);
                slopeBias = Random.Range(-0.3f, 0.3f);
                break;
                
            case TerrainProfile.Custom:
                // Use inspector settings
                break;
        }
    }

    private void GenerateElevationProfile()
    {
        int samples = Mathf.CeilToInt(routeLength);
        elevationProfile = new float[samples];
        
        // Generate base terrain using multi-octave Perlin noise
        for (int i = 0; i < samples; i++)
        {
            float elevation = baseElevation;
            float amplitude = maxElevationChange;
            float frequency = baseFrequency;
            
            // Multi-octave noise
            for (int octave = 0; octave < octaves; octave++)
            {
                float sampleX = i * frequency + (currentSeed * 0.1f);
                float noise = Mathf.PerlinNoise(sampleX, octave * 10f);
                elevation += (noise - 0.5f) * 2f * amplitude;
                
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            
            // Apply slope bias
            float t = (float)i / (samples - 1);
            elevation += slopeBias * maxElevationChange * t;
            
            elevationProfile[i] = elevation;
        }
        
        // Add steep sections
        if (addSteepSections && steepSectionCount > 0)
        {
            AddSteepSections(samples);
        }
        
        // Add recovery sections
        if (addRecoverySections && recoverySectionCount > 0)
        {
            AddRecoverySections(samples);
        }
        
        // Smooth transitions
        if (smoothTransitions)
        {
            for (int pass = 0; pass < smoothingPasses; pass++)
            {
                elevationProfile = SmoothProfile(elevationProfile);
            }
        }
        
        // Constrain gradients
        if (constrainGradient)
        {
            ConstrainGradients();
        }
        
        // Calculate gradient profile
        CalculateGradients();
    }
    
    private void AddSteepSections(int samples)
    {
        for (int i = 0; i < steepSectionCount; i++)
        {
            // Random position (avoid start and end)
            int startIdx = Random.Range(samples / 10, samples * 9 / 10);
            int length = Random.Range(samples / 20, samples / 10);
            
            // Random steepness
            float steepness = Random.Range(15f, 30f);
            bool isClimb = Random.value > 0.3f; // 70% chance of climb
            
            if (!isClimb) steepness *= -1f;
            
            // Apply steep section
            for (int j = 0; j < length && startIdx + j < samples; j++)
            {
                float t = (float)j / length;
                float ramp = Mathf.Sin(t * Mathf.PI); // Smooth ramp up and down
                elevationProfile[startIdx + j] += steepness * ramp;
            }
        }
    }
    
    private void AddRecoverySections(int samples)
    {
        for (int i = 0; i < recoverySectionCount; i++)
        {
            int startIdx = Random.Range(samples / 10, samples * 9 / 10);
            int length = Random.Range(samples / 15, samples / 8);
            
            // Flatten this section
            if (startIdx > 0 && startIdx + length < samples)
            {
                float startElev = elevationProfile[startIdx];
                float endElev = elevationProfile[startIdx + length];
                
                for (int j = 0; j < length; j++)
                {
                    float t = (float)j / length;
                    elevationProfile[startIdx + j] = Mathf.Lerp(startElev, endElev, t);
                }
            }
        }
    }

    private float[] SmoothProfile(float[] profile)
    {
        float[] smoothed = new float[profile.Length];
        int windowSize = 3;
        
        for (int i = 0; i < profile.Length; i++)
        {
            float sum = 0f;
            int count = 0;
            
            for (int j = -windowSize; j <= windowSize; j++)
            {
                int idx = i + j;
                if (idx >= 0 && idx < profile.Length)
                {
                    sum += profile[idx];
                    count++;
                }
            }
            
            smoothed[i] = sum / count;
        }
        
        return smoothed;
    }
    
    private void ConstrainGradients()
    {
        float sampleDistance = routeLength / (elevationProfile.Length - 1);
        
        for (int i = 1; i < elevationProfile.Length; i++)
        {
            float heightDiff = elevationProfile[i] - elevationProfile[i - 1];
            float gradient = (heightDiff / sampleDistance) * 100f;
            
            if (Mathf.Abs(gradient) > maxGradient)
            {
                float maxHeightDiff = (maxGradient / 100f) * sampleDistance;
                if (gradient < 0) maxHeightDiff *= -1f;
                elevationProfile[i] = elevationProfile[i - 1] + maxHeightDiff;
            }
        }
    }
    
    private void CalculateGradients()
    {
        gradientProfile = new float[elevationProfile.Length];
        float sampleDistance = routeLength / (elevationProfile.Length - 1);
        
        for (int i = 1; i < elevationProfile.Length; i++)
        {
            float heightDiff = elevationProfile[i] - elevationProfile[i - 1];
            gradientProfile[i] = (heightDiff / sampleDistance) * 100f;
        }
        
        gradientProfile[0] = gradientProfile[1];
    }
    
    private void ApplyToTerrain()
    {
        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
        
        Vector3 bikePos = bikeController != null ? bikeController.transform.position : Vector3.zero;
        float roadZ = bikePos.z;
        
        int roadCenterZ = Mathf.RoundToInt((roadZ / terrainData.size.z) * resolution);
        int roadWidthSamples = Mathf.CeilToInt((roadWidth / terrainData.size.z) * resolution);
        
        // Apply elevation to the ENTIRE terrain width (not just road strip)
        // This ensures the generated terrain is visible from all camera angles
        for (int x = 0; x < resolution; x++)
        {
            float t = (float)x / (resolution - 1);
            int profileIdx = Mathf.FloorToInt(t * (elevationProfile.Length - 1));
            profileIdx = Mathf.Clamp(profileIdx, 0, elevationProfile.Length - 1);
            
            float elevation = elevationProfile[profileIdx];
            float normalizedHeight = elevation / terrainData.size.y;
            
            for (int z = 0; z < resolution; z++)
            {
                float distFromRoadCenter = Mathf.Abs(z - roadCenterZ);
                
                if (distFromRoadCenter <= roadWidthSamples)
                {
                    // On the road - use exact elevation
                    heights[z, x] = normalizedHeight;
                }
                else
                {
                    // Off-road - blend between road elevation and some variation
                    float blendDistance = roadWidthSamples * 3f; // Gradual blend zone
                    float blendFactor = Mathf.Clamp01((distFromRoadCenter - roadWidthSamples) / blendDistance);
                    
                    // Add slight variation off-road for natural look
                    float offRoadNoise = Mathf.PerlinNoise(x * 0.02f + currentSeed, z * 0.02f) * 0.02f;
                    float offRoadHeight = normalizedHeight + offRoadNoise;
                    
                    heights[z, x] = Mathf.Lerp(normalizedHeight, offRoadHeight, blendFactor);
                }
            }
        }
        
        terrainData.SetHeights(0, 0, heights);
    }

    private void NotifyTerrainChanged()
    {
        TerrainSyncManager syncManager = FindObjectOfType<TerrainSyncManager>();
        if (syncManager != null)
        {
            syncManager.SynchronizeAfterTerrainGeneration();
        }
        else
        {
            ElevationProgressBar progressBar = FindObjectOfType<ElevationProgressBar>();
            if (progressBar != null)
            {
                progressBar.SetTotalRouteLength(routeLength);
            }
        }
    }
    
    private void LogTerrainStatistics()
    {
        if (elevationProfile == null || gradientProfile == null) return;
        
        float minElev = float.MaxValue;
        float maxElev = float.MinValue;
        float totalClimb = 0f;
        float totalDescent = 0f;
        float minGrad = float.MaxValue;
        float maxGrad = float.MinValue;
        float avgGrad = 0f;
        
        for (int i = 0; i < elevationProfile.Length; i++)
        {
            float elev = elevationProfile[i];
            if (elev < minElev) minElev = elev;
            if (elev > maxElev) maxElev = elev;
            
            if (i > 0)
            {
                float diff = elevationProfile[i] - elevationProfile[i - 1];
                if (diff > 0) totalClimb += diff;
                if (diff < 0) totalDescent += Mathf.Abs(diff);
            }
            
            if (i < gradientProfile.Length)
            {
                float grad = gradientProfile[i];
                if (grad < minGrad) minGrad = grad;
                if (grad > maxGrad) maxGrad = grad;
                avgGrad += grad;
            }
        }
        
        avgGrad /= gradientProfile.Length;
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"IMPROVED TERRAIN STATISTICS");
        Debug.Log($"{'='*60}");
        Debug.Log($"Profile: {terrainProfile}");
        Debug.Log($"Seed: {currentSeed}");
        Debug.Log($"\nElevation:");
        Debug.Log($"  Min: {minElev:F1}m");
        Debug.Log($"  Max: {maxElev:F1}m");
        Debug.Log($"  Range: {maxElev - minElev:F1}m");
        Debug.Log($"  Total Climb: {totalClimb:F1}m");
        Debug.Log($"  Total Descent: {totalDescent:F1}m");
        Debug.Log($"\nGradient:");
        Debug.Log($"  Min: {minGrad:F2}%");
        Debug.Log($"  Max: {maxGrad:F2}%");
        Debug.Log($"  Average: {avgGrad:F2}%");
        Debug.Log($"\nFeatures:");
        Debug.Log($"  Steep Sections: {steepSectionCount}");
        Debug.Log($"  Recovery Sections: {recoverySectionCount}");
        Debug.Log($"  Octaves: {octaves}");
        Debug.Log($"{'='*60}\n");
    }
    
    public float GetRouteLength()
    {
        return routeLength;
    }
    
    public int GetCurrentSeed()
    {
        return currentSeed;
    }
}
