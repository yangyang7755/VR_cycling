using UnityEngine;

/// <summary>
/// Simple step-by-step terrain builder
/// Step 1: Clear terrain
/// Step 2: Add elevation profile
/// Step 3: Paint road
/// </summary>
public class SimpleTerrainBuilder : MonoBehaviour
{
    [Header("Terrain Reference")]
    [SerializeField] private Terrain terrain;

    [Header("Elevation Profile")]
    [Tooltip("Base height of terrain at start")]
    [SerializeField] private float baseHeight = 0.1f;
    
    [Tooltip("Gradient of the hill (5% = 0.05)")]
    [Range(0f, 0.2f)]
    [SerializeField] private float gradient = 0.05f; // 5% gradient
    
    [Tooltip("Visual gradient multiplier (makes slope more visible - 1.0 = same as physics, higher = more visible)")]
    [Range(1f, 20f)]
    [SerializeField] private float visualGradientMultiplier = 3.0f; // Make slope more visible (increased default and range)
    
    [Header("Hill Transitions")]
    [Tooltip("Smooth transition at start of hill (meters)")]
    [Range(0f, 100f)]
    [SerializeField] private float startTransition = 10f;
    
    [Tooltip("Smooth transition at end of hill (meters). Hill extends to (length - endTransition)")]
    [Range(0f, 100f)]
    [SerializeField] private float endTransition = 50f;
    
    [Header("Randomization")]
    [Tooltip("Random seed for terrain generation (0 = use random seed each time)")]
    [SerializeField] private int randomSeed = 0;
    
    [Tooltip("Enable randomization for each trial")]
    [SerializeField] private bool enableRandomization = true;
    
    [Tooltip("Add subtle noise for natural variation (disabled for clearer slope)")]
    [SerializeField] private bool addNoise = false; // Disabled by default for clearer slope
    
    [Tooltip("Noise scale (smaller = more detail)")]
    [SerializeField] private float noiseScale = 0.05f;
    
    [Tooltip("Noise amplitude (how much noise affects height) - keep low for subtle variation")]
    [Range(0f, 0.02f)]
    [SerializeField] private float noiseAmplitude = 0.005f; // Very subtle noise
    
    [Header("Randomization Settings")]
    [Tooltip("Random variation in noise scale (0 = no variation)")]
    [Range(0f, 0.1f)]
    [SerializeField] private float noiseScaleVariation = 0.02f;
    
    [Tooltip("Random variation in noise amplitude (0 = no variation)")]
    [Range(0f, 0.01f)]
    [SerializeField] private float noiseAmplitudeVariation = 0.003f;
    
    [Tooltip("Add random undulations (small hills/valleys)")]
    [SerializeField] private bool addRandomUndulations = true;
    
    [Tooltip("Number of random undulations")]
    [Range(0, 10)]
    [SerializeField] private int undulationCount = 3;
    
    [Tooltip("Undulation amplitude (height variation)")]
    [Range(0f, 0.05f)]
    [SerializeField] private float undulationAmplitude = 0.01f;
    
    [Tooltip("Undulation size (radius in meters)")]
    [Range(10f, 100f)]
    [SerializeField] private float undulationSize = 30f;

    [Header("Frequency-Based Undulation (for HillConfiguration)")]
    [Tooltip("Enable frequency-based undulation (rolling terrain using sine waves)")]
    [SerializeField] private bool enableFrequencyBasedUndulation = false;
    
    [Tooltip("Undulation frequency (how often terrain undulates - higher = more frequent hills/valleys)")]
    [Range(0.01f, 2f)]
    [SerializeField] private float undulationFrequency = 0.15f;
    
    [Tooltip("Undulation amplitude in meters (height variation - how high/low the rolling hills are)")]
    [Range(0f, 10f)]
    [SerializeField] private float undulationAmplitudeMeters = 1.5f;
    
    [Tooltip("Random seed for undulation (0 = random each time)")]
    [SerializeField] private int undulationSeed = 0;

    [Header("Road Settings")]
    [Tooltip("Width of road in meters")]
    [SerializeField] private float roadWidth = 15f;
    
    [Tooltip("Road position offset (negative = forward, positive = backward)")]
    [SerializeField] private float roadOffsetX = 0f;
    
    [Header("Mountain Settings")]
    [Tooltip("Add mountain texture at the end of the path (in front of cyclist)")]
    [SerializeField] private bool addMountainAtEnd = true;
    
