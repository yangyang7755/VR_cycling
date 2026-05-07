using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Complete route configuration for a long rolling route (5-50km)
/// Contains base terrain settings and multiple discrete climb segments
/// </summary>
[CreateAssetMenu(fileName = "NewRouteConfiguration", menuName = "Cycling/Route Configuration")]
public class RouteConfiguration : ScriptableObject
{
    [Header("Route Overview")]
    [Tooltip("Route name")]
    public string routeName = "Rolling Route";

    [Tooltip("Total route length (meters) - 5-50 km")]
    [Range(5000f, 50000f)]
    public float totalRouteLength = 10000f; // 10km default

    [Header("Base Rolling Terrain")]
    [Tooltip("Base terrain gradient (flat sections between climbs)")]
    [Range(-2f, 2f)]
    public float baseTerrainGradient = 0f;

    [Tooltip("Rolling terrain amplitude (height variation in flat sections)")]
    [Range(0f, 5f)]
    public float rollingAmplitude = 1f;

    [Tooltip("Rolling terrain frequency (how often terrain undulates)")]
    [Range(0f, 0.1f)]
    public float rollingFrequency = 0.02f;

    [Tooltip("Visual gradient for base terrain")]
    [Range(0f, 3f)]
    public float baseVisualGradient = 0f;

    [Header("Climb Segments")]
    [Tooltip("List of discrete climb segments")]
    public List<ClimbSegment> climbSegments = new List<ClimbSegment>();

    [Tooltip("Default climb settings (used when creating new climbs)")]
    public ClimbSegment defaultClimbSettings = new ClimbSegment
    {
        climbName = "Climb",
        difficulty = DifficultyLevel.Moderate,
        startLocation = 1000f,
        length = 500f,
        averageGrade = 5f,
        visualGradientMultiplier = 1.0f,
        undulationFrequency = 0.2f,
        undulationAmplitude = 0.5f,
        startTransition = 50f,
        endTransition = 50f,
        checkpointAtStart = true,
        checkpointAtEnd = true,
        checkpointMidClimb = false,
        midClimbPosition = 0.5f
    };

    [Header("Checkpoint Settings")]
    [Tooltip("Place checkpoint at route start")]
    public bool checkpointAtStart = true;

    [Tooltip("Place checkpoint at route end")]
    public bool checkpointAtEnd = true;

    [Tooltip("Place checkpoints every N meters (0 = disable)")]
    [Range(0f, 5000f)]
    public float regularCheckpointInterval = 0f;

    /// <summary>
    /// Get all checkpoints for this route (start, climbs, regular intervals, end)
    /// </summary>
    public List<CheckpointData> GetAllCheckpoints()
    {
        List<CheckpointData> checkpoints = new List<CheckpointData>();

        // Start checkpoint
        if (checkpointAtStart)
        {
            checkpoints.Add(new CheckpointData
            {
                location = 0f,
                name = "Start",
                type = CheckpointType.Start
            });
        }

        // Regular interval checkpoints
        if (regularCheckpointInterval > 0f)
        {
            for (float distance = regularCheckpointInterval; distance < totalRouteLength; distance += regularCheckpointInterval)
            {
                // Check if this checkpoint conflicts with climb checkpoints
                bool conflictsWithClimb = climbSegments.Any(c => 
                    Mathf.Abs(distance - c.startLocation) < 10f || 
                    Mathf.Abs(distance - c.EndLocation) < 10f);

                if (!conflictsWithClimb)
                {
                    checkpoints.Add(new CheckpointData
                    {
                        location = distance,
                        name = $"Checkpoint {distance:F0}m",
                        type = CheckpointType.Regular
                    });
                }
            }
        }

        // Climb segment checkpoints
        foreach (ClimbSegment climb in climbSegments)
        {
            if (climb.checkpointAtStart)
            {
                checkpoints.Add(new CheckpointData
                {
                    location = climb.startLocation,
                    name = $"{climb.climbName} - Start",
                    type = CheckpointType.ClimbStart,
                    climbSegment = climb
                });
            }

            if (climb.checkpointMidClimb)
            {
                float midLocation = climb.startLocation + (climb.length * climb.midClimbPosition);
                checkpoints.Add(new CheckpointData
                {
                    location = midLocation,
                    name = $"{climb.climbName} - Mid",
                    type = CheckpointType.ClimbMid,
                    climbSegment = climb
                });
            }

            if (climb.checkpointAtEnd)
            {
                checkpoints.Add(new CheckpointData
                {
                    location = climb.EndLocation,
                    name = $"{climb.climbName} - End",
                    type = CheckpointType.ClimbEnd,
                    climbSegment = climb
                });
            }
        }

        // End checkpoint
        if (checkpointAtEnd)
        {
            checkpoints.Add(new CheckpointData
            {
                location = totalRouteLength,
                name = "Finish",
                type = CheckpointType.Finish
            });
        }

        // Sort by location
        checkpoints.Sort((a, b) => a.location.CompareTo(b.location));

        return checkpoints;
    }

