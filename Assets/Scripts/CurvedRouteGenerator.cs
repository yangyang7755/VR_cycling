using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates curved cycling routes with terrain that follows the road path.
/// Creates a SplinePath with random curves, sculpts terrain along it,
/// and optionally paints a road texture. The bike follows the spline.
/// 
/// SETUP:
/// 1. Add this component to an empty GameObject
/// 2. Assign your Terrain
/// 3. Optionally assign a SplinePath (one will be created if missing)
/// 4. Press Play — terrain is generated and bike follows the curved road
/// </summary>
public class CurvedRouteGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private SplinePath splinePath;
    [SerializeField] private BikeController bikeController;
    
    [Header("Route Shape")]
    [Tooltip("Total route length in meters")]
    [SerializeField] private float routeLength = 2000f;
    
    [Tooltip("Number of control points (more = more curves)")]
    [Range(4, 30)]
    [SerializeField] private int controlPointCount = 12;
    
    [Tooltip("How much the road curves left/right (meters)")]
    [SerializeField] private float curveAmplitude = 60f;
    
    [Tooltip("Minimum turn radius to keep curves rideable (meters)")]
    [SerializeField] private float minTurnRadius = 30f;
    
    [Tooltip("Road style")]
    [SerializeField] private RoadStyle roadStyle = RoadStyle.Winding;
    
    public enum RoadStyle
    {
        Winding,        // Natural winding road with varied curves
        Serpentine,     // Back-and-forth switchbacks (mountain style)
        GentleCurves,   // Long sweeping curves
        Technical        // Tight turns mixed with straights
    }
    
    [Header("Elevation")]
    [Tooltip("Terrain profile for elevation changes")]
    [SerializeField] private ElevationStyle elevationStyle = ElevationStyle.Rolling;
    
    [Tooltip("Maximum elevation change (meters)")]
    [SerializeField] private float maxElevation = 40f;
    
    [Tooltip("Base elevation (meters)")]
    [SerializeField] private float baseElevation = 20f;
    
    public enum ElevationStyle
    {
        Flat,
        Rolling,
        Hilly,
        Mountain,
        Mixed
    }
    
    [Header("Gradient Detail")]
    [Tooltip("Number of distinct gradient segments along the route")]
    [Range(5, 60)]
    [SerializeField] private int gradientSegmentCount = 20;
    
    [Tooltip("Maximum instantaneous gradient (%)")]
    [Range(3f, 20f)]
    [SerializeField] private float maxGradientPercent = 12f;
    
    [Tooltip("Chance of a steep kick within a segment (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float steepKickChance = 0.25f;
    
    [Tooltip("Chance of a flat/false-flat recovery (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float recoveryChance = 0.2f;
    
    [Tooltip("Smoothing passes on the elevation profile")]
    [Range(0, 5)]
    [SerializeField] private int elevationSmoothingPasses = 2;
    
    [Header("Road Surface")]
    [Tooltip("Road width in meters")]
    [SerializeField] private float roadWidth = 12f;
    
    [Tooltip("Shoulder width on each side (meters)")]
    [SerializeField] private float shoulderWidth = 4f;
    
    [Tooltip("Paint road texture on terrain (disable if using road mesh overlay)")]
    [SerializeField] private bool paintRoadTexture = false;
    
    [Tooltip("Road terrain layer index (in terrain layers array)")]
    [SerializeField] private int roadLayerIndex = 1;
    
    [Tooltip("Custom road material (e.g. EasyRoads3D material). If null, uses procedural asphalt.")]
    [SerializeField] private Material customRoadMaterial;
    
    [Header("Visual Gradient Exaggeration")]
    [Tooltip("Multiply the visual elevation by this factor. Real gradient is used for physics/resistance. Higher values make hills visually dramatic.")]
    [Range(1f, 15f)]
    [SerializeField] private float visualGradientMultiplier = 8f;
    
    [Header("Randomization")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int seed = 42;
    
    [Header("Auto-Generate")]
    [Tooltip("Generate a default route on scene start. Set to false when using HillClimbExperiment — it generates per trial.")]
    [SerializeField] private bool generateOnStart = false;
    
    [Header("── Manual Gradient Override ──────────────────────")]
    [Tooltip("Set > 0 to force a specific gradient regardless of ExperimentController. 0 = use random profile.")]
    [SerializeField] private float manualGradientPercent = 0f;
    
    [Tooltip("Flat lead-in before the climb starts (meters)")]
    [SerializeField] private float manualFlatLeadIn = 50f;
    
    [Tooltip("Gradient variation ± (0 = perfectly steady)")]
    [SerializeField] private float manualGradientVariation = 0.3f;
    
    [Tooltip("Rolling (oscillating) vs steady climb")]
    [SerializeField] private bool manualRolling = false;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private int currentSeed;
    private float[] elevationProfile; // per-meter elevation along the route

    private void Awake()
    {
        DisableCompetingGenerators();
    }

    private void Start()
    {
        if (terrain == null) terrain = FindObjectOfType<Terrain>();
        if (bikeController == null) bikeController = FindObjectOfType<BikeController>();

        if (splinePath == null)
        {
            splinePath = GetComponent<SplinePath>();
            if (splinePath == null)
                splinePath = gameObject.AddComponent<SplinePath>();
        }

        if (generateOnStart)
        {
            // Wait two frames so ExperimentController.Awake() has had a chance to call
            // SetTrialGradient() for the first condition before we sculpt the terrain.
            StartCoroutine(GenerateRouteDelayed());
        }
    }

    private System.Collections.IEnumerator GenerateRouteDelayed()
    {
        // Frame 0: all Awake() done, Start() methods running
        yield return null;
        // Frame 1: all Start() done — ExperimentController has pre-set the gradient
        yield return null;
        DisableCompetingGenerators();
        GenerateRoute();
    }

    [ContextMenu("Generate Curved Route")]
    public void GenerateRoute()
    {
        if (terrain == null)
        {
            Debug.LogError("[CurvedRouteGenerator] No terrain assigned!");
            return;
        }

        // Diagnostic: log what gradient will be used
        // Manual override takes priority, then ExperimentController, then random
        if (manualGradientPercent > 0f && trialTargetGradient < 0f)
        {
            trialTargetGradient   = manualGradientPercent;
            trialGradientVariation = manualGradientVariation;
            trialIsRolling        = manualRolling;
            trialFlatSectionLength = manualFlatLeadIn;
            Debug.Log($"[CurvedRouteGenerator] GenerateRoute() — MANUAL gradient={manualGradientPercent}%, flat={manualFlatLeadIn}m, rolling={manualRolling}");
        }
        else if (trialTargetGradient >= 0f)
            Debug.Log($"[CurvedRouteGenerator] GenerateRoute() — CONTROLLED gradient={trialTargetGradient}%, flat={trialFlatSectionLength}m");
        else
            Debug.Log($"[CurvedRouteGenerator] GenerateRoute() — RANDOM profile (elevationStyle={elevationStyle})");

        currentSeed = useRandomSeed ? UnityEngine.Random.Range(0, 999999) : seed;
        UnityEngine.Random.InitState(currentSeed);

        if (terrain != null)
        {
            float terrainWidth = terrain.terrainData.size.x;
            // If terrain is too small for the route, expand it
            if (terrainWidth < routeLength + 20f)
            {
                Vector3 newSize = terrain.terrainData.size;
                newSize.x = routeLength + 100f; // add 100m margin
                terrain.terrainData.size = newSize;
                Debug.Log($"[CurvedRouteGenerator] Terrain expanded to {newSize.x}m wide for {routeLength}m route");
            }
        }

        // 1. Generate 2D control points and build spline
        List<Vector3> points = GenerateControlPoints();
        splinePath.SetControlPoints(points);

        // 2. Iteratively scale control points until spline length = exactly routeLength
        for (int attempt = 0; attempt < 5; attempt++)
        {
            float actualLen = splinePath.GetTotalLength();
            if (Mathf.Abs(actualLen - routeLength) < 1f) break; // close enough
            
            float scale = routeLength / actualLen;
            Vector3 origin = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 offset = points[i] - origin;
                points[i] = origin + offset * scale;
            }
            splinePath.SetControlPoints(points);
        }
        float finalLen = splinePath.GetTotalLength();
        Debug.Log($"[CurvedRouteGenerator] Spline length: {finalLen:F0}m (target {routeLength:F0}m)");

        // 3. Build elevation profile matched to spline length
        float splineLen = splinePath.GetTotalLength();
        elevationProfile = GenerateDetailedElevationProfile(Mathf.CeilToInt(splineLen));

        // 3b. Create visually exaggerated profile for terrain/road (physics uses original)
        float[] visualProfile = new float[elevationProfile.Length];
        float profileBase = elevationProfile[0];
        for (int i = 0; i < elevationProfile.Length; i++)
        {
            float rise = elevationProfile[i] - profileBase;
            visualProfile[i] = profileBase + rise * visualGradientMultiplier;
        }

        // 4. Apply VISUAL elevation to control points (terrain looks steeper)
        ApplyElevationToControlPoints(points, visualProfile);
        splinePath.SetControlPoints(points);

        // 5. Sculpt terrain using visual profile
        SculptTerrainAlongPath(visualProfile);
        terrain.terrainData.SyncHeightmap();
        terrain.Flush();

        // Verify terrain position after sculpting
        Debug.Log($"[CurvedRouteGenerator] Post-sculpt: terrain.position.y={terrain.transform.position.y:F2}, " +
                  $"terrainData.size={terrain.terrainData.size}, " +
                  $"spline Y at start={splinePath.SampleAtDistance(0f).position.y:F2}, " +
                  $"spline Y at end={splinePath.SampleAtDistance(splineLen).position.y:F2}");

        if (paintRoadTexture) PaintRoadOnTerrain();

        // 6. Road mesh — built from spline Y directly (not terrain.SampleHeight)
        //    so it always matches the elevation profile regardless of heightmap timing
        GenerateRoadMesh();

        // 7. Position bike
        PositionBikeAtStart();

        // 8. Set course distance to exact route length
        if (bikeController != null)
            bikeController.SetMaxCourseDistance(splineLen);

        NotifyTerrainChanged();

        // 9. Spawn roadside objects along the curved path (grounded to sculpted terrain)
        SpawnRoadsideAssets();

        if (showDebugInfo)
            LogRouteStatistics();
    }
    
    private void DisableCompetingGenerators()
    {
        // Disable ALL terrain generators so nothing overwrites our curved terrain
        DisableIfExists<ProceduralRouteGenerator>();
        DisableIfExists<ImprovedTerrainGenerator>();
        DisableIfExists<SimpleTerrainBuilder>();
        DisableIfExists<HillTrialManager>();
        DisableIfExists<StraightRoadRenderer>();
        DisableIfExists<EnhancedTerrainManager>();
        DisableIfExists<DynamicTerrainRandomizer>();
        DisableIfExists<ContinuousTerrainSystem>();
        DisableIfExists<SeamlessTerrainLoop>();
        DisableIfExists<ImprovedCyclingSetup>();
        DisableIfExists<CurvatureIllusion>();
        DisableIfExists<EnhancedSlopeVisualizer>();
        DisableIfExists<LongRouteTerrainBuilder>();
        DisableIfExists<CompleteSceneReset>();

        // Destroy any existing road meshes from other generators
        string[] oldRoadNames = { "Road", "StraightRoad", "RoadMesh", "RoadSurface", "GeneratedRoad" };
        foreach (string name in oldRoadNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Destroy(obj);
                Debug.Log($"[CurvedRouteGenerator] Destroyed old road object: {name}");
            }
        }

        // CRITICAL: Stop ConfigurableTrialSequence from regenerating terrain on trial start
        var trialSeq = FindObjectOfType<ConfigurableTrialSequence>();
        if (trialSeq != null)
        {
            var field = typeof(ConfigurableTrialSequence).GetField("regenerateTerrainBetweenTrials",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(trialSeq, false);
                Debug.Log("[CurvedRouteGenerator] Disabled terrain regeneration in trial sequence");
            }
        }
    }

    private void DisableIfExists<T>() where T : MonoBehaviour
    {
        T comp = FindObjectOfType<T>();
        if (comp != null && comp.enabled)
        {
            comp.enabled = false;
            
            // Also clear any mesh this component generated
            MeshFilter mf = comp.GetComponent<MeshFilter>();
            if (mf != null && mf.mesh != null)
            {
                mf.mesh = null;
            }
            MeshRenderer meshRend = comp.GetComponent<MeshRenderer>();
            if (meshRend != null)
            {
                meshRend.enabled = false;
            }
            
            Debug.Log($"[CurvedRouteGenerator] Disabled {typeof(T).Name}");
        }
    }
    
    private void LogRouteStatistics()
    {
        float splineLen = splinePath.GetTotalLength();
        float totalClimb = 0f, totalDescent = 0f;
        float minGrad = 0f, maxGrad = 0f;
        
        if (elevationProfile != null && elevationProfile.Length > 1)
        {
            for (int i = 1; i < elevationProfile.Length; i++)
            {
                float diff = elevationProfile[i] - elevationProfile[i - 1];
                if (diff > 0) totalClimb += diff;
                else totalDescent += Mathf.Abs(diff);
                
                float grad = diff * 100f; // per-meter, so gradient = diff * 100%
                if (grad < minGrad) minGrad = grad;
                if (grad > maxGrad) maxGrad = grad;
            }
        }
        
        Debug.Log($"[CurvedRouteGenerator] Route generated!");
        Debug.Log($"  Seed: {currentSeed}, Style: {roadStyle}, Elevation: {elevationStyle}");
        Debug.Log($"  Spline length: {splineLen:F0}m, Control points: {controlPointCount}");
        Debug.Log($"  Total climb: {totalClimb:F1}m, Total descent: {totalDescent:F1}m");
        Debug.Log($"  Gradient range: {minGrad:F1}% to {maxGrad:F1}%");
    }
    
    private List<Vector3> GenerateControlPoints()
    {
        List<Vector3> points = new List<Vector3>();
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        
        float centerZ = terrainPos.z + terrainSize.z * 0.5f;
        float startX = terrainPos.x + 20f;
        
        float availableX = terrainSize.x - 40f;
        float xScale = Mathf.Min(1f, availableX / routeLength);
        
        float maxAmplitude = (terrainSize.z * 0.5f) - roadWidth - shoulderWidth - 10f;
        float amplitude = Mathf.Min(curveAmplitude, maxAmplitude);
        
        for (int i = 0; i < controlPointCount; i++)
        {
            float t = (float)i / (controlPointCount - 1);
            float x = startX + t * routeLength * xScale;
            float z = centerZ + GetLateralOffset(t, i, amplitude);
            float y = baseElevation; // flat for now — elevation applied later from profile
            
            float minZ = terrainPos.z + roadWidth + shoulderWidth + 5f;
            float maxZ = terrainPos.z + terrainSize.z - roadWidth - shoulderWidth - 5f;
            z = Mathf.Clamp(z, minZ, maxZ);
            
            // Clamp X within terrain bounds
            x = Mathf.Clamp(x, terrainPos.x + 10f, terrainPos.x + terrainSize.x - 10f);
            
            points.Add(new Vector3(x, y, z));
        }
        
        points = EnforceMinTurnRadius(points);
        return points;
    }
    
    private float GetLateralOffset(float t, int index, float amplitude)
    {
        switch (roadStyle)
        {
            case RoadStyle.Winding:
                // Multi-frequency sine waves + random offsets
                float wave1 = Mathf.Sin(t * Mathf.PI * 2f * 1.5f) * amplitude * 0.6f;
                float wave2 = Mathf.Sin(t * Mathf.PI * 2f * 3.2f + 1.3f) * amplitude * 0.3f;
                float noise = (UnityEngine.Random.value - 0.5f) * amplitude * 0.4f;
                return wave1 + wave2 + noise;
                
            case RoadStyle.Serpentine:
                // Alternating left-right with increasing amplitude
                float dir = (index % 2 == 0) ? 1f : -1f;
                return dir * amplitude * UnityEngine.Random.Range(0.5f, 1f);
                
            case RoadStyle.GentleCurves:
                // Long sweeping curves
                return Mathf.Sin(t * Mathf.PI * 2f * 0.8f) * amplitude * 0.7f +
                       Mathf.Sin(t * Mathf.PI * 2f * 0.3f + 2f) * amplitude * 0.3f;
                
            case RoadStyle.Technical:
                // Mix of tight turns and straights
                if (UnityEngine.Random.value > 0.4f)
                    return (UnityEngine.Random.value - 0.5f) * amplitude * 1.2f;
                else
                    return 0f; // Straight section
                    
            default:
                return 0f;
        }
    }
    
    private float GetElevationOffset(float t, int index)
    {
        // Legacy — no longer used directly. Elevation comes from GenerateDetailedElevationProfile.
        return 0f;
    }
    
    /// <summary>
    /// Generate a per-meter elevation profile with rich gradient variation.
    /// Builds gradient segments first (what gradient to hold for how long),
    /// then integrates to get elevation. This produces realistic road feel:
    /// steep kicks, false flats, rollers, recovery dips.
    /// </summary>
    private float[] GenerateDetailedElevationProfile(int sampleCount)
    {
        // If a trial gradient is set by ExperimentController, use controlled generation
        if (trialTargetGradient >= 0f)
        {
            Debug.Log($"[CurvedRouteGenerator] Using CONTROLLED profile: target={trialTargetGradient}%, flat={trialFlatSectionLength}m, rolling={trialIsRolling}");
            return GenerateControlledElevationProfile(sampleCount);
        }

        // Otherwise use the default random generation
        Debug.Log($"[CurvedRouteGenerator] Using RANDOM profile (no trial gradient set)");
        return GenerateRandomElevationProfile(sampleCount);
    }

    /// <summary>
    /// Generate elevation profile with a specific target average gradient.
    /// For "Steady/Hill" profiles: gradient stays close to target with minimal variation.
    /// For "Rolling" profiles: gradient oscillates around the target average.
    /// The actual average gradient of the output matches the target precisely.
    /// </summary>
    private float[] GenerateControlledElevationProfile(int sampleCount)
    {
        float[] profile = new float[sampleCount];
        float targetGradFraction = trialTargetGradient / 100f;
        float variationFraction = trialGradientVariation / 100f;

        // Flat section: first N meters at 0% gradient
        int flatSamples = Mathf.Clamp(Mathf.RoundToInt(trialFlatSectionLength), 0, sampleCount - 1);

        profile[0] = baseElevation;

        // Phase 1: flat section
        for (int i = 1; i <= flatSamples && i < sampleCount; i++)
        {
            profile[i] = baseElevation; // exactly flat
        }

        // Phase 2: hill section
        int hillStart = flatSamples;
        int hillSamples = sampleCount - hillStart;

        if (hillSamples <= 0)
        {
            trialTargetGradient = -1f;
            return profile;
        }

        // Use fixed seed for reproducibility
        UnityEngine.Random.InitState(currentSeed);

        if (trialIsRolling)
        {
            float waveFreq = 0.006f; // fixed frequency for reproducibility
            float phase = 0f; // fixed phase

            for (int i = hillStart + 1; i < sampleCount; i++)
            {
                int hillI = i - hillStart;
                float grad = targetGradFraction;
                float wave = Mathf.Sin(hillI * waveFreq * Mathf.PI * 2f + phase) * variationFraction * 0.5f;
                grad += wave;
                if (targetGradFraction > 0.01f)
                    grad = Mathf.Max(grad, 0.005f);
                profile[i] = profile[i - 1] + grad;
            }
        }
        else
        {
            for (int i = hillStart + 1; i < sampleCount; i++)
            {
                float grad = targetGradFraction;
                float noise = (Mathf.PerlinNoise(i * 0.01f + currentSeed, 5f) - 0.5f) * 0.002f;
                grad += noise;
                profile[i] = profile[i - 1] + grad;
            }
        }

        // Correct hill section to match target average gradient exactly
        if (hillSamples > 1)
        {
            float actualHillRise = profile[sampleCount - 1] - profile[hillStart];
            float targetHillRise = targetGradFraction * (hillSamples - 1);
            float correction = (targetHillRise - actualHillRise) / (hillSamples - 1);
            for (int i = hillStart + 1; i < sampleCount; i++)
            {
                profile[i] += correction * (i - hillStart);
            }
        }

        // Ensure positive elevation
        float minElev = float.MaxValue;
        for (int i = 0; i < sampleCount; i++)
            if (profile[i] < minElev) minElev = profile[i];
        if (minElev < 5f)
            for (int i = 0; i < sampleCount; i++)
                profile[i] += (5f - minElev);

        // Log
        float flatGrad = flatSamples > 1 ? (profile[flatSamples] - profile[0]) / flatSamples * 100f : 0f;
        float hillGrad = hillSamples > 1 ? (profile[sampleCount-1] - profile[hillStart]) / (hillSamples-1) * 100f : 0f;
        Debug.Log($"[CurvedRouteGenerator] Profile: flat={flatSamples}m ({flatGrad:F2}%), hill={hillSamples}m ({hillGrad:F2}%), rolling={trialIsRolling}");

        // Reset so a stale gradient isn't reused if GenerateRoute is called again without SetTrialGradient
        trialTargetGradient = -1f;
        return profile;
    }

    /// <summary>
    /// Original random elevation profile generation (used when no trial gradient is set).
    /// </summary>
    private float[] GenerateRandomElevationProfile(int sampleCount)
    {
        float[] profile = new float[sampleCount];
        float[] gradients = new float[sampleCount];

        int segCount = gradientSegmentCount;
        float metersPerSeg = (float)sampleCount / segCount;

        for (int seg = 0; seg < segCount; seg++)
        {
            int startIdx = Mathf.RoundToInt(seg * metersPerSeg);
            int endIdx = Mathf.Min(Mathf.RoundToInt((seg + 1) * metersPerSeg), sampleCount);

            float segGradient = GenerateSegmentGradient(seg, segCount);

            for (int i = startIdx; i < endIdx; i++)
            {
                float micro = (Mathf.PerlinNoise(i * 0.05f + currentSeed * 0.3f, seg * 3.7f) - 0.5f) * 0.02f;
                gradients[i] = segGradient + micro;
            }
        }

        // Smooth
        for (int pass = 0; pass < elevationSmoothingPasses; pass++)
        {
            float[] smoothed = new float[sampleCount];
            int window = Mathf.Max(3, Mathf.RoundToInt(metersPerSeg * 0.3f));
            for (int i = 0; i < sampleCount; i++)
            {
                float sum = 0f; int count = 0;
                for (int j = -window; j <= window; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < sampleCount) { sum += gradients[idx]; count++; }
                }
                smoothed[i] = sum / count;
            }
            gradients = smoothed;
        }

        float maxGrad = maxGradientPercent / 100f;
        for (int i = 0; i < sampleCount; i++)
            gradients[i] = Mathf.Clamp(gradients[i], -maxGrad, maxGrad);

        profile[0] = baseElevation;
        for (int i = 1; i < sampleCount; i++)
            profile[i] = profile[i - 1] + gradients[i];

        float minE = float.MaxValue;
        for (int i = 0; i < sampleCount; i++)
            if (profile[i] < minE) minE = profile[i];
        if (minE < 5f)
            for (int i = 0; i < sampleCount; i++)
                profile[i] += (5f - minE);

        return profile;
    }
    
    /// <summary>
    /// Generate the gradient (as a fraction, e.g. 0.05 = 5%) for one segment,
    /// based on the elevation style and randomization.
    /// </summary>
    private float GenerateSegmentGradient(int segIndex, int totalSegments)
    {
        float t = (float)segIndex / (totalSegments - 1);
        float roll = UnityEngine.Random.value;
        
        // Base gradient from elevation style
        float baseGrad = 0f;
        switch (elevationStyle)
        {
            case ElevationStyle.Flat:
                baseGrad = (UnityEngine.Random.value - 0.5f) * 0.02f; // -1% to +1%
                break;
                
            case ElevationStyle.Rolling:
                // Alternating up/down with varied intensity
                baseGrad = Mathf.Sin(t * Mathf.PI * 2f * 3f + currentSeed) * 0.05f;
                baseGrad += (UnityEngine.Random.value - 0.5f) * 0.03f;
                break;
                
            case ElevationStyle.Hilly:
                baseGrad = Mathf.Sin(t * Mathf.PI * 2f * 2f + currentSeed) * 0.07f;
                baseGrad += (UnityEngine.Random.value - 0.5f) * 0.04f;
                break;
                
            case ElevationStyle.Mountain:
                // Net uphill trend with undulations
                baseGrad = 0.03f + Mathf.Sin(t * Mathf.PI * 4f) * 0.04f;
                baseGrad += (UnityEngine.Random.value - 0.5f) * 0.02f;
                // Descent in last 20%
                if (t > 0.8f) baseGrad = -0.04f + (UnityEngine.Random.value - 0.5f) * 0.03f;
                break;
                
            case ElevationStyle.Mixed:
                // Random mix of everything
                float style = UnityEngine.Random.value;
                if (style < 0.3f)
                    baseGrad = (UnityEngine.Random.value - 0.5f) * 0.02f; // flat
                else if (style < 0.6f)
                    baseGrad = (UnityEngine.Random.value - 0.3f) * 0.08f; // climb
                else
                    baseGrad = -(UnityEngine.Random.value * 0.06f); // descent
                break;
        }
        
        // Steep kick override
        if (roll < steepKickChance && elevationStyle != ElevationStyle.Flat)
        {
            float kickGrad = UnityEngine.Random.Range(0.06f, maxGradientPercent / 100f);
            if (UnityEngine.Random.value < 0.3f) kickGrad *= -1f; // 30% chance downhill kick
            baseGrad = kickGrad;
        }
        
        // Recovery/false-flat override
        if (roll > (1f - recoveryChance))
        {
            baseGrad = (UnityEngine.Random.value - 0.5f) * 0.01f; // nearly flat
        }
        
        return baseGrad;
    }
    
    /// <summary>
    /// Map the per-meter elevation profile onto the spline control points.
    /// Each control point gets the elevation at its distance along the spline.
    /// </summary>
    private void ApplyElevationToControlPoints(List<Vector3> points, float[] profile = null)
    {
        float[] useProfile = profile ?? elevationProfile;
        if (useProfile == null || useProfile.Length == 0) return;
        
        float totalLen = splinePath.GetTotalLength();
        
        for (int i = 0; i < points.Count; i++)
        {
            float t = (float)i / (points.Count - 1);
            float dist = t * totalLen;
            
            int profileIdx = Mathf.Clamp(Mathf.RoundToInt(dist), 0, useProfile.Length - 1);
            float elevation = useProfile[profileIdx];
            
            points[i] = new Vector3(points[i].x, elevation, points[i].z);
        }
    }

    private List<Vector3> EnforceMinTurnRadius(List<Vector3> points)
    {
        // Iteratively smooth points that create turns tighter than minTurnRadius
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 prev = points[i - 1];
                Vector3 curr = points[i];
                Vector3 next = points[i + 1];
                
                Vector3 d1 = (curr - prev).normalized;
                Vector3 d2 = (next - curr).normalized;
                
                // Approximate turn radius from angle change and segment length
                float angle = Vector3.Angle(d1, d2);
                float segLen = Vector3.Distance(prev, curr);
                
                if (angle > 1f)
                {
                    float radius = segLen / (2f * Mathf.Sin(angle * Mathf.Deg2Rad * 0.5f));
                    
                    if (radius < minTurnRadius)
                    {
                        // Smooth toward midpoint of neighbors
                        Vector3 midpoint = (prev + next) * 0.5f;
                        float smoothFactor = 0.3f;
                        points[i] = Vector3.Lerp(curr, midpoint, smoothFactor);
                    }
                }
            }
        }
        
        return points;
    }
    
    /// <summary>
    /// Sculpt the ENTIRE terrain heightmap to follow the spline path elevation.
    /// Every point on the terrain is set to the elevation of the nearest road point,
    /// with gentle noise added further from the road. This ensures the ground level
    /// always matches the road — no flat plains at the wrong height.
    /// </summary>
    private void SculptTerrainAlongPath(float[] visualProfile = null)
    {
        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;
        float[,] heights = new float[res, res];
        Vector3 terrainPos = terrain.transform.position;

        float totalLen = splinePath.GetTotalLength();
        float totalRoadWidth = roadWidth + shoulderWidth * 2f;

        // Sample the spline to find the actual Y range — this is authoritative since
        // both the road mesh and terrain sculpting will use spline Y directly.
        float profileMin = float.MaxValue, profileMax = float.MinValue;
        int previewSamples = Mathf.CeilToInt(totalLen / 5f) + 1;
        for (int i = 0; i < previewSamples; i++)
        {
            float d = Mathf.Min(i * 5f, totalLen);
            float y = splinePath.SampleAtDistance(d).position.y;
            if (y < profileMin) profileMin = y;
            if (y > profileMax) profileMax = y;
        }
        // Fallback if spline has no elevation yet
        if (profileMin > profileMax) { profileMin = baseElevation; profileMax = baseElevation + 10f; }

        // FORCE terrain Y size to tightly fit the visual profile
        // This ensures the full heightmap resolution is used for the visible elevation range
        float profileRange = profileMax - profileMin;
        if (profileRange < 1f) profileRange = 1f;
        float desiredTerrainY = profileRange * 1.5f; // 50% margin above and below
        if (desiredTerrainY < 50f) desiredTerrainY = 50f;
        
        Vector3 newSize = td.size;
        newSize.y = desiredTerrainY;
        td.size = newSize;
        
        // Position terrain Y so the profile sits in the middle
        float terrainBaseY = profileMin - (desiredTerrainY - profileRange) * 0.25f;
        terrain.transform.position = new Vector3(terrainPos.x, terrainBaseY, terrainPos.z);
        terrainPos = terrain.transform.position;
        
        float terrainMaxY = desiredTerrainY;

        // Map: normalize profile to fill 15%-85% of the terrain height
        float targetLow = 0.15f;
        float targetHigh = 0.85f;

        Debug.Log($"[CurvedRouteGenerator] Sculpt: visual profile {profileMin:F1}m → {profileMax:F1}m " +
                  $"(range={profileRange:F1}m, ×{visualGradientMultiplier}), " +
                  $"terrain Y={desiredTerrainY:F0}m at base={terrainBaseY:F1}m");

        // Pre-sample spline positions every 2m — use spline Y directly as ground truth.
        // This is the ONLY reliable source: profile[dist] indexed by arc length diverges
        // from spline Y because arc length accounts for vertical distance (3D path length
        // is longer than horizontal distance). Sampling s.position.y ensures terrain
        // height matches the road mesh exactly at every point.
        int splineSamples = Mathf.CeilToInt(totalLen / 2f) + 1;
        Vector3[] splinePositions = new Vector3[splineSamples];
        float[] splineNormHeights = new float[splineSamples];

        for (int i = 0; i < splineSamples; i++)
        {
            float dist = Mathf.Min(i * 2f, totalLen);
            SplinePath.SplineSample s = splinePath.SampleAtDistance(dist);
            splinePositions[i] = new Vector3(s.position.x, 0f, s.position.z);

            // Use spline's own Y — this is what the road mesh uses, so terrain will match
            float worldElev = s.position.y;

            float normH = (worldElev - terrainPos.y) / terrainMaxY;
            normH = Mathf.Clamp(normH, 0.01f, 0.99f);
            splineNormHeights[i] = normH;
        }

        Debug.Log($"[CurvedRouteGenerator] Terrain aligned to spline: world Y " +
                  $"{terrainPos.y + splineNormHeights[0]*terrainMaxY:F1}m → " +
                  $"{terrainPos.y + splineNormHeights[splineSamples-1]*terrainMaxY:F1}m");

        // --- Build entire heightmap ---
        Vector3 terrainSize = td.size; // re-read after resize
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float worldX = terrainPos.x + ((float)x / (res - 1)) * terrainSize.x;
                float worldZ = terrainPos.z + ((float)z / (res - 1)) * terrainSize.z;
                Vector3 worldPt = new Vector3(worldX, 0f, worldZ);

                // Find nearest spline sample
                float nearestSqDist = float.MaxValue;
                float nearestRoadH = splineNormHeights[0];
                for (int i = 0; i < splineSamples; i++)
                {
                    float d = (worldPt - splinePositions[i]).sqrMagnitude;
                    if (d < nearestSqDist)
                    {
                        nearestSqDist = d;
                        nearestRoadH = splineNormHeights[i];
                    }
                }
                float nearestDist = Mathf.Sqrt(nearestSqDist);

                // Small terrain noise for visual variety
                float nx = (float)x / (res - 1);
                float nz = (float)z / (res - 1);
                float noise = Mathf.PerlinNoise(nx * 3f + currentSeed, nz * 3f) * 0.015f
                            + Mathf.PerlinNoise(nx * 7f + currentSeed + 50f, nz * 7f) * 0.005f;

                float blendStart = totalRoadWidth * 0.5f;
                float blendEnd = blendStart + 30f;

                if (nearestDist < blendStart)
                {
                    // Road surface — exact height, no noise
                    heights[z, x] = nearestRoadH;
                }
                else if (nearestDist < blendEnd)
                {
                    float blend = (nearestDist - blendStart) / (blendEnd - blendStart);
                    blend = blend * blend * (3f - 2f * blend);
                    heights[z, x] = nearestRoadH + noise * blend;
                }
                else
                {
                    heights[z, x] = nearestRoadH + noise;
                }
            }
        }

        td.SetHeights(0, 0, heights);
        ClearTerrainAlphamap();

        Debug.Log("[CurvedRouteGenerator] Terrain sculpted — ground follows road elevation.");
    }

    private void ClearTerrainAlphamap()
    {
        TerrainData td = terrain.terrainData;
        int alphaRes = td.alphamapWidth;
        int layerCount = td.terrainLayers != null ? td.terrainLayers.Length : 0;

        if (layerCount == 0) return;

        float[,,] alphas = td.GetAlphamaps(0, 0, alphaRes, alphaRes);

        // Set everything to first layer (grass) — removes old road painting
        for (int z = 0; z < alphaRes; z++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                for (int l = 0; l < layerCount; l++)
                {
                    alphas[z, x, l] = (l == 0) ? 1f : 0f;
                }
            }
        }

        td.SetAlphamaps(0, 0, alphas);
        Debug.Log("[CurvedRouteGenerator] Cleared old terrain alphamap (removed straight road)");
    }
    
    /// <summary>
    /// Paint road texture along the spline path on the terrain alphamap.
    /// </summary>
    private void PaintRoadOnTerrain()
    {
        TerrainData td = terrain.terrainData;
        
        if (td.terrainLayers == null || td.terrainLayers.Length <= roadLayerIndex)
        {
            Debug.LogWarning("[CurvedRouteGenerator] Road layer index out of range. Skipping road painting.");
            return;
        }
        
        int alphaRes = td.alphamapWidth;
        float[,,] alphas = td.GetAlphamaps(0, 0, alphaRes, alphaRes);
        int layerCount = td.terrainLayers.Length;
        
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = td.size;
        float totalLen = splinePath.GetTotalLength();
        float sampleStep = 0.5f;
        float halfRoad = (roadWidth + shoulderWidth) * 0.5f;
        
        for (float dist = 0f; dist <= totalLen; dist += sampleStep)
        {
            SplinePath.SplineSample sample = splinePath.SampleAtDistance(dist);
            Vector3 worldPos = sample.position;
            Vector3 right = sample.right;
            
            for (float offset = -halfRoad; offset <= halfRoad; offset += 0.5f)
            {
                Vector3 point = worldPos + right * offset;
                
                float ax = (point.z - terrainPos.z) / terrainSize.z * (alphaRes - 1);
                float az = (point.x - terrainPos.x) / terrainSize.x * (alphaRes - 1);
                
                int ix = Mathf.RoundToInt(ax);
                int iz = Mathf.RoundToInt(az);
                
                if (ix < 0 || ix >= alphaRes || iz < 0 || iz >= alphaRes) continue;
                
                // Set road layer to 1, others to 0
                for (int l = 0; l < layerCount; l++)
                {
                    alphas[ix, iz, l] = (l == roadLayerIndex) ? 1f : 0f;
                }
            }
        }
        
        td.SetAlphamaps(0, 0, alphas);
    }
    
    private void PositionBikeAtStart()
    {
        if (bikeController == null || splinePath == null) return;

        // CRITICAL: Assign the spline path to the bike controller
        bikeController.SetSplinePath(splinePath);

        SplinePath.SplineSample start = splinePath.SampleAtDistance(0f);

        // Position the actual bike object, not just the controller
        GameObject bikeObj = bikeController.GetBikeObject();
        Transform bikeTransform = bikeObj != null ? bikeObj.transform : bikeController.transform;

        Vector3 startPos = start.position;
        if (terrain != null)
        {
            float terrainH = terrain.SampleHeight(startPos);
            startPos.y = terrainH + 0.5f;
        }

        bikeTransform.position = startPos;
        if (start.forward.sqrMagnitude > 0.01f)
        {
            bikeTransform.rotation = Quaternion.LookRotation(start.forward, Vector3.up);
        }

        // Also position the controller's parent if different
        if (bikeController.transform != bikeTransform)
        {
            bikeController.transform.position = startPos;
        }

        bikeController.ResetCourse();
        bikeController.SetMaxCourseDistance(splinePath.GetTotalLength());

        // Snap camera to follow the bike immediately
        CyclistCamera cam = FindObjectOfType<CyclistCamera>();
        if (cam != null)
        {
            Vector3 camPos = startPos - start.forward * 6f + Vector3.up * 3f;
            cam.transform.position = camPos;
            cam.transform.LookAt(startPos + Vector3.up * 1.5f);
        }

        Debug.Log($"[CurvedRouteGenerator] Bike positioned at {startPos}, facing {start.forward}");
    }
    
    private void SpawnRoadsideAssets()
    {
        // Clear old roadside objects from previous trial
        RoadsideEnvironment roadsideEnv = FindObjectOfType<RoadsideEnvironment>();
        if (roadsideEnv != null)
        {
            roadsideEnv.ClearAll();
            Debug.Log("[CurvedRouteGenerator] Cleared previous roadside objects");
        }

        TerrainAssetRandomizer assetRandomizer = FindObjectOfType<TerrainAssetRandomizer>();
        if (assetRandomizer != null)
        {
            assetRandomizer.SetSplinePath(splinePath);
            assetRandomizer.SpawnAlongRoute();
            Debug.Log("[CurvedRouteGenerator] Triggered roadside asset placement along spline");
        }
    }

    private void NotifyTerrainChanged()
    {
        ElevationProgressBar bar = FindObjectOfType<ElevationProgressBar>();
        if (bar != null)
        {
            bar.SetTotalRouteLength(splinePath.GetTotalLength());
        }
    }
    
    private void GenerateRoadMesh()
    {
        // Clean up ALL old road objects
        string[] roadNames = { "CurvedRoad", "Road", "StraightRoad", "RoadMesh", "RoadSurface", "road" };
        foreach (string name in roadNames)
        {
            GameObject old = GameObject.Find(name);
            if (old != null) Destroy(old);
        }
        // Also destroy any objects with "Road" in their name that have MeshRenderer (road meshes)
        foreach (var roadMR in FindObjectsOfType<MeshRenderer>())
        {
            if (roadMR.gameObject.name.Contains("Road") && roadMR.gameObject.name != "CurvedRoad")
                Destroy(roadMR.gameObject);
        }

        GameObject roadObj = new GameObject("CurvedRoad");
        roadObj.transform.position = Vector3.zero;
        roadObj.transform.rotation = Quaternion.identity;

        MeshFilter mf = roadObj.AddComponent<MeshFilter>();
        MeshRenderer mr = roadObj.AddComponent<MeshRenderer>();

        Material roadMat;
        if (customRoadMaterial != null && customRoadMaterial.shader != null && customRoadMaterial.shader.name != "Hidden/InternalErrorShader")
        {
            // Use assigned material (e.g. EasyRoads3D textured road)
            roadMat = new Material(customRoadMaterial);
        }
        else
        {
            // Procedural asphalt fallback — simple dark road color
            GameObject tempPrim = GameObject.CreatePrimitive(PrimitiveType.Quad);
            roadMat = new Material(tempPrim.GetComponent<Renderer>().sharedMaterial);
            Destroy(tempPrim);

            Color asphalt = new Color(0.22f, 0.22f, 0.25f);
            if (roadMat.HasProperty("_BaseColor")) roadMat.SetColor("_BaseColor", asphalt);
            if (roadMat.HasProperty("_Color")) roadMat.SetColor("_Color", asphalt);
            if (roadMat.HasProperty("_Smoothness")) roadMat.SetFloat("_Smoothness", 0.2f);
        }
        roadMat.SetInt("_Cull", 0);

        mr.material = roadMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Build mesh — collect valid points first, then build triangles
        float totalLen = splinePath.GetTotalLength();
        float step = 2f;
        float halfW = roadWidth * 0.5f;

        Vector3 tPos = terrain != null ? terrain.transform.position : Vector3.zero;
        Vector3 tSize = terrain != null ? terrain.terrainData.size : new Vector3(1000, 100, 1000);

        // Collect valid road points
        System.Collections.Generic.List<Vector3> leftPts = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<Vector3> rightPts = new System.Collections.Generic.List<Vector3>();

        int skippedCount = 0;
        for (float dist = 0f; dist <= totalLen; dist += step)
        {
            SplinePath.SplineSample s = splinePath.SampleAtDistance(dist);

            // Use a horizontal right vector (project forward onto XZ plane) so road edges
            // stay horizontally level even on steep hills.
            Vector3 fwdFlat = new Vector3(s.forward.x, 0f, s.forward.z);
            if (fwdFlat.sqrMagnitude < 0.0001f) fwdFlat = Vector3.forward;
            fwdFlat.Normalize();
            Vector3 rightFlat = new Vector3(fwdFlat.z, 0f, -fwdFlat.x);

            // Use the spline's own Y (set from the visual elevation profile) as the road height.
            // Do NOT re-query terrain.SampleHeight() — the heightmap update may not have
            // propagated yet in the same frame, which causes the road to appear flat.
            // The spline control points already carry the correct visual elevation from
            // ApplyElevationToControlPoints(), so s.position.y is always authoritative.
            Vector3 l = new Vector3(s.position.x - rightFlat.x * halfW, s.position.y + 0.12f, s.position.z - rightFlat.z * halfW);
            Vector3 r = new Vector3(s.position.x + rightFlat.x * halfW, s.position.y + 0.12f, s.position.z + rightFlat.z * halfW);

            // Skip if outside terrain X/Z bounds
            if (l.x < tPos.x || l.x > tPos.x + tSize.x || l.z < tPos.z || l.z > tPos.z + tSize.z)
            {
                skippedCount++;
                continue;
            }

            leftPts.Add(l);
            rightPts.Add(r);
        }

        int count = leftPts.Count;
        Debug.Log($"[CurvedRouteGenerator] Road mesh: {count} points collected, {skippedCount} skipped (terrain bounds: pos={tPos}, size={tSize})");
        
        if (count < 2)
        {
            Debug.LogWarning($"[CurvedRouteGenerator] Not enough road points! All {skippedCount} points were outside terrain bounds.");
            // Try without bounds check as fallback — still use spline Y directly
            for (float dist = 0f; dist <= totalLen; dist += step)
            {
                SplinePath.SplineSample s = splinePath.SampleAtDistance(dist);

                Vector3 fwdFlat = new Vector3(s.forward.x, 0f, s.forward.z);
                if (fwdFlat.sqrMagnitude < 0.0001f) fwdFlat = Vector3.forward;
                fwdFlat.Normalize();
                Vector3 rightFlat = new Vector3(fwdFlat.z, 0f, -fwdFlat.x);

                Vector3 l = new Vector3(s.position.x - rightFlat.x * halfW, s.position.y + 0.12f, s.position.z - rightFlat.z * halfW);
                Vector3 r = new Vector3(s.position.x + rightFlat.x * halfW, s.position.y + 0.12f, s.position.z + rightFlat.z * halfW);

                leftPts.Add(l);
                rightPts.Add(r);
            }
            count = leftPts.Count;
            Debug.Log($"[CurvedRouteGenerator] Fallback: collected {count} points without bounds check");
            
            if (count < 2) return;
        }

        Vector3[] verts = new Vector3[count * 2];
        Vector2[] uvs = new Vector2[count * 2];
        int[] tris = new int[(count - 1) * 6];

        for (int i = 0; i < count; i++)
        {
            verts[i * 2] = leftPts[i];
            verts[i * 2 + 1] = rightPts[i];
            float v = (float)i / count;
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        for (int i = 0; i < count - 1; i++)
        {
            int ti = i * 6;
            int vi = i * 2;
            tris[ti] = vi; tris[ti+1] = vi+2; tris[ti+2] = vi+1;
            tris[ti+3] = vi+1; tris[ti+4] = vi+2; tris[ti+5] = vi+3;
        }

        Mesh mesh = new Mesh();
        mesh.name = "CurvedRoadMesh";
        if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.mesh = mesh;

        // Add white center line
        CreateCenterLine(roadObj, leftPts, rightPts, count);

        Debug.Log($"[CurvedRouteGenerator] Road mesh: {count} segments, shader: {roadMat.shader.name}");
    }

    private void CreateCenterLine(GameObject parent, System.Collections.Generic.List<Vector3> leftPts, System.Collections.Generic.List<Vector3> rightPts, int count)
    {
        GameObject lineObj = new GameObject("CenterLine");
        lineObj.transform.SetParent(parent.transform);
        lineObj.transform.localPosition = Vector3.zero;

        MeshFilter mf = lineObj.AddComponent<MeshFilter>();
        MeshRenderer mr = lineObj.AddComponent<MeshRenderer>();

        GameObject tempPrim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Material lineMat = new Material(tempPrim.GetComponent<Renderer>().sharedMaterial);
        Destroy(tempPrim);
        if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", Color.white);
        if (lineMat.HasProperty("_Color")) lineMat.SetColor("_Color", Color.white);
        mr.material = lineMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        float lineW = 0.15f;
        System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();

        for (int i = 0; i < count; i++)
        {
            // Dashed: skip every other 3m segment
            bool isDash = ((int)(i * 2f / 3f)) % 2 == 0;
            if (!isDash) continue;

            Vector3 center = (leftPts[i] + rightPts[i]) * 0.5f;
            center.y += 0.02f;
            Vector3 right = (rightPts[i] - leftPts[i]).normalized;

            int idx = verts.Count;
            verts.Add(center - right * lineW);
            verts.Add(center + right * lineW);

            if (idx >= 2)
            {
                tris.Add(idx - 2); tris.Add(idx); tris.Add(idx - 1);
                tris.Add(idx - 1); tris.Add(idx); tris.Add(idx + 1);
            }
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mf.mesh = mesh;
    }
    
    // Public API

    /// <summary>
    /// Right-click → "Test: Generate with Manual Gradient" to verify gradient generation
    /// works independently of ExperimentController.
    /// Set Manual Gradient Percent in the Inspector first (e.g. 8).
    /// </summary>
    [ContextMenu("Test: Generate with Manual Gradient")]
    public void TestManualGradient()
    {
        if (manualGradientPercent <= 0f)
        {
            Debug.LogWarning("[CurvedRouteGenerator] Set 'Manual Gradient Percent' > 0 first, then run this test.");
            return;
        }
        trialTargetGradient    = manualGradientPercent;
        trialGradientVariation = manualGradientVariation;
        trialIsRolling         = manualRolling;
        trialFlatSectionLength = manualFlatLeadIn;
        Debug.Log($"[CurvedRouteGenerator] TEST: forcing gradient={manualGradientPercent}%, flat={manualFlatLeadIn}m, rolling={manualRolling}");
        GenerateRoute();
    }

    public SplinePath GetSplinePath() => splinePath;
    public float GetRouteLength() => splinePath != null ? splinePath.GetTotalLength() : routeLength;
    public int GetCurrentSeed() => currentSeed;
    
    public float[] GetElevationProfile() => elevationProfile;
    
    // ─────────────────────────────────────────────────────────────────────────
    // HARDCODED EXPERIMENT CONDITIONS
    // Each condition = 50 m flat lead-in + 450 m climb at the target gradient.
    // Route length is always 500 m.
    // ─────────────────────────────────────────────────────────────────────────

    public enum ExperimentCondition
    {
        Flat_0pct,          // 0 %  steady
        Steady_2pct,        // 2 %  steady   ±0.3 %
        Rolling_2pct,       // 2 %  rolling  ±1.5 %
        Steady_5pct,        // 5 %  steady   ±0.3 %
        Rolling_5pct,       // 5 %  rolling  ±2.0 %
        Steady_8pct,        // 8 %  steady   ±0.3 %
        Rolling_8pct,       // 8 %  rolling  ±3.0 %
        Steady_12pct,       // 12 % steady   ±0.3 %
        Rolling_12pct,      // 12 % rolling  ±4.0 %
    }

    private struct ConditionParams
    {
        public float gradient;      // average gradient %
        public float variation;     // ± variation %
        public bool  rolling;       // oscillating vs steady
        public float flatLeadIn;    // flat metres before climb
        public float climbLength;   // metres of actual climb
    }

    private static ConditionParams GetConditionParams(ExperimentCondition cond)
    {
        const float LEAD  = 50f;
        const float CLIMB = 450f;

        switch (cond)
        {
            case ExperimentCondition.Flat_0pct:
                return new ConditionParams { gradient=0f,  variation=0.1f, rolling=false, flatLeadIn=LEAD, climbLength=CLIMB };
            case ExperimentCondition.Steady_2pct:
                return new ConditionParams { gradient=2f,  variation=0.3f, rolling=false, flatLeadIn=LEAD, climbLength=CLIMB };
            case ExperimentCondition.Rolling_2pct:
                return new ConditionParams { gradient=2f,  variation=1.5f, rolling=true,  flatLeadIn=LEAD, climbLength=CLIMB };
            case ExperimentCondition.Steady_5pct:
                return new ConditionParams { gradient=5f,  variation=0.3f, rolling=false, flatLeadIn=LEAD, climbLength=CLIMB };
            case ExperimentCondition.Rolling_5pct:
                return new ConditionParams { gradient=5f,  variation=2.0f, rolling=true,  flatLeadIn=LEAD, climbLength=CLIMB };
            case ExperimentCondition.Steady_8pct:
                return new ConditionParams { gradient=8f,  variation=0.3f, rolling=false, flatLeadIn=LEAD, climbLength=CLIMB };
            case ExperimentCondition.Rolling_8pct:
                return new ConditionParams { gradient=8f,  variation=3.0f, rolling=true,  flatLeadIn=LEAD, climbLength=CLIMB };
            case ExperimentCondition.Steady_12pct:
                return new ConditionParams { gradient=12f, variation=0.3f, rolling=false, flatLeadIn=LEAD, climbLength=CLIMB };
            case ExperimentCondition.Rolling_12pct:
                return new ConditionParams { gradient=12f, variation=4.0f, rolling=true,  flatLeadIn=LEAD, climbLength=CLIMB };
            default:
                return new ConditionParams { gradient=0f,  variation=0.1f, rolling=false, flatLeadIn=LEAD, climbLength=CLIMB };
        }
    }

    /// <summary>
    /// Apply one of the 9 hardcoded experiment conditions and regenerate the route.
    /// Call this from ExperimentController or right-click the component in the Inspector.
    /// </summary>
    public void ApplyCondition(ExperimentCondition cond)
    {
        ConditionParams p = GetConditionParams(cond);
        routeLength = p.flatLeadIn + p.climbLength;   // always 500 m

        trialTargetGradient    = p.gradient;
        trialGradientVariation = p.variation;
        trialIsRolling         = p.rolling;
        trialFlatSectionLength = p.flatLeadIn;

        Debug.Log($"[CurvedRouteGenerator] Condition: {cond}  " +
                  $"gradient={p.gradient}%  variation=±{p.variation}%  " +
                  $"rolling={p.rolling}  lead-in={p.flatLeadIn}m  climb={p.climbLength}m");

        GenerateRoute();
    }

    // ── Context-menu shortcuts (right-click the component in Play mode) ──────

    [ContextMenu("Condition: Flat 0%")]
    void _Cond_Flat()          => ApplyCondition(ExperimentCondition.Flat_0pct);

    [ContextMenu("Condition: Steady 2%")]
    void _Cond_S2()            => ApplyCondition(ExperimentCondition.Steady_2pct);

    [ContextMenu("Condition: Rolling 2%")]
    void _Cond_R2()            => ApplyCondition(ExperimentCondition.Rolling_2pct);

    [ContextMenu("Condition: Steady 5%")]
    void _Cond_S5()            => ApplyCondition(ExperimentCondition.Steady_5pct);

    [ContextMenu("Condition: Rolling 5%")]
    void _Cond_R5()            => ApplyCondition(ExperimentCondition.Rolling_5pct);

    [ContextMenu("Condition: Steady 8%")]
    void _Cond_S8()            => ApplyCondition(ExperimentCondition.Steady_8pct);

    [ContextMenu("Condition: Rolling 8%")]
    void _Cond_R8()            => ApplyCondition(ExperimentCondition.Rolling_8pct);

    [ContextMenu("Condition: Steady 12%")]
    void _Cond_S12()           => ApplyCondition(ExperimentCondition.Steady_12pct);

    [ContextMenu("Condition: Rolling 12%")]
    void _Cond_R12()           => ApplyCondition(ExperimentCondition.Rolling_12pct);

    // ── Legacy / ExperimentController compatibility ───────────────────────────

    private float trialTargetGradient    = -1f;
    private float trialGradientVariation = 1f;
    private bool  trialIsRolling         = false;
    private float trialFlatSectionLength = 0f;

    /// <summary>
    /// Called by ExperimentController to set gradient before GenerateRoute().
    /// Prefer ApplyCondition() for the 9 hardcoded conditions.
    /// </summary>
    public void SetTrialGradient(float avgGradientPercent, float variation,
                                  bool rolling, float flatMeters = 50f)
    {
        trialTargetGradient    = avgGradientPercent;
        trialGradientVariation = variation;
        trialIsRolling         = rolling;
        trialFlatSectionLength = flatMeters;
        Debug.Log($"[CurvedRouteGenerator] SetTrialGradient: {avgGradientPercent}% ±{variation}%, " +
                  $"rolling={rolling}, flat={flatMeters}m");
    }

    public float GetElevationAtDistance(float distance)
    {
        if (elevationProfile == null || elevationProfile.Length == 0) return baseElevation;
        int idx = Mathf.Clamp(Mathf.RoundToInt(distance), 0, elevationProfile.Length - 1);
        return elevationProfile[idx];
    }

    public float GetGradientAtDistance(float distance)
    {
        if (elevationProfile == null || elevationProfile.Length < 2) return 0f;
        int idx = Mathf.Clamp(Mathf.RoundToInt(distance), 1, elevationProfile.Length - 1);
        return (elevationProfile[idx] - elevationProfile[idx - 1]) * 100f;
    }
}
