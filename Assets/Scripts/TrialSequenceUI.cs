using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI display for trial sequence progress
/// Shows current trial, overall progress, and upcoming trials
/// </summary>
public class TrialSequenceUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ConfigurableTrialSequence trialSequence;
    [SerializeField] private BikeController bikeController;
    
    [Header("UI Elements - Current Trial")]
    [SerializeField] private TextMeshProUGUI currentTrialNameText;
    [SerializeField] private TextMeshProUGUI currentTrialProgressText;
    [SerializeField] private Slider currentTrialProgressBar;
    [SerializeField] private TextMeshProUGUI currentTrialDistanceText;
    
    [Header("UI Elements - Overall Progress")]
    [SerializeField] private TextMeshProUGUI overallProgressText;
    [SerializeField] private Slider overallProgressBar;
    [SerializeField] private TextMeshProUGUI trialsCompletedText;
    
    [Header("UI Elements - Stats")]
    [SerializeField] private TextMeshProUGUI currentSpeedText;
    [SerializeField] private TextMeshProUGUI currentPowerText;
    [SerializeField] private TextMeshProUGUI currentGradientText;
    [SerializeField] private TextMeshProUGUI timeElapsedText;
    
    [Header("UI Elements - Next Trial")]
    [SerializeField] private GameObject nextTrialPanel;
    [SerializeField] private TextMeshProUGUI nextTrialNameText;
    [SerializeField] private TextMeshProUGUI nextTrialInfoText;
    
    [Header("Update Settings")]
    [SerializeField] private float updateRate = 10f;
    
    private float updateInterval;
    private float lastUpdateTime;
    private float sessionStartTime;
    
    private void Start()
    {
        if (trialSequence == null)
            trialSequence = FindObjectOfType<ConfigurableTrialSequence>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        updateInterval = 1f / updateRate;
        sessionStartTime = Time.time;
        
        if (nextTrialPanel != null)
            nextTrialPanel.SetActive(false);
    }
    
    private void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        
        if (trialSequence != null && trialSequence.IsSequenceActive())
        {
            UpdateAllDisplays();
        }
    }
    
    private void UpdateAllDisplays()
    {
        UpdateCurrentTrialDisplay();
        UpdateOverallProgressDisplay();
        UpdateStatsDisplay();
        UpdateNextTrialDisplay();
    }
    
    private void UpdateCurrentTrialDisplay()
    {
        var currentTrial = trialSequence.GetCurrentTrial();
        if (currentTrial == null) return;
        
        float progress = trialSequence.GetCurrentTrialProgress();
        
        // Trial name
        if (currentTrialNameText != null)
        {
            int trialNum = trialSequence.GetCurrentTrialIndex() + 1;
            int totalTrials = trialSequence.GetTotalTrials();
            currentTrialNameText.text = $"{currentTrial.trialName} ({trialNum}/{totalTrials})";
        }
        
        // Progress percentage
        if (currentTrialProgressText != null)
        {
            currentTrialProgressText.text = $"{progress * 100f:F0}%";
        }
        
        // Progress bar
        if (currentTrialProgressBar != null)
        {
            currentTrialProgressBar.value = progress;
        }
        
        // Distance
        if (currentTrialDistanceText != null)
        {
            float distanceCovered = progress * currentTrial.lengthMeters;
            currentTrialDistanceText.text = $"{distanceCovered:F0}m / {currentTrial.lengthMeters:F0}m";
        }
    }
    
    private void UpdateOverallProgressDisplay()
    {
        int completed = trialSequence.GetCompletedTrialsCount();
        int total = trialSequence.GetTotalTrials();
        int current = trialSequence.GetCurrentTrialIndex();
        
        // Overall progress text
        if (overallProgressText != null)
        {
            float overallProgress = (completed + trialSequence.GetCurrentTrialProgress()) / total;
            overallProgressText.text = $"Overall: {overallProgress * 100f:F0}%";
        }
        
        // Overall progress bar
        if (overallProgressBar != null)
        {
            float overallProgress = (completed + trialSequence.GetCurrentTrialProgress()) / total;
            overallProgressBar.value = overallProgress;
        }
        
        // Trials completed
        if (trialsCompletedText != null)
        {
            trialsCompletedText.text = $"Trials: {completed}/{total}";
        }
    }
    
    private void UpdateStatsDisplay()
    {
        if (bikeController == null) return;
        
        // Speed
        if (currentSpeedText != null)
        {
            float speed = bikeController.GetSpeedKmh();
            currentSpeedText.text = $"{speed:F1} km/h";
        }
        
        // Power
        if (currentPowerText != null)
        {
            float power = bikeController.GetPower();
            currentPowerText.text = $"{power:F0}W";
        }
        
        // Gradient
        if (currentGradientText != null)
        {
            float gradient = bikeController.GetGradient();
            string sign = gradient >= 0 ? "+" : "";
            currentGradientText.text = $"{sign}{gradient:F1}%";
        }
        
        // Time elapsed
        if (timeElapsedText != null)
        {
            float elapsed = Time.time - sessionStartTime;
            int minutes = Mathf.FloorToInt(elapsed / 60f);
            int seconds = Mathf.FloorToInt(elapsed % 60f);
            timeElapsedText.text = $"{minutes:D2}:{seconds:D2}";
        }
    }
    
    private void UpdateNextTrialDisplay()
    {
        if (nextTrialPanel == null) return;
        
        int currentIndex = trialSequence.GetCurrentTrialIndex();
        int totalTrials = trialSequence.GetTotalTrials();
        
        // Check if there's a next trial
        if (currentIndex + 1 < totalTrials)
        {
            nextTrialPanel.SetActive(true);
            
            // This would need to be added to ConfigurableTrialSequence
            // For now, just show that there's a next trial
            if (nextTrialNameText != null)
            {
                nextTrialNameText.text = $"Next: Trial {currentIndex + 2}";
            }
        }
        else
        {
            nextTrialPanel.SetActive(false);
        }
    }
    
    public void ResetUI()
    {
        sessionStartTime = Time.time;
        
        if (currentTrialProgressBar != null)
            currentTrialProgressBar.value = 0f;
        
        if (overallProgressBar != null)
            overallProgressBar.value = 0f;
    }
}