    /// <summary>
    /// Get gradient at a specific distance along the route
    /// Uses pre-computed grade array from LongRouteTerrainBuilder if available
    /// Otherwise falls back to simple calculation
    /// </summary>
    public float GetGradientAtDistance(float distance)
    {
        if (distance < 0f || distance > totalRouteLength) return 0f;

        // If grade array is available, use it (more accurate)
        if (cachedGradeArray != null && cachedGradeArray.Length > 0 && cachedSampleInterval > 0f)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt(distance / cachedSampleInterval), 0, cachedGradeArray.Length - 1);
            return cachedGradeArray[index];
        }

        // Fallback: Check if within a climb segment
        foreach (ClimbSegment climb in climbSegments)
        {
            if (climb.IsWithinClimb(distance))
            {
                float normalizedPos = climb.GetNormalizedPosition(distance);
                float effectiveGrade = CalculateClimbGradient(normalizedPos, climb);
                return effectiveGrade;
            }
        }

        // Base rolling terrain
        float rollingGradient = Mathf.Sin(distance * rollingFrequency) * rollingAmplitude;
        return baseTerrainGradient + rollingGradient;
    }

    // Cached grade array from terrain builder
    private float[] cachedGradeArray;
    private float cachedSampleInterval;

    /// <summary>
    /// Set cached grade array (called by LongRouteTerrainBuilder after generation)
    /// </summary>
    public void SetCachedGradeArray(float[] gradeArray, float sampleInterval)
    {
        cachedGradeArray = gradeArray;
        cachedSampleInterval = sampleInterval;
    }

    /// <summary>
    /// Calculate gradient within a climb segment (with transitions and undulation)
    /// </summary>
    private float CalculateClimbGradient(float normalizedPos, ClimbSegment climb)
    {
        // Transition factors
        float startTransitionFactor = 1f;
        float endTransitionFactor = 1f;

        if (normalizedPos < (climb.startTransition / climb.length))
        {
            // In start transition
            float transitionProgress = normalizedPos / (climb.startTransition / climb.length);
            startTransitionFactor = Mathf.SmoothStep(0f, 1f, transitionProgress);
        }

        if (normalizedPos > 1f - (climb.endTransition / climb.length))
        {
            // In end transition
            float transitionProgress = (1f - normalizedPos) / (climb.endTransition / climb.length);
            endTransitionFactor = Mathf.SmoothStep(0f, 1f, transitionProgress);
        }

        // Base gradient
        float baseGradient = climb.averageGrade;

        // Add undulation
        float undulation = Mathf.Sin(normalizedPos * Mathf.PI * 2f * climb.undulationFrequency) * climb.undulationAmplitude;

        // Combine
        float effectiveGradient = (baseGradient + undulation) * startTransitionFactor * endTransitionFactor;

        return effectiveGradient;
    }

    /// <summary>
    /// Get visual gradient at a specific distance (for appearance)
    /// </summary>
    public float GetVisualGradientAtDistance(float distance)
    {
        float physicsGradient = GetGradientAtDistance(distance);

        // Check if within a climb segment
        foreach (ClimbSegment climb in climbSegments)
        {
            if (climb.IsWithinClimb(distance))
            {
                return physicsGradient * climb.visualGradientMultiplier;
            }
        }

        return physicsGradient * baseVisualGradient;
    }

    /// <summary>
    /// Validate route configuration (check for overlaps, etc.)
    /// </summary>
    public bool ValidateRoute(out string errorMessage)
    {
        errorMessage = "";

        // Check climb segments don't overlap
        for (int i = 0; i < climbSegments.Count; i++)
        {
            for (int j = i + 1; j < climbSegments.Count; j++)
            {
                ClimbSegment a = climbSegments[i];
                ClimbSegment b = climbSegments[j];

                if (a.startLocation < b.EndLocation && b.startLocation < a.EndLocation)
                {
                    errorMessage = $"Climb segments overlap: '{a.climbName}' and '{b.climbName}'";
                    return false;
                }
            }

            // Check climb is within route bounds
            if (climbSegments[i].startLocation < 0f || climbSegments[i].EndLocation > totalRouteLength)
            {
                errorMessage = $"Climb '{climbSegments[i].climbName}' is outside route bounds";
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Checkpoint data structure
/// </summary>
[System.Serializable]
public class CheckpointData
{
    public float location; // Meters from start
    public string name;
    public CheckpointType type;
    public ClimbSegment climbSegment; // If part of a climb
    public float timestamp; // Set when checkpoint is reached
    public bool isReached;
}

/// <summary>
/// Checkpoint types
/// </summary>
public enum CheckpointType
{
    Start,
    Regular,
    ClimbStart,
    ClimbMid,
    ClimbEnd,
    Finish
}
