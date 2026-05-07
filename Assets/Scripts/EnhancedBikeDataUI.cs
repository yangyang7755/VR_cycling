using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Enhanced bike data UI with climb progress bar and improved visuals
/// Displays: Speed, Power, Gradient, and Climb Progress
/// </summary>
public class EnhancedBikeDataUI : MonoBehaviour
{
    [Header("Bike Controller")]
    [Tooltip("Bike controller to get data from")]
    [SerializeField] private BikeController bikeController;
    
    // OLD SYSTEM - Commented out
    // [Tooltip("Trial Terrain Manager to get current gradient")]
    // [SerializeField] private TrialTerrainManager trialTerrainManager;

    [Header("UI Panel Background")]
    [Tooltip("Background panel for the data display")]
    [SerializeField] private Image backgroundPanel;
    
    [Tooltip("Background color")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.7f); // Semi-transparent black

    [Header("Speed Display")]
    [Tooltip("Text to display speed")]
    [SerializeField] private TextMeshProUGUI speedText;
    
    [Tooltip("Speed label text")]
    [SerializeField] private TextMeshProUGUI speedLabel;

    [Header("Power Display")]
    [Tooltip("Text to display power")]
    [SerializeField] private TextMeshProUGUI powerText;
    
    [Tooltip("Power label text")]
    [SerializeField] private TextMeshProUGUI powerLabel;
    
    [Tooltip("Power progress bar (optional)")]
    [SerializeField] private Slider powerProgressBar;
    
    [Tooltip("Power progress bar fill image (for color coding)")]
    [SerializeField] private Image powerProgressFill;
    
    [Tooltip("Maximum power for progress bar")]
    [SerializeField] private float maxPower = 500f;

    [Header("Gradient Display")]
    [Tooltip("Text to display current gradient")]
    [SerializeField] private TextMeshProUGUI gradientText;
    
    [Tooltip("Gradient label text")]
    [SerializeField] private TextMeshProUGUI gradientLabel;

    [Header("Climb Progress Bar")]
    [Tooltip("Climb progress bar (horizontal bar showing climb progress)")]
    [SerializeField] private Slider climbProgressBar;
    
    [Tooltip("Climb progress bar background")]
    [SerializeField] private Image climbProgressBackground;
    
    [Tooltip("Climb progress bar fill")]
    [SerializeField] private Image climbProgressFill;
    
    [Tooltip("Current position marker on climb bar")]
    [SerializeField] private RectTransform climbPositionMarker;
    
    [Tooltip("Text showing current gradient on climb bar")]
    [SerializeField] private TextMeshProUGUI climbGradientText;
    
    [Tooltip("Text showing climb progress percentage")]
    [SerializeField] private TextMeshProUGUI climbProgressText;

    [Header("Climb Settings")]
    [Tooltip("Total climb distance (meters) - 0 = use terrain length")]
    [SerializeField] private float totalClimbDistance = 0f;
    
    [Tooltip("Show climb progress as percentage or distance")]
    [SerializeField] private bool showClimbAsPercentage = true;

    [Header("Visual Settings")]
    [Tooltip("Update frequency (times per second)")]
    [SerializeField] private float updateRate = 10f;
    
    [Tooltip("Font size for values")]
    [SerializeField] private float valueFontSize = 24f;
    
    [Tooltip("Font size for labels")]
    [SerializeField] private float labelFontSize = 14f;
    
    [Tooltip("Spacing between elements")]
    [SerializeField] private float elementSpacing = 10f;

    [Header("Color Coding")]
    [Tooltip("Power colors based on intensity")]
    [SerializeField] private Gradient powerColorGradient;
    
    [Tooltip("Climb bar colors based on gradient")]
    [SerializeField] private Gradient climbColorGradient;

    private float updateInterval;
    private float lastUpdateTime;
    private float currentDistance = 0f;
    private float climbStartDistance = 0f;

    private void Start()
    {
        // Auto-find components
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        // OLD SYSTEM - Commented out
        // if (trialTerrainManager == null)
        // {
        //     trialTerrainManager = FindObjectOfType<TrialTerrainManager>();
        // }

        // Setup background panel
        SetupBackgroundPanel();

        // Setup UI elements
        SetupUIElements();

        // Initialize color gradients if not set
        InitializeColorGradients();

        // Get terrain length for climb distance
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain != null && totalClimbDistance <= 0f)
        {
            totalClimbDistance = terrain.terrainData.size.x; // Use terrain length
        }

