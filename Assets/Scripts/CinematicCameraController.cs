using UnityEngine;

/// <summary>
/// Cinematic camera controller for Zwift-like experience
/// Multiple camera angles with smooth transitions
/// Dynamic FOV and positioning based on speed and terrain
/// </summary>
public class CinematicCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bikeTransform;
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Camera mainCamera;
    
    [Header("Camera Modes")]
    [SerializeField] private CameraMode currentMode = CameraMode.ChaseCamera;
    
    public enum CameraMode
    {
        ChaseCamera,        // Behind and above (default)
        CloseChase,         // Closer behind rider
        SideView,           // Side angle
        FirstPerson,        // Rider's perspective
        DroneView,          // High overhead
        CinematicClimb      // Dynamic angle for climbs
    }
    
    [Header("Chase Camera Settings")]
    [SerializeField] private Vector3 chaseOffset = new Vector3(0f, 3f, -8f);
    [SerializeField] private float chaseSmoothSpeed = 5f;
    [SerializeField] private float chaseRotationSpeed = 3f;
    
    [Header("Close Chase Settings")]
    [SerializeField] private Vector3 closeChaseOffset = new Vector3(0f, 2f, -4f);
    
    [Header("Side View Settings")]
    [SerializeField] private Vector3 sideViewOffset = new Vector3(5f, 2f, -2f);
    [SerializeField] private float sideViewSmoothSpeed = 8f;
    
    [Header("First Person Settings")]
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 1.8f, 0.5f);
    [SerializeField] private float firstPersonTilt = 5f;
    
    [Header("Drone View Settings")]
    [SerializeField] private Vector3 droneOffset = new Vector3(0f, 20f, -10f);
    [SerializeField] private float droneSmoothSpeed = 2f;
    
    [Header("Dynamic FOV")]
    [SerializeField] private bool enableDynamicFOV = true;
    [SerializeField] private float baseFOV = 60f;
    [SerializeField] private float maxFOV = 75f;
    [SerializeField] private float fovSpeedThreshold = 40f; // km/h
    
    [Header("Camera Shake")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeIntensity = 0.02f;
    [SerializeField] private float shakeSpeed = 20f;
    
    [Header("Keyboard Controls")]
    [SerializeField] private KeyCode nextCameraKey = KeyCode.C;
    [SerializeField] private KeyCode previousCameraKey = KeyCode.V;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private Vector3 currentVelocity;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private float currentFOV;
    private float shakeTimer;
    
    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (bikeTransform == null && bikeController != null)
        {
            // Try to get the actual bike object from BikeController
            GameObject bikeObj = bikeController.GetBikeObject();
            bikeTransform = bikeObj != null ? bikeObj.transform : bikeController.transform;
        }
        
        // Fallback: search for biker in scene
        if (bikeTransform == null)
        {
            GameObject biker = GameObject.Find("biker");
            if (biker != null) bikeTransform = biker.transform;
        }
        
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        if (mainCamera != null)
            currentFOV = mainCamera.fieldOfView;
        
        if (bikeTransform != null)
            Debug.Log($"[Camera] Following: {bikeTransform.name}");
        else
            Debug.LogWarning("[Camera] No bike transform found!");
    }
    
    private void LateUpdate()
    {
        // Retry finding bike if not found yet
        if (bikeTransform == null)
        {
            if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
            if (bikeController != null)
            {
                GameObject bikeObj = bikeController.GetBikeObject();
                bikeTransform = bikeObj != null ? bikeObj.transform : bikeController.transform;
            }
            if (bikeTransform == null)
            {
                GameObject biker = GameObject.Find("biker");
                if (biker != null) bikeTransform = biker.transform;
            }
            if (bikeTransform == null) return;
        }
        
        if (mainCamera == null) return;
        
        // Handle camera switching
        HandleCameraInput();
        
        // Update camera based on current mode
        UpdateCameraPosition();
        UpdateCameraRotation();
        
        // Dynamic effects
        if (enableDynamicFOV)
            UpdateDynamicFOV();
        
        if (enableCameraShake)
            ApplyCameraShake();
    }
    
    private void HandleCameraInput()
    {
        if (Input.GetKeyDown(nextCameraKey))
        {
            CycleCamera(1);
        }
        else if (Input.GetKeyDown(previousCameraKey))
        {
            CycleCamera(-1);
        }
    }
    
    private void CycleCamera(int direction)
    {
        int modeCount = System.Enum.GetValues(typeof(CameraMode)).Length;
        int currentIndex = (int)currentMode;
        currentIndex = (currentIndex + direction + modeCount) % modeCount;
        currentMode = (CameraMode)currentIndex;
        
        if (showDebugInfo)
            Debug.Log($"[Camera] Switched to: {currentMode}");
    }
    
    private void UpdateCameraPosition()
    {
        Vector3 offset = GetCurrentOffset();
        float smoothSpeed = GetCurrentSmoothSpeed();
        
        // Calculate target position
        targetPosition = bikeTransform.position + bikeTransform.TransformDirection(offset);
        
        // Smooth movement
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            1f / smoothSpeed
        );
    }
    
    private void UpdateCameraRotation()
    {
        Vector3 lookTarget = bikeTransform.position;
        
        // Adjust look target based on camera mode
        switch (currentMode)
        {
            case CameraMode.FirstPerson:
                lookTarget += bikeTransform.forward * 10f;
                break;
            case CameraMode.DroneView:
                lookTarget += bikeTransform.forward * 5f;
                break;
            default:
                lookTarget += Vector3.up * 1.5f; // Look at rider's upper body
                break;
        }
        
        targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        
        float rotationSpeed = GetCurrentRotationSpeed();
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
    }
    
    private Vector3 GetCurrentOffset()
    {
        switch (currentMode)
        {
            case CameraMode.ChaseCamera:
                return chaseOffset;
            case CameraMode.CloseChase:
                return closeChaseOffset;
            case CameraMode.SideView:
                return sideViewOffset;
            case CameraMode.FirstPerson:
                return firstPersonOffset;
            case CameraMode.DroneView:
                return droneOffset;
            case CameraMode.CinematicClimb:
                return GetCinematicClimbOffset();
            default:
                return chaseOffset;
        }
    }
    
    private Vector3 GetCinematicClimbOffset()
    {
        // Dynamic offset based on gradient
        float gradient = bikeController != null ? bikeController.GetGradient() : 0f;
        float heightBoost = Mathf.Clamp(gradient * 0.3f, 0f, 3f);
        return new Vector3(2f, 3f + heightBoost, -6f);
    }
    
    private float GetCurrentSmoothSpeed()
    {
        switch (currentMode)
        {
            case CameraMode.SideView:
                return sideViewSmoothSpeed;
            case CameraMode.DroneView:
                return droneSmoothSpeed;
            default:
                return chaseSmoothSpeed;
        }
    }
    
    private float GetCurrentRotationSpeed()
    {
        switch (currentMode)
        {
            case CameraMode.FirstPerson:
                return 10f;
            case CameraMode.DroneView:
                return 2f;
            default:
                return chaseRotationSpeed;
        }
    }
    
    private void UpdateDynamicFOV()
    {
        if (bikeController == null) return;
        
        float speedKmh = bikeController.GetSpeedKmh();
        float speedFactor = Mathf.Clamp01(speedKmh / fovSpeedThreshold);
        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedFactor);
        
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * 2f);
        mainCamera.fieldOfView = currentFOV;
    }
    
    private void ApplyCameraShake()
    {
        if (bikeController == null) return;
        
        float speed = bikeController.GetSpeed();
        if (speed < 0.1f) return;
        
        shakeTimer += Time.deltaTime * shakeSpeed;
        
        float shakeX = Mathf.Sin(shakeTimer) * shakeIntensity * speed;
        float shakeY = Mathf.Cos(shakeTimer * 1.3f) * shakeIntensity * speed * 0.5f;
        
        transform.position += new Vector3(shakeX, shakeY, 0f);
    }
    
    public void SetCameraMode(CameraMode mode)
    {
        currentMode = mode;
        if (showDebugInfo)
            Debug.Log($"[Camera] Set to: {currentMode}");
    }
    
    public CameraMode GetCurrentMode()
    {
        return currentMode;
    }
    
    private void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUI.Label(new Rect(10, 100, 300, 20), $"Camera: {currentMode}");
        GUI.Label(new Rect(10, 120, 300, 20), $"Press C/V to change camera");
    }
}
