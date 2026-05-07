using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Display current trial information on screen
/// Shows trial number, target gradient, and instructions
/// </summary>
public class TrialInfoDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExperimentalTrialManager trialManager;
    
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI trialNumberText;
    [SerializeField] private TextMeshProUGUI gradientText;
    [SerializeField] private TextMeshProUGUI instructionsText;
    [SerializeField] private GameObject trialInfoPanel;
    [SerializeField] private Image gradientColorIndicator;
    
    [Header("Settings")]
    [SerializeField] private bool showInstructions = true;
    [SerializeField] private float updateInterval = 0.5f;
    
    private float lastUpdateTime = 0f;

    private void Start()
    {
        if (trialManager == null)
            trialManager = FindObjectOfType<ExperimentalTrialManager>();
        
        UpdateDisplay();
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDisplay();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateDisplay()
    {
        if (trialManager == null) return;
        
        string trialInfo = trialManager.GetCurrentTrialInfo();
        
        if (trialNumberText != null)
        {
            trialNumberText.text = trialInfo;
        }
        
        // Update instructions
        if (instructionsText != null && showInstructions)
        {
            instructionsText.text = GetInstructions();
        }
    }

    private string GetInstructions()
    {
        return "G: Generate Trials | N: Next Trial | E: End Trial";
    }

    private Color GetGradientColor(float gradient)
    {
        if (gradient <= 0f)
            return new Color(0.3f, 0.8f, 0.3f); // Green - flat
        else if (gradient <= 3f)
            return new Color(1f, 0.9f, 0.2f); // Yellow - easy
        else if (gradient <= 7f)
            return new Color(1f, 0.6f, 0.1f); // Orange - moderate
        else
            return new Color(1f, 0.2f, 0.1f); // Red - hard
    }
}