        updateInterval = 1f / updateRate;
        lastUpdateTime = Time.time;
    }

    private void SetupBackgroundPanel()
    {
        if (backgroundPanel == null)
        {
            // Try to find or create background
            backgroundPanel = GetComponent<Image>();
            if (backgroundPanel == null)
            {
                GameObject bgObj = new GameObject("Background");
                bgObj.transform.SetParent(transform, false);
                backgroundPanel = bgObj.AddComponent<Image>();
                
                RectTransform bgRect = backgroundPanel.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;
                bgRect.anchoredPosition = Vector2.zero;
            }
        }

        if (backgroundPanel != null)
        {
            backgroundPanel.color = backgroundColor;
        }
    }

    private void SetupUIElements()
    {
        // Fix UI position to top-left
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(20, -20);
        }

        // Setup font sizes
        if (speedText != null) speedText.fontSize = valueFontSize;
        if (powerText != null) powerText.fontSize = valueFontSize;
        if (gradientText != null) gradientText.fontSize = valueFontSize;
        if (speedLabel != null) speedLabel.fontSize = labelFontSize;
        if (powerLabel != null) powerLabel.fontSize = labelFontSize;
        if (gradientLabel != null) gradientLabel.fontSize = labelFontSize;
    }

    private void InitializeColorGradients()
    {
        // Initialize power color gradient (green to yellow to red)
        if (powerColorGradient == null || powerColorGradient.colorKeys.Length == 0)
        {
            powerColorGradient = new Gradient();
            powerColorGradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.green, 0f),
                    new GradientColorKey(Color.yellow, 0.5f),
                    new GradientColorKey(Color.red, 1f)
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
        }

        // Initialize climb color gradient (blue to purple to red for steep climbs)
        if (climbColorGradient == null || climbColorGradient.colorKeys.Length == 0)
        {
            climbColorGradient = new Gradient();
            climbColorGradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.blue, 0f),      // Flat
                    new GradientColorKey(Color.cyan, 0.25f),  // Slight incline
                    new GradientColorKey(Color.yellow, 0.5f),  // Moderate
                    new GradientColorKey(new Color(1f, 0.5f, 0f), 0.75f), // Orange - Steep
                    new GradientColorKey(Color.red, 1f)        // Very steep
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
        }
    }

    private void Update()
    {
        if (bikeController == null) return;

        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDisplay();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateDisplay()
    {
        // Update Speed
        UpdateSpeed();

        // Update Power
        UpdatePower();

        // Update Gradient
        UpdateGradient();

        // Update Climb Progress
        UpdateClimbProgress();
    }

    private void UpdateSpeed()
    {
        if (speedText != null)
        {
            float speedKmh = bikeController.GetSpeedKmh();
            speedText.text = $"{speedKmh:F1}";
        }

        if (speedLabel != null && speedLabel.text != "Speed (km/h)")
        {
            speedLabel.text = "Speed (km/h)";
        }
    }

    private void UpdatePower()
    {
        float power = bikeController.GetPower();

        if (powerText != null)
        {
            powerText.text = $"{power:F0}";
        }

        if (powerLabel != null && powerLabel.text != "Power (W)")
        {
            powerLabel.text = "Power (W)";
        }

        // Update power progress bar
        if (powerProgressBar != null)
        {
            float progress = Mathf.Clamp01(power / maxPower);
            powerProgressBar.value = progress;

            // Update color based on power
            if (powerProgressFill != null)
            {
                powerProgressFill.color = powerColorGradient.Evaluate(progress);
            }
        }
    }

    private void UpdateGradient()
    {
        float gradient = bikeController.GetGradient();

        if (gradientText != null)
        {
            gradientText.text = $"{gradient:F1}%";
        }

        if (gradientLabel != null && gradientLabel.text != "Gradient")
        {
            gradientLabel.text = "Gradient";
        }
    }

    private void UpdateClimbProgress()
    {
        if (climbProgressBar == null) return;

        // Get current distance
        currentDistance = bikeController.GetDistance();

        // Calculate climb progress
        float progress = 0f;
        if (totalClimbDistance > 0f)
        {
            progress = Mathf.Clamp01((currentDistance - climbStartDistance) / totalClimbDistance);
        }

        // Update climb progress bar
        climbProgressBar.value = progress;

        // Update progress text
        if (climbProgressText != null)
        {
            if (showClimbAsPercentage)
            {
                climbProgressText.text = $"{progress * 100f:F0}%";
            }
            else
            {
                float remaining = totalClimbDistance - (currentDistance - climbStartDistance);
                climbProgressText.text = $"{remaining:F0}m";
            }
        }

        // Get current gradient
        float currentGradient = bikeController.GetGradient();
        
        // Update gradient text on climb bar
        if (climbGradientText != null)
        {
            climbGradientText.text = $"{currentGradient:F1}%";
        }

        // Update climb bar color based on gradient
        if (climbProgressFill != null)
        {
            // Normalize gradient to 0-1 (assuming max 20%)
            float gradientNormalized = Mathf.Clamp01(Mathf.Abs(currentGradient) / 20f);
            climbProgressFill.color = climbColorGradient.Evaluate(gradientNormalized);
        }

        // Update position marker
        if (climbPositionMarker != null && climbProgressBar != null)
        {
            RectTransform barRect = climbProgressBar.GetComponent<RectTransform>();
            if (barRect != null)
            {
                // Position marker at current progress
                float barWidth = barRect.rect.width;
                float markerX = progress * barWidth - barWidth * 0.5f;
                climbPositionMarker.anchoredPosition = new Vector2(markerX, 0);
            }
        }
    }

    /// <summary>
    /// Reset climb progress (call when starting a new climb)
    /// </summary>
    public void ResetClimbProgress()
    {
        climbStartDistance = bikeController != null ? bikeController.GetDistance() : 0f;
        currentDistance = climbStartDistance;
    }

    /// <summary>
    /// Set total climb distance
    /// </summary>
    public void SetTotalClimbDistance(float distance)
    {
        totalClimbDistance = distance;
    }

    [ContextMenu("Reset Climb Progress")]
    public void ResetClimbProgressManual()
    {
        ResetClimbProgress();
    }
}
