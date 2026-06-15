using UnityEngine;

public enum BikeSpeedMode
{
    PhysicsBased,
    DirectFromTrainer
}

/// <summary>
/// Controls the virtual bike based on power and cadence from trainer
/// Calculates speed and updates bike position
/// Supports both modes:
/// 1. Physics-based: Use real power + virtual gradient to calculate speed
/// 2. Direct: Use actual speed from trainer
/// </summary>
public class BikeController : MonoBehaviour
{
    [Header("Bike References")]
    [Tooltip("Bike GameObject to move")]
    [SerializeField] private GameObject bikeObject;

    [Tooltip("Terrain for height sampling")]
    [SerializeField] private Terrain terrain;

    [Header("Speed Calculation Mode")]
    [Tooltip("Use physics-based speed calculation (power + gradient) or direct speed from trainer")]
    [SerializeField] private BikeSpeedMode speedMode = BikeSpeedMode.DirectFromTrainer;

    [Header("Bike Physics Parameters")]
    [Tooltip("Total mass (rider + bike) in kg")]
    [SerializeField] private float totalMass = 80f;

    [Tooltip("Drivetrain effectiveness (0-100%)")]
    [Range(0f, 100f)]
    [SerializeField] private float drivetrainEffectiveness = 95f;

    [Tooltip("Coefficient of rolling resistance")]
    [SerializeField] private float rollingResistance = 0.005f;

    [Tooltip("Air density (kg/m³)")]
    [SerializeField] private float airDensity = 1.226f;

    [Tooltip("Frontal area (m²)")]
    [SerializeField] private float frontalArea = 0.5f;

    [Tooltip("Drag coefficient")]
    [SerializeField] private float dragCoefficient = 0.63f;

    [Header("Current State")]
    [SerializeField] private float currentPower = 0f; // Watts
    [SerializeField] private float currentCadence = 0f; // RPM
    [SerializeField] private float currentSpeed = 0f; // m/s
    [SerializeField] private float distanceTravelled = 0f; // meters
    [SerializeField] private float currentSlope = 0f; // percentage

    [Header("Grade Array Controller (Optional)")]
    [Tooltip("GradeArrayController for pre-computed grade array (for long routes)")]
    [SerializeField] private GradeArrayController gradeArrayController;

    [Header("Spline Path (Curved Road)")]
    [Tooltip("SplinePath to follow for curved roads. If assigned, bike follows the spline instead of going straight.")]
    [SerializeField] private SplinePath splinePath;

    [Header("Loop Path Support (Optional)")]
    [Tooltip("LoopPathMapper for looped paths (for long routes in limited terrain space)")]
    [SerializeField] private LoopPathMapper loopPathMapper;

    [Tooltip("Route configuration for total route length")]
    [SerializeField] private RouteConfiguration routeConfig;

    [Header("Movement Settings")]
    [Tooltip("Smooth movement (disabled for more visible movement)")]
    [SerializeField] private bool smoothMovement = false;

    [Tooltip("Movement smoothing speed")]
    [SerializeField] private float smoothingSpeed = 10f;

    [Tooltip("Minimum speed to start moving (m/s)")]
    [SerializeField] private float minSpeedToMove = 0.01f;

    [Tooltip("Follow road direction automatically (DISABLED - causes direction changes on hills)")]
    [SerializeField] private bool followRoadDirection = false;

    [Tooltip("Road detection distance ahead (meters)")]
    [SerializeField] private float roadDetectionDistance = 5f;

    [Header("WebSocket Integration")]
    [Tooltip("WebSocket client for receiving real-time data from trainer")]
    [SerializeField] private WebSocketClient webSocketClient;

    [Tooltip("Send resistance commands based on terrain slope")]
    [SerializeField] private bool enableResistanceControl = true;

    [Tooltip("Resistance update interval (seconds)")]
    [SerializeField] private float resistanceUpdateInterval = 0.5f;
    
    [Tooltip("Resistance at 0% gradient (flat road baseline, 0-100)")]
    [Range(0, 100)]
    [SerializeField] private int flatResistance = 30;
    
    [Tooltip("Resistance added per 1% gradient")]
    [Range(0f, 10f)]
    [SerializeField] private float resistancePerPercent = 5f;
    
    [Tooltip("Maximum resistance sent to trainer")]
    [Range(0, 100)]
    [SerializeField] private int maxResistance = 100;

