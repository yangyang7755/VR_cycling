using UnityEngine;

/// <summary>
/// Generates curved roads instead of straight roads
/// Adds visual interest and more realistic cycling experience
/// </summary>
public class CurvedRoadGenerator : MonoBehaviour
{
    [Header("Road Curve Settings")]
    [Tooltip("Enable curved road generation")]
    [SerializeField] public bool enableCurvedRoad = true;
    
    [Tooltip("Curve type: Sine wave (smooth S-curves) or Random (winding)")]
    [SerializeField] private CurveType curveType = CurveType.SineWave;
    
    [Tooltip("Number of curves along the road (for Random type)")]
    [Range(1, 10)]
    [SerializeField] private int curveCount = 3;
    
    [Tooltip("Curve amplitude - how much the road curves left/right (in meters)")]
    [Range(5f, 50f)]
    [SerializeField] private float curveAmplitude = 20f;
    
    [Tooltip("Curve frequency - how many waves along the road (for SineWave type)")]
    [Range(0.5f, 5f)]
    [SerializeField] private float curveFrequency = 1.5f;
    
    [Tooltip("Curve smoothness - higher = smoother curves")]
    [Range(0.1f, 2f)]
    [SerializeField] private float curveSmoothness = 1f;

    public enum CurveType
    {
        SineWave,    // Smooth S-curves using sine wave
        Random       // Random winding curves
    }
    
    [Header("Road Width")]
    [Tooltip("Road width in meters")]
    [SerializeField] private float roadWidth = 15f;
    
    [Header("References")]
    [Tooltip("Terrain to paint road on")]
    [SerializeField] private Terrain terrain;
    
    [Tooltip("SimpleTerrainBuilder reference (for road layer)")]
    [SerializeField] private SimpleTerrainBuilder terrainBuilder;

    private float[] curvePoints; // Normalized positions (0-1) along road
    private float[] curveOffsets; // Z-axis offsets at each curve point

    /// <summary>
    /// Generate curve points for the road
    /// </summary>
    public void GenerateCurvePoints()
    {
        if (!enableCurvedRoad)
        {
            curvePoints = null;
            curveOffsets = null;
            return;
        }

        curvePoints = new float[curveCount + 2]; // +2 for start and end
        curveOffsets = new float[curveCount + 2];
        
        // Start point (no offset)
        curvePoints[0] = 0f;
        curveOffsets[0] = 0f;
        
        // Generate random curve points
        for (int i = 1; i <= curveCount; i++)
        {
            curvePoints[i] = Random.Range(0.1f + (i - 1) * 0.2f, 0.9f - (curveCount - i) * 0.2f);
            curveOffsets[i] = Random.Range(-curveAmplitude, curveAmplitude);
        }
        
        // End point (no offset)
        curvePoints[curveCount + 1] = 1f;
        curveOffsets[curveCount + 1] = 0f;
        
        // Sort by position
        System.Array.Sort(curvePoints, curveOffsets);
        
        Debug.Log($"[CurvedRoadGenerator] Generated {curveCount} curves");
    }

    /// <summary>
    /// Get road center Z position at a given X position (normalized 0-1)
    /// Returns the Z offset from center (positive = right, negative = left)
    /// </summary>
    public float GetRoadCenterZAtX(float normalizedX)
    {
        if (!enableCurvedRoad)
        {
            // Straight road - return center (no offset)
            return 0f;
        }

        float offset = 0f;

        if (curveType == CurveType.SineWave)
        {
            // Use sine wave for smooth S-curves
            // normalizedX goes from 0 to 1 along the road
            // Multiply by frequency to get multiple waves
            float wave = Mathf.Sin(normalizedX * curveFrequency * Mathf.PI * 2f);
            offset = wave * curveAmplitude;
        }
        else if (curveType == CurveType.Random)
        {
            // Use random curve points with interpolation
            if (curvePoints == null || curvePoints.Length < 2)
            {
                GenerateCurvePoints();
            }

            if (curvePoints != null && curvePoints.Length >= 2)
            {
                // Find the two curve points to interpolate between
                int index = 0;
                for (int i = 0; i < curvePoints.Length - 1; i++)
                {
                    if (normalizedX >= curvePoints[i] && normalizedX <= curvePoints[i + 1])
                    {
                        index = i;
                        break;
                    }
                }

                // Interpolate between the two points using smooth curve
                float t = (normalizedX - curvePoints[index]) / (curvePoints[index + 1] - curvePoints[index]);
                t = Mathf.Clamp01(t);
                
                // Apply smooth interpolation (ease in/out)
                float smoothT = t * t * (3f - 2f * t); // Smoothstep
                smoothT = Mathf.Pow(smoothT, curveSmoothness);
                
                offset = Mathf.Lerp(curveOffsets[index], curveOffsets[index + 1], smoothT);
            }
        }

        return offset;
    }