    [Tooltip("Distance from end where mountain texture starts (0 = at the very end, higher = starts earlier)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float mountainStartPosition = 0.2f; // 0.2 = starts at 80% of path length
    
    [Tooltip("Blend distance for mountain texture transition (in meters)")]
    [Range(5f, 50f)]
    [SerializeField] private float mountainBlendDistance = 20f;

    [Header("Textures")]
    [SerializeField] private TerrainLayer grassLayer;
    [SerializeField] private TerrainLayer roadLayer;
    [SerializeField] private TerrainLayer mountainLayer;

    // Public properties for access by other scripts
    public TerrainLayer GrassLayer => grassLayer;
    public TerrainLayer RoadLayer => roadLayer;
    public TerrainLayer MountainLayer => mountainLayer;

    [Header("Auto Initialization")]
    [Tooltip("Automatically initialize terrain with grass and road on Start")]
    [SerializeField] private bool autoInitializeOnStart = true;

    private bool isInitialized = false;

    private void Start()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        // Auto-load terrain layers if not assigned
        #if UNITY_EDITOR
        if (Application.isPlaying)
        {
            if (grassLayer == null)
            {
                LoadTexture("Grass", ref grassLayer);
            }
            if (roadLayer == null)
            {
                LoadTexture("Road", ref roadLayer);
            }
            if (mountainLayer == null)
            {
                LoadTexture("Mountain", ref mountainLayer);
            }
        }
        #endif

        // Auto-initialize terrain if enabled
        if (autoInitializeOnStart && !isInitialized)
        {
            InitializeTerrain();
        }
    }

    /// <summary>
    /// Initialize terrain with grass texture and road (for flat course)
    /// This should be called once at the start to set up the base terrain
    /// </summary>
    public void InitializeTerrain()
    {
        if (isInitialized)
        {
            Debug.Log("[SimpleTerrainBuilder] Terrain already initialized, skipping...");
            return;
        }

        Debug.Log("[SimpleTerrainBuilder] Initializing terrain with grass and road...");
        
        // Set gradient to 0 for flat course initialization
        float originalGradient = gradient;
        gradient = 0f;
        
        // Do all steps: clear, add elevation (flat), setup textures (grass), paint road
        DoAllSteps();
        
        // Restore original gradient
        gradient = originalGradient;
        
        isInitialized = true;
        Debug.Log("[SimpleTerrainBuilder] ✓ Terrain initialized successfully!");
    }

    /// <summary>
    /// Set gradient and rebuild terrain (for use by TrialTerrainManager)
    /// Clamps to maximum 12% as per requirement
    /// </summary>
    public void SetGradient(float newGradient)
    {
        // Clamp to maximum 12% (0.12 decimal)
        float clampedGradient = Mathf.Clamp(newGradient, 0f, 12f);
        if (newGradient > 12f)
        {
            Debug.LogWarning($"[SimpleTerrainBuilder] Gradient {newGradient}% exceeds 12% limit. Clamping to {clampedGradient}%");
        }
        gradient = clampedGradient / 100f; // Convert percentage to decimal (12% = 0.12)
        Debug.Log($"[SimpleTerrainBuilder] Gradient set to {clampedGradient}% ({gradient} decimal)");
    }

    /// <summary>
    /// Get current gradient as percentage
    /// </summary>
    public float GetGradient()
    {
        return gradient * 100f; // Convert decimal to percentage
    }

    /// <summary>
    /// Get road width (for use by other scripts)
    /// </summary>
    public float GetRoadWidth()
    {
        return roadWidth;
    }

    /// <summary>
    /// Get road center Z position (for use by other scripts)
    /// </summary>
    public float GetRoadCenterZ()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }
        
        if (terrain != null)
        {
            return terrain.terrainData.size.z / 2f; // Road is in middle of Z axis
        }
        