    private float lastResistanceUpdateTime = 0f;

    [Header("Simulation Mode (No Trainer Required)")]
    [Tooltip("Enable simulation mode - use keyboard input instead of trainer")]
    [SerializeField] private bool simulationMode = false;

    [Tooltip("Simulated power (Watts) - controlled by keyboard")]
    [SerializeField] private float simulatedPower = 150f;

    [Tooltip("Power increase/decrease step (Watts per key press)")]
    [SerializeField] private float powerStep = 10f;

    [Tooltip("Minimum simulated power")]
    [SerializeField] private float minSimulatedPower = 0f;

    [Tooltip("Maximum simulated power")]
    [SerializeField] private float maxSimulatedPower = 400f;

    [Header("Course Boundary")]
    [Tooltip("Stop bike at end of course")]
    [SerializeField] private bool stopAtCourseEnd = true;
    
    [Tooltip("Maximum course distance (0 = no limit). Set by trial systems automatically.")]
    [SerializeField] private float maxCourseDistance = 0f;
    
    private bool courseCompleted = false;

    [Header("References")]
    [Tooltip("Trainer adapter (legacy support)")]
    [SerializeField] private TacxTrainerAdapter trainerAdapter;

    [Header("Bike Start Settings")]
    [Tooltip("Rotate bike 90 degrees on start")]
    [SerializeField] private bool rotateBikeOnStart = false;

    private Vector3 lastPosition;
    private float lastUpdateTime;
    private Vector3 stableForwardDirection;

    private void Start()
    {
        // Find bike object if not assigned
        if (bikeObject == null)
        {
            bikeObject = GameObject.Find("Bike");
        }
        if (bikeObject == null)
        {
            bikeObject = GameObject.Find("biker");
        }
        if (bikeObject == null)
        {
            // Try to find any object with "bike" in the name under Terrain
            Terrain t = FindObjectOfType<Terrain>();
            if (t != null)
            {
                foreach (Transform child in t.transform)
                {
                    if (child.name.ToLower().Contains("bike"))
                    {
                        bikeObject = child.gameObject;
                        Debug.Log($"[BikeController] Found bike object: {bikeObject.name}");
                        break;
                    }
                }
            }
        }

        // Find terrain if not assigned
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        // Find WebSocket client if not assigned
        if (webSocketClient == null)
        {
            webSocketClient = FindObjectOfType<WebSocketClient>();
            if (webSocketClient != null)
            {
                Debug.Log("[BikeController] ✓ WebSocket client found and connected");
            }
            else
            {
                Debug.LogWarning("[BikeController] WebSocket client not found. Will use legacy trainer adapter.");
            }
        }

        // Find trainer adapter if not assigned (legacy support)
        if (trainerAdapter == null)
        {
            trainerAdapter = FindObjectOfType<TacxTrainerAdapter>();
        }

        // Find grade array controller if not assigned
        if (gradeArrayController == null)
        {
            gradeArrayController = FindObjectOfType<GradeArrayController>();
        }

        // Find spline path if not assigned
        if (splinePath == null)
        {
            splinePath = FindObjectOfType<SplinePath>();
            if (splinePath != null)
            {
                Debug.Log("[BikeController] ✓ SplinePath found — bike will follow curved road");
            }
        }

        // Find loop path mapper if not assigned
        if (loopPathMapper == null)
        {
            loopPathMapper = FindObjectOfType<LoopPathMapper>();
        }

        // Find route configuration if not assigned
        if (routeConfig == null && gradeArrayController != null)
        {
            LongRouteTerrainBuilder routeBuilder = FindObjectOfType<LongRouteTerrainBuilder>();
            if (routeBuilder != null)
            {
                routeConfig = routeBuilder.GetRouteConfiguration();
            }
        }

        // Subscribe to trainer events (legacy support)
        if (trainerAdapter != null)
        {
            trainerAdapter.OnPowerUpdated += SetPower;
            trainerAdapter.OnCadenceUpdated += SetCadence;
        }

        if (bikeObject != null)
        {
            // Check if bikeObject is a prefab asset
            #if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(bikeObject))
            {
                Debug.LogError("[BikeController] Bike object is a prefab asset! It must be an instance in the scene.");
                return;
            }
            #endif

            // Store initial position and rotation
            lastPosition = bikeObject.transform.position;

            // Initialize stable forward direction
            stableForwardDirection = bikeObject.transform.forward;
            if (stableForwardDirection.magnitude < 0.1f)
            {
                stableForwardDirection = Vector3.right;
            }

            // Rotate bike 90 degrees on Y axis when starting the game
            if (rotateBikeOnStart)
            {
                bikeObject.transform.Rotate(0, 90, 0);
                stableForwardDirection = bikeObject.transform.forward;
                Debug.Log($"✓ Bike rotated 90 degrees on start. New rotation: {bikeObject.transform.rotation.eulerAngles}");
            }
            else
            {
                Debug.Log($"✓ Bike starting at position: {bikeObject.transform.position}, rotation: {bikeObject.transform.rotation.eulerAngles}");
                Debug.Log($"✓ Bike forward direction: {bikeObject.transform.forward}");
            }
        }

        lastUpdateTime = Time.time;

        // Simulation mode: force OFF for real bike usage
        // To test without bike, change this to: simulationMode = true;
        // simulationMode = false;
        currentPower = 0f;
        simulatedPower = 150f; // Default simulated power for testing
        enableResistanceControl = true;

        Debug.Log($"[BikeController] Initialized in {speedMode} mode");
    }

