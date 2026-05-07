using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Configurable trial sequence system
/// Allows manual configuration of multiple trials with different lengths
/// Runs trials one after another with smooth transitions
/// Perfect for experiments with varied trial durations
/// </summary>
public class ConfigurableTrialSequence : MonoBehaviour
{
    [Header("Trial Configuration")]
    [Tooltip("List of trials to run in sequence")]
    [SerializeField] private List<TrialConfig> trials = new List<TrialConfig>();
    
    [Tooltip("Current trial index")]
    [SerializeField] private int currentTrialIndex = 0;
    
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private ImprovedTerrainGenerator terrainGenerator;
    [SerializeField] private UnityDataLogger dataLogger;
    [SerializeField] private VASScale vasScale;
    
    [Header("VAS Ratings")]
    [Tooltip("Show Pain VAS after each trial before advancing")]
    [SerializeField] private bool showPainVASAfterTrial = true;
    
    [Tooltip("Show Reward VAS after each trial before advancing")]
    [SerializeField] private bool showRewardVASAfterTrial = true;
    
    [Header("Transition Settings")]
    [Tooltip("Pause duration between trials (seconds)")]
    [SerializeField] private float interTrialPause = 3f;
    
    [Tooltip("Show countdown during pause")]
    [SerializeField] private bool showCountdown = true;
    
    [Tooltip("Regenerate terrain between trials")]
    [SerializeField] private bool regenerateTerrainBetweenTrials = true;
    
    [Header("Auto-Start Settings")]
    [Tooltip("Start first trial automatically")]
    [SerializeField] private bool autoStartFirstTrial = false;
    
    [Tooltip("Auto-advance to next trial")]
    [SerializeField] private bool autoAdvanceTrials = true;
    
    [Header("Completion Settings")]
    [Tooltip("Show completion overlay after each trial")]
    [SerializeField] private bool showCompletionAfterEachTrial = true;
    
    [Tooltip("Show final summary after all trials")]
    [SerializeField] private bool showFinalSummary = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    // Trial state
    private bool sequenceActive = false;
    private bool trialInProgress = false;
    private float trialStartDistance = 0f;
    private float trialStartTime = 0f;
    private bool waitingForNextTrial = false;
    private float pauseEndTime = 0f;
    
    // VAS state
    private bool showingVAS = false;
    private int vasStep = 0; // 0 = pain, 1 = reward, 2 = done
    
    // Statistics
    private List<TrialResult> completedTrials = new List<TrialResult>();
    
    [Serializable]
    public class TrialConfig
    {
        [Tooltip("Trial name/identifier")]
        public string trialName = "Trial 1";
        
        [Tooltip("Trial length in meters")]
        public float lengthMeters = 500f;
        
        [Tooltip("Terrain profile for this trial")]
        public ImprovedTerrainGenerator.TerrainProfile terrainProfile = ImprovedTerrainGenerator.TerrainProfile.Mixed;
        
        [Tooltip("Fixed seed for reproducibility (0 = random)")]
        public int terrainSeed = 0;
        
        [Tooltip("Notes about this trial")]
        public string notes = "";
    }
    
    [Serializable]
    public class TrialResult
    {
        public string trialName;
        public float lengthMeters;
        public float durationSeconds;
        public float averageSpeedKmh;
        public float averagePowerWatts;
        public float averageGradient;
        public DateTime completionTime;
    }
    
    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (terrainGenerator == null)
            terrainGenerator = FindObjectOfType<ImprovedTerrainGenerator>();
        
        if (dataLogger == null)
            dataLogger = FindObjectOfType<UnityDataLogger>();
        
        if (vasScale == null)
            vasScale = FindObjectOfType<VASScale>();
        
        // Create default trials if none configured
        if (trials.Count == 0)
        {
            CreateDefaultTrials();
        }
        
        if (autoStartFirstTrial)
        {
            StartSequence();
        }
        
