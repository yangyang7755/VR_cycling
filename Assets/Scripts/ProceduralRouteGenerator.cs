using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates procedural cycling routes with specific average gradients
/// Maintains realistic undulations while achieving target average gradient
/// Perfect for experimental trials with randomized conditions
/// </summary>
public class ProceduralRouteGenerator : MonoBehaviour
{
    [Header("Route Settings")]
    [Tooltip("Length of the route in meters")]
    [SerializeField] private float routeLength = 500f;
    
    [Tooltip("Target average gradients to choose from (%)")]
    [SerializeField] private float[] targetGradients = { 0f, 3f, 5f, 7f, 10f, 15f };
    
    [Tooltip("Randomize gradient on Start")]
    [SerializeField] private bool randomizeOnStart = true;
    
    [Tooltip("Current target gradient (set manually or randomized)")]
    [SerializeField] private float currentTargetGradient = 0f;
    
    [Header("Terrain References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private BikeController bikeController;
    
    [Header("Undulation Settings")]
    [Tooltip("Number of undulations along the route")]
    [SerializeField] private int undulationCount = 8;
    
    [Tooltip("Amplitude of undulations in meters (height variation)")]
    [SerializeField] private float undulationAmplitude = 2f;
    
    [Tooltip("Randomness in undulation placement (0-1)")]
    [SerializeField] private float undulationRandomness = 0.3f;
    
    [Tooltip("Smoothness of terrain (higher = smoother)")]
    [SerializeField] private int smoothingPasses = 3;
    
    [Header("Terrain Constraints")]
    [Tooltip("Minimum local gradient (%)")]
    [SerializeField] private float minLocalGradient = -8f;
    
    [Tooltip("Maximum local gradient (%)")]
    [SerializeField] private float maxLocalGradient = 20f;
    
    [Tooltip("Road width in terrain units")]
    [SerializeField] private float roadWidth = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool logTerrainStats = true;
    
    private float actualAverageGradient = 0f;
    private float startElevation = 0f;
    private float endElevation = 0f;
    private int selectedGradientIndex = -1;

    private void Start()
    {
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (randomizeOnStart)
        {
            RandomizeAndGenerate();
        }
    }

    /// <summary>
    /// Randomize gradient and generate new route
    /// </summary>
    [ContextMenu("Randomize and Generate Route")]
    public void RandomizeAndGenerate()
    {
        if (targetGradients == null || targetGradients.Length == 0)
        {
            Debug.LogError("[RouteGenerator] No target gradients defined!");
            return;
        }
        
        // Randomly select a gradient
        selectedGradientIndex = Random.Range(0, targetGradients.Length);
        currentTargetGradient = targetGradients[selectedGradientIndex];
        
        if (showDebugInfo)
        {
            Debug.Log($"[RouteGenerator] Selected gradient: {currentTargetGradient}% (option {selectedGradientIndex + 1}/{targetGradients.Length})");
        }
        
        GenerateRoute();
    }

    /// <summary>
    /// Generate route with current target gradient
    /// </summary>
    [ContextMenu("Generate Route with Current Gradient")]
    public void GenerateRoute()
    {
        if (terrain == null)
        {
            Debug.LogError("[RouteGenerator] Terrain not assigned!");
            return;
        }
        
        GenerateTerrainProfile();
        
        if (showDebugInfo)
        {
            Debug.Log($"[RouteGenerator] Route generated!");
            Debug.Log($"  Target gradient: {currentTargetGradient}%");
            Debug.Log($"  Actual gradient: {actualAverageGradient:F2}%");
            Debug.Log($"  Start elevation: {startElevation:F1}m");
            Debug.Log($"  End elevation: {endElevation:F1}m");
            Debug.Log($"  Total climb: {endElevation - startElevation:F1}m");
        }
    }

    /// <summary>
    /// Generate terrain profile with target average gradient
    /// </summary>
    private void GenerateTerrainProfile()
    {
        TerrainData terrainData = terrain.terrainData;
        int heightmapResolution = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);
        
        // Calculate target elevation change
        float targetElevationChange = (currentTargetGradient / 100f) * routeLength;
        
        // Generate base elevation profile
        int routeSamples = Mathf.Min(heightmapResolution, Mathf.CeilToInt(routeLength));
        float[] elevationProfile = GenerateElevationProfile(routeSamples, targetElevationChange);
        
        // Apply to terrain heightmap
        ApplyProfileToTerrain(heights, elevationProfile, heightmapResolution);
        
        // Set the modified heights
        terrainData.SetHeights(0, 0, heights);
        
        // Calculate actual statistics
        CalculateTerrainStatistics(elevationProfile);
        
        if (logTerrainStats)
        {
            LogDetailedStatistics(elevationProfile);
        }
        
        // Notify elevation progress bar to resample
        NotifyTerrainChanged();
    }
    
    /// <summary>
    /// Notify other components that terrain has changed
    /// </summary>
    private void NotifyTerrainChanged()
    {
        // Use sync manager if available
        TerrainSyncManager syncManager = FindObjectOfType<TerrainSyncManager>();
        if (syncManager != null)
        {
            syncManager.SynchronizeAfterTerrainGeneration();
            if (showDebugInfo)
            {
                Debug.Log("[RouteGenerator] Triggered terrain synchronization");
            }
        }
        else
        {
            // Fallback: directly update elevation progress bar
            ElevationProgressBar progressBar = FindObjectOfType<ElevationProgressBar>();
            if (progressBar != null)
            {
                progressBar.SetTotalRouteLength(routeLength);
                if (showDebugInfo)
                {
                    Debug.Log("[RouteGenerator] Notified ElevationProgressBar to resample terrain");
                }
            }
        }
    }

    /// <summary>
    /// Generate elevation profile with undulations
    /// </summary>
    private float[] GenerateElevationProfile(int samples, float targetElevationChange)
    {
        float[] profile = new float[samples];
        
        // Start at a base elevation
        startElevation = 50f; // Arbitrary starting height
        
        // Generate base linear climb/descent
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            profile[i] = startElevation + (targetElevationChange * t);
        }
        
        // Add undulations using multiple sine waves
        for (int wave = 0; wave < undulationCount; wave++)
        {
            float frequency = Random.Range(1f, 3f);
            float amplitude = undulationAmplitude * Random.Range(0.5f, 1.5f);
            float phase = Random.Range(0f, Mathf.PI * 2f);
            
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1);
                float undulation = Mathf.Sin(t * Mathf.PI * 2f * frequency + phase) * amplitude;
                
                // Apply randomness
                undulation *= Random.Range(1f - undulationRandomness, 1f + undulationRandomness);
                
                profile[i] += undulation;
            }
        }
        
        // Add some noise for realism
        for (int i = 1; i < samples - 1; i++)
        {
            profile[i] += Random.Range(-0.5f, 0.5f);
        }
        
        // Smooth the profile
        for (int pass = 0; pass < smoothingPasses; pass++)
        {
            profile = SmoothProfile(profile);
        }
        
        // Adjust to match target average gradient exactly
        profile = AdjustToTargetGradient(profile, targetElevationChange);
        
        // Constrain local gradients
        profile = ConstrainLocalGradients(profile);
        
        endElevation = profile[samples - 1];
        
        return profile;
    }

    /// <summary>
    /// Smooth elevation profile using moving average
    /// </summary>
    private float[] SmoothProfile(float[] profile)
    {
        float[] smoothed = new float[profile.Length];
        int windowSize = 5;
        
        for (int i = 0; i < profile.Length; i++)
        {
            float sum = 0f;
            int count = 0;
            
            for (int j = -windowSize; j <= windowSize; j++)
            {
                int index = i + j;
                if (index >= 0 && index < profile.Length)
                {
                    sum += profile[index];
                    count++;
                }
            }
            
            smoothed[i] = sum / count;
        }
        
        // Preserve start and end points
        smoothed[0] = profile[0];
        smoothed[profile.Length - 1] = profile[profile.Length - 1];
        
        return smoothed;
    }

    /// <summary>
    /// Adjust profile to match target gradient exactly
    /// </summary>
    private float[] AdjustToTargetGradient(float[] profile, float targetElevationChange)
    {
        float currentChange = profile[profile.Length - 1] - profile[0];
        float adjustment = targetElevationChange - currentChange;
        
        // Distribute adjustment linearly
        for (int i = 0; i < profile.Length; i++)
        {
            float t = (float)i / (profile.Length - 1);
            profile[i] += adjustment * t;
        }
        
        return profile;
    }

    /// <summary>
    /// Constrain local gradients to realistic values
    /// </summary>
    private float[] ConstrainLocalGradients(float[] profile)
    {
        float sampleDistance = routeLength / (profile.Length - 1);
        
        for (int i = 1; i < profile.Length; i++)
        {
            float heightDiff = profile[i] - profile[i - 1];
            float localGradient = (heightDiff / sampleDistance) * 100f;
            
            // Constrain gradient
            if (localGradient < minLocalGradient)
            {
                float maxDrop = (minLocalGradient / 100f) * sampleDistance;
                profile[i] = profile[i - 1] + maxDrop;
            }
            else if (localGradient > maxLocalGradient)
            {
                float maxRise = (maxLocalGradient / 100f) * sampleDistance;
                profile[i] = profile[i - 1] + maxRise;
            }
        }
        
        return profile;
    }

    /// <summary>
    /// Apply elevation profile to terrain heightmap
    /// </summary>
    private void ApplyProfileToTerrain(float[,] heights, float[] profile, int resolution)
    {
        TerrainData terrainData = terrain.terrainData;
        float terrainLength = terrainData.size.x;
        float terrainWidth = terrainData.size.z;
        float terrainHeight = terrainData.size.y;
        
        // Calculate road center in terrain coordinates
        int roadCenterZ = resolution / 2;
        int roadWidthSamples = Mathf.CeilToInt((roadWidth / terrainWidth) * resolution);
        
        for (int x = 0; x < resolution; x++)
        {
            // Map x position to profile index
            float t = (float)x / (resolution - 1);
            int profileIndex = Mathf.FloorToInt(t * (profile.Length - 1));
            profileIndex = Mathf.Clamp(profileIndex, 0, profile.Length - 1);
            
            float elevation = profile[profileIndex];
            float normalizedHeight = elevation / terrainHeight;
            
            // Apply to road width
            for (int z = roadCenterZ - roadWidthSamples; z <= roadCenterZ + roadWidthSamples; z++)
            {
                if (z >= 0 && z < resolution)
                {
                    // Smooth edges of road
                    float distanceFromCenter = Mathf.Abs(z - roadCenterZ);
                    float edgeFactor = 1f - Mathf.Clamp01((distanceFromCenter - roadWidthSamples * 0.5f) / (roadWidthSamples * 0.5f));
                    
                    float blendedHeight = Mathf.Lerp(heights[z, x], normalizedHeight, edgeFactor);
                    heights[z, x] = blendedHeight;
                }
            }
        }
    }

    /// <summary>
    /// Calculate actual terrain statistics
    /// </summary>
    private void CalculateTerrainStatistics(float[] profile)
    {
        float totalElevationChange = profile[profile.Length - 1] - profile[0];
        actualAverageGradient = (totalElevationChange / routeLength) * 100f;
    }

    /// <summary>
    /// Log detailed terrain statistics
    /// </summary>
    private void LogDetailedStatistics(float[] profile)
    {
        float sampleDistance = routeLength / (profile.Length - 1);
        
        // Calculate gradient statistics
        List<float> gradients = new List<float>();
        float totalClimb = 0f;
        float totalDescent = 0f;
        float maxGradient = float.MinValue;
        float minGradient = float.MaxValue;
        
        for (int i = 1; i < profile.Length; i++)
        {
            float heightDiff = profile[i] - profile[i - 1];
            float gradient = (heightDiff / sampleDistance) * 100f;
            gradients.Add(gradient);
            
            if (heightDiff > 0) totalClimb += heightDiff;
            if (heightDiff < 0) totalDescent += Mathf.Abs(heightDiff);
            
            if (gradient > maxGradient) maxGradient = gradient;
            if (gradient < minGradient) minGradient = gradient;
        }
        
        // Calculate average of absolute gradients (undulation measure)
        float avgAbsGradient = 0f;
        foreach (float g in gradients)
        {
            avgAbsGradient += Mathf.Abs(g);
        }
        avgAbsGradient /= gradients.Count;
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"ROUTE STATISTICS");
        Debug.Log($"{'='*60}");
        Debug.Log($"Target Average Gradient: {currentTargetGradient}%");
        Debug.Log($"Actual Average Gradient: {actualAverageGradient:F2}%");
        Debug.Log($"Gradient Error: {Mathf.Abs(actualAverageGradient - currentTargetGradient):F2}%");
        Debug.Log($"\nElevation:");
        Debug.Log($"  Start: {startElevation:F1}m");
        Debug.Log($"  End: {endElevation:F1}m");
        Debug.Log($"  Net Change: {endElevation - startElevation:F1}m");
        Debug.Log($"  Total Climb: {totalClimb:F1}m");
        Debug.Log($"  Total Descent: {totalDescent:F1}m");
        Debug.Log($"\nGradient Range:");
        Debug.Log($"  Maximum: {maxGradient:F1}%");
        Debug.Log($"  Minimum: {minGradient:F1}%");
        Debug.Log($"  Avg Absolute: {avgAbsGradient:F2}% (undulation measure)");
        Debug.Log($"\nRoute:");
        Debug.Log($"  Length: {routeLength}m");
        Debug.Log($"  Undulations: {undulationCount}");
        Debug.Log($"  Samples: {profile.Length}");
        Debug.Log($"{'='*60}\n");
    }

    /// <summary>
    /// Get the currently selected gradient
    /// </summary>
    public float GetCurrentGradient()
    {
        return currentTargetGradient;
    }

    /// <summary>
    /// Get the actual achieved gradient
    /// </summary>
    public float GetActualGradient()
    {
        return actualAverageGradient;
    }

    /// <summary>
    /// Get the index of selected gradient (for logging)
    /// </summary>
    public int GetSelectedGradientIndex()
    {
        return selectedGradientIndex;
    }

    /// <summary>
    /// Set specific gradient and generate
    /// </summary>
    public void SetGradientAndGenerate(float gradient)
    {
        currentTargetGradient = gradient;
        GenerateRoute();
    }

    /// <summary>
    /// Get route length
    /// </summary>
    public float GetRouteLength()
    {
        return routeLength;
    }
}