    private void Update()
    {
        if (bikeObject == null)
        {
            Debug.LogError("BikeController: Bike object is null!");
            return;
        }

        if (terrain == null)
        {
            Debug.LogError("BikeController: Terrain is null!");
            return;
        }

        // If bike is locked (during cue/countdown), don't process any movement
        if (bikeLocked)
        {
            currentSpeed = 0f;
            currentPower = 0f;
            return;
        }

        // Handle simulation mode input or get real-time data from WebSocket
        if (simulationMode)
        {
            HandleSimulationInput();
        }
        else
        {
            UpdateFromWebSocket();
        }

        // Calculate current slope at bike position
        UpdateSlope();

        // Calculate speed based on mode
        if (speedMode == BikeSpeedMode.PhysicsBased)
        {
            CalculateSpeedFromPhysics();
        }
        else
        {
            // Direct mode: speed is set in UpdateFromWebSocket, just update distance
            distanceTravelled += currentSpeed * Time.deltaTime;
        }

        // Update bike position
        UpdateBikePosition();

        // Check course boundary — hard stop, no slowdown zone
        if (stopAtCourseEnd && maxCourseDistance > 0f && !courseCompleted)
        {
            if (distanceTravelled >= maxCourseDistance)
            {
                distanceTravelled = maxCourseDistance; // clamp exactly
                courseCompleted = true;
                currentSpeed = 0f;
                Debug.Log($"[BikeController] Course complete at {distanceTravelled:F0}m");
            }
        }

        // Send resistance control to trainer based on terrain slope (only if not in simulation mode)
        if (!simulationMode && enableResistanceControl && Time.time - lastResistanceUpdateTime > resistanceUpdateInterval)
        {
            SendResistanceControl();
            lastResistanceUpdateTime = Time.time;
        }

        // Legacy: Send virtual speed back to trainer adapter
        if (trainerAdapter != null)
        {
            trainerAdapter.SetVirtualBikeSpeed(currentSpeed * 3.6f);
            trainerAdapter.SetTerrainSlope(currentSlope);
        }

        // Debug every second
        if (Time.time - lastUpdateTime > 1f)
        {
            string modeStr = simulationMode ? "SIMULATION" : speedMode.ToString();
            Debug.Log($"[BikeController] Mode: {modeStr}, Power: {currentPower:F1}W, Speed: {currentSpeed:F3}m/s ({currentSpeed * 3.6f:F2}km/h), Slope: {currentSlope:F2}%, Distance: {distanceTravelled:F1}m");
            lastUpdateTime = Time.time;
        }
    }

    /// <summary>
    /// Handle keyboard input for simulation mode
    /// Up Arrow / W = Increase power
    /// Down Arrow / S = Decrease power
    /// Space = Coast (reduce power gradually)
    /// </summary>
    private void HandleSimulationInput()
    {
        // Increase power
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
        {
            simulatedPower = Mathf.Min(simulatedPower + powerStep * Time.deltaTime * 10f, maxSimulatedPower);
        }
        // Decrease power
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
        {
            simulatedPower = Mathf.Max(simulatedPower - powerStep * Time.deltaTime * 10f, minSimulatedPower);
        }
        // Space = stop pedaling (power to 0)
        else if (Input.GetKey(KeyCode.Space))
        {
            simulatedPower = Mathf.Max(simulatedPower - powerStep * Time.deltaTime * 5f, 0f);
        }
        // No input = maintain current power (constant pedaling)

        // Update current power with simulated value
        currentPower = simulatedPower;
        
        // Simulate cadence based on power (rough approximation)
        currentCadence = currentPower > 10f ? 60f + (currentPower / 5f) : 0f;
        currentCadence = Mathf.Min(currentCadence, 120f);
    }

