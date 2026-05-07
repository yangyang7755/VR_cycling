using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;

public class HillTrialManager : MonoBehaviour
{
    [Header("Trial Configuration")]
    public List<HillConfiguration> trialConfigurations = new List<HillConfiguration>();
    public int currentTrialIndex = 0;

    [Header("References")]
    public SimpleTerrainBuilder terrainBuilder;
    public BikeController bicycleController;
    // Note: RealBike and HillConfigurationUI are optional - comment out if not available
    // public RealBike realBike;
    // public HillConfigurationUI hillConfigurationUI;

    [Header("Auto Apply")]
    public bool autoApplyOnStart = false;

    [Header("Preset Trials")]
    [Tooltip("If true, creates preset trials on start (Trial 1: Flat, Trials 2-5: Undulating with increasing gradients)")]
    public bool usePresetTrials = true;

    private void Start()
    {
        if (terrainBuilder == null)
            terrainBuilder = FindObjectOfType<SimpleTerrainBuilder>();

        if (bicycleController == null)
            bicycleController = FindObjectOfType<BikeController>();

        // Optional references - uncomment if these classes exist in your project
        // if (realBike == null)
        //     realBike = FindObjectOfType<RealBike>();

        // if (hillConfigurationUI == null)
        //     hillConfigurationUI = FindObjectOfType<HillConfigurationUI>();

        if (usePresetTrials && trialConfigurations.Count == 0)
        {
            InitializePresetTrials();
        }
        else if (trialConfigurations.Count == 0)
        {
            trialConfigurations.Add(new HillConfiguration());
        }

        if (autoApplyOnStart)
        {
            ApplyCurrentTrial();
        }
    }

    /// <summary>
    /// Initialize preset trials:
    /// Trial 1: Flat terrain (0% gradient, no undulations)
    /// Trial 2: Undulating terrain with 2.5% average gradient
    /// Trial 3: Undulating terrain with 3.6% average gradient
    /// Trial 4: Undulating terrain with 5.8% average gradient
    /// Trial 5: Undulating terrain with 10% average gradient
    /// </summary>
    private void InitializePresetTrials()
    {
        trialConfigurations.Clear();

        // Trial 1: Flat terrain (no gradient, no undulations)
        HillConfiguration trial1 = new HillConfiguration();
        trial1.length = 500f;
        trial1.physicsGradient = 0f;
        trial1.visualGradient = 0f;
        trial1.startTransition = 5f;
        trial1.endTransition = 5f;
        trial1.enableUndulations = false;
        trial1.undulationAmplitude = 0f;
        trial1.undulationFrequency = 0.15f;
        trial1.undulationSeed = 0;
        trialConfigurations.Add(trial1);

        // Trial 2: Undulating terrain with reduced average gradient, increased undulation frequency
        // Hill extends to 450m (50m flat at end)
        HillConfiguration trial2 = new HillConfiguration();
        trial2.length = 500f;
        trial2.physicsGradient = 1.5f; // PHYSICS: Keep at 1.5% (affects bike resistance)
        trial2.visualGradient = 3.0f; // VISUAL: Slightly higher than physics (1.5% * 2 = 3.0%) for visibility
        trial2.startTransition = 10f;
        trial2.endTransition = 50f; // Hill extends to 450m, then 50m transition to flat (reduced flat section)
        trial2.enableUndulations = true;
        trial2.undulationAmplitude = 1.5f;
        trial2.undulationFrequency = 0.25f; // Increased from 0.15f to 0.25f (more frequent undulations)
        trial2.undulationSeed = 1000;
        trialConfigurations.Add(trial2);

        // Trial 3: Undulating terrain with reduced average gradient, increased undulation frequency
        // Hill extends to 450m (50m flat at end)
        HillConfiguration trial3 = new HillConfiguration();
        trial3.length = 500f;
        trial3.physicsGradient = 2.0f; // PHYSICS: Keep at 2.0% (affects bike resistance)
        trial3.visualGradient = 4.5f; // VISUAL: Slightly higher than physics (2.0% * 2.25 = 4.5%) for visibility
        trial3.startTransition = 10f;
        trial3.endTransition = 50f; // Hill extends to 450m, then 50m transition to flat (reduced flat section)
        trial3.enableUndulations = true;
        trial3.undulationAmplitude = 2.0f;
        trial3.undulationFrequency = 0.30f; // Increased from 0.18f to 0.30f (more frequent undulations)
        trial3.undulationSeed = 2000;
        trialConfigurations.Add(trial3);

        // Trial 4: Undulating terrain with reduced average gradient, increased undulation frequency
        // Hill extends to 450m (50m flat at end)
        HillConfiguration trial4 = new HillConfiguration();
        trial4.length = 500f;
        trial4.physicsGradient = 3.0f; // PHYSICS: Keep at 3.0% (affects bike resistance)
        trial4.visualGradient = 6.5f; // VISUAL: Slightly higher than physics (3.0% * 2.17 = 6.5%) for visibility
        trial4.startTransition = 10f;
        trial4.endTransition = 50f; // Hill extends to 450m, then 50m transition to flat (reduced flat section)
        trial4.enableUndulations = true;
        trial4.undulationAmplitude = 2.5f;
        trial4.undulationFrequency = 0.35f; // Increased from 0.20f to 0.35f (more frequent undulations)
        trial4.undulationSeed = 3000;
        trialConfigurations.Add(trial4);

        // Trial 5: Undulating terrain with maximum 12% gradient (cap at 12% as per requirements)
        HillConfiguration trial5 = new HillConfiguration();
        trial5.length = 500f;
        trial5.physicsGradient = 8f; // Reduced from 10% to 8% to stay within 12% limit when visual multiplier is applied
        trial5.visualGradient = 12f; // Maximum 12% visual gradient (reduced from 20% to comply with requirement)
        trial5.startTransition = 20f;
        trial5.endTransition = 20f;
        trial5.enableUndulations = true;
        trial5.undulationAmplitude = 3.0f;
        trial5.undulationFrequency = 0.22f;
        trial5.undulationSeed = 4000;
        trialConfigurations.Add(trial5);

        currentTrialIndex = 0;
        Debug.Log($"Initialized {trialConfigurations.Count} preset trials:");
        for (int i = 0; i < trialConfigurations.Count; i++)
        {
            Debug.Log($"  Trial {i + 1}: {trialConfigurations[i]}");
        }
    }

