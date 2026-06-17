using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;


/// <summary>
/// Main experiment controller for the hill climb effort-based decision-making study.
/// 
/// Two groups:
///   Group 1 (Reward): Coins visible during hills
///   Group 2 (Control): Same hills, no coins
/// 
/// 8 conditions (randomized): 2%/5%/8%/10% × Steady/Rolling
/// 
/// Each condition:
///   1. Flat approach (100m) with hill cue
///   2. Hill climb (500m) with persistent Pain VAS + coins (Group 1)
///   3. Quit detection (10s no pedaling = abandoned)
///   4. End: Reward VAS → next condition
/// </summary>
public class HillClimbExperiment : MonoBehaviour
{
    [Header("Group Assignment")]
    [SerializeField] public ExperimentGroup group = ExperimentGroup.Reward;
    public enum ExperimentGroup { Reward, Control }
    
    [Header("References")]
    [SerializeField] private CurvedRouteGenerator routeGenerator;
    [SerializeField] private BikeController bikeController;
    [SerializeField] private PersistentPainVAS painVAS;
    [SerializeField] private VASScale rewardVAS;
    
    [Header("Timing")]
    [SerializeField] private float flatApproachLength = 50f;   // 50m flat start
    [SerializeField] private float hillLength = 450f;           // 450m hill = 500m total
    [SerializeField] private float quitTimeThreshold = 10f;
    [SerializeField] private float quitSpeedThreshold = 0.5f;
    
    [Header("Coins (Group 1 only)")]
    [SerializeField] private int baseCoinsPerHill = 3;
    [SerializeField] private float coinSpacing = 50f;
    
    [Header("Participant")]
    [SerializeField] private string participantID = "P001";
    [SerializeField] private int blockNumber = 1;
    
    // Conditions — editable in Inspector. Leave empty to use defaults.
    [Header("Conditions (leave empty to use defaults)")]
    [SerializeField] private List<HillCondition> conditions = new List<HillCondition>();
    private List<int> conditionOrder = new List<int>();
    private int currentConditionIndex = 0;
    // Insert MC Code for Agent Bridge
    [SerializeField] private MonoBehaviour mcpAdapter;

    // State machine
    private enum Phase { Idle, HillCue, Countdown, HillClimb, Quitting, Completed, Abandoned, RewardQuestion, RewardRating, BlockEnd }
    private Phase currentPhase = Phase.Idle;
    
    private float phaseStartTime;
    private float phaseStartDistance;
    private float noSpeedTimer = 0f; // tracks how long speed has been zero
    private bool rewardRatingConfirmed = false;
    private float rewardConfirmTime = 0f;
    private int rewardResponseCountAtStart = 0;
    private int coinsCollected = 0;
    private int totalCoinsCollected = 0;
    private bool experimentRunning = false;
    
    // UI
    private GameObject cuePanel;
    private TextMeshProUGUI cueText;
    private GameObject completionPanel;
    private TextMeshProUGUI completionText;
    private TextMeshProUGUI coinCounterText;
    private Canvas uiCanvas;
    private GameObject cueBgPanel; // Full-screen black overlay for EEG-clean cue display
    
    // Coin objects
    private List<GameObject> activeCoinObjects = new List<GameObject>();
    
    // Data logging
    private string logFilePath;
    private System.IO.StreamWriter trialLogWriter;
    private List<ConditionResult> results = new List<ConditionResult>();

    [System.Serializable]
    public class HillCondition
    {
        [Header("Identity")]
        public string name = "Condition";
        
        [Header("Gradient Profile")]
        [Tooltip("Average gradient across the hill section (%)")]
        [Range(0f, 20f)]
        public float averageGradient = 5f;
        
        [Tooltip("Rolling = gradient oscillates around average. Steady = near-constant gradient.")]
        public bool isRolling = false;
        
        [Tooltip("For Rolling: how much gradient varies around the average (%). E.g. 2 = ±2% variation")]
        [Range(0f, 5f)]
        public float gradientVariation = 1.5f;
        
        [Tooltip("For Steady: tiny noise amount (%). Keep very small (0.1-0.3) for near-constant grade.")]
        [Range(0f, 1f)]
        public float steadyNoise = 0.2f;
        
        [Header("Route Structure")]
        [Tooltip("Flat section at the start (meters). 0% gradient.")]
        [Range(0f, 200f)]
        public float flatApproachMeters = 50f;
        
        [Tooltip("Hill section length (meters). Total = flat + hill.")]
        [Range(100f, 2000f)]
        public float hillLengthMeters = 450f;
        
        [Header("Elevation")]
        [Tooltip("Total elevation gain on the hill (meters). Overrides averageGradient if > 0.")]
        [Range(0f, 200f)]
        public float totalElevationGain = 0f;
        
        [Tooltip("Starting elevation (meters above terrain base)")]
        [Range(5f, 100f)]
        public float startElevation = 20f;
        
        [Header("Reproducibility")]
        [Tooltip("Fixed seed — same terrain every time this condition runs. Change to get different terrain shape.")]
        public int terrainSeed = 1001;
        
        [Header("Reward (Group 2 only)")]
        [Tooltip("Number of coins placed on this hill")]
        [Range(0, 20)]
        public int coinReward = 4;
    }
    
    public class ConditionResult
    {
        public int conditionIndex;
        public string conditionName;
        public float gradient;
        public bool isRolling;
        public bool completed;
        public float duration;
        public float distanceCovered;
        public float avgSpeed;
        public float avgPower;
        public float avgPainVAS;
        public float rewardVAS;
        public int coinsEarned;
        public string quitReason;
    }
    
    private void Start()
    {
        // Insert MC Code for Agent Bridge
        //mcpAdapter.Initialize(Application.dataPath + "/AgentBridge/");
        //mcpAdapter.Connect();

        FindMcpAdapter();
        Debug.Log($"[MC Code for Agent Bridge] MCP Adapter: {mcpAdapter}");

        if (routeGenerator == null) routeGenerator = FindObjectOfType<CurvedRouteGenerator>();
        if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
        if (rewardVAS == null) rewardVAS = FindObjectOfType<VASScale>();
        if (painVAS == null) painVAS = FindObjectOfType<PersistentPainVAS>();
        if (painVAS == null)
        {
            GameObject vasObj = new GameObject("PersistentPainVAS");
            painVAS = vasObj.AddComponent<PersistentPainVAS>();
        }

        ExperimentController oldController = FindObjectOfType<ExperimentController>();
        if (oldController != null) oldController.enabled = false;

        CreateConditions();
        CreateUI();

        // Pre-configure the route generator with the first condition's settings
        // so the initial auto-generated route (generateOnStart) is already correct
        if (conditions.Count > 0 && routeGenerator != null)
        {
            HillCondition firstCond = conditions[0]; // use first condition as preview
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            float totalLen = firstCond.flatApproachMeters + firstCond.hillLengthMeters;

            var routeLenField = typeof(CurvedRouteGenerator).GetField("routeLength", flags);
            if (routeLenField != null) routeLenField.SetValue(routeGenerator, totalLen);

            var seedField = typeof(CurvedRouteGenerator).GetField("seed", flags);
            var useRandomField = typeof(CurvedRouteGenerator).GetField("useRandomSeed", flags);
            if (seedField != null) seedField.SetValue(routeGenerator, firstCond.terrainSeed);
            if (useRandomField != null) useRandomField.SetValue(routeGenerator, false);

            float variation = firstCond.isRolling ? firstCond.gradientVariation : firstCond.steadyNoise;
            routeGenerator.SetTrialGradient(firstCond.averageGradient, variation, firstCond.isRolling, firstCond.flatApproachMeters);

            Debug.Log($"[HillExperiment] Pre-configured route for preview: {firstCond.name}");
        }

        Debug.Log($"[HillExperiment] Ready. {conditions.Count} conditions. Hill={hillLength}m, Flat={flatApproachLength}m");
        Debug.Log($"[HillExperiment] Conditions: {string.Join(", ", conditions.Select(c => c.name))}");
        
        // Ensure HUD elements are visible from the start (speed, power, cadence, etc.)
        // These get hidden by some scripts during setup; force them on.
        EnableHUDElements();
    }
    