    /// <summary>
    /// Update power, cadence, and speed from WebSocket client
    /// </summary>
    private void UpdateFromWebSocket()
    {
        if (webSocketClient == null)
        {
            // Try to re-find it in case it was on a different object
            webSocketClient = FindObjectOfType<WebSocketClient>();
            if (webSocketClient == null) return;
        }

        // Read data regardless of connection state flag
        float power = webSocketClient.GetPowerWatts();
        float cadence = webSocketClient.GetCadenceRpm();
        float speedKmh = webSocketClient.GetSpeedKmh();

        // Debug: log what we're receiving (every 2 seconds)
        if (Time.frameCount % 120 == 0)
        {
            Debug.Log($"[BikeController] WebSocket data → Power:{power:F0}W, Cadence:{cadence:F0}rpm, Speed:{speedKmh:F1}km/h, " +
                      $"Connected:{webSocketClient.IsConnected()}, CurrentSpeed:{currentSpeed:F2}m/s, Dist:{distanceTravelled:F0}m");
        }

        // Update power and cadence
        if (power > 0)
            currentPower = power;
        else if (cadence > 0 && speedKmh > 0)
        {
            // Power not reported but rider is pedaling — estimate from speed
            // Simple estimate: P = Crr*m*g*v + 0.5*CdA*rho*v³ (flat road estimate)
            float v = speedKmh / 3.6f;
            float estimatedPower = (rollingResistance * totalMass * 9.81f * v) + 
                                   (0.5f * airDensity * frontalArea * dragCoefficient * v * v * v);
            currentPower = Mathf.Max(estimatedPower, cadence * 0.8f); // At least cadence-based estimate
        }
        else
            currentPower = 0f;

        if (cadence >= 0)
            currentCadence = cadence;

        // If using direct speed mode, OR if power is unavailable but speed is reported,
        // use the trainer's speed directly for movement
        if (speedMode == BikeSpeedMode.DirectFromTrainer && speedKmh > 0)
        {
            currentSpeed = speedKmh / 3.6f;
        }
        else if (power <= 0 && speedKmh > 0.5f && cadence > 0)
        {
            // Fallback: trainer reports speed but not power — use reported speed
            // but apply gradient effect (slow down on hills, speed up downhill)
            float targetSpeed = speedKmh / 3.6f;
            float gradientFactor = 1f - (currentSlope * 0.08f); // 5% slope → 60% of flat speed
            gradientFactor = Mathf.Clamp(gradientFactor, 0.3f, 1.5f);
            float adjustedSpeed = targetSpeed * gradientFactor;
            currentSpeed = Mathf.Lerp(currentSpeed, adjustedSpeed, Time.deltaTime * 3f);
            distanceTravelled += currentSpeed * Time.deltaTime;
        }
    }

    /// <summary>
    /// Send resistance control command to trainer based on terrain slope
    /// Maps slope percentage to resistance level (0-100)
    /// </summary>
    private void SendResistanceControl()
    {
        if (webSocketClient == null)
            return;

        // Map gradient to resistance:
        // resistance = flatResistance + (gradient% × resistancePerPercent)
        float resistance = flatResistance + (currentSlope * resistancePerPercent);
        int resistanceLevel = Mathf.Clamp(Mathf.RoundToInt(resistance), 0, maxResistance);

        webSocketClient.SetResistance(resistanceLevel);
        
        // Log every time resistance is sent (every 0.5s based on resistanceUpdateInterval)
        Debug.Log($"[BikeController] RESISTANCE SENT → {resistanceLevel} (slope={currentSlope:F1}%, dist={distanceTravelled:F0}m)");
    }