    public void AddNewTrial()
    {
        HillConfiguration newTrial = new HillConfiguration();
        if (trialConfigurations.Count > 0)
        {
            newTrial = trialConfigurations[trialConfigurations.Count - 1].Clone();
        }
        trialConfigurations.Add(newTrial);
        currentTrialIndex = trialConfigurations.Count - 1;
        UpdateUI();
    }

    public void RemoveCurrentTrial()
    {
        if (trialConfigurations.Count <= 1)
        {
            Debug.LogWarning("Cannot remove the last trial. At least one trial must exist.");
            return;
        }

        trialConfigurations.RemoveAt(currentTrialIndex);
        if (currentTrialIndex >= trialConfigurations.Count)
        {
            currentTrialIndex = trialConfigurations.Count - 1;
        }
        UpdateUI();
    }

    public void SelectTrial(int index)
    {
        if (index >= 0 && index < trialConfigurations.Count)
        {
            currentTrialIndex = index;
            UpdateUI();
        }
    }

    public void NextTrial()
    {
        currentTrialIndex = (currentTrialIndex + 1) % trialConfigurations.Count;
        UpdateUI();
    }

    public void PreviousTrial()
    {
        currentTrialIndex = (currentTrialIndex - 1 + trialConfigurations.Count) % trialConfigurations.Count;
        UpdateUI();
    }

    public void ApplyCurrentTrial()
    {
        if (currentTrialIndex < 0 || currentTrialIndex >= trialConfigurations.Count)
        {
            Debug.LogError("Invalid trial index");
            return;
        }

        HillConfiguration config = trialConfigurations[currentTrialIndex];
        ApplyTrialConfiguration(config);
    }