    /// <summary>
    /// Paint curved road on terrain
    /// </summary>
    public void PaintCurvedRoad()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogError("[CurvedRoadGenerator] No terrain found!");
            return;
        }

        if (terrainBuilder == null)
        {
            terrainBuilder = FindObjectOfType<SimpleTerrainBuilder>();
        }

        if (terrainBuilder == null)
        {
            Debug.LogError("[CurvedRoadGenerator] SimpleTerrainBuilder not found!");
            return;
        }

        // Generate curve points
        GenerateCurvePoints();

        TerrainData data = terrain.terrainData;
        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        Vector3 size = data.size;

        float[,,] alphamap = data.GetAlphamaps(0, 0, width, height);

        // Find road layer index
        int roadIndex = -1;
        int grassIndex = -1;
        int mountainIndex = -1;
        
        for (int i = 0; i < data.terrainLayers.Length; i++)
        {
            if (data.terrainLayers[i] == terrainBuilder.RoadLayer)
            {
                roadIndex = i;
            }
            else if (data.terrainLayers[i] == terrainBuilder.GrassLayer)
            {
                grassIndex = i;
            }
            else if (data.terrainLayers[i] == terrainBuilder.MountainLayer)
            {
                mountainIndex = i;
            }
        }

        if (roadIndex == -1)
        {
            Debug.LogError("[CurvedRoadGenerator] Road layer not found!");
            return;
        }

        // Calculate road width in pixels
        float roadWidthInPixels = (roadWidth / size.z) * height;
        int halfWidth = Mathf.RoundToInt(roadWidthInPixels / 2f);

        // Get base road center (middle of terrain in Z direction)
        float baseRoadCenterZ = size.z / 2f;
        int baseCenterZPixel = height / 2;

        // Paint curved road
        int pixelsPainted = 0;
        
        for (int x = 0; x < width; x++) // From left to right (X axis - road direction)
        {
            // Get normalized X position (0-1) along the road
            float normalizedX = (float)x / (width - 1);
            
            // Get Z offset from center at this X position (positive = right/back, negative = left/front)
            float zOffset = GetRoadCenterZAtX(normalizedX);
            
            // Calculate actual road center Z position
            float roadCenterZ = baseRoadCenterZ + zOffset;
            
            // Convert to pixel position
            int centerZ = Mathf.RoundToInt((roadCenterZ / size.z) * height);
            centerZ = Mathf.Clamp(centerZ, halfWidth, height - halfWidth - 1); // Keep within bounds
            
            for (int z = 0; z < height; z++) // Across depth (Z axis - perpendicular to road)
            {
                int distFromCenter = Mathf.Abs(z - centerZ);
                
                if (distFromCenter <= halfWidth)
                {
                    // Paint road
                    alphamap[x, z, roadIndex] = 1f;
                    
                    // Clear other layers on road
                    if (grassIndex >= 0) alphamap[x, z, grassIndex] = 0f;
                    if (mountainIndex >= 0) alphamap[x, z, mountainIndex] = 0f;
                    
                    pixelsPainted++;
                }
            }
        }

        data.SetAlphamaps(0, 0, alphamap);
        Debug.Log($"[CurvedRoadGenerator] ✓ Curved road painted! {pixelsPainted} pixels");
        Debug.Log($"  Curves: {curveCount}, Amplitude: {curveAmplitude}m");
    }

    [ContextMenu("Generate Curved Road")]
    public void GenerateCurvedRoadManual()
    {
        PaintCurvedRoad();
    }

    /// <summary>
    /// Get road width (for use by other scripts)
    /// </summary>
    public float GetRoadWidth()
    {
        return roadWidth;
    }
}