    private void UpdateSlope()
    {
        // Priority 1: Use pre-computed grade array from GradeArrayController (for long routes)
        if (gradeArrayController != null)
        {
            currentSlope = gradeArrayController.GetCurrentGrade();
            return;
        }

        // Priority 2: Calculate from spline path elevation
        if (splinePath != null && splinePath.GetTotalLength() > 0f)
        {
            // Use the detailed elevation profile from CurvedRouteGenerator — most accurate
            CurvedRouteGenerator routeGen = FindObjectOfType<CurvedRouteGenerator>();
            if (routeGen != null && routeGen.GetElevationProfile() != null)
            {
                // Clamp look-ahead so we never read past the end of the profile
                float profileLen = routeGen.GetElevationProfile().Length - 1f;
                float clampedDist = Mathf.Clamp(distanceTravelled, 0f, profileLen - 5f);
                currentSlope = Mathf.Clamp(routeGen.GetGradientAtDistance(clampedDist), -15f, 15f);
                return;
            }
            
            // Fallback: sample spline positions, clamped within spline length
            float splineLen = splinePath.GetTotalLength();
            float dist  = Mathf.Clamp(distanceTravelled,       0f, splineLen - 6f);
            float dist2 = Mathf.Clamp(distanceTravelled + 5f,  0f, splineLen);
            SplinePath.SplineSample here  = splinePath.SampleAtDistance(dist);
            SplinePath.SplineSample ahead = splinePath.SampleAtDistance(dist2);
            
            if (terrain != null)
            {
                float hHere  = terrain.SampleHeight(here.position);
                float hAhead = terrain.SampleHeight(ahead.position);
                float rise = hAhead - hHere;
                float run  = Vector3.Distance(
                    new Vector3(here.position.x,  0, here.position.z),
                    new Vector3(ahead.position.x, 0, ahead.position.z));
                if (run > 0.1f)
                    currentSlope = Mathf.Clamp((rise / run) * 100f, -15f, 15f);
            }
            else
            {
                float rise = ahead.position.y - here.position.y;
                float run  = Vector3.Distance(here.position, ahead.position);
                if (run > 0.1f)
                    currentSlope = Mathf.Clamp((rise / run) * 100f, -15f, 15f);
            }
            return;
        }

        // Fallback: Calculate from terrain height (for short routes or backward compatibility)
        if (terrain == null) return;

        Vector3 bikePos = bikeObject.transform.position;
        Vector3 forward = stableForwardDirection;

        // Sample height at current position and ahead
        float heightCurrent = terrain.SampleHeight(bikePos);
        float samplingDistance = 5f;
        Vector3 aheadPos = bikePos + forward * samplingDistance;
        float heightAhead = terrain.SampleHeight(aheadPos);

        // Also sample at intermediate point for better accuracy
        Vector3 midPos = bikePos + forward * (samplingDistance * 0.5f);
        float heightMid = terrain.SampleHeight(midPos);

        // Calculate average slope using multiple samples
        float rise1 = heightMid - heightCurrent;
        float run1 = samplingDistance * 0.5f;
        float slope1 = (rise1 / run1) * 100f;

        float rise2 = heightAhead - heightMid;
        float run2 = samplingDistance * 0.5f;
        float slope2 = (rise2 / run2) * 100f;

        // Average the two slopes for smoother result
        float newSlope = (slope1 + slope2) * 0.5f;

        // Clamp slope to maximum 12%
        currentSlope = Mathf.Clamp(newSlope, -12f, 12f);
    }

    /// <summary>
    /// Calculate speed using physics-based model
    /// Uses real power from trainer + virtual gradient to calculate realistic speed
    /// This is the Zwift-like approach
    /// </summary>
    private void CalculateSpeedFromPhysics()
    {
        // Calculate resisting forces
        float gravityForce = totalMass * 9.81f * (currentSlope / 100f);
        float rollingResistanceForce = totalMass * 9.81f * rollingResistance;
        float dragForce = 0.5f * airDensity * frontalArea * dragCoefficient * currentSpeed * currentSpeed;

        float totalResistingForce = gravityForce + rollingResistanceForce + dragForce;

        // Calculate available power (accounting for drivetrain loss)
        float wheelPower = (currentPower * drivetrainEffectiveness / 100f);

        // Calculate net force
        float powerForce = 0f;

        if (currentSpeed > 0.05f)
        {
            powerForce = wheelPower / currentSpeed;
        }
        else
        {
            // When speed is very low, use stable force calculation
            float minForce = 50f;
            float estimatedForce = wheelPower * 1.5f;
            powerForce = Mathf.Max(minForce, estimatedForce);
        }

        float netForce = powerForce - totalResistingForce;

        // Calculate acceleration
        float acceleration = netForce / totalMass;

        // Update speed with damping
        currentSpeed += acceleration * Time.deltaTime;

        // Add damping when speed is very low to prevent oscillation
        if (currentSpeed < 0.1f)
        {
            currentSpeed *= 0.95f;
        }

        currentSpeed = Mathf.Max(0f, currentSpeed);

        // Update distance
        distanceTravelled += currentSpeed * Time.deltaTime;
    }