    public void ApplyTrialConfiguration(HillConfiguration config)
    {
        if (config == null)
        {
            Debug.LogError("HillConfiguration is null");
            return;
        }

        if (terrainBuilder == null)
        {
            Debug.LogError("TerrainBuilder not found");
            return;
        }

        // Apply terrain configuration using SimpleTerrainBuilder
        if (terrainBuilder != null)
        {
            // Set gradient (visual gradient is used for terrain appearance)
            // Clamp to maximum 12% as per requirement
            float clampedVisualGradient = Mathf.Clamp(config.visualGradient, 0f, 12f);
            if (config.visualGradient > 12f)
            {
                Debug.LogWarning($"[HillTrialManager] Visual gradient {config.visualGradient}% exceeds 12% limit. Clamping to {clampedVisualGradient}%");
            }
            // Note: SetGradient expects percentage (0-12), not decimal
            terrainBuilder.SetGradient(clampedVisualGradient);
            
            // Set transition parameters if available
            var startTransitionField = terrainBuilder.GetType().GetField("startTransition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var endTransitionField = terrainBuilder.GetType().GetField("endTransition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            
            if (startTransitionField != null)
            {
                startTransitionField.SetValue(terrainBuilder, config.startTransition);
            }
            if (endTransitionField != null)
            {
                endTransitionField.SetValue(terrainBuilder, config.endTransition);
            }
            
            // Set undulation parameters if available (for rolling terrain)
            var setUndulationMethod = terrainBuilder.GetType().GetMethod("SetUndulationParameters");
            if (setUndulationMethod != null)
            {
                setUndulationMethod.Invoke(terrainBuilder, new object[] { 
                    config.enableUndulations, 
                    config.undulationAmplitude, 
                    config.undulationFrequency, 
                    config.undulationSeed 
                });
                
                Debug.Log($"[HillTrialManager] Applied undulation parameters: Enable={config.enableUndulations}, Amplitude={config.undulationAmplitude}m, Frequency={config.undulationFrequency}, Seed={config.undulationSeed}");
            }
            else
            {
                Debug.LogWarning("[HillTrialManager] SetUndulationParameters method not found in SimpleTerrainBuilder! Rolling terrain will not be applied.");
            }
            
            // Regenerate terrain with new settings
            terrainBuilder.ClearTerrain();
            terrainBuilder.AddElevationProfile();
            terrainBuilder.SetupTextures();
            terrainBuilder.PaintRoad();
        }

        if (bicycleController != null)
        {
            // If undulations are enabled, use real slope from terrain
            // Otherwise, use fixed slope for flat terrain
            if (config.enableUndulations)
            {
                var useRealSlopeMethod = bicycleController.GetType().GetMethod("UseRealSlope");
                if (useRealSlopeMethod != null)
                {
                    useRealSlopeMethod.Invoke(bicycleController, null);
                }
            }
            else
            {
                var setFixedSlopeMethod = bicycleController.GetType().GetMethod("SetFixedSlope", new Type[] { typeof(float) });
                if (setFixedSlopeMethod != null)
                {
                    setFixedSlopeMethod.Invoke(bicycleController, new object[] { config.physicsGradient });
                }
            }
        }

        // Optional: RealBike integration (uncomment if available)
        // if (realBike != null)
        // {
        //     realBike.SetSlope(config.physicsGradient);
        // }

        Debug.Log($"Applied trial {currentTrialIndex + 1}: {config}");
    }

    public HillConfiguration GetCurrentTrial()
    {
        if (currentTrialIndex >= 0 && currentTrialIndex < trialConfigurations.Count)
        {
            return trialConfigurations[currentTrialIndex];
        }
        return null;
    }

    public void UpdateCurrentTrialFromUI()
    {
        // Optional: Update from UI if HillConfigurationUI exists
        // var hillConfigurationUI = FindObjectOfType<MonoBehaviour>().GetType().Name == "HillConfigurationUI" ? FindObjectOfType<MonoBehaviour>() : null;
        // if (hillConfigurationUI != null && currentTrialIndex >= 0 && currentTrialIndex < trialConfigurations.Count)
        // {
        //     var getConfigMethod = hillConfigurationUI.GetType().GetMethod("GetConfiguration");
        //     if (getConfigMethod != null)
        //     {
        //         trialConfigurations[currentTrialIndex] = (HillConfiguration)getConfigMethod.Invoke(hillConfigurationUI, null);
        //     }
        // }
    }

    private void UpdateUI()
    {
        // Optional: Update UI if HillConfigurationUI exists
        // var hillConfigurationUI = FindObjectOfType<MonoBehaviour>().GetType().Name == "HillConfigurationUI" ? FindObjectOfType<MonoBehaviour>() : null;
        // if (hillConfigurationUI != null && currentTrialIndex >= 0 && currentTrialIndex < trialConfigurations.Count)
        // {
        //     var setConfigMethod = hillConfigurationUI.GetType().GetMethod("SetConfiguration", new Type[] { typeof(HillConfiguration) });
        //     if (setConfigMethod != null)
        //     {
        //         setConfigMethod.Invoke(hillConfigurationUI, new object[] { trialConfigurations[currentTrialIndex] });
        //     }
        // }
    }

    public void SaveTrialConfigurations(string filePath)
    {
        try
        {
            SerializableTrialList wrapper = new SerializableTrialList();
            wrapper.trials = new List<HillConfiguration>();
            foreach (var config in trialConfigurations)
            {
                wrapper.trials.Add(config);
            }
            string json = JsonUtility.ToJson(wrapper, true);
            System.IO.File.WriteAllText(filePath, json);
            Debug.Log($"Saved {trialConfigurations.Count} trial configurations to {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving trial configurations: {e.Message}");
        }
    }

    public void LoadTrialConfigurations(string filePath)
    {
        if (System.IO.File.Exists(filePath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(filePath);
                SerializableTrialList loaded = JsonUtility.FromJson<SerializableTrialList>(json);
                if (loaded != null && loaded.trials != null)
                {
                    trialConfigurations = loaded.trials;
                    currentTrialIndex = 0;
                    UpdateUI();
                    Debug.Log($"Loaded {trialConfigurations.Count} trial configurations from {filePath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading trial configurations: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"File not found: {filePath}");
        }
    }

    [Serializable]
    private class SerializableTrialList
    {
        public List<HillConfiguration> trials = new List<HillConfiguration>();
    }
}
