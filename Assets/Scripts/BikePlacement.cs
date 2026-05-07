using UnityEngine;

/// <summary>
/// Places bike at the start of the road and aligns wheels with rider
/// </summary>
public class BikePlacement : MonoBehaviour
{
    [Header("Bike References")]
    [SerializeField] private GameObject bikeObject;
    [SerializeField] private GameObject bikerModel;
    [SerializeField] private GameObject wheelsModel;

    [Header("Placement Settings")]
    [Tooltip("Place bike at start of road (left side)")]
    [SerializeField] private bool placeAtRoadStart = true;
    
    [Tooltip("Exact start position (set from correct position)")]
    [SerializeField] private Vector3 exactStartPosition = new Vector3(247.7f, 4f, 0f);
    
    [Tooltip("Exact start rotation")]
    [SerializeField] private Vector3 exactStartRotation = Vector3.zero;
    
    [Tooltip("Exact start scale")]
    [SerializeField] private Vector3 exactStartScale = new Vector3(3f, 3f, 3f);
    
    [Tooltip("Use exact position instead of calculating from terrain")]
    [SerializeField] private bool useExactPosition = true;
    
    [Tooltip("Offset from road edge (if not using exact position)")]
    [SerializeField] private float roadOffset = 0f;
    
    [Tooltip("Height above terrain (if not using exact position)")]
    [SerializeField] private float heightOffset = 0.5f;

    [Header("Alignment Settings")]
    [Tooltip("Wheel position offset relative to biker (will be set to match biker position)")]
    [SerializeField] private Vector3 wheelOffset = new Vector3(0, 0, 0);
    
    [Tooltip("Wheel rotation offset")]
    [SerializeField] private Vector3 wheelRotation = Vector3.zero;
    
    [Tooltip("Wheel scale adjustment")]
    [SerializeField] private Vector3 wheelScale = new Vector3(3f, 3f, 3f);

    [Header("Auto Setup")]
    [SerializeField] private bool autoSetupOnStart = false;

    private void Start()
    {
        // DISABLED: Causing prefab corruption errors
        // Re-enable only when bike models are properly set up as scene instances
        Debug.LogWarning("[BikePlacement] Auto-setup disabled to prevent prefab corruption. Enable manually if needed.");
        return;
        
        /*
        if (autoSetupOnStart)
        {
            SetupBike();
        }
        */
    }

    [ContextMenu("Setup Bike")]
    public void SetupBike()
    {
        // Safety check: Don't run in edit mode if we might modify prefabs
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[BikePlacement] SetupBike should only run in Play mode to avoid prefab corruption.");
            return;
        }

        // Find or create bike object
        if (bikeObject == null)
        {
            bikeObject = GameObject.Find("Bike");
            if (bikeObject == null)
            {
                bikeObject = new GameObject("Bike");
            }
        }