    private void UpdateBikePosition()
    {
        if (bikeObject == null)
        {
            Debug.LogError("[BikeController] Bike object is null in UpdateBikePosition!");
            return;
        }

        if (!Application.isPlaying)
        {
            return;
        }

        if (!PrefabSafetyHelper.CanModifyTransform(bikeObject))
        {
            Debug.LogError("[BikeController] Cannot modify prefab asset! Bike object must be an instance in the scene.");
            return;
        }

        // Use loop path if available, otherwise use straight path
        if (splinePath != null && splinePath.GetTotalLength() > 0f)
        {
            UpdateBikePositionSpline();
        }
        else if (loopPathMapper != null && routeConfig != null)
        {
            UpdateBikePositionLooped();
        }
        else
        {
            UpdateBikePositionStraight();
        }
    }

    private void UpdateBikePositionSpline()
    {
        if (splinePath == null) return;

        Vector3 currentPos = bikeObject.transform.position;

        if (currentSpeed > minSpeedToMove)
        {
            // distanceTravelled is already updated in CalculateSpeedFromPhysics
            // Clamp to spline length
            float clampedDist = Mathf.Clamp(distanceTravelled, 0f, splinePath.GetTotalLength());
            
            SplinePath.SplineSample sample = splinePath.SampleAtDistance(clampedDist);
            Vector3 targetPosition = sample.position;
            
            // Use spline Y directly (it has the visual elevation applied)
            // Add small offset so bike sits ON the road surface
            targetPosition.y += 0.5f;
            
            // Smooth position
            if (smoothMovement)
            {
                Vector3 newPosition = Vector3.Lerp(currentPos, targetPosition, Time.deltaTime * smoothingSpeed);
                PrefabSafetyHelper.SafeSetPosition(bikeObject, newPosition);
            }
            else
            {
                PrefabSafetyHelper.SafeSetPosition(bikeObject, targetPosition);
            }
            
            // Orient bike along spline tangent
            stableForwardDirection = sample.forward;
            if (stableForwardDirection.sqrMagnitude > 0.01f)
            {
                // Project forward onto terrain slope for realistic tilt
                Vector3 fwd = stableForwardDirection;
                if (terrain != null)
                {
                    float hAhead = terrain.SampleHeight(targetPosition + fwd * 2f);
                    float hHere = terrain.SampleHeight(targetPosition);
                    fwd.y = (hAhead - hHere) / 2f;
                    fwd.Normalize();
                }
                
                Quaternion targetRotation = Quaternion.LookRotation(fwd, Vector3.up);
                Quaternion newRotation = Quaternion.Slerp(
                    bikeObject.transform.rotation,
                    targetRotation,
                    Time.deltaTime * 5f
                );
                PrefabSafetyHelper.SafeSetRotation(bikeObject, newRotation);
            }
        }
        else
        {
            // Snap to spline position when stopped
            float clampedDist = Mathf.Clamp(distanceTravelled, 0f, splinePath.GetTotalLength());
            SplinePath.SplineSample sample = splinePath.SampleAtDistance(clampedDist);
            Vector3 targetPos = sample.position;
            if (terrain != null)
            {
                targetPos.y = terrain.SampleHeight(targetPos) + 0.5f;
            }
            
            if (Vector3.Distance(currentPos, targetPos) > 1f)
            {
                PrefabSafetyHelper.SafeSetPosition(bikeObject, targetPos);
            }
        }
        
        lastPosition = bikeObject.transform.position;
    }

