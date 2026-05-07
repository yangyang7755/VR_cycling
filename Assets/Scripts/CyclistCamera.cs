using UnityEngine;

/// <summary>
/// Follows the cyclist from behind. Snaps to the correct position every frame.
/// No initialization issues — works regardless of where the camera starts.
/// 
/// SETUP: Attach to Main Camera. Assign the biker GameObject to Target.
/// </summary>
public class CyclistCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag the 'biker' GameObject here")]
    [SerializeField] private Transform target;
    
    [Header("Chase Position")]
    [Tooltip("How far behind the cyclist (meters)")]
    [SerializeField] private float distanceBehind = 6f;
    
    [Tooltip("How high above the cyclist (meters)")]
    [SerializeField] private float heightAbove = 3f;
    
    [Header("Smoothing")]
    [Tooltip("How fast the camera follows (higher = snappier)")]
    [SerializeField] private float followSpeed = 8f;
    
    [Tooltip("How fast the camera rotates to look at cyclist")]
    [SerializeField] private float rotationSpeed = 5f;
    
    [Header("Look Target")]
    [Tooltip("How far ahead of the cyclist to look")]
    [SerializeField] private float lookAheadDistance = 3f;
    
    [Tooltip("Height offset for look target")]
    [SerializeField] private float lookHeightOffset = 1.5f;
    
    private bool initialized = false;

    private void LateUpdate()
    {
        // Auto-find target if not assigned
        if (target == null)
        {
            // Try BikeController's bike object
            BikeController bc = FindObjectOfType<BikeController>();
            if (bc != null)
            {
                GameObject bikeObj = bc.GetBikeObject();
                if (bikeObj != null) target = bikeObj.transform;
                else target = bc.transform;
            }
            
            // Fallback: find by name
            if (target == null)
            {
                GameObject biker = GameObject.Find("biker");
                if (biker != null) target = biker.transform;
            }
            
            if (target == null) return;
        }
        
        // Calculate desired camera position: behind and above the cyclist
        Vector3 forward = target.forward;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.right;
        
        Vector3 desiredPos = target.position 
            - forward * distanceBehind 
            + Vector3.up * heightAbove;
        
        // On first frame or if camera is far away, snap instantly
        float dist = Vector3.Distance(transform.position, desiredPos);
        if (!initialized || dist > 15f)
        {
            transform.position = desiredPos;
            initialized = true;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * followSpeed);
        }
        
        // Look at a point slightly ahead of and above the cyclist
        Vector3 lookTarget = target.position 
            + forward * lookAheadDistance 
            + Vector3.up * lookHeightOffset;
        
        Quaternion desiredRot = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * rotationSpeed);
    }
}
