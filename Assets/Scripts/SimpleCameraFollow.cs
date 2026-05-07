using UnityEngine;

/// <summary>
/// Camera follow with gradient-aware angling.
/// Tilts up when going uphill, down when going downhill.
/// </summary>
public class SimpleCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    
    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -8f);
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float rotationSpeed = 3f;
    
    [Header("Gradient Tilt")]
    [Tooltip("How much the camera tilts with the terrain slope")]
    [SerializeField] private float gradientTiltMultiplier = 1.5f;
    
    private BikeController bikeController;
    private Terrain terrain;
    
    private void LateUpdate()
    {
        if (target == null)
        {
            if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
            if (bikeController != null)
            {
                GameObject bikeObj = bikeController.GetBikeObject();
                target = bikeObj != null ? bikeObj.transform : bikeController.transform;
            }
            if (target == null)
            {
                GameObject biker = GameObject.Find("biker");
                if (biker != null) target = biker.transform;
            }
            if (target == null) return;
            
            terrain = FindObjectOfType<Terrain>();
        }
        
        // Get terrain slope at bike position for camera tilt
        float slopeAngle = 0f;
        if (terrain != null)
        {
            Vector3 bikePos = target.position;
            Vector3 ahead = bikePos + target.forward * 5f;
            float hHere = terrain.SampleHeight(bikePos);
            float hAhead = terrain.SampleHeight(ahead);
            slopeAngle = Mathf.Atan2(hAhead - hHere, 5f) * Mathf.Rad2Deg * gradientTiltMultiplier;
        }
        
        // Calculate camera position — offset tilts with slope
        Vector3 slopedOffset = offset;
        slopedOffset.y += Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * 2f;
        
        Vector3 desiredPosition = target.position + target.TransformDirection(slopedOffset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
        
        // Look at target with slope-aware pitch
        Vector3 lookTarget = target.position + Vector3.up * 1.5f + target.forward * 3f;
        lookTarget.y += slopeAngle * 0.1f; // look slightly up/down with slope
        
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSpeed);
    }
}