        LogSequenceConfiguration();
    }
    
    private void CreateDefaultTrials()
    {
        trials.Add(new TrialConfig 
        { 
            trialName = "Trial 1 - Warm Up",
            lengthMeters = 500f,
            terrainProfile = ImprovedTerrainGenerator.TerrainProfile.Flat
        });
        
        trials.Add(new TrialConfig 
        { 
            trialName = "Trial 2 - Rolling",
            lengthMeters = 1000f,
            terrainProfile = ImprovedTerrainGenerator.TerrainProfile.Rolling
        });
        
        trials.Add(new TrialConfig 
        { 
            trialName = "Trial 3 - Hilly",
            lengthMeters = 750f,
            terrainProfile = ImprovedTerrainGenerator.TerrainProfile.Hilly
        });
        
        Debug.Log("[TrialSequence] Created 3 default trials");
    }
    
    private void Update()
    {
        if (!sequenceActive) return;
        
        // --- VAS ratings phase: wait for participant to complete both scales ---
        if (showingVAS)
        {
            CheckVASProgress();
            return; // block everything else while VAS is showing
        }
        
        // --- Inter-trial pause (after VAS, before next trial starts) ---
        if (waitingForNextTrial)
        {
            if (Time.time >= pauseEndTime)
            {
                waitingForNextTrial = false;
                StartNextTrial();
            }
            return;
        }
        
        // --- Active trial ---
        if (trialInProgress)
        {
            CheckTrialCompletion();
        }
        
        HandleManualControls();
    }
    
    // ── VAS flow ──────────────────────────────────────────────────────────────
    
    private void BeginVASPhase()
    {
        showingVAS = true;
        vasStep = 0;
        
        bool needPain   = showPainVASAfterTrial   && vasScale != null;
        bool needReward = showRewardVASAfterTrial  && vasScale != null;
        
        if (needPain)
        {
            vasScale.TriggerVASNow("Pain");
            Debug.Log("[TrialSequence] VAS: showing Pain scale");
        }
        else if (needReward)
        {
            vasStep = 1;
            vasScale.TriggerVASNow("Reward");
            Debug.Log("[TrialSequence] VAS: showing Reward scale (no Pain)");
        }
        else
        {
            // Nothing to show – go straight to next trial
            FinishVASPhase();
        }
    }
    
    private void CheckVASProgress()
    {
        if (vasScale == null || vasScale.IsShowing()) return; // still waiting for response
        
        if (vasStep == 0 && showRewardVASAfterTrial)
        {
            // Pain done → show Reward
            vasStep = 1;
            vasScale.TriggerVASNow("Reward");
            Debug.Log("[TrialSequence] VAS: Pain done, showing Reward scale");
        }
        else
        {
            // All VAS done
            FinishVASPhase();
        }
    }
    
    private void FinishVASPhase()
    {
        showingVAS = false;
        Debug.Log("[TrialSequence] VAS phase complete, advancing to next trial");
        
        if (currentTrialIndex < trials.Count - 1)
        {
            StartInterTrialPause();
        }
        else
        {
            CompleteSequence();
        }
    }
    
    private void HandleManualControls()
    {
        // Skip to next trial
        if (Input.GetKeyDown(KeyCode.N) && trialInProgress)
        {
            ForceCompleteTrial();
        }
        
        // Restart current trial
        if (Input.GetKeyDown(KeyCode.R) && trialInProgress)
        {
            RestartCurrentTrial();
        }
        
        // Abort sequence
        if (Input.GetKeyDown(KeyCode.Escape) && sequenceActive)
        {
            AbortSequence();
        }
    }
    
    [ContextMenu("Start Trial Sequence")]
    public void StartSequence()
    {
        if (trials.Count == 0)
        {
            Debug.LogError("[TrialSequence] No trials configured!");
            return;
        }
        
        sequenceActive = true;
        currentTrialIndex = 0;
        completedTrials.Clear();
        
        if (showDebugInfo)
        {
            Debug.Log($"[TrialSequence] Starting sequence with {trials.Count} trials");
        }
        
        StartTrial(currentTrialIndex);
    }
    
    private void StartTrial(int index)
    {
        if (index >= trials.Count)
        {
            CompleteSequence();
            return;
        }
        
        TrialConfig trial = trials[index];
        
        // Generate terrain
        if (regenerateTerrainBetweenTrials && terrainGenerator != null)
        {
            // Set seed if specified
            if (trial.terrainSeed > 0)
            {
                UnityEngine.Random.InitState(trial.terrainSeed);
            }
            
            terrainGenerator.GenerateTerrain();
        }
        
        // Reset bike position if needed
        if (bikeController != null)
        {
            trialStartDistance = bikeController.GetDistance();
            trialStartTime = Time.time;
            bikeController.SetMaxCourseDistance(trialStartDistance + trial.lengthMeters);
        }
        
        // Start data logging
        if (dataLogger != null && !dataLogger.IsLogging())
        {
            dataLogger.SetTrialInfo($"Sequence_{index + 1}", index + 1);
            dataLogger.StartLogging();
        }
        
        trialInProgress = true;
        
        // Show trial start notification
        ShowTrialStartNotification(trial);
        
        if (showDebugInfo)
        {
            Debug.Log($"\n{'='*60}");
            Debug.Log($"TRIAL {index + 1}/{trials.Count} STARTED");
            Debug.Log($"{'='*60}");
            Debug.Log($"Name: {trial.trialName}");
            Debug.Log($"Length: {trial.lengthMeters}m");
            Debug.Log($"Terrain: {trial.terrainProfile}");
            if (!string.IsNullOrEmpty(trial.notes))
                Debug.Log($"Notes: {trial.notes}");
            Debug.Log($"{'='*60}\n");
        }
    }
    
    private void CheckTrialCompletion()
    {
        if (bikeController == null) return;
        
        TrialConfig currentTrial = trials[currentTrialIndex];
        float currentDistance = bikeController.GetDistance();
        float distanceInTrial = currentDistance - trialStartDistance;
        
        // Check if trial is complete
        if (distanceInTrial >= currentTrial.lengthMeters)
        {
            CompleteTrial();
        }
    }
    
    private void CompleteTrial()
    {
        if (!trialInProgress) return;
        
        trialInProgress = false;
        
        TrialConfig trial = trials[currentTrialIndex];
        float duration = Time.time - trialStartTime;
        
        // Calculate statistics
        TrialResult result = new TrialResult
        {
            trialName = trial.trialName,
            lengthMeters = trial.lengthMeters,
            durationSeconds = duration,
            averageSpeedKmh = (trial.lengthMeters / duration) * 3.6f,
            averagePowerWatts = bikeController != null ? bikeController.GetPower() : 0f,
            averageGradient = bikeController != null ? bikeController.GetGradient() : 0f,
            completionTime = DateTime.Now
        };
        
        completedTrials.Add(result);
        
        // Stop logging
        if (dataLogger != null && dataLogger.IsLogging())
        {
            dataLogger.StopLogging();
        }
        
        // Show completion
        if (showCompletionAfterEachTrial)
        {
            ShowTrialCompletionNotification(result);
        }
        
        LogTrialCompletion(result);
        
        // Show VAS ratings before advancing to next trial
        BeginVASPhase();
    }
    
    private void ForceCompleteTrial()
    {
        if (showDebugInfo)
            Debug.Log("[TrialSequence] Force completing current trial");
        
        CompleteTrial();
    }
    
    private void StartInterTrialPause()
    {
        waitingForNextTrial = true;
        pauseEndTime = Time.time + interTrialPause;
        
        if (showDebugInfo)
        {
            Debug.Log($"[TrialSequence] Inter-trial pause: {interTrialPause}s");
        }
        
        ShowInterTrialPauseNotification();
    }
    
    private void StartNextTrial()
    {
        currentTrialIndex++;
        StartTrial(currentTrialIndex);
    }
    
    private void RestartCurrentTrial()
    {
        if (showDebugInfo)
            Debug.Log("[TrialSequence] Restarting current trial");
        
        trialInProgress = false;
        StartTrial(currentTrialIndex);
    }
    
    private void CompleteSequence()
    {
        sequenceActive = false;
        trialInProgress = false;
        
        if (showDebugInfo)
        {
            Debug.Log($"\n{'='*60}");
            Debug.Log($"SEQUENCE COMPLETE!");
            Debug.Log($"{'='*60}");
            Debug.Log($"Trials Completed: {completedTrials.Count}/{trials.Count}");
            Debug.Log($"{'='*60}\n");
        }
        
        if (showFinalSummary)
        {
            ShowFinalSummary();
        }
        
        LogFinalStatistics();
    }
    
    private void AbortSequence()
    {
        sequenceActive = false;
        trialInProgress = false;
        
        if (dataLogger != null && dataLogger.IsLogging())
        {
            dataLogger.StopLogging();
        }
        
        Debug.Log("[TrialSequence] Sequence aborted by user");
    }

    // Notification methods
    private void ShowTrialStartNotification(TrialConfig trial)
    {
        VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
        if (feedback != null)
        {
            feedback.ShowNotification(
                $"{trial.trialName}\n{trial.lengthMeters}m - {trial.terrainProfile}",
                Color.cyan
            );
        }
    }
    
    private void ShowTrialCompletionNotification(TrialResult result)
    {
        VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
        if (feedback != null)
        {
            feedback.ShowNotification(
                $"{result.trialName} Complete!\n{result.durationSeconds:F1}s - {result.averageSpeedKmh:F1} km/h",
                Color.green
            );
        }
        
        SimpleCompletionOverlay overlay = FindObjectOfType<SimpleCompletionOverlay>();
        if (overlay != null)
        {
            overlay.ShowCompletionOverlay(result.durationSeconds, result.lengthMeters);
        }
    }
    
    private void ShowInterTrialPauseNotification()
    {
        VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
        if (feedback != null)
        {
            feedback.ShowNotification(
                $"Next trial in {interTrialPause:F0}s...",
                Color.yellow
            );
        }
    }
    
    private void ShowWaitingForNextTrialMessage()
    {
        Debug.Log("[TrialSequence] Press 'N' to start next trial");
    }
    
    private void ShowFinalSummary()
    {
        VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
        if (feedback != null)
        {
            feedback.ShowNotification(
                $"All Trials Complete!\n{completedTrials.Count} trials finished",
                Color.green
            );
        }
    }
    
    // Logging methods
    private void LogSequenceConfiguration()
    {
        if (!showDebugInfo) return;
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"TRIAL SEQUENCE CONFIGURATION");
        Debug.Log($"{'='*60}");
        Debug.Log($"Total Trials: {trials.Count}");
        Debug.Log($"Auto-Start: {autoStartFirstTrial}");
        Debug.Log($"Auto-Advance: {autoAdvanceTrials}");
        Debug.Log($"Inter-Trial Pause: {interTrialPause}s");
        Debug.Log($"\nTrial List:");
        
        for (int i = 0; i < trials.Count; i++)
        {
            TrialConfig trial = trials[i];
            Debug.Log($"  {i + 1}. {trial.trialName}");
            Debug.Log($"     Length: {trial.lengthMeters}m");
            Debug.Log($"     Terrain: {trial.terrainProfile}");
            if (trial.terrainSeed > 0)
                Debug.Log($"     Seed: {trial.terrainSeed}");
        }
        
        Debug.Log($"{'='*60}\n");
    }
    
    private void LogTrialCompletion(TrialResult result)
    {
        if (!showDebugInfo) return;
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"TRIAL {currentTrialIndex + 1} COMPLETE");
        Debug.Log($"{'='*60}");
        Debug.Log($"Name: {result.trialName}");
        Debug.Log($"Duration: {result.durationSeconds:F1}s");
        Debug.Log($"Distance: {result.lengthMeters:F0}m");
        Debug.Log($"Avg Speed: {result.averageSpeedKmh:F1} km/h");
        Debug.Log($"Avg Power: {result.averagePowerWatts:F0}W");
        Debug.Log($"Avg Gradient: {result.averageGradient:F1}%");
        Debug.Log($"{'='*60}\n");
    }
    
    private void LogFinalStatistics()
    {
        if (!showDebugInfo) return;
        
        float totalDistance = 0f;
        float totalDuration = 0f;
        float totalPower = 0f;
        
        foreach (TrialResult result in completedTrials)
        {
            totalDistance += result.lengthMeters;
            totalDuration += result.durationSeconds;
            totalPower += result.averagePowerWatts;
        }
        
        float overallAvgSpeed = (totalDistance / totalDuration) * 3.6f;
        float overallAvgPower = totalPower / completedTrials.Count;
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"FINAL SEQUENCE STATISTICS");
        Debug.Log($"{'='*60}");
        Debug.Log($"Trials Completed: {completedTrials.Count}/{trials.Count}");
        Debug.Log($"Total Distance: {totalDistance:F0}m");
        Debug.Log($"Total Duration: {totalDuration:F1}s ({totalDuration / 60f:F1} minutes)");
        Debug.Log($"Overall Avg Speed: {overallAvgSpeed:F1} km/h");
        Debug.Log($"Overall Avg Power: {overallAvgPower:F0}W");
        Debug.Log($"\nIndividual Trial Results:");
        
        for (int i = 0; i < completedTrials.Count; i++)
        {
            TrialResult result = completedTrials[i];
            Debug.Log($"\n  Trial {i + 1}: {result.trialName}");
            Debug.Log($"    {result.lengthMeters:F0}m in {result.durationSeconds:F1}s");
            Debug.Log($"    {result.averageSpeedKmh:F1} km/h @ {result.averagePowerWatts:F0}W");
        }
        
        Debug.Log($"\n{'='*60}\n");
    }
    
    // Public API
    public void AddTrial(string name, float lengthMeters, ImprovedTerrainGenerator.TerrainProfile profile)
    {
        trials.Add(new TrialConfig
        {
            trialName = name,
            lengthMeters = lengthMeters,
            terrainProfile = profile
        });
        
        if (showDebugInfo)
            Debug.Log($"[TrialSequence] Added trial: {name} ({lengthMeters}m)");
    }
    
    public void ClearTrials()
    {
        trials.Clear();
        if (showDebugInfo)
            Debug.Log("[TrialSequence] Cleared all trials");
    }
    
    public int GetCurrentTrialIndex()
    {
        return currentTrialIndex;
    }
    
    public int GetTotalTrials()
    {
        return trials.Count;
    }
    
    public int GetCompletedTrialsCount()
    {
        return completedTrials.Count;
    }
    
    public float GetCurrentTrialProgress()
    {
        if (!trialInProgress || bikeController == null) return 0f;
        
        TrialConfig currentTrial = trials[currentTrialIndex];
        float currentDistance = bikeController.GetDistance();
        float distanceInTrial = currentDistance - trialStartDistance;
        
        return Mathf.Clamp01(distanceInTrial / currentTrial.lengthMeters);
    }
    
    public TrialConfig GetCurrentTrial()
    {
        if (currentTrialIndex >= 0 && currentTrialIndex < trials.Count)
            return trials[currentTrialIndex];
        return null;
    }
    
    public List<TrialResult> GetCompletedTrials()
    {
        return new List<TrialResult>(completedTrials);
    }
    
    public bool IsSequenceActive()
    {
        return sequenceActive;
    }
    
    public bool IsTrialInProgress()
    {
        return trialInProgress;
    }
    
    // GUI for debugging
    private void OnGUI()
    {
        if (!showDebugInfo || !sequenceActive) return;
        
        int y = 150;
        GUI.Label(new Rect(10, y, 400, 20), $"Trial: {currentTrialIndex + 1}/{trials.Count}");
        y += 20;
        
        if (trialInProgress)
        {
            TrialConfig trial = trials[currentTrialIndex];
            float progress = GetCurrentTrialProgress();
            GUI.Label(new Rect(10, y, 400, 20), $"{trial.trialName}: {progress * 100f:F0}%");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"{progress * trial.lengthMeters:F0}m / {trial.lengthMeters:F0}m");
        }
        else if (waitingForNextTrial)
        {
            float timeLeft = pauseEndTime - Time.time;
            GUI.Label(new Rect(10, y, 400, 20), $"Next trial in {timeLeft:F1}s...");
        }
        
        y += 30;
        GUI.Label(new Rect(10, y, 400, 20), "N: Next Trial | R: Restart | ESC: Abort");
    }
}
