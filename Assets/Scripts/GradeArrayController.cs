using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controller that uses pre-computed grade array from LongRouteTerrainBuilder
/// Provides real-time gradient lookup and trainer bridge integration
/// </summary>
public class GradeArrayController : MonoBehaviour
{
    [Header("Grade Array Source")]
    [Tooltip("LongRouteTerrainBuilder that generated the grade array")]
    [SerializeField] private LongRouteTerrainBuilder routeBuilder;

    [Header("Bike Controller")]
    [Tooltip("Bike controller to get current distance")]
    [SerializeField] private BikeController bikeController;

    [Header("Trainer Bridge")]
    [Tooltip("Trainer adapter for setting resistance (e.g., TacxTrainerAdapter)")]
    [SerializeField] private MonoBehaviour trainerAdapter;

    [Header("Visualization")]
    [Tooltip("Enable real-time grade visualization")]
    [SerializeField] private bool enableVisualization = true;

    [Tooltip("Update frequency for trainer grade setting (Hz)")]
    [Range(1f, 20f)]
    [SerializeField] private float updateRate = 10f;

    [Header("Grade Interpolation")]
    [Tooltip("Smooth grade changes over time")]
    [SerializeField] private bool smoothGradeChanges = true;

    [Tooltip("Smoothing speed (higher = faster response)")]
    [Range(1f, 20f)]
    [SerializeField] private float smoothingSpeed = 5f;

    // Cached grade array data
    private float[] gradeArray;
    private float sampleInterval;
    private float routeStartDistance = 0f;

    // Current state
    private float currentGrade = 0f;
    private float targetGrade = 0f;
    private float lastUpdateTime = 0f;
    private float updateInterval;

    private void Start()
    {
        // Auto-find components
        if (routeBuilder == null)
        {
            routeBuilder = FindObjectOfType<LongRouteTerrainBuilder>();
        }

        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        if (trainerAdapter == null)
        {
            trainerAdapter = FindObjectOfType<TacxTrainerAdapter>();
        }

        updateInterval = 1f / updateRate;

        // Load grade array from route builder
        LoadGradeArray();
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval) return;

        UpdateCurrentGrade();
        ApplyGradeToTrainer();

        lastUpdateTime = Time.time;
    }

    /// <summary>
    /// Load grade array from route builder
    /// </summary>
    public void LoadGradeArray()
    {
        if (routeBuilder == null)
        {
            Debug.LogWarning("[GradeArrayController] Route builder not found. Cannot load grade array.");
            return;
        }

        gradeArray = routeBuilder.GetGradeArray();
        sampleInterval = routeBuilder.GetSampleInterval();

        if (gradeArray == null || gradeArray.Length == 0)
        {
            Debug.LogWarning("[GradeArrayController] Grade array is empty. Route may not be built yet.");
            return;
        }

        // Record route start distance
        if (bikeController != null)
        {
            routeStartDistance = bikeController.GetDistance();
        }

        Debug.Log($"[GradeArrayController] Loaded grade array: {gradeArray.Length} samples, interval: {sampleInterval:F2}m");
    }

    /// <summary>
    /// Set grade array manually (called by LongRouteTerrainBuilder after generation)
    /// </summary>
    public void SetGradeArray(float[] grades, float interval)
    {
        gradeArray = grades;
        sampleInterval = interval;

        if (bikeController != null)
        {
            routeStartDistance = bikeController.GetDistance();
        }

        Debug.Log($"[GradeArrayController] Grade array set: {grades.Length} samples, interval: {interval:F2}m");
    }

    /// <summary>
    /// Update current grade based on bike position
    /// </summary>
    private void UpdateCurrentGrade()
    {
        if (gradeArray == null || gradeArray.Length == 0 || bikeController == null) return;

        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - routeStartDistance;

        // Find grade at current distance
        targetGrade = GetGradeAtDistance(distanceFromStart);

        // Smooth grade changes
        if (smoothGradeChanges)
        {
            currentGrade = Mathf.Lerp(currentGrade, targetGrade, Time.deltaTime * smoothingSpeed);
        }
        else
        {
            currentGrade = targetGrade;
        }
    }

    /// <summary>
    /// Get grade at a specific distance (interpolated from array)
    /// </summary>
    private float GetGradeAtDistance(float distance)
    {
        if (gradeArray == null || gradeArray.Length == 0 || sampleInterval <= 0f) return 0f;

        // Clamp distance to valid range
        float maxDistance = (gradeArray.Length - 1) * sampleInterval;
        distance = Mathf.Clamp(distance, 0f, maxDistance);

        // Find sample indices
        float sampleIndex = distance / sampleInterval;
        int index0 = Mathf.FloorToInt(sampleIndex);
        int index1 = Mathf.Min(index0 + 1, gradeArray.Length - 1);

        index0 = Mathf.Clamp(index0, 0, gradeArray.Length - 1);

        // Linear interpolation
        float t = sampleIndex - index0;
        float grade0 = gradeArray[index0];
        float grade1 = gradeArray[index1];

        return Mathf.Lerp(grade0, grade1, t);
    }

    /// <summary>
    /// Apply current grade to trainer bridge
    /// </summary>
    private void ApplyGradeToTrainer()
    {
        if (trainerAdapter == null) return;

        // Try to find SetSlope or SetGrade method
        var setSlopeMethod = trainerAdapter.GetType().GetMethod("SetSlope");
        if (setSlopeMethod != null)
        {
            setSlopeMethod.Invoke(trainerAdapter, new object[] { currentGrade });
            return;
        }

        var setGradeMethod = trainerAdapter.GetType().GetMethod("SetGrade");
        if (setGradeMethod != null)
        {
            setGradeMethod.Invoke(trainerAdapter, new object[] { currentGrade });
            return;
        }

        // Try TacxTrainerAdapter specifically
        if (trainerAdapter is TacxTrainerAdapter tacxAdapter)
        {
            // TacxTrainerAdapter might have a different method name
            var setGradientMethod = trainerAdapter.GetType().GetMethod("SetGradient");
            if (setGradientMethod != null)
            {
                setGradientMethod.Invoke(tacxAdapter, new object[] { currentGrade });
            }
        }
    }

    /// <summary>
    /// Get current grade (for visualization or other systems)
    /// </summary>
    public float GetCurrentGrade()
    {
        return currentGrade;
    }

    /// <summary>
    /// Get grade at a specific distance along the route (public method)
    /// </summary>
    public float GetGradeAtRouteDistance(float distanceFromStart)
    {
        return GetGradeAtDistance(distanceFromStart);
    }

    /// <summary>
    /// Reset grade controller (call when starting a new route)
    /// </summary>
    public void ResetController()
    {
        if (bikeController != null)
        {
            routeStartDistance = bikeController.GetDistance();
        }
        currentGrade = 0f;
        targetGrade = 0f;
    }
}
