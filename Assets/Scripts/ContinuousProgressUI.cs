using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI for continuous multi-segment trials
/// Shows overall progress, current segment, and upcoming segments
/// </summary>
public class ContinuousProgressUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SeamlessTerrainLoop terrainLoop;
    [SerializeField] private BikeController bikeController;
    
    [Header("UI Elements - Overall Progress")]
    [SerializeField] private Slider overallProgressBar;
    [SerializeField] private TextMeshProUGUI overallProgressText;
    [SerializeField] private TextMeshProUGUI totalDistanceText;
    
    [Header("UI Elements - Current Segment")]
    [SerializeField] private Slider segmentProgressBar;
    [SerializeField] private TextMeshProUGUI segmentNumberText;
    [SerializeField] private TextMeshProUGUI segmentProgressText;
    [SerializeField] private TextMeshProUGUI segmentDistanceText;
    
    [Header("UI Elements - Stats")]
    [SerializeField] private TextMeshProUGUI timeElapsedText;
    [SerializeField] private TextMeshProUGUI avgSpeedText;
    [SerializeField] private TextMeshProUGUI segmentsCompletedText;
    
    [Header("Visual Settings")]
    [SerializeField] private Color segmentCompleteColor = Color.green;
    [SerializeField] private Color segmentActiveColor = Color.yellow;
    [SerializeField] private bool animateProgress = true;
    
    [Header("Update Settings")]
    [SerializeField] private float updateRate = 10f;
    
    private float updateInterval;
    private float lastUpdateTime;
    private float sessionStartTime;
    
    private void Start()
    {
        if (terrainLoop == null)
            terrainLoop = FindObjectOfType<SeamlessTerrainLoop>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        updateInterval = 1f / updateRate;
        sessionStartTime = Time.time;
    }
    
    private void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        UpdateAllDisplays();
    }
    
    private void UpdateAllDisplays()
    {
        if (terrainLoop == null) return;
        
        UpdateOverallProgress();
        UpdateSegmentProgress();
        UpdateStats();
    }
    
    private void UpdateOverallProgress()
    {
        float totalProgress = terrainLoop.GetTotalProgress();
        
        if (overallProgressBar != null)
        {
            if (animateProgress)
            {
                overallProgressBar.value = Mathf.Lerp(
                    overallProgressBar.value,
                    totalProgress,
                    Time.deltaTime * 5f
                );
            }
            else
            {
                overallProgressBar.value = totalProgress;
            }
        }
        
        if (overallProgressText != null)
        {
            overallProgressText.text = $"{totalProgress * 100f:F0}%";
        }
        
        if (totalDistanceText != null)
        {
            float distance = terrainLoop.GetVirtualDistance();
            totalDistanceText.text = $"{distance:F0}m";
        }
    }
    
    private void UpdateSegmentProgress()
    {
        float segmentProgress = terrainLoop.GetSegmentProgress();
        int currentSegment = terrainLoop.GetCurrentSegment();
        
        if (segmentProgressBar != null)
        {
            if (animateProgress)
            {
                segmentProgressBar.value = Mathf.Lerp(
                    segmentProgressBar.value,
                    segmentProgress,
                    Time.deltaTime * 5f
                );
            }
            else
            {
                segmentProgressBar.value = segmentProgress;
            }
            
            // Color based on progress
            Image fillImage = segmentProgressBar.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = Color.Lerp(segmentActiveColor, segmentCompleteColor, segmentProgress);
            }
        }
        
        if (segmentNumberText != null)
        {
            segmentNumberText.text = $"Segment {currentSegment + 1}";
        }
        
        if (segmentProgressText != null)
        {
            segmentProgressText.text = $"{segmentProgress * 100f:F0}%";
        }
        
        if (segmentDistanceText != null)
        {
            // Assuming 500m segments
            float segmentDistance = segmentProgress * 500f;
            segmentDistanceText.text = $"{segmentDistance:F0}m / 500m";
        }
    }
    
    private void UpdateStats()
    {
        float elapsedTime = Time.time - sessionStartTime;
        
        if (timeElapsedText != null)
        {
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            timeElapsedText.text = $"{minutes:D2}:{seconds:D2}";
        }
        
        if (avgSpeedText != null && bikeController != null)
        {
            float distance = terrainLoop.GetVirtualDistance();
            float avgSpeed = distance / elapsedTime;
            avgSpeedText.text = $"{avgSpeed * 3.6f:F1} km/h";
        }
        
        if (segmentsCompletedText != null)
        {
            int completed = terrainLoop.GetCurrentSegment();
            segmentsCompletedText.text = $"{completed}";
        }
    }
    
    public void ResetUI()
    {
        sessionStartTime = Time.time;
        
        if (overallProgressBar != null)
            overallProgressBar.value = 0f;
        
        if (segmentProgressBar != null)
            segmentProgressBar.value = 0f;
    }
}
