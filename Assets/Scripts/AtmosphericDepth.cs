using UnityEngine;

/// <summary>
/// Creates atmospheric depth effect using distance fog/haze
/// Fog density increases with elevation (especially on climbs)
/// Enhances the sense of depth and makes climbs feel more immersive
/// </summary>
public class AtmosphericDepth : MonoBehaviour
{
    [Header("Fog Settings")]
    [Tooltip("Enable atmospheric fog")]
    [SerializeField] private bool enableFog = true;
    
    [Tooltip("Base fog density (at sea level / start elevation)")]
    [Range(0f, 0.1f)]
    [SerializeField] private float baseFogDensity = 0.01f;
    
    [Tooltip("Maximum fog density (at high elevation)")]
    [Range(0f, 0.2f)]
    [SerializeField] private float maxFogDensity = 0.08f;
    
    [Tooltip("Fog color (atmospheric haze color)")]
    [SerializeField] private Color fogColor = new Color(0.7f, 0.8f, 0.9f, 1f); // Light blue-gray
    
    [Tooltip("Fog mode (Linear, Exponential, Exponential Squared)")]
    [SerializeField] private FogMode fogMode = FogMode.Exponential;

    [Header("Elevation-Based Fog")]
    [Tooltip("Enable elevation-based fog density")]
    [SerializeField] private bool enableElevationFog = true;
    
    [Tooltip("Fog density increase per meter of elevation gain")]
    [Range(0f, 0.001f)]
    [SerializeField] private float fogDensityPerMeter = 0.0001f;
    
    [Tooltip("Reference elevation (starting point, meters above sea level)")]
    [SerializeField] private float referenceElevation = 0f;
    
    [Tooltip("Elevation at which fog reaches maximum density (meters)")]
    [Range(50f, 500f)]
    [SerializeField] private float maxElevationForFog = 200f;

    [Header("Gradient-Based Fog (Climb Enhancement)")]
    [Tooltip("Enable additional fog on steep climbs")]
    [SerializeField] private bool enableGradientFog = true;
    
    [Tooltip("Fog density increase per 1% grade (for climbs)")]
    [Range(0f, 0.01f)]
    [SerializeField] private float fogDensityPerGrade = 0.002f;
    
    [Tooltip("Maximum additional fog from gradient")]
    [Range(0f, 0.05f)]
    [SerializeField] private float maxGradientFog = 0.03f;

    [Header("Distance Fog")]
    [Tooltip("Enable distance-based fog (fog increases with distance)")]
    [SerializeField] private bool enableDistanceFog = true;
    
    [Tooltip("Fog start distance (meters)")]
    [Range(10f, 500f)]
    [SerializeField] private float fogStartDistance = 50f;
    
    [Tooltip("Fog end distance (meters)")]
    [Range(100f, 2000f)]
    [SerializeField] private float fogEndDistance = 500f;

    [Header("Smoothing")]
    [Tooltip("Smooth fog density transitions")]
    [SerializeField] private bool smoothFogTransitions = true;
    
    [Tooltip("Fog density smoothing speed")]
    [Range(1f, 20f)]
    [SerializeField] private float fogSmoothingSpeed = 5f;

    [Header("References")]
    [Tooltip("Bike controller to get current gradient and position")]
    [SerializeField] private BikeController bikeController;
    
    [Tooltip("Terrain to sample elevation")]
    [SerializeField] private Terrain terrain;
    
    [Tooltip("Camera to calculate distance fog")]
    [SerializeField] private Camera mainCamera;

    private float currentFogDensity = 0f;
    private float targetFogDensity = 0f;
    private float currentElevation = 0f;

    private void Start()
    {
        // Auto-find components
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }

        // Initialize fog settings
        InitializeFog();
        
        // Get reference elevation from terrain or bike starting position
        if (terrain != null && bikeController != null)
        {
            GameObject bikeObject = bikeController.gameObject;
            if (bikeObject != null)
            {
                Vector3 startPos = bikeObject.transform.position;
                referenceElevation = terrain.SampleHeight(startPos);
            }
        }

