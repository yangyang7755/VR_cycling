using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Diagnostics;
using System.IO;

/// <summary>
/// Pre-trial Bluetooth connection UI.
/// Shows connection status for the bike trainer and HR monitor before allowing trial start.
/// 
/// SELF-CONTAINED: Creates all UI elements from code. No manual setup required.
/// Just add this script to any GameObject in the scene.
/// 
/// Flow:
///   1. Panel appears on scene start (builds its own Canvas + UI)
///   2. Automatically monitors WebSocket connection for bike and HR data
///   3. "Proceed" button enables when both devices are confirmed
///   4. On proceed, hides panel and shows TrialStarterUI
/// </summary>
public class BluetoothConnectionUI : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Require HR data before allowing proceed")]
    [SerializeField] private bool requireHR = false;

    [Tooltip("Require bike data before allowing proceed")]
    [SerializeField] private bool requireBike = false;

    [Tooltip("Seconds before stale data = disconnected")]
    [SerializeField] private float dataTimeoutSeconds = 5f;

    [Header("Python Auto-Launch")]
    [Tooltip("Automatically launch the Python BLE bridge on start. Disable this if you run main.py manually.")]
    [SerializeField] private bool autoLaunchPython = false;

    [Tooltip("Path to Python executable (leave empty to use 'python3')")]
    [SerializeField] private string pythonPath = "";

    [Tooltip("Path to main.py relative to project root")]
    [SerializeField] private string scriptPath = "bluetooth/main.py";

    [Header("References (Auto-found if empty)")]
    [SerializeField] private WebSocketClient webSocketClient;
    [SerializeField] private TrialStarterUI trialStarterUI;

    // UI references (created at runtime)
    private GameObject canvasObj;
    private GameObject panelObj;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI wsStatusText;
    private TextMeshProUGUI bikeStatusText;
    private TextMeshProUGUI bikeTelemetryText;
    private Image bikeStatusIcon;
    private TextMeshProUGUI hrStatusText;
    private TextMeshProUGUI hrTelemetryText;
    private Image hrStatusIcon;
    private Button connectButton;
    private Button proceedButton;
    private TextMeshProUGUI proceedButtonText;

    // State
    private bool bikeDataReceived = false;
    private bool hrDataReceived = false;
    private float lastBikeDataTime = -999f;
    private float lastHRDataTime = -999f;
    private float lastSpeedKmh;
    private float lastPowerWatts;
    private float lastCadenceRpm;
    private float lastHeartRateBpm;
    private bool uiBuilt = false;

    // Python process
    private Process pythonProcess;
    private bool pythonLaunched = false;

    // Colors
    private readonly Color connectedColor = new Color(0.2f, 0.85f, 0.3f);
    private readonly Color disconnectedColor = new Color(0.9f, 0.25f, 0.25f);
    private readonly Color waitingColor = new Color(0.95f, 0.75f, 0.1f);
    private readonly Color panelBgColor = new Color(0.12f, 0.12f, 0.16f, 0.97f);
    private readonly Color cardBgColor = new Color(0.18f, 0.18f, 0.22f, 1f);
    private readonly Color buttonColor = new Color(0.2f, 0.5f, 0.9f, 1f);
    private readonly Color buttonDisabledColor = new Color(0.3f, 0.3f, 0.35f, 1f);

    private void Start()
    {
        // Auto-find references
        if (webSocketClient == null)
            webSocketClient = FindObjectOfType<WebSocketClient>();

        if (trialStarterUI == null)
            trialStarterUI = FindObjectOfType<TrialStarterUI>();

        // Build UI
        BuildUI();

        // Launch Python BLE bridge automatically
        if (autoLaunchPython)
        {
            LaunchPythonBridge();
        }

        // Subscribe to WebSocket events
        if (webSocketClient != null)
        {
            webSocketClient.OnConnected += OnWebSocketConnected;
            webSocketClient.OnDisconnected += OnWebSocketDisconnected;
            webSocketClient.OnTelemetryReceived += OnTelemetryReceived;
        }

        // Hide trial starter — connection must be confirmed first
        if (trialStarterUI != null)
        {
            trialStarterUI.HideStartPanel();
        }
    }

    private void OnDestroy()
    {
        if (webSocketClient != null)
        {
            webSocketClient.OnConnected -= OnWebSocketConnected;
            webSocketClient.OnDisconnected -= OnWebSocketDisconnected;
            webSocketClient.OnTelemetryReceived -= OnTelemetryReceived;
        }

        KillPythonProcess();
    }

    private void OnApplicationQuit()
    {
        KillPythonProcess();
    }

    // =========================================================================
    // PYTHON PROCESS MANAGEMENT
    // =========================================================================

    private void LaunchPythonBridge()
    {
        if (pythonLaunched) return;

        // Resolve paths
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string fullScriptPath = Path.Combine(projectRoot, scriptPath);
        string workingDir = Path.GetDirectoryName(fullScriptPath);

        if (!File.Exists(fullScriptPath))
        {
            UnityEngine.Debug.LogError($"[BluetoothUI] Python script not found: {fullScriptPath}");
            return;
        }

        // Determine Python executable — prefer venv if it exists
        string python = string.IsNullOrEmpty(pythonPath) ? "python3" : pythonPath;
        string venvPython = Path.Combine(workingDir, "venv", "bin", "python");
        if (File.Exists(venvPython))
        {
            python = venvPython;
            UnityEngine.Debug.Log($"[BluetoothUI] Using venv Python: {python}");
        }

        // Launch main.py with --headless flag (no prompts, no Python-side recording)
        string arguments = $"\"{fullScriptPath}\" --headless";

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = python,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            pythonProcess = new Process();
            pythonProcess.StartInfo = startInfo;
            pythonProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    UnityEngine.Debug.Log($"[BLE Bridge] {e.Data}");
            };
            pythonProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    UnityEngine.Debug.LogWarning($"[BLE Bridge ERR] {e.Data}");
            };

            pythonProcess.Start();
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();

            pythonLaunched = true;
            UnityEngine.Debug.Log($"[BluetoothUI] Python BLE bridge launched (PID: {pythonProcess.Id})");
            UnityEngine.Debug.Log($"[BluetoothUI] Mode: headless (no prompts, Unity controls recording)");
            UnityEngine.Debug.Log($"[BluetoothUI] Working dir: {workingDir}");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[BluetoothUI] Failed to launch Python: {e.Message}");
            UnityEngine.Debug.LogError("[BluetoothUI] Make sure Python 3 is installed and in PATH, or set pythonPath in Inspector.");
        }
    }

    private void KillPythonProcess()
    {
        if (pythonProcess != null && !pythonProcess.HasExited)
        {
            try
            {
                UnityEngine.Debug.Log($"[BluetoothUI] Killing Python BLE bridge (PID: {pythonProcess.Id})...");
                pythonProcess.Kill();
                pythonProcess.WaitForExit(3000);
                UnityEngine.Debug.Log("[BluetoothUI] Python process terminated.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[BluetoothUI] Error killing Python process: {e.Message}");
            }
            finally
            {
                pythonProcess.Dispose();
                pythonProcess = null;
            }
        }
    }

    private void Update()
    {
        if (uiBuilt && panelObj != null && panelObj.activeSelf)
        {
            UpdateUI();
        }
    }

    // =========================================================================
    // UI CONSTRUCTION (all from code — no manual setup needed)
    // =========================================================================

    private void BuildUI()
    {
        // --- Canvas ---
        canvasObj = new GameObject("BluetoothConnectionCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // On top of everything
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Ensure EventSystem exists
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // --- Full-screen dark panel ---
        panelObj = CreatePanel(canvasObj.transform, "ConnectionPanel", panelBgColor);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // --- Content container (centered, fixed width) ---
        GameObject content = CreatePanel(panelObj.transform, "Content", Color.clear);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(700, 520);
        contentRect.anchoredPosition = Vector2.zero;

        // Add vertical layout
        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 16;
        vlg.padding = new RectOffset(30, 30, 30, 30);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // --- Title ---
        titleText = CreateText(content.transform, "Title", "Device Connection", 28, FontStyles.Bold, Color.white);
        SetHeight(titleText.gameObject, 45);

        // --- WebSocket status ---
        wsStatusText = CreateText(content.transform, "WSStatus", "WebSocket: Checking...", 16, FontStyles.Normal, Color.gray);
        SetHeight(wsStatusText.gameObject, 28);

        // --- Bike Card ---
        GameObject bikeCard = CreateCard(content.transform, "BikeCard");
        bikeStatusIcon = CreateStatusIcon(bikeCard.transform, disconnectedColor);
        bikeStatusText = CreateText(bikeCard.transform, "BikeStatus", "Bike Trainer: Not detected", 20, FontStyles.Bold, Color.white);
        bikeTelemetryText = CreateText(bikeCard.transform, "BikeTelemetry", "No data", 15, FontStyles.Normal, new Color(0.7f, 0.7f, 0.7f));

        // --- HR Card ---
        GameObject hrCard = CreateCard(content.transform, "HRCard");
        hrStatusIcon = CreateStatusIcon(hrCard.transform, disconnectedColor);
        hrStatusText = CreateText(hrCard.transform, "HRStatus", "HR Monitor: Not detected", 20, FontStyles.Bold, Color.white);
        hrTelemetryText = CreateText(hrCard.transform, "HRTelemetry", "No data", 15, FontStyles.Normal, new Color(0.7f, 0.7f, 0.7f));

        // --- Spacer ---
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(content.transform, false);
        spacer.AddComponent<RectTransform>();
        SetHeight(spacer, 10);
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1;

        // --- Buttons row ---
        GameObject buttonRow = new GameObject("ButtonRow");
        buttonRow.transform.SetParent(content.transform, false);
        buttonRow.AddComponent<RectTransform>();
        SetHeight(buttonRow, 50);
        HorizontalLayoutGroup hlg = buttonRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        // Connect button
        connectButton = CreateButton(buttonRow.transform, "ConnectBtn", "Reconnect", 160, new Color(0.4f, 0.4f, 0.45f));
        connectButton.onClick.AddListener(OnConnectClicked);

        // Proceed button
        proceedButton = CreateButton(buttonRow.transform, "ProceedBtn", "Proceed to Trial Setup", 300, buttonColor);
        proceedButton.interactable = false;
        proceedButtonText = proceedButton.GetComponentInChildren<TextMeshProUGUI>();

        proceedButton.onClick.AddListener(OnProceedClicked);

        uiBuilt = true;
        UnityEngine.Debug.Log("[BluetoothUI] Connection panel built and displayed.");
    }

    // =========================================================================
    // UI HELPERS
    // =========================================================================

    private GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        Image img = obj.AddComponent<Image>();
        img.color = color;
        return obj;
    }

    private GameObject CreateCard(Transform parent, string name)
    {
        GameObject card = CreatePanel(parent, name, cardBgColor);
        SetHeight(card, 100);

        // Rounded look via slight padding
        VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.padding = new RectOffset(60, 20, 15, 15);
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        return card;
    }

    private Image CreateStatusIcon(Transform parent, Color color)
    {
        GameObject iconObj = new GameObject("StatusIcon");
        iconObj.transform.SetParent(parent, false);
        RectTransform rt = iconObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(25, 0);
        rt.sizeDelta = new Vector2(20, 20);

        Image img = iconObj.AddComponent<Image>();
        img.color = color;

        // Make it circular by using default sprite
        img.sprite = null; // Will appear as a square; Unity's default UI is fine
        return img;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(600, fontSize + 15);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.enableWordWrapping = true;
        tmp.richText = true;

        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string label, float width, Color bgColor)
    {
        GameObject btnObj = CreatePanel(parent, name, bgColor);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 50);
        btnObj.AddComponent<LayoutElement>().preferredWidth = width;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = bgColor;
        cb.highlightedColor = bgColor * 1.2f;
        cb.pressedColor = bgColor * 0.8f;
        cb.disabledColor = buttonDisabledColor;
        btn.colors = cb;

        // Button text
        TextMeshProUGUI txt = CreateText(btnObj.transform, "Label", label, 16, FontStyles.Bold, Color.white);
        txt.alignment = TextAlignmentOptions.Center;
        RectTransform txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        return btn;
    }

    private void SetHeight(GameObject obj, float height)
    {
        LayoutElement le = obj.GetComponent<LayoutElement>();
        if (le == null) le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
    }

    // =========================================================================
    // UI UPDATE
    // =========================================================================

    private void UpdateUI()
    {
        bool wsConnected = webSocketClient != null && webSocketClient.IsConnected();
        float now = Time.time;

        // WebSocket status
        if (wsStatusText != null)
        {
            if (wsConnected)
                wsStatusText.text = "WebSocket: <color=#33CC33>Connected</color> to Python BLE bridge";
            else if (pythonLaunched)
                wsStatusText.text = "WebSocket: <color=#CCAA11>Python bridge starting...</color>";
            else
                wsStatusText.text = "WebSocket: <color=#CC3333>Disconnected</color> — launch Python bridge";
        }

        // Bike trainer
        bool bikeActive = wsConnected && bikeDataReceived && (now - lastBikeDataTime < dataTimeoutSeconds);

        if (bikeStatusText != null)
        {
            if (bikeActive)
                bikeStatusText.text = "Bike Trainer: <color=#33CC33>Connected</color>";
            else if (wsConnected && !bikeDataReceived)
                bikeStatusText.text = "Bike Trainer: <color=#CCAA11>Waiting for data...</color>";
            else
                bikeStatusText.text = "Bike Trainer: <color=#CC3333>Not detected</color>";
        }

        if (bikeStatusIcon != null)
        {
            if (bikeActive) bikeStatusIcon.color = connectedColor;
            else if (wsConnected) bikeStatusIcon.color = waitingColor;
            else bikeStatusIcon.color = disconnectedColor;
        }

        if (bikeTelemetryText != null)
        {
            bikeTelemetryText.text = bikeActive
                ? $"Speed: {lastSpeedKmh:F1} km/h   Power: {lastPowerWatts:F0} W   Cadence: {lastCadenceRpm:F0} rpm"
                : "Waiting for telemetry...";
        }

        // HR monitor
        bool hrActive = wsConnected && hrDataReceived && (now - lastHRDataTime < dataTimeoutSeconds);

        if (hrStatusText != null)
        {
            if (hrActive)
                hrStatusText.text = "HR Monitor: <color=#33CC33>Connected</color>";
            else if (wsConnected && !hrDataReceived)
                hrStatusText.text = "HR Monitor: <color=#CCAA11>Waiting for signal...</color>";
            else
                hrStatusText.text = "HR Monitor: <color=#CC3333>Not detected</color>";
        }

        if (hrStatusIcon != null)
        {
            if (hrActive) hrStatusIcon.color = connectedColor;
            else if (wsConnected) hrStatusIcon.color = waitingColor;
            else hrStatusIcon.color = disconnectedColor;
        }

        if (hrTelemetryText != null)
        {
            hrTelemetryText.text = hrActive
                ? $"Heart Rate: {lastHeartRateBpm:F0} bpm"
                : "Waiting for heart rate...";
        }

        // Proceed button — only requires WebSocket connection
        // Device status is informational; you can proceed with any subset connected
        bool canProceed = wsConnected;

        if (proceedButton != null)
            proceedButton.interactable = canProceed;

        if (proceedButtonText != null)
            proceedButtonText.text = canProceed ? "Proceed to Trial Setup  \u2192" : "Connecting...";

        // Show/hide connect button based on WS state
        if (connectButton != null)
            connectButton.gameObject.SetActive(!wsConnected);
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void OnWebSocketConnected()
    {
        UnityEngine.Debug.Log("[BluetoothUI] WebSocket connected");
    }

    private void OnWebSocketDisconnected()
    {
        UnityEngine.Debug.Log("[BluetoothUI] WebSocket disconnected");
        bikeDataReceived = false;
        hrDataReceived = false;
    }

    private void OnTelemetryReceived(WebSocketClient.TelemetryData data)
    {
        float now = Time.time;

        // Bike data — only mark as connected if we get actual non-zero values
        bool hasBikeData = false;
        if (data.speed_kmh.HasValue) { lastSpeedKmh = data.speed_kmh.Value; hasBikeData = true; }
        if (data.power_watts.HasValue) { lastPowerWatts = data.power_watts.Value; hasBikeData = true; }
        if (data.cadence_rpm.HasValue) { lastCadenceRpm = data.cadence_rpm.Value; hasBikeData = true; }

        if (hasBikeData)
        {
            lastBikeDataTime = now;
            // Only mark as truly connected if at least one value is non-zero
            if (lastSpeedKmh > 0 || lastPowerWatts > 0 || lastCadenceRpm > 0)
            {
                if (!bikeDataReceived)
                    UnityEngine.Debug.Log($"[BluetoothUI] Bike trainer CONFIRMED — Power:{lastPowerWatts}W Speed:{lastSpeedKmh}km/h Cadence:{lastCadenceRpm}rpm");
                bikeDataReceived = true;
            }
        }

        // HR data
        if (data.heart_rate_bpm.HasValue && data.heart_rate_bpm.Value > 0)
        {
            if (!hrDataReceived)
                UnityEngine.Debug.Log($"[BluetoothUI] HR monitor CONFIRMED — {data.heart_rate_bpm.Value} bpm");
            hrDataReceived = true;
            lastHRDataTime = now;
            lastHeartRateBpm = data.heart_rate_bpm.Value;
        }
    }

    // =========================================================================
    // BUTTON ACTIONS
    // =========================================================================

    private void OnConnectClicked()
    {
        if (webSocketClient != null)
            webSocketClient.Connect();
    }

    private void OnProceedClicked()
    {
        // Hide connection panel
        if (panelObj != null)
            panelObj.SetActive(false);

        // Show trial starter
        if (trialStarterUI != null)
            trialStarterUI.ShowStartPanel();

        UnityEngine.Debug.Log("[BluetoothUI] Devices confirmed — proceeding to trial setup.");
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Show the connection panel again (e.g. between blocks to re-verify)
    /// </summary>
    public void ShowConnectionPanel()
    {
        if (panelObj != null)
            panelObj.SetActive(true);
    }

    /// <summary>
    /// Check if all required devices are currently active
    /// </summary>
    public bool AreDevicesReady()
    {
        bool wsConnected = webSocketClient != null && webSocketClient.IsConnected();
        float now = Time.time;
        bool bikeOk = !requireBike || (bikeDataReceived && (now - lastBikeDataTime < dataTimeoutSeconds));
        bool hrOk = !requireHR || (hrDataReceived && (now - lastHRDataTime < dataTimeoutSeconds));
        return wsConnected && bikeOk && hrOk;
    }

    /// <summary>
    /// Returns true if bike trainer data is actively being received
    /// </summary>
    public bool IsBikeConnected()
    {
        return bikeDataReceived && (Time.time - lastBikeDataTime < dataTimeoutSeconds);
    }

    /// <summary>
    /// Returns true if HR monitor data is actively being received
    /// </summary>
    public bool IsHRConnected()
    {
        return hrDataReceived && (Time.time - lastHRDataTime < dataTimeoutSeconds);
    }
}