        // Find terrain to get road position
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("No terrain found! Cannot place bike.");
            return;
        }

        Vector3 bikePosition;
        Vector3 bikeRotation;
        
        if (useExactPosition)
        {
            // Use exact position from settings
            bikePosition = exactStartPosition;
            bikeRotation = exactStartRotation;
            Debug.Log($"✓ Using exact start position: {bikePosition}");
        }
        else
        {
            // Calculate bike position at start of road (left side)
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            
            // Road is horizontal (left to right), so start is at left side (X = -terrainSize.x/2)
            // Road center in Z is at terrain center (Z = 0)
            float startX = terrainPos.x - terrainSize.x / 2f + roadOffset; // Left side
            float startZ = terrainPos.z; // Center of terrain (where road is)
            
            // Get terrain height at this position
            float terrainHeight = terrain.SampleHeight(new Vector3(startX, 0, startZ));
            
            bikePosition = new Vector3(startX, terrainHeight + heightOffset, startZ);
            bikeRotation = new Vector3(0, 90, 0); // Face right (positive X)
            
            Debug.Log($"✓ Calculated position: {bikePosition}");
        }
        
        // Set bike position and rotation
        bikeObject.transform.position = bikePosition;
        bikeObject.transform.rotation = Quaternion.Euler(bikeRotation);
        
        // Make sure bike object is active
        bikeObject.SetActive(true);
        
        // Reset biker and wheels to exact same position
        ResetModelsToExactPosition();
        
        // Also adjust camera to see bike
        AdjustCameraToSeeBike(bikePosition);

        // Load and align bike models
        LoadAndAlignModels();
        
        // Note: SimpleBikeController can be added manually if needed for bike physics
        // This script only handles placement and model alignment
        
        // Create a simple visual if no models found
        if (bikerModel == null && wheelsModel == null)
        {
            CreatePlaceholderVisual();
        }
    }

    private void CreatePlaceholderVisual()
    {
        // Create a simple cube as placeholder
        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.name = "BikePlaceholder";
        placeholder.transform.SetParent(bikeObject.transform);
        placeholder.transform.localPosition = Vector3.zero;
        placeholder.transform.localScale = new Vector3(0.5f, 1f, 2f);
        placeholder.GetComponent<Renderer>().material.color = Color.red;
        
        // Remove collider
        Collider col = placeholder.GetComponent<Collider>();
        if (col != null)
        {
            DestroyImmediate(col);
        }
        
        Debug.LogWarning("No bike models found. Created red placeholder cube. Please assign bike models manually.");
    }

    private void LoadAndAlignModels()
    {
        // Try to find existing models
        if (bikerModel == null)
        {
            bikerModel = FindModelInChildren("biker");
        }

        if (wheelsModel == null)
        {
            wheelsModel = FindModelInChildren("wheel");
        }

        // Try to load from assets if not found
        if (bikerModel == null)
        {
            bikerModel = LoadModel("biker");
            if (bikerModel == null)
            {
                // Try alternative names
                bikerModel = LoadModel("biker_aero");
            }
        }

        if (wheelsModel == null)
        {
            wheelsModel = LoadModel("wheels");
            if (wheelsModel == null)
            {
                // Try alternative names
                wheelsModel = LoadModel("wheel");
            }
        }

        if (bikerModel == null && wheelsModel == null)
        {
            Debug.LogWarning("No bike models found. Please assign biker and wheels models manually.");
            return;
        }

        // Reset models will be called from SetupBike

        if (wheelsModel != null)
        {
            wheelsModel.SetActive(true);
            Renderer wheelsRenderer = wheelsModel.GetComponentInChildren<Renderer>();
            if (wheelsRenderer != null)
            {
                wheelsRenderer.enabled = true;
                Debug.Log($"✓ Wheels model active: {wheelsModel.name}, Renderer: {wheelsRenderer.name}, Bounds: {wheelsRenderer.bounds}");
            }
        }

        // Align models
        if (bikerModel != null && wheelsModel != null)
        {
            AlignWheelsWithBiker();
            Debug.Log($"✓ Both models found and aligned: Biker={bikerModel.name}, Wheels={wheelsModel.name}");
        }
        else if (bikerModel != null)
        {
            Debug.LogWarning($"Biker model found ({bikerModel.name}) but wheels not found. Please assign wheels manually.");
            Debug.LogWarning("  You can drag wheels.FBX from Assets/Models/Bike/ to the Wheels Model field in Inspector.");
        }
        else if (wheelsModel != null)
        {
            Debug.LogWarning($"Wheels model found ({wheelsModel.name}) but biker not found. Please assign biker manually.");
        }
        else
        {
            Debug.LogWarning("Neither biker nor wheels models found. Check Console for details.");
        }
    }

    private GameObject FindModelInChildren(string namePattern)
    {
        if (bikeObject == null) return null;

        foreach (Transform child in bikeObject.transform)
        {
            if (child.name.ToLower().Contains(namePattern.ToLower()))
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private GameObject LoadModel(string modelName)
    {
        #if UNITY_EDITOR
        // Try multiple search patterns
        string[] searchPaths = new string[] { "Assets/Models/Bike", "Assets" };
        
        foreach (string searchPath in searchPaths)
        {
            // Try exact name first
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{modelName} t:GameObject", new[] { searchPath });
            
            // If not found, try with .FBX extension
            if (guids.Length == 0)
            {
                guids = UnityEditor.AssetDatabase.FindAssets($"{modelName} t:Model", new[] { searchPath });
            }
            
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                // If it's a Model (FBX), we need to get the root GameObject
                if (model == null)
                {
                    // Try loading as Model and getting the root
                    UnityEngine.Object[] allObjects = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var obj in allObjects)
                    {
                        if (obj is GameObject)
                        {
                            model = obj as GameObject;
                            break;
                        }
                    }
                }
                
                if (model != null)
                {
                    // Instantiate as child of bike
                    GameObject instance = Instantiate(model, bikeObject.transform);
                    instance.name = modelName;
                    instance.SetActive(true); // Make sure it's active
                    
                    // Reset transform to start from origin
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    
                    Debug.Log($"✓ Loaded and instantiated: {modelName} from {path}");
                    Debug.Log($"  Position: {instance.transform.position}, Local: {instance.transform.localPosition}");
                    return instance;
                }
            }
        }
        
        Debug.LogWarning($"Model not found: {modelName}. Searched in Assets/Models/Bike/ and Assets/");
        #endif
        return null;
    }

    private void ResetModelsToExactPosition()
    {
        // Reset biker model
        if (bikerModel != null)
        {
            bikerModel.SetActive(true);
            
            // Set exact transform values
            bikerModel.transform.localPosition = Vector3.zero;
            bikerModel.transform.localRotation = Quaternion.Euler(Vector3.zero);
            bikerModel.transform.localScale = exactStartScale;
            
            // Make sure renderer is enabled
            Renderer bikerRenderer = bikerModel.GetComponentInChildren<Renderer>();
            if (bikerRenderer != null)
            {
                bikerRenderer.enabled = true;
            }
            
            Debug.Log($"✓ Biker reset: Position={bikerModel.transform.localPosition}, Rotation={bikerModel.transform.localRotation.eulerAngles}, Scale={bikerModel.transform.localScale}");
        }

        // Reset wheels model to same position as biker
        if (wheelsModel != null)
        {
            wheelsModel.SetActive(true);
            
            // Set wheels to same world position as biker (or as child of biker with offset)
            if (bikerModel != null)
            {
                // Make wheels child of biker
                if (wheelsModel.transform.parent != bikerModel.transform)
                {
                    wheelsModel.transform.SetParent(bikerModel.transform);
                }
                
                // Set wheels to same local position as biker (will be adjusted later)
                wheelsModel.transform.localPosition = wheelOffset;
                wheelsModel.transform.localRotation = Quaternion.Euler(wheelRotation);
                wheelsModel.transform.localScale = wheelScale;
            }
            else
            {
                // If no biker, set wheels directly to bike position
                wheelsModel.transform.localPosition = Vector3.zero;
                wheelsModel.transform.localRotation = Quaternion.Euler(Vector3.zero);
                wheelsModel.transform.localScale = wheelScale;
            }
            
            // Enable renderer
            Renderer wheelsRenderer = wheelsModel.GetComponentInChildren<Renderer>();
            if (wheelsRenderer != null)
            {
                wheelsRenderer.enabled = true;
            }
            
            Debug.Log($"✓ Wheels reset: Local Position={wheelsModel.transform.localPosition}, Rotation={wheelsModel.transform.localRotation.eulerAngles}, Scale={wheelsModel.transform.localScale}");
        }
    }

    [ContextMenu("Align Wheels with Biker")]
    public void AlignWheelsWithBiker()
    {
        if (bikerModel == null || wheelsModel == null)
        {
            Debug.LogError("Biker or wheels model not assigned!");
            return;
        }

        // Check if we're trying to modify prefab assets
        #if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(bikerModel) || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(wheelsModel))
        {
            Debug.LogError("[BikePlacement] Cannot modify prefab assets! Biker and wheels must be instances in the scene, not prefab assets.");
            return;
        }
        #endif

        // Make wheels child of biker (if not already)
        if (wheelsModel.transform.parent != bikerModel.transform)
        {
            wheelsModel.transform.SetParent(bikerModel.transform);
        }

        // Apply offset, rotation, and scale
        wheelsModel.transform.localPosition = wheelOffset;
        wheelsModel.transform.localRotation = Quaternion.Euler(wheelRotation);
        wheelsModel.transform.localScale = wheelScale;

        Debug.Log($"Wheels aligned! Offset: {wheelOffset}, Rotation: {wheelRotation}, Scale: {wheelScale}");
    }

    [ContextMenu("Find Models")]
    public void FindModels()
    {
        if (bikeObject == null)
        {
            bikeObject = GameObject.Find("Bike");
        }

        if (bikeObject != null)
        {
            bikerModel = FindModelInChildren("biker");
            wheelsModel = FindModelInChildren("wheel");
            
            if (bikerModel != null)
            {
                Debug.Log($"Found biker: {bikerModel.name}");
                Debug.Log($"  Position: {bikerModel.transform.position}, Local: {bikerModel.transform.localPosition}");
                Debug.Log($"  Scale: {bikerModel.transform.localScale}");
                Debug.Log($"  Active: {bikerModel.activeSelf}");
            }
            if (wheelsModel != null)
            {
                Debug.Log($"Found wheels: {wheelsModel.name}");
                Debug.Log($"  Position: {wheelsModel.transform.position}, Local: {wheelsModel.transform.localPosition}");
            }
        }
    }

    [ContextMenu("Make Bike Visible")]
    public void MakeBikeVisible()
    {
        if (bikeObject == null)
        {
            bikeObject = GameObject.Find("Bike");
        }

        if (bikeObject == null)
        {
            Debug.LogError("Bike GameObject not found!");
            return;
        }

        // Find biker if not assigned
        if (bikerModel == null)
        {
            bikerModel = FindModelInChildren("biker");
        }

        if (bikerModel != null)
        {
            // Ensure it's active
            bikerModel.SetActive(true);
            
            // Reset transform to reasonable values
            bikerModel.transform.localPosition = Vector3.zero;
            bikerModel.transform.localRotation = Quaternion.identity;
            bikerModel.transform.localScale = Vector3.one;
            
            // Enable all renderers
            Renderer[] renderers = bikerModel.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                r.enabled = true;
            }
            
            Debug.Log($"✓ Made biker visible: {bikerModel.name}, Renderers: {renderers.Length}");
        }
        else
        {
            Debug.LogWarning("Biker model not found in Bike GameObject!");
        }

        // Focus camera on bike
        Camera mainCam = Camera.main;
        if (mainCam != null && bikeObject != null)
        {
            Vector3 bikePos = bikeObject.transform.position;
            mainCam.transform.position = bikePos + new Vector3(-5, 3, 0);
            mainCam.transform.LookAt(bikePos);
            Debug.Log($"✓ Camera focused on bike at {bikePos}");
        }
    }

    private void AdjustCameraToSeeBike(Vector3 bikePosition)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // Position camera behind and above bike
            Vector3 cameraOffset = new Vector3(-5, 3, -5);
            mainCam.transform.position = bikePosition + cameraOffset;
            mainCam.transform.LookAt(bikePosition);
            Debug.Log($"✓ Camera adjusted to see bike");
        }
    }

    private void OnValidate()
    {
        // Update alignment in real-time when values change (in editor)
        if (Application.isPlaying && bikerModel != null && wheelsModel != null)
        {
            AlignWheelsWithBiker();
        }
    }
}
