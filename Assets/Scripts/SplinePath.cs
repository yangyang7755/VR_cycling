using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Catmull-Rom spline path for generating smooth curved roads.
/// Control points define the road shape; the spline interpolates smoothly between them.
/// Used by CurvedRouteGenerator to define road geometry and by BikeController to follow it.
/// </summary>
public class SplinePath : MonoBehaviour
{
    [Header("Path Settings")]
    [Tooltip("Control points are generated procedurally if empty")]
    [SerializeField] private List<Vector3> controlPoints = new List<Vector3>();
    
    [Tooltip("Number of interpolated samples per segment (higher = smoother)")]
    [SerializeField] private int samplesPerSegment = 20;
    
    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color pathColor = Color.yellow;
    [SerializeField] private Color controlPointColor = Color.red;
    
    // Cached lookup table: distance -> position/direction
    private List<SplineSample> lookupTable = new List<SplineSample>();
    private float totalLength = 0f;
    private bool isDirty = true;
    
    public struct SplineSample
    {
        public float distance;    // cumulative distance from start
        public Vector3 position;
        public Vector3 forward;   // tangent direction
        public Vector3 right;     // perpendicular (for road width)
    }
    
    /// <summary>
    /// Set control points and rebuild the lookup table.
    /// </summary>
    public void SetControlPoints(List<Vector3> points)
    {
        controlPoints = new List<Vector3>(points);
        isDirty = true;
        RebuildLookupTable();
    }
    
    public List<Vector3> GetControlPoints() => controlPoints;
    public float GetTotalLength() => totalLength;
    public int GetSampleCount() => lookupTable.Count;

    /// <summary>
    /// Rebuild the distance-indexed lookup table from control points.
    /// </summary>
    public void RebuildLookupTable()
    {
        lookupTable.Clear();
        totalLength = 0f;
        
        if (controlPoints.Count < 2) return;
        
        int segmentCount = controlPoints.Count - 1;
        Vector3 prevPos = EvaluateCatmullRom(0, 0f);
        
        SplineSample first = new SplineSample
        {
            distance = 0f,
            position = prevPos,
            forward = Vector3.forward,
            right = Vector3.right
        };
        lookupTable.Add(first);
        
        for (int seg = 0; seg < segmentCount; seg++)
        {
            for (int s = 1; s <= samplesPerSegment; s++)
            {
                float t = (float)s / samplesPerSegment;
                Vector3 pos = EvaluateCatmullRom(seg, t);
                float dist = Vector3.Distance(prevPos, pos);
                totalLength += dist;
                
                Vector3 fwd = (pos - prevPos).normalized;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                
                lookupTable.Add(new SplineSample
                {
                    distance = totalLength,
                    position = pos,
                    forward = fwd,
                    right = right
                });
                
                prevPos = pos;
            }
        }
        
        isDirty = false;
    }
    
    /// <summary>
    /// Get position and direction at a given distance along the path.
    /// </summary>
    public SplineSample SampleAtDistance(float distance)
    {
        if (isDirty) RebuildLookupTable();
        if (lookupTable.Count == 0) return default;
        
        distance = Mathf.Clamp(distance, 0f, totalLength);
        
        // Binary search for the right segment
        int lo = 0, hi = lookupTable.Count - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (lookupTable[mid].distance <= distance)
                lo = mid;
            else
                hi = mid;
        }
        
        SplineSample a = lookupTable[lo];
        SplineSample b = lookupTable[hi];
        
        float segLen = b.distance - a.distance;
        float t = segLen > 0.001f ? (distance - a.distance) / segLen : 0f;
        
        return new SplineSample
        {
            distance = distance,
            position = Vector3.Lerp(a.position, b.position, t),
            forward = Vector3.Slerp(a.forward, b.forward, t).normalized,
            right = Vector3.Slerp(a.right, b.right, t).normalized
        };
    }
    
    /// <summary>
    /// Catmull-Rom spline evaluation for one segment.
    /// </summary>
    private Vector3 EvaluateCatmullRom(int segmentIndex, float t)
    {
        // Get 4 control points (p0, p1, p2, p3) with clamped boundaries
        Vector3 p0 = controlPoints[Mathf.Max(0, segmentIndex - 1)];
        Vector3 p1 = controlPoints[segmentIndex];
        Vector3 p2 = controlPoints[Mathf.Min(controlPoints.Count - 1, segmentIndex + 1)];
        Vector3 p3 = controlPoints[Mathf.Min(controlPoints.Count - 1, segmentIndex + 2)];
        
        float t2 = t * t;
        float t3 = t2 * t;
        
        // Catmull-Rom formula
        Vector3 result = 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
        
        return result;
    }
    
    private void OnDrawGizmos()
    {
        if (!drawGizmos || lookupTable.Count < 2) return;
        
        // Draw path
        Gizmos.color = pathColor;
        for (int i = 1; i < lookupTable.Count; i++)
        {
            Gizmos.DrawLine(lookupTable[i - 1].position, lookupTable[i].position);
        }
        
        // Draw control points
        Gizmos.color = controlPointColor;
        foreach (var cp in controlPoints)
        {
            Gizmos.DrawSphere(cp, 2f);
        }
    }
}
