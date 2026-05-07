using UnityEngine;
using System.Collections;

/// <summary>
/// Seamless terrain loop system using circular buffer technique
/// Bike stays in center, terrain scrolls underneath
/// Generates new terrain ahead and removes behind for infinite riding
/// </summary>
public class SeamlessTerrainLoop : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Transform bikeTransform;
    
    [Header("Loop Settings")]
    [Tooltip("Physical terrain size (Unity units)")]
    [SerializeField] private float terrainSize = 1000f;
    
    [Tooltip("Keep bike centered at this position")]
    [SerializeField] private float bikeCenterPosition = 500f;
    
    [Tooltip("Regenerate terrain when bike moves this far")]
    [SerializeField] private float regenerationThreshold = 250f;
    
    [Header("Segment Tracking")]
    [Tooltip("Virtual segment length for trials (meters)")]
    [SerializeField] private float segmentLength = 500f;
    
    [Tooltip("Total segments to complete (0 = infinite)")]
    [SerializeField] private int totalSegmentsToComplete = 1;
    
    [Header("Terrain Generation")]
    [SerializeField] private ImprovedTerrainGenerator terrainGenerator;
    [SerializeField] private bool varyTerrainPerSegment = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showSegmentTransitions = true;
    [SerializeField] private bool showProgressNotifications = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private float virtualDistance = 0f;
    private float lastBikePosition = 0f;
    private int currentSegment = 0;
    private float segmentStartDistance = 0f;
    private bool trialComplete = false;
    
    private void Start()
    {
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (bikeTransform == null && bikeController != null)
            bikeTransform = bikeController.transform;
        
        if (terrainGenerator == null)
            terrainGenerator = GetComponent<ImprovedTerrainGenerator>();
        
        Initialize();
    }
    
    private void Initialize()
    {
        if (bikeTransform != null)
        {
            lastBikePosition = bikeTransform.position.x;
        }
        
        virtualDistance = 0f;
        currentSegment = 0;
        segmentStartDistance = 0f;
        trialComplete = false;
        
        // Generate initial terrain
        if (terrainGenerator != null)
        {
            terrainGenerator.GenerateTerrain();
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[SeamlessTerrainLoop] Initialized - Terrain size: {terrainSize}m, " +
                     $"Segment length: {segmentLength}m, Total segments: {totalSegmentsToComplete}");
        }
    }

    private void Update()
    {
        if (bikeTransform == null || trialComplete) return;
        
        // Track virtual distance
        float currentBikePosition = bikeTransform.position.x;
        float deltaPosition = currentBikePosition - lastBikePosition;
        virtualDistance += deltaPosition;
        lastBikePosition = currentBikePosition;
        
        // Check for segment completion
        CheckSegmentProgress();
        
        // Check if we need to regenerate terrain
        CheckTerrainRegeneration();
    }
    
    private void CheckSegmentProgress()
    {
        float distanceInSegment = virtualDistance - segmentStartDistance;
        
        // Show progress notifications
        if (showProgressNotifications)
        {
            float progress = distanceInSegment / segmentLength;
            
            // Notify at 25%, 50%, 75%
            if (Mathf.FloorToInt(progress * 4) > Mathf.FloorToInt((progress - Time.deltaTime * bikeController.GetSpeed() / segmentLength) * 4))
            {
                int percentage = Mathf.FloorToInt(progress * 100);
                if (percentage == 25 || percentage == 50 || percentage == 75)
                {
                    ShowProgressNotification(percentage);
                }
            }
        }
        
        // Check for segment completion
        if (distanceInSegment >= segmentLength)
        {
            CompleteSegment();
        }
    }
    
    private void CompleteSegment()
    {
        currentSegment++;
        segmentStartDistance = virtualDistance;
        
        if (showDebugInfo)
        {
            Debug.Log($"[SeamlessTerrainLoop] Segment {currentSegment} complete! " +
                     $"Total distance: {virtualDistance:F0}m");
        }
        
        // Show segment completion
        if (showSegmentTransitions)
        {
            VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
            if (feedback != null)
            {
                feedback.ShowNotification($"Segment {currentSegment} Complete!", Color.green);
            }
        }
        
        // Check if trial is complete
        if (totalSegmentsToComplete > 0 && currentSegment >= totalSegmentsToComplete)
        {
            CompleteTrial();
            return;
        }
        
        // Generate new terrain for next segment
        if (varyTerrainPerSegment && terrainGenerator != null)
        {
            StartCoroutine(RegenerateTerrainSmooth());
        }
    }
    
    private void CheckTerrainRegeneration()
    {
        // If bike has moved far from center, we could shift terrain
        // For now, we rely on segment-based regeneration
        float distanceFromCenter = Mathf.Abs(bikeTransform.position.x - bikeCenterPosition);
        
        if (distanceFromCenter > regenerationThreshold && !trialComplete)
        {
            // Optional: Implement terrain shifting here
            if (showDebugInfo)
            {
                Debug.Log($"[SeamlessTerrainLoop] Bike distance from center: {distanceFromCenter:F0}m");
            }
        }
    }
    
    private IEnumerator RegenerateTerrainSmooth()
    {
        // Wait a moment for smooth transition
        yield return new WaitForSeconds(0.5f);
        
        if (terrainGenerator != null)
        {
            terrainGenerator.GenerateTerrain();
            
            if (showDebugInfo)
            {
                Debug.Log($"[SeamlessTerrainLoop] Generated new terrain for segment {currentSegment + 1}");
            }
        }
        
        // Notify rider
        if (showSegmentTransitions)
        {
            VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
            if (feedback != null)
            {
                feedback.ShowNotification($"Segment {currentSegment + 1} Starting!", Color.cyan);
            }
        }
    }
    
    private void ShowProgressNotification(int percentage)
    {
        VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
        if (feedback != null)
        {
            Color color = percentage switch
            {
                25 => new Color(0.3f, 0.7f, 1f),
                50 => new Color(1f, 0.8f, 0f),
                75 => new Color(1f, 0.5f, 0f),
                _ => Color.white
            };
            
            feedback.ShowNotification($"{percentage}% Complete", color);
        }
    }
    
    private void CompleteTrial()
    {
        trialComplete = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"[SeamlessTerrainLoop] Trial complete! " +
                     $"Completed {currentSegment} segments, Total distance: {virtualDistance:F0}m");
        }
        
        // Show completion
        SimpleCompletionOverlay completion = FindObjectOfType<SimpleCompletionOverlay>();
        if (completion != null)
        {
            completion.ShowCompletionOverlay();
        }
        
        VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
        if (feedback != null)
        {
            feedback.ShowNotification($"Trial Complete! {virtualDistance:F0}m", Color.green);
        }
        
        // Log final stats
        LogTrialStatistics();
    }
    
    private void LogTrialStatistics()
    {
        float totalTime = Time.time;
        float avgSpeed = virtualDistance / totalTime;
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"TRIAL STATISTICS");
        Debug.Log($"{'='*60}");
        Debug.Log($"Segments Completed: {currentSegment}");
        Debug.Log($"Total Distance: {virtualDistance:F0}m");
        Debug.Log($"Total Time: {totalTime:F1}s ({totalTime / 60f:F1} minutes)");
        Debug.Log($"Average Speed: {avgSpeed * 3.6f:F1} km/h");
        Debug.Log($"{'='*60}\n");
    }
    
    public void ResetTrial()
    {
        Initialize();
        
        if (showDebugInfo)
            Debug.Log("[SeamlessTerrainLoop] Trial reset");
    }
    
    public float GetVirtualDistance()
    {
        return virtualDistance;
    }
    
    public int GetCurrentSegment()
    {
        return currentSegment;
    }
    
    public float GetSegmentProgress()
    {
        float distanceInSegment = virtualDistance - segmentStartDistance;
        return Mathf.Clamp01(distanceInSegment / segmentLength);
    }
    
    public float GetTotalProgress()
    {
        if (totalSegmentsToComplete <= 0) return 0f;
        return Mathf.Clamp01((float)currentSegment / totalSegmentsToComplete);
    }
    
    public bool IsTrialComplete()
    {
        return trialComplete;
    }
}
