using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Continuous terrain system that generates seamless 500m segments
/// Creates an endless riding experience by generating terrain ahead and removing behind
/// Maintains smooth transitions between segments for natural feel
/// </summary>
public class ContinuousTerrainSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private BikeController bikeController;
    [SerializeField] private ImprovedTerrainGenerator terrainGenerator;
    
    [Header("Segment Settings")]
    [Tooltip("Length of each terrain segment (meters)")]
    [SerializeField] private float segmentLength = 500f;
    
    [Tooltip("Generate new segment when this distance ahead")]
    [SerializeField] private float generationTriggerDistance = 250f;
    
    [Tooltip("Number of segments to keep loaded")]
    [SerializeField] private int activeSegmentCount = 3;
    
    [Header("Transition Settings")]
    [Tooltip("Blend distance between segments for smooth transitions")]
    [SerializeField] private float transitionBlendDistance = 50f;
    
    [Tooltip("Match elevation at segment boundaries")]
    [SerializeField] private bool matchElevationAtBoundaries = true;
    
    [Header("Variety Settings")]
    [Tooltip("Randomize each segment")]
    [SerializeField] private bool randomizeSegments = true;
    
    [Tooltip("Terrain profiles to cycle through")]
    [SerializeField] private ImprovedTerrainGenerator.TerrainProfile[] segmentProfiles = {
        ImprovedTerrainGenerator.TerrainProfile.Rolling,
        ImprovedTerrainGenerator.TerrainProfile.Hilly,
        ImprovedTerrainGenerator.TerrainProfile.Flat
    };
    
    [Header("Trial Mode")]
    [Tooltip("Stop generating after X segments (0 = infinite)")]
    [SerializeField] private int maxSegments = 0;
    
    [Tooltip("Show completion overlay when max segments reached")]
    [SerializeField] private bool showCompletionAtEnd = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool logSegmentGeneration = true;
    
    private List<TerrainSegment> activeSegments = new List<TerrainSegment>();
    private int currentSegmentIndex = 0;
    private int totalSegmentsGenerated = 0;
    private float lastGenerationCheckDistance = 0f;
    private bool trialComplete = false;
    
    [System.Serializable]
    private class TerrainSegment
    {
        public int index;
        public float startDistance;
        public float endDistance;
        public float startElevation;
        public float endElevation;
        public ImprovedTerrainGenerator.TerrainProfile profile;
        public int seed;
    }
    
    private void Start()
    {
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (terrainGenerator == null)
            terrainGenerator = GetComponent<ImprovedTerrainGenerator>();
        
        if (terrainGenerator == null)
            terrainGenerator = gameObject.AddComponent<ImprovedTerrainGenerator>();
        
        InitializeFirstSegment();
    }

    private void InitializeFirstSegment()
    {
        if (showDebugInfo)
            Debug.Log("[ContinuousTerrainSystem] Initializing first segment...");
        
        GenerateNextSegment();
        lastGenerationCheckDistance = 0f;
    }
    
    private void Update()
    {
        if (bikeController == null || trialComplete) return;
        
        float currentDistance = bikeController.GetDistance();
        
        // Check if we need to generate next segment
        if (currentDistance - lastGenerationCheckDistance >= generationTriggerDistance)
        {
            CheckAndGenerateNextSegment(currentDistance);
            lastGenerationCheckDistance = currentDistance;
        }
        
        // Update current segment index
        UpdateCurrentSegment(currentDistance);
    }
    
    private void CheckAndGenerateNextSegment(float currentDistance)
    {
        // Check if we've reached max segments
        if (maxSegments > 0 && totalSegmentsGenerated >= maxSegments)
        {
            if (!trialComplete)
            {
                CompleteTrial();
            }
            return;
        }
        
        // Calculate distance to end of last segment
        if (activeSegments.Count > 0)
        {
            TerrainSegment lastSegment = activeSegments[activeSegments.Count - 1];
            float distanceToEnd = lastSegment.endDistance - currentDistance;
            
            // Generate if we're getting close to the end
            if (distanceToEnd < generationTriggerDistance)
            {
                GenerateNextSegment();
            }
        }
    }
    
    private void GenerateNextSegment()
    {
        // Create new segment
        TerrainSegment newSegment = new TerrainSegment();
        newSegment.index = totalSegmentsGenerated;
        
        // Calculate start and end distances
        if (activeSegments.Count > 0)
        {
            TerrainSegment lastSegment = activeSegments[activeSegments.Count - 1];
            newSegment.startDistance = lastSegment.endDistance;
            newSegment.startElevation = lastSegment.endElevation;
        }
        else
        {
            newSegment.startDistance = 0f;
            newSegment.startElevation = 50f; // Default starting elevation
        }
        
        newSegment.endDistance = newSegment.startDistance + segmentLength;
        
        // Select terrain profile
        if (randomizeSegments)
        {
            newSegment.profile = segmentProfiles[Random.Range(0, segmentProfiles.Length)];
            newSegment.seed = Random.Range(0, 999999);
        }
        else
        {
            int profileIndex = totalSegmentsGenerated % segmentProfiles.Length;
            newSegment.profile = segmentProfiles[profileIndex];
            newSegment.seed = totalSegmentsGenerated * 1000;
        }
        
        // Generate terrain for this segment
        GenerateSegmentTerrain(newSegment);
        
        // Add to active segments
        activeSegments.Add(newSegment);
        totalSegmentsGenerated++;
        
        // Remove old segments if we have too many
        while (activeSegments.Count > activeSegmentCount)
        {
            activeSegments.RemoveAt(0);
        }
        
        if (logSegmentGeneration)
        {
            Debug.Log($"[ContinuousTerrainSystem] Generated segment {newSegment.index}: " +
                     $"{newSegment.startDistance:F0}m - {newSegment.endDistance:F0}m, " +
                     $"Profile: {newSegment.profile}, Seed: {newSegment.seed}");
        }
    }
    
    private void GenerateSegmentTerrain(TerrainSegment segment)
    {
        if (terrainGenerator == null || terrain == null) return;
        
        // Generate terrain using ImprovedTerrainGenerator
        // This is a simplified version - you may need to modify ImprovedTerrainGenerator
        // to support segment-based generation
        
        terrainGenerator.GenerateTerrain();
        
        // Calculate end elevation for next segment
        segment.endElevation = CalculateEndElevation(segment);
    }
    
    private float CalculateEndElevation(TerrainSegment segment)
    {
        // Sample terrain at the end of this segment
        if (terrain == null) return segment.startElevation;
        
        Vector3 bikePos = bikeController != null ? bikeController.transform.position : Vector3.zero;
        Vector3 endPos = new Vector3(segment.endDistance, 0f, bikePos.z);
        
        return terrain.SampleHeight(endPos);
    }
    
    private void UpdateCurrentSegment(float currentDistance)
    {
        for (int i = 0; i < activeSegments.Count; i++)
        {
            TerrainSegment segment = activeSegments[i];
            if (currentDistance >= segment.startDistance && currentDistance < segment.endDistance)
            {
                if (currentSegmentIndex != i)
                {
                    currentSegmentIndex = i;
                    OnSegmentEntered(segment);
                }
                break;
            }
        }
    }
    
    private void OnSegmentEntered(TerrainSegment segment)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[ContinuousTerrainSystem] Entered segment {segment.index} ({segment.profile})");
        }
        
        // Notify visual feedback system
        VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
        if (feedback != null)
        {
            string message = GetSegmentMessage(segment);
            feedback.ShowNotification(message, GetSegmentColor(segment.profile));
        }
    }
    
    private string GetSegmentMessage(TerrainSegment segment)
    {
        switch (segment.profile)
        {
            case ImprovedTerrainGenerator.TerrainProfile.Flat:
                return "Flat Section - Time to Push!";
            case ImprovedTerrainGenerator.TerrainProfile.Rolling:
                return "Rolling Hills Ahead";
            case ImprovedTerrainGenerator.TerrainProfile.Hilly:
                return "Hilly Terrain - Stay Strong!";
            case ImprovedTerrainGenerator.TerrainProfile.Mountainous:
                return "Mountain Climb - Dig Deep!";
            default:
                return $"New Segment - {segment.profile}";
        }
    }
    
    private Color GetSegmentColor(ImprovedTerrainGenerator.TerrainProfile profile)
    {
        switch (profile)
        {
            case ImprovedTerrainGenerator.TerrainProfile.Flat:
                return new Color(0.3f, 1f, 0.3f); // Green
            case ImprovedTerrainGenerator.TerrainProfile.Rolling:
                return new Color(0.3f, 0.7f, 1f); // Blue
            case ImprovedTerrainGenerator.TerrainProfile.Hilly:
                return new Color(1f, 0.7f, 0f); // Orange
            case ImprovedTerrainGenerator.TerrainProfile.Mountainous:
                return new Color(1f, 0.3f, 0.3f); // Red
            default:
                return Color.white;
        }
    }
    
    private void CompleteTrial()
    {
        trialComplete = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"[ContinuousTerrainSystem] Trial complete! Generated {totalSegmentsGenerated} segments");
        }
        
        if (showCompletionAtEnd)
        {
            SimpleCompletionOverlay completion = FindObjectOfType<SimpleCompletionOverlay>();
            if (completion != null)
            {
                completion.ShowCompletionOverlay();
            }
            
            VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
            if (feedback != null)
            {
                feedback.ShowNotification("Trial Complete!", Color.green);
            }
        }
    }
    
    public void ResetTrial()
    {
        activeSegments.Clear();
        currentSegmentIndex = 0;
        totalSegmentsGenerated = 0;
        lastGenerationCheckDistance = 0f;
        trialComplete = false;
        
        InitializeFirstSegment();
        
        if (showDebugInfo)
            Debug.Log("[ContinuousTerrainSystem] Trial reset");
    }
    
    public int GetCurrentSegmentIndex()
    {
        return currentSegmentIndex;
    }
    
    public int GetTotalSegmentsGenerated()
    {
        return totalSegmentsGenerated;
    }
    
    public float GetProgressInCurrentSegment()
    {
        if (bikeController == null || activeSegments.Count == 0) return 0f;
        
        float currentDistance = bikeController.GetDistance();
        TerrainSegment currentSegment = activeSegments[currentSegmentIndex];
        
        float segmentProgress = (currentDistance - currentSegment.startDistance) / segmentLength;
        return Mathf.Clamp01(segmentProgress);
    }
    
    public float GetTotalDistance()
    {
        return totalSegmentsGenerated * segmentLength;
    }
}
