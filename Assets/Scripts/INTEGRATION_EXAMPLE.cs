using UnityEngine;

/// <summary>
/// Example integration showing how to use the experimental trial system
/// with your existing bike controller and UI
/// 
/// This is a REFERENCE EXAMPLE - adapt to your needs
/// </summary>
public class INTEGRATION_EXAMPLE : MonoBehaviour
{
    // ============================================================================
    // EXAMPLE 1: Basic Setup
    // ============================================================================
    
    void Example1_BasicSetup()
    {
        // 1. Add ProceduralRouteGenerator to scene
        GameObject routeGenObj = new GameObject("RouteGenerator");
        ProceduralRouteGenerator routeGen = routeGenObj.AddComponent<ProceduralRouteGenerator>();
        
        // 2. Configure it
        // (Set these in Inspector or via code)
        // routeGen.routeLength = 500f;
        // routeGen.targetGradients = new float[] { 0f, 3f, 5f, 7f, 10f, 15f };
        
        // 3. Generate a random route
        routeGen.RandomizeAndGenerate();
        
        // 4. Get the gradient that was selected
        float selectedGradient = routeGen.GetCurrentGradient();
        Debug.Log($"Generated route with {selectedGradient}% gradient");
    }
    
    // ============================================================================
    // EXAMPLE 2: Manual Gradient Selection
    // ============================================================================
    
    void Example2_ManualGradient()
    {
        ProceduralRouteGenerator routeGen = FindObjectOfType<ProceduralRouteGenerator>();
        
        // Generate route with specific gradient
        routeGen.SetGradientAndGenerate(5f); // 5% gradient
        
        // Check accuracy
        float actual = routeGen.GetActualGradient();
        Debug.Log($"Target: 5%, Actual: {actual:F2}%");
    }
    
    // ============================================================================
    // EXAMPLE 3: Full Experimental Session
    // ============================================================================
    
    void Example3_ExperimentalSession()
    {
        // 1. Setup trial manager
        GameObject managerObj = new GameObject("TrialManager");
        ExperimentalTrialManager trialManager = managerObj.AddComponent<ExperimentalTrialManager>();
        
        // 2. Configure participant
        // trialManager.participantID = "P001";
        // trialManager.repetitionsPerGradient = 3;
        // trialManager.randomizeTrialOrder = true;
        
        // 3. Generate trial sequence (normally done with 'G' key)
        trialManager.GenerateTrialSequence();
        
        // 4. Start first trial (normally done with 'N' key)
        trialManager.StartNextTrial();
        
        // 5. When participant finishes, end trial (normally done with 'E' key)
        // trialManager.EndCurrentTrial();
        
        // 6. Repeat steps 4-5 for all trials
    }
    
    // ============================================================================
    // EXAMPLE 4: Integration with BikeController
    // ============================================================================
    
    void Example4_BikeControllerIntegration()
    {
        BikeController bike = FindObjectOfType<BikeController>();
        ProceduralRouteGenerator routeGen = FindObjectOfType<ProceduralRouteGenerator>();
        
        // Generate route
        routeGen.SetGradientAndGenerate(7f);
        
        // Reset bike position to start
        // bike.ResetPosition(); // You may need to add this method
        
        // Get current gradient from bike (for display)
        float currentGradient = bike.GetGradient();
        Debug.Log($"Current gradient under bike: {currentGradient}%");
    }
    
    // ============================================================================
    // EXAMPLE 5: Integration with ElevationProgressBar
    // ============================================================================
    
    void Example5_ProgressBarIntegration()
    {
        ProceduralRouteGenerator routeGen = FindObjectOfType<ProceduralRouteGenerator>();
        ElevationProgressBar progressBar = FindObjectOfType<ElevationProgressBar>();
        
        // Generate route
        routeGen.SetGradientAndGenerate(10f);
        
        // Update progress bar with new route
        float routeLength = routeGen.GetRouteLength();
        progressBar.SetTotalRouteLength(routeLength);
        progressBar.ResetProgress();
        
        Debug.Log($"Progress bar updated for {routeLength}m route");
    }
    
    // ============================================================================
    // EXAMPLE 6: Custom Trial Sequence
    // ============================================================================
    
    void Example6_CustomSequence()
    {
        ProceduralRouteGenerator routeGen = FindObjectOfType<ProceduralRouteGenerator>();
        
        // Define your own sequence
        float[] customSequence = { 0f, 5f, 10f, 5f, 0f, 15f };
        
        // Run through sequence
        foreach (float gradient in customSequence)
        {
            routeGen.SetGradientAndGenerate(gradient);
            Debug.Log($"Generated {gradient}% gradient route");
            
            // Wait for participant to complete...
            // Then generate next one
        }
    }
    
