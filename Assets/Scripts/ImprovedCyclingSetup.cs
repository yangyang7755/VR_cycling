using UnityEngine;

/// <summary>
/// Quick setup script for improved cycling simulation
/// Automatically configures all new components
/// Use this to quickly set up a scene with improved UI and terrain
/// </summary>
public class ImprovedCyclingSetup : MonoBehaviour
{
    [Header("Auto-Setup Options")]
    [SerializeField] private bool autoSetupOnStart = false;
    [SerializeField] private bool setupModernUI = true;
    [SerializeField] private bool setupImprovedTerrain = true;
    [SerializeField] private bool setupDynamicRandomizer = true;
    [SerializeField] private bool setupVisualFeedback = true;
    [SerializeField] private bool setupMiniMap = false;
    
    [Header("References (Auto-found if not assigned)")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Terrain terrain;
    [SerializeField] private Canvas uiCanvas;
    
    [Header("Terrain Settings")]
    [SerializeField] private ImprovedTerrainGenerator.TerrainProfile initialTerrainProfile = 
        ImprovedTerrainGenerator.TerrainProfile.Mixed;
    [SerializeField] private bool generateTerrainOnStart = true;
    
    private void Start()
    {
        if (autoSetupOnStart)
        {
            SetupAll();
        }
    }
    
    [ContextMenu("Setup All Components")]
    public void SetupAll()
    {
        Debug.Log("[ImprovedCyclingSetup] Starting automatic setup...");
        
        FindReferences();
        
        if (setupImprovedTerrain)
            SetupTerrainGenerator();
        
        if (setupDynamicRandomizer)
            SetupTerrainRandomizer();
        
        if (setupModernUI)
            SetupModernCyclingUI();
        
        if (setupVisualFeedback)
            SetupVisualFeedbackSystem();
        
        if (setupMiniMap)
            SetupMiniMapController();
        
        Debug.Log("[ImprovedCyclingSetup] ✓ Setup complete!");
    }
    
    private void FindReferences()
    {
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
            if (bikeController != null)
                Debug.Log("[ImprovedCyclingSetup] ✓ Found BikeController");
        }
        
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
            if (terrain != null)
                Debug.Log("[ImprovedCyclingSetup] ✓ Found Terrain");
        }
        
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas != null)
                Debug.Log("[ImprovedCyclingSetup] ✓ Found Canvas");
        }
    }
    
    private void SetupTerrainGenerator()
    {
        ImprovedTerrainGenerator generator = FindObjectOfType<ImprovedTerrainGenerator>();
        
        if (generator == null)
        {
            GameObject terrainObj = terrain != null ? terrain.gameObject : new GameObject("TerrainGenerator");
            generator = terrainObj.AddComponent<ImprovedTerrainGenerator>();
            Debug.Log("[ImprovedCyclingSetup] ✓ Added ImprovedTerrainGenerator");
        }
        
        if (generateTerrainOnStart)
        {
            generator.GenerateTerrain();
            Debug.Log("[ImprovedCyclingSetup] ✓ Generated initial terrain");
        }
    }
    
    private void SetupTerrainRandomizer()
    {
        DynamicTerrainRandomizer randomizer = FindObjectOfType<DynamicTerrainRandomizer>();
        
        if (randomizer == null)
        {
            ImprovedTerrainGenerator generator = FindObjectOfType<ImprovedTerrainGenerator>();
            if (generator != null)
            {
                randomizer = generator.gameObject.AddComponent<DynamicTerrainRandomizer>();
                Debug.Log("[ImprovedCyclingSetup] ✓ Added DynamicTerrainRandomizer");
            }
        }
    }
    
    private void SetupModernCyclingUI()
    {
        ModernCyclingUI modernUI = FindObjectOfType<ModernCyclingUI>();
        
        if (modernUI == null && uiCanvas != null)
        {
            GameObject uiObj = new GameObject("ModernCyclingUI");
            uiObj.transform.SetParent(uiCanvas.transform, false);
            modernUI = uiObj.AddComponent<ModernCyclingUI>();
            
            Debug.Log("[ImprovedCyclingSetup] ✓ Added ModernCyclingUI");
            Debug.LogWarning("[ImprovedCyclingSetup] Please manually create UI elements and assign references!");
        }
    }
    
    private void SetupVisualFeedbackSystem()
    {
        VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
        
        if (feedback == null && uiCanvas != null)
        {
            GameObject feedbackObj = new GameObject("VisualFeedbackSystem");
            feedbackObj.transform.SetParent(uiCanvas.transform, false);
            feedback = feedbackObj.AddComponent<VisualFeedbackSystem>();
            
            Debug.Log("[ImprovedCyclingSetup] ✓ Added VisualFeedbackSystem");
            Debug.LogWarning("[ImprovedCyclingSetup] Please manually create notification panel and assign references!");
        }
    }
    
    private void SetupMiniMapController()
    {
        MiniMapController miniMap = FindObjectOfType<MiniMapController>();
        
        if (miniMap == null)
        {
            GameObject miniMapObj = new GameObject("MiniMapController");
            miniMap = miniMapObj.AddComponent<MiniMapController>();
            
            Debug.Log("[ImprovedCyclingSetup] ✓ Added MiniMapController");
            Debug.LogWarning("[ImprovedCyclingSetup] Please manually create mini-map UI and assign references!");
        }
    }
    
    [ContextMenu("Generate New Random Terrain")]
    public void GenerateNewTerrain()
    {
        ImprovedTerrainGenerator generator = FindObjectOfType<ImprovedTerrainGenerator>();
        if (generator != null)
        {
            generator.GenerateTerrain();
            Debug.Log("[ImprovedCyclingSetup] ✓ Generated new terrain");
        }
    }
    
    [ContextMenu("Test Visual Feedback")]
    public void TestVisualFeedback()
    {
        VisualFeedbackSystem feedback = FindObjectOfType<VisualFeedbackSystem>();
        if (feedback != null)
        {
            feedback.ShowNotification("Test Notification!", Color.cyan);
        }
    }
}
