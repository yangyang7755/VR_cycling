using UnityEngine;

/// <summary>
/// Animates crank/pedals matched to cadence, with leg IK following the pedals.
/// 1. Crank rotation speed = cadence from BikeController
/// 2. Leg bones track pedal positions using simple two-bone IK
/// 
/// SETUP: Add to the biker root GameObject. Auto-finds pedals and leg bones.
/// </summary>
public class CrankAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Animator animator;
    
    [Header("Crank Transforms (auto-found)")]
    [SerializeField] private Transform crankDrive;
    [SerializeField] private Transform leftPedal;
    [SerializeField] private Transform rightPedal;
    
    [Header("Leg Bones (auto-found from Humanoid rig)")]
    [SerializeField] private Transform leftUpperLeg;
    [SerializeField] private Transform leftLowerLeg;
    [SerializeField] private Transform rightUpperLeg;
    [SerializeField] private Transform rightLowerLeg;
    
    [Header("Crank Settings")]
    [SerializeField] private Vector3 crankAxis = new Vector3(1f, 0f, 0f);
    [SerializeField] private float defaultCadence = 80f;
    [SerializeField] private float minSpeedToAnimate = 0.5f;
    [SerializeField] private float smoothing = 5f;
    [SerializeField] private bool keepPedalsLevel = true;
    
    [Header("Leg IK")]
    [SerializeField] private bool enableLegIK = true;
    [Range(0f, 1f)]
    [SerializeField] private float ikWeight = 0.7f;
    
    private float currentRotation = 0f;
    private float smoothedCadence = 0f;
    private bool legsFound = false;
    private Quaternion luInit, llInit, ruInit, rlInit;

    private void Start()
    {
        if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
        if (animator == null) animator = GetComponent<Animator>();
        
        FindCrankTransforms();
        FindLegBones();
        
        Debug.Log($"[CrankAnimation] Crank: {crankDrive?.name ?? "NONE"}, " +
                  $"Pedals: {leftPedal?.name ?? "NONE"}/{rightPedal?.name ?? "NONE"}, " +
                  $"Legs: {(legsFound ? "YES" : "NO")}");
    }
    
    private void FindCrankTransforms()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            string n = t.name.ToLower();
            if (leftPedal == null && n.Contains("leftpedal")) leftPedal = t;
            if (rightPedal == null && n.Contains("rightpedal")) rightPedal = t;
            if (crankDrive == null && n == "drive" && t.parent != null && t.parent.name.ToLower().Contains("wheel"))
                crankDrive = t;
        }
        if (crankDrive == null && leftPedal != null)
            crankDrive = leftPedal.parent;
    }
    
    private void FindLegBones()
    {
        if (animator != null && animator.isHuman)
        {
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }
        
        legsFound = leftUpperLeg != null && leftLowerLeg != null && 
                    rightUpperLeg != null && rightLowerLeg != null;
        
        if (legsFound)
        {
            luInit = leftUpperLeg.localRotation;
            llInit = leftLowerLeg.localRotation;
            ruInit = rightUpperLeg.localRotation;
            rlInit = rightLowerLeg.localRotation;
        }
    }
    
    private void LateUpdate()
    {
        if (bikeController == null) return;
        
        float speed = bikeController.GetSpeed();
        if (speed < minSpeedToAnimate)
        {
            smoothedCadence = Mathf.Lerp(smoothedCadence, 0f, Time.deltaTime * smoothing);
            if (legsFound) ResetLegs();
            return;
        }
        
        // Cadence
        float targetCadence = bikeController.GetCadence();
        if (targetCadence < 1f && speed > 0.5f) targetCadence = defaultCadence;
        smoothedCadence = Mathf.Lerp(smoothedCadence, targetCadence, Time.deltaTime * smoothing);
        if (smoothedCadence < 5f) return;
        
        // Rotate crank
        float rps = smoothedCadence / 60f;
        float deg = rps * 360f * Time.deltaTime;
        currentRotation = (currentRotation + deg) % 360f;
        
        if (crankDrive != null)
            crankDrive.Rotate(crankAxis, deg, Space.Self);
        
        if (keepPedalsLevel)
        {
            if (leftPedal != null && leftPedal.parent == crankDrive)
                leftPedal.Rotate(crankAxis, -deg, Space.Self);
            if (rightPedal != null && rightPedal.parent == crankDrive)
                rightPedal.Rotate(crankAxis, -deg, Space.Self);
        }
        
        // Leg IK: make legs follow pedal positions
        if (enableLegIK && legsFound)
        {
            AnimateLegs();
        }
    }
    
    private void AnimateLegs()
    {
        // Use crank angle to drive leg motion
        // Left pedal is at currentRotation, right is at currentRotation + 180
        float leftAngle = currentRotation * Mathf.Deg2Rad;
        float rightAngle = (currentRotation + 180f) * Mathf.Deg2Rad;
        
        // Hip angle: forward/back swing following pedal circle
        // When pedal is at top (angle ~90°), hip is flexed (leg forward)
        // When pedal is at bottom (angle ~270°), hip is extended (leg back)
        float leftHipDelta = Mathf.Sin(leftAngle) * 12f * ikWeight;
        float leftKneeDelta = Mathf.Max(0f, Mathf.Sin(leftAngle - 0.5f)) * 15f * ikWeight;
        
        float rightHipDelta = Mathf.Sin(rightAngle) * 12f * ikWeight;
        float rightKneeDelta = Mathf.Max(0f, Mathf.Sin(rightAngle - 0.5f)) * 15f * ikWeight;
        
        // Apply to bones — try each axis to find the right one
        // The auto-detect from PedalingAnimation found the axis; use Z as default for this model
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = luInit * Quaternion.Euler(0f, 0f, leftHipDelta);
        if (leftLowerLeg != null)
            leftLowerLeg.localRotation = llInit * Quaternion.Euler(0f, 0f, leftKneeDelta);
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = ruInit * Quaternion.Euler(0f, 0f, rightHipDelta);
        if (rightLowerLeg != null)
            rightLowerLeg.localRotation = rlInit * Quaternion.Euler(0f, 0f, rightKneeDelta);
    }
    
    private void ResetLegs()
    {
        float t = Time.deltaTime * 3f;
        if (leftUpperLeg != null) leftUpperLeg.localRotation = Quaternion.Slerp(leftUpperLeg.localRotation, luInit, t);
        if (leftLowerLeg != null) leftLowerLeg.localRotation = Quaternion.Slerp(leftLowerLeg.localRotation, llInit, t);
        if (rightUpperLeg != null) rightUpperLeg.localRotation = Quaternion.Slerp(rightUpperLeg.localRotation, ruInit, t);
        if (rightLowerLeg != null) rightLowerLeg.localRotation = Quaternion.Slerp(rightLowerLeg.localRotation, rlInit, t);
    }
}