    /// <summary>
    /// Enable all cycling HUD elements (speed, power, cadence, distance displays).
    /// Called at startup and when entering HillClimb phase.
    /// </summary>
    private void EnableHUDElements()
    {
        foreach (var obj in FindObjectsOfType<MonoBehaviour>(true))
        {
            string typeName = obj.GetType().Name;
            if (typeName == "ModernCyclingUI" || typeName == "BikeDataUI" || 
                typeName == "CyclingHUD" || typeName == "BikeDataDisplay" ||
                typeName == "EnhancedBikeDataUI" || typeName == "SimpleDataDisplay" ||
                typeName == "ContinuousProgressUI" || typeName == "GradientIndicator")
            {
                obj.gameObject.SetActive(true);
            }
        }
        foreach (var canvas in FindObjectsOfType<Canvas>(true))
        {
            string name = canvas.gameObject.name.ToLower();
            if (name.Contains("hud") || name.Contains("cycling") || name.Contains("bike") || name.Contains("data"))
            {
                canvas.gameObject.SetActive(true);
            }
        }
    }
    // Insert MC Code for Agent Bridge
    private MonoBehaviour FindMcpAdapter()
    {
        if (mcpAdapter) return mcpAdapter;

        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
        {
            if (mb.GetType().Name == "VRCyclingMcpAdapter")
            {
                mcpAdapter = mb;
                break;
            }
        }

        return mcpAdapter;
    }

    private void PublishHillConditionToMcp(HillCondition condition)
    {
        if (condition == null) return;

        var adapter = FindMcpAdapter();
        if (!adapter)
        {
            Debug.LogWarning("[MC Code for Agent Bridge] VRCyclingMcpAdapter not found.");
            return;
        }

        var type = adapter.GetType();
        type.GetField("slopeGradePct")?.SetValue(adapter, condition.averageGradient);
        type.GetField("terrain")?.SetValue(adapter, condition.name);
        type.GetField("terrainMode")?.SetValue(adapter, condition.isRolling ? "rolling" : "steady");
        type.GetField("difficultyLevel")?.SetValue(adapter, GradientToDifficulty(condition.averageGradient));

        Debug.Log($"[MC Code for Agent Bridge] MCP slope={condition.averageGradient}% terrain={condition.name}");
    }

    private void PublishFlatRecoveryToMcp(string reason)
    {
        var adapter = FindMcpAdapter();
        if (!adapter)
        {
            Debug.LogWarning($"[MC Code for Agent Bridge] Cannot reset MCP to flat ({reason}); VRCyclingMcpAdapter not found.");
            return;
        }

        var type = adapter.GetType();
        type.GetField("slopeGradePct")?.SetValue(adapter, 0f);
        type.GetField("terrain")?.SetValue(adapter, reason);
        type.GetField("terrainMode")?.SetValue(adapter, "flat");
        type.GetField("difficultyLevel")?.SetValue(adapter, 1);

        Debug.Log($"[MC Code for Agent Bridge] MCP reset to flat: {reason}");
    }

    private int GradientToDifficulty(float gradient)
    {
        if (gradient >= 8f) return 5;
        if (gradient >= 5f) return 4;
        if (gradient >= 3f) return 3;
        if (gradient >= 1f) return 2;
        return 1;
    }
    private void CreateConditions()
    {
        if (conditions.Count > 0) return;
        ResetToDefaultConditions();
    }
    
    [ContextMenu("Reset Conditions to Defaults")]
    public void ResetToDefaultConditions()
    {
        conditions.Clear();
        conditions.Add(new HillCondition { name = "Flat_0pct",    averageGradient = 0f,  isRolling = false, gradientVariation = 0.1f, steadyNoise = 0.05f, flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 5, terrainSeed = 1000 });
        conditions.Add(new HillCondition { name = "Steady_1pct",  averageGradient = 1f,  isRolling = false, gradientVariation = 0.1f, steadyNoise = 0.05f, flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 5, terrainSeed = 1001 });
        conditions.Add(new HillCondition { name = "Steady_3pct",  averageGradient = 3f,  isRolling = false, gradientVariation = 0.2f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 5, terrainSeed = 1002 });
        conditions.Add(new HillCondition { name = "Steady_5pct",  averageGradient = 5f,  isRolling = false, gradientVariation = 0.2f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 5, terrainSeed = 1003 });
        conditions.Add(new HillCondition { name = "Steady_8pct",  averageGradient = 8f,  isRolling = false, gradientVariation = 0.2f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 5, terrainSeed = 1004 });
        Debug.Log($"[HillExperiment] Reset to {conditions.Count} default conditions");
    }
    
    private void GenerateConditionOrder()
    {
        conditionOrder.Clear();
        for (int i = 0; i < conditions.Count; i++)
            conditionOrder.Add(i);

        // Randomize using participant ID + block number as seed.
        // Same participant in a new block (different blockNumber) gets a different order.
        // Seed is deterministic: re-running the same pid + block always reproduces the same order.
        int participantNum = 0;
        string numStr = System.Text.RegularExpressions.Regex.Replace(participantID, "[^0-9]", "");
        if (!string.IsNullOrEmpty(numStr))
            int.TryParse(numStr, out participantNum);

        int seed = participantNum * 1000 + blockNumber;
        System.Random rng = new System.Random(seed);

        // Fisher-Yates shuffle
        for (int i = conditionOrder.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int temp = conditionOrder[i];
            conditionOrder[i] = conditionOrder[j];
            conditionOrder[j] = temp;
        }

        Debug.Log($"[HillExperiment] Condition order (seed={seed}, pid={participantID}, block={blockNumber}): " +
                  $"{string.Join(", ", conditionOrder.Select(i => conditions[i].name))}");
    }
    
