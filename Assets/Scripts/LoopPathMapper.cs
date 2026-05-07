using UnityEngine;

/// <summary>
/// Maps long-distance routes to looped paths within limited terrain space
/// Supports serpentine (back-and-forth) and spiral patterns
/// This allows 5-50km routes to be displayed within a smaller terrain area
/// </summary>
public class LoopPathMapper : MonoBehaviour
{
    public enum LoopType
    {
        Serpentine,  // Back-and-forth like a snake
        Spiral,      // Spiral from outer to inner
        SimpleLoop   // Simple circular loops
    }

    [Header("Loop Settings")]
    [Tooltip("Type of loop pattern")]
    [SerializeField] private LoopType loopType = LoopType.Serpentine;

    [Tooltip("Terrain size (X and Z dimensions in meters)")]
    [SerializeField] private Vector2 terrainSize = new Vector2(500f, 200f);

    [Tooltip("Number of loops/segments (for Serpentine)")]
    [Range(2, 20)]
    [SerializeField] private int loopCount = 5;

    [Tooltip("Road width (for spacing calculations)")]
    [SerializeField] private float roadWidth = 15f;

    [Tooltip("Spacing between parallel road segments (meters)")]
    [Range(20f, 100f)]
    [SerializeField] private float segmentSpacing = 50f;

    [Header("Curved Road")]
    [Tooltip("Enable curved road within each segment")]
    [SerializeField] private bool enableCurvedSegments = true;

    [Tooltip("Curve amplitude for each segment (meters)")]
    [Range(0f, 30f)]
    [SerializeField] private float segmentCurveAmplitude = 10f;

    [Tooltip("Curve frequency (waves per segment)")]
    [Range(0.5f, 3f)]
    [SerializeField] private float segmentCurveFrequency = 1.5f;

    [Header("Auto-Detection")]
    [Tooltip("Auto-detect terrain size")]
    [SerializeField] private bool autoDetectTerrainSize = true;

    private float segmentLength; // Length of each segment
    private System.Random random;
    private Terrain terrain;

    private void Awake()
    {
        random = new System.Random();
        if (autoDetectTerrainSize)
        {
            terrain = FindObjectOfType<Terrain>();
            if (terrain != null)
            {
                Vector3 size = terrain.terrainData.size;
                terrainSize = new Vector2(size.x, size.z);
            }
        }
    }

    /// <summary>
    /// Initialize loop mapper with terrain size and route length
    /// </summary>
    public void Initialize(float routeLength, Vector2 terrainSize, int loopCount)
    {
        this.terrainSize = terrainSize;
        this.loopCount = loopCount;
        this.segmentLength = terrainSize.x / loopCount;
    }

    /// <summary>
    /// Map distance along route to world position (X, Z)
    /// Returns world position and normalized progress (0-1) within current segment
    /// </summary>
    public Vector3 GetWorldPosition(float distance, float totalRouteLength, out float normalizedSegmentProgress)
    {
        // Normalize distance to [0, 1]
        float normalizedDistance = Mathf.Clamp01(distance / totalRouteLength);

        Vector3 position = Vector3.zero;

        switch (loopType)
        {
            case LoopType.Serpentine:
                position = GetSerpentinePosition(normalizedDistance, out normalizedSegmentProgress);
                break;
            case LoopType.Spiral:
                position = GetSpiralPosition(normalizedDistance, out normalizedSegmentProgress);
                break;
            case LoopType.SimpleLoop:
                position = GetSimpleLoopPosition(normalizedDistance, out normalizedSegmentProgress);
                break;
            default:
                position = GetSerpentinePosition(normalizedDistance, out normalizedSegmentProgress);
                break;
        }

        return position;
    }

