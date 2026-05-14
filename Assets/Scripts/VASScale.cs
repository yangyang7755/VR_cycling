using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Visual Analogue Scale (VAS) for pain, reward, effort, or any subjective measure.
/// Pops up at configurable intervals during cycling trials.
/// Participant clicks/drags on a horizontal line to indicate their rating (0-100).
/// Responses are logged with timestamps for analysis.
/// 
/// SETUP: Add to any GameObject. Creates its own UI Canvas.
/// Configure scales and timing in the Inspector.
/// </summary>
public class VASScale : MonoBehaviour
{
    [Header("Scale Configuration")]
    [SerializeField] private List<VASConfig> scales = new List<VASConfig>();
    
    [Header("Timing")]
    [Tooltip("Time between VAS prompts (seconds)")]
    [SerializeField] private float promptInterval = 60f;
    
    [Tooltip("Randomize interval (±seconds)")]
    [SerializeField] private float intervalJitter = 10f;
    
    [Tooltip("Time allowed to respond before auto-dismiss (seconds, 0 = wait forever)")]
    [SerializeField] private float responseTimeout = 15f;
    
    [Tooltip("Delay before first prompt (seconds)")]
    [SerializeField] private float initialDelay = 30f;
    
    [Header("Appearance")]
    [SerializeField] private float scaleWidth = 500f;
    [SerializeField] private float scaleHeight = 8f;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private Color markerColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.1f, 0.85f);
    
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    
    [Header("Data Output")]
    [Tooltip("Log file path (relative to Application.persistentDataPath)")]
    [SerializeField] private string logFileName = "vas_responses.csv";
    [SerializeField] private bool logToConsole = true;
    
    [System.Serializable]
    public class VASConfig
    {
        public string scaleName = "Pain";
        public string leftAnchor = "No Pain";
        public string rightAnchor = "Worst Pain Imaginable";
        public Color accentColor = new Color(1f, 0.3f, 0.3f);
        [Tooltip("Use 1-10 discrete scale instead of 0-100 continuous")]
        public bool useDiscreteScale = false;
        [Tooltip("Color at high end of scale (for gradient effect)")]
        public Color highColor = new Color(0.6f, 0.2f, 0.9f); // Purple
    }
    
    [System.Serializable]
    public class VASResponse
    {
        public float timestamp;
        public float distance;
        public string scaleName;
        public float value; // 0-100
        public float responseTime; // seconds to respond
    }
    
    // Runtime state
    private Canvas canvas;
    private GameObject vasPanel;
    private Image scaleLine;
    private Image marker;
    private TextMeshProUGUI questionText;
    private TextMeshProUGUI leftLabel;
    private TextMeshProUGUI rightLabel;
    private TextMeshProUGUI valueText;
    private Button confirmButton;
    
    private bool isShowing = false;
    private int currentScaleIndex = 0;
    private float currentValue = 50f;
    private float promptShowTime = 0f;
    private float nextPromptTime = 0f;
    private bool isDragging = false;
    
    private List<VASResponse> responses = new List<VASResponse>();
    private RectTransform scaleRect;
    private string logFilePath;

    private void Start()
    {
        if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
        
        if (scales.Count == 0)
        {
            scales.Add(new VASConfig { scaleName = "Pain", leftAnchor = "No Pain", rightAnchor = "Worst Pain Imaginable", accentColor = new Color(1f, 0.3f, 0.3f) });
            scales.Add(new VASConfig { scaleName = "Reward", leftAnchor = "No Reward", rightAnchor = "Extremely Rewarding", accentColor = new Color(0.3f, 0.8f, 0.3f) });
        }
        
        logFilePath = System.IO.Path.Combine(Application.persistentDataPath, logFileName);
        WriteLogHeader();
        
        CreateUI();
        HideVAS();
        
        nextPromptTime = Time.time + initialDelay;
        
        Debug.Log($"[VAS] Initialized with {scales.Count} scales. First prompt in {initialDelay}s. Log: {logFilePath}");
    }
    
    private void Update()
    {
        if (isShowing)
        {
            HandleInput();
            
            // Auto-dismiss on timeout
            if (responseTimeout > 0f && Time.time - promptShowTime > responseTimeout)
            {
                RecordResponse();
            }
        }
        else
        {
            // Only show VAS when explicitly triggered (not on timer)
            // The HillClimbExperiment calls TriggerVASNow() when needed
        }
    }
    
    private void HandleInput()
    {
        VASConfig config = scales[(currentScaleIndex - 1 + scales.Count) % scales.Count];
        bool discrete = config.useDiscreteScale;
        float maxVal = discrete ? 10f : 100f;
        float minVal = discrete ? 1f : 0f;

        if (Input.GetMouseButton(0))
        {
            // Check if clicking on the scale
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(scaleRect, Input.mousePosition, null, out localPoint))
            {
                float normalized = Mathf.Clamp01((localPoint.x + scaleWidth * 0.5f) / scaleWidth);
                currentValue = discrete 
                    ? Mathf.Round(normalized * 9f + 1f) // 1-10
                    : normalized * 100f;
                UpdateMarkerPosition();
                isDragging = true;
            }
        }
        
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
        }
        
        // Keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            RecordResponse();
        }
        
        // Arrow keys
        if (discrete)
        {
            // Discrete: step by 1
            if (Input.GetKeyDown(KeyCode.LeftArrow))
                currentValue = Mathf.Max(minVal, currentValue - 1f);
            if (Input.GetKeyDown(KeyCode.RightArrow))
                currentValue = Mathf.Min(maxVal, currentValue + 1f);
        }
        else
        {
            // Continuous: smooth movement
            if (Input.GetKey(KeyCode.LeftArrow))
                currentValue = Mathf.Max(minVal, currentValue - 30f * Time.deltaTime);
            if (Input.GetKey(KeyCode.RightArrow))
                currentValue = Mathf.Min(maxVal, currentValue + 30f * Time.deltaTime);
        }
        
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            UpdateMarkerPosition();
    }
    
    private void ShowNextVAS()
    {
        currentScaleIndex = currentScaleIndex % scales.Count;
        ShowVAS(scales[currentScaleIndex]);
        currentScaleIndex++;
    }
    
    private void ShowVAS(VASConfig config)
    {
        isShowing = true;
        currentValue = config.useDiscreteScale ? 5f : 50f; // Start at middle
        promptShowTime = Time.time;
        
        vasPanel.SetActive(true);
        
        questionText.text = config.useDiscreteScale 
            ? $"How much {config.scaleName.ToLower()} did you feel in this trial?"
            : $"Rate your current {config.scaleName}:";
        leftLabel.text = config.leftAnchor;
        rightLabel.text = config.rightAnchor;
        marker.color = config.accentColor;
        
        UpdateMarkerPosition();
        valueText.text = config.useDiscreteScale 
            ? "Use ← → arrows to select (1-10), then press Enter"
            : "Click on the line or use ← → arrows, then press Enter";
        
        // Send event marker
        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent($"{config.scaleName.ToUpper()}_VAS_SHOW");
    }
    
    private void HideVAS()
    {
        isShowing = false;
        if (vasPanel != null) vasPanel.SetActive(false);
    }
    
    private void RecordResponse()
    {
        VASConfig config = scales[(currentScaleIndex - 1 + scales.Count) % scales.Count];
        float responseTime = Time.time - promptShowTime;
        
        // For discrete scale, store as integer 1-10; for continuous, store 0-100
        float storedValue = config.useDiscreteScale ? Mathf.RoundToInt(currentValue) : currentValue;
        
        VASResponse response = new VASResponse
        {
            timestamp = Time.time,
            distance = bikeController != null ? bikeController.GetDistance() : 0f,
            scaleName = config.scaleName,
            value = storedValue,
            responseTime = responseTime
        };
        
        responses.Add(response);
        WriteResponseToLog(response);
        
        // Send event marker
        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent($"{config.scaleName.ToUpper()}_VAS_RESPONSE", 
                $"value={storedValue:F1},rt={responseTime:F2}");
        
        string scaleLabel = config.useDiscreteScale ? "/10" : "/100";
        if (logToConsole)
        {
            Debug.Log($"[VAS] {config.scaleName}: {storedValue:F0}{scaleLabel} (RT: {responseTime:F1}s, Dist: {response.distance:F0}m)");
        }
        
        HideVAS();
        
        // Schedule next prompt
        float jitter = Random.Range(-intervalJitter, intervalJitter);
        nextPromptTime = Time.time + promptInterval + jitter;
    }
    
    private void UpdateMarkerPosition()
    {
        if (marker == null) return;
        
        VASConfig config = scales[(currentScaleIndex - 1 + scales.Count) % scales.Count];
        bool discrete = config.useDiscreteScale;
        
        float normalized;
        if (discrete)
        {
            // 1-10 maps to 0-1
            normalized = (currentValue - 1f) / 9f;
            valueText.text = $"{Mathf.RoundToInt(currentValue)}";
            
            // Purple gradient: low = accent color, high = purple
            marker.color = Color.Lerp(config.accentColor, config.highColor, normalized);
        }
        else
        {
            normalized = currentValue / 100f;
            valueText.text = $"{currentValue:F0}";
        }
        
        float x = (normalized - 0.5f) * scaleWidth;
        marker.rectTransform.anchoredPosition = new Vector2(x, 0f);
    }

    private void CreateUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("VAS_Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // Above HUD
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Panel (centered)
        vasPanel = new GameObject("VASPanel");
        vasPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = vasPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(scaleWidth + 60f, 180f);
        Image panelBg = vasPanel.AddComponent<Image>();
        panelBg.color = panelColor;
        
        // Question text
        GameObject qObj = new GameObject("Question");
        qObj.transform.SetParent(vasPanel.transform, false);
        questionText = qObj.AddComponent<TextMeshProUGUI>();
        questionText.text = "Rate your current Pain:";
        questionText.fontSize = 22;
        questionText.color = Color.white;
        questionText.alignment = TextAlignmentOptions.Center;
        RectTransform qRect = qObj.GetComponent<RectTransform>();
        qRect.anchorMin = new Vector2(0f, 1f);
        qRect.anchorMax = new Vector2(1f, 1f);
        qRect.pivot = new Vector2(0.5f, 1f);
        qRect.anchoredPosition = new Vector2(0f, -10f);
        qRect.sizeDelta = new Vector2(0f, 35f);
        
        // Scale line
        GameObject lineObj = new GameObject("ScaleLine");
        lineObj.transform.SetParent(vasPanel.transform, false);
        scaleLine = lineObj.AddComponent<Image>();
        scaleLine.color = lineColor;
        scaleRect = lineObj.GetComponent<RectTransform>();
        scaleRect.anchorMin = new Vector2(0.5f, 0.5f);
        scaleRect.anchorMax = new Vector2(0.5f, 0.5f);
        scaleRect.pivot = new Vector2(0.5f, 0.5f);
        scaleRect.sizeDelta = new Vector2(scaleWidth, scaleHeight);
        scaleRect.anchoredPosition = new Vector2(0f, -5f);
        
        // End ticks
        CreateTick(vasPanel.transform, -scaleWidth * 0.5f, 20f);
        CreateTick(vasPanel.transform, scaleWidth * 0.5f, 20f);
        
        // Marker (draggable)
        GameObject markerObj = new GameObject("Marker");
        markerObj.transform.SetParent(lineObj.transform, false);
        marker = markerObj.AddComponent<Image>();
        marker.color = markerColor;
        marker.rectTransform.sizeDelta = new Vector2(16f, 30f);
        marker.rectTransform.anchoredPosition = Vector2.zero;
        
        // Left anchor label
        GameObject leftObj = new GameObject("LeftAnchor");
        leftObj.transform.SetParent(vasPanel.transform, false);
        leftLabel = leftObj.AddComponent<TextMeshProUGUI>();
        leftLabel.text = "No Pain";
        leftLabel.fontSize = 14;
        leftLabel.color = new Color(0.7f, 0.7f, 0.7f);
        leftLabel.alignment = TextAlignmentOptions.Center;
        RectTransform lRect = leftObj.GetComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0.5f, 0.5f);
        lRect.anchorMax = new Vector2(0.5f, 0.5f);
        lRect.sizeDelta = new Vector2(150f, 25f);
        lRect.anchoredPosition = new Vector2(-scaleWidth * 0.5f, -25f);
        
        // Right anchor label
        GameObject rightObj = new GameObject("RightAnchor");
        rightObj.transform.SetParent(vasPanel.transform, false);
        rightLabel = rightObj.AddComponent<TextMeshProUGUI>();
        rightLabel.text = "Worst Pain";
        rightLabel.fontSize = 14;
        rightLabel.color = new Color(0.7f, 0.7f, 0.7f);
        rightLabel.alignment = TextAlignmentOptions.Center;
        RectTransform rRect = rightObj.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 0.5f);
        rRect.anchorMax = new Vector2(0.5f, 0.5f);
        rRect.sizeDelta = new Vector2(200f, 25f);
        rRect.anchoredPosition = new Vector2(scaleWidth * 0.5f, -25f);
        
        // Value / instruction text
        GameObject vObj = new GameObject("ValueText");
        vObj.transform.SetParent(vasPanel.transform, false);
        valueText = vObj.AddComponent<TextMeshProUGUI>();
        valueText.fontSize = 16;
        valueText.color = new Color(0.8f, 0.8f, 0.8f);
        valueText.alignment = TextAlignmentOptions.Center;
        RectTransform vRect = vObj.GetComponent<RectTransform>();
        vRect.anchorMin = new Vector2(0f, 0f);
        vRect.anchorMax = new Vector2(1f, 0f);
        vRect.pivot = new Vector2(0.5f, 0f);
        vRect.anchoredPosition = new Vector2(0f, 8f);
        vRect.sizeDelta = new Vector2(0f, 25f);
        
        // Confirm button
        GameObject btnObj = new GameObject("ConfirmBtn");
        btnObj.transform.SetParent(vasPanel.transform, false);
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.2f, 0.5f, 0.2f);
        confirmButton = btnObj.AddComponent<Button>();
        confirmButton.onClick.AddListener(RecordResponse);
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0f);
        btnRect.anchorMax = new Vector2(1f, 0f);
        btnRect.pivot = new Vector2(1f, 0f);
        btnRect.anchoredPosition = new Vector2(-5f, 5f);
        btnRect.sizeDelta = new Vector2(80f, 30f);
        
        GameObject btnTextObj = new GameObject("BtnText");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "Confirm";
        btnText.fontSize = 16;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
        RectTransform btRect = btnTextObj.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.sizeDelta = Vector2.zero;
    }
    
    private void CreateTick(Transform parent, float xPos, float height)
    {
        GameObject tick = new GameObject("Tick");
        tick.transform.SetParent(parent, false);
        Image tickImg = tick.AddComponent<Image>();
        tickImg.color = lineColor;
        RectTransform tRect = tick.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 0.5f);
        tRect.anchorMax = new Vector2(0.5f, 0.5f);
        tRect.sizeDelta = new Vector2(3f, height);
        tRect.anchoredPosition = new Vector2(xPos, -5f);
    }
    
    // --- Data Logging ---
    
    private void WriteLogHeader()
    {
        try
        {
            using (var writer = new System.IO.StreamWriter(logFilePath, false))
            {
                writer.WriteLine("timestamp,distance_m,scale_name,value_0to100,response_time_s");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VAS] Failed to write log header: {e.Message}");
        }
    }
    
    private void WriteResponseToLog(VASResponse r)
    {
        try
        {
            using (var writer = new System.IO.StreamWriter(logFilePath, true))
            {
                writer.WriteLine($"{r.timestamp:F2},{r.distance:F1},{r.scaleName},{r.value:F1},{r.responseTime:F2}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VAS] Failed to write response: {e.Message}");
        }
    }
    
    // --- Public API ---
    
    public void TriggerVASNow(string scaleName = null)
    {
        if (scaleName != null)
        {
            for (int i = 0; i < scales.Count; i++)
            {
                if (scales[i].scaleName == scaleName)
                {
                    currentScaleIndex = i;
                    break;
                }
            }
        }
        ShowNextVAS();
    }
    
    public List<VASResponse> GetAllResponses() => new List<VASResponse>(responses);
    public int GetResponseCount() => responses.Count;
    public bool IsShowing() => isShowing;
    
    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
