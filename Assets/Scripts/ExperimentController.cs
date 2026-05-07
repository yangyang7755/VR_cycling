using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Master experiment controller for gradient perception study.
/// Manages trial types with specific average gradients, randomized order,
/// and integrates with VAS scales for pain/reward ratings.
/// 
/// Trial Types:
///   Type 1: Flat (0% average gradient)
///   Type 2: Moderate (2%, 5% average gradient)  
///   Type 3: Steep (8%, 10% average gradient)
/// 
/// Each trial generates a curved road with the target average gradient
/// plus natural undulations. Trials are randomized across participants.
/// 
/// SETUP: Add to any GameObject. Assign CurvedRouteGenerator reference.
/// </summary>
public class ExperimentController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CurvedRouteGenerator routeGenerator;
    [SerializeField] private BikeController bikeController;
    [SerializeField] private VASScale vasScale;
    [SerializeField] private UnityDataLogger dataLogger;
    
    [Header("Trial Configuration")]
    [SerializeField] private List<TrialType> trialTypes = new List<TrialType>();
    
    [Tooltip("Number of repetitions per trial type")]
    [SerializeField] private int repetitionsPerType = 2;
    
    [Tooltip("Trial route length (meters)")]
    [SerializeField] private float trialLength = 500f;
    
    [Tooltip("Flat section at the start of every trial (meters). Rider gets this many metres at 0% before the target gradient begins.")]
    [SerializeField] private float flatLeadInMeters = 50f;
    
    [Tooltip("Rest period between trials (seconds)")]
    [SerializeField] private float restPeriod = 30f;
    
    [Header("Randomization")]
    [Tooltip("Randomize trial order (counterbalanced)")]
    [SerializeField] private bool randomizeOrder = true;
    
    [Tooltip("Participant ID (affects randomization seed)")]
    [SerializeField] private string participantID = "P001";
    
    [Header("VAS Prompts")]
    [Tooltip("Show Reward VAS at the end of each trial")]
    [SerializeField] private bool rewardVASAfterTrial = true;
    
    [Tooltip("Show Pain VAS during trial at these distance percentages (e.g. 0.25, 0.5, 0.75)")]
    [SerializeField] private float[] painVASDuringTrial = { 0.25f, 0.5f, 0.75f };
    
    [Header("Status")]
    [SerializeField] private int currentTrialIndex = 0;
    [SerializeField] private bool experimentRunning = false;
    [SerializeField] private bool trialActive = false;
    
    [System.Serializable]
    public class TrialType
    {
        public string name = "Flat";
        public float averageGradient = 0f; // target average gradient %
        public float gradientVariation = 1f; // how much gradient varies around the average
        public GradientProfile profile = GradientProfile.Steady;
        public Color debugColor = Color.green;
    }
    
    public enum GradientProfile
    {
        Steady,  // Hill: consistent gradient, minimal variation
        Rolling  // Rolling: gradient oscillates around the average
    }
    
    [System.Serializable]
    public class TrialRecord
    {
        public int trialNumber;
        public string trialTypeName;
        public float targetGradient;
        public float actualDuration;
        public float averageSpeed;
        public float averagePower;
        public float distance;
        public System.DateTime startTime;
    }
    
    private List<int> trialOrder = new List<int>(); // indices into trialTypes
    private List<TrialRecord> completedTrials = new List<TrialRecord>();
    private float trialStartTime;
    private float trialStartDistance;
    private bool waitingForRest;
    private float restEndTime;
    private bool[] vasTriggeredDuringTrial;
    private string logFilePath;
    private int blockNumber = 1;

    // ── Full-screen prompt system ────────────────────────────────────────────
    private GameObject promptCanvas;
    private UnityEngine.UI.Image promptBg;
    private TMPro.TextMeshProUGUI promptText;
    private float promptHideTime = 0f;
    private bool[] promptsTriggered; // tracks which distance-based prompts have fired

    // ── Idle detection (stop pedalling → end trial) ──────────────────────────
    [Header("Idle Detection")]
    [Tooltip("Seconds of no pedalling (speed < threshold) before auto-ending trial")]
    [SerializeField] private float idleTimeoutSeconds = 8f;
    
    [Tooltip("Speed below this (km/h) counts as idle")]
    [SerializeField] private float idleSpeedThreshold = 1f;
    
    private float idleTimer = 0f;
    private bool idleTriggered = false;

    private void Awake()
    {
        // Pre-set the first trial's gradient BEFORE CurvedRouteGenerator.Start() fires.
        // CurvedRouteGenerator waits 2 frames before generating, so this Awake() call
        // ensures trialTargetGradient is already set when GenerateRouteDelayed() runs.
        if (routeGenerator == null)
            routeGenerator = FindObjectOfType<CurvedRouteGenerator>();

        if (trialTypes.Count == 0)
            CreateDefaultTrialTypes();

        GenerateTrialOrder();
        PreSetFirstTrialGradient();
    }

    /// <summary>
    /// Called from Awake so the gradient is ready before CurvedRouteGenerator generates.
    /// </summary>
    private void PreSetFirstTrialGradient()
    {
        if (routeGenerator == null || trialOrder.Count == 0) return;

        int typeIndex = trialOrder[0];
        TrialType first = trialTypes[typeIndex];

        // Mirror ConfigureRouteForGradient but without reflection (just the gradient call)
        routeGenerator.SetTrialGradient(
            first.averageGradient,
            first.gradientVariation,
            first.profile == GradientProfile.Rolling,
            flatLeadInMeters);

        Debug.Log($"[Experiment] Awake: pre-set first trial gradient → {first.name} ({first.averageGradient}%)");
    }

    private void Start()
    {
        if (routeGenerator == null) routeGenerator = FindObjectOfType<CurvedRouteGenerator>();
        if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
        if (vasScale == null) vasScale = FindObjectOfType<VASScale>();
        if (dataLogger == null) dataLogger = FindObjectOfType<UnityDataLogger>();
        
        // trialTypes and trialOrder already built in Awake — only rebuild if empty
        if (trialTypes.Count == 0) CreateDefaultTrialTypes();
        if (trialOrder.Count == 0) GenerateTrialOrder();
        
        // Set up log file
        logFilePath = System.IO.Path.Combine(Application.persistentDataPath, 
            $"experiment_{participantID}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        WriteExperimentHeader();
        
        Debug.Log($"[Experiment] Initialized for {participantID}");
        Debug.Log($"[Experiment] {trialTypes.Count} trial types × {repetitionsPerType} reps = {trialOrder.Count} total trials");
        Debug.Log($"[Experiment] Order: {string.Join(", ", trialOrder.Select(i => trialTypes[i].name))}");
        Debug.Log($"[Experiment] Log: {logFilePath}");

        // Generate the first trial's route immediately so the scene is ready on load.
        if (routeGenerator != null && trialOrder.Count > 0)
        {
            ConfigureRouteForGradient(trialTypes[trialOrder[0]]);
            // ConfigureRouteForGradient → ApplyCondition → GenerateRoute already called
            Debug.Log($"[Experiment] Initial route generated for: {trialTypes[trialOrder[0]].name}");
        }
    }
    
    private void CreateDefaultTrialTypes()
    {
        // 9 conditions: 3 gradients × 3 profiles
        // Each trial starts with 50m flat, then the target gradient for the remaining 450m.
        // Steady  = constant gradient, minimal variation (±0.3%)
        // Rolling = gradient oscillates around the average (±2-3%)
        // Flat    = 0% throughout (control condition)

        // --- 0% (Flat control) ---
        trialTypes.Add(new TrialType {
            name = "Flat_0pct",
            averageGradient = 0f, gradientVariation = 0.2f,
            profile = GradientProfile.Steady,
            debugColor = Color.green
        });

        // --- 2% conditions ---
        trialTypes.Add(new TrialType {
            name = "Steady_2pct",
            averageGradient = 2f, gradientVariation = 0.3f,
            profile = GradientProfile.Steady,
            debugColor = Color.yellow
        });
        trialTypes.Add(new TrialType {
            name = "Rolling_2pct",
            averageGradient = 2f, gradientVariation = 1.5f,
            profile = GradientProfile.Rolling,
            debugColor = new Color(0.8f, 0.9f, 0f)
        });

        // --- 5% conditions ---
        trialTypes.Add(new TrialType {
            name = "Steady_5pct",
            averageGradient = 5f, gradientVariation = 0.3f,
            profile = GradientProfile.Steady,
            debugColor = new Color(1f, 0.6f, 0f)
        });
        trialTypes.Add(new TrialType {
            name = "Rolling_5pct",
            averageGradient = 5f, gradientVariation = 2f,
            profile = GradientProfile.Rolling,
            debugColor = new Color(1f, 0.7f, 0f)
        });

        // --- 8% conditions ---
        trialTypes.Add(new TrialType {
            name = "Steady_8pct",
            averageGradient = 8f, gradientVariation = 0.3f,
            profile = GradientProfile.Steady,
            debugColor = new Color(1f, 0.3f, 0f)
        });
        trialTypes.Add(new TrialType {
            name = "Rolling_8pct",
            averageGradient = 8f, gradientVariation = 3f,
            profile = GradientProfile.Rolling,
            debugColor = Color.red
        });

        // --- 12% conditions ---
        trialTypes.Add(new TrialType {
            name = "Steady_12pct",
            averageGradient = 12f, gradientVariation = 0.3f,
            profile = GradientProfile.Steady,
            debugColor = new Color(0.8f, 0f, 0f)
        });
        trialTypes.Add(new TrialType {
            name = "Rolling_12pct",
            averageGradient = 12f, gradientVariation = 4f,
            profile = GradientProfile.Rolling,
            debugColor = new Color(0.6f, 0f, 0.2f)
        });
    }
    
    private void GenerateTrialOrder()
    {
        trialOrder.Clear();
        
        // Create list with repetitions
        for (int rep = 0; rep < repetitionsPerType; rep++)
        {
            for (int i = 0; i < trialTypes.Count; i++)
            {
                trialOrder.Add(i);
            }
        }
        
        // Randomize using participant ID as seed
        if (randomizeOrder)
        {
            int seed = participantID.GetHashCode();
            System.Random rng = new System.Random(seed);
            
            // Fisher-Yates shuffle
            for (int i = trialOrder.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int temp = trialOrder[i];
                trialOrder[i] = trialOrder[j];
                trialOrder[j] = temp;
            }
        }
    }
    
    private void Update()
    {
        // F5 to start experiment when not running
        if (!experimentRunning)
        {
            if (Input.GetKeyDown(KeyCode.F5)) StartExperiment();
            return;
        }

        // Ratings page active — wait for Pain then Reward VAS
        if (showingRatingsPage)
        {
            CheckRatingsProgress();
            return;
        }

        if (trialActive)
        {
            CheckTrialProgress();
            CheckIdleTimeout();
        }

        // Manual controls
        if (Input.GetKeyDown(KeyCode.F6)) SkipTrial();
    }
    
    [ContextMenu("Start Experiment")]
    public void StartExperiment()
    {
        experimentRunning = true;
        currentTrialIndex = 0;
        completedTrials.Clear();
        waitingForRest = false;

        // Disable competing systems that fight over course distance and completion
        SimpleCompletionOverlay sco = FindObjectOfType<SimpleCompletionOverlay>();
        if (sco != null) { sco.enabled = false; Debug.Log("[Experiment] Disabled SimpleCompletionOverlay"); }
        
        HillClimbExperiment hce = FindObjectOfType<HillClimbExperiment>();
        if (hce != null) { hce.enabled = false; Debug.Log("[Experiment] Disabled HillClimbExperiment"); }

        ConfigurableTrialSequence cts = FindObjectOfType<ConfigurableTrialSequence>();
        if (cts != null) { cts.enabled = false; Debug.Log("[Experiment] Disabled ConfigurableTrialSequence"); }

        // Re-generate trial order (uses participant ID as seed)
        GenerateTrialOrder();

        Debug.Log($"\n{'='*60}");
        Debug.Log($"EXPERIMENT BLOCK {blockNumber} STARTED");
        Debug.Log($"{'='*60}");
        Debug.Log($"Participant: {participantID}");
        Debug.Log($"Block: {blockNumber}");
        Debug.Log($"Conditions: {trialOrder.Count} ({string.Join(" → ", trialOrder.Select(i => trialTypes[i].name))})");
        Debug.Log($"Each condition: {trialLength}m");
        Debug.Log($"Pain VAS: at {string.Join("%, ", painVASDuringTrial.Select(v => (v*100).ToString("F0")))}% of each condition");
        Debug.Log($"Reward VAS: at end of each condition");
        Debug.Log($"{'='*60}\n");

        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("BLOCK_START", $"block={blockNumber},participant={participantID},conditions={trialOrder.Count}");

        StartNextTrial();
    }

    public void SetBlockNumber(int block) { blockNumber = block; }
    
    private void StartNextTrial()
    {
        if (currentTrialIndex >= trialOrder.Count)
        {
            CompleteExperiment();
            return;
        }

        int typeIndex = trialOrder[currentTrialIndex];
        TrialType trial = trialTypes[typeIndex];

        Debug.Log($"\n[Experiment] === CONDITION {currentTrialIndex + 1}/{trialOrder.Count}: {trial.name} (avg {trial.averageGradient}%, {trial.profile}) ===");

        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("TRIAL_START", $"trial={currentTrialIndex + 1},type={trial.name},gradient={trial.averageGradient}");

        // Configure and generate route — ApplyCondition() calls GenerateRoute() internally
        ConfigureRouteForGradient(trial);

        // Reset bike immediately
        if (bikeController != null)
        {
            bikeController.ResetCourse();
            trialStartDistance = bikeController.GetDistance();
        }

        trialStartTime = Time.time;
        trialActive = true;
        vasTriggeredDuringTrial = new bool[painVASDuringTrial.Length];
        promptsTriggered = new bool[2]; // 0=StartPedalling, 1=ClimbStart
        idleTimer = 0f;
        idleTriggered = false;

        // Force 500m limit AFTER a delay (to override whatever GenerateRoute sets)
        StartCoroutine(ForceCourseLimitAfterDelay());

        if (dataLogger != null)
        {
            dataLogger.SetTrialInfo($"{participantID}_B{blockNumber}_T{currentTrialIndex + 1}_{trial.name}", currentTrialIndex + 1);
            if (!dataLogger.IsLogging()) dataLogger.StartLogging();
        }
    }

    private System.Collections.IEnumerator ForceCourseLimitAfterDelay()
    {
        yield return null;
        yield return null;
        yield return null;

        if (bikeController != null)
        {
            bikeController.SetMaxCourseDistance(trialLength);
        }

        // Also fix the elevation progress bar to show 500m
        ElevationProgressBar progressBar = FindObjectOfType<ElevationProgressBar>();
        if (progressBar != null)
        {
            progressBar.SetTotalRouteLength(trialLength);
        }

        Debug.Log($"[Experiment] Course + elevation bar set to {trialLength}m");
    }
    
    private void ConfigureRouteForGradient(TrialType trial)
    {
        if (routeGenerator == null) return;

        // Map trial name to the hardcoded ExperimentCondition enum
        if (System.Enum.TryParse(trial.name, out CurvedRouteGenerator.ExperimentCondition cond))
        {
            routeGenerator.ApplyCondition(cond);
            Debug.Log($"[Experiment] Applied condition: {cond}");
            return;
        }

        // Fallback: use SetTrialGradient for any trial not matching the enum names
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        var routeLenField = typeof(CurvedRouteGenerator).GetField("routeLength", flags);
        if (routeLenField != null) routeLenField.SetValue(routeGenerator, trialLength);

        var maxGradField = typeof(CurvedRouteGenerator).GetField("maxGradientPercent", flags);
        if (maxGradField != null)
            maxGradField.SetValue(routeGenerator, trial.averageGradient + trial.gradientVariation + 2f);

        var elevStyleField = typeof(CurvedRouteGenerator).GetField("elevationStyle", flags);
        if (elevStyleField != null)
        {
            if (trial.averageGradient < 1f)
                elevStyleField.SetValue(routeGenerator, CurvedRouteGenerator.ElevationStyle.Flat);
            else if (trial.profile == GradientProfile.Rolling)
                elevStyleField.SetValue(routeGenerator, CurvedRouteGenerator.ElevationStyle.Rolling);
            else
                elevStyleField.SetValue(routeGenerator, CurvedRouteGenerator.ElevationStyle.Mountain);
        }

        routeGenerator.SetTrialGradient(trial.averageGradient, trial.gradientVariation,
            trial.profile == GradientProfile.Rolling, flatLeadInMeters);

        Debug.Log($"[Experiment] Fallback route config: {trial.name}, avg={trial.averageGradient}%, " +
                  $"var=±{trial.gradientVariation}%, profile={trial.profile}");
    }
    
    private void CheckTrialProgress()
    {
        if (bikeController == null) return;

        float currentDist = bikeController.GetDistance() - trialStartDistance;
        float progress = currentDist / trialLength;

        // Auto-hide prompt overlay
        if (promptCanvas != null && promptCanvas.activeSelf && Time.time >= promptHideTime)
            promptCanvas.SetActive(false);

        // Prompt 0 — "START PEDALLING" at 0m
        if (!promptsTriggered[0] && currentDist >= 0f)
        {
            promptsTriggered[0] = true;
            ShowPrompt("START PEDALLING", 3f, new Color(0.2f, 0.7f, 1f));
            SendMarker("START_PEDALLING");
        }

        // Prompt 1 — "CLIMB START" at flat lead-in
        if (!promptsTriggered[1] && currentDist >= flatLeadInMeters)
        {
            promptsTriggered[1] = true;
            ShowPrompt("CLIMB START", 2.5f, new Color(1f, 0.6f, 0.1f));
            SendMarker("CLIMB_START");
        }

        // Pain VAS at configured percentages
        for (int i = 0; i < painVASDuringTrial.Length; i++)
        {
            if (!vasTriggeredDuringTrial[i] && progress >= painVASDuringTrial[i])
            {
                vasTriggeredDuringTrial[i] = true;
                ShowPrompt("RATE YOUR PAIN", 1.5f, new Color(1f, 0.3f, 0.3f));
                if (vasScale != null) vasScale.TriggerVASNow("Pain");
                SendMarker("PAIN_VAS_PROMPT");
                Debug.Log($"[Experiment] >>> Pain VAS at {progress*100:F0}% ({currentDist:F0}m)");
            }
        }

        // Log every second
        if (Mathf.FloorToInt(Time.time) != Mathf.FloorToInt(Time.time - Time.deltaTime))
            Debug.Log($"[Experiment] DIST={currentDist:F0}m / {trialLength:F0}m ({progress*100:F0}%)");

        // Condition complete
        if (currentDist >= trialLength)
        {
            ShowPrompt("CONDITION COMPLETE", 2f, new Color(0.3f, 1f, 0.3f));
            SendMarker("CONDITION_COMPLETE");
            Debug.Log($"[Experiment] >>> CONDITION COMPLETE at {currentDist:F0}m");
            CompleteTrial();
        }
    }

    // ── Idle detection ──────────────────────────────────────────────────────

    private void CheckIdleTimeout()
    {
        if (bikeController == null || idleTriggered) return;

        float speedKmh = bikeController.GetSpeedKmh();

        if (speedKmh < idleSpeedThreshold)
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleTimeoutSeconds)
            {
                idleTriggered = true;
                Debug.Log($"[Experiment] >>> IDLE TIMEOUT — no pedalling for {idleTimeoutSeconds}s, ending trial");
                ShowPrompt("TRIAL ENDED\n(stopped pedalling)", 2.5f, new Color(1f, 0.5f, 0.2f));
                SendMarker("IDLE_TIMEOUT");
                CompleteTrial();
            }
        }
        else
        {
            // Reset timer when pedalling resumes
            idleTimer = 0f;
        }
    }

    // ── Prompt overlay ───────────────────────────────────────────────────────

    private void ShowPrompt(string message, float duration, Color textColor)
    {
        EnsurePromptCanvas();
        promptText.text = message;
        promptText.color = textColor;
        promptCanvas.SetActive(true);
        promptHideTime = Time.time + duration;
        Debug.Log($"[Experiment] PROMPT: \"{message}\" for {duration}s");
    }

    private void EnsurePromptCanvas()
    {
        if (promptCanvas != null) return;

        GameObject canvasObj = new GameObject("ExperimentPromptCanvas");
        Canvas c = canvasObj.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 180;
        UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject panel = new GameObject("PromptPanel");
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        promptBg = panel.AddComponent<UnityEngine.UI.Image>();
        promptBg.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.3f);
        textRect.anchorMax = new Vector2(1f, 0.7f);
        textRect.sizeDelta = Vector2.zero;
        promptText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        promptText.text = "";
        promptText.fontSize = 72;
        promptText.color = Color.white;
        promptText.alignment = TMPro.TextAlignmentOptions.Center;
        promptText.fontStyle = TMPro.FontStyles.Bold;
        promptText.enableWordWrapping = true;

        promptCanvas = canvasObj;
        promptCanvas.SetActive(false);
    }

    private void SendMarker(string eventName)
    {
        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent(eventName);
    }
    
    private void CompleteTrial()
    {
        trialActive = false;

        int typeIndex = trialOrder[currentTrialIndex];
        TrialType trial = trialTypes[typeIndex];
        float duration = Time.time - trialStartTime;
        float distance = bikeController != null ? bikeController.GetDistance() : trialLength;

        TrialRecord record = new TrialRecord
        {
            trialNumber = currentTrialIndex + 1,
            trialTypeName = trial.name,
            targetGradient = trial.averageGradient,
            actualDuration = duration,
            averageSpeed = (distance / Mathf.Max(duration, 1f)) * 3.6f,
            averagePower = bikeController != null ? bikeController.GetPower() : 0f,
            distance = distance,
            startTime = System.DateTime.Now
        };
        completedTrials.Add(record);
        WriteTrialRecord(record);

        Debug.Log($"[Experiment] Condition {record.trialNumber}/{trialOrder.Count} complete: {record.trialTypeName} ({trial.averageGradient}%), {duration:F1}s");

        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("TRIAL_END", $"trial={record.trialNumber},type={record.trialTypeName},gradient={trial.averageGradient},duration={duration:F1}");

        // Show ratings page: Pain first, then Reward
        ShowRatingsPage();
    }

    private bool showingRatingsPage = false;
    private int ratingsStep = 0; // 0 = pain, 1 = reward, 2 = done

    private void ShowRatingsPage()
    {
        showingRatingsPage = true;
        ratingsStep = 0;

        // Show Pain VAS
        if (vasScale != null)
        {
            vasScale.TriggerVASNow("Pain");
            Debug.Log("[Experiment] RATINGS: Pain VAS shown");
        }
        else
        {
            // No VAS system — skip to next condition
            FinishRatings();
        }
    }

    private void CheckRatingsProgress()
    {
        if (vasScale == null || !vasScale.IsShowing())
        {
            if (ratingsStep == 0)
            {
                // Pain VAS submitted — now show Reward
                ratingsStep = 1;
                vasScale.TriggerVASNow("Reward");
                Debug.Log("[Experiment] RATINGS: Reward VAS shown");
            }
            else if (ratingsStep == 1)
            {
                // Reward VAS submitted — ratings complete
                ratingsStep = 2;
                FinishRatings();
            }
        }
    }

    private void FinishRatings()
    {
        showingRatingsPage = false;
        currentTrialIndex++;

        if (currentTrialIndex < trialOrder.Count)
        {
            Debug.Log($"[Experiment] Loading condition {currentTrialIndex + 1}/{trialOrder.Count}...");
            ShowPrompt($"NEXT CONDITION\n{currentTrialIndex + 1} of {trialOrder.Count}", 3f, new Color(0.2f, 0.7f, 1f));
            SendMarker("NEXT_CONDITION");
            // Delay starting next trial so the prompt is visible
            StartCoroutine(DelayedStartNextTrial(3.5f));
        }
        else
        {
            CompleteExperiment();
        }
    }

    private System.Collections.IEnumerator DelayedStartNextTrial(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNextTrial();
    }

    private void SkipTrial()
    {
        if (trialActive)
        {
            Debug.Log("[Experiment] Trial skipped by experimenter");
            CompleteTrial();
        }
    }
    
    private void CompleteExperiment()
    {
        experimentRunning = false;
        trialActive = false;
        showingRatingsPage = false;

        ShowPrompt($"BLOCK {blockNumber} COMPLETE\nAll {completedTrials.Count} conditions finished", 5f, new Color(0.3f, 1f, 0.3f));
        SendMarker("BLOCK_END");

        Debug.Log($"\n{'='*60}");
        Debug.Log($"BLOCK {blockNumber} END — ALL {completedTrials.Count} CONDITIONS COMPLETE");
        Debug.Log($"{'='*60}");
        Debug.Log($"Participant: {participantID}");
        Debug.Log($"Data saved to: {logFilePath}");
        if (vasScale != null)
            Debug.Log($"VAS responses: {vasScale.GetResponseCount()}");
        Debug.Log($"{'='*60}\n");

        if (EventMarkerSender.Instance != null)
            EventMarkerSender.Instance.SendEvent("BLOCK_END", $"block={blockNumber},conditions={completedTrials.Count}");

        // Show the trial starter UI so experimenter can re-enter info for next block
        TrialStarterUI starterUI = FindObjectOfType<TrialStarterUI>();
        if (starterUI != null)
        {
            starterUI.ShowStartPanel();
            Debug.Log("[Experiment] Trial starter UI shown — enter next trial info");
        }
    }

    // --- Data Logging ---
    
    private void WriteExperimentHeader()
    {
        try
        {
            using (var w = new System.IO.StreamWriter(logFilePath, false))
            {
                w.WriteLine($"# Experiment: Gradient Perception Study");
                w.WriteLine($"# Participant: {participantID}");
                w.WriteLine($"# Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                w.WriteLine($"# Trial Types: {string.Join(", ", trialTypes.Select(t => $"{t.name}({t.averageGradient}%)"))}");
                w.WriteLine($"# Repetitions: {repetitionsPerType}");
                w.WriteLine($"# Trial Length: {trialLength}m");
                w.WriteLine();
                w.WriteLine("trial_number,trial_type,target_gradient_pct,duration_s,avg_speed_kmh,avg_power_w,distance_m,timestamp");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Experiment] Log write error: {e.Message}");
        }
    }
    
    private void WriteTrialRecord(TrialRecord r)
    {
        try
        {
            using (var w = new System.IO.StreamWriter(logFilePath, true))
            {
                w.WriteLine($"{r.trialNumber},{r.trialTypeName},{r.targetGradient:F1},{r.actualDuration:F1}," +
                           $"{r.averageSpeed:F1},{r.averagePower:F0},{r.distance:F0},{r.startTime:yyyy-MM-dd HH:mm:ss}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Experiment] Log write error: {e.Message}");
        }
    }
    
    // --- GUI ---
    
    private void OnGUI()
    {
        if (!experimentRunning) 
        {
            GUI.Label(new Rect(Screen.width / 2 - 100, 10, 200, 25), "Press F5 to Start Experiment");
            return;
        }

        // Condition info box (top-right area, below HUD)
        int boxW = 280;
        int boxH = 100;
        int boxX = Screen.width - boxW - 10;
        int boxY = 10;

        GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.fontStyle = FontStyle.Bold;

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 12;

        if (currentTrialIndex < trialOrder.Count)
        {
            int typeIdx = trialOrder[currentTrialIndex];
            TrialType trial = trialTypes[typeIdx];

            string status = trialActive ? "RIDING" : 
                           (showingRatingsPage ? (ratingsStep == 0 ? "PAIN RATING" : "REWARD RATING") : "LOADING");

            float dist = bikeController != null ? bikeController.GetDistance() - trialStartDistance : 0f;

            GUI.Label(new Rect(boxX + 8, boxY + 5, boxW - 16, 20), 
                $"Block {blockNumber} — Condition {currentTrialIndex + 1}/{trialOrder.Count}", labelStyle);
            GUI.Label(new Rect(boxX + 8, boxY + 25, boxW - 16, 18), 
                $"Type: {trial.name}", infoStyle);
            GUI.Label(new Rect(boxX + 8, boxY + 43, boxW - 16, 18), 
                $"Avg Gradient: {trial.averageGradient}% ({trial.profile})", infoStyle);
            GUI.Label(new Rect(boxX + 8, boxY + 61, boxW - 16, 18), 
                $"Distance: {dist:F0}m / {trialLength:F0}m", infoStyle);
            GUI.Label(new Rect(boxX + 8, boxY + 79, boxW - 16, 18), 
                $"Status: {status}", infoStyle);
        }
        else
        {
            GUI.Label(new Rect(boxX + 8, boxY + 5, boxW - 16, 20), "BLOCK COMPLETE", labelStyle);
            GUI.Label(new Rect(boxX + 8, boxY + 25, boxW - 16, 18), 
                $"Conditions completed: {completedTrials.Count}", infoStyle);
        }
    }
    
    // --- Public API ---
    public void SetParticipantID(string id) { participantID = id; }
    public int GetCurrentTrialIndex() => currentTrialIndex;
    public int GetTotalTrials() => trialOrder.Count;
    public bool IsRunning() => experimentRunning;
    public List<TrialRecord> GetCompletedTrials() => new List<TrialRecord>(completedTrials);
}
