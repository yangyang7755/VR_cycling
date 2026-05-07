using UnityEngine;

/// <summary>
/// Procedural pedaling animation for the cyclist
/// Rotates upper and lower leg bones in a circular pedaling motion
/// Syncs pedaling speed to cadence from BikeController
/// Works with any humanoid rig - finds leg bones automatically
/// </summary>
public class PedalingAnimation : MonoBehaviour
{
    [Header("Master Toggle")]
    [Tooltip("Enable/disable all pedaling animation")]
    [SerializeField] private bool enablePedaling = false;
    
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Animator animator;
    
    [Header("Bone References (Auto-found if using Humanoid rig)")]
    [SerializeField] private Transform leftUpperLeg;
    [SerializeField] private Transform leftLowerLeg;
    [SerializeField] private Transform rightUpperLeg;
    [SerializeField] private Transform rightLowerLeg;
    
    [Header("Pedaling Settings")]
    [Tooltip("Pedaling amplitude in degrees (how far legs move)")]
    [SerializeField] private float pedalAmplitude = 10f;
    
    [Tooltip("Knee bend amplitude in degrees")]
    [SerializeField] private float kneeBendAmplitude = 12f;
    
    [Tooltip("Default cadence when no data (RPM)")]
    [SerializeField] private float defaultCadence = 80f;
    
    [Tooltip("Minimum cadence to start pedaling animation")]
    [SerializeField] private float minCadenceToAnimate = 5f;
    
    [Tooltip("Smoothing for cadence changes")]
    [SerializeField] private float cadenceSmoothing = 5f;
    
    [Header("Rotation Axis")]
    [Tooltip("Auto-detect the correct pedaling axis from bone orientation")]
    [SerializeField] private bool autoDetectAxis = true;
    
    [Tooltip("Which local axis the hip rotates around for pedaling (used if auto-detect is off)")]
    [SerializeField] private RotationAxis hipRotationAxis = RotationAxis.Z;
    
    [Tooltip("Which local axis the knee bends around")]
    [SerializeField] private RotationAxis kneeRotationAxis = RotationAxis.Z;
    
    [Tooltip("Invert hip rotation direction")]
    [SerializeField] private bool invertHipDirection = false;
    
    [Tooltip("Invert knee rotation direction")]
    [SerializeField] private bool invertKneeDirection = false;
    
    public enum RotationAxis { X, Y, Z }
    
    [Header("Leg Rest Position — leave at 0 to use model's default pose")]
    [Tooltip("Upper leg rest offset from bind pose (degrees)")]
    [SerializeField] private float upperLegRestAngle = 0f;
    
    [Tooltip("Lower leg rest offset from bind pose (degrees)")]
    [SerializeField] private float lowerLegRestAngle = 0f;
    
    [Header("Body Movement")]
    [Tooltip("Enable subtle body sway while pedaling")]
    [SerializeField] private bool enableBodySway = true;
    
    [Tooltip("Body sway amount")]
    [SerializeField] private float bodySwayAmount = 1f;
    
    [Tooltip("Spine/body transform for sway")]
    [SerializeField] private Transform spineTransform;
    
    [Header("Standing Climb")]
    [Tooltip("Stand up on steep gradients")]
    [SerializeField] private bool enableStandingClimb = true;
    
    [Tooltip("Gradient threshold to stand (%)")]
    [SerializeField] private float standingGradientThreshold = 8f;
    
    [Tooltip("Standing pedal amplitude (larger motion)")]
    [SerializeField] private float standingPedalAmplitude = 22f;
    
    [Tooltip("Standing body sway (more sway when standing)")]
    [SerializeField] private float standingBodySwayAmount = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool manualTestMode = false;
    [SerializeField] private float manualTestCadence = 80f;
    
    private float pedalAngle = 0f;
    private float smoothedCadence = 0f;
    private bool isStanding = false;
    private bool bonesFound = false;
    
