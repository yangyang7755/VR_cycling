using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

/// <summary>
/// Manages trial completion - detects when cyclist reaches the end
/// Stops the bike, shows completion message, and handles trial end
/// </summary>
public class TrialCompletionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private UnityDataLogger dataLogger;
    [SerializeField] private ExperimentalTrialManager trialManager;
    [SerializeField] private ProceduralRouteGenerator routeGenerator;
    
    [Header("UI References")]
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private TextMeshProUGUI completionTitleText;
    [SerializeField] private TextMeshProUGUI completionMessageText;
    [SerializeField] private TextMeshProUGUI statisticsText;
    [SerializeField] private Button nextTrialButton;
    [SerializeField] private Button finishButton;
    
    [Header("Route Settings")]
    [SerializeField] private float routeLength = 500f;
    [SerializeField] private float completionThreshold = 0.95f; // 95% of route
    [SerializeField] private float stopDistance = 5f; // Stop 5m before end
    
    [Header("Trial State")]
    [SerializeField] private bool trialActive = false;
    [SerializeField] private bool trialCompleted = false;
    [SerializeField] private float trialStartTime = 0f;
    [SerializeField] private float trialEndTime = 0f;
    [SerializeField] private float startDistance = 0f;
    
    [Header("Settings")]
    [SerializeField] private bool autoStopAtEnd = true;
    [SerializeField] private bool autoEndTrial = true;
    [SerializeField] private bool showCompletionUI = true;
    
    private bool hasShownCompletion = false;
    private float originalMaxSpeed = 0f;

    private void Start()
    {
        // Auto-find references
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (dataLogger == null)
            dataLogger = FindObjectOfType<UnityDataLogger>();
        
        if (trialManager == null)
            trialManager = FindObjectOfType<ExperimentalTrialManager>();
        
        if (routeGenerator == null)
            routeGenerator = FindObjectOfType<ProceduralRouteGenerator>();
        
        // Setup UI buttons
        if (nextTrialButton != null)
            nextTrialButton.onClick.AddListener(OnNextTrial);
        
        if (finishButton != null)
            finishButton.onClick.AddListener(OnFinish);
        
        // Hide completion panel initially
        if (completionPanel != null)
            completionPanel.SetActive(false);
        
        // Get route length from generator if available
        if (routeGenerator != null)
        {
            routeLength = routeGenerator.GetRouteLength();
        }
    }

    private void Update()
    {
        if (!trialActive || trialCompleted)
            return;
        
        CheckTrialCompletion();
    }

    /// <summary>
    /// Start a new trial
    /// </summary>
    public void StartTrial()
    {
        trialActive = true;
        trialCompleted = false;
        hasShownCompletion = false;
        trialStartTime = Time.time;
        
        if (bikeController != null)
        {
            startDistance = bikeController.GetDistance();
        }
        
        // Get route length from generator
        if (routeGenerator != null)
        {
            routeLength = routeGenerator.GetRouteLength();
        }
        
        // Hide completion panel
        if (completionPanel != null)
            completionPanel.SetActive(false);
        
        Debug.Log($"[TrialCompletion] Trial started - Route length: {routeLength}m");
    }

    /// <summary>
    /// Check if trial is completed
    /// </summary>
    private void CheckTrialCompletion()
    {
        if (bikeController == null)
            return;
        
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - startDistance;
        float progress = distanceFromStart / routeLength;
        
        // Check if reached completion threshold
        if (progress >= completionThreshold && !hasShownCompletion)
        {
            OnTrialCompleted();
        }
        
        // Gradually slow down near the end
        if (autoStopAtEnd && progress >= completionThreshold)
        {
            float remainingDistance = routeLength - distanceFromStart;
            
            if (remainingDistance <= stopDistance)
            {
                // Gradually reduce speed to zero
                float slowdownFactor = Mathf.Clamp01(remainingDistance / stopDistance);
                // You may need to add a method to BikeController to limit speed
                // bikeController.SetSpeedLimit(slowdownFactor);
            }
        }
    }

    /// <summary>
    /// Called when trial is completed
    /// </summary>
    private void OnTrialCompleted()
    {
        if (hasShownCompletion)
            return;
        
        hasShownCompletion = true;
        trialCompleted = true;
        trialEndTime = Time.time;
        
        float trialDuration = trialEndTime - trialStartTime;
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"TRIAL COMPLETED!");
        Debug.Log($"{'='*60}");
        Debug.Log($"Duration: {trialDuration:F1}s ({trialDuration / 60f:F1} minutes)");
        Debug.Log($"Distance: {routeLength:F0}m");
        Debug.Log($"{'='*60}\n");
        
        // Auto-end trial if enabled
        if (autoEndTrial)
        {
            EndTrial();
        }
        
        // Show completion UI
        if (showCompletionUI)
        {
            ShowCompletionUI(trialDuration);
        }
    }

    /// <summary>
    /// End the current trial
    /// </summary>
    private void EndTrial()
    {
        trialActive = false;
        
        // Stop data logging
        if (dataLogger != null && dataLogger.IsLogging())
        {
            dataLogger.StopLogging();
        }
        
        // End trial in trial manager
        if (trialManager != null)
        {
            // trialManager.EndCurrentTrial(); // Uncomment if method exists
        }
        
        Debug.Log("[TrialCompletion] Trial ended - data saved");
    }

    /// <summary>
    /// Show completion UI with statistics
    /// </summary>
    private void ShowCompletionUI(float duration)
    {
        if (completionPanel == null)
            return;
        
        completionPanel.SetActive(true);
        
        // Set title
        if (completionTitleText != null)
        {
            completionTitleText.text = "TRIAL COMPLETED!";
        }
        
        // Set message
        if (completionMessageText != null)
        {
            completionMessageText.text = $"Great job! You completed the {routeLength:F0}m route in {duration:F1} seconds.";
        }
        
        // Set statistics
        if (statisticsText != null)
        {
            string stats = GenerateStatisticsText(duration);
            statisticsText.text = stats;
        }
        
        // Show appropriate buttons
        if (nextTrialButton != null && finishButton != null)
        {
            // Check if more trials remain
            bool hasMoreTrials = trialManager != null; // && trialManager.HasMoreTrials();
            
            nextTrialButton.gameObject.SetActive(hasMoreTrials);
            finishButton.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Generate statistics text for completion UI
    /// </summary>
    private string GenerateStatisticsText(float duration)
    {
        string stats = $"<b>TRIAL STATISTICS</b>\n\n";
        stats += $"Duration: {duration:F1}s ({duration / 60f:F1} min)\n";
        stats += $"Distance: {routeLength:F0}m\n";
        
        if (bikeController != null)
        {
            float avgSpeed = routeLength / duration;
            stats += $"Average Speed: {avgSpeed:F2} m/s ({avgSpeed * 3.6f:F1} km/h)\n";
        }
        
        if (routeGenerator != null)
        {
            stats += $"\nRoute Gradient: {routeGenerator.GetCurrentGradient():F1}%\n";
        }
        
        if (dataLogger != null)
        {
            stats += $"\nData Points Collected: {dataLogger.GetComponent<UnityDataLogger>()}";
        }
        
        stats += $"\n\nPress 'Next Trial' to continue or 'Finish' to end session.";
        
        return stats;
    }

    /// <summary>
    /// Next trial button clicked
    /// </summary>
    private void OnNextTrial()
    {
        Debug.Log("[TrialCompletion] Starting next trial...");
        
        // Hide completion panel
        if (completionPanel != null)
            completionPanel.SetActive(false);
        
        // Start next trial in trial manager
        if (trialManager != null)
        {
            // trialManager.StartNextTrial(); // This will generate new route and reset
        }
        else
        {
            // Manual reset
            ResetForNextTrial();
        }
    }

    /// <summary>
    /// Finish button clicked
    /// </summary>
    private void OnFinish()
    {
        Debug.Log("[TrialCompletion] Session finished");
        
        // Hide completion panel
        if (completionPanel != null)
            completionPanel.SetActive(false);
        
        // Show summary or return to main menu
        // You could add a session summary here
    }

    /// <summary>
    /// Reset for next trial (manual mode)
    /// </summary>
    private void ResetForNextTrial()
    {
        trialActive = false;
        trialCompleted = false;
        hasShownCompletion = false;
        
        // Reset bike position
        if (bikeController != null)
        {
            // You may need to add a reset method to BikeController
            // bikeController.ResetPosition();
        }
        
        // Generate new route
        if (routeGenerator != null)
        {
            routeGenerator.RandomizeAndGenerate();
            routeLength = routeGenerator.GetRouteLength();
        }
        
        // Start new trial
        StartTrial();
        
        // Start logging
        if (dataLogger != null && !dataLogger.IsLogging())
        {
            dataLogger.StartLogging();
        }
    }

    /// <summary>
    /// Set route length
    /// </summary>
    public void SetRouteLength(float length)
    {
        routeLength = length;
    }

    /// <summary>
    /// Check if trial is active
    /// </summary>
    public bool IsTrialActive()
    {
        return trialActive;
    }

    /// <summary>
    /// Check if trial is completed
    /// </summary>
    public bool IsTrialCompleted()
    {
        return trialCompleted;
    }

    /// <summary>
    /// Get trial progress (0-1)
    /// </summary>
    public float GetTrialProgress()
    {
        if (!trialActive || bikeController == null)
            return 0f;
        
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - startDistance;
        return Mathf.Clamp01(distanceFromStart / routeLength);
    }

    /// <summary>
    /// Get remaining distance
    /// </summary>
    public float GetRemainingDistance()
    {
        if (!trialActive || bikeController == null)
            return routeLength;
        
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - startDistance;
        return Mathf.Max(0f, routeLength - distanceFromStart);
    }
}
