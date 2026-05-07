using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Simple distance progress bar showing only distance information
/// Displays: current position / total route length
/// Positioned at top center of screen
/// </summary>
public class DistanceProgressBar : MonoBehaviour
{
    [Header("Bike Controller")]
    [Tooltip("Bike controller to get distance from")]
    [SerializeField] private BikeController bikeController;

    [Header("UI References")]
    [Tooltip("Main container for the progress bar")]
    [SerializeField] private RectTransform container;

    [Tooltip("Progress bar slider (horizontal bar)")]
    [SerializeField] private Slider progressSlider;

    [Tooltip("Progress bar fill image")]
    [SerializeField] private Image progressFill;

    [Tooltip("Text showing distance (e.g., '250m / 500m')")]
    [SerializeField] private TextMeshProUGUI distanceText;

    [Tooltip("Text showing progress percentage (optional)")]
    [SerializeField] private TextMeshProUGUI percentageText;

    [Header("Settings")]
    [Tooltip("Total route length in meters")]
    [SerializeField] private float totalRouteLength = 500f;

    [Tooltip("Update frequency (times per second)")]
    [SerializeField] private float updateRate = 10f;

    [Tooltip("Color of progress bar fill")]
    [SerializeField] private Color fillColor = Color.yellow;

    [Header("Position Settings")]
    [Tooltip("Anchor position: Top Center")]
    [SerializeField] private bool autoPosition = true;

    [Tooltip("Offset from top of screen (pixels)")]
    [SerializeField] private float topOffset = 50f;

    private float updateInterval;
    private float lastUpdateTime;
    private float courseStartDistance = 0f;

    private void Start()
    {
        // Auto-find bike controller
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
        }

        // Calculate update interval
        updateInterval = 1f / updateRate;

        // Auto-position container at top center
        if (autoPosition && container != null)
        {
            SetupTopCenterPosition();
        }

        // Set progress bar fill color
        if (progressFill != null)
        {
            progressFill.color = fillColor;
        }

        // Initialize course start distance
        if (bikeController != null)
        {
            courseStartDistance = bikeController.GetDistance();
        }

        // Initialize UI
        UpdateProgress();
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateProgress();
            lastUpdateTime = Time.time;
        }
    }

    /// <summary>
    /// Setup container position at top center of screen
    /// </summary>
    private void SetupTopCenterPosition()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        if (canvas != null && container != null)
        {
            // Set anchor to top center
            container.anchorMin = new Vector2(0.5f, 1f);
            container.anchorMax = new Vector2(0.5f, 1f);
            container.pivot = new Vector2(0.5f, 1f);

            // Position at top center with offset
            float canvasHeight = canvas.GetComponent<RectTransform>().rect.height;
            container.anchoredPosition = new Vector2(0f, -topOffset);
        }
    }

    /// <summary>
    /// Update progress bar and distance text
    /// </summary>
    private void UpdateProgress()
    {
        if (bikeController == null) return;

        // Get current distance
        float currentDistance = bikeController.GetDistance();
        float distanceFromStart = currentDistance - courseStartDistance;

        // Clamp distance to total route length
        distanceFromStart = Mathf.Clamp(distanceFromStart, 0f, totalRouteLength);

        // Calculate progress (0 to 1)
        float progress = totalRouteLength > 0f ? distanceFromStart / totalRouteLength : 0f;

        // Update slider
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }

        // Update distance text
        if (distanceText != null)
        {
            distanceText.text = $"{distanceFromStart:F0}m / {totalRouteLength:F0}m";
        }

        // Update percentage text (optional)
        if (percentageText != null)
        {
            percentageText.text = $"{(progress * 100f):F0}%";
        }
    }

    /// <summary>
    /// Reset progress bar (call when starting a new trial)
    /// </summary>
    public void ResetProgress()
    {
        if (bikeController != null)
        {
            courseStartDistance = bikeController.GetDistance();
        }
        UpdateProgress();
    }

    /// <summary>
    /// Set total route length
    /// </summary>
    public void SetTotalRouteLength(float length)
    {
        totalRouteLength = length;
        UpdateProgress();
    }
}
