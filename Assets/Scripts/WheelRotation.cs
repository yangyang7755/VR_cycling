using UnityEngine;

/// <summary>
/// Rotates bike wheels based on speed from BikeController.
/// Auto-finds wheel transforms by name in the hierarchy.
/// 
/// SETUP: Add to the biker root GameObject. It finds frontwheel/rearwheel automatically.
/// </summary>
public class WheelRotation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Transform frontWheel;
    [SerializeField] private Transform rearWheel;
    
    [Header("Settings")]
    [Tooltip("Wheel radius in meters (for rotation speed calculation)")]
    [SerializeField] private float wheelRadius = 0.35f;
    
    [Tooltip("Rotation axis (local space)")]
    [SerializeField] private Vector3 rotationAxis = Vector3.right; // X axis typically
    
    [Tooltip("Rotation smoothing")]
    [SerializeField] private float smoothing = 5f;
    
    private float currentRotationSpeed;

    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        // Auto-find wheels
        if (frontWheel == null || rearWheel == null)
            FindWheels();
        
        if (frontWheel != null)
            Debug.Log($"[WheelRotation] Front wheel: {frontWheel.name}");
        if (rearWheel != null)
            Debug.Log($"[WheelRotation] Rear wheel: {rearWheel.name}");
    }
    
    private void FindWheels()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            string name = t.name.ToLower();
            if (frontWheel == null && (name.Contains("frontwheel") || name.Contains("front_wheel") || name.Contains("fwheel")))
                frontWheel = t;
            if (rearWheel == null && (name.Contains("rearwheel") || name.Contains("rear_wheel") || name.Contains("rwheel") || name.Contains("backwheel")))
                rearWheel = t;
        }
    }
    
    private void Update()
    {
        if (bikeController == null) return;
        
        float speed = bikeController.GetSpeed(); // m/s
        
        // Calculate rotation speed: degrees per second
        // circumference = 2 * PI * radius
        // rotations per second = speed / circumference
        // degrees per second = rotations * 360
        float targetRPS = speed / (2f * Mathf.PI * wheelRadius);
        float targetDPS = targetRPS * 360f;
        
        currentRotationSpeed = Mathf.Lerp(currentRotationSpeed, targetDPS, Time.deltaTime * smoothing);
        
        float rotationThisFrame = currentRotationSpeed * Time.deltaTime;
        
        if (frontWheel != null)
            frontWheel.Rotate(rotationAxis, rotationThisFrame, Space.Self);
        
        if (rearWheel != null)
            rearWheel.Rotate(rotationAxis, rotationThisFrame, Space.Self);
    }
}
