using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Displays a panel when the cyclist reaches the end of a trial (500m)
/// Shows message and button to continue to next trial
/// </summary>
public class TrialEndPanel : MonoBehaviour
{
    [Header("UI Panel")]
    [Tooltip("The panel GameObject to show/hide")]
    [SerializeField] private GameObject endPanel;

    [Header("UI Elements")]
    [Tooltip("Main message text")]
    [SerializeField] private TextMeshProUGUI messageText;
    
    [Tooltip("Continue button")]
    [SerializeField] private Button continueButton;
    
    [Tooltip("Button text (optional)")]
    [SerializeField] private TextMeshProUGUI buttonText;

    [Header("Settings")]
    [Tooltip("Total trial distance in meters")]
    [SerializeField] private float trialDistance = 500f;
    
    [Tooltip("Auto-find bike controller")]
    [SerializeField] private bool autoFindBikeController = true;
    
    [Tooltip("Auto-find trial terrain manager")]
    [SerializeField] private bool autoFindTrialManager = true;

    [Header("References")]
    [Tooltip("Bike controller to track distance")]
    [SerializeField] private BikeController bikeController;
    
    // OLD SYSTEM - Replaced by ExperimentalTrialManager and ProceduralRouteGenerator
    // [Tooltip("Trial terrain manager to start next trial")]
    // [SerializeField] private TrialTerrainManager trialTerrainManager;
    
    [Tooltip("Bike data UI to reset progress")]
    [SerializeField] private BikeDataUI bikeDataUI;

    private float trialStartDistance = 0f;
    private bool hasReachedEnd = false;
    private bool isPanelVisible = false;

    private void Start()
    {
        // Auto-find components
        if (autoFindBikeController && bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        if (autoFindTrialManager)
        {
            // OLD SYSTEM - Commented out
            // trialTerrainManager = FindObjectOfType<TrialTerrainManager>();
        }

        if (bikeDataUI == null)
        {
            bikeDataUI = FindObjectOfType<BikeDataUI>();
        }

        // Setup button
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonClicked);
        }

        // Setup button text
        if (buttonText != null && buttonText.text == "")
        {
            buttonText.text = "Continue to Next Trial";
        }

        // Hide panel initially
        if (endPanel != null)
        {
            endPanel.SetActive(false);
        }

