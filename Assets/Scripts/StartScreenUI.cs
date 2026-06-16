using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

/// <summary>
/// Start screen UI that prompts for Participant ID, Date, and Trial Number
/// </summary>
public class StartScreenUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Main panel for start screen")]
    [SerializeField] private GameObject startPanel;
    
    [Tooltip("Bike Data Panel (optional - will be shown after start)")]
    [SerializeField] private GameObject bikeDataPanel;

    [Header("Game Components to Disable")]
    [Tooltip("Bike Controller - will be disabled until trial starts")]
    [SerializeField] private BikeController bikeController;
    
    [Tooltip("Trainer Adapter - will be disabled until trial starts")]
    [SerializeField] private TacxTrainerAdapter trainerAdapter;
    
    [Tooltip("Cyclist Camera - will be disabled until trial starts")]
    [SerializeField] private MonoBehaviour cyclistCameraScript;
    
    [Tooltip("Input field for Participant ID")]
    [SerializeField] private TMP_InputField participantIDInput;
    
    [Tooltip("Input field for Date")]
    [SerializeField] private TMP_InputField dateInput;
    
    [Tooltip("Input field for Trial Number")]
    [SerializeField] private TMP_InputField trialNumberInput;
    
    [Tooltip("Button to start the game")]
    [SerializeField] private Button startButton;
    
    [Tooltip("Error message text (optional)")]
    [SerializeField] private TextMeshProUGUI errorText;

    [Header("Settings")]
    [Tooltip("Auto-fill date with today's date")]
    [SerializeField] private bool autoFillDate = true;
    
    [Tooltip("Auto-increment trial number")]
    [SerializeField] private bool autoIncrementTrial = true;

    [Header("Trial Manager")]
    [Tooltip("HillTrialManager to apply trial configuration based on trial number")]
    [SerializeField] private HillTrialManager hillTrialManager;

    [Header("EEG Baseline")]
    [Tooltip("Duration of baseline recording in seconds (300 = 5 minutes)")]
    [SerializeField] private float baselineDurationSeconds = 300f;
    
    [Tooltip("Skip baseline (for testing)")]
    [SerializeField] private bool skipBaseline = false;

    [Header("Mode")]
    [Tooltip("Simulation mode — no bike needed, uses keyboard power")]
    [SerializeField] private bool simulationMode = false;

    private ParticipantData currentData;
    private int lastTrialNumber = 1;
    private GameObject baselinePanel;
    private TextMeshProUGUI baselineText;

    public static ParticipantData CurrentParticipantData { get; private set; }

    private void Start()
    {
        // Disable game components BEFORE showing start screen
        DisableGameComponents();

        // Auto-find HillTrialManager if not assigned
        if (hillTrialManager == null)
        {
            hillTrialManager = FindObjectOfType<HillTrialManager>();
            if (hillTrialManager == null)
            {
                Debug.LogWarning("[StartScreenUI] HillTrialManager not found. Trial configurations will not be applied automatically.");
            }
        }

        // Initialize
        currentData = new ParticipantData();
        
        // Auto-fill date if enabled
        if (autoFillDate && dateInput != null)
        {
            dateInput.text = DateTime.Now.ToString("yyyy-MM-dd");
            currentData.date = dateInput.text;
        }

        // Set up start button
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        // Set up input field listeners
        if (participantIDInput != null)
        {
            participantIDInput.onValueChanged.AddListener(OnParticipantIDChanged);
        }

        if (dateInput != null)
        {
            dateInput.onValueChanged.AddListener(OnDateChanged);
        }

        if (trialNumberInput != null)
        {
            trialNumberInput.onValueChanged.AddListener(OnTrialNumberChanged);
            
            // Auto-increment trial number
            if (autoIncrementTrial)
            {
                trialNumberInput.text = lastTrialNumber.ToString();
                currentData.trialNumber = lastTrialNumber;
            }
        }

        // Show start screen
        ShowStartScreen();
    }

    private void DisableGameComponents()
    {
        // Disable BikeController
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }
        if (bikeController != null)
        {
            bikeController.enabled = false;
            Debug.Log("[StartScreenUI] BikeController disabled");
        }

        // Disable TrainerAdapter
        if (trainerAdapter == null)
        {
            trainerAdapter = FindObjectOfType<TacxTrainerAdapter>();
        }
        if (trainerAdapter != null)
        {
            trainerAdapter.enabled = false;
            Debug.Log("[StartScreenUI] TacxTrainerAdapter disabled");
        }

        // Disable CyclistCamera
        if (cyclistCameraScript == null)
        {
            cyclistCameraScript = FindObjectOfType<CyclistCamera>() as MonoBehaviour;
        }
        if (cyclistCameraScript != null)
        {
            cyclistCameraScript.enabled = false;
            Debug.Log("[StartScreenUI] CyclistCamera disabled");
        }
    }

    private void EnableGameComponents()
    {
        // Enable BikeController
        if (bikeController != null)
        {
            bikeController.enabled = true;
            Debug.Log("[StartScreenUI] BikeController enabled");
        }

        // Enable TrainerAdapter
        if (trainerAdapter != null)
        {
            trainerAdapter.enabled = true;
            Debug.Log("[StartScreenUI] TacxTrainerAdapter enabled");
            
            // Connect to trainer after enabling (if it has auto-connect)
            // Note: TacxTrainerAdapter's autoConnectOnStart is now false by default
            // You can manually connect via Inspector or add a ConnectToTrainer call here if needed
        }

        // Enable CyclistCamera
        if (cyclistCameraScript != null)
        {
            cyclistCameraScript.enabled = true;
            Debug.Log("[StartScreenUI] CyclistCamera enabled");
        }
    }

    private void ShowStartScreen()
    {
        if (startPanel != null)
        {
            startPanel.SetActive(true);
        }

        // Hide bike data panel while start screen is showing
        if (bikeDataPanel != null)
        {
            bikeDataPanel.SetActive(false);
        }

        // Hide all HUD elements — retry for a few frames since some are created late
        HideAllHUD();
        StartCoroutine(DelayedHideHUD());

        // Pause game time
        Time.timeScale = 0f;
    }

    private System.Collections.IEnumerator DelayedHideHUD()
    {
        // Keep hiding for several real-time frames (works even when timeScale=0)
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            if (startPanel != null && startPanel.activeSelf)
                HideAllHUD();
        }
    }

    private void HideStartScreen()
    {
        if (startPanel != null)
        {
            startPanel.SetActive(false);
        }

        // Show bike data panel after start screen is hidden
        if (bikeDataPanel != null)
        {
            bikeDataPanel.SetActive(true);
        }

        // Show HUD elements again
        ShowAllHUD();

        // Enable game components
        EnableGameComponents();

        // Resume game time
        Time.timeScale = 1f;
    }

    private void HideAllHUD()
    {
        // Hide cycling HUD (speed, power, cadence display) — all variants
        var cyclingHUD = FindObjectOfType<ModernCyclingUI>();
        if (cyclingHUD != null) cyclingHUD.gameObject.SetActive(false);

        var bikeDataUI = FindObjectOfType<BikeDataUI>();
        if (bikeDataUI != null) bikeDataUI.gameObject.SetActive(false);

        var cyclingHUD2 = FindObjectOfType<CyclingHUD>();
        if (cyclingHUD2 != null) cyclingHUD2.gameObject.SetActive(false);

        var bikeDataDisplay = FindObjectOfType<BikeDataDisplay>();
        if (bikeDataDisplay != null) bikeDataDisplay.gameObject.SetActive(false);

        var enhancedUI = FindObjectOfType<EnhancedBikeDataUI>();
        if (enhancedUI != null) enhancedUI.gameObject.SetActive(false);

        var simpleDisplay = FindObjectOfType<SimpleDataDisplay>();
        if (simpleDisplay != null) simpleDisplay.gameObject.SetActive(false);

        // Hide progress bars
        var elevationBar = FindObjectOfType<ElevationProgressBar>();
        if (elevationBar != null) elevationBar.gameObject.SetActive(false);

        var elevationProfile = FindObjectOfType<ElevationProfileBar>();
        if (elevationProfile != null) elevationProfile.gameObject.SetActive(false);

        var distanceBar = FindObjectOfType<DistanceProgressBar>();
        if (distanceBar != null) distanceBar.gameObject.SetActive(false);

        var continuousProgress = FindObjectOfType<ContinuousProgressUI>();
        if (continuousProgress != null) continuousProgress.gameObject.SetActive(false);

        // Hide gradient indicator
        var gradientIndicator = FindObjectOfType<GradientIndicator>();
        if (gradientIndicator != null) gradientIndicator.gameObject.SetActive(false);

        // Hide persistent pain VAS
        var painVAS = FindObjectOfType<PersistentPainVAS>();
        if (painVAS != null) painVAS.gameObject.SetActive(false);

        // Hide coin counter and HillClimbExperiment GUI
        // (find any canvas with "Coin" or "HUD" in name)
        foreach (var canvas in FindObjectsOfType<Canvas>())
        {
            string name = canvas.gameObject.name.ToLower();
            if (name.Contains("hud") || name.Contains("cycling") || name.Contains("coin"))
            {
                canvas.gameObject.SetActive(false);
            }
        }
    }

    private void ShowAllHUD()
    {
        var cyclingHUD = FindObjectOfType<ModernCyclingUI>(true);
        if (cyclingHUD != null) cyclingHUD.gameObject.SetActive(true);

        var bikeDataUI = FindObjectOfType<BikeDataUI>(true);
        if (bikeDataUI != null) bikeDataUI.gameObject.SetActive(true);

        var cyclingHUD2 = FindObjectOfType<CyclingHUD>(true);
        if (cyclingHUD2 != null) cyclingHUD2.gameObject.SetActive(true);

        var bikeDataDisplay = FindObjectOfType<BikeDataDisplay>(true);
        if (bikeDataDisplay != null) bikeDataDisplay.gameObject.SetActive(true);

        var enhancedUI = FindObjectOfType<EnhancedBikeDataUI>(true);
        if (enhancedUI != null) enhancedUI.gameObject.SetActive(true);

        var simpleDisplay = FindObjectOfType<SimpleDataDisplay>(true);
        if (simpleDisplay != null) simpleDisplay.gameObject.SetActive(true);

        var elevationBar = FindObjectOfType<ElevationProgressBar>(true);
        if (elevationBar != null) elevationBar.gameObject.SetActive(true);

        var elevationProfile = FindObjectOfType<ElevationProfileBar>(true);
        if (elevationProfile != null) elevationProfile.gameObject.SetActive(true);

        var distanceBar = FindObjectOfType<DistanceProgressBar>(true);
        if (distanceBar != null) distanceBar.gameObject.SetActive(true);

        var continuousProgress = FindObjectOfType<ContinuousProgressUI>(true);
        if (continuousProgress != null) continuousProgress.gameObject.SetActive(true);

        var gradientIndicator = FindObjectOfType<GradientIndicator>(true);
        if (gradientIndicator != null) gradientIndicator.gameObject.SetActive(true);

        var painVAS = FindObjectOfType<PersistentPainVAS>(true);
        if (painVAS != null) painVAS.gameObject.SetActive(true);

        // Re-show any hidden canvases
        foreach (var canvas in FindObjectsOfType<Canvas>(true))
        {
            string name = canvas.gameObject.name.ToLower();
            if (name.Contains("hud") || name.Contains("cycling") || name.Contains("coin"))
            {
                canvas.gameObject.SetActive(true);
            }
        }
    }

    private void OnParticipantIDChanged(string value)
    {
        if (currentData != null)
        {
            currentData.participantID = value;
        }
    }

    private void OnDateChanged(string value)
    {
        if (currentData != null)
        {
            currentData.date = value;
        }
    }

    private void OnTrialNumberChanged(string value)
    {
        if (currentData != null && int.TryParse(value, out int trialNum))
        {
            currentData.trialNumber = trialNum;
            lastTrialNumber = trialNum;
        }
    }

    private void OnStartButtonClicked()
    {
        // Validate input
        if (!ValidateInput())
        {
            return;
        }

        // Store data
        CurrentParticipantData = currentData;
        lastTrialNumber = currentData.trialNumber;

        // Auto-increment for next time
        if (autoIncrementTrial && trialNumberInput != null)
        {
            lastTrialNumber++;
        }

        // Log data
        Debug.Log($"[StartScreenUI] Starting trial with data: {currentData}");

        // Apply trial configuration based on trial number
        ApplyTrialConfiguration(currentData.trialNumber);

        // Run baseline then start experiment
        // StartScreenUI is on the panel itself, so we need a persistent runner
        GameObject runner = new GameObject("ExperimentRunner");
        DontDestroyOnLoad(runner);
        CoroutineRunner cr = runner.AddComponent<CoroutineRunner>();
        cr.StartCoroutine(BaselineThenExperiment(runner));
    }

    private bool ValidateInput()
    {
        // Check Participant ID
        if (string.IsNullOrWhiteSpace(currentData.participantID))
        {
            ShowError("Please enter Participant ID");
            return false;
        }

        // Check Date
        if (string.IsNullOrWhiteSpace(currentData.date))
        {
            ShowError("Please enter Date");
            return false;
        }

        // Check Trial Number
        if (currentData.trialNumber <= 0)
        {
            ShowError("Trial Number must be greater than 0");
            return false;
        }

        // Clear error if validation passes
        ClearError();
        return true;
    }

    private void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.color = Color.red;
        }
        else
        {
            Debug.LogWarning($"[StartScreenUI] {message}");
        }
    }

    private void ClearError()
    {
        if (errorText != null)
        {
            errorText.text = "";
        }
    }

    // Event for other scripts to listen to
    public static event System.Action<ParticipantData> OnTrialStarted;

    // Public method to get current data
    public static ParticipantData GetCurrentData()
    {
        return CurrentParticipantData;
    }

    /// <summary>
    /// Apply trial configuration based on trial number (1-5)
    /// Trial 1: Flat (0%)
    /// Trial 2: 2.5% gradient
    /// Trial 3: 3.6% gradient
    /// Trial 4: 5.8% gradient
    /// Trial 5: 10% gradient
    /// </summary>
    private void ApplyTrialConfiguration(int trialNumber)
    {
        if (hillTrialManager == null)
        {
            Debug.LogWarning("[StartScreenUI] HillTrialManager not found. Cannot apply trial configuration.");
            return;
        }

        // Convert trial number to index (1-based to 0-based)
        int trialIndex = trialNumber - 1;

        // Validate trial index
        if (trialIndex < 0)
        {
            Debug.LogWarning($"[StartScreenUI] Invalid trial number: {trialNumber}. Using trial 1 instead.");
            trialIndex = 0;
        }

        // Select and apply trial
        hillTrialManager.SelectTrial(trialIndex);
        hillTrialManager.ApplyCurrentTrial();

        Debug.Log($"[StartScreenUI] Applied trial {trialNumber} (index {trialIndex})");
    }

    [ContextMenu("Show Start Screen")]
    public void ShowStartScreenManual()
    {
        ShowStartScreen();
    }

    public void SetSimulationMode(bool enabled)
    {
        simulationMode = enabled;
        if (enabled) skipBaseline = true; // Also skip baseline in sim mode
    }

    // Show simulation/live mode toggle on start screen
    private void OnGUI()
    {
        if (startPanel == null || !startPanel.activeSelf) return;

        // Mode toggle button in bottom-left corner
        float bw = 200f, bh = 40f;
        float x = 10f;
        float y = Screen.height - bh - 10f;

        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;

        string modeLabel = simulationMode ? "MODE: SIMULATION" : "MODE: LIVE (Bike)";
        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = simulationMode ? Color.yellow : Color.green;

        if (GUI.Button(new Rect(x, y, bw, bh), modeLabel, style))
        {
            simulationMode = !simulationMode;
        }

        GUI.backgroundColor = oldColor;

        // Show info below
        GUIStyle info = new GUIStyle(GUI.skin.label);
        info.fontSize = 11;
        info.normal.textColor = Color.gray;
        string infoText = simulationMode 
            ? "No bike needed. Use Up/Down arrows for power." 
            : "Requires Python BLE bridge + bike connected.";
        GUI.Label(new Rect(x, y - 20f, 400f, 20f), infoText, info);
    }

    // =========================================================================
    // BASELINE EEG RECORDING PHASE
    // =========================================================================

    private System.Collections.IEnumerator BaselineThenExperiment(GameObject runner)
    {
        // Hide start screen now (safe because coroutine runs on a different object)
        HideStartScreen();

        if (!skipBaseline && !simulationMode)
        {
            // Hide ALL HUD elements during baseline
            HideAllHUD();

            // Create baseline overlay (black screen with fixation cross)
            CreateBaselinePanel();
            baselinePanel.SetActive(true);

            // Send EEG marker: BASELINE_START
            if (EventMarkerSender.Instance != null)
                EventMarkerSender.Instance.SendEvent("BASELINE_START", $"participant={currentData.participantID},duration={baselineDurationSeconds}");
            Debug.Log($"[EEG] BASELINE_START — {baselineDurationSeconds}s");

            // Countdown
            float elapsed = 0f;
            while (elapsed < baselineDurationSeconds)
            {
                // Press Escape to skip baseline
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Debug.Log("[EEG] Baseline SKIPPED by user (Escape key)");
                    break;
                }

                float remaining = baselineDurationSeconds - elapsed;
                int minutes = Mathf.FloorToInt(remaining / 60f);
                int seconds = Mathf.FloorToInt(remaining % 60f);
                baselineText.text = $"+\n\n\n\nBaseline Recording\nPlease sit still and relax\n\n{minutes:D2}:{seconds:D2}\n\n<size=14><color=#666>(Press Escape to skip)</color></size>";
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Send EEG marker: BASELINE_END
            if (EventMarkerSender.Instance != null)
                EventMarkerSender.Instance.SendEvent("BASELINE_END", $"duration={baselineDurationSeconds}");
            Debug.Log("[EEG] BASELINE_END");

            // Remove baseline panel
            if (baselinePanel != null)
            {
                Destroy(baselinePanel);
                baselinePanel = null;
            }
        }

        // Now start the experiment
        // Apply simulation mode to BikeController
        BikeController bike = FindObjectOfType<BikeController>();
        if (bike != null)
        {
            bike.SetSimulationMode(simulationMode);
            Debug.Log($"[StartScreenUI] BikeController simulation mode: {simulationMode}");
        }

        HillClimbExperiment hillExp = FindObjectOfType<HillClimbExperiment>();
        if (hillExp != null)
        {
            hillExp.StartBlock(currentData.participantID, currentData.trialNumber, HillClimbExperiment.ExperimentGroup.Control);
            Debug.Log($"[StartScreenUI] Started HillClimbExperiment: {currentData.participantID}, Block {currentData.trialNumber}");
        }
        else
        {
            ExperimentController expController = FindObjectOfType<ExperimentController>();
            if (expController != null)
            {
                expController.SetParticipantID(currentData.participantID);
                expController.SetBlockNumber(currentData.trialNumber);
                expController.StartExperiment();
                Debug.Log($"[StartScreenUI] Started ExperimentController: {currentData.participantID}, Block {currentData.trialNumber}");
            }
        }

        // Trigger event
        OnTrialStarted?.Invoke(currentData);

        // Cleanup runner
        if (runner != null) Destroy(runner);
    }

    // Simple helper class to run coroutines on a persistent GameObject
    private class CoroutineRunner : MonoBehaviour { }

    private void CreateBaselinePanel()
    {
        // Always create a dedicated canvas with highest sort order
        GameObject canvasObj = new GameObject("BaselineCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Above everything
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        baselinePanel = canvasObj; // The canvas itself IS the panel

        // Black background (full screen)
        GameObject bgObj = new GameObject("BlackBG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bg = bgObj.AddComponent<Image>();
        bg.color = Color.black;

        // Text (centered)
        GameObject textObj = new GameObject("BaselineText");
        textObj.transform.SetParent(canvasObj.transform, false);
        baselineText = textObj.AddComponent<TextMeshProUGUI>();
        baselineText.text = "+\n\n\n\nBaseline Recording\nPlease sit still and relax\n\n05:00";
        baselineText.fontSize = 42;
        baselineText.color = Color.white;
        baselineText.alignment = TextAlignmentOptions.Center;
        RectTransform trt = textObj.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.1f, 0.2f);
        trt.anchorMax = new Vector2(0.9f, 0.8f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }
}