        return 0f;
    }

    /// <summary>
    /// Set visual gradient multiplier (for use by TrialTerrainManager)
    /// </summary>
    public void SetVisualGradientMultiplier(float multiplier)
    {
        visualGradientMultiplier = Mathf.Clamp(multiplier, 1f, 5f);
        Debug.Log($"[SimpleTerrainBuilder] Visual gradient multiplier set to {visualGradientMultiplier}x");
    }

    /// <summary>
    /// Get current visual gradient (physics gradient * visual multiplier)
    /// This is the gradient used for terrain appearance
    /// </summary>
    public float GetVisualGradient()
    {
        return gradient * visualGradientMultiplier;
    }

    /// <summary>
    /// Get current physics gradient (used for resistance calculation)
    /// </summary>
    public float GetPhysicsGradient()
    {
        return gradient;
    }

    /// <summary>
    /// Get visual gradient multiplier
    /// </summary>
    public float GetVisualGradientMultiplier()
    {
        return visualGradientMultiplier;
    }

    /// <summary>
    /// Set undulation parameters for rolling terrain (called by HillTrialManager)
    /// </summary>
    /// <param name="enable">Enable/disable undulations</param>
    /// <param name="amplitude">Amplitude in meters (height variation)</param>
    /// <param name="frequency">Frequency (how often hills occur - higher = more frequent)</param>
    /// <param name="seed">Random seed (0 = random each time)</param>
    public void SetUndulationParameters(bool enable, float amplitude, float frequency, int seed)
    {
        enableFrequencyBasedUndulation = enable;
        undulationAmplitudeMeters = Mathf.Clamp(amplitude, 0f, 10f);
        undulationFrequency = Mathf.Clamp(frequency, 0.01f, 2f);
        undulationSeed = seed;
        
        Debug.Log($"[SimpleTerrainBuilder] Undulation parameters set: Enable={enable}, Amplitude={amplitude}m, Frequency={frequency}, Seed={seed}");
    }

    /// <summary>
    /// Sample Fractal Brownian Motion (FBM) noise for irregular, natural-looking terrain
    /// Uses multiple octaves of Perlin noise for more random and irregular patterns
    /// </summary>
    /// <param name="distance">Distance along road in meters</param>
    /// <param name="normalizedZ">Normalized Z position (0-1)</param>
    /// <param name="baseFrequency">Base frequency for undulation</param>
    /// <param name="seed">Random seed for variation</param>
    /// <param name="octaves">Number of octaves (more = more detail, more irregular)</param>
    /// <returns>Noise value in range -1 to 1</returns>
    private float SampleFBMNoise(float distance, float normalizedZ, float baseFrequency, int seed, int octaves = 4)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = baseFrequency;
        float maxValue = 0f; // For normalization
        
        for (int i = 0; i < octaves; i++)
        {
            // Sample Perlin noise at different scales
            float noiseX = (distance * frequency) + (seed * 0.01f * (i + 1));
            float noiseZ = (normalizedZ * frequency * 0.5f) + (seed * 0.01f * (i + 1));
            float noiseValue = Mathf.PerlinNoise(noiseX, noiseZ);
            
            // Add weighted contribution from this octave
            value += (noiseValue - 0.5f) * 2f * amplitude; // Convert 0-1 to -1 to 1
            maxValue += amplitude;
            
            // Increase frequency and decrease amplitude for next octave
            frequency *= 2f; // Double frequency for each octave
            amplitude *= 0.5f; // Halve amplitude for each octave (standard FBM)
        }
        
        // Normalize to -1 to 1 range
        if (maxValue > 0.01f)
        {
            value /= maxValue;
        }
        
        return value;
    }

    /// <summary>
    /// Set random seed for terrain generation (0 = random)
    /// </summary>
    public void SetRandomSeed(int seed)
    {
        randomSeed = seed;
        Debug.Log($"[SimpleTerrainBuilder] Random seed set to {randomSeed}");
    }

    /// <summary>
    /// Enable or disable randomization
    /// </summary>
    public void SetRandomizationEnabled(bool enabled)
    {
        enableRandomization = enabled;
        Debug.Log($"[SimpleTerrainBuilder] Randomization {(enabled ? "enabled" : "disabled")}");
    }

    [ContextMenu("Step 1: Clear Terrain")]
    public void ClearTerrain()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogError("No terrain found!");
            return;
        }

        TerrainData data = terrain.terrainData;
        int width = data.heightmapResolution;
        int height = data.heightmapResolution;

        // Set all heights to zero
        float[,] heights = new float[width, height];
        data.SetHeights(0, 0, heights);

        // Clear all textures
        int numLayers = data.terrainLayers.Length;
        float[,,] alphamap = new float[data.alphamapWidth, data.alphamapHeight, numLayers];
        data.SetAlphamaps(0, 0, alphamap);

        Debug.Log("✓ Terrain cleared!");
    }

    [ContextMenu("Step 2: Add Elevation Profile")]
    public void AddElevationProfile()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogError("No terrain found!");
            return;
        }

        TerrainData data = terrain.terrainData;
        int width = data.heightmapResolution;
        int height = data.heightmapResolution;
        Vector3 size = data.size;

        float[,] heights = new float[width, height];

        // Calculate height increment per pixel in X direction (road direction)
        // Gradient is rise/run, so for each meter in X, we rise by gradient meters
        float heightIncrementPerMeter = gradient; // 5% = 0.05 means 0.05m rise per 1m run
        float heightIncrementPerPixel = (heightIncrementPerMeter / size.x) * size.y; // Convert to heightmap units

        // Set up randomization seed
        int seed = randomSeed;
        if (enableRandomization && seed == 0)
        {
            seed = Random.Range(1, 999999);
        }
        else if (!enableRandomization)
        {
            seed = 12345; // Fixed seed when randomization disabled
        }
        
        Random.InitState(seed);
        Debug.Log($"[SimpleTerrainBuilder] Using random seed: {seed}");

        // Calculate randomized noise parameters
        float currentNoiseScale = noiseScale;
        float currentNoiseAmplitude = noiseAmplitude;
        
        if (enableRandomization)
        {
            currentNoiseScale = noiseScale + Random.Range(-noiseScaleVariation, noiseScaleVariation);
            currentNoiseAmplitude = noiseAmplitude + Random.Range(-noiseAmplitudeVariation, noiseAmplitudeVariation);
            currentNoiseScale = Mathf.Max(0.01f, currentNoiseScale); // Ensure positive
            currentNoiseAmplitude = Mathf.Max(0f, currentNoiseAmplitude); // Ensure non-negative
        }

        // Generate random undulation positions
        Vector2[] undulationPositions = new Vector2[undulationCount];
        float[] undulationHeights = new float[undulationCount];
        
        if (enableRandomization && addRandomUndulations && undulationCount > 0)
        {
            for (int i = 0; i < undulationCount; i++)
            {
                // Random position across terrain (normalized 0-1)
                undulationPositions[i] = new Vector2(
                    Random.Range(0.1f, 0.9f), // X position (avoid edges)
                    Random.Range(0.1f, 0.9f)  // Z position (avoid edges)
                );
                // Random height variation (positive or negative)
                undulationHeights[i] = Random.Range(-undulationAmplitude, undulationAmplitude);
            }
        }

        // Calculate visual gradient (with multiplier for clearer slope)
        // Increased limit to 30% to allow steeper visual gradients for better perception
        float visualGradient = gradient * visualGradientMultiplier;
        float clampedVisualGradient = Mathf.Clamp(visualGradient, 0f, 0.30f); // Maximum 30% (0.30 decimal) - increased from 12%
        
        if (visualGradient > 0.30f)
        {
            Debug.LogWarning($"[SimpleTerrainBuilder] Visual gradient {visualGradient * 100f}% exceeds 30% limit after applying {visualGradientMultiplier}x multiplier. Clamping to {clampedVisualGradient * 100f}%");
        }
        
        visualGradient = clampedVisualGradient; // Use clamped value
        float visualHeightGain = size.x * visualGradient;
        
        Debug.Log($"Creating hill with {gradient * 100f}% gradient");
        Debug.Log($"  Terrain size: {size.x}m (X) x {size.z}m (Z) x {size.y}m (Y)");
        Debug.Log($"  Physics gradient: {gradient * 100f}%");
        Debug.Log($"  Visual gradient: {visualGradient * 100f}% (multiplier: {visualGradientMultiplier}x, CLAMPED to 30% max)");
        Debug.Log($"  Total height gain: {visualHeightGain}m over {size.x}m");
        
        // #region agent log
        System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log", 
            $"{{\"id\":\"visual_gradient_calculation\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"SimpleTerrainBuilder.cs:449\",\"message\":\"Visual gradient calculated and clamped\",\"data\":{{\"physicsGradient\":{gradient * 100f:F2},\"multiplier\":{visualGradientMultiplier:F2},\"rawVisualGradient\":{(gradient * visualGradientMultiplier) * 100f:F2},\"clampedVisualGradient\":{visualGradient * 100f:F2}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"J\"}}\n");
        // #endregion
        Debug.Log($"  Randomization: {(enableRandomization ? "Enabled" : "Disabled")} (Seed: {seed})");
        if (enableFrequencyBasedUndulation)
        {
            Debug.Log($"  Frequency-Based Undulation: Enabled (Amplitude: {undulationAmplitudeMeters}m, Frequency: {undulationFrequency}, Seed: {undulationSeed})");
        }
        else if (enableRandomization && addRandomUndulations)
        {
            Debug.Log($"  Random Undulations: {undulationCount} random variations");
        }

        // Calculate transition factors
        float terrainLength = size.x; // Total terrain length in meters
        float startTransitionNorm = startTransition / terrainLength; // Normalized start transition
        float endTransitionNorm = endTransition / terrainLength; // Normalized end transition
        float mainSlopeStart = startTransitionNorm; // Where main slope starts
        float mainSlopeEnd = 1f - endTransitionNorm; // Where main slope ends
        
        Debug.Log($"[SimpleTerrainBuilder] Transitions: Start={startTransition}m ({startTransitionNorm:F3}), End={endTransition}m ({endTransitionNorm:F3})");
        Debug.Log($"[SimpleTerrainBuilder] Main slope: {mainSlopeStart:F3} to {mainSlopeEnd:F3} (hill extends to {terrainLength - endTransition:F1}m)");

        // Create elevation profile with gradient along X axis (road direction)
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Normalized position (0 to 1) along X axis
                float normalizedX = (float)x / (width - 1);
                float normalizedZ = (float)z / (height - 1);
                
                // Calculate transition factor (0 to 1)
                float transitionFactor = 1f;
                float maxHeightNormalizedX = mainSlopeEnd; // Maximum height is at end of main slope
                
                if (normalizedX < mainSlopeStart && startTransition > 0.1f)
                {
                    // Start transition: gradually increase from 0 to 1
                    transitionFactor = normalizedX / mainSlopeStart;
                }
                else if (normalizedX > mainSlopeEnd && endTransition > 0.1f)
                {
                    // End transition: Keep terrain flat (at maximum height) instead of going downhill
                    // After mainSlopeEnd, use the height at mainSlopeEnd (transitionFactor = 1 at mainSlopeEnd)
                    transitionFactor = 1f; // Keep at maximum height (flat after hill finishes)
                    
                    // #region agent log
                    if (x == width - 1 && z == height / 2) // Log once per row at end
                    {
                        System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log", 
                            $"{{\"id\":\"end_transition_flat\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"SimpleTerrainBuilder.cs:509\",\"message\":\"End transition: keeping terrain flat\",\"data\":{{\"normalizedX\":{normalizedX:F3},\"mainSlopeEnd\":{mainSlopeEnd:F3},\"endTransition\":{endTransition:F1},\"transitionFactor\":{transitionFactor:F3}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                    }
                    // #endregion
                }
                
                // Base height with visual gradient (increases along X axis)
                // Only apply gradient up to mainSlopeEnd, then keep flat
                float effectiveNormalizedX = normalizedX > mainSlopeEnd ? maxHeightNormalizedX : normalizedX;
                float effectiveGradient = effectiveNormalizedX * transitionFactor;
                float heightAtX = baseHeight + (effectiveGradient * visualHeightGain / size.y);
                
                // Add subtle noise for natural variation if enabled
                if (addNoise)
                {
                    float noiseX = (float)x * currentNoiseScale + seed * 0.1f; // Add seed offset for variation
                    float noiseZ = (float)z * currentNoiseScale + seed * 0.1f;
                    float noise = Mathf.PerlinNoise(noiseX, noiseZ) * currentNoiseAmplitude;
                    heightAtX += noise;
                }
                
                // Add frequency-based undulation (rolling terrain with irregular, random patterns)
                // This is used when called from HillTrialManager with HillConfiguration
                if (enableFrequencyBasedUndulation && undulationFrequency > 0f && undulationAmplitudeMeters > 0f)
                {
                    float distance = normalizedX * size.x; // Distance along road in meters
                    
                    // Use seed offset for variation (even if seed is 0, use a default offset)
                    int effectiveSeed = undulationSeed != 0 ? undulationSeed : 12345;
                    
                    // Use Fractal Brownian Motion (FBM) with multiple octaves for irregular, natural-looking terrain
                    // This creates much more random and irregular undulations than simple sine waves
                    float fbmValue = SampleFBMNoise(distance, normalizedZ, undulationFrequency, effectiveSeed, 4); // 4 octaves
                    
                    // Add additional random variation at different scales for more irregularity
                    // Multiple scales create natural-looking terrain that is much less repetitive than sine waves
                    
                    // Large scale variations (long wavelength) - creates major hills/valleys
                    float largeNoiseX = (distance * undulationFrequency * 0.3f) + (effectiveSeed * 0.05f);
                    float largeNoiseZ = (normalizedZ * 0.5f) + (effectiveSeed * 0.05f);
                    float largeScaleVariation = (Mathf.PerlinNoise(largeNoiseX, largeNoiseZ) - 0.5f) * 0.4f; // 40% of amplitude
                    
                    // Medium scale variations (medium wavelength) - creates medium features
                    float medNoiseX = (distance * undulationFrequency * 1.2f) + (effectiveSeed * 0.1f);
                    float medNoiseZ = (normalizedZ * 2f) + (effectiveSeed * 0.1f);
                    float mediumScaleVariation = (Mathf.PerlinNoise(medNoiseX, medNoiseZ) - 0.5f) * 0.35f; // 35% of amplitude
                    
                    // Small scale variations (short wavelength) - creates fine detail
                    float smallNoiseX = (distance * undulationFrequency * 3.5f) + (effectiveSeed * 0.2f);
                    float smallNoiseZ = (normalizedZ * 5f) + (effectiveSeed * 0.2f);
                    float smallScaleVariation = (Mathf.PerlinNoise(smallNoiseX, smallNoiseZ) - 0.5f) * 0.25f; // 25% of amplitude
                    
                    // Combine all variations for irregular, non-repetitive terrain
                    // FBM provides base structure, multi-scale variations add randomness
                    float combinedUndulation = fbmValue * 0.5f + largeScaleVariation * 0.25f + mediumScaleVariation * 0.15f + smallScaleVariation * 0.1f;
                    
                    // Add occasional "spikes" or sudden changes for more randomness (5% chance per sample)
                    // This creates unexpected terrain features that break up regular patterns
                    float spikeNoiseX = (distance * 0.02f) + (effectiveSeed * 0.03f);
                    float spikeNoiseZ = (normalizedZ * 0.02f) + (effectiveSeed * 0.03f);
                    float spikeValue = Mathf.PerlinNoise(spikeNoiseX, spikeNoiseZ);
                    if (spikeValue > 0.95f) // Rare spikes (5% chance)
                    {
                        combinedUndulation += (spikeValue - 0.95f) * 3f; // Sudden increase
                    }
                    else if (spikeValue < 0.05f) // Rare valleys (5% chance)
                    {
                        combinedUndulation -= (0.05f - spikeValue) * 3f; // Sudden decrease
                    }
                    
                    // Apply amplitude and convert to normalized height (0-1 range)
                    // CRITICAL: Limit undulation amplitude to ensure local gradient does not exceed 12%
                    // Estimate maximum gradient contribution from undulation
                    // If undulation creates height variation of 'A' meters over distance 'D', gradient = (A/D) * 100%
                    // For undulation with frequency F, typical wavelength is ~1/F, so D ≈ (1/F) * terrain_length
                    // To ensure gradient contribution < 2% (leaving 10% for base slope), we need: (A/D) * 100% < 2%
                    // Therefore: A < (D * 0.02) = ((1/F) * terrain_length) * 0.02
                    // Note: terrainLength is already defined above in the method scope (line 482), so reuse it
                    float undulationWavelength = (1f / Mathf.Max(undulationFrequency, 0.01f)) * terrainLength;
                    float maxSafeUndulationAmplitude = undulationWavelength * 0.02f; // Max 2% gradient contribution
                    float safeUndulationAmplitude = Mathf.Min(undulationAmplitudeMeters, maxSafeUndulationAmplitude);
                    
                    if (undulationAmplitudeMeters > maxSafeUndulationAmplitude)
                    {
                        Debug.LogWarning($"[SimpleTerrainBuilder] Undulation amplitude {undulationAmplitudeMeters}m reduced to {safeUndulationAmplitude}m to prevent gradient > 12% (frequency: {undulationFrequency}, wavelength: {undulationWavelength:F1}m)");
                    }
                    
                    float undulationHeight = combinedUndulation * safeUndulationAmplitude;
                    float normalizedUndulationHeight = undulationHeight / size.y; // Convert meters to normalized height
                    
                    heightAtX += normalizedUndulationHeight;
                    
                    // #region agent log
                    if (x == 0 && z == 0) // Log once per terrain generation
                    {
                        System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log", 
                            $"{{\"id\":\"undulation_applied_irregular\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"SimpleTerrainBuilder.cs:468\",\"message\":\"Irregular frequency-based undulation applied (FBM)\",\"data\":{{\"enabled\":{enableFrequencyBasedUndulation},\"amplitudeMeters\":{undulationAmplitudeMeters:F2},\"frequency\":{undulationFrequency:F3},\"seed\":{undulationSeed},\"method\":\"FBM_4octaves\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"H\"}}\n");
                    }
                    // #endregion
                }
                // Add random undulations (small hills/valleys) - legacy method
                else if (enableRandomization && addRandomUndulations && undulationCount > 0)
                {
                    for (int i = 0; i < undulationCount; i++)
                    {
                        float distX = (normalizedX - undulationPositions[i].x) * size.x;
                        float distZ = (normalizedZ - undulationPositions[i].y) * size.z;
                        float distance = Mathf.Sqrt(distX * distX + distZ * distZ);
                        
                        // Apply Gaussian falloff for smooth undulation
                        if (distance < undulationSize)
                        {
                            float falloff = Mathf.Exp(-(distance * distance) / (undulationSize * undulationSize * 0.5f));
                            heightAtX += undulationHeights[i] * falloff / size.y;
                        }
                    }
                }
                
                // Clamp to valid height range (0 to 1)
                heights[x, z] = Mathf.Clamp01(heightAtX);
            }
        }

        data.SetHeights(0, 0, heights);
        Debug.Log($"✓ Elevation profile added!");
        Debug.Log($"  Physics gradient: {gradient * 100f}%");
        Debug.Log($"  Visual gradient: {visualGradient * 100f}% (multiplier: {visualGradientMultiplier}x, CLAMPED to 30% max)");
        Debug.Log($"  Start height: {baseHeight}");
        Debug.Log($"  End height: {baseHeight + visualHeightGain / size.y}");
        Debug.Log($"  Noise: {(addNoise ? "Enabled" : "Disabled")}");
    }

    [ContextMenu("Step 3: Setup Textures")]
    public void SetupTextures()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogError("No terrain found!");
            return;
        }

        TerrainData data = terrain.terrainData;

        // Load textures if not assigned
        if (grassLayer == null)
        {
            LoadTexture("Grass", ref grassLayer);
        }

        if (roadLayer == null)
        {
            LoadTexture("Road", ref roadLayer);
        }

        if (mountainLayer == null)
        {
            LoadTexture("Mountain", ref mountainLayer);
        }

        // Build layers list (only include non-null layers)
        System.Collections.Generic.List<TerrainLayer> layers = new System.Collections.Generic.List<TerrainLayer>();
        if (grassLayer != null) layers.Add(grassLayer);
        if (roadLayer != null) layers.Add(roadLayer);
        if (mountainLayer != null) layers.Add(mountainLayer);

        if (layers.Count == 0)
        {
            Debug.LogError("Could not load any textures! Please assign them manually.");
            return;
        }

        // Set up terrain layers
        data.terrainLayers = layers.ToArray();

        // Get layer indices
        int grassIndex = grassLayer != null ? layers.IndexOf(grassLayer) : -1;
        int roadIndex = roadLayer != null ? layers.IndexOf(roadLayer) : -1;
        int mountainIndex = mountainLayer != null ? layers.IndexOf(mountainLayer) : -1;

        // Paint entire terrain with grass (or mountain if no grass)
        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        int numLayers = layers.Count;
        float[,,] alphamap = new float[width, height, numLayers];

        // Calculate road center for initial texture setup
        int centerZ = height / 2;
        if (roadOffsetX != 0f)
        {
            float offsetInPixels = (roadOffsetX / data.size.z) * height;
            centerZ = Mathf.RoundToInt(centerZ + offsetInPixels);
        }

        // Calculate mountain position at end of path (X axis - forward direction)
        int mountainStartX = Mathf.RoundToInt(width * (1f - mountainStartPosition));
        float blendInPixels = (mountainBlendDistance / data.size.x) * width;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                // Initialize all layers to 0
                for (int layer = 0; layer < numLayers; layer++)
                {
                    alphamap[x, z, layer] = 0f;
                }
                
                // Check if we're in the mountain area at the end of path
                bool inMountainArea = addMountainAtEnd && x >= mountainStartX && mountainIndex >= 0;
                
                if (inMountainArea)
                {
                    // Calculate blend factor for smooth transition
                    float distFromMountainStart = x - mountainStartX;
                    float blendFactor = 1f;
                    
                    // Smooth blend at the start of mountain area
                    if (distFromMountainStart < blendInPixels)
                    {
                        blendFactor = Mathf.SmoothStep(0f, 1f, distFromMountainStart / blendInPixels);
                    }
                    
                    // Apply mountain texture with blending
                    alphamap[x, z, mountainIndex] = blendFactor;
                    
                    // Blend with grass if available
                    if (grassIndex >= 0)
                    {
                        alphamap[x, z, grassIndex] = 1f - blendFactor;
                    }
                }
                else
                {
                    // Default to grass
                    if (grassIndex >= 0)
                    {
                        alphamap[x, z, grassIndex] = 1f;
                    }
                    else if (mountainIndex >= 0)
                    {
                        alphamap[x, z, mountainIndex] = 1f; // Fallback to mountain
                    }
                }
            }
        }

        data.SetAlphamaps(0, 0, alphamap);
        Debug.Log($"✓ Textures set up! Terrain painted with {layers.Count} layers.");
        Debug.Log($"  Layers: Grass={grassIndex >= 0}, Road={roadIndex >= 0}, Mountain={mountainIndex >= 0}");
    }

    [ContextMenu("Step 4: Paint Road")]
    public void PaintRoad()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogError("No terrain found!");
            return;
        }

        if (roadLayer == null)
        {
            Debug.LogError("Road layer not set! Run Step 3 first.");
            return;
        }

        TerrainData data = terrain.terrainData;
        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        Vector3 size = data.size;

        float[,,] alphamap = data.GetAlphamaps(0, 0, width, height);

        // Find layer indices
        int roadIndex = -1;
        int grassIndex = -1;
        int mountainIndex = -1;
        
        for (int i = 0; i < data.terrainLayers.Length; i++)
        {
            if (data.terrainLayers[i] == roadLayer)
            {
                roadIndex = i;
            }
            else if (data.terrainLayers[i] == grassLayer)
            {
                grassIndex = i;
            }
            else if (data.terrainLayers[i] == mountainLayer)
            {
                mountainIndex = i;
            }
        }

        if (roadIndex == -1)
        {
            Debug.LogError("Road layer not found in terrain!");
            return;
        }

        // Calculate road center position (in Z direction, for horizontal road)
        // Road goes from left to right (X axis), so center is in Z direction (forward/backward)
        int centerZ = height / 2;
        if (roadOffsetX != 0f)
        {
            // roadOffsetX means offset in Z direction (forward/backward)
            float offsetInPixels = (roadOffsetX / size.z) * height;
            centerZ = Mathf.RoundToInt(centerZ + offsetInPixels);
        }

        // Calculate road width in pixels (road width is in Z direction, forward/backward)
        float roadWidthInPixels = (roadWidth / size.z) * height;
        int halfWidth = Mathf.RoundToInt(roadWidthInPixels / 2f);

        Debug.Log($"Painting road: center Z={centerZ}, width={roadWidth}m");
        Debug.Log($"Road direction: X axis (left to right, HORIZONTAL)");
        Debug.Log($"Elevation profile: Z axis (front to back, VERTICAL)");

        // Paint road from left to right (X axis direction) - HORIZONTAL
        int pixelsPainted = 0;
        
        for (int x = 0; x < width; x++) // From left to right (X axis)
        {
            for (int z = 0; z < height; z++) // Across depth (Z axis)
            {
                int distFromCenter = Mathf.Abs(z - centerZ);
                
                if (distFromCenter <= halfWidth)
                {
                    // Paint road
                    alphamap[x, z, roadIndex] = 1f;
                    
                    // Clear other layers on road
                    if (grassIndex >= 0) alphamap[x, z, grassIndex] = 0f;
                    // Keep mountain layer if we're in mountain area (at end of path)
                    // Mountain will blend with road naturally
                    
                    pixelsPainted++;
                }
            }
        }

        data.SetAlphamaps(0, 0, alphamap);
        Debug.Log($"✓ Road painted! {pixelsPainted} road pixels");
        Debug.Log($"  Road width: {roadWidth}m");
        if (addMountainAtEnd && mountainIndex >= 0)
        {
            Debug.Log($"  Mountain at end: starts at {mountainStartPosition * 100f}% of path length");
        }
    }

    [ContextMenu("Do All Steps")]
    public void DoAllSteps()
    {
        ClearTerrain();
        AddElevationProfile();
        SetupTextures();
        PaintRoad();
        Debug.Log("✓ All steps completed!");
    }

    private void LoadTexture(string name, ref TerrainLayer layer)
    {
        #if UNITY_EDITOR
        // Try multiple search paths and name variations
        string[] searchPaths = new string[] 
        { 
            "Assets/Textures/Terrain",
            "Assets/Textures",
            "Assets"
        };

        // Try exact name first, then case-insensitive search
        string[] nameVariations = new string[] 
        { 
            name,  // Exact match
            name.ToLower(),  // Lowercase
            name.ToUpper(),  // Uppercase
            char.ToUpper(name[0]) + name.Substring(1).ToLower()  // Title case
        };

        foreach (string searchPath in searchPaths)
        {
            foreach (string nameVar in nameVariations)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets($"{nameVar} t:TerrainLayer", new[] { searchPath });
                if (guids.Length > 0)
                {
                    // Try all found assets to find the best match
                    foreach (string guid in guids)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        TerrainLayer foundLayer = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                        if (foundLayer != null)
                        {
                            // Check if name matches (case-insensitive)
                            if (foundLayer.name.Equals(name, System.StringComparison.OrdinalIgnoreCase) ||
                                foundLayer.name.Equals(nameVar, System.StringComparison.OrdinalIgnoreCase))
                            {
                                layer = foundLayer;
                                Debug.Log($"✓ Loaded texture: {layer.name} from {path}");
                                return;
                            }
                        }
                    }
                }
            }
        }

        // If still not found, try direct path
        string directPath = $"Assets/Textures/Terrain/{name}.terrainlayer";
        if (System.IO.File.Exists(directPath))
        {
            layer = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(directPath);
            if (layer != null)
            {
                Debug.Log($"✓ Loaded texture: {layer.name} from {directPath}");
                return;
            }
        }

        Debug.LogWarning($"[SimpleTerrainBuilder] Could not find TerrainLayer named '{name}'. Please assign it manually in Inspector.");
        #endif
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Auto-assign terrain layers from common paths (called from editor)
    /// </summary>
    [ContextMenu("Auto-Assign Terrain Layers")]
    public void AutoAssignTerrainLayers()
    {
        if (grassLayer == null)
        {
            string grassPath = "Assets/Textures/Terrain/Grass.terrainlayer";
            grassLayer = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(grassPath);
            if (grassLayer != null)
            {
                Debug.Log($"✓ Auto-assigned Grass layer: {grassLayer.name}");
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        if (roadLayer == null)
        {
            string roadPath = "Assets/Textures/Terrain/Road.terrainlayer";
            roadLayer = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(roadPath);
            if (roadLayer != null)
            {
                Debug.Log($"✓ Auto-assigned Road layer: {roadLayer.name}");
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        if (grassLayer == null || roadLayer == null)
        {
            Debug.LogWarning("[SimpleTerrainBuilder] Could not auto-assign all terrain layers. Please assign them manually.");
        }
    }
    #endif
}
