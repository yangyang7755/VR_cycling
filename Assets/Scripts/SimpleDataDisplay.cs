using UnityEngine;
using TMPro;

/// <summary>
/// Simple real-time display for Speed, Power, and Gradient
/// Easy to set up - just drag the text components
/// </summary>
public class SimpleDataDisplay : MonoBehaviour
{
    [Header("Bike Controller Reference")]
    [Tooltip("Bike controller to get data from")]
    [SerializeField] private BikeController bikeController;

    [Header("UI Text References")]
    [Tooltip("Text to display speed (km/h)")]
    [SerializeField] private TextMeshProUGUI speedText;
    
    [Tooltip("Text to display power (watts)")]
    [SerializeField] private TextMeshProUGUI powerText;
    
    [Tooltip("Text to display gradient (percentage)")]
    [SerializeField] private TextMeshProUGUI gradientText;

    [Header("Display Settings")]
    [Tooltip("Update frequency (times per second)")]
    [SerializeField] private float updateRate = 10f; // Update 10 times per second
    
    [Tooltip("Auto-adjust font size on start (DISABLED - set font size in Inspector instead)")]
    [SerializeField] private bool autoSetFontSize = false; // Disabled by default to prevent runtime changes

    private float updateInterval;
    private float lastUpdateTime;
    private bool hasInitialized = false;

    private void Start()
    {
        // Auto-find bike controller if not assigned
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        if (bikeController == null)
        {
            Debug.LogError("[SimpleDataDisplay] BikeController not found! Please assign it in Inspector.");
        }

        // Only set font size once if enabled (but disabled by default)
        if (autoSetFontSize && !hasInitialized)
        {
            // This is disabled by default - set font size in Inspector instead
            // to prevent runtime changes that might affect UI layout
        }

        // Calculate update interval
        updateInterval = 1f / updateRate;
        lastUpdateTime = Time.time;
        hasInitialized = true;
    }

    private void Update()
    {
        if (bikeController == null) return;

        // Update at specified rate
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDisplay();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateDisplay()
    {
        // Update Speed
        if (speedText != null)
        {
            float speedKmh = bikeController.GetSpeedKmh();
            speedText.text = $"Speed: {speedKmh:F1} km/h";
        }

        // Update Power
        if (powerText != null)
        {
            float power = bikeController.GetPower();
            powerText.text = $"Power: {power:F0} W";
        }

        // Update Gradient
        if (gradientText != null)
        {
            float gradient = bikeController.GetGradient();
            gradientText.text = $"Gradient: {gradient:F1}%";
        }
    }
}