    /// <summary>
    /// Serpentine pattern: back-and-forth like a snake
    /// </summary>
    private Vector3 GetSerpentinePosition(float normalizedDistance, out float segmentProgress)
    {
        // Calculate which segment we're in
        float totalSegments = loopCount;
        float segmentIndex = normalizedDistance * totalSegments;
        int currentSegment = Mathf.FloorToInt(segmentIndex);
        currentSegment = Mathf.Clamp(currentSegment, 0, loopCount - 1);
        segmentProgress = (segmentIndex - currentSegment); // 0-1 within segment

        // Calculate X position (along terrain length)
        float x = (currentSegment + segmentProgress) * (terrainSize.x / loopCount);

        // Calculate Z position (alternating left/right)
        bool goingRight = (currentSegment % 2 == 0);
        float baseZ = goingRight ? 
            (terrainSize.y * 0.2f) : // Start near left edge
            (terrainSize.y * 0.8f);  // End near right edge

        // Interpolate Z within segment
        float z = goingRight ?
            Mathf.Lerp(terrainSize.y * 0.2f, terrainSize.y * 0.8f, segmentProgress) :
            Mathf.Lerp(terrainSize.y * 0.8f, terrainSize.y * 0.2f, segmentProgress);

        // Add curve within segment if enabled
        if (enableCurvedSegments)
        {
            float curveOffset = Mathf.Sin(segmentProgress * Mathf.PI * 2f * segmentCurveFrequency) * segmentCurveAmplitude;
            z += curveOffset;
        }

        // Clamp to terrain bounds (with road width margin)
        z = Mathf.Clamp(z, roadWidth, terrainSize.y - roadWidth);

        return new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Spiral pattern: from outer to inner
    /// </summary>
    private Vector3 GetSpiralPosition(float normalizedDistance, out float segmentProgress)
    {
        // Spiral from outer edge to center
        float angle = normalizedDistance * Mathf.PI * 2f * loopCount;
        float radius = Mathf.Lerp(terrainSize.y * 0.4f, terrainSize.y * 0.1f, normalizedDistance);

        float x = terrainSize.x * normalizedDistance;
        float z = terrainSize.y * 0.5f + Mathf.Cos(angle) * radius;

        segmentProgress = normalizedDistance; // For spiral, progress is just normalized distance

        // Clamp to terrain bounds
        z = Mathf.Clamp(z, roadWidth, terrainSize.y - roadWidth);

        return new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Simple loop pattern: circular loops
    /// </summary>
    private Vector3 GetSimpleLoopPosition(float normalizedDistance, out float segmentProgress)
    {
        // Similar to serpentine but with smoother transitions
        float totalSegments = loopCount;
        float segmentIndex = normalizedDistance * totalSegments;
        int currentSegment = Mathf.FloorToInt(segmentIndex);
        currentSegment = Mathf.Clamp(currentSegment, 0, loopCount - 1);
        segmentProgress = (segmentIndex - currentSegment);

        float x = (currentSegment + segmentProgress) * (terrainSize.x / loopCount);
        float z = terrainSize.y * 0.5f + Mathf.Sin(segmentProgress * Mathf.PI * 2f) * (terrainSize.y * 0.3f);

        z = Mathf.Clamp(z, roadWidth, terrainSize.y - roadWidth);

        return new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Get road center Z at a given distance (for road painting)
    /// </summary>
    public float GetRoadCenterZAtDistance(float distance, float totalRouteLength)
    {
        float normalizedDistance = Mathf.Clamp01(distance / totalRouteLength);
        Vector3 pos = GetWorldPosition(distance, totalRouteLength, out _);
        return pos.z;
    }

    /// <summary>
    /// Get road direction at a given distance (for bike orientation)
    /// </summary>
    public Vector3 GetRoadDirection(float distance, float totalRouteLength)
    {
        float delta = 1f; // Small distance for direction calculation
        Vector3 pos1 = GetWorldPosition(distance, totalRouteLength, out _);
        Vector3 pos2 = GetWorldPosition(distance + delta, totalRouteLength, out _);
        Vector3 direction = (pos2 - pos1).normalized;
        
        // If direction is too small (near boundaries), use previous direction or default
        if (direction.magnitude < 0.01f)
        {
            direction = Vector3.forward; // Default forward direction
        }
        
        return direction;
    }

    /// <summary>
    /// Get terrain size (for external use)
    /// </summary>
    public Vector2 GetTerrainSize()
    {
        return terrainSize;
    }

    /// <summary>
    /// Set terrain size (called when terrain is detected)
    /// </summary>
    public void SetTerrainSize(Vector2 size)
    {
        terrainSize = size;
    }

    /// <summary>
    /// Get number of loops
    /// </summary>
    public int GetLoopCount()
    {
        return loopCount;
    }

    /// <summary>
    /// Set loop count
    /// </summary>
    public void SetLoopCount(int count)
    {
        loopCount = Mathf.Clamp(count, 2, 20);
    }
}
