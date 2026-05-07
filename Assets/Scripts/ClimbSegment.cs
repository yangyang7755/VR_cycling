using UnityEngine;

/// <summary>
/// Defines a single climb segment with tunable parameters
/// Each climb is a discrete event with configurable start, length, grade, and difficulty
/// </summary>
[System.Serializable]
public class ClimbSegment
{
    [Header("Climb Identity")]
    [Tooltip("Climb name/identifier")]
    public string climbName = "Climb";

    [Tooltip("Difficulty label (e.g., 'Easy', 'Moderate', 'Hard', 'Very Hard')")]
    public DifficultyLevel difficulty = DifficultyLevel.Moderate;

    [Header("Climb Location")]
    [Tooltip("Start location along route (meters from start)")]
    [Range(0f, 50000f)]
    public float startLocation = 1000f;

    [Tooltip("Climb length (meters)")]
    [Range(100f, 5000f)]
    public float length = 500f;

    [Header("Gradient Settings")]
    [Tooltip("Average grade percentage (physics gradient for resistance)")]
    [Range(0f, 20f)]
    public float averageGrade = 5f;

    [Tooltip("Visual gradient multiplier (appearance - can be different from physics)")]
    [Range(0.5f, 5f)]
    public float visualGradientMultiplier = 1.0f;

    [Tooltip("Undulation frequency (higher = more rolling hills within climb)")]
    [Range(0f, 1f)]
    public float undulationFrequency = 0.2f;

    [Tooltip("Undulation amplitude (height variation within climb)")]
    [Range(0f, 2f)]
    public float undulationAmplitude = 0.5f;

    [Header("Transitions")]
    [Tooltip("Transition zone before climb starts (meters)")]
    [Range(0f, 200f)]
    public float startTransition = 50f;

    [Tooltip("Transition zone after climb ends (meters)")]
    [Range(0f, 200f)]
    public float endTransition = 50f;

    [Header("Checkpoints")]
    [Tooltip("Place checkpoint at climb start")]
    public bool checkpointAtStart = true;

    [Tooltip("Place checkpoint at climb end")]
    public bool checkpointAtEnd = true;

    [Tooltip("Place checkpoint mid-climb")]
    public bool checkpointMidClimb = false;

    [Tooltip("Mid-climb checkpoint position (0-1, 0.5 = halfway)")]
    [Range(0f, 1f)]
    public float midClimbPosition = 0.5f;

    /// <summary>
    /// Get end location of climb
    /// </summary>
    public float EndLocation => startLocation + length;

    /// <summary>
    /// Check if a given distance is within this climb segment
    /// </summary>
    public bool IsWithinClimb(float distance)
    {
        return distance >= startLocation && distance <= EndLocation;
    }

    /// <summary>
    /// Get normalized position within climb (0 = start, 1 = end)
    /// </summary>
    public float GetNormalizedPosition(float distance)
    {
        if (!IsWithinClimb(distance)) return -1f;
        return (distance - startLocation) / length;
    }
}

/// <summary>
/// Difficulty levels for climbs
/// </summary>
public enum DifficultyLevel
{
    Easy = 1,           // 1-3% average grade
    Moderate = 2,       // 3-6% average grade
    Hard = 3,           // 6-10% average grade
    VeryHard = 4,       // 10-15% average grade
    Extreme = 5         // 15%+ average grade
}
