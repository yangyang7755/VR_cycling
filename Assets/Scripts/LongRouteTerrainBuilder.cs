using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Builds long rolling routes (5-50km) with base terrain and discrete climb segments
/// Supports configurable climbs with checkpoints and timestamped markers
/// </summary>
public class LongRouteTerrainBuilder : MonoBehaviour
{
    [Header("Route Configuration")]
    [Tooltip("Route configuration asset (ScriptableObject)")]
    [SerializeField] private RouteConfiguration routeConfig;

    [Tooltip("Terrain to modify")]
    [SerializeField] private Terrain terrain;

    [Header("Terrain Settings")]
    [Tooltip("Base height of terrain at start (meters)")]
    [SerializeField] private float baseHeight = 0f;

    [Tooltip("Terrain width (meters)")]
    [SerializeField] private float terrainWidth = 200f;

    [Tooltip("Random seed for terrain generation")]
    [SerializeField] private int randomSeed = 0;

    [Tooltip("Use random seed each time")]
    [SerializeField] private bool useRandomSeed = true;

    [Tooltip("Manually set random seed (overrides useRandomSeed)")]
    private bool manualSeedSet = false;

    [Header("Perlin Noise Settings")]
    [Tooltip("Number of samples along route (higher = smoother but slower)")]
    [Range(100, 10000)]
    [SerializeField] private int numSamples = 1000;

    [Tooltip("Perlin noise scale (lower = smoother, higher = more variation)")]
    [Range(0.0001f, 0.01f)]
    [SerializeField] private float noiseScale = 0.001f;

    [Tooltip("Perlin noise amplitude (height variation in meters)")]
    [Range(0f, 20f)]
    [SerializeField] private float noiseAmplitude = 5f;

    [Tooltip("Number of octaves for Perlin noise (more = more detail)")]
    [Range(1, 4)]
    [SerializeField] private int noiseOctaves = 2;

    [Header("Smoothing Settings")]
    [Tooltip("Enable smoothing filter")]
    [SerializeField] private bool enableSmoothing = true;

    [Tooltip("Smoothing window size (samples) - larger = smoother")]
    [Range(3, 50)]
    [SerializeField] private int smoothingWindowSize = 10;

    [Tooltip("Preserve desired steepness during smoothing")]
    [SerializeField] private bool preserveSteepness = true;

    [Header("Gradient Calculation")]
    [Tooltip("Window size for gradient calculation (meters)")]
    [Range(5f, 50f)]
    [SerializeField] private float gradientWindowSize = 10f;

    [Header("Export Settings")]
    [Tooltip("Export grade array for visualization and trainer bridge")]
    [SerializeField] private bool exportGradeArray = true;

    [Tooltip("GradeArrayController reference (for grade array export)")]
    [SerializeField] private GradeArrayController gradeArrayController;

    [Header("Road Settings")]
    [Tooltip("Road center Z position")]
    [SerializeField] private float roadCenterZ = 0f;

    [Tooltip("Road width (meters)")]
    [SerializeField] private float roadWidth = 15f;

    [Header("Loop Path Settings")]
    [Tooltip("Enable looped path (fold long route into terrain space)")]
    [SerializeField] private bool enableLoopedPath = true;

    [Tooltip("Loop path mapper component")]
    [SerializeField] private LoopPathMapper loopPathMapper;

    [Tooltip("Number of loops for serpentine pattern")]
    [Range(3, 15)]
    [SerializeField] private int loopCount = 5;

    [Header("Terrain Layers")]
    [Tooltip("Grass terrain layer")]
    [SerializeField] private TerrainLayer grassLayer;

    [Tooltip("Road terrain layer")]
    [SerializeField] private TerrainLayer roadLayer;

    [Tooltip("Mountain terrain layer (for steep sections)")]
    [SerializeField] private TerrainLayer mountainLayer;

    private System.Random random;
    private float currentElevation = 0f;

    // Elevation and grade arrays (sampled along route)
    private float[] elevationSamples;
    private float[] gradeSamples;
    private float sampleInterval;

    // Perlin noise offset
    private float noiseOffsetX;
    private float noiseOffsetY;

