using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

/// <summary>
/// Manages experimental trials with randomized gradients
/// Handles trial sequencing, counterbalancing, and data logging
/// </summary>
public class ExperimentalTrialManager : MonoBehaviour
{
    [Header("Trial Configuration")]
    [Tooltip("Participant ID")]
    [SerializeField] private string participantID = "P001";
    
    [Tooltip("Randomize trial order (counterbalancing)")]
    [SerializeField] private bool randomizeTrialOrder = true;
    
    [Tooltip("Number of repetitions per gradient")]
    [SerializeField] private int repetitionsPerGradient = 1;
    
    [Tooltip("Rest period between trials (seconds)")]
    [SerializeField] private float restPeriodSeconds = 60f;
    
    [Header("References")]
    [SerializeField] private ProceduralRouteGenerator routeGenerator;
    [SerializeField] private BikeController bikeController;
    [SerializeField] private ElevationProgressBar progressBar;
    [SerializeField] private UnityDataLogger dataLogger;
    [SerializeField] private SimpleCompletionOverlay completionOverlay;
    
    [Header("Trial State")]
    [SerializeField] private int currentTrialNumber = 0;
    [SerializeField] private int totalTrials = 0;
    [SerializeField] private bool trialInProgress = false;
    [SerializeField] private float trialStartTime = 0f;
    
    [Header("Data Logging")]
    [SerializeField] private bool logTrialData = true;
    [SerializeField] private string dataFolder = "ExperimentData";
    
    private List<TrialConfig> trialSequence = new List<TrialConfig>();
    private TrialConfig currentTrial;
    private string sessionID;
    private string logFilePath;

    [System.Serializable]
    public class TrialConfig
    {
        public int trialNumber;
        public float targetGradient;
        public int gradientIndex;
        public int repetitionNumber;
        public float startTime;
        public float endTime;
        public float actualGradient;
        public bool completed;
    }

    private void Start()
    {
        if (routeGenerator == null)
            routeGenerator = FindObjectOfType<ProceduralRouteGenerator>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (progressBar == null)
            progressBar = FindObjectOfType<ElevationProgressBar>();
        
        if (dataLogger == null)
            dataLogger = FindObjectOfType<UnityDataLogger>();
        
        if (completionOverlay == null)
            completionOverlay = FindObjectOfType<SimpleCompletionOverlay>();
        
        // Generate session ID
        sessionID = $"{participantID}_{DateTime.Now:yyyyMMdd_HHmmss}";
        
        // Setup data logging
        if (logTrialData)
        {
            SetupDataLogging();
        }
        
        Debug.Log($"[TrialManager] Session started: {sessionID}");
        Debug.Log($"[TrialManager] Press 'G' to generate trial sequence");
        Debug.Log($"[TrialManager] Press 'N' to start next trial");
        Debug.Log($"[TrialManager] Press 'E' to end current trial");
    }

    private void Update()
    {
        // Keyboard controls for trial management
        if (Input.GetKeyDown(KeyCode.G))
        {
            GenerateTrialSequence();
        }
        
        if (Input.GetKeyDown(KeyCode.N))
        {
            StartNextTrial();
        }
        
        if (Input.GetKeyDown(KeyCode.E))
        {
            EndCurrentTrial();
        }
        
        // Auto-end trial when route is completed
        if (trialInProgress && progressBar != null)
        {
            // Check if participant reached the end (you may need to add a method to check this)
            // For now, we'll rely on manual ending
        }
    }

    /// <summary>
    /// Generate randomized trial sequence
    /// </summary>
    [ContextMenu("Generate Trial Sequence")]
    public void GenerateTrialSequence()
    {
        if (routeGenerator == null)
        {
            Debug.LogError("[TrialManager] Route generator not assigned!");
            return;
        }
        
        trialSequence.Clear();
        
        // Get available gradients from route generator
        var gradients = new float[] { 0f, 2f, 5f, 7f, 10f, 12f };
        
        // Create trials for each gradient with repetitions
        int trialNum = 1;
        for (int rep = 0; rep < repetitionsPerGradient; rep++)
        {
            for (int i = 0; i < gradients.Length; i++)
            {
                TrialConfig trial = new TrialConfig
                {
                    trialNumber = trialNum++,
                    targetGradient = gradients[i],
                    gradientIndex = i,
                    repetitionNumber = rep + 1,
                    completed = false
                };
                trialSequence.Add(trial);
            }
        }
        
        totalTrials = trialSequence.Count;
        
        // Randomize order if enabled
        if (randomizeTrialOrder)
        {
            ShuffleTrials();
            Debug.Log($"[TrialManager] Trial order randomized (counterbalanced)");
        }
        
        // Renumber trials after shuffling
        for (int i = 0; i < trialSequence.Count; i++)
        {
            trialSequence[i].trialNumber = i + 1;
        }
        
        Debug.Log($"[TrialManager] Generated {totalTrials} trials");
        Debug.Log($"[TrialManager] Gradients: {string.Join(", ", gradients)}%");
        Debug.Log($"[TrialManager] Repetitions per gradient: {repetitionsPerGradient}");
        
        // Log trial sequence
        LogTrialSequence();
        
        currentTrialNumber = 0;
    }

