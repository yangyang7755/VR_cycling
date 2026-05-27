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

        // Pause game time
        Time.timeScale = 0f;
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

        // Enable game components
        EnableGameComponents();

        // Resume game time
        Time.timeScale = 1f;
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

        // Hide start screen
        HideStartScreen();

        // Run baseline then start experiment
        StartCoroutine(BaselineThenExperiment());
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

    // =========================================================================
    // BASELINE EEG RECORDING PHASE
    // =========================================================================

    private System.Collections.IEnumerator BaselineThenExperiment()
    {
        if (!skipBaseline)
        {
            // Create baseline overlay (black screen with text)
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
                float remaining = baselineDurationSeconds - elapsed;
                int minutes = Mathf.FloorToInt(remaining / 60f);
                int seconds = Mathf.FloorToInt(remaining % 60f);
                baselineText.text = $"Baseline Recording\n\nPlease sit still and relax\n\n{minutes:D2}:{seconds:D2}";
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
    }

    private void CreateBaselinePanel()
    {
        // Full-screen black panel
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("BaselineCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        baselinePanel = new GameObject("BaselinePanel");
        baselinePanel.transform.SetParent(canvas.transform, false);
        RectTransform rt = baselinePanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Black background
        Image bg = baselinePanel.AddComponent<Image>();
        bg.color = Color.black;

        // Text
        GameObject textObj = new GameObject("BaselineText");
        textObj.transform.SetParent(baselinePanel.transform, false);
        baselineText = textObj.AddComponent<TextMeshProUGUI>();
        baselineText.text = "Baseline Recording\n\nPlease sit still and relax\n\n05:00";
        baselineText.fontSize = 36;
        baselineText.color = Color.white;
        baselineText.alignment = TextAlignmentOptions.Center;
        RectTransform trt = textObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }
}