    // Store initial rotations
    private Quaternion leftUpperLegInitial;
    private Quaternion leftLowerLegInitial;
    private Quaternion rightUpperLegInitial;
    private Quaternion rightLowerLegInitial;
    private Quaternion spineInitial;

    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        FindBones();
    }
    
    private void FindBones()
    {
        // Try to find bones from Animator (Humanoid rig)
        if (animator != null && animator.isHuman)
        {
            if (leftUpperLeg == null)
                leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            if (leftLowerLeg == null)
                leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            if (rightUpperLeg == null)
                rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (rightLowerLeg == null)
                rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            if (spineTransform == null)
                spineTransform = animator.GetBoneTransform(HumanBodyBones.Spine);
            
            if (leftUpperLeg != null)
            {
                Debug.Log("[PedalingAnimation] Found humanoid leg bones automatically");
                bonesFound = true;
            }
        }
        
        // Fallback: search by common bone names
        if (!bonesFound)
        {
            FindBonesByName();
        }
        
        // Store initial rotations
        if (bonesFound)
        {
            StoreInitialRotations();
        }
        
        if (!bonesFound)
        {
            Debug.LogWarning("[PedalingAnimation] Could not find leg bones! " +
                "Please assign them manually in the Inspector, or ensure the model " +
                "uses a Humanoid rig (check FBX import settings → Rig → Animation Type → Humanoid)");
        }
        else
        {
            Debug.Log($"[PedalingAnimation] Bones found and ready:");
            Debug.Log($"  Left Upper Leg: {leftUpperLeg?.name} (localRot: {leftUpperLeg?.localEulerAngles})");
            Debug.Log($"  Left Lower Leg: {leftLowerLeg?.name} (localRot: {leftLowerLeg?.localEulerAngles})");
            Debug.Log($"  Right Upper Leg: {rightUpperLeg?.name} (localRot: {rightUpperLeg?.localEulerAngles})");
            Debug.Log($"  Right Lower Leg: {rightLowerLeg?.name} (localRot: {rightLowerLeg?.localEulerAngles})");
            Debug.Log($"  Spine: {spineTransform?.name}");
        }
    }
    
    private void FindBonesByName()
    {
        string[] upperLegNames = { "UpperLeg", "Thigh", "upper_leg", "thigh", "Hip", "hip" };
        string[] lowerLegNames = { "LowerLeg", "Calf", "lower_leg", "calf", "Shin", "shin", "Knee", "knee" };
        string[] spineNames = { "Spine", "spine", "Torso", "torso", "Back", "back" };
        
        Transform[] allBones = GetComponentsInChildren<Transform>();
        
        foreach (Transform bone in allBones)
        {
            string boneName = bone.name.ToLower();
            
            // Left upper leg
            if (leftUpperLeg == null && (boneName.Contains("left") || boneName.Contains("l_")))
            {
                foreach (string name in upperLegNames)
                {
                    if (boneName.Contains(name.ToLower()))
                    {
                        leftUpperLeg = bone;
                        break;
                    }
                }
            }
            
            // Right upper leg
            if (rightUpperLeg == null && (boneName.Contains("right") || boneName.Contains("r_")))
            {
                foreach (string name in upperLegNames)
                {
                    if (boneName.Contains(name.ToLower()))
                    {
                        rightUpperLeg = bone;
                        break;
                    }
                }
            }
            
            // Left lower leg
            if (leftLowerLeg == null && (boneName.Contains("left") || boneName.Contains("l_")))
            {
                foreach (string name in lowerLegNames)
                {
                    if (boneName.Contains(name.ToLower()))
                    {
                        leftLowerLeg = bone;
                        break;
                    }
                }
            }
            
            // Right lower leg
            if (rightLowerLeg == null && (boneName.Contains("right") || boneName.Contains("r_")))
            {
                foreach (string name in lowerLegNames)
                {
                    if (boneName.Contains(name.ToLower()))
                    {
                        rightLowerLeg = bone;
                        break;
                    }
                }
            }
            
            // Spine
            if (spineTransform == null)
            {
                foreach (string name in spineNames)
                {
                    if (boneName.Contains(name.ToLower()) && !boneName.Contains("leg"))
                    {
                        spineTransform = bone;
                        break;
                    }
                }
            }
        }
        
        bonesFound = (leftUpperLeg != null && rightUpperLeg != null);
    }
    
    private void StoreInitialRotations()
    {
        if (leftUpperLeg != null) leftUpperLegInitial = leftUpperLeg.localRotation;
        if (leftLowerLeg != null) leftLowerLegInitial = leftLowerLeg.localRotation;
        if (rightUpperLeg != null) rightUpperLegInitial = rightUpperLeg.localRotation;
        if (rightLowerLeg != null) rightLowerLegInitial = rightLowerLeg.localRotation;
        if (spineTransform != null) spineInitial = spineTransform.localRotation;
        
        if (autoDetectAxis)
        {
            DetectPedalingAxis();
        }
    }
    
    /// <summary>
    /// Auto-detect which local axis the legs should rotate around for pedaling.
    /// Looks at the direction from upper leg to lower leg in the upper leg's local space.
    /// The pedaling axis is perpendicular to that direction and to the model's forward.
    /// </summary>
    private void DetectPedalingAxis()
    {
        if (leftUpperLeg == null || leftLowerLeg == null) return;
        
        // Get the direction from upper leg to lower leg in the upper leg's local space
        Vector3 toLowerLeg = leftUpperLeg.InverseTransformPoint(leftLowerLeg.position);
        
        // The leg extends mostly along one axis — find which one
        float absX = Mathf.Abs(toLowerLeg.x);
        float absY = Mathf.Abs(toLowerLeg.y);
        float absZ = Mathf.Abs(toLowerLeg.z);
        
        // The leg bone direction (longest axis)
        // The pedaling rotation axis should be perpendicular to the leg direction
        // and perpendicular to the model's forward direction
        
        // For most humanoid rigs:
        // If leg extends along Y (down) → pedal around X or Z
        // If leg extends along X → pedal around Y or Z
        
        // The key insight: we want to rotate so the leg swings forward/back
        // relative to the model. Check which axis, when rotated, moves the
        // lower leg in the model's forward direction.
        
        RotationAxis bestAxis = RotationAxis.Z; // default fallback
        float bestScore = 0f;
        
        foreach (RotationAxis testAxis in new[] { RotationAxis.X, RotationAxis.Y, RotationAxis.Z })
        {
            // Apply a small test rotation and see how much the lower leg moves
            // in the model's forward direction (world space)
            Quaternion testRot = MakeAxisRotation(testAxis, 10f);
            Quaternion rotated = leftUpperLegInitial * testRot;
            
            // Temporarily apply
            Quaternion saved = leftUpperLeg.localRotation;
            leftUpperLeg.localRotation = rotated;
            
            Vector3 newLowerLegPos = leftLowerLeg.position;
            leftUpperLeg.localRotation = saved;
            
            Vector3 originalLowerLegPos = leftLowerLeg.position;
            Vector3 movement = newLowerLegPos - originalLowerLegPos;
            
            // We want the axis that moves the leg most in the model's forward direction
            // (the direction the bike faces) and least sideways
            Vector3 modelForward = transform.forward;
            Vector3 modelRight = transform.right;
            
            float forwardMovement = Mathf.Abs(Vector3.Dot(movement, modelForward));
            float sidewaysMovement = Mathf.Abs(Vector3.Dot(movement, modelRight));
            
            // Score: high forward movement, low sideways movement
            float score = forwardMovement - sidewaysMovement * 2f;
            
            if (score > bestScore)
            {
                bestScore = score;
                bestAxis = testAxis;
            }
        }
        
        hipRotationAxis = bestAxis;
        kneeRotationAxis = bestAxis;
        
        Debug.Log($"[PedalingAnimation] Auto-detected pedaling axis: {bestAxis}");
        Debug.Log($"  Upper→Lower leg local offset: {toLowerLeg}");
        Debug.Log($"  (If legs still look wrong, try changing hipRotationAxis in Inspector)");
    }

    private void LateUpdate()
    {
        if (!enablePedaling || !bonesFound) return;
        
        // Get current cadence
        float targetCadence = GetCurrentCadence();
        
        // Smooth cadence transitions
        smoothedCadence = Mathf.Lerp(smoothedCadence, targetCadence, Time.deltaTime * cadenceSmoothing);
        
        // Check if we should animate
        if (smoothedCadence < minCadenceToAnimate)
        {
            // Reset to rest position when not pedaling
            ResetToRestPosition();
            return;
        }
        
        // Check standing vs sitting
        float gradient = bikeController != null ? bikeController.GetGradient() : 0f;
        isStanding = enableStandingClimb && gradient >= standingGradientThreshold;
        
        // Calculate pedal angle from cadence
        // Cadence is in RPM, convert to radians per second
        float rps = smoothedCadence / 60f; // Revolutions per second
        pedalAngle += rps * 360f * Time.deltaTime;
        pedalAngle %= 360f;
        
        // Animate legs
        AnimateLegs();
        
        // Animate body sway
        if (enableBodySway)
        {
            AnimateBodySway();
        }
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[PedalingAnimation] Cadence: {smoothedCadence:F0} RPM, " +
                     $"Angle: {pedalAngle:F0}°, Standing: {isStanding}");
        }
    }
    
    private float GetCurrentCadence()
    {
        if (manualTestMode)
            return manualTestCadence;
        
        if (bikeController != null)
        {
            float cadence = bikeController.GetCadence();
            
            // If cadence is 0 but speed > 0, estimate from speed
            if (cadence < 1f && bikeController.GetSpeed() > 0.5f)
            {
                cadence = defaultCadence;
            }
            
            return cadence;
        }
        
        return defaultCadence;
    }
    
    private void AnimateLegs()
    {
        float amplitude = isStanding ? standingPedalAmplitude : pedalAmplitude;
        float kneeAmplitude = kneeBendAmplitude;
        float hipSign = invertHipDirection ? -1f : 1f;
        float kneeSign = invertKneeDirection ? -1f : 1f;
        
        float angleRad = pedalAngle * Mathf.Deg2Rad;
        
        // Pedaling motion: hip swings forward/back, knee bends more when leg is up
        // Left leg at angle 0, right leg at angle PI (opposite)
        float leftHipDelta = (upperLegRestAngle + Mathf.Sin(angleRad) * amplitude) * hipSign;
        float rightHipDelta = (upperLegRestAngle + Mathf.Sin(angleRad + Mathf.PI) * amplitude) * hipSign;
        
        // Knee bends more when hip is forward (leg coming up), less when pushing down
        // This creates the circular pedaling look rather than a running gait
        float leftKneePhase = Mathf.Sin(angleRad - 0.5f); // slightly offset from hip
        float rightKneePhase = Mathf.Sin(angleRad + Mathf.PI - 0.5f);
        float leftKneeDelta = (lowerLegRestAngle + Mathf.Max(0f, leftKneePhase) * kneeAmplitude) * kneeSign;
        float rightKneeDelta = (lowerLegRestAngle + Mathf.Max(0f, rightKneePhase) * kneeAmplitude) * kneeSign;
        
        // Try all three axes and use the configured one
        Quaternion leftHipRot = MakeAxisRotation(hipRotationAxis, leftHipDelta);
        Quaternion leftKneeRot = MakeAxisRotation(kneeRotationAxis, leftKneeDelta);
        Quaternion rightHipRot = MakeAxisRotation(hipRotationAxis, rightHipDelta);
        Quaternion rightKneeRot = MakeAxisRotation(kneeRotationAxis, rightKneeDelta);
        
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = leftUpperLegInitial * leftHipRot;
        
        if (leftLowerLeg != null)
            leftLowerLeg.localRotation = leftLowerLegInitial * leftKneeRot;
        
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = rightUpperLegInitial * rightHipRot;
        
        if (rightLowerLeg != null)
            rightLowerLeg.localRotation = rightLowerLegInitial * rightKneeRot;
    }
    
    private Quaternion MakeAxisRotation(RotationAxis axis, float degrees)
    {
        switch (axis)
        {
            case RotationAxis.X: return Quaternion.Euler(degrees, 0f, 0f);
            case RotationAxis.Y: return Quaternion.Euler(0f, degrees, 0f);
            case RotationAxis.Z: return Quaternion.Euler(0f, 0f, degrees);
            default: return Quaternion.Euler(degrees, 0f, 0f);
        }
    }
    
    private void AnimateBodySway()
    {
        if (spineTransform == null) return;
        
        float swayAmount = isStanding ? standingBodySwayAmount : bodySwayAmount;
        float angleRad = pedalAngle * Mathf.Deg2Rad;
        
        // Sway side to side with pedaling
        float swayZ = Mathf.Sin(angleRad) * swayAmount;
        
        // Slight forward lean when standing
        float leanX = isStanding ? 5f : 0f;
        
        spineTransform.localRotation = spineInitial * Quaternion.Euler(leanX, 0f, swayZ);
    }
    
    private void ResetToRestPosition()
    {
        // Smoothly return to rest position
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = Quaternion.Slerp(leftUpperLeg.localRotation, leftUpperLegInitial, Time.deltaTime * 3f);
        
        if (leftLowerLeg != null)
            leftLowerLeg.localRotation = Quaternion.Slerp(leftLowerLeg.localRotation, leftLowerLegInitial, Time.deltaTime * 3f);
        
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = Quaternion.Slerp(rightUpperLeg.localRotation, rightUpperLegInitial, Time.deltaTime * 3f);
        
        if (rightLowerLeg != null)
            rightLowerLeg.localRotation = Quaternion.Slerp(rightLowerLeg.localRotation, rightLowerLegInitial, Time.deltaTime * 3f);
        
        if (spineTransform != null)
            spineTransform.localRotation = Quaternion.Slerp(spineTransform.localRotation, spineInitial, Time.deltaTime * 3f);
    }
    
    /// <summary>
    /// Lists all bones in the hierarchy for debugging
    /// Use this to find the correct bone names for your model
    /// </summary>
    [ContextMenu("List All Bones")]
    public void ListAllBones()
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>();
        Debug.Log($"[PedalingAnimation] All bones in {gameObject.name}:");
        
        foreach (Transform t in allTransforms)
        {
            string indent = "";
            Transform parent = t.parent;
            while (parent != null && parent != transform)
            {
                indent += "  ";
                parent = parent.parent;
            }
            Debug.Log($"{indent}{t.name} (pos: {t.localPosition}, rot: {t.localEulerAngles})");
        }
    }
    
    [ContextMenu("Test Pedaling (Toggle)")]
    public void ToggleTestMode()
    {
        manualTestMode = !manualTestMode;
        Debug.Log($"[PedalingAnimation] Test mode: {(manualTestMode ? "ON" : "OFF")}");
    }
}
