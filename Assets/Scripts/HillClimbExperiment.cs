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
    
    // State machine
    private enum Phase { Idle, FlatApproach, HillCue, HillClimb, Quitting, Completed, Abandoned, RewardRating, BlockEnd }
    private Phase currentPhase = Phase.Idle;
    
    private float phaseStartTime;
    private float phaseStartDistance;
    private float noSpeedTimer = 0f; // tracks how long speed has been zero
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
        conditions.Add(new HillCondition { name = "Flat_0pct",     averageGradient = 0f,  isRolling = false, gradientVariation = 0.1f, steadyNoise = 0.05f, flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 0,  terrainSeed = 1000 });
        conditions.Add(new HillCondition { name = "Steady_2pct",   averageGradient = 2f,  isRolling = false, gradientVariation = 0.2f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 2,  terrainSeed = 1001 });
        conditions.Add(new HillCondition { name = "Rolling_2pct",  averageGradient = 2f,  isRolling = true,  gradientVariation = 1.5f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 2,  terrainSeed = 1002 });
        conditions.Add(new HillCondition { name = "Steady_5pct",   averageGradient = 5f,  isRolling = false, gradientVariation = 0.2f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 4,  terrainSeed = 1003 });
        conditions.Add(new HillCondition { name = "Rolling_5pct",  averageGradient = 5f,  isRolling = true,  gradientVariation = 1.5f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 4,  terrainSeed = 1004 });
        conditions.Add(new HillCondition { name = "Steady_8pct",   averageGradient = 8f,  isRolling = false, gradientVariation = 0.2f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 6,  terrainSeed = 1005 });
        conditions.Add(new HillCondition { name = "Rolling_8pct",  averageGradient = 8f,  isRolling = true,  gradientVariation = 1.5f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 6,  terrainSeed = 1006 });
        conditions.Add(new HillCondition { name = "Steady_10pct",  averageGradient = 10f, isRolling = false, gradientVariation = 0.2f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 8,  terrainSeed = 1007 });
        conditions.Add(new HillCondition { name = "Rolling_10pct", averageGradient = 10f, isRolling = true,  gradientVariation = 1.5f, steadyNoise = 0.1f,  flatApproachMeters = 50f, hillLengthMeters = 450f, coinReward = 8,  terrainSeed = 1008 });
        Debug.Log($"[HillExperiment] Reset to {conditions.Count} default conditions");
    }
    
    private void GenerateConditionOrder()
    {
        conditionOrder.Clear();
        for (int i = 0; i < conditions.Count; i++)
            conditionOrder.Add(i);
        
        // Shuffle using participant ID + block as seed
        int seed = (participantID + blockNumber.ToString()).GetHashCode();
        System.Random rng = new System.Random(seed);
        for (int i = conditionOrder.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = conditionOrder[i];
            conditionOrder[i] = conditionOrder[j];
            conditionOrder[j] = tmp;
        }
        
        Debug.Log($"[Experiment] Order: {string.Join(", ", conditionOrder.Select(i => conditions[i].name))}");
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
        
        StartNextCondition();
    }
    
    private void StartNextCondition()
    {
        if (currentConditionIndex >= conditionOrder.Count)
        {
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
        }

        // Wait for route generation to complete
        yield return null;
        yield return null;
        yield return null;

        if (bikeController != null)
            bikeController.ResetCourse();

        coinsCollected = 0;
        noSpeedTimer = 0f;
        phaseStartTime = Time.time;
        phaseStartDistance = bikeController != null ? bikeController.GetDistance() : 0f;

        SetPhase(Phase.FlatApproach);

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
        
        switch (newPhase)
        {
            case Phase.FlatApproach:
                // Pain VAS stays visible throughout
                break;
                
            case Phase.HillCue:
                ShowHillCue();
                break;
                
            case Phase.HillClimb:
                StartHillClimb();
                break;
                
            case Phase.Completed:
                ShowCompletion(true);
                break;
                
            case Phase.Abandoned:
                ShowCompletion(false);
                break;
                
            case Phase.RewardRating:
                ShowRewardRating();
                break;
        }
    }
    
    // === UPDATE LOOP ===
    
    private void Update()
    {
        if (!experimentRunning) return;
        
        float dist = bikeController != null ? bikeController.GetDistance() : 0f;
        float speed = bikeController != null ? bikeController.GetSpeedKmh() : 0f;
        
        switch (currentPhase)
        {
            case Phase.FlatApproach:
                if (dist - phaseStartDistance >= flatApproachLength)
                    SetPhase(Phase.HillCue);
                break;
                
            case Phase.HillCue:
                // Auto-transition to hill climb after brief notification (no keypress needed)
                if (Time.time - phaseStartTime > 2f)
                    SetPhase(Phase.HillClimb);
                break;
                
            case Phase.HillClimb:
                UpdateHillClimb(dist, speed);
                break;
                
            case Phase.Completed:
            case Phase.Abandoned:
                // Wait 2 seconds then show reward rating
                if (Time.time - phaseStartTime > 2f)
                    SetPhase(Phase.RewardRating);
                break;
                
            case Phase.RewardRating:
                // Wait for reward VAS to be dismissed
                if (rewardVAS != null)
                {
                    // Give VAS at least 1 second to appear before checking if dismissed
                    if (Time.time - phaseStartTime > 1f && !rewardVAS.IsShowing())
                    {
                        FinishCondition();
                    }
                }
                else
                {
                    // No VAS assigned — auto-proceed after 3 seconds
                    if (Time.time - phaseStartTime > 3f)
                    {
                        FinishCondition();
                    }
                }
                break;
        }
        
        // Log data continuously during hill climb
        if (currentPhase == Phase.HillClimb || currentPhase == Phase.FlatApproach)
        {
            LogDataPoint("");
        }
        
        // Animate coins: spin and float up/down
        foreach (GameObject coin in activeCoinObjects)
        {
            if (coin != null && coin.activeSelf)
            {
                // Spin around Y axis
                coin.transform.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);
                // Bob up and down
                Vector3 pos = coin.transform.position;
                pos.y += Mathf.Sin(Time.time * 3f + coin.GetInstanceID()) * 0.005f;
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

        string cueMsg = $"⛰ CLIMB AHEAD\n\n" +
                        $"Gradient: {cond.averageGradient}%\n" +
                        $"({(cond.isRolling ? "Rolling" : "Steady")})";

        if (group == ExperimentGroup.Reward)
            cueMsg += $"\n\nCoins: 5 🪙";

        cueText.text = cueMsg;
        cuePanel.SetActive(true);

        // Spawn a physical "CLIMB BEGINS" sign at the 50m mark
        SpawnClimbSign(cond);

        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("HILL_CUE", $"gradient={cond.averageGradient},elevation_gain={elevGain:F0},rolling={cond.isRolling}");
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

        hillLength = cond.hillLengthMeters;
        flatApproachLength = cond.flatApproachMeters;

        StartCoroutine(SetCourseLimitDelayed());

        phaseStartDistance = bikeController != null ? bikeController.GetDistance() : 0f;
        noSpeedTimer = 0f;

        if (painVAS != null) painVAS.Show();

        if (group == ExperimentGroup.Reward)
            SpawnCoins(cond.coinReward);

        // Spawn roadside signs at the 50m mark
        SpawnRoadsideSigns(cond);

        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("HILL_START", $"gradient={cond.averageGradient},rolling={cond.isRolling}");

        Debug.Log($"[HillExperiment] Hill started: {cond.name}, {cond.averageGradient}%, {hillLength}m");
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

        // Offset to side of road
        float sideOffset = 8f;
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
    
    private void UpdateHillClimb(float dist, float speed)
    {
        float hillDist = dist - phaseStartDistance;
        
        // Quit detection: 5s no pedaling triggers a 5s countdown, then abandon
        if (!simulationModeActive && speed < quitSpeedThreshold)
        {
            noSpeedTimer += Time.deltaTime;
            
            // After 5s of no pedaling, show countdown
            if (noSpeedTimer >= 5f)
            {
                float countdownRemaining = quitTimeThreshold - noSpeedTimer;
                if (countdownRemaining > 0f)
                {
                    // Show countdown on cue panel
                    cuePanel.SetActive(true);
                    cueText.text = $"⚠ Resume pedaling!\n\nHill abandoned in: {Mathf.CeilToInt(countdownRemaining)}s";
                }
                
                if (noSpeedTimer >= quitTimeThreshold)
                {
                    cuePanel.SetActive(false);
                    Debug.Log($"[HillExperiment] QUIT — no pedaling for {quitTimeThreshold}s");
                    if (EventMarkerSender.Instance != null)
                        EventMarkerSender.Instance.SendEvent("QUIT_DETECTED", $"no_speed_duration={noSpeedTimer:F1}");
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
        // Pain VAS stays visible
        
        if (completed)
        {
            string msg = "Hill Complete!";
            if (group == ExperimentGroup.Reward)
                msg += $"\n\nCoins earned: {coinsCollected} 🪙";
            completionText.text = msg;
            completionText.color = Color.green;
        }
        else
        {
            completionText.text = "Hill Incomplete!";
            completionText.color = new Color(1f, 0.3f, 0.3f);
        }
        
        completionPanel.SetActive(true);
    }
    
    private void ShowRewardRating()
    {
        completionPanel.SetActive(false);
        if (rewardVAS != null)
            rewardVAS.TriggerVASNow("Reward");
    }
    
    private void FinishCondition()
    {
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
            // Create a flat coin (cylinder squashed flat, rotated to face camera)
            GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = $"Coin_{i}";
            coin.transform.localScale = new Vector3(1.2f, 0.08f, 1.2f); // Flat disc
            coin.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Face forward (vertical disc)
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
                Vector3 pos = s.position;
                // Place coin at same height as the road + floating offset
                Terrain terrain = FindObjectOfType<Terrain>();
                if (terrain != null)
                {
                    float terrainY = terrain.SampleHeight(pos);
                    pos.y = Mathf.Max(pos.y, terrainY) + 1.5f;
                }
                else
                {
                    pos.y += 1.5f;
                }
                coin.transform.position = pos;
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
        Vector3 bikePos = bikeController.transform.position;
        
        foreach (GameObject coin in activeCoinObjects)
        {
            if (coin == null) continue;
            CoinData data = coin.GetComponent<CoinData>();
            if (data == null || data.collected) continue;
            
            // Use horizontal distance only (ignore height difference)
            Vector3 coinPos = coin.transform.position;
            float dx = bikePos.x - coinPos.x;
            float dz = bikePos.z - coinPos.z;
            float horizontalDist = Mathf.Sqrt(dx * dx + dz * dz);
            
            if (horizontalDist < 4f)
            {
                data.collected = true;
                coinsCollected++;
                totalCoinsCollected++;
                coin.SetActive(false);
                UpdateCoinCounter();
                
                Debug.Log($"[HillExperiment] 🪙 Coin collected! ({coinsCollected}/5)");
                
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
        }
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
        
        // Show start screen for next block
        StartScreenUI startScreen = FindObjectOfType<StartScreenUI>(true); // include inactive
        if (startScreen != null)
        {
            startScreen.gameObject.SetActive(true);
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