        // Initialize trial start distance
        ResetTrial();
    }

    private void Update()
    {
        if (bikeController == null || hasReachedEnd || isPanelVisible) return;

        // Check if cyclist has reached the end
        float currentDistance = bikeController.GetDistance();
        float distanceTravelled = currentDistance - trialStartDistance;

        if (distanceTravelled >= trialDistance)
        {
            ShowEndPanel();
        }
    }

    /// <summary>
    /// Show the end panel (can be called externally, e.g., from ElevationProgressBar)
    /// </summary>
    public void ShowEndPanel()
    {
        // #region agent log
        System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log",
            $"{{\"id\":\"show_end_panel_entered\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"TrialEndPanel.cs:ShowEndPanel\",\"message\":\"ShowEndPanel called\",\"data\":{{\"hasReachedEnd\":{hasReachedEnd},\"isPanelVisible\":{isPanelVisible},\"endPanelIsNull\":{endPanel == null}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"G\"}}\n");
        // #endregion
        
        // If panel is already visible, don't do anything (but allow if called externally to ensure it's shown)
        if (isPanelVisible && endPanel != null && endPanel.activeSelf)
        {
            // #region agent log
            System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log",
                $"{{\"id\":\"show_end_panel_already_visible\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"TrialEndPanel.cs:ShowEndPanel\",\"message\":\"Panel already visible, skipping\",\"data\":{{\"isPanelVisible\":{isPanelVisible},\"endPanelActive\":{endPanel.activeSelf}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"H\"}}\n");
            // #endregion
            return; // Panel already visible
        }
        
        // Allow external calls to show panel even if hasReachedEnd is true (e.g., from ElevationProgressBar)
        // This ensures the panel is shown when progress bar detects completion
        if (hasReachedEnd && isPanelVisible)
        {
            // #region agent log
            System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log",
                $"{{\"id\":\"show_end_panel_early_return\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"TrialEndPanel.cs:ShowEndPanel\",\"message\":\"ShowEndPanel early return - already reached end and panel visible\",\"data\":{{\"hasReachedEnd\":{hasReachedEnd},\"isPanelVisible\":{isPanelVisible}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"H2\"}}\n");
            // #endregion
            return; // Prevent multiple triggers only if panel is already visible
        }

        hasReachedEnd = true;
        isPanelVisible = true;

        // Calculate actual distance travelled
        float actualDistance = 0f;
        if (bikeController != null)
        {
            actualDistance = bikeController.GetDistance() - trialStartDistance;
        }

        // Show panel
        if (endPanel != null)
        {
            endPanel.SetActive(true);
            
            // #region agent log
            System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log",
                $"{{\"id\":\"end_panel_activated\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"TrialEndPanel.cs:ShowEndPanel\",\"message\":\"End panel activated\",\"data\":{{\"endPanelActive\":{endPanel.activeSelf},\"endPanelActiveInHierarchy\":{endPanel.activeInHierarchy}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"I\"}}\n");
            // #endregion
        }
        else
        {
            // #region agent log
            System.IO.File.AppendAllText("/Users/lsna8/Documents/MRes_Clinical_Neuroscience/BreathlessVR-main/.cursor/debug.log",
                $"{{\"id\":\"end_panel_is_null\",\"timestamp\":{System.DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"TrialEndPanel.cs:ShowEndPanel\",\"message\":\"End panel is null - cannot show\",\"data\":{{}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"J\"}}\n");
            // #endregion
            Debug.LogError("[TrialEndPanel] End panel GameObject is null! Cannot show panel.");
        }

        // Update message text with trial information
        if (messageText != null)
        {
            int currentTrial = GetCurrentTrialNumber();
            messageText.text = $"That was the end of Trial {currentTrial}!\nYou completed {actualDistance:F0}m.\nClick 'Continue' to start Trial {currentTrial + 1}";
        }

        // Pause game (optional - you can remove this if you want the game to continue)
        // Time.timeScale = 0f;

        Debug.Log($"[TrialEndPanel] Trial {GetCurrentTrialNumber()} completed! Distance: {actualDistance:F1}m / {trialDistance}m");
    }

    private void OnContinueButtonClicked()
    {
        Debug.Log("[TrialEndPanel] Continue button clicked - Starting next trial");

        // Hide panel
        if (endPanel != null)
        {
            endPanel.SetActive(false);
        }

        isPanelVisible = false;
        hasReachedEnd = false;

        // Resume game if paused
        // Time.timeScale = 1f;

        // OLD SYSTEM - Commented out
        // Start next trial if trial manager exists
        // if (trialTerrainManager != null)
        // {
        //     // Get current trial number and increment
        //     int currentTrial = GetCurrentTrialNumber();
        //     int nextTrial = currentTrial + 1;
        //     
        //     Debug.Log($"[TrialEndPanel] Current trial: {currentTrial}, Starting Trial {nextTrial}");
        //     
        //     // Apply next trial terrain (this will automatically reset bike position and generate new terrain)
        //     trialTerrainManager.ApplyTerrainForTrial(nextTrial);
        //     
        //     // Reset for next trial AFTER terrain is applied (so we get the correct start distance)
        //     ResetTrial();
        //     
        //     Debug.Log($"[TrialEndPanel] ✓ Trial {nextTrial} started successfully");
        // }
        // else
        // {
        //     Debug.LogWarning("[TrialEndPanel] TrialTerrainManager not found! Cannot start next trial automatically.");
        //     // Still reset even if manager not found
        //     ResetTrial();
        // }
        
        // NEW SYSTEM - Use ExperimentalTrialManager instead
        ResetTrial();
        Debug.Log("[TrialEndPanel] Trial completed. Use ExperimentalTrialManager to start next trial.");
    }

    private void ResetTrial()
    {
        // Reset trial start distance
        if (bikeController != null)
        {
            trialStartDistance = bikeController.GetDistance();
        }

        // Reset bike data UI progress
        if (bikeDataUI != null)
        {
            bikeDataUI.ResetCourseProgress();
        }

        hasReachedEnd = false;
        isPanelVisible = false;

        Debug.Log($"[TrialEndPanel] Trial reset. Start distance: {trialStartDistance}m");
    }

    private int GetCurrentTrialNumber()
    {
        // OLD SYSTEM - Commented out
        // Get current trial number from trial manager
        // if (trialTerrainManager != null)
        // {
        //     return trialTerrainManager.GetCurrentTrialNumber();
        // }
        
        // NEW SYSTEM - Get from ExperimentalTrialManager
        ExperimentalTrialManager trialManager = FindObjectOfType<ExperimentalTrialManager>();
        if (trialManager != null)
        {
            return trialManager.GetCurrentTrialIndex() + 1; // +1 because index is 0-based
        }
        
        // Default to 1 if we can't determine
        return 1;
    }

    /// <summary>
    /// Manually reset the trial (call when starting a new trial)
    /// </summary>
    public void ResetTrialManual()
    {
        ResetTrial();
    }

    /// <summary>
    /// Set trial distance
    /// </summary>
    public void SetTrialDistance(float distance)
    {
        trialDistance = distance;
    }

    [ContextMenu("Test: Show End Panel")]
    public void TestShowEndPanel()
    {
        ShowEndPanel();
    }
}