    private void CreateUI()
    {
        GameObject canvasObj = new GameObject("ExperimentUI_Canvas");
        uiCanvas = canvasObj.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 180;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Hill cue panel (center screen)
        cuePanel = CreatePanel(canvasObj.transform, "CuePanel", new Vector2(0.5f, 0.5f), new Vector2(400f, 200f));
        cueText = CreateText(cuePanel.transform, "CueText", 24, Color.white);
        cuePanel.SetActive(false);
        
        // Completion panel
        completionPanel = CreatePanel(canvasObj.transform, "CompletionPanel", new Vector2(0.5f, 0.5f), new Vector2(400f, 150f));
        completionText = CreateText(completionPanel.transform, "CompletionText", 28, Color.white);
        completionPanel.SetActive(false);
        
        // Coin counter (top center, only for Group 1)
        if (group == ExperimentGroup.Reward)
        {
            GameObject coinPanel = CreatePanel(canvasObj.transform, "CoinPanel", new Vector2(0.5f, 1f), new Vector2(150f, 40f));
            RectTransform cpRect = coinPanel.GetComponent<RectTransform>();
            cpRect.anchoredPosition = new Vector2(0f, -60f);
            coinCounterText = CreateText(coinPanel.transform, "CoinText", 20, new Color(1f, 0.85f, 0.2f));
            coinCounterText.text = "🪙 0";
            coinPanel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.6f);
        }
    }
    
    private GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);
        return panel;
    }
    
    private TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        return tmp;
    }

    // === PUBLIC API ===
    
    public void StartBlock(string pid, int block, ExperimentGroup grp)
    {
        // CRITICAL: Ensure game time is running (StartScreenUI may have paused it)
        Time.timeScale = 1f;
        
        participantID = pid;
        blockNumber = block;
        
        // Auto-assign group based on participant number:
        // Odd participant numbers → Reward (coins visible)
        // Even participant numbers → Control (no coins)
        int participantNum = 0;
        // Extract number from participant ID (e.g., "P001" → 1, "P12" → 12, "3" → 3)
        string numStr = System.Text.RegularExpressions.Regex.Replace(pid, "[^0-9]", "");
        if (!string.IsNullOrEmpty(numStr))
            int.TryParse(numStr, out participantNum);
        
        group = (participantNum % 2 != 0) ? ExperimentGroup.Reward : ExperimentGroup.Control;
        Debug.Log($"[HillExperiment] Participant {pid} (number={participantNum}) → Group: {group} (odd=Reward/coins, even=Control)");
        
        GenerateConditionOrder();
        currentConditionIndex = 0;
        totalCoinsCollected = 0;
        results.Clear();
        experimentRunning = true;
        
        // Setup log file
        logFilePath = System.IO.Path.Combine(Application.persistentDataPath,
            $"hill_experiment_{participantID}_B{blockNumber}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        trialLogWriter = new System.IO.StreamWriter(logFilePath, false);
        trialLogWriter.WriteLine("timestamp,condition,gradient,rolling,phase,distance,speed,power,cadence,hr,pain_vas,gradient_current,coins,event");
        trialLogWriter.Flush();
        
        Debug.Log($"\n[HillExperiment] Block {blockNumber} started. Group: {group}, Participant: {participantID}");
        Debug.Log($"[HillExperiment] {conditions.Count} conditions, Log: {logFilePath}");
        
        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("BLOCK_START", $"participant={participantID},block={blockNumber},group={group}");
        
        PublishFlatRecoveryToMcp("block_start");
        StartNextCondition();
    }
    
    private void StartNextCondition()
    {
        if (currentConditionIndex >= conditionOrder.Count)
        {
            finishingCondition = false;
            EndBlock();
            return;
        }
        StartCoroutine(StartNextConditionDelayed());
    }

    private System.Collections.IEnumerator StartNextConditionDelayed()
    {
        int condIdx = conditionOrder[currentConditionIndex];
        HillCondition cond = conditions[condIdx];

        Debug.Log($"\n[HillExperiment] === Condition {currentConditionIndex + 1}/{conditionOrder.Count}: {cond.name} ({cond.averageGradient}%, {(cond.isRolling ? "Rolling" : "Steady")}) ===");

        if (routeGenerator != null)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            float totalLen = cond.flatApproachMeters + cond.hillLengthMeters;

            var routeLenField = typeof(CurvedRouteGenerator).GetField("routeLength", flags);
            if (routeLenField != null) routeLenField.SetValue(routeGenerator, totalLen);

            var seedField = typeof(CurvedRouteGenerator).GetField("seed", flags);
            var useRandomField = typeof(CurvedRouteGenerator).GetField("useRandomSeed", flags);
            if (seedField != null) seedField.SetValue(routeGenerator, cond.terrainSeed);
            if (useRandomField != null) useRandomField.SetValue(routeGenerator, false);

            float variation = cond.isRolling ? cond.gradientVariation : cond.steadyNoise;
            routeGenerator.SetTrialGradient(cond.averageGradient, variation, cond.isRolling, cond.flatApproachMeters);
            routeGenerator.GenerateRoute();

            // Insert MC Code for Agent Bridge
            PublishHillConditionToMcp(cond);
            Debug.Log($"[MC Code for Agent Bridge] Published condition to MCP: {cond.name}");
        }

        // Wait for route generation to complete
        yield return null;
        yield return null;
        yield return null;

        if (bikeController != null)
            bikeController.ResetCourse();

        // Force progress bar to resample with the new elevation profile
        ElevationProgressBar progressBar = FindObjectOfType<ElevationProgressBar>();
        if (progressBar != null)
            progressBar.ResetProgress();

        coinsCollected = 0;
        noSpeedTimer = 0f;
        phaseStartTime = Time.time;
        phaseStartDistance = bikeController != null ? bikeController.GetDistance() : 0f;

        // Reset the guard now — we're about to enter a new phase so FinishCondition can work again
        finishingCondition = false;

        SetPhase(Phase.HillCue);

        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("CONDITION_START", $"condition={cond.name},gradient={cond.averageGradient},rolling={cond.isRolling}");

        Debug.Log($"[HillExperiment] Condition ready: {cond.name}, {cond.averageGradient}%, flat={cond.flatApproachMeters}m, hill={cond.hillLengthMeters}m");
    }
    
    private void SetPhase(Phase newPhase)
    {
        currentPhase = newPhase;
        phaseStartTime = Time.time;
        
        // Hide all panels
        cuePanel.SetActive(false);
        completionPanel.SetActive(false);
        
        // Hide progress bar during non-climb phases
        ElevationProfileBar profileBar = FindObjectOfType<ElevationProfileBar>(true);
        
        switch (newPhase)
        {
            case Phase.HillCue:
                if (profileBar != null) profileBar.Hide();
                ShowHillCue();
                // Lock bike during cue — participant sits still for EEG
                if (bikeController != null) bikeController.LockBike();
                break;
            
            case Phase.Countdown:
                if (profileBar != null) profileBar.Hide();
                if (bikeController != null) bikeController.LockBike();
                // Show countdown on cue panel (no black bg)
                cuePanel.SetActive(true);
                cueText.text = "3";
                if (EventMarkerSender.Instance != null)
                    EventMarkerSender.Instance.SendEvent("COUNTDOWN_START");
                break;
                
            case Phase.HillClimb:
                if (profileBar != null) profileBar.Show();
                StartHillClimb();
                break;
                
            case Phase.Completed:
                if (profileBar != null) profileBar.Hide();
                ShowCompletion(true);
                
                // Insert MC Code for Agent Bridge
                PublishFlatRecoveryToMcp("condition_completed");
                
                break;
                
            case Phase.Abandoned:
                if (profileBar != null) profileBar.Hide();
                ShowCompletion(false);
                
                // Insert MC Code for Agent Bridge
                PublishFlatRecoveryToMcp("condition_abandoned");
                
                break;
            
            case Phase.RewardQuestion:
                if (profileBar != null) profileBar.Hide();
                ShowRewardQuestion();
                break;
                
            case Phase.RewardRating:
                if (profileBar != null) profileBar.Hide();
                rewardRatingConfirmed = false;
                ShowRewardRating();
                // Capture response count AFTER triggering the VAS — this way we only
                // detect genuinely new submissions, not leftover counts from prior conditions
                rewardResponseCountAtStart = rewardVAS != null ? rewardVAS.GetResponseCount() : 0;
                break;
        }
    }
    
    // === UPDATE LOOP ===
    
    private void Update()
    {
        // F7 = Force start experiment (debug shortcut)
        if (!experimentRunning && Input.GetKeyDown(KeyCode.F7))
        {
            Debug.Log("[HillExperiment] F7 pressed — FORCE STARTING BLOCK");
            StartBlock("TEST", 1, ExperimentGroup.Reward);
            return;
        }

        if (!experimentRunning) return;
        
        float dist = bikeController != null ? bikeController.GetDistance() : 0f;
        float speed = bikeController != null ? bikeController.GetSpeedKmh() : 0f;
        
        switch (currentPhase)
        {
            case Phase.HillCue:
                // Show difficulty cue for 10 seconds (EEG time-locked window)
                // Bike is locked — participant sits still
                float cueElapsed = Time.time - phaseStartTime;
                if (Time.frameCount % 60 == 0)
                    Debug.Log($"[HillCue] Waiting... {cueElapsed:F1}s / 10s");
                
                if (cueElapsed > 10f)
                {
                    if (cueBgPanel != null) cueBgPanel.SetActive(false);
                    cuePanel.SetActive(false);
                    Debug.Log("[HillExperiment] HillCue complete → Countdown");
                    SetPhase(Phase.Countdown);
                }
                break;
            
            case Phase.Countdown:
                // 3-2-1-GO countdown before cycling starts
                float countdownElapsed = Time.time - phaseStartTime;
                if (countdownElapsed < 1f)
                    cueText.text = "3";
                else if (countdownElapsed < 2f)
                    cueText.text = "2";
                else if (countdownElapsed < 3f)
                    cueText.text = "1";
                else if (countdownElapsed < 4f)
                    cueText.text = "GO!";
                else
                {
                    cuePanel.SetActive(false);
                    SetPhase(Phase.HillClimb);
                }
                break;
                
            case Phase.HillClimb:
                UpdateHillClimb(dist, speed);
                break;
                
            case Phase.Completed:
            case Phase.Abandoned:
                // POST-CLIMB REST PERIOD: 5 seconds for pain rating
                // Pain VAS still visible so participants rate residual pain
                float restElapsed = Time.time - phaseStartTime;
                
                // Update countdown timer on screen
                int remainingRest = Mathf.CeilToInt(5f - restElapsed);
                if (completionPanel.activeSelf && completionText != null)
                {
                    int condIdx2 = conditionOrder[currentConditionIndex];
                    HillCondition cond2 = conditions[condIdx2];
                    bool wasCompleted = (currentPhase == Phase.Completed);
                    
                    string timerMsg;
                    if (wasCompleted)
                    {
                        timerMsg = "<size=36><color=#4CAF50>✓ Hill Complete!</color></size>";
                        if (group == ExperimentGroup.Reward && coinsCollected > 0)
                            timerMsg += $"\n<size=24><color=#FFD700>Coins earned: {coinsCollected} 🪙</color></size>";
                    }
                    else
                    {
                        timerMsg = "<size=36><color=#FF5555>Hill Incomplete</color></size>";
                    }
                    
                    timerMsg += "\n\n<size=22><color=#CCCCCC>Rate your pain.</color></size>" +
                               $"\n\n<size=20><color=#AAAAAA>{remainingRest}s</color></size>";
                    
                    completionText.text = timerMsg;
                }
                
                // Send EEG marker at start of rest
                if (restElapsed < 0.1f)
                {
                    if (EventMarkerSender.Instance != null)
                        EventMarkerSender.Instance.SendEvent("POST_CLIMB_REST_START", 
                            $"condition={currentConditionIndex + 1},completed={currentPhase == Phase.Completed}");
                }
                
                // After 5s rest, proceed to reward question
                if (restElapsed > 5f)
                {
                    if (EventMarkerSender.Instance != null)
                        EventMarkerSender.Instance.SendEvent("POST_CLIMB_REST_END", 
                            $"final_pain_vas={(painVAS != null ? painVAS.GetCurrentValue() : -1f):F1}");
                    completionPanel.SetActive(false);
                    SetPhase(Phase.RewardQuestion);
                }
                break;
            
            case Phase.RewardQuestion:
                // Show "How rewarding was this trial?" for 5 seconds (EEG deliberation window)
                if (Time.time - phaseStartTime > 5f)
                    SetPhase(Phase.RewardRating);
                break;
                
            case Phase.RewardRating:
                // Detect reward submission by counting VAS responses
                // F6 = force skip for experimenter
                if (Input.GetKeyDown(KeyCode.F6))
                {
                    FinishCondition();
                }
                else if (rewardVAS == null)
                {
                    // No VAS system — auto-advance after 3 seconds (not instant)
                    if (Time.time - phaseStartTime > 3f)
                    {
                        Debug.Log("[HillExperiment] No reward VAS — auto-advancing after 3s");
                        FinishCondition();
                    }
                }
                else if (Time.time - phaseStartTime > 0.5f && rewardVAS.GetResponseCount() > rewardResponseCountAtStart)
                {
                    // Participant submitted a rating — wait 2s then advance
                    // (0.5s grace period prevents detecting stale responses from prior condition)
                    if (!rewardRatingConfirmed)
                    {
                        rewardRatingConfirmed = true;
                        rewardConfirmTime = Time.time;
                        Debug.Log($"[HillExperiment] Reward rating submitted (count: {rewardVAS.GetResponseCount()}, expected > {rewardResponseCountAtStart})");
                    }
                    if (Time.time - rewardConfirmTime > 2f)
                    {
                        FinishCondition();
                    }
                }
                else if (Time.time - phaseStartTime > 60f)
                {
                    // 60s timeout fallback
                    Debug.Log("[HillExperiment] Reward timeout (60s)");
                    FinishCondition();
                }
                break;
        }
        
        // Log data continuously during hill climb
        if (currentPhase == Phase.HillClimb)
        {
            LogDataPoint("");
        }
        
        // Animate coins: spin and float up/down
        foreach (GameObject coin in activeCoinObjects)
        {
            if (coin != null && coin.activeSelf)
            {
                // Spin around local Y axis (the thin edge of the disc spins like a real coin)
                coin.transform.Rotate(0f, 200f * Time.deltaTime, 0f, Space.Self);
                // Bob up and down gently
                Vector3 pos = coin.transform.position;
                pos.y += Mathf.Sin(Time.time * 2.5f + coin.GetInstanceID()) * 0.003f;
                coin.transform.position = pos;
            }
        }
    }
    
    // === PHASE HANDLERS ===
    
    private void ShowHillCue()
    {
        int condIdx = conditionOrder[currentConditionIndex];
        HillCondition cond = conditions[condIdx];

        float elevGain = (cond.averageGradient / 100f) * cond.hillLengthMeters;

        // Create full-screen black overlay for clean EEG recording
        if (cueBgPanel == null)
        {
            Transform parent = cuePanel.transform.parent;
            cueBgPanel = new GameObject("CueBgPanel");
            cueBgPanel.transform.SetParent(parent, false);
            RectTransform bgrt = cueBgPanel.AddComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero;
            bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero;
            bgrt.offsetMax = Vector2.zero;
            Image bgImg = cueBgPanel.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.02f, 0.05f, 1f);
        }
        cueBgPanel.SetActive(true);
        cueBgPanel.transform.SetAsLastSibling();

        // Make cue panel full-screen
        RectTransform cueRect = cuePanel.GetComponent<RectTransform>();
        cueRect.anchorMin = new Vector2(0.05f, 0.05f);
        cueRect.anchorMax = new Vector2(0.95f, 0.95f);
        cueRect.offsetMin = Vector2.zero;
        cueRect.offsetMax = Vector2.zero;
        cuePanel.GetComponent<Image>().color = Color.clear;

        // Clear old content
        foreach (Transform child in cuePanel.transform)
        {
            if (child.gameObject.name != "CueText")
                Destroy(child.gameObject);
        }

        // --- Difficulty color based on gradient ---
        Color difficultyColor = GetDifficultyColor(cond.averageGradient);
        Color dimColor = difficultyColor * 0.4f;
        dimColor.a = 1f;

        // --- Build visual cue layout ---
        // 1. Title: "NEXT HILL" with condition counter
        string titleStr = $"<size=22><color=#888888>CONDITION {currentConditionIndex + 1} / {conditionOrder.Count}</color></size>";

        // 2. Hill silhouette description (gradient as visual angle)
        float difficultyNorm = Mathf.Clamp01(cond.averageGradient / 12f); // 0-12% mapped to 0-1
        string difficultyLabel = GetDifficultyLabel(cond.averageGradient);
        string gradientColorHex = ColorUtility.ToHtmlStringRGB(difficultyColor);

        // 3. Build the visual hill representation using Unicode block characters
        string hillVisual = BuildHillVisualization(cond.averageGradient, cond.isRolling);

        // 4. Difficulty bar: filled proportional to gradient
        string diffBar = BuildDifficultyBar(difficultyNorm, gradientColorHex);

        // 5. Compose full message
        string cueMsg = $"{titleStr}\n\n" +
                        $"<size=48><color=#{gradientColorHex}>{cond.averageGradient}%</color></size>\n" +
                        $"<size=28><color=#{gradientColorHex}>{difficultyLabel}</color></size>\n\n" +
                        $"{hillVisual}\n\n" +
                        $"{diffBar}\n\n" +
                        $"<size=20>" +
                        $"<color=#CCCCCC>Profile:</color> <color=#FFFFFF>{(cond.isRolling ? "Rolling ↝" : "Steady →")}</color>    " +
                        $"<color=#CCCCCC>Distance:</color> <color=#FFFFFF>{cond.hillLengthMeters:F0}m</color>    " +
                        $"<color=#CCCCCC>Climb:</color> <color=#FFFFFF>+{elevGain:F0}m</color></size>";

        if (group == ExperimentGroup.Reward)
        {
            cueMsg += $"\n\n<size=26><color=#FFD700>🪙 × 5</color></size>";
        }

        cueText.text = cueMsg;
        cueText.fontSize = 28;
        cueText.richText = true;
        cuePanel.SetActive(true);
        cuePanel.transform.SetAsLastSibling();

        // === EEG MARKER: CUE_ONSET ===
        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("CUE_ONSET", 
                $"condition={currentConditionIndex + 1},gradient={cond.averageGradient},rolling={cond.isRolling},name={cond.name}");
        Debug.Log($"[EEG] CUE_ONSET — Condition {currentConditionIndex + 1}: {cond.name} ({cond.averageGradient}%)");
    }

    /// <summary>
    /// Get color representing difficulty: green (easy) → yellow → orange → red (hard)
    /// </summary>
    private Color GetDifficultyColor(float gradient)
    {
        if (gradient <= 0f) return new Color(0.3f, 0.8f, 0.3f);      // Green - flat
        if (gradient <= 2f) return new Color(0.4f, 0.85f, 0.3f);     // Light green
        if (gradient <= 4f) return new Color(0.9f, 0.85f, 0.2f);     // Yellow
        if (gradient <= 6f) return new Color(1f, 0.6f, 0.15f);       // Orange
        if (gradient <= 8f) return new Color(1f, 0.35f, 0.1f);       // Dark orange
        if (gradient <= 10f) return new Color(0.9f, 0.15f, 0.1f);    // Red
        return new Color(0.8f, 0.05f, 0.2f);                          // Deep red
    }

    /// <summary>
    /// Human-readable difficulty label
    /// </summary>
    private string GetDifficultyLabel(float gradient)
    {
        if (gradient <= 0f) return "FLAT";
        if (gradient <= 2f) return "EASY";
        if (gradient <= 4f) return "MODERATE";
        if (gradient <= 6f) return "CHALLENGING";
        if (gradient <= 8f) return "HARD";
        if (gradient <= 10f) return "VERY HARD";
        return "EXTREME";
    }

    /// <summary>
    /// Build an ASCII/Unicode hill visualization showing the climb angle.
    /// Taller hill = steeper gradient. Rolling shows waves.
    /// </summary>
    private string BuildHillVisualization(float gradient, bool isRolling)
    {
        // Height of the visual hill (number of rows) scales with gradient
        int hillHeight = Mathf.Clamp(Mathf.RoundToInt(gradient * 0.8f), 1, 8);
        int hillWidth = 20;
        
        string colorHex = ColorUtility.ToHtmlStringRGB(GetDifficultyColor(gradient));
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("<mspace=0.6em>");
        
        if (isRolling)
        {
            // Rolling: wave pattern
            string[] wave = new string[hillHeight + 1];
            for (int row = hillHeight; row >= 0; row--)
            {
                string line = "";
                for (int col = 0; col < hillWidth; col++)
                {
                    float t = (float)col / hillWidth;
                    float waveH = (Mathf.Sin(t * Mathf.PI * 4f) * 0.3f + 0.7f) * hillHeight;
                    if (row <= waveH)
                        line += "█";
                    else
                        line += " ";
                }
                sb.Append($"<color=#{colorHex}>{line}</color>\n");
            }
        }
        else
        {
            // Steady: clean ramp from left to right
            for (int row = hillHeight; row >= 0; row--)
            {
                string line = "";
                for (int col = 0; col < hillWidth; col++)
                {
                    float rampH = ((float)col / hillWidth) * hillHeight;
                    if (row <= rampH)
                        line += "█";
                    else
                        line += " ";
                }
                sb.Append($"<color=#{colorHex}>{line}</color>\n");
            }
        }
        
        // Ground line
        sb.Append($"<color=#555555>{new string('▔', hillWidth)}</color>");
        sb.Append("</mspace>");
        
        return sb.ToString();
    }

    /// <summary>
    /// Build a horizontal difficulty bar (filled blocks)
    /// </summary>
    private string BuildDifficultyBar(float fillNorm, string colorHex)
    {
        int totalBlocks = 12;
        int filledBlocks = Mathf.Clamp(Mathf.RoundToInt(fillNorm * totalBlocks), 1, totalBlocks);
        
        string filled = new string('■', filledBlocks);
        string empty = new string('□', totalBlocks - filledBlocks);
        
        return $"<size=24>DIFFICULTY  <color=#{colorHex}>{filled}</color><color=#444444>{empty}</color></size>";
    }

    private GameObject climbSign;

    private void SpawnClimbSign(HillCondition cond)
    {
        if (climbSign != null) Destroy(climbSign);

        // Find position at 50m along the spline
        SplinePath spline = FindObjectOfType<SplinePath>();
        Terrain terrain = FindObjectOfType<Terrain>();

        Vector3 signPos = Vector3.zero;
        if (spline != null && spline.GetTotalLength() > 0f)
        {
            SplinePath.SplineSample s = spline.SampleAtDistance(cond.flatApproachMeters);
            signPos = s.position;
            if (terrain != null) signPos.y = terrain.SampleHeight(signPos) + 0.5f;
        }
        else if (bikeController != null)
        {
            signPos = bikeController.transform.position + bikeController.transform.forward * cond.flatApproachMeters;
        }

        if (signPos == Vector3.zero) return;

        // Create sign post
        climbSign = new GameObject("ClimbSign");
        climbSign.transform.position = signPos;

        // Post
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.transform.SetParent(climbSign.transform);
        post.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        post.transform.localScale = new Vector3(0.1f, 1.5f, 0.1f);
        Collider pc = post.GetComponent<Collider>();
        if (pc != null) Destroy(pc);

        // Sign board
        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.transform.SetParent(climbSign.transform);
        board.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        board.transform.localScale = new Vector3(2.5f, 0.8f, 0.1f);
        Collider bc2 = board.GetComponent<Collider>();
        if (bc2 != null) Destroy(bc2);

        // Color the sign
        GameObject tempPrim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Material signMat = new Material(tempPrim.GetComponent<Renderer>().sharedMaterial);
        Destroy(tempPrim);
        if (signMat.HasProperty("_BaseColor")) signMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.1f));
        if (signMat.HasProperty("_Color")) signMat.SetColor("_Color", new Color(1f, 0.85f, 0.1f));
        board.GetComponent<Renderer>().material = signMat;

        // Face the road direction
        if (spline != null && spline.GetTotalLength() > 0f)
        {
            SplinePath.SplineSample s = spline.SampleAtDistance(cond.flatApproachMeters);
            if (s.forward.sqrMagnitude > 0.01f)
                climbSign.transform.rotation = Quaternion.LookRotation(s.forward, Vector3.up);
        }
    }
    
    private void StartHillClimb()
    {
        int condIdx = conditionOrder[currentConditionIndex];
        HillCondition cond = conditions[condIdx];
        // Insert MC Code for Agent Bridge
        PublishHillConditionToMcp(cond);
        Debug.Log($"[MC Code for Agent Bridge] MCP hill climb started:: {cond.name}");

        hillLength = cond.hillLengthMeters;
        flatApproachLength = cond.flatApproachMeters;

        // CRITICAL: Hide all overlay panels so the cycling scene is visible
        if (cueBgPanel != null) cueBgPanel.SetActive(false);
        cuePanel.SetActive(false);
        completionPanel.SetActive(false);

        // Unlock the bike for riding
        if (bikeController != null) bikeController.UnlockBike();

        // Reset the bike course NOW (not in a delayed coroutine)
        if (bikeController != null)
        {
            bikeController.ResetCourse();
            // Total rideable distance = flat approach + hill
            float totalDistance = flatApproachLength + hillLength;
            bikeController.SetMaxCourseDistance(totalDistance);
        }

        // Set phase start distance AFTER reset (should be 0)
        phaseStartDistance = bikeController != null ? bikeController.GetDistance() : 0f;
        noSpeedTimer = 0f;

        // Re-enable all HUD elements for the cycling phase
        EnableHUDElements();

        Debug.Log($"[HillExperiment] === HILL CLIMB STARTED === {cond.name}, {cond.averageGradient}%, " +
                  $"distance={flatApproachLength + hillLength}m, experimentRunning={experimentRunning}");

        if (painVAS != null) painVAS.Show();

        if (group == ExperimentGroup.Reward)
            SpawnCoins(cond.coinReward);

        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("HILL_START", $"gradient={cond.averageGradient},rolling={cond.isRolling}");
        
        // Reset EEG epoch tracking for this climb
        lastEpochMarkerTime = 0f;
        epochCount = 0;
    }

    private List<GameObject> activeSignObjects = new List<GameObject>();

    private void SpawnRoadsideSigns(HillCondition cond)
    {
        // Clear old signs
        foreach (var s in activeSignObjects) if (s != null) Destroy(s);
        activeSignObjects.Clear();

        SplinePath spline = FindObjectOfType<SplinePath>();
        Terrain terrain = FindObjectOfType<Terrain>();

        // Sign 1: "START PEDALLING" at 0m (start of flat)
        SpawnSign(0f, "START\nPEDALLING", new Color(0.2f, 0.7f, 0.2f), spline, terrain, true);

        // Sign 2: "CLIMB BEGINS" at 50m (start of hill) with condition info
        float elevGain = (cond.averageGradient / 100f) * cond.hillLengthMeters;
        string climbInfo = $"CLIMB\n{cond.averageGradient}% avg\n{cond.hillLengthMeters:F0}m\n+{elevGain:F0}m";
        SpawnSign(cond.flatApproachMeters, climbInfo, new Color(1f, 0.6f, 0.1f), spline, terrain, true);

        // Sign 3: "FINISH" at end of hill
        SpawnSign(cond.flatApproachMeters + cond.hillLengthMeters - 5f, "FINISH", new Color(0.2f, 0.5f, 1f), spline, terrain, false);
    }

    private void SpawnSign(float distanceAlongRoute, string text, Color color, SplinePath spline, Terrain terrain, bool leftSide)
    {
        Vector3 signPos = Vector3.zero;
        Vector3 signForward = Vector3.right;
        Vector3 signRight = Vector3.forward;

        if (spline != null && spline.GetTotalLength() > 0f)
        {
            float clampedDist = Mathf.Clamp(distanceAlongRoute, 0f, spline.GetTotalLength() - 1f);
            SplinePath.SplineSample s = spline.SampleAtDistance(clampedDist);
            signPos = s.position;
            signForward = s.forward;
            signRight = s.right;
        }
        else if (bikeController != null)
        {
            signPos = bikeController.transform.position + bikeController.transform.forward * distanceAlongRoute;
            signForward = bikeController.transform.forward;
            signRight = bikeController.transform.right;
        }

        if (terrain != null) signPos.y = terrain.SampleHeight(signPos) + 0.1f;

        // Offset to side of road (far enough to not block the cyclist)
        float sideOffset = 12f;
        signPos += signRight * (leftSide ? -sideOffset : sideOffset);

        GameObject sign = new GameObject($"RoadsideSign_{distanceAlongRoute:F0}m");
        sign.transform.position = signPos;
        sign.transform.rotation = Quaternion.LookRotation(signForward, Vector3.up);

        // Post
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.transform.SetParent(sign.transform);
        post.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        post.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
        Collider pc = post.GetComponent<Collider>();
        if (pc != null) Destroy(pc);

        // Board
        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.transform.SetParent(sign.transform);
        board.transform.localPosition = new Vector3(0f, 2.8f, 0f);

        // Size based on text length
        int lines = text.Split('\n').Length;
        board.transform.localScale = new Vector3(1.8f, 0.35f * lines, 0.08f);
        Collider bc = board.GetComponent<Collider>();
        if (bc != null) Destroy(bc);

        // Material
        GameObject tempPrim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Material mat = new Material(tempPrim.GetComponent<Renderer>().sharedMaterial);
        Destroy(tempPrim);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        board.GetComponent<Renderer>().material = mat;

        // Post material (white)
        Material postMat = new Material(mat);
        if (postMat.HasProperty("_BaseColor")) postMat.SetColor("_BaseColor", Color.white);
        if (postMat.HasProperty("_Color")) postMat.SetColor("_Color", Color.white);
        post.GetComponent<Renderer>().material = postMat;

        activeSignObjects.Add(sign);
    }

    private System.Collections.IEnumerator SetCourseLimitDelayed()
    {
        yield return null;
        yield return null;
        yield return null;

        if (bikeController != null)
        {
            // Set the max distance for the full condition (flat + hill)
            bikeController.SetMaxCourseDistance(flatApproachLength + hillLength);
        }

        // Also update elevation bar
        ElevationProgressBar bar = FindObjectOfType<ElevationProgressBar>();
        if (bar != null) bar.SetTotalRouteLength(hillLength);
    }
    
    // EEG epoch tracking
    private float lastEpochMarkerTime = 0f;
    private float eegEpochInterval = 30f; // Send EEG epoch marker every 30 seconds
    private int epochCount = 0;
    
    private void UpdateHillClimb(float dist, float speed)
    {
        float hillDist = dist - phaseStartDistance;
        float climbElapsed = Time.time - phaseStartTime;
        
        // === EEG: Periodic epoch markers for spectral analysis (every 30s) ===
        if (climbElapsed - lastEpochMarkerTime >= eegEpochInterval)
        {
            epochCount++;
            lastEpochMarkerTime = climbElapsed;
            
            float currentPainVAS = painVAS != null ? painVAS.GetCurrentValue() : -1f;
            float currentGradient = bikeController != null ? bikeController.GetGradient() : 0f;
            
            if (EventMarkerSender.Instance != null)
                EventMarkerSender.Instance.SendEvent("EEG_EPOCH", 
                    $"epoch={epochCount},elapsed={climbElapsed:F0}s,dist={hillDist:F0}m," +
                    $"pain_vas={currentPainVAS:F1},gradient={currentGradient:F1},speed={speed:F1}");
            
            Debug.Log($"[EEG] EPOCH {epochCount} — {climbElapsed:F0}s, pain={currentPainVAS:F1}, grade={currentGradient:F1}%");
        }
        
        // Debug: log on first frame of hill climb
        if (Time.frameCount % 60 == 0)
            Debug.Log($"[HillClimb] dist={dist:F1}, phaseStart={phaseStartDistance:F1}, hillDist={hillDist:F1}, hillLength={hillLength:F1}");
        
        // Quit detection: 5s no pedaling triggers a 10s countdown, then abandon
        // SKIP entirely in simulation mode
        // SKIP during flat approach (rider may not have started pedalling yet)
        bool pastFlatApproach = hillDist > flatApproachLength;
        if (simulationModeActive)
        {
            noSpeedTimer = 0f; // Never accumulate in sim mode
        }
        else if (!pastFlatApproach)
        {
            noSpeedTimer = 0f; // Don't penalise during flat lead-in
        }
        else if (speed < quitSpeedThreshold)
        {
            noSpeedTimer += Time.deltaTime;
            
            // After 5s of no pedaling, show 10s countdown
            if (noSpeedTimer >= 5f)
            {
                float countdownRemaining = 15f - noSpeedTimer; // 5s grace + 10s countdown = 15s total
                
                // Send EEG marker at the start of the countdown (once)
                if (noSpeedTimer >= 5f && noSpeedTimer - Time.deltaTime < 5f)
                {
                    if (EventMarkerSender.Instance != null)
                        EventMarkerSender.Instance.SendEvent("DISENGAGE_ONSET", 
                            $"condition={currentConditionIndex + 1}");
                    Debug.Log("[EEG] DISENGAGE_ONSET — 10s countdown started");
                }
                
                if (countdownRemaining > 0f)
                {
                    cuePanel.SetActive(true);
                    cueText.text = $"⚠ Resume pedaling!\n\nTrial ends in: {Mathf.CeilToInt(countdownRemaining)}s";
                }
                
                if (noSpeedTimer >= 15f) // 5s grace + 10s countdown
                {
                    cuePanel.SetActive(false);
                    Debug.Log($"[HillExperiment] QUIT — no pedaling for 15s");
                    if (EventMarkerSender.Instance != null)
                        EventMarkerSender.Instance.SendEvent("DISENGAGE_QUIT", 
                            $"condition={currentConditionIndex + 1},no_speed_duration={noSpeedTimer:F1}");
                    Debug.Log("[EEG] DISENGAGE_QUIT");
                    SetPhase(Phase.Abandoned);
                    return;
                }
            }
        }
        else
        {
            // Resumed pedaling — reset timer and hide countdown
            if (noSpeedTimer >= 5f)
            {
                cuePanel.SetActive(false);
                if (EventMarkerSender.Instance != null)
                    EventMarkerSender.Instance.SendEvent("DISENGAGE_RESUME", 
                        $"condition={currentConditionIndex + 1}");
                Debug.Log("[EEG] DISENGAGE_RESUME — participant resumed pedaling");
            }
            noSpeedTimer = 0f;
        }
        
        // Check coin collection (Group 1)
        if (group == ExperimentGroup.Reward)
            CheckCoinCollection(hillDist);
        
        // Check hill completion
        if (hillDist >= hillLength)
        {
            Debug.Log($"[HillExperiment] Hill COMPLETED at {hillDist:F0}m");
            if (EventMarkerSender.Instance != null)
                EventMarkerSender.Instance.SendEvent("HILL_COMPLETE", $"distance={hillDist:F0}");
            SetPhase(Phase.Completed);
        }
    }

    // Helper to check if we're in simulation (don't trigger quit in sim mode)
    private bool simulationModeActive
    {
        get { return bikeController != null && bikeController.IsSimulationMode(); }
    }
    
    private void ShowCompletion(bool completed)
    {
        // Keep pain VAS visible for residual pain rating during rest
        if (painVAS != null) painVAS.Show();
        
        // Hide the black cue background if it was showing
        if (cueBgPanel != null) cueBgPanel.SetActive(false);
        
        // Show completion message with rest instructions
        string msg;
        if (completed)
        {
            msg = "<size=36><color=#4CAF50>✓ Hill Complete!</color></size>";
            if (group == ExperimentGroup.Reward && coinsCollected > 0)
                msg += $"\n<size=24><color=#FFD700>Coins earned: {coinsCollected} 🪙</color></size>";
        }
        else
        {
            msg = "<size=36><color=#FF5555>Hill Incomplete</color></size>";
        }
        
        msg += "\n\n<size=22><color=#CCCCCC>Please remain still.</color></size>" +
               "\n<size=22><color=#CCCCCC>Continue rating your pain.</color></size>" +
               "\n\n<size=18><color=#888888>Rest period: 30 seconds</color></size>";
        
        completionText.text = msg;
        completionText.richText = true;
        completionText.color = Color.white;
        completionPanel.SetActive(true);
        
        // Set resistance to zero so bike freewheels
        if (bikeController != null)
            bikeController.SetMaxCourseDistance(0f);
    }
    
    private void ShowRewardQuestion()
    {
        completionPanel.SetActive(false);
        
        // Show black background with question text (EEG deliberation window)
        if (cueBgPanel == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            cueBgPanel = new GameObject("CueBgPanel");
            cueBgPanel.transform.SetParent(canvas != null ? canvas.transform : cuePanel.transform.parent, false);
            RectTransform bgrt = cueBgPanel.AddComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero;
            bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero;
            bgrt.offsetMax = Vector2.zero;
            Image bgImg = cueBgPanel.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.95f);
        }
        cueBgPanel.SetActive(true);
        cueBgPanel.transform.SetAsLastSibling();
        
        cueText.text = "How rewarding was this trial?\n\n(Think about your answer...)";
        cuePanel.SetActive(true);
        cuePanel.transform.SetAsLastSibling();
        
        // === EEG MARKER: REWARD_QUESTION_ONSET ===
        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("REWARD_QUESTION_ONSET", 
                $"condition={currentConditionIndex + 1}");
        Debug.Log("[EEG] REWARD_QUESTION_ONSET — 5s deliberation window");
    }
    
    private void ShowRewardRating()
    {
        // Hide question overlay, show rating slider
        if (cueBgPanel != null) cueBgPanel.SetActive(false);
        cuePanel.SetActive(false);
        completionPanel.SetActive(false);
        
        // === EEG MARKER: REWARD_SLIDER_ONSET ===
        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("REWARD_SLIDER_ONSET", 
                $"condition={currentConditionIndex + 1}");
        Debug.Log("[EEG] REWARD_SLIDER_ONSET — rating scale active");
        
        if (rewardVAS != null)
            rewardVAS.TriggerVASNow("Reward");
    }
    
    private bool finishingCondition = false; // Guard against double-calling
    
    private void FinishCondition()
    {
        // Prevent being called multiple times for the same condition
        if (finishingCondition) return;
        finishingCondition = true;
        
        // CRITICAL: Set phase to Idle immediately so the Update loop doesn't
        // re-trigger reward/completion logic during the coroutine wait frames
        currentPhase = Phase.Idle;
        
        int condIdx = conditionOrder[currentConditionIndex];
        HillCondition cond = conditions[condIdx];
        
        float duration = Time.time - phaseStartTime;
        bool completed = currentPhase == Phase.RewardRating && results.Count > 0 ? 
            results[results.Count - 1].completed : (currentPhase != Phase.Abandoned);
        
        // Record result
        ConditionResult result = new ConditionResult
        {
            conditionIndex = currentConditionIndex,
            conditionName = cond.name,
            gradient = cond.averageGradient,
            isRolling = cond.isRolling,
            completed = completed || (currentPhase == Phase.RewardRating),
            duration = Time.time - phaseStartTime,
            distanceCovered = bikeController != null ? bikeController.GetDistance() - phaseStartDistance : 0f,
            avgSpeed = bikeController != null ? bikeController.GetSpeedKmh() : 0f,
            avgPower = bikeController != null ? bikeController.GetPower() : 0f,
            avgPainVAS = painVAS != null ? painVAS.GetCurrentValue() : 0f,
            rewardVAS = rewardVAS != null ? rewardVAS.GetAllResponses().Count > 0 ? 
                rewardVAS.GetAllResponses().Last().value : 0f : 0f,
            coinsEarned = coinsCollected,
            quitReason = currentPhase == Phase.Abandoned ? "no_pedaling_10s" : ""
        };
        results.Add(result);
        
        // Log
        LogDataPoint(completed ? "CONDITION_COMPLETE" : "CONDITION_ABANDONED");
        
        Debug.Log($"[HillExperiment] Condition {currentConditionIndex + 1} done: {cond.name}, " +
                  $"completed={result.completed}, coins={coinsCollected}");
        
        // Clean up coins and sign
        ClearCoins();
        if (climbSign != null) { Destroy(climbSign); climbSign = null; }
        foreach (var s in activeSignObjects) if (s != null) Destroy(s);
        activeSignObjects.Clear();
        
        // Insert MC Code for Agent Bridge
        PublishFlatRecoveryToMcp("condition_end");

        // Next condition
        currentConditionIndex++;
        StartNextCondition();
    }

    // === COINS ===
    
    private void SpawnCoins(int count)
    {
        ClearCoins();
        if (group != ExperimentGroup.Reward) return;
        
        // Always 5 coins per condition
        count = 5;
        
        // Generate random distances along the hill (sorted)
        System.Random rng = new System.Random(currentConditionIndex * 100 + blockNumber);
        List<float> coinDistances = new List<float>();
        for (int i = 0; i < count; i++)
        {
            float minDist = hillLength * 0.1f;
            float maxDist = hillLength * 0.9f;
            float dist = minDist + (float)rng.NextDouble() * (maxDist - minDist);
            coinDistances.Add(dist);
        }
        coinDistances.Sort();
        
        for (int i = 0; i < count; i++)
        {
            // Create a coin (cylinder squashed flat, standing upright facing the cyclist)
            GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = $"Coin_{i}";
            coin.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f); // Flat disc, larger for visibility
            coin.transform.position = new Vector3(0f, -100f, 0f); // Hidden until positioned
            
            // Gold material — use the same shader as other objects in the scene
            Renderer rend = coin.GetComponent<Renderer>();
            Material coinMat = rend.material; // Start with the default primitive material
            coinMat.color = new Color(1f, 0.85f, 0.1f);
            if (coinMat.HasProperty("_BaseColor")) coinMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.1f));
            if (coinMat.HasProperty("_Color")) coinMat.SetColor("_Color", new Color(1f, 0.85f, 0.1f));
            if (coinMat.HasProperty("_Smoothness")) coinMat.SetFloat("_Smoothness", 0.9f);
            if (coinMat.HasProperty("_Metallic")) coinMat.SetFloat("_Metallic", 0.8f);
            
            Collider col = coin.GetComponent<Collider>();
            if (col != null) Destroy(col);
            
            CoinData coinData = coin.AddComponent<CoinData>();
            coinData.distanceAlongRoute = coinDistances[i];
            coinData.collected = false;
            
            activeCoinObjects.Add(coin);
        }
        
        // Position coins immediately using bike's current transform as reference
        PositionCoinsAlongRoute();
        
        UpdateCoinCounter();
        Debug.Log($"[HillExperiment] Spawned {count} coins (Reward group)");
    }
    
    /// <summary>
    /// Position coins along the route using the same method the bike uses to move.
    /// Called once at spawn and can be called again if needed.
    /// </summary>
    private void PositionCoinsAlongRoute()
    {
        if (bikeController == null) return;
        
        SplinePath spline = FindObjectOfType<SplinePath>();
        
        foreach (GameObject coin in activeCoinObjects)
        {
            if (coin == null) continue;
            CoinData data = coin.GetComponent<CoinData>();
            if (data == null || data.collected) continue;
            
            // Total distance from course start = flat approach + coin's hill distance
            float totalDist = flatApproachLength + data.distanceAlongRoute;
            
            if (spline != null && spline.GetTotalLength() > 0f)
            {
                float clampedDist = Mathf.Clamp(totalDist, 0f, spline.GetTotalLength() - 1f);
                SplinePath.SplineSample s = spline.SampleAtDistance(clampedDist);

                // Use spline Y directly — same source as the road mesh and bike,
                // so coins sit exactly on the road surface regardless of terrain timing.
                // Position coin 1.2m above road so it floats at rider eye level
                Vector3 pos = new Vector3(s.position.x, s.position.y + 1.2f, s.position.z);
                coin.transform.position = pos;

                // Orient the coin perpendicular to the road (face toward the approaching cyclist).
                // The flat face of a cylinder is along its local Y axis.
                // We want the flat face facing the rider (back along the road = -forward).
                // Rotate so the cylinder's Y axis points along the road direction (disc face toward rider).
                Vector3 fwd = s.forward;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                coin.transform.rotation = Quaternion.LookRotation(Vector3.up, fwd);
            }
            else
            {
                // No spline — place coins ahead of bike start using forward direction
                Vector3 bikeStart = bikeController.transform.position;
                Vector3 forward = bikeController.transform.forward;
                Vector3 pos = bikeStart + forward * totalDist;
                pos.y += 1.5f;
                coin.transform.position = pos;
            }
        }
    }
    
    private void CheckCoinCollection(float currentHillDist)
    {
        if (bikeController == null) return;
        
        float bikeDistance = bikeController.GetDistance();
        
        foreach (GameObject coin in activeCoinObjects)
        {
            if (coin == null) continue;
            CoinData data = coin.GetComponent<CoinData>();
            if (data == null || data.collected) continue;
            
            // Coin is placed at spline distance = flatApproachLength + distanceAlongRoute
            // Collect when bike distance reaches or passes the coin position
            float coinSplineDist = flatApproachLength + data.distanceAlongRoute;
            
            if (bikeDistance >= coinSplineDist - 3f)
            {
                data.collected = true;
                coinsCollected++;
                totalCoinsCollected++;
                
                // Animate collection: scale down and disable
                coin.transform.localScale = Vector3.zero;
                coin.SetActive(false);
                
                UpdateCoinCounter();
                
                Debug.Log($"[HillExperiment] 🪙 Coin collected! ({coinsCollected}/5) at bikeDist={bikeDistance:F0}m, coinDist={coinSplineDist:F0}m");
                
                if (EventMarkerSender.Instance != null)
                    EventMarkerSender.Instance.SendEvent("COIN_COLLECTED", $"coin={coinsCollected},total={totalCoinsCollected}");
            }
        }
    }
    
    private void UpdateCoinCounter()
    {
        if (coinCounterText != null)
        {
            coinCounterText.text = $"🪙 {coinsCollected} / 5";
            
            // Flash effect on collection
            if (coinsCollected > 0)
            {
                coinCounterText.fontSize = 26;
                StartCoroutine(CoinCounterPulse());
            }
        }
    }
    
    private System.Collections.IEnumerator CoinCounterPulse()
    {
        if (coinCounterText == null) yield break;
        
        // Brief scale-up pulse
        float elapsed = 0f;
        float duration = 0.3f;
        Color original = coinCounterText.color;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            coinCounterText.transform.localScale = Vector3.one * scale;
            coinCounterText.color = Color.Lerp(Color.white, original, t);
            yield return null;
        }
        
        coinCounterText.transform.localScale = Vector3.one;
        coinCounterText.color = original;
        coinCounterText.fontSize = 20;
    }
    
    private void ClearCoins()
    {
        foreach (GameObject coin in activeCoinObjects)
        {
            if (coin != null) Destroy(coin);
        }
        activeCoinObjects.Clear();
    }
    
    // === DATA LOGGING ===
    
    private void LogDataPoint(string eventType)
    {
        if (trialLogWriter == null) return;
        
        int condIdx = currentConditionIndex < conditionOrder.Count ? conditionOrder[currentConditionIndex] : 0;
        HillCondition cond = condIdx < conditions.Count ? conditions[condIdx] : null;
        
        float dist = bikeController != null ? bikeController.GetDistance() : 0f;
        float speed = bikeController != null ? bikeController.GetSpeedKmh() : 0f;
        float power = bikeController != null ? bikeController.GetPower() : 0f;
        float cadence = bikeController != null ? bikeController.GetCadence() : 0f;
        float hr = 0f;
        WebSocketClient ws = FindObjectOfType<WebSocketClient>();
        if (ws != null) hr = ws.GetHeartRateBpm();
        float painVal = painVAS != null ? painVAS.GetCurrentValue() : 0f;
        float gradient = bikeController != null ? bikeController.GetGradient() : 0f;
        
        trialLogWriter.WriteLine($"{Time.time:F3},{cond?.name ?? "none"},{cond?.averageGradient ?? 0:F1}," +
            $"{cond?.isRolling ?? false},{currentPhase},{dist:F1},{speed:F1},{power:F0},{cadence:F0}," +
            $"{hr:F0},{painVal:F1},{gradient:F1},{coinsCollected},{eventType}");
        
        if (Time.frameCount % 50 == 0) trialLogWriter.Flush();
    }
    
    // === BLOCK END ===

    /// <summary>
    /// Appends one row to the shared participant_earnings.csv file.
    /// File lives in Application.persistentDataPath so it survives across sessions.
    /// Each coin = £0.10. Row is appended so all participants / blocks accumulate in one place.
    /// Columns: timestamp, participant_id, block, group, conditions_completed,
    ///          coins_collected, coins_possible, earnings_gbp
    /// </summary>
    private void WriteEarningsCSV()
    {
        const float poundPerCoin = 0.10f;
        int conditionsCompleted = results.Count(r => r.completed);
        int coinsPossible       = conditionsCompleted * 5; // 5 coins per completed condition
        float earningsGbp       = totalCoinsCollected * poundPerCoin;

        string csvPath = System.IO.Path.Combine(
            Application.persistentDataPath, "participant_earnings.csv");

        bool fileExists = System.IO.File.Exists(csvPath);

        try
        {
            using (var w = new System.IO.StreamWriter(csvPath, append: true))
            {
                // Write header only when creating the file for the first time
                if (!fileExists)
                    w.WriteLine("timestamp,participant_id,block,group," +
                                "conditions_completed,coins_collected,coins_possible,earnings_gbp");

                w.WriteLine(
                    $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
                    $"{participantID}," +
                    $"{blockNumber}," +
                    $"{group}," +
                    $"{conditionsCompleted}," +
                    $"{totalCoinsCollected}," +
                    $"{coinsPossible}," +
                    $"{earningsGbp:F2}");
            }

            Debug.Log($"[HillExperiment] Earnings written → {csvPath}");
            Debug.Log($"[HillExperiment] {participantID} earned £{earningsGbp:F2} " +
                      $"({totalCoinsCollected}/{coinsPossible} coins)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HillExperiment] Failed to write earnings CSV: {e.Message}");
        }
    }

    private void EndBlock()
    {
        experimentRunning = false;
        currentPhase = Phase.BlockEnd;
        
        if (painVAS != null) painVAS.Hide();
        ClearCoins();
        
        if (trialLogWriter != null)
        {
            trialLogWriter.Flush();
            trialLogWriter.Close();
        }
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"BLOCK {blockNumber} END — ALL {results.Count} CONDITIONS COMPLETE");
        Debug.Log($"{'='*60}");
        Debug.Log($"Participant: {participantID}, Group: {group}");
        Debug.Log($"Total coins: {totalCoinsCollected}");
        Debug.Log($"Completed: {results.Count(r => r.completed)}/{results.Count}");
        Debug.Log($"Data: {logFilePath}");
        Debug.Log($"{'='*60}\n");
        
        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("BLOCK_END", $"block={blockNumber},completed={results.Count(r => r.completed)},coins={totalCoinsCollected}");

        PublishFlatRecoveryToMcp("block_end");

        // Write participant earnings to the persistent CSV tracker
        WriteEarningsCSV();
        
        // Return to participant ID entry screen for next participant/block
        StartScreenUI startScreen = FindObjectOfType<StartScreenUI>(true); // include inactive
        if (startScreen != null)
        {
            startScreen.gameObject.SetActive(true);
            startScreen.ShowStartScreenManual();
            Debug.Log("[HillExperiment] Returned to participant entry screen");
        }
        else
        {
            TrialStarterUI starter = FindObjectOfType<TrialStarterUI>();
            if (starter != null) starter.ShowStartPanel();
        }
    }
    
    // === GUI ===
    
    private void OnGUI()
    {
        if (!experimentRunning) return;
        
        int condIdx = currentConditionIndex < conditionOrder.Count ? conditionOrder[currentConditionIndex] : 0;
        HillCondition cond = condIdx < conditions.Count ? conditions[condIdx] : null;
        
        // Info box (top-right)
        int bw = 260, bh = 90;
        GUI.Box(new Rect(Screen.width - bw - 10, 10, bw, bh), "");
        
        GUIStyle bold = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        GUIStyle info = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        
        int x = Screen.width - bw - 2, y = 15;
        GUI.Label(new Rect(x, y, bw - 16, 18), $"Block {blockNumber} — {currentConditionIndex + 1}/{conditionOrder.Count}", bold);
        GUI.Label(new Rect(x, y + 18, bw - 16, 16), $"{cond?.name ?? "?"} ({cond?.averageGradient ?? 0}%)", info);
        GUI.Label(new Rect(x, y + 34, bw - 16, 16), $"Phase: {currentPhase}", info);
        
        float dist = bikeController != null ? bikeController.GetDistance() - phaseStartDistance : 0f;
        GUI.Label(new Rect(x, y + 50, bw - 16, 16), $"Dist: {dist:F0}m / {hillLength:F0}m", info);
        
        if (noSpeedTimer > 2f)
            GUI.Label(new Rect(x, y + 66, bw - 16, 16), $"⚠ No pedaling: {noSpeedTimer:F0}s / {quitTimeThreshold:F0}s", info);
    }
    
    private void OnDestroy()
    {
        if (trialLogWriter != null)
        {
            trialLogWriter.Flush();
            trialLogWriter.Close();
        }
        if (uiCanvas != null) Destroy(uiCanvas.gameObject);
        ClearCoins();
    }
}

// Simple component to store coin data
public class CoinData : MonoBehaviour
{
    public float distanceAlongRoute;
    public bool collected;
}