        currentFogDensity = baseFogDensity;
        targetFogDensity = baseFogDensity;
    }

    private void Update()
    {
        if (!enableFog) return;

        // Calculate target fog density based on elevation and gradient
        targetFogDensity = CalculateTargetFogDensity();

        // Smooth fog density transition
        if (smoothFogTransitions)
        {
            currentFogDensity = Mathf.Lerp(currentFogDensity, targetFogDensity, Time.deltaTime * fogSmoothingSpeed);
        }
        else
        {
            currentFogDensity = targetFogDensity;
        }

        // Apply fog settings
        ApplyFogSettings();
    }

    /// <summary>
    /// Initialize fog settings
    /// </summary>
    private void InitializeFog()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;
        
        if (fogMode == FogMode.Linear && enableDistanceFog)
        {
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;
        }
    }

    /// <summary>
    /// Calculate target fog density based on elevation and gradient
    /// </summary>
    private float CalculateTargetFogDensity()
    {
        float density = baseFogDensity;

        // Elevation-based fog
        if (enableElevationFog)
        {
            float elevation = GetCurrentElevation();
            currentElevation = elevation;
            
            // Calculate elevation gain from reference
            float elevationGain = Mathf.Max(0f, elevation - referenceElevation);
            
            // Increase fog density with elevation
            float elevationFog = elevationGain * fogDensityPerMeter;
            
            // Normalize to max elevation for smoother curve
            float normalizedElevation = Mathf.Clamp01(elevationGain / maxElevationForFog);
            
            // Use smooth curve (ease-in-out) for more natural fog increase
            float elevationFactor = normalizedElevation * normalizedElevation; // Quadratic curve
            float maxElevationFog = maxFogDensity - baseFogDensity;
            elevationFog = elevationFactor * maxElevationFog;
            
            density += elevationFog;
        }

        // Gradient-based fog (additional fog on climbs)
        if (enableGradientFog && bikeController != null)
        {
            float gradient = bikeController.GetGradient();
            
            // Only apply additional fog on uphill (positive gradient)
            if (gradient > 0.1f)
            {
                float gradientFog = gradient * fogDensityPerGrade;
                gradientFog = Mathf.Clamp(gradientFog, 0f, maxGradientFog);
                density += gradientFog;
            }
        }

        // Clamp to maximum density
        density = Mathf.Clamp(density, baseFogDensity, maxFogDensity + maxGradientFog);

        return density;
    }

    /// <summary>
    /// Get current elevation (height above sea level)
    /// </summary>
    private float GetCurrentElevation()
    {
        if (bikeController == null) return referenceElevation;

        GameObject bikeObject = bikeController.gameObject;
        if (bikeObject == null) return referenceElevation;

        Vector3 bikePosition = bikeObject.transform.position;
        
        // Get terrain height at bike position
        if (terrain != null)
        {
            float terrainHeight = terrain.SampleHeight(bikePosition);
            return terrainHeight;
        }

        // Fallback to bike Y position
        return bikePosition.y;
    }

    /// <summary>
    /// Apply fog settings to render settings
    /// </summary>
    private void ApplyFogSettings()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;

        if (fogMode == FogMode.Linear && enableDistanceFog)
        {
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;
        }
        else
        {
            // Exponential fog modes use density
            RenderSettings.fogDensity = currentFogDensity;
        }
    }

    /// <summary>
    /// Get current fog density (for debugging)
    /// </summary>
    public float GetCurrentFogDensity()
    {
        return currentFogDensity;
    }

    /// <summary>
    /// Get current elevation value (for debugging)
    /// </summary>
    public float GetCurrentElevationValue()
    {
        return currentElevation;
    }

    /// <summary>
    /// Set reference elevation manually
    /// </summary>
    public void SetReferenceElevation(float elevation)
    {
        referenceElevation = elevation;
    }

    /// <summary>
    /// Enable/disable fog
    /// </summary>
    public void SetFogEnabled(bool enabled)
    {
        enableFog = enabled;
        RenderSettings.fog = enabled;
    }

    /// <summary>
    /// Set fog color
    /// </summary>
    public void SetFogColor(Color color)
    {
        fogColor = color;
        RenderSettings.fogColor = color;
    }

    private void OnValidate()
    {
        // Update fog settings in editor when values change
        if (Application.isPlaying)
        {
            InitializeFog();
        }
    }

    private void OnDisable()
    {
        // Reset fog when disabled
        RenderSettings.fog = false;
    }
}
