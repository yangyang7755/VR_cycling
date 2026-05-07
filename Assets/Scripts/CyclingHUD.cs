using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Clean Zwift-style cycling HUD. Creates its own UI elements.
/// Shows: Speed, Power, Cadence, HR, Gradient, Distance.
/// 
/// SETUP: Add to any GameObject. It creates a Canvas with all UI elements.
/// Disable or remove old HUD scripts (SimpleDataDisplay, BikeDataUI, etc.)
/// </summary>
public class CyclingHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private WebSocketClient webSocketClient;
    
    [Header("Display Options")]
    [SerializeField] private bool showSpeed = true;
    [SerializeField] private bool showPower = true;
    [SerializeField] private bool showCadence = true;
    [SerializeField] private bool showHeartRate = true;
    [SerializeField] private bool showGradient = true;
    [SerializeField] private bool showDistance = true;
    
    [Header("Position")]
    [SerializeField] private HUDPosition position = HUDPosition.TopLeft;
    public enum HUDPosition { TopLeft, TopRight, TopCenter }
    
    [Header("Style")]
    [SerializeField] private float panelOpacity = 0.75f;
    [SerializeField] private int fontSize = 22;
    
    // UI references
    private Canvas canvas;
    private TextMeshProUGUI speedLabel, speedValue;
    private TextMeshProUGUI powerLabel, powerValue;
    private TextMeshProUGUI cadenceLabel, cadenceValue;
    private TextMeshProUGUI hrLabel, hrValue;
    private TextMeshProUGUI gradientLabel, gradientValue;
    private TextMeshProUGUI distanceLabel, distanceValue;
    private float updateTimer;

    private void Start()
    {
        if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
        if (webSocketClient == null) webSocketClient = FindObjectOfType<WebSocketClient>();
        
        CreateHUD();
    }
    
    private void CreateHUD()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("CyclingHUD_Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Panel background
        GameObject panel = new GameObject("HUDPanel");
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.05f, 0.1f, panelOpacity);
        
        // Position
        switch (position)
        {
            case HUDPosition.TopLeft:
                panelRect.anchorMin = new Vector2(0f, 1f);
                panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                panelRect.anchoredPosition = new Vector2(15f, -15f);
                break;
            case HUDPosition.TopRight:
                panelRect.anchorMin = new Vector2(1f, 1f);
                panelRect.anchorMax = new Vector2(1f, 1f);
                panelRect.pivot = new Vector2(1f, 1f);
                panelRect.anchoredPosition = new Vector2(-15f, -15f);
                break;
            case HUDPosition.TopCenter:
                panelRect.anchorMin = new Vector2(0.5f, 1f);
                panelRect.anchorMax = new Vector2(0.5f, 1f);
                panelRect.pivot = new Vector2(0.5f, 1f);
                panelRect.anchoredPosition = new Vector2(0f, -15f);
                break;
        }
        
        // Add vertical layout
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 2f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Create data rows
        if (showSpeed) CreateRow(panel.transform, "SPEED", "0.0 km/h", new Color(1f, 1f, 1f), out speedLabel, out speedValue);
        if (showPower) CreateRow(panel.transform, "POWER", "0 W", new Color(1f, 0.85f, 0.3f), out powerLabel, out powerValue);
        if (showCadence) CreateRow(panel.transform, "CADENCE", "0 rpm", new Color(0.5f, 0.85f, 1f), out cadenceLabel, out cadenceValue);
        if (showHeartRate) CreateRow(panel.transform, "HR", "-- bpm", new Color(1f, 0.4f, 0.4f), out hrLabel, out hrValue);
        if (showGradient) CreateRow(panel.transform, "GRADE", "0.0%", new Color(0.5f, 1f, 0.5f), out gradientLabel, out gradientValue);
        if (showDistance) CreateRow(panel.transform, "DIST", "0 m", new Color(0.7f, 0.7f, 0.8f), out distanceLabel, out distanceValue);
    }
    
    private void CreateRow(Transform parent, string label, string defaultValue, Color valueColor, out TextMeshProUGUI labelTMP, out TextMeshProUGUI valueTMP)
    {
        GameObject row = new GameObject($"Row_{label}");
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(200f, fontSize + 6);
        
        HorizontalLayoutGroup hLayout = row.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 8f;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;
        hLayout.childAlignment = TextAnchor.MiddleLeft;
        
        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = label;
        labelTMP.fontSize = fontSize * 0.55f;
        labelTMP.color = new Color(0.5f, 0.5f, 0.6f);
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.minWidth = 70f;
        labelLE.preferredWidth = 70f;
        
        // Value
        GameObject valueObj = new GameObject("Value");
        valueObj.transform.SetParent(row.transform, false);
        valueTMP = valueObj.AddComponent<TextMeshProUGUI>();
        valueTMP.text = defaultValue;
        valueTMP.fontSize = fontSize;
        valueTMP.color = valueColor;
        valueTMP.alignment = TextAlignmentOptions.MidlineRight;
        valueTMP.fontStyle = FontStyles.Bold;
        LayoutElement valueLE = valueObj.AddComponent<LayoutElement>();
        valueLE.minWidth = 120f;
        valueLE.preferredWidth = 120f;
    }
    
    private void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer < 0.1f) return; // 10 Hz update
        updateTimer = 0f;
        
        if (bikeController == null) return;
        
        if (showSpeed && speedValue != null)
            speedValue.text = $"{bikeController.GetSpeedKmh():F1} km/h";
        
        if (showPower && powerValue != null)
            powerValue.text = $"{bikeController.GetPower():F0} W";
        
        if (showCadence && cadenceValue != null)
            cadenceValue.text = $"{bikeController.GetCadence():F0} rpm";
        
        if (showHeartRate && hrValue != null)
        {
            // Re-find WebSocket client if not found yet
            if (webSocketClient == null)
                webSocketClient = FindObjectOfType<WebSocketClient>();
            
            float hr = 0f;
            if (webSocketClient != null)
            {
                hr = webSocketClient.GetHeartRateBpm();
                // Debug: log once when HR first appears
                if (hr > 0 && hrValue.text == "-- bpm")
                    Debug.Log($"[CyclingHUD] HR data received: {hr:F0} bpm (connected={webSocketClient.IsConnected()})");
            }
            
            hrValue.text = hr > 0 ? $"{hr:F0} bpm" : "-- bpm";
            hrValue.color = hr > 0 ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 0.4f, 0.4f);
        }
        
        if (showGradient && gradientValue != null)
        {
            float grad = bikeController.GetGradient();
            string sign = grad >= 0 ? "+" : "";
            gradientValue.text = $"{sign}{grad:F1}%";
            // Color by gradient
            if (grad > 6f) gradientValue.color = new Color(1f, 0.3f, 0.2f);
            else if (grad > 3f) gradientValue.color = new Color(1f, 0.7f, 0.2f);
            else if (grad > 0.5f) gradientValue.color = new Color(0.5f, 1f, 0.5f);
            else if (grad < -1f) gradientValue.color = new Color(0.4f, 0.7f, 1f);
            else gradientValue.color = new Color(0.5f, 1f, 0.5f);
        }
        
        if (showDistance && distanceValue != null)
        {
            float dist = bikeController.GetDistance();
            distanceValue.text = dist >= 1000f ? $"{dist/1000f:F2} km" : $"{dist:F0} m";
        }
    }
    
    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
