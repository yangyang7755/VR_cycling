using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Simple completion overlay - shows "TRIAL COMPLETE" message
/// Minimal UI that appears when trial ends
/// </summary>
public class SimpleCompletionOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private UnityDataLogger dataLogger;
    [SerializeField] private ProceduralRouteGenerator routeGenerator;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject overlayPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI instructionsText;
    
    [Header("Settings")]
    [SerializeField] private float routeLength = 500f;
    [SerializeField] private float completionThreshold = 0.98f; // 98% of route
    [SerializeField] private Color completionColor = Color.green;
    
    private bool trialActive = false;
    private bool trialCompleted = false;
    private float startDistance = 0f;
    private float trialStartTime = 0f;

    private void Start()
    {
        // Auto-find references
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (dataLogger == null)
            dataLogger = FindObjectOfType<UnityDataLogger>();
        
        if (routeGenerator == null)
            routeGenerator = FindObjectOfType<ProceduralRouteGenerator>();
        
        // Get route length
        if (routeGenerator != null)
        {
            routeLength = routeGenerator.GetRouteLength();
        }
        
        // Hide overlay initially
        if (overlayPanel != null)
            overlayPanel.SetActive(false);
        
        // Start trial automatically
        StartTrial();
    }

    private void Update()
    {
        if (!trialActive || trialCompleted)
            return;
        
        CheckCompletion();
        
        // Manual end with E key
        if (Input.GetKeyDown(KeyCode.E))
        {
            ForceEndTrial();
        }
    }

    /// <summary>
    /// Start trial
    /// </summary>
    public void StartTrial()
    {
        trialActive = true;
        trialCompleted = false;
        trialStartTime = Time.time;
        
        if (bikeController != null)
        {
            startDistance = bikeController.GetDistance();
            bikeController.SetMaxCourseDistance(startDistance + routeLength);
        }
        
        if (routeGenerator != null)
        {
            routeLength = routeGenerator.GetRouteLength();
        }
        
        if (overlayPanel != null)
            overlayPanel.SetActive(false);
        
        Debug.Log($"[Completion] Trial started - {routeLength}m route");
    }

    /// <summary>
    /// Check if trial is completed
    /// </summary>
    private void CheckCompletion()
    {
        if (bikeController == null)
            return;
        
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - startDistance;
        float progress = distanceFromStart / routeLength;
        
        if (progress >= completionThreshold)
        {
            CompleteTrialAutomatically();
        }
    }

    /// <summary>
    /// Complete trial automatically
    /// </summary>
    private void CompleteTrialAutomatically()
    {
        if (trialCompleted)
            return;
        
        trialCompleted = true;
        trialActive = false;
        
        float duration = Time.time - trialStartTime;
        
        // Stop logging
        if (dataLogger != null && dataLogger.IsLogging())
        {
            dataLogger.StopLogging();
        }
        
        // Show completion overlay
        ShowCompletionOverlay(duration);
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"✓ TRIAL COMPLETED!");
        Debug.Log($"{'='*60}");
        Debug.Log($"Duration: {duration:F1}s");
        Debug.Log($"Distance: {routeLength:F0}m");
        Debug.Log($"Average Speed: {(routeLength / duration) * 3.6f:F1} km/h");
        Debug.Log($"\nPress 'N' for next trial or 'T' to enter new trial info");
        Debug.Log($"{'='*60}\n");
    }

    /// <summary>
    /// Force end trial (E key)
    /// </summary>
    private void ForceEndTrial()
    {
        if (trialCompleted)
            return;
        
        trialCompleted = true;
        trialActive = false;
        
        float duration = Time.time - trialStartTime;
        float currentDistance = bikeController != null ? bikeController.GetDistance() : 0f;
        float distanceFromStart = currentDistance - startDistance;
        
        // Stop logging
        if (dataLogger != null && dataLogger.IsLogging())
        {
            dataLogger.StopLogging();
        }
        
        // Show completion overlay
        ShowCompletionOverlay(duration, distanceFromStart);
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"✓ TRIAL ENDED (Manual)");
        Debug.Log($"{'='*60}");
        Debug.Log($"Duration: {duration:F1}s");
        Debug.Log($"Distance: {distanceFromStart:F0}m / {routeLength:F0}m");
        Debug.Log($"\nPress 'N' for next trial or 'T' to enter new trial info");
        Debug.Log($"{'='*60}\n");
    }

    /// <summary>
    /// Show completion overlay with duration and distance
    /// </summary>
    public void ShowCompletionOverlay(float duration, float? actualDistance = null)
    {
        if (overlayPanel == null)
            return;
        
        overlayPanel.SetActive(true);
        
        // Set title
        if (titleText != null)
        {
            titleText.text = "TRIAL COMPLETE!";
            titleText.color = completionColor;
        }
        
        // Set message
        if (messageText != null)
        {
            float distance = actualDistance ?? routeLength;
            float avgSpeed = distance / duration;
            
            messageText.text = $"Duration: {duration:F1}s\n" +
                             $"Distance: {distance:F0}m\n" +
                             $"Avg Speed: {avgSpeed * 3.6f:F1} km/h";
            
            if (routeGenerator != null)
            {
                messageText.text += $"\nGradient: {routeGenerator.GetCurrentGradient():F1}%";
            }
        }
        
        // Set instructions
        if (instructionsText != null)
        {
            instructionsText.text = "Press 'N' for Next Trial\nPress 'T' to Enter New Trial Info";
        }
    }
    
    /// <summary>
    /// Show completion overlay (simple version)
    /// </summary>
    public void ShowCompletionOverlay()
    {
        if (overlayPanel == null)
            return;
        
        overlayPanel.SetActive(true);
        
        // Set title
        if (titleText != null)
        {
            titleText.text = "TRIAL COMPLETE!";
            titleText.color = completionColor;
        }
        
        // Set simple message
        if (messageText != null)
        {
            messageText.text = "Trial completed successfully!";
        }
        
        // Set instructions
        if (instructionsText != null)
        {
            instructionsText.text = "Press 'N' for Next Trial\nPress 'T' to Enter New Trial Info";
        }
    }

    /// <summary>
    /// Hide overlay and prepare for next trial
    /// </summary>
    public void PrepareNextTrial()
    {
        if (overlayPanel != null)
            overlayPanel.SetActive(false);
        
        trialCompleted = false;
        
        // Don't start automatically - wait for user to press N or T
    }

    /// <summary>
    /// Check if trial is completed
    /// </summary>
    public bool IsCompleted()
    {
        return trialCompleted;
    }

    /// <summary>
    /// Get trial progress
    /// </summary>
    public float GetProgress()
    {
        if (!trialActive || bikeController == null)
            return 0f;
        
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - startDistance;
        return Mathf.Clamp01(distanceFromStart / routeLength);
    }
}