    // ============================================================================
    // EXAMPLE 7: Accessing Terrain Data
    // ============================================================================
    
    void Example7_TerrainData()
    {
        ProceduralRouteGenerator routeGen = FindObjectOfType<ProceduralRouteGenerator>();
        
        // Generate route
        routeGen.SetGradientAndGenerate(5f);
        
        // Get statistics
        float targetGradient = routeGen.GetCurrentGradient();
        float actualGradient = routeGen.GetActualGradient();
        float error = Mathf.Abs(actualGradient - targetGradient);
        
        Debug.Log($"Target: {targetGradient}%");
        Debug.Log($"Actual: {actualGradient:F2}%");
        Debug.Log($"Error: {error:F2}%");
    }
    
    // ============================================================================
    // EXAMPLE 8: Event-Based Integration
    // ============================================================================
    
    // You could extend ExperimentalTrialManager to add events:
    /*
    public class ExtendedTrialManager : ExperimentalTrialManager
    {
        public event System.Action<float> OnTrialStarted;
        public event System.Action<float, float> OnTrialEnded;
        
        public new void StartNextTrial()
        {
            base.StartNextTrial();
            OnTrialStarted?.Invoke(GetCurrentGradient());
        }
        
        public new void EndCurrentTrial()
        {
            float duration = Time.time - trialStartTime;
            base.EndCurrentTrial();
            OnTrialEnded?.Invoke(GetCurrentGradient(), duration);
        }
    }
    */
    
    // ============================================================================
    // EXAMPLE 9: Validation and Testing
    // ============================================================================
    
    void Example9_Validation()
    {
        ProceduralRouteGenerator routeGen = FindObjectOfType<ProceduralRouteGenerator>();
        
        // Test all gradients
        float[] testGradients = { 0f, 3f, 5f, 7f, 10f, 15f };
        
        Debug.Log("Validating terrain generation...");
        foreach (float target in testGradients)
        {
            routeGen.SetGradientAndGenerate(target);
            float actual = routeGen.GetActualGradient();
            float error = Mathf.Abs(actual - target);
            
            string status = error < 0.2f ? "✓ PASS" : "✗ FAIL";
            Debug.Log($"{status} Target: {target}%, Actual: {actual:F2}%, Error: {error:F2}%");
        }
    }
    
    // ============================================================================
    // EXAMPLE 10: Complete Workflow
    // ============================================================================
    
    void Example10_CompleteWorkflow()
    {
        // This is what a complete experimental session looks like:
        
        // 1. SETUP PHASE
        ProceduralRouteGenerator routeGen = FindObjectOfType<ProceduralRouteGenerator>();
        ExperimentalTrialManager trialManager = FindObjectOfType<ExperimentalTrialManager>();
        BikeController bike = FindObjectOfType<BikeController>();
        ElevationProgressBar progressBar = FindObjectOfType<ElevationProgressBar>();
        
        // 2. GENERATE TRIAL SEQUENCE
        trialManager.GenerateTrialSequence();
        // This creates randomized order of all gradients × repetitions
        
        // 3. FOR EACH TRIAL:
        for (int trial = 0; trial < 18; trial++) // Example: 6 gradients × 3 reps
        {
            // a. Start trial
            trialManager.StartNextTrial();
            // This automatically generates terrain with next gradient
            
            // b. Update UI
            progressBar.ResetProgress();
            
            // c. Wait for participant to complete route
            // (In real code, this would be event-driven or distance-based)
            
            // d. End trial
            trialManager.EndCurrentTrial();
            // This logs data and prepares for next trial
            
            // e. Rest period
            // (Participant rests while you press 'N' for next trial)
        }
        
        // 4. SESSION COMPLETE
        // Data is automatically saved to CSV files
        // Summary statistics are generated
    }
    
    // ============================================================================
    // KEYBOARD SHORTCUTS REFERENCE
    // ============================================================================
    
    void Update()
    {
        // These are built into ExperimentalTrialManager:
        
        // G - Generate trial sequence
        // N - Start next trial  
        // E - End current trial
        
        // You can add your own shortcuts:
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Reset everything
            Debug.Log("Reset requested");
        }
        
        if (Input.GetKeyDown(KeyCode.P))
        {
            // Pause trial
            Debug.Log("Pause requested");
        }
    }
}