    private void UpdateBikePositionLooped()
    {
        if (loopPathMapper == null || routeConfig == null) return;

        Vector3 currentPos = bikeObject.transform.position;
        float totalRouteLength = routeConfig.totalRouteLength;

        if (currentSpeed > minSpeedToMove)
        {
            float distanceIncrement = currentSpeed * Time.deltaTime;
            distanceTravelled += distanceIncrement;

            if (distanceTravelled > totalRouteLength)
            {
                distanceTravelled = totalRouteLength;
            }

            Vector3 targetPosition = loopPathMapper.GetWorldPosition(distanceTravelled, totalRouteLength, out _);
            Vector3 roadDirection = loopPathMapper.GetRoadDirection(distanceTravelled, totalRouteLength);

            if (terrain != null)
            {
                float terrainHeight = terrain.SampleHeight(targetPosition);
                targetPosition.y = terrainHeight + 0.5f;

                if (targetPosition.y < terrainHeight)
                {
                    targetPosition.y = terrainHeight + 0.5f;
                }
            }

            if (smoothMovement)
            {
                Vector3 newPosition = Vector3.Lerp(currentPos, targetPosition, Time.deltaTime * smoothingSpeed);
                PrefabSafetyHelper.SafeSetPosition(bikeObject, newPosition);
            }
            else
            {
                PrefabSafetyHelper.SafeSetPosition(bikeObject, targetPosition);
            }

            if (roadDirection.magnitude > 0.1f)
            {
                stableForwardDirection = roadDirection;
                Quaternion targetRotation = Quaternion.LookRotation(roadDirection);
                Quaternion newRotation = Quaternion.Slerp(
                    bikeObject.transform.rotation,
                    targetRotation,
                    Time.deltaTime * 5f
                );
                PrefabSafetyHelper.SafeSetRotation(bikeObject, newRotation);
            }
        }
        else
        {
            Vector3 targetPosition = loopPathMapper.GetWorldPosition(distanceTravelled, totalRouteLength, out _);
            if (terrain != null)
            {
                float terrainHeight = terrain.SampleHeight(targetPosition);
                targetPosition.y = terrainHeight + 0.5f;
            }

            if (Vector3.Distance(currentPos, targetPosition) > 1f)
            {
                PrefabSafetyHelper.SafeSetPosition(bikeObject, targetPosition);
            }
        }

        lastPosition = bikeObject.transform.position;
    }

    private void UpdateBikePositionStraight()
    {
        Vector3 currentPos = bikeObject.transform.position;
        Vector3 forward = stableForwardDirection;

        if (followRoadDirection && terrain != null && currentSpeed > 0.1f)
        {
            Vector3 newDirection = GetRoadDirection(currentPos);
            stableForwardDirection = Vector3.Slerp(stableForwardDirection, newDirection, Time.deltaTime * 2f);
            forward = stableForwardDirection;
        }

        if (currentSpeed > minSpeedToMove)
        {
            Vector3 movement = forward * currentSpeed * Time.deltaTime;
            Vector3 newPosition = currentPos + movement;

            if (terrain != null)
            {
                float terrainHeight = terrain.SampleHeight(newPosition);
                newPosition.y = terrainHeight + 0.5f;

                if (newPosition.y < terrainHeight)
                {
                    newPosition.y = terrainHeight + 0.5f;
                }
            }

            PrefabSafetyHelper.SafeSetPosition(bikeObject, newPosition);

            if (forward.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forward);
                Quaternion newRotation = Quaternion.Slerp(
                    bikeObject.transform.rotation,
                    targetRotation,
                    Time.deltaTime * 5f
                );
                PrefabSafetyHelper.SafeSetRotation(bikeObject, newRotation);
            }
        }
        else
        {
            if (terrain != null)
            {
                float terrainHeight = terrain.SampleHeight(currentPos);
                if (Mathf.Abs(currentPos.y - terrainHeight) > 0.1f)
                {
                    Vector3 correctedPos = currentPos;
                    correctedPos.y = terrainHeight + 0.5f;
                    PrefabSafetyHelper.SafeSetPosition(bikeObject, correctedPos);
                }
            }
        }

