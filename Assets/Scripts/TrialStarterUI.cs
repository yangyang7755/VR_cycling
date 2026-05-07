using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

/// <summary>
/// Trial starter UI - collects participant ID and trial number before starting
/// Integrates with data logger and experimental trial manager
/// </summary>
public class TrialStarterUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private TMP_InputField participantIDInput;
    [SerializeField] private TMP_InputField trialNumberInput;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button cancelButton;
    
    [Header("Group Selection")]
    [Tooltip("Group 1 = Control (no coins), Group 2 = Reward (coins visible)")]
    [SerializeField] private Toggle groupControlToggle;
    [SerializeField] private Toggle groupRewardToggle;
    private HillClimbExperiment.ExperimentGroup selectedGroup = HillClimbExperiment.ExperimentGroup.Control;
    
    [Header("References")]
    [SerializeField] private UnityDataLogger dataLogger;
    [SerializeField] private ExperimentalTrialManager trialManager;
    [SerializeField] private ProceduralRouteGenerator routeGenerator;
    
    [Header("Settings")]
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private string defaultParticipantID = "P001";
    [SerializeField] private int defaultTrialNumber = 1;
    
    private void Start()
    {
        // Auto-find references
        if (dataLogger == null)
            dataLogger = FindObjectOfType<UnityDataLogger>();
        
        if (trialManager == null)
            trialManager = FindObjectOfType<ExperimentalTrialManager>();
        
        if (routeGenerator == null)
            routeGenerator = FindObjectOfType<ProceduralRouteGenerator>();
        
        // Setup UI
        if (startButton != null)
            startButton.onClick.AddListener(OnStartTrial);
        
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
        
        // Wire up group toggles if assigned
        if (groupControlToggle != null)
            groupControlToggle.onValueChanged.AddListener(v => { if (v) selectedGroup = HillClimbExperiment.ExperimentGroup.Control; });
        if (groupRewardToggle != null)
            groupRewardToggle.onValueChanged.AddListener(v => { if (v) selectedGroup = HillClimbExperiment.ExperimentGroup.Reward; });
        
        // Set defaults
        if (participantIDInput != null)
            participantIDInput.text = defaultParticipantID;
        
        if (trialNumberInput != null)
            trialNumberInput.text = defaultTrialNumber.ToString();
        
        // Update date
        if (dateText != null)
            dateText.text = DateTime.Now.ToString("yyyy-MM-dd");
        
        // Show panel if enabled — but defer to BluetoothConnectionUI if present
        BluetoothConnectionUI btUI = FindObjectOfType<BluetoothConnectionUI>();
        if (btUI != null)
        {
            // BluetoothConnectionUI will call ShowStartPanel() after devices are confirmed
            if (startPanel != null)
                startPanel.SetActive(false);
        }
        else if (showOnStart && startPanel != null)
        {
            ShowStartPanel();
        }
        else if (startPanel != null)
        {
            startPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // Keyboard shortcut to show panel
        if (Input.GetKeyDown(KeyCode.T) && !startPanel.activeSelf)
        {
            ShowStartPanel();
        }
    }

    /// <summary>
    /// Show the start panel
    /// </summary>
    public void ShowStartPanel()
    {
        if (startPanel != null)
        {
            startPanel.SetActive(true);
            
            // Focus on participant ID input
            if (participantIDInput != null)
            {
                participantIDInput.Select();
                participantIDInput.ActivateInputField();
            }
        }
    }

    /// <summary>
    /// Hide the start panel
    /// </summary>
    public void HideStartPanel()
    {
        if (startPanel != null)
        {
            startPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Start trial button clicked
    /// </summary>
    private void OnStartTrial()
    {
        string participantID = participantIDInput != null ? participantIDInput.text : defaultParticipantID;
        int trialNumber = defaultTrialNumber;

        if (trialNumberInput != null && int.TryParse(trialNumberInput.text, out int parsed))
        {
            trialNumber = parsed;
        }

        if (string.IsNullOrWhiteSpace(participantID))
        {
            Debug.LogWarning("[TrialStarter] Participant ID cannot be empty!");
            return;
        }

        // Set trial info in data logger
        if (dataLogger != null)
        {
            dataLogger.SetTrialInfo(participantID, trialNumber);
        }

        // Always use ExperimentController (the main experiment system)
        ExperimentController expController = FindObjectOfType<ExperimentController>();
        if (expController != null)
        {
            expController.SetParticipantID(participantID);
            expController.SetBlockNumber(trialNumber);
            expController.StartExperiment();
            Debug.Log($"[TrialStarter] Started ExperimentController: {participantID}, Block {trialNumber}");
        }
        else
        {
            // Fallback to HillClimbExperiment if no ExperimentController
            HillClimbExperiment hillExp = FindObjectOfType<HillClimbExperiment>();
            if (hillExp != null)
            {
                Debug.Log($"\n[HillExperiment] Starting Block {trialNumber} for {participantID} (Group: {selectedGroup})");
                hillExp.StartBlock(participantID, trialNumber, selectedGroup);
            }
            else
            {
                Debug.LogWarning("[TrialStarter] No experiment controller found!");
            }
        }

        HideStartPanel();

        if (dataLogger != null && !dataLogger.IsLogging())
        {
            dataLogger.StartLogging();
        }
    }

    /// <summary>
    /// Cancel button clicked
    /// </summary>
    private void OnCancel()
    {
        HideStartPanel();
    }

    /// <summary>
    /// Set participant ID programmatically
    /// </summary>
    public void SetParticipantID(string id)
    {
        if (participantIDInput != null)
        {
            participantIDInput.text = id;
        }
    }

    /// <summary>
    /// Set trial number programmatically
    /// </summary>
    public void SetTrialNumber(int number)
    {
        if (trialNumberInput != null)
        {
            trialNumberInput.text = number.ToString();
        }
    }
    
    // Fallback GUI group selector (shown when start panel is active and no toggles assigned)
    private void OnGUI()
    {
        if (startPanel == null || !startPanel.activeSelf) return;
        if (groupControlToggle != null || groupRewardToggle != null) return; // toggles handle it
        
        // Draw group selector in center-bottom of screen
        float w = 300f, h = 80f;
        float x = Screen.width * 0.5f - w * 0.5f;
        float y = Screen.height * 0.5f + 60f;
        
        GUI.Box(new Rect(x, y, w, h), "");
        GUI.Label(new Rect(x + 10, y + 5, w - 20, 20), "Group Assignment:");
        
        GUIStyle selected = new GUIStyle(GUI.skin.button);
        selected.fontStyle = FontStyle.Bold;
        selected.normal.textColor = Color.yellow;
        
        bool isControl = selectedGroup == HillClimbExperiment.ExperimentGroup.Control;
        
        if (GUI.Button(new Rect(x + 10, y + 30, 130, 35), 
            isControl ? "✓ Group 1: Control" : "Group 1: Control",
            isControl ? selected : GUI.skin.button))
        {
            selectedGroup = HillClimbExperiment.ExperimentGroup.Control;
        }
        
        if (GUI.Button(new Rect(x + 155, y + 30, 130, 35),
            !isControl ? "✓ Group 2: Reward" : "Group 2: Reward",
            !isControl ? selected : GUI.skin.button))
        {
            selectedGroup = HillClimbExperiment.ExperimentGroup.Reward;
        }
    }
}
