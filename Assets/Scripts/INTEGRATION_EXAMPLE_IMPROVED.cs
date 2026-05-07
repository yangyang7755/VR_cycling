using UnityEngine;

/// <summary>
/// Example integration showing how to use all improved components together
/// This demonstrates common usage patterns and best practices
/// </summary>
public class INTEGRATION_EXAMPLE_IMPROVED : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private ImprovedTerrainGenerator terrainGenerator;
    [SerializeField] private DynamicTerrainRandomizer terrainRandomizer;
    [SerializeField] private ModernCyclingUI modernUI;
    [SerializeField] private VisualFeedbackSystem visualFeedback;
    [SerializeField] private BikeController bikeController;
    
    private void Start()
    {
        // Auto-find components if not assigned
        if (terrainGenerator == null)
            terrainGenerator = FindObjectOfType<ImprovedTerrainGenerator>();
        
        if (terrainRandomizer == null)
            terrainRandomizer = FindObjectOfType<DynamicTerrainRandomizer>();
        
        if (modernUI == null)
            modernUI = FindObjectOfType<ModernCyclingUI>();
        
        if (visualFeedback == null)
            visualFeedback = FindObjectOfType<VisualFeedbackSystem>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        // Example: Start a new session
        StartNewSession();
    }
    
    /// <summary>
    /// Example: Start a new training session
    /// </summary>
    public void StartNewSession()
    {
        Debug.Log("=== Starting New Training Session ===");
        
        // Generate new random terrain
        if (terrainGenerator != null)
        {
            terrainGenerator.GenerateTerrain();
            Debug.Log("✓ Generated new terrain");
        }
        
        // Reset UI
        if (modernUI != null)
        {
            modernUI.ResetSession();
            Debug.Log("✓ Reset UI session timer");
        }
        
        // Reset visual feedback milestones
        if (visualFeedback != null)
        {
            visualFeedback.ResetMilestones();
            Debug.Log("✓ Reset achievement milestones");
        }
        
        // Show welcome notification
        if (visualFeedback != null)
        {
            visualFeedback.ShowNotification("Session Started!", Color.green);
        }
    }
    
    /// <summary>
    /// Example: Start an experiment trial with specific terrain
    /// </summary>
    public void StartExperimentTrial(int trialNumber, int terrainSeed)
    {
        Debug.Log($"=== Starting Experiment Trial {trialNumber} ===");
        
        // Generate terrain with specific seed for reproducibility
        if (terrainGenerator != null)
        {
            // Note: You'll need to add a method to set seed in ImprovedTerrainGenerator
            terrainGenerator.GenerateTerrain();
            Debug.Log($"✓ Generated terrain with seed: {terrainSeed}");
        }
        
        // Disable randomization during experiments
        if (terrainRandomizer != null)
        {
            // terrainRandomizer.enablePeriodicRandomization = false;
            Debug.Log("✓ Disabled terrain randomization");
        }
        
        // Reset everything
        if (modernUI != null)
            modernUI.ResetSession();
        
        if (visualFeedback != null)
            visualFeedback.ResetMilestones();
        
        Debug.Log($"Trial {trialNumber} ready to start");
    }
    
    /// <summary>
    /// Example: Generate specific terrain type
    /// </summary>
    public void GenerateSpecificTerrain(string terrainType)
    {
        if (terrainGenerator == null) return;
        
        Debug.Log($"Generating {terrainType} terrain...");
        
        // Set terrain profile based on type
        // Note: You'll need to make terrainProfile public or add setter method
        terrainGenerator.GenerateTerrain();
        
        if (visualFeedback != null)
        {
            visualFeedback.ShowNotification($"{terrainType} Route Generated!", Color.cyan);
        }
    }
    
    /// <summary>
    /// Example: Handle session completion
    /// </summary>
    public void CompleteSession()
    {
        Debug.Log("=== Session Complete ===");
        
        if (bikeController != null)
        {
            float distance = bikeController.GetDistance();
            float avgSpeed = distance / (Time.time - Time.realtimeSinceStartup);
            
            Debug.Log($"Total Distance: {distance:F0}m");
            Debug.Log($"Average Speed: {avgSpeed * 3.6f:F1} km/h");
        }
        
        if (visualFeedback != null)
        {
            visualFeedback.ShowNotification("Session Complete!", Color.green);
        }
    }
    
    /// <summary>
    /// Example: Keyboard shortcuts for testing
    /// </summary>
    private void Update()
    {
        // Press R to randomize terrain
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (terrainRandomizer != null)
            {
                terrainRandomizer.RandomizeTerrain();
                Debug.Log("Terrain randomized!");
            }
        }
        
        // Press N to start new session
        if (Input.GetKeyDown(KeyCode.N))
        {
            StartNewSession();
        }
        
        // Press M to show test notification
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (visualFeedback != null)
            {
                visualFeedback.ShowNotification("Test Message!", Color.yellow);
            }
        }
    }
    
    /// <summary>
    /// Example: Custom terrain configuration
    /// </summary>
    public void ConfigureCustomTerrain(float maxElevation, int steepSections, int recoverySections)
    {
        if (terrainGenerator == null) return;
        
        Debug.Log("Configuring custom terrain...");
        
        // Set custom parameters
        // Note: You'll need to add public setters or make fields public
        
        terrainGenerator.GenerateTerrain();
        
        Debug.Log($"Custom terrain generated: {maxElevation}m elevation, {steepSections} climbs, {recoverySections} recovery zones");
    }
    
    /// <summary>
    /// Example: Monitor rider performance and provide feedback
    /// </summary>
    private float lastPowerCheckTime = 0f;
    
    private void MonitorPerformance()
    {
        if (Time.time - lastPowerCheckTime < 5f) return;
        lastPowerCheckTime = Time.time;
        
        if (bikeController == null || visualFeedback == null) return;
        
        float power = bikeController.GetPower();
        float gradient = bikeController.GetGradient();
        
        // Provide contextual feedback
        if (gradient > 8f && power < 150f)
        {
            visualFeedback.ShowNotification("Steep climb - increase power!", Color.yellow);
        }
        else if (gradient < -3f && power > 200f)
        {
            visualFeedback.ShowNotification("Downhill - recover!", Color.cyan);
        }
        else if (power > 300f)
        {
            visualFeedback.ShowNotification("High power - great effort!", Color.red);
        }
    }
}

/*
 * USAGE EXAMPLES:
 * 
 * 1. Simple Training Session:
 *    - Add this script to scene
 *    - Press Play
 *    - Press N to start new session with random terrain
 *    - Press R to randomize terrain during ride
 * 
 * 2. Experiment Trial:
 *    - Call StartExperimentTrial(trialNumber, seed) from your trial manager
 *    - Terrain will be reproducible with same seed
 *    - Randomization disabled for consistency
 * 
 * 3. Custom Workout:
 *    - Call GenerateSpecificTerrain("Hilly") for specific type
 *    - Or ConfigureCustomTerrain() for precise control
 * 
 * 4. Integration with Existing Code:
 *    - Call StartNewSession() when starting a trial
 *    - Call CompleteSession() when finishing
 *    - Use keyboard shortcuts for testing
 * 
 * KEYBOARD SHORTCUTS:
 * - R: Randomize terrain
 * - N: New session
 * - M: Test notification
 * - T: Show trial starter (if TrialStarterUI present)
 */