        lastPosition = bikeObject.transform.position;
    }

    private Vector3 GetRoadDirection(Vector3 position)
    {
        if (terrain == null) return stableForwardDirection;

        Vector3 rightDir = Vector3.right;
        Vector3 leftDir = Vector3.left;

        float heightCurrent = terrain.SampleHeight(position);
        float heightRight = terrain.SampleHeight(position + rightDir * 2f);
        float heightLeft = terrain.SampleHeight(position + leftDir * 2f);

        float heightChangeRight = Mathf.Abs(heightRight - heightCurrent);
        float heightChangeLeft = Mathf.Abs(heightLeft - heightCurrent);

        if (Mathf.Abs(heightChangeRight - heightChangeLeft) < 0.1f)
        {
            return stableForwardDirection;
        }

        if (heightChangeRight < heightChangeLeft - 0.2f)
        {
            return rightDir;
        }
        else if (heightChangeLeft < heightChangeRight - 0.2f)
        {
            return leftDir;
        }

        return stableForwardDirection;
    }

    public void SetPower(float power)
    {
        currentPower = power;
    }

    public void SetCadence(float cadence)
    {
        currentCadence = cadence;
    }

    public float GetSpeed()
    {
        return currentSpeed;
    }

    public float GetSpeedKmh()
    {
        return currentSpeed * 3.6f;
    }

    public float GetPower()
    {
        return currentPower;
    }

    public float GetGradient()
    {
        return Mathf.Clamp(currentSlope, -12f, 12f);
    }

    public float GetDistance()
    {
        return distanceTravelled;
    }

    public bool IsSimulationMode()
    {
        return simulationMode;
    }

    public void SetSimulationMode(bool enabled)
    {
        simulationMode = enabled;
        if (enabled)
        {
            simulatedPower = 150f;
            currentPower = 150f;
            this.enabled = true; // Make sure BikeController is active
            Debug.Log("[BikeController] SIMULATION MODE enabled — 150W constant, Up/Down to adjust");
        }
        else
        {
            simulatedPower = 0f;
            currentPower = 0f;
            Debug.Log("[BikeController] LIVE MODE — power from real bike");
        }
    }

    public float GetCurrentPower()
    {
        return currentPower;
    }

    public float GetCadence()
    {
        return currentCadence;
    }

    [ContextMenu("Test: Force Speed to 5 m/s")]
    public void TestForceSpeed()
    {
        currentSpeed = 5f;
        Debug.Log($"[BikeController] TEST: Forced speed to {currentSpeed}m/s ({currentSpeed * 3.6f}km/h)");
    }

    [ContextMenu("Switch to Physics-Based Mode")]
    public void SwitchToPhysicsMode()
    {
        speedMode = BikeSpeedMode.PhysicsBased;
        Debug.Log("[BikeController] Switched to Physics-Based speed calculation");
    }

    [ContextMenu("Switch to Direct Speed Mode")]
    public void SwitchToDirectMode()
    {
        speedMode = BikeSpeedMode.DirectFromTrainer;
        Debug.Log("[BikeController] Switched to Direct speed from trainer");
    }
    
    /// <summary>
    /// Set the maximum course distance. Bike will stop at this distance.
    /// Called by trial systems (ConfigurableTrialSequence, SimpleCompletionOverlay, etc.)
    /// </summary>
    public void SetMaxCourseDistance(float distance)
    {
        maxCourseDistance = distance;
        courseCompleted = false;
        Debug.Log($"[BikeController] Course distance set to {distance:F0}m");
    }
    
    /// <summary>
    /// Assign a SplinePath for the bike to follow. Call this after generating a curved route.
    /// </summary>
    public void SetSplinePath(SplinePath path)
    {
        splinePath = path;
        if (splinePath != null && splinePath.GetTotalLength() > 0f)
        {
            // Also clear the loop path mapper so spline takes priority
            loopPathMapper = null;
            routeConfig = null;
            Debug.Log($"[BikeController] SplinePath assigned — bike will follow curved road ({splinePath.GetTotalLength():F0}m)");
        }
    }
    
    /// <summary>
    /// Get the bike GameObject being controlled
    /// </summary>
    public GameObject GetBikeObject() => bikeObject;
    
    /// <summary>
    /// Reset course completion state (call when starting a new trial)
    /// </summary>
    public void ResetCourse()
    {
        courseCompleted = false;
        distanceTravelled = 0f;
        currentSpeed = 0f;
    }
    
    private bool bikeLocked = false;
    
    /// <summary>
    /// Lock the bike — prevents all movement (for cue/countdown phases)
    /// </summary>
    public void LockBike()
    {
        bikeLocked = true;
        currentSpeed = 0f;
    }
    
    /// <summary>
    /// Unlock the bike — allows movement again
    /// </summary>
    public void UnlockBike()
    {
        bikeLocked = false;
    }
    
    /// <summary>
    /// Check if the course has been completed
    /// </summary>
    public bool IsCourseCompleted()
    {
        return courseCompleted;
    }
}