    /// <summary>
    /// Shuffle trials using Fisher-Yates algorithm
    /// </summary>
    private void ShuffleTrials()
    {
        System.Random rng = new System.Random();
        int n = trialSequence.Count;
        
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            TrialConfig temp = trialSequence[k];
            trialSequence[k] = trialSequence[n];
            trialSequence[n] = temp;
        }
    }

    /// <summary>
    /// Start next trial in sequence
    /// </summary>
    [ContextMenu("Start Next Trial")]
    public void StartNextTrial()
    {
        if (trialInProgress)
        {
            Debug.LogWarning("[TrialManager] Trial already in progress! End current trial first.");
            return;
        }
        
        if (trialSequence.Count == 0)
        {
            Debug.LogWarning("[TrialManager] No trial sequence generated! Press 'G' to generate.");
            return;
        }
        
        if (currentTrialNumber >= trialSequence.Count)
        {
            Debug.Log("[TrialManager] All trials completed!");
            LogSessionSummary();
            return;
        }
        
        currentTrial = trialSequence[currentTrialNumber];
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"STARTING TRIAL {currentTrial.trialNumber}/{totalTrials}");
        Debug.Log($"{'='*60}");
        Debug.Log($"Target Gradient: {currentTrial.targetGradient}%");
        Debug.Log($"Repetition: {currentTrial.repetitionNumber}");
        Debug.Log($"{'='*60}\n");
        
        // Generate route with target gradient
        routeGenerator.SetGradientAndGenerate(currentTrial.targetGradient);
        
        // Store actual gradient achieved
        currentTrial.actualGradient = routeGenerator.GetActualGradient();
        
        // Reset progress bar and wait for terrain to update
        if (progressBar != null)
        {
            // Use coroutine to wait for terrain update
            StartCoroutine(UpdateProgressBarAfterTerrain());
        }
        
        // Reset bike controller position if needed
        if (bikeController != null)
        {
            // You may want to add a reset method to BikeController
        }
        
        // Mark trial as started
        trialInProgress = true;
        trialStartTime = Time.time;
        currentTrial.startTime = trialStartTime;
        
        // Start data logging
        if (dataLogger != null)
        {
            dataLogger.SetTrialInfo(participantID, currentTrial.trialNumber);
            if (!dataLogger.IsLogging())
            {
                dataLogger.StartLogging();
            }
        }
        
        // Start completion overlay
        if (completionOverlay != null)
        {
            completionOverlay.StartTrial();
        }
        
        Debug.Log($"[TrialManager] Trial started. Press 'E' to end trial when complete.");
    }

    /// <summary>
    /// End current trial
    /// </summary>
    [ContextMenu("End Current Trial")]
    public void EndCurrentTrial()
    {
        if (!trialInProgress)
        {
            Debug.LogWarning("[TrialManager] No trial in progress!");
            return;
        }
        
        currentTrial.endTime = Time.time;
        currentTrial.completed = true;
        
        float trialDuration = currentTrial.endTime - currentTrial.startTime;
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"TRIAL {currentTrial.trialNumber} COMPLETED");
        Debug.Log($"{'='*60}");
        Debug.Log($"Duration: {trialDuration:F1}s");
        Debug.Log($"Target Gradient: {currentTrial.targetGradient}%");
        Debug.Log($"Actual Gradient: {currentTrial.actualGradient:F2}%");
        Debug.Log($"{'='*60}\n");
        
        // Log trial data
        if (logTrialData)
        {
            LogTrialData(currentTrial, trialDuration);
        }
        
        trialInProgress = false;
        currentTrialNumber++;
        
        // Check if more trials remain
        if (currentTrialNumber < trialSequence.Count)
        {
            Debug.Log($"[TrialManager] {trialSequence.Count - currentTrialNumber} trials remaining");
            Debug.Log($"[TrialManager] Rest period: {restPeriodSeconds}s");
            Debug.Log($"[TrialManager] Press 'N' to start next trial");
        }
        else
        {
            Debug.Log($"[TrialManager] ✓ All trials completed!");
            LogSessionSummary();
        }
    }

    /// <summary>
    /// Setup data logging
    /// </summary>
    private void SetupDataLogging()
    {
        string folderPath = Path.Combine(Application.dataPath, "..", dataFolder);
        Directory.CreateDirectory(folderPath);
        
        logFilePath = Path.Combine(folderPath, $"{sessionID}_trials.csv");
        
        // Write header
        using (StreamWriter writer = new StreamWriter(logFilePath, false))
        {
            writer.WriteLine("SessionID,ParticipantID,TrialNumber,TargetGradient,ActualGradient,GradientError,RepetitionNumber,StartTime,EndTime,Duration,Completed");
        }
        
        Debug.Log($"[TrialManager] Data logging to: {logFilePath}");
    }

    /// <summary>
    /// Log trial sequence to file
    /// </summary>
    private void LogTrialSequence()
    {
        if (!logTrialData) return;
        
        string folderPath = Path.Combine(Application.dataPath, "..", dataFolder);
        string sequencePath = Path.Combine(folderPath, $"{sessionID}_sequence.txt");
        
        using (StreamWriter writer = new StreamWriter(sequencePath, false))
        {
            writer.WriteLine($"Session ID: {sessionID}");
            writer.WriteLine($"Participant ID: {participantID}");
            writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Total Trials: {totalTrials}");
            writer.WriteLine($"Randomized: {randomizeTrialOrder}");
            writer.WriteLine($"Repetitions per gradient: {repetitionsPerGradient}");
            writer.WriteLine();
            writer.WriteLine("Trial Sequence:");
            writer.WriteLine("Trial#\tGradient\tRepetition");
            
            foreach (var trial in trialSequence)
            {
                writer.WriteLine($"{trial.trialNumber}\t{trial.targetGradient}%\t{trial.repetitionNumber}");
            }
        }
        
        Debug.Log($"[TrialManager] Trial sequence logged to: {sequencePath}");
    }

    /// <summary>
    /// Log individual trial data
    /// </summary>
    private void LogTrialData(TrialConfig trial, float duration)
    {
        if (!logTrialData) return;
        
        float gradientError = Mathf.Abs(trial.actualGradient - trial.targetGradient);
        
        using (StreamWriter writer = new StreamWriter(logFilePath, true))
        {
            writer.WriteLine($"{sessionID},{participantID},{trial.trialNumber},{trial.targetGradient},{trial.actualGradient:F2},{gradientError:F2},{trial.repetitionNumber},{trial.startTime:F2},{trial.endTime:F2},{duration:F2},{trial.completed}");
        }
    }

    /// <summary>
    /// Log session summary
    /// </summary>
    private void LogSessionSummary()
    {
        if (!logTrialData) return;
        
        string folderPath = Path.Combine(Application.dataPath, "..", dataFolder);
        string summaryPath = Path.Combine(folderPath, $"{sessionID}_summary.txt");
        
        int completedTrials = 0;
        float totalDuration = 0f;
        float avgGradientError = 0f;
        
        foreach (var trial in trialSequence)
        {
            if (trial.completed)
            {
                completedTrials++;
                totalDuration += trial.endTime - trial.startTime;
                avgGradientError += Mathf.Abs(trial.actualGradient - trial.targetGradient);
            }
        }
        
        if (completedTrials > 0)
        {
            avgGradientError /= completedTrials;
        }
        
        using (StreamWriter writer = new StreamWriter(summaryPath, false))
        {
            writer.WriteLine($"SESSION SUMMARY");
            writer.WriteLine($"{'='*60}");
            writer.WriteLine($"Session ID: {sessionID}");
            writer.WriteLine($"Participant ID: {participantID}");
            writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine();
            writer.WriteLine($"Trials Completed: {completedTrials}/{totalTrials}");
            writer.WriteLine($"Total Duration: {totalDuration:F1}s ({totalDuration / 60f:F1} minutes)");
            writer.WriteLine($"Average Gradient Error: {avgGradientError:F2}%");
            writer.WriteLine();
            writer.WriteLine("Trial Results:");
            writer.WriteLine("Trial#\tTarget%\tActual%\tError%\tDuration(s)\tCompleted");
            
            foreach (var trial in trialSequence)
            {
                if (trial.completed)
                {
                    float error = Mathf.Abs(trial.actualGradient - trial.targetGradient);
                    float duration = trial.endTime - trial.startTime;
                    writer.WriteLine($"{trial.trialNumber}\t{trial.targetGradient}\t{trial.actualGradient:F2}\t{error:F2}\t{duration:F1}\t{trial.completed}");
                }
            }
        }
        
        Debug.Log($"[TrialManager] Session summary saved to: {summaryPath}");
    }

    /// <summary>
    /// Get current trial info for display
    /// </summary>
    public string GetCurrentTrialInfo()
    {
        if (currentTrial == null) return "No trial active";
        
        return $"Trial {currentTrial.trialNumber}/{totalTrials} - {currentTrial.targetGradient}% gradient";
    }
    /// <summary>
    /// Get current trial index (0-based)
    /// </summary>
    public int GetCurrentTrialIndex()
    {
        return currentTrialNumber;
    }
    
    /// <summary>
    /// Update progress bar after terrain generation
    /// </summary>
    private System.Collections.IEnumerator UpdateProgressBarAfterTerrain()
    {
        // Wait for terrain to be fully updated
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        if (progressBar != null && routeGenerator != null)
        {
            progressBar.SetTotalRouteLength(routeGenerator.GetRouteLength());
            progressBar.ResetProgress();
            Debug.Log("[TrialManager] Progress bar updated after terrain generation");
        }
    }
}