    // Debug logging helper - simple string-based logging
    private void DebugLog(string hypothesisId, string location, string message, Dictionary<string, object> data = null)
    {
        try
        {
            long timestamp = (long)(System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1))).TotalMilliseconds;
            string dataStr = "{}";
            if (data != null && data.Count > 0)
            {
                List<string> pairs = new List<string>();
                foreach (var kvp in data)
                {
                    string value = kvp.Value == null ? "null" : 
                                   kvp.Value is bool ? kvp.Value.ToString().ToLower() :
                                   kvp.Value is string ? "\"" + kvp.Value.ToString().Replace("\"", "\\\"") + "\"" :
                                   kvp.Value.ToString();
                    pairs.Add($"\"{kvp.Key}\":{value}");
                }
                dataStr = "{" + string.Join(",", pairs) + "}";
            }
            string logLine = $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"{hypothesisId}\",\"location\":\"{location}\",\"message\":\"{message}\",\"data\":{dataStr},\"timestamp\":{timestamp}}}\n";
            System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log", logLine);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[DebugLog] Failed to write log: {e.Message}");
        }
    }

    private void Start()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (routeConfig == null)
        {
            Debug.LogError("[LongRouteTerrainBuilder] Route configuration is not assigned!");
            return;
        }

        // Validate route
        string errorMessage;
        if (!routeConfig.ValidateRoute(out errorMessage))
        {
            Debug.LogError($"[LongRouteTerrainBuilder] Route validation failed: {errorMessage}");
            return;
        }

        BuildRoute();
    }

    /// <summary>
    /// Build the complete route terrain
    /// </summary>
    public void BuildRoute()
    {
        // #region agent log
        DebugLog("E", "LongRouteTerrainBuilder.cs:140", "BuildRoute called", new Dictionary<string, object> {
            {"terrainNotNull", terrain != null},
            {"routeConfigNotNull", routeConfig != null},
            {"enableLoopedPath", enableLoopedPath},
            {"loopPathMapperNotNull", loopPathMapper != null}
        });
        // #endregion

        if (terrain == null || routeConfig == null)
        {
            // #region agent log
            DebugLog("E", "LongRouteTerrainBuilder.cs:143", "BuildRoute early return", new Dictionary<string, object> {
                {"terrainNull", terrain == null},
                {"routeConfigNull", routeConfig == null}
            });
            // #endregion
            return;
        }

        // Initialize random
        // If manual seed was set, use it; otherwise use auto-random if enabled
        if (!manualSeedSet && useRandomSeed)
        {
            randomSeed = Random.Range(0, 100000);
        }
        random = new System.Random(randomSeed);
        manualSeedSet = false; // Reset flag after use

        // #region agent log
        DebugLog("A", "LongRouteTerrainBuilder.cs:154", "Before InitializeLoopMapper check", new Dictionary<string, object> {
            {"enableLoopedPath", enableLoopedPath},
            {"loopPathMapperNotNull", loopPathMapper != null},
            {"loopCount", loopCount}
        });
        // #endregion

        // Initialize loop mapper if enabled
        if (enableLoopedPath)
        {
            InitializeLoopMapper();
        }

        // Clear terrain
        ClearTerrain();

        // Build base rolling terrain with climbs
        BuildRollingTerrainWithClimbs();

        // Setup terrain layers
        SetupTerrainLayers();

        // Paint road
        PaintRoad();

        Debug.Log($"[LongRouteTerrainBuilder] Built route: {routeConfig.routeName}, Length: {routeConfig.totalRouteLength}m, Climbs: {routeConfig.climbSegments.Count}, Looped: {enableLoopedPath}");
    }

    /// <summary>
    /// Initialize loop path mapper
    /// </summary>
    private void InitializeLoopMapper()
    {
        // #region agent log
        DebugLog("B", "LongRouteTerrainBuilder.cs:177", "InitializeLoopMapper called", new Dictionary<string, object> {
            {"loopPathMapperNotNull", loopPathMapper != null}
        });
        // #endregion

        if (loopPathMapper == null)
        {
            loopPathMapper = FindObjectOfType<LoopPathMapper>();
            // #region agent log
            DebugLog("B", "LongRouteTerrainBuilder.cs:182", "FindObjectOfType result", new Dictionary<string, object> {
                {"found", loopPathMapper != null}
            });
            // #endregion

            if (loopPathMapper == null)
            {
                GameObject mapperObj = new GameObject("LoopPathMapper");
                loopPathMapper = mapperObj.AddComponent<LoopPathMapper>();
                // #region agent log
                DebugLog("B", "LongRouteTerrainBuilder.cs:186", "Created new LoopPathMapper", new Dictionary<string, object> {
                    {"created", true}
                });
                // #endregion
            }
        }

        // Get terrain size
        Vector3 terrainSize = terrain != null ? terrain.terrainData.size : new Vector3(500f, 200f, 200f);
        
        // #region agent log
        DebugLog("B", "LongRouteTerrainBuilder.cs:193", "Before Initialize call", new Dictionary<string, object> {
            {"totalRouteLength", routeConfig.totalRouteLength},
            {"terrainSizeX", terrainSize.x},
            {"terrainSizeZ", terrainSize.z},
            {"loopCount", loopCount}
        });
        // #endregion

        // Initialize mapper
        loopPathMapper.Initialize(routeConfig.totalRouteLength, new Vector2(terrainSize.x, terrainSize.z), loopCount);
        loopPathMapper.SetLoopCount(loopCount);
        
        Debug.Log($"[LongRouteTerrainBuilder] Initialized LoopPathMapper: {loopCount} loops, Terrain: {terrainSize.x}m x {terrainSize.z}m");
        
        // #region agent log
        DebugLog("B", "LongRouteTerrainBuilder.cs:197", "LoopPathMapper initialized", new Dictionary<string, object> {
            {"success", true},
            {"loopCount", loopCount}
        });
        // #endregion
    }

    /// <summary>
    /// Clear terrain to flat base
    /// </summary>
    private void ClearTerrain()
    {
        if (terrain == null) return;

        TerrainData data = terrain.terrainData;
        int width = data.heightmapResolution;
        int height = data.heightmapResolution;

        float[,] heights = new float[width, height];

        data.SetHeights(0, 0, heights);
        terrain.Flush();

        currentElevation = baseHeight;
    }

    /// <summary>
    /// Build rolling terrain with discrete climb segments using Perlin noise + bump functions
    /// Algorithm:
    /// 1. Sample Perlin noise for base rolling elevation
    /// 2. Inject climb shapes using bump functions (raised cosine or Gaussian)
    /// 3. Sum contributions (base noise + all climbs)
    /// 4. Smooth with moving average/low-pass filter
    /// 5. Convert elevation -> grade% by approximating slope
    /// </summary>
    private void BuildRollingTerrainWithClimbs()
    {
        if (terrain == null || routeConfig == null) return;

        // Initialize noise offset for Perlin noise
        noiseOffsetX = (float)random.NextDouble() * 10000f;
        noiseOffsetY = (float)random.NextDouble() * 10000f;

        // Step 1: Generate elevation samples using Perlin noise + bump functions
        GenerateElevationProfile();

        // Step 2: Smooth elevation signal (preserve desired steepness)
        if (enableSmoothing)
        {
            SmoothElevationProfile();
        }

        // Step 3: Convert elevation -> grade% by approximating slope
        CalculateGradientFromElevation();

        // Step 4: Apply to terrain heightmap
        ApplyElevationToTerrain();

        // Step 5: Export grade array for HillController and trainer bridge
        if (exportGradeArray)
        {
            ExportGradeArray();
        }

        Debug.Log($"[LongRouteTerrainBuilder] Generated elevation profile: {numSamples} samples, {routeConfig.climbSegments.Count} climbs");
    }

    /// <summary>
    /// Generate elevation profile: Perlin noise (base) + bump functions (climbs)
    /// </summary>
    private void GenerateElevationProfile()
    {
        elevationSamples = new float[numSamples];
        sampleInterval = routeConfig.totalRouteLength / (numSamples - 1);

        // Step 1a: Generate base rolling elevation using Perlin noise
        for (int i = 0; i < numSamples; i++)
        {
            float distance = i * sampleInterval;
            
            // Sample Perlin noise at this distance
            float noiseValue = SamplePerlinNoise(distance);
            float baseElevation = baseHeight + (noiseValue * noiseAmplitude);
            
            elevationSamples[i] = baseElevation;
        }

        // Step 1b: Inject climb shapes using bump functions
        foreach (ClimbSegment climb in routeConfig.climbSegments)
        {
            InjectClimbShape(climb);
        }
    }

    /// <summary>
    /// Sample Perlin noise with multiple octaves
    /// </summary>
    private float SamplePerlinNoise(float distance)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = noiseScale;
        float maxValue = 0f;

        for (int octave = 0; octave < noiseOctaves; octave++)
        {
            float sampleX = (distance * frequency) + noiseOffsetX;
            float sampleY = noiseOffsetY;
            
            float noiseValue = Mathf.PerlinNoise(sampleX, sampleY);
            value += noiseValue * amplitude;
            maxValue += amplitude;

            amplitude *= 0.5f; // Each octave contributes half as much
            frequency *= 2f;   // Each octave has double the frequency
        }

        // Normalize to [-1, 1] range
        return (value / maxValue) * 2f - 1f;
    }

    /// <summary>
    /// Inject a climb shape using a bump function (raised cosine or normalized Gaussian)
    /// Scaled so that the resulting grade (slope) has desired average/peak
    /// </summary>
    private void InjectClimbShape(ClimbSegment climb)
    {
        // Calculate climb parameters
        float climbStart = climb.startLocation;
        float climbLength = climb.length;
        float climbEnd = climb.EndLocation;

        // Calculate desired height gain based on average grade
        // grade% = (height_gain / length) * 100
        // height_gain = (grade% * length) / 100
        float desiredHeightGain = (climb.averageGrade * climbLength) / 100f;

        // Find samples within climb range
        int startSample = Mathf.Max(0, Mathf.FloorToInt(climbStart / sampleInterval));
        int endSample = Mathf.Min(numSamples - 1, Mathf.CeilToInt(climbEnd / sampleInterval));

        // Calculate scale factor to achieve desired average grade
        // For raised cosine bump function f(x) = 0.5*(1+cos(π*(2x-1))) on [0,1]:
        // The average value (integral from 0 to 1) is ∫[0,1] 0.5*(1+cos(π*(2x-1))) dx = 0.5
        // To get average height gain H_avg over length L with average grade G%:
        // H_avg = (G% * L) / 100
        // If we use bump function scaled by S: H(x) = S * f(x)
        // Then average height: H_avg = S * ∫f(x)dx = S * 0.5
        // So: S = H_avg / 0.5 = 2 * H_avg = 2 * (G% * L) / 100
        // But we're adding to base elevation, so the height gain at any point is S * f(x)
        // The average height gain across the climb is S * 0.5, which should equal H_avg
        // Therefore: S = 2 * H_avg = 2 * desiredHeightGain
        float bumpAverage = 0.5f; // Average value of raised cosine bump on [0,1]
        float scaleFactor = desiredHeightGain / bumpAverage; // Scale to achieve desired average

        for (int i = startSample; i <= endSample; i++)
        {
            float distance = i * sampleInterval;
            
            if (distance < climbStart || distance > climbEnd) continue;

            // Normalized position within climb [0, 1]
            float normalizedPos = (distance - climbStart) / climbLength;

            // Apply bump function (raised cosine for smooth transition)
            float bumpValue = RaisedCosineBump(normalizedPos);

            // Alternative: Use normalized Gaussian bump (uncomment to use)
            // float bumpValue = GaussianBump(normalizedPos, 0.5f, climb.undulationAmplitude * 0.1f + 0.15f);

            // Scale bump to achieve desired average grade
            // Use scaleFactor to ensure average grade matches desired
            float heightContribution = bumpValue * scaleFactor;

            // Add undulation if enabled (adds variation within climb)
            if (climb.undulationFrequency > 0f && climb.undulationAmplitude > 0f)
            {
                float undulation = Mathf.Sin(normalizedPos * Mathf.PI * 2f * climb.undulationFrequency * 10f) * climb.undulationAmplitude;
                heightContribution += undulation * (desiredHeightGain * 0.1f); // 10% variation
            }

            // Add to elevation
            elevationSamples[i] += heightContribution;
        }
    }

    /// <summary>
    /// Raised cosine bump function: smooth, bell-shaped curve
    /// Returns value in [0, 1] range, centered at x=0.5
    /// </summary>
    private float RaisedCosineBump(float x)
    {
        // Clamp to [0, 1]
        x = Mathf.Clamp01(x);

        // Raised cosine: 0.5 * (1 + cos(2π * (x - 0.5)))
        // This gives smooth transitions at edges
        float centered = (x - 0.5f) * 2f; // Map [0,1] to [-1,1]
        float cosine = Mathf.Cos(Mathf.PI * centered);
        return 0.5f * (1f + cosine);
    }

    /// <summary>
    /// Normalized Gaussian bump function (alternative to raised cosine)
    /// </summary>
    private float GaussianBump(float x, float center, float width)
    {
        x = Mathf.Clamp01(x);
        float exponent = -0.5f * Mathf.Pow((x - center) / width, 2f);
        float gaussian = Mathf.Exp(exponent);
        // Normalize so peak is at center
        float peak = Mathf.Exp(-0.5f * Mathf.Pow((center - center) / width, 2f));
        return gaussian / peak;
    }

    /// <summary>
    /// Smooth elevation profile using moving average or low-pass filter
    /// Preserves desired steepness if enabled
    /// </summary>
    private void SmoothElevationProfile()
    {
        if (elevationSamples == null || elevationSamples.Length == 0) return;

        float[] smoothed = new float[elevationSamples.Length];
        int halfWindow = smoothingWindowSize / 2;

        for (int i = 0; i < elevationSamples.Length; i++)
        {
            float sum = 0f;
            int count = 0;

            // Moving average window
            for (int j = -halfWindow; j <= halfWindow; j++)
            {
                int index = i + j;
                if (index >= 0 && index < elevationSamples.Length)
                {
                    // Apply weights (can be uniform or Gaussian-weighted)
                    float weight = 1f;
                    if (preserveSteepness && j != 0)
                    {
                        // Reduce smoothing in steep areas (preserve gradients)
                        float localGradient = Mathf.Abs(GetLocalGradient(i));
                        if (localGradient > 5f) // Steep area (>5% grade)
                        {
                            weight *= 0.5f; // Less smoothing in steep areas
                        }
                    }
                    sum += elevationSamples[index] * weight;
                    count++;
                }
            }

            smoothed[i] = sum / count;
        }

        elevationSamples = smoothed;
    }

    /// <summary>
    /// Get local gradient at a sample index (approximate)
    /// </summary>
    private float GetLocalGradient(int sampleIndex)
    {
        if (sampleIndex <= 0 || sampleIndex >= elevationSamples.Length - 1) return 0f;

        float elevationDiff = elevationSamples[sampleIndex + 1] - elevationSamples[sampleIndex - 1];
        float distanceDiff = sampleInterval * 2f; // Distance between samples
        float gradient = (elevationDiff / distanceDiff) * 100f; // Convert to percentage

        return gradient;
    }

    /// <summary>
    /// Convert elevation -> grade% by approximating slope over a small window
    /// </summary>
    private void CalculateGradientFromElevation()
    {
        if (elevationSamples == null || elevationSamples.Length == 0) return;

        gradeSamples = new float[elevationSamples.Length];
        int gradientWindowSamples = Mathf.Max(2, Mathf.CeilToInt(gradientWindowSize / sampleInterval));

        for (int i = 0; i < elevationSamples.Length; i++)
        {
            // Find window boundaries
            int startIndex = Mathf.Max(0, i - gradientWindowSamples / 2);
            int endIndex = Mathf.Min(elevationSamples.Length - 1, i + gradientWindowSamples / 2);

            if (startIndex >= endIndex)
            {
                gradeSamples[i] = 0f;
                continue;
            }

            // Calculate slope (rise over run)
            float elevationStart = elevationSamples[startIndex];
            float elevationEnd = elevationSamples[endIndex];
            float elevationDiff = elevationEnd - elevationStart;
            float distanceDiff = (endIndex - startIndex) * sampleInterval;

            // Convert to grade percentage: (rise / run) * 100
            if (distanceDiff > 0.001f)
            {
                gradeSamples[i] = (elevationDiff / distanceDiff) * 100f;
            }
            else
            {
                gradeSamples[i] = 0f;
            }
        }
    }

    /// <summary>
    /// Apply elevation profile to terrain heightmap
    /// Supports both straight and looped paths
    /// </summary>
    private void ApplyElevationToTerrain()
    {
        if (terrain == null || elevationSamples == null) return;

        TerrainData data = terrain.terrainData;
        int width = data.heightmapResolution;
        int height = data.heightmapResolution;
        Vector3 size = data.size;

        // DON'T change terrain size if using looped path - use existing terrain size
        if (!enableLoopedPath)
        {
            size.x = routeConfig.totalRouteLength;
            size.z = terrainWidth;
            data.size = size;
        }

        float[,] heights = data.GetHeights(0, 0, width, height);

        // Find min/max elevation for normalization
        float minElevation = float.MaxValue;
        float maxElevation = float.MinValue;
        for (int i = 0; i < elevationSamples.Length; i++)
        {
            if (elevationSamples[i] < minElevation) minElevation = elevationSamples[i];
            if (elevationSamples[i] > maxElevation) maxElevation = elevationSamples[i];
        }
        float elevationRange = maxElevation - minElevation;
        if (elevationRange < 0.1f) elevationRange = size.y; // Default range

        // Apply elevation based on path type
        if (enableLoopedPath && loopPathMapper != null)
        {
            ApplyElevationLoopedPath(width, height, size, heights, minElevation, elevationRange);
        }
        else
        {
            ApplyElevationStraightPath(width, height, size, heights, minElevation, elevationRange);
        }

        data.SetHeights(0, 0, heights);
        terrain.Flush();
    }

    /// <summary>
    /// Apply elevation for looped path
    /// </summary>
    private void ApplyElevationLoopedPath(int width, int height, Vector3 size, float[,] heights, float minElevation, float elevationRange)
    {
        // Sample points along the route and apply elevation at corresponding world positions
        int numSamples = elevationSamples.Length;
        float sampleDistance = routeConfig.totalRouteLength / (numSamples - 1);

        // First pass: sample elevation at route points and map to terrain
        for (int i = 0; i < numSamples; i++)
        {
            float distance = i * sampleDistance;
            float elevation = elevationSamples[i];
            float normalizedHeight = (elevation - minElevation) / elevationRange;
            normalizedHeight = Mathf.Clamp01(normalizedHeight);

            // Get world position for this distance along the route
            Vector3 worldPos = loopPathMapper.GetWorldPosition(distance, routeConfig.totalRouteLength, out _);

            // Convert world position to terrain heightmap coordinates
            int terrainX = Mathf.RoundToInt((worldPos.x / size.x) * width);
            int terrainZ = Mathf.RoundToInt((worldPos.z / size.z) * height);
            terrainX = Mathf.Clamp(terrainX, 0, width - 1);
            terrainZ = Mathf.Clamp(terrainZ, 0, height - 1);

            // Apply elevation in a radius around the road (smooth transition)
            float roadRadius = (roadWidth / size.z) * height * 1.5f; // 1.5x road width for smooth blending
            int radiusPixels = Mathf.CeilToInt(roadRadius);

            for (int dx = -radiusPixels; dx <= radiusPixels; dx++)
            {
                for (int dz = -radiusPixels; dz <= radiusPixels; dz++)
                {
                    int x = terrainX + dx;
                    int z = terrainZ + dz;

                    if (x >= 0 && x < width && z >= 0 && z < height)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        float influence = 1f - Mathf.Clamp01(dist / roadRadius);

                        // Blend with existing height (for overlapping segments)
                        float currentHeight = heights[x, z];
                        heights[x, z] = Mathf.Lerp(currentHeight, normalizedHeight, influence);
                    }
                }
            }
        }

        // Second pass: interpolate and fill gaps between route segments
        InterpolateElevationGaps(width, height, size, heights, minElevation, elevationRange);
    }

    /// <summary>
    /// Interpolate elevation gaps for smoother terrain
    /// </summary>
    private void InterpolateElevationGaps(int width, int height, Vector3 size, float[,] heights, float minElevation, float elevationRange)
    {
        // Use a simple blur/interpolation pass to fill gaps
        float[,] smoothedHeights = new float[width, height];
        
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                float sum = 0f;
                float count = 0f;

                // Sample neighboring pixels
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        int nx = x + dx;
                        int nz = z + dz;
                        if (nx >= 0 && nx < width && nz >= 0 && nz < height)
                        {
                            sum += heights[nx, nz];
                            count += 1f;
                        }
                    }
                }

                smoothedHeights[x, z] = count > 0 ? (sum / count) : heights[x, z];
            }
        }

        // Blend original with smoothed (50% blend)
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                heights[x, z] = Mathf.Lerp(heights[x, z], smoothedHeights[x, z], 0.5f);
            }
        }
    }

    /// <summary>
    /// Apply elevation for straight path (original method)
    /// </summary>
    private void ApplyElevationStraightPath(int width, int height, Vector3 size, float[,] heights, float minElevation, float elevationRange)
    {
        float terrainSampleInterval = routeConfig.totalRouteLength / width;

        for (int x = 0; x < width; x++)
        {
            float distance = x * terrainSampleInterval;
            int sampleIndex = Mathf.Clamp(Mathf.RoundToInt(distance / sampleInterval), 0, elevationSamples.Length - 1);

            float elevation = elevationSamples[sampleIndex];
            float normalizedHeight = (elevation - minElevation) / elevationRange;
            normalizedHeight = Mathf.Clamp01(normalizedHeight);

            for (int z = 0; z < height; z++)
            {
                float worldZ = (z / (float)height) * size.z;
                bool isRoadArea = Mathf.Abs(worldZ - roadCenterZ) < (roadWidth / 2f + 1f);

                if (isRoadArea)
                {
                    heights[x, z] = normalizedHeight;
                }
                else
                {
                    float variation = (float)(random.NextDouble() * 0.02f);
                    heights[x, z] = Mathf.Clamp01(normalizedHeight + variation);
                }
            }
        }
    }

    /// <summary>
    /// Export grade array for HillController visualization and trainer bridge
    /// </summary>
    private void ExportGradeArray()
    {
        if (gradeSamples == null || gradeSamples.Length == 0)
        {
            Debug.LogWarning("[LongRouteTerrainBuilder] Grade array is empty. Cannot export.");
            return;
        }

        // Store in RouteConfiguration for access by other systems
        if (routeConfig != null)
        {
            routeConfig.SetCachedGradeArray(gradeSamples, sampleInterval);
            Debug.Log($"[LongRouteTerrainBuilder] Grade array exported to RouteConfiguration: {gradeSamples.Length} samples, Range: {GetMinGrade():F1}% to {GetMaxGrade():F1}%");
        }

        // Try to pass to GradeArrayController if assigned
        if (gradeArrayController == null)
        {
            gradeArrayController = FindObjectOfType<GradeArrayController>();
        }

        if (gradeArrayController != null)
        {
            gradeArrayController.SetGradeArray(gradeSamples, sampleInterval);
            Debug.Log($"[LongRouteTerrainBuilder] Grade array passed to GradeArrayController");
        }

        // Emit event for other systems to subscribe
        OnGradeArrayGenerated?.Invoke(gradeSamples, sampleInterval);
    }

    /// <summary>
    /// Get minimum grade in the array
    /// </summary>
    private float GetMinGrade()
    {
        if (gradeSamples == null || gradeSamples.Length == 0) return 0f;
        float min = float.MaxValue;
        foreach (float grade in gradeSamples)
        {
            if (grade < min) min = grade;
        }
        return min;
    }

    /// <summary>
    /// Get maximum grade in the array
    /// </summary>
    private float GetMaxGrade()
    {
        if (gradeSamples == null || gradeSamples.Length == 0) return 0f;
        float max = float.MinValue;
        foreach (float grade in gradeSamples)
        {
            if (grade > max) max = grade;
        }
        return max;
    }

    /// <summary>
    /// Get grade array (for external access)
    /// </summary>
    public float[] GetGradeArray()
    {
        return gradeSamples;
    }

    /// <summary>
    /// Get elevation array (for external access)
    /// </summary>
    public float[] GetElevationArray()
    {
        return elevationSamples;
    }

    /// <summary>
    /// Get sample interval (meters per sample)
    /// </summary>
    public float GetSampleInterval()
    {
        return sampleInterval;
    }

    // Event for grade array generation
    public System.Action<float[], float> OnGradeArrayGenerated;

    /// <summary>
    /// Setup terrain layers (grass, road, mountain)
    /// </summary>
    private void SetupTerrainLayers()
    {
        if (terrain == null) return;

        TerrainData data = terrain.terrainData;
        List<TerrainLayer> layers = new List<TerrainLayer>();

        // Grass layer (default - must be first)
        if (grassLayer != null)
        {
            layers.Add(grassLayer);
        }
        else
        {
            Debug.LogWarning("[LongRouteTerrainBuilder] Grass layer is not assigned!");
        }

        // Road layer (for painting roads)
        if (roadLayer != null)
        {
            layers.Add(roadLayer);
        }
        else
        {
            Debug.LogWarning("[LongRouteTerrainBuilder] Road layer is not assigned!");
        }

        // Mountain layer (for steep climbs)
        if (mountainLayer != null)
        {
            layers.Add(mountainLayer);
        }

        if (layers.Count > 0)
        {
            data.terrainLayers = layers.ToArray();
            Debug.Log($"[LongRouteTerrainBuilder] Setup terrain layers: {layers.Count} layers (Grass, Road, Mountain)");
        }
        else
        {
            Debug.LogError("[LongRouteTerrainBuilder] No terrain layers assigned! Please assign at least grass and road layers.");
        }
    }

    /// <summary>
    /// Paint road on terrain
    /// Supports both straight and looped paths
    /// </summary>
    private void PaintRoad()
    {
        // #region agent log
        DebugLog("A", "LongRouteTerrainBuilder.cs:913", "PaintRoad called", new Dictionary<string, object> {
            {"terrainNotNull", terrain != null},
            {"roadLayerNotNull", roadLayer != null},
            {"enableLoopedPath", enableLoopedPath},
            {"loopPathMapperNotNull", loopPathMapper != null}
        });
        // #endregion

        if (terrain == null || roadLayer == null)
        {
            // #region agent log
            DebugLog("A", "LongRouteTerrainBuilder.cs:920", "PaintRoad early return", new Dictionary<string, object> {
                {"terrainNull", terrain == null},
                {"roadLayerNull", roadLayer == null}
            });
            // #endregion
            return;
        }

        if (enableLoopedPath && loopPathMapper != null)
        {
            // #region agent log
            DebugLog("A", "LongRouteTerrainBuilder.cs:929", "Calling PaintLoopedRoad", new Dictionary<string, object> {
                {"pathType", "looped"}
            });
            // #endregion
            PaintLoopedRoad();
        }
        else
        {
            // #region agent log
            DebugLog("A", "LongRouteTerrainBuilder.cs:937", "Calling PaintStraightRoad", new Dictionary<string, object> {
                {"pathType", "straight"},
                {"enableLoopedPath", enableLoopedPath},
                {"loopPathMapperNull", loopPathMapper == null}
            });
            // #endregion
            PaintStraightRoad();
        }
    }

    /// <summary>
    /// Paint road for looped path
    /// </summary>
    private void PaintLoopedRoad()
    {
        // #region agent log
        DebugLog("C", "LongRouteTerrainBuilder.cs:950", "PaintLoopedRoad called", new Dictionary<string, object> {
            {"terrainNotNull", terrain != null},
            {"roadLayerNotNull", roadLayer != null},
            {"routeConfigNotNull", routeConfig != null}
        });
        // #endregion

        if (terrain == null || roadLayer == null || routeConfig == null)
        {
            // #region agent log
            DebugLog("C", "LongRouteTerrainBuilder.cs:957", "PaintLoopedRoad early return", new Dictionary<string, object> {
                {"terrainNull", terrain == null},
                {"roadLayerNull", roadLayer == null},
                {"routeConfigNull", routeConfig == null}
            });
            // #endregion
            return;
        }

        TerrainData data = terrain.terrainData;
        int alphamapWidth = data.alphamapWidth;
        int alphamapHeight = data.alphamapHeight;
        Vector3 size = data.size;

        // #region agent log
        DebugLog("D", "LongRouteTerrainBuilder.cs:971", "PaintLoopedRoad terrain data", new Dictionary<string, object> {
            {"alphamapWidth", alphamapWidth},
            {"alphamapHeight", alphamapHeight},
            {"sizeX", size.x},
            {"sizeZ", size.z},
            {"totalRouteLength", routeConfig.totalRouteLength}
        });
        // #endregion

        float[,,] alphaMap = data.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        // Initialize all pixels to grass
        for (int x = 0; x < alphamapWidth; x++)
        {
            for (int z = 0; z < alphamapHeight; z++)
            {
                alphaMap[x, z, 0] = 1f; // Grass layer
                // Clear other layers
                for (int i = 1; i < data.terrainLayers.Length; i++)
                {
                    alphaMap[x, z, i] = 0f;
                }
            }
        }

        // Find layer indices
        int grassIndex = 0;
        int roadIndex = -1;
        int mountainIndex = -1;

        for (int i = 0; i < data.terrainLayers.Length; i++)
        {
            if (data.terrainLayers[i] == roadLayer)
                roadIndex = i;
            else if (data.terrainLayers[i] == mountainLayer)
                mountainIndex = i;
            else if (data.terrainLayers[i] == grassLayer)
                grassIndex = i;
        }

        if (roadIndex == -1)
        {
            // #region agent log
            DebugLog("C", "LongRouteTerrainBuilder.cs:1028", "Road layer not found, early return", new Dictionary<string, object> {
                {"roadIndex", -1}
            });
            // #endregion
            Debug.LogWarning("[LongRouteTerrainBuilder] Road layer not found in terrain layers!");
            return;
        }

        // Calculate road width in pixels
        float roadWidthInPixels = (roadWidth / size.z) * alphamapHeight;
        int halfRoadWidth = Mathf.CeilToInt(roadWidthInPixels / 2f);

        // Sample points along the route (every 5 meters for smooth road)
        float sampleSpacing = 5f; // 5 meters between samples
        int numSamples = Mathf.CeilToInt(routeConfig.totalRouteLength / sampleSpacing);

        // #region agent log
        DebugLog("D", "LongRouteTerrainBuilder.cs:1046", "Before PaintLoopedRoad loop", new Dictionary<string, object> {
            {"roadIndex", roadIndex},
            {"numSamples", numSamples},
            {"sampleSpacing", sampleSpacing},
            {"halfRoadWidth", halfRoadWidth},
            {"loopPathMapperNotNull", loopPathMapper != null}
        });
        // #endregion

        int pixelsPainted = 0;
        for (int i = 0; i < numSamples; i++)
        {
            float distance = i * sampleSpacing;
            if (distance > routeConfig.totalRouteLength) break;

            // Get world position for this distance
            Vector3 worldPos = loopPathMapper.GetWorldPosition(distance, routeConfig.totalRouteLength, out _);

            // Convert to terrain alphamap coordinates
            int terrainX = Mathf.RoundToInt((worldPos.x / size.x) * alphamapWidth);
            int terrainZ = Mathf.RoundToInt((worldPos.z / size.z) * alphamapHeight);
            terrainX = Mathf.Clamp(terrainX, 0, alphamapWidth - 1);
            terrainZ = Mathf.Clamp(terrainZ, 0, alphamapHeight - 1);

            // #region agent log
            if (i == 0 || i == numSamples / 2 || i == numSamples - 1)
            {
                DebugLog("D", "LongRouteTerrainBuilder.cs:1067", "Sample position mapping", new Dictionary<string, object> {
                    {"sampleIndex", i},
                    {"distance", distance},
                    {"worldPosX", worldPos.x},
                    {"worldPosZ", worldPos.z},
                    {"terrainX", terrainX},
                    {"terrainZ", terrainZ}
                });
            }
            // #endregion

            // Get road direction for width calculation
            Vector3 direction = loopPathMapper.GetRoadDirection(distance, routeConfig.totalRouteLength);
            
            // Paint road segment (perpendicular to direction)
            for (int dz = -halfRoadWidth; dz <= halfRoadWidth; dz++)
            {
                // For serpentine paths, road width is perpendicular to direction
                // Calculate perpendicular offset
                Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x).normalized;
                int offsetZ = terrainZ + dz;
                
                if (offsetZ >= 0 && offsetZ < alphamapHeight)
                {
                    // Paint road
                    alphaMap[terrainX, offsetZ, grassIndex] = 0f; // Remove grass
                    alphaMap[terrainX, offsetZ, roadIndex] = 1f; // Add road
                    pixelsPainted++;

                    // Paint nearby segments too for smooth road
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nearbyX = terrainX + dx;
                        if (nearbyX >= 0 && nearbyX < alphamapWidth)
                        {
                            alphaMap[nearbyX, offsetZ, grassIndex] = 0f;
                            alphaMap[nearbyX, offsetZ, roadIndex] = 1f;
                        }
                    }
                }
            }

            // Paint mountain layer on steep climbs
            if (mountainIndex >= 0 && routeConfig != null)
            {
                float gradient = Mathf.Abs(routeConfig.GetGradientAtDistance(distance));
                if (gradient > 8f) // Steep climb
                {
                    float mountainWeight = Mathf.Clamp01((gradient - 8f) / 7f);
                    for (int dz = -halfRoadWidth * 2; dz <= halfRoadWidth * 2; dz++)
                    {
                        int offsetZ = terrainZ + dz;
                        if (offsetZ >= 0 && offsetZ < alphamapHeight)
                        {
                            // Blend mountain texture
                            alphaMap[terrainX, offsetZ, grassIndex] = Mathf.Lerp(alphaMap[terrainX, offsetZ, grassIndex], 0f, mountainWeight * 0.5f);
                            alphaMap[terrainX, offsetZ, mountainIndex] = Mathf.Max(alphaMap[terrainX, offsetZ, mountainIndex], mountainWeight * 0.5f);
                        }
                    }
                }
            }
        }

        data.SetAlphamaps(0, 0, alphaMap);
        terrain.Flush();
        
        // #region agent log
        DebugLog("D", "LongRouteTerrainBuilder.cs:1129", "PaintLoopedRoad completed", new Dictionary<string, object> {
            {"numSamples", numSamples},
            {"pixelsPainted", pixelsPainted},
            {"totalRouteLength", routeConfig.totalRouteLength}
        });
        // #endregion

        Debug.Log($"[LongRouteTerrainBuilder] ✓ Looped road painted! {numSamples} samples along {routeConfig.totalRouteLength:F0}m route, {pixelsPainted} pixels painted");
    }

    /// <summary>
    /// Paint road for straight path (original method)
    /// </summary>
    private void PaintStraightRoad()
    {
        if (terrain == null || roadLayer == null) return;

        TerrainData data = terrain.terrainData;
        int alphamapWidth = data.alphamapWidth;
        int alphamapHeight = data.alphamapHeight;

        float[,,] alphaMap = data.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        // Get road center in alphamap coordinates
        float roadCenterNormalized = (roadCenterZ / terrainWidth) * alphamapHeight;
        float roadWidthNormalized = (roadWidth / terrainWidth) * alphamapHeight;

        for (int x = 0; x < alphamapWidth; x++)
        {
            for (int z = 0; z < alphamapHeight; z++)
            {
                // Default: grass layer (index 0)
                alphaMap[x, z, 0] = 1f;

                // Check if in road area
                float distanceFromRoadCenter = Mathf.Abs(z - roadCenterNormalized);
                if (distanceFromRoadCenter < roadWidthNormalized / 2f)
                {
                    // Road area
                    alphaMap[x, z, 0] = 0f; // No grass
                    if (data.terrainLayers.Length > 1 && roadLayer != null)
                    {
                        // Find road layer index
                        for (int i = 0; i < data.terrainLayers.Length; i++)
                        {
                            if (data.terrainLayers[i] == roadLayer)
                            {
                                alphaMap[x, z, i] = 1f;
                                break;
                            }
                        }
                    }
                }

                // Paint mountain layer on steep climbs
                if (routeConfig != null && data.terrainLayers.Length > 1)
                {
                    float distance = (x / (float)alphamapWidth) * routeConfig.totalRouteLength;
                    float gradient = Mathf.Abs(routeConfig.GetGradientAtDistance(distance));

                    if (gradient > 8f) // Steep climb
                    {
                        // Find mountain layer index
                        for (int i = 0; i < data.terrainLayers.Length; i++)
                        {
                            if (data.terrainLayers[i] == mountainLayer)
                            {
                                float mountainWeight = Mathf.Clamp01((gradient - 8f) / 7f); // 8-15% gradient
                                alphaMap[x, z, 0] = Mathf.Lerp(alphaMap[x, z, 0], 0f, mountainWeight);
                                alphaMap[x, z, i] = Mathf.Lerp(alphaMap[x, z, i], mountainWeight, mountainWeight);
                                break;
                            }
                        }
                    }
                }
            }
        }

        data.SetAlphamaps(0, 0, alphaMap);
        terrain.Flush();
        
        Debug.Log("[LongRouteTerrainBuilder] ✓ Straight road painted!");
    }

    /// <summary>
    /// Set route configuration
    /// </summary>
    public void SetRouteConfiguration(RouteConfiguration config)
    {
        routeConfig = config;
        BuildRoute();
    }

    /// <summary>
    /// Get route configuration
    /// </summary>
    public RouteConfiguration GetRouteConfiguration()
    {
        return routeConfig;
    }

    /// <summary>
    /// Set random seed manually (for trial randomization)
    /// </summary>
    public void SetRandomSeed(int seed)
    {
        randomSeed = seed;
        manualSeedSet = true; // Mark that we're using a manual seed
        useRandomSeed = false; // Disable auto-random when manual seed is set
    }
}
