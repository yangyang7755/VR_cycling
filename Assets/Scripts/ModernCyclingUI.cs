using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Modern, clean cycling UI with improved visual hierarchy
/// Features: Large speed display, power zones, gradient visualization, mini-map
/// Inspired by modern cycling apps (Zwift, TrainerRoad, Rouvy)
/// </summary>
public class ModernCyclingUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private WebSocketClient webSocketClient;
    
    [Header("Main Display - Speed")]
    [SerializeField] private TextMeshProUGUI speedValueText;
    [SerializeField] private TextMeshProUGUI speedUnitText;
    [SerializeField] private Image speedBackgroundGlow;
    
    [Header("Power Display")]
    [SerializeField] private TextMeshProUGUI powerValueText;
    [SerializeField] private TextMeshProUGUI powerZoneText;
    [SerializeField] private Image powerZoneBar;
    [SerializeField] private Image powerZoneIndicator;
    
    [Header("Gradient Display")]
    [SerializeField] private TextMeshProUGUI gradientValueText;
    [SerializeField] private Image gradientArrow;
    [SerializeField] private Image gradientBackground;
    
    [Header("Secondary Metrics")]
    [SerializeField] private TextMeshProUGUI cadenceText;
    [SerializeField] private TextMeshProUGUI heartRateText;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private TextMeshProUGUI timeText;
    
    [Header("Power Zones (Watts)")]
    [SerializeField] private float zone1Max = 100f;  // Recovery
    [SerializeField] private float zone2Max = 150f;  // Endurance
    [SerializeField] private float zone3Max = 200f;  // Tempo
    [SerializeField] private float zone4Max = 250f;  // Threshold
    [SerializeField] private float zone5Max = 300f;  // VO2 Max
    // Above zone5Max = Anaerobic
    
    [Header("Visual Settings")]
    [SerializeField] private bool enableGlowEffects = true;
    [SerializeField] private bool enablePowerZoneColors = true;
    [SerializeField] private float updateRate = 10f;
    
    [Header("Colors")]
    [SerializeField] private Color zone1Color = new Color(0.5f, 0.5f, 0.5f); // Gray
    [SerializeField] private Color zone2Color = new Color(0.3f, 0.6f, 1f);   // Blue
    [SerializeField] private Color zone3Color = new Color(0.3f, 1f, 0.3f);   // Green
    [SerializeField] private Color zone4Color = new Color(1f, 0.8f, 0f);     // Yellow
    [SerializeField] private Color zone5Color = new Color(1f, 0.4f, 0f);     // Orange
    [SerializeField] private Color zone6Color = new Color(1f, 0.2f, 0.2f);   // Red
    
    private float updateInterval;
    private float lastUpdateTime;
    private float sessionStartTime;
    
    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (webSocketClient == null)
            webSocketClient = FindObjectOfType<WebSocketClient>();
        
        updateInterval = 1f / updateRate;
        sessionStartTime = Time.time;
    }
    
    private void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        UpdateAllDisplays();
    }
    
    private void UpdateAllDisplays()
    {
        if (bikeController == null) return;
        
        UpdateSpeedDisplay();
        UpdatePowerDisplay();
        UpdateGradientDisplay();
        UpdateSecondaryMetrics();
    }
    
    private void UpdateSpeedDisplay()
    {
        float speedKmh = bikeController.GetSpeedKmh();
        
        if (speedValueText != null)
        {
            speedValueText.text = speedKmh.ToString("F1");
        }
        
        if (speedUnitText != null)
        {
            speedUnitText.text = "km/h";
        }
        
        // Glow effect based on speed
        if (enableGlowEffects && speedBackgroundGlow != null)
        {
            float glowIntensity = Mathf.Clamp01(speedKmh / 40f);
            Color glowColor = Color.Lerp(Color.white, new Color(0.3f, 0.8f, 1f), glowIntensity);
            speedBackgroundGlow.color = glowColor * new Color(1f, 1f, 1f, 0.3f);
        }
    }
    
    private void UpdatePowerDisplay()
    {
        float power = bikeController.GetPower();
        
        if (powerValueText != null)
        {
            powerValueText.text = $"{power:F0}W";
        }
        
        // Determine power zone
        int zone = GetPowerZone(power);
        string zoneName = GetPowerZoneName(zone);
        Color zoneColor = GetPowerZoneColor(zone);
        
        if (powerZoneText != null)
        {
            powerZoneText.text = zoneName;
            if (enablePowerZoneColors)
            {
                powerZoneText.color = zoneColor;
            }
        }
        
        // Update power zone bar
        if (powerZoneBar != null && enablePowerZoneColors)
        {
            powerZoneBar.color = zoneColor * new Color(1f, 1f, 1f, 0.5f);
        }
        
        // Update power zone indicator position
        if (powerZoneIndicator != null)
        {
            float normalizedPower = Mathf.Clamp01(power / 400f);
            RectTransform rect = powerZoneIndicator.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(normalizedPower, 0f);
                rect.anchorMax = new Vector2(normalizedPower, 1f);
            }
        }
    }
    
    private void UpdateGradientDisplay()
    {
        float gradient = bikeController.GetGradient();
        
        if (gradientValueText != null)
        {
            string sign = gradient >= 0 ? "+" : "";
            gradientValueText.text = $"{sign}{gradient:F1}%";
        }
        
        // Rotate arrow based on gradient
        if (gradientArrow != null)
        {
            float angle = Mathf.Clamp(gradient * -6f, -70f, 70f);
            gradientArrow.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }
        
        // Color background based on gradient
        if (gradientBackground != null)
        {
            Color bgColor;
            if (gradient < -1f)
            {
                bgColor = new Color(0.3f, 0.6f, 1f); // Blue for downhill
            }
            else if (gradient < 3f)
            {
                bgColor = new Color(0.3f, 0.8f, 0.3f); // Green for flat
            }
            else if (gradient < 7f)
            {
                bgColor = new Color(1f, 0.7f, 0f); // Orange for moderate
            }
            else
            {
                bgColor = new Color(1f, 0.3f, 0.3f); // Red for steep
            }
            gradientBackground.color = bgColor * new Color(1f, 1f, 1f, 0.6f);
        }
    }
    
    private void UpdateSecondaryMetrics()
    {
        // Cadence
        if (cadenceText != null)
        {
            float cadence = bikeController.GetCadence();
            cadenceText.text = $"{cadence:F0} RPM";
        }
        
        // Heart Rate
        if (heartRateText != null && webSocketClient != null && webSocketClient.IsConnected())
        {
            float hr = webSocketClient.GetHeartRateBpm();
            heartRateText.text = hr > 0 ? $"{hr:F0} BPM" : "-- BPM";
        }
        
        // Distance
        if (distanceText != null)
        {
            float distance = bikeController.GetDistance();
            distanceText.text = $"{distance:F0}m";
        }
        
        // Time
        if (timeText != null)
        {
            float elapsed = Time.time - sessionStartTime;
            int minutes = Mathf.FloorToInt(elapsed / 60f);
            int seconds = Mathf.FloorToInt(elapsed % 60f);
            timeText.text = $"{minutes:D2}:{seconds:D2}";
        }
    }
    
    private int GetPowerZone(float power)
    {
        if (power <= zone1Max) return 1;
        if (power <= zone2Max) return 2;
        if (power <= zone3Max) return 3;
        if (power <= zone4Max) return 4;
        if (power <= zone5Max) return 5;
        return 6;
    }
    
    private string GetPowerZoneName(int zone)
    {
        switch (zone)
        {
            case 1: return "Recovery";
            case 2: return "Endurance";
            case 3: return "Tempo";
            case 4: return "Threshold";
            case 5: return "VO2 Max";
            case 6: return "Anaerobic";
            default: return "Unknown";
        }
    }
    
    private Color GetPowerZoneColor(int zone)
    {
        switch (zone)
        {
            case 1: return zone1Color;
            case 2: return zone2Color;
            case 3: return zone3Color;
            case 4: return zone4Color;
            case 5: return zone5Color;
            case 6: return zone6Color;
            default: return Color.white;
        }
    }
    
    public void ResetSession()
    {
        sessionStartTime = Time.time;
    }
}
