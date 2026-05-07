using UnityEngine;
using System;

/// <summary>
/// Adapter for Garmin Tacx bike trainer
/// Handles connection via ANT+ or Bluetooth and data exchange
/// </summary>
public class TacxTrainerAdapter : MonoBehaviour
{
    [Header("Connection Settings")]
    [Tooltip("Connection type: ANT+ or Bluetooth")]
    [SerializeField] private ConnectionType connectionType = ConnectionType.ANT;
    
    [Tooltip("Auto-connect on start (will be disabled until StartScreenUI allows it)")]
    [SerializeField] private bool autoConnectOnStart = false; // Changed to false - StartScreenUI will enable this
    
    [Tooltip("Trainer device ID (0 = auto-detect)")]
    [SerializeField] private int trainerDeviceID = 0;

    [Header("Status")]
    [SerializeField] private bool isConnected = false;
    [SerializeField] private bool isScanning = false;

    [Header("Trainer Data (Read from Trainer)")]
    [SerializeField] private float currentPower = 0f; // Watts
    [SerializeField] private float currentCadence = 0f; // RPM
    [SerializeField] private float currentSpeed = 0f; // km/h
    [SerializeField] private float currentDistance = 0f; // meters

    [Header("Virtual Bike Data (Send to Trainer)")]
    [Tooltip("Current virtual bike speed to send to trainer")]
    [SerializeField] private float virtualBikeSpeed = 0f; // km/h
    
    [Tooltip("Current terrain slope to send to trainer")]
    [SerializeField] private float terrainSlope = 0f; // percentage

    [Header("References")]
    [Tooltip("Bike controller to receive power/cadence")]
    [SerializeField] private BikeController bikeController;

    // Events for data updates
    public event Action<float> OnPowerUpdated;
    public event Action<float> OnCadenceUpdated;
    public event Action<float> OnSpeedUpdated;
    public event Action OnTrainerConnected;
    public event Action OnTrainerDisconnected;

    // For ANT+ connection (using existing RealBike system from BreathlessVR)
    // Note: RealBike class needs to be copied from BreathlessVR-main/Assets/BreathlessVR/Scripts/RealBike.cs
    private MonoBehaviour realBike; // Using MonoBehaviour to avoid compile error if RealBike doesn't exist
    
    // For simulation mode (when no trainer connected)
    [Header("Simulation Mode")]
    [Tooltip("Enable simulation mode (for testing without trainer)")]
    [SerializeField] private bool useSimulationMode = false;
    
    [Tooltip("Simulated power (watts)")]
    [SerializeField] private float simulatedPower = 150f;
    
    [Tooltip("Simulated cadence (RPM)")]
    [SerializeField] private float simulatedCadence = 80f;

    public enum ConnectionType
    {
        ANT,
        Bluetooth,
        Simulation
    }

    private void Start()
    {
        // Disable this legacy adapter when WebSocket bridge is handling bike data
        WebSocketClient wsClient = FindObjectOfType<WebSocketClient>();
        if (wsClient != null)
        {
            Debug.Log("[TacxTrainerAdapter] WebSocketClient found — disabling legacy TacxTrainerAdapter (WebSocket handles bike data)");
            useSimulationMode = false;
            enabled = false;
            return;
        }

        // Auto-find BikeController if not assigned
        if (bikeController == null)
        {
            bikeController = FindObjectOfType<BikeController>();
            if (bikeController != null)
            {
                Debug.Log("[TacxTrainerAdapter] Auto-found BikeController component");
            }
            else
            {
                Debug.LogError("[TacxTrainerAdapter] BikeController not found! Please assign it in Inspector.");
            }
        }

        if (autoConnectOnStart)
        {
            ConnectToTrainer();
        }
    }

    private void Update()
    {
        if (useSimulationMode || connectionType == ConnectionType.Simulation)
        {
            // Use simulated data
            UpdatePower(simulatedPower);
            UpdateCadence(simulatedCadence);
            UpdateSpeed(simulatedPower * 0.1f); // Simple conversion for simulation
            
            // Debug log every second
            if (Time.frameCount % 60 == 0) // Every ~1 second at 60fps
            {
                Debug.Log($"[Simulation] Power: {simulatedPower}W, Cadence: {simulatedCadence}RPM, Speed: {currentSpeed:F2}km/h");
            }
        }
        else if (isConnected)
        {
            // Update data from real trainer (handled by RealBike callbacks)
            // Data is updated via OnReceiveData callbacks
        }
    }

    /// <summary>
    /// Connect to Tacx trainer
    /// </summary>
    [ContextMenu("Connect to Trainer")]
    public void ConnectToTrainer()
    {
        if (useSimulationMode || connectionType == ConnectionType.Simulation)
        {
            isConnected = true;
            OnTrainerConnected?.Invoke();
            Debug.Log("✓ Connected to trainer (Simulation Mode)");
            Debug.Log($"  Simulated Power: {simulatedPower}W");
            Debug.Log($"  Simulated Cadence: {simulatedCadence}RPM");
            Debug.Log("  Data will be sent to BikeController automatically");
            return;
        }

        if (connectionType == ConnectionType.ANT)
        {
            ConnectViaANT();
        }
        else if (connectionType == ConnectionType.Bluetooth)
        {
            ConnectViaBluetooth();
        }
    }

    private void ConnectViaANT()
    {
        Debug.Log("Attempting to connect to Tacx trainer via ANT+...");
        
        // Try to find existing RealBike component using reflection (to avoid compile error if class doesn't exist)
        System.Type realBikeType = System.Type.GetType("RealBike");
        if (realBikeType != null)
        {
            MonoBehaviour[] allMonoBehaviours = FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour mb in allMonoBehaviours)
            {
                if (mb.GetType() == realBikeType)
                {
                    realBike = mb;
                    break;
                }
            }
            
            if (realBike == null)
            {
                // Create RealBike component using reflection
                GameObject realBikeObj = new GameObject("RealBike");
                realBike = realBikeObj.AddComponent(realBikeType) as MonoBehaviour;
                
                // Set properties using reflection
                var autoStartScanProp = realBikeType.GetProperty("autoStartScan");
                if (autoStartScanProp != null) autoStartScanProp.SetValue(realBike, true);
                
                var deviceIDProp = realBikeType.GetProperty("deviceID");
                if (deviceIDProp != null) deviceIDProp.SetValue(realBike, trainerDeviceID);
            }
        }
        else
        {
            Debug.LogError("RealBike class not found! Please copy RealBike.cs from BreathlessVR-main/Assets/BreathlessVR/Scripts/ to PAIN_LAB/Assets/Scripts/");
            Debug.LogError("Alternatively, use Simulation mode for testing.");
            return;
        }

        isScanning = true;
        
        // Check connection status periodically
        InvokeRepeating("CheckANTConnection", 1f, 1f);
    }

    private void ConnectViaBluetooth()
    {
        Debug.Log("Attempting to connect to Tacx trainer via Bluetooth...");
        Debug.LogWarning("Bluetooth connection requires additional setup.");
        Debug.LogWarning("Options:");
        Debug.LogWarning("  1. Use ANT+ USB dongle (recommended - works immediately)");
        Debug.LogWarning("  2. Install a Bluetooth plugin for Unity");
        Debug.LogWarning("  3. Use a bridge application (Zwift, TrainerRoad)");
        Debug.LogWarning("");
        Debug.LogWarning("For now, please:");
        Debug.LogWarning("  - Use ANT+ connection (set Connection Type to ANT), OR");
        Debug.LogWarning("  - Use Simulation mode for testing");
        
        // TODO: Implement Bluetooth connection
        // This would require:
        // - A Bluetooth plugin (e.g., from Unity Asset Store)
        // - Or system Bluetooth APIs (Windows.Devices.Bluetooth / Core Bluetooth)
        // - Tacx uses BLE services:
        //   * Cycling Power Service (0x1818)
        //   * Cycling Speed and Cadence Service (0x1816)
        
        // For now, fall back to simulation mode
        useSimulationMode = true;
        connectionType = ConnectionType.Simulation;
        Debug.LogWarning("Falling back to Simulation mode. Please use ANT+ for real trainer connection.");
    }

    private void CheckANTConnection()
    {
        if (realBike == null) return;
        
        // Use reflection to access RealBike properties
        System.Type realBikeType = realBike.GetType();
        var connectedProp = realBikeType.GetProperty("connected");
        var powerProp = realBikeType.GetProperty("instantaneousPower");
        var cadenceProp = realBikeType.GetProperty("cadence");
        var speedProp = realBikeType.GetProperty("speed");
        var distanceProp = realBikeType.GetProperty("distanceTraveled");
        
        if (connectedProp == null || powerProp == null || cadenceProp == null || speedProp == null || distanceProp == null)
        {
            Debug.LogWarning("RealBike properties not accessible. Using simulation mode.");
            return;
        }
        
        bool connected = (bool)connectedProp.GetValue(realBike);
        
        if (connected)
        {
            if (!isConnected)
            {
                isConnected = true;
                isScanning = false;
                OnTrainerConnected?.Invoke();
                Debug.Log("✓ Connected to Tacx trainer via ANT+");
                CancelInvoke("CheckANTConnection");
            }

            // Update data from RealBike using reflection
            int power = (int)powerProp.GetValue(realBike);
            int cadence = (int)cadenceProp.GetValue(realBike);
            float speed = (float)speedProp.GetValue(realBike);
            int distance = (int)distanceProp.GetValue(realBike);
            
            UpdatePower(power);
            UpdateCadence(cadence);
            UpdateSpeed(speed);
            UpdateDistance(distance);
        }
    }

    /// <summary>
    /// Disconnect from trainer
    /// </summary>
    [ContextMenu("Disconnect from Trainer")]
    public void DisconnectFromTrainer()
    {
        if (realBike != null)
        {
            // RealBike doesn't have explicit disconnect, but we can stop using it
            realBike = null;
        }

        isConnected = false;
        isScanning = false;
        OnTrainerDisconnected?.Invoke();
        Debug.Log("Disconnected from trainer");
    }

    private void UpdatePower(float power)
    {
        if (Mathf.Abs(currentPower - power) > 0.1f) // Only update if changed significantly
        {
            float oldPower = currentPower;
            currentPower = power;
            OnPowerUpdated?.Invoke(currentPower);
            
            Debug.Log($"[TacxTrainerAdapter] Power: {oldPower:F1}W → {currentPower:F1}W");
            
            // Auto-find BikeController if still null
            if (bikeController == null)
            {
                bikeController = FindObjectOfType<BikeController>();
            }
            
            // Send to bike controller
            if (bikeController != null)
            {
                bikeController.SetPower(currentPower);
            }
            else
            {
                Debug.LogError("[TacxTrainerAdapter] BikeController is null! Cannot send power data. Please assign BikeController in Inspector.");
            }
        }
    }

    private void UpdateCadence(float cadence)
    {
        if (Mathf.Abs(currentCadence - cadence) > 0.1f)
        {
            currentCadence = cadence;
            OnCadenceUpdated?.Invoke(currentCadence);
            
            // Auto-find BikeController if still null
            if (bikeController == null)
            {
                bikeController = FindObjectOfType<BikeController>();
            }
            
            // Send to bike controller
            if (bikeController != null)
            {
                bikeController.SetCadence(currentCadence);
            }
            else
            {
                Debug.LogError("[TacxTrainerAdapter] BikeController is null! Cannot send cadence data.");
            }
        }
    }

    private void UpdateSpeed(float speed)
    {
        if (Mathf.Abs(currentSpeed - speed) > 0.1f)
        {
            currentSpeed = speed;
            OnSpeedUpdated?.Invoke(currentSpeed);
        }
    }

    private void UpdateDistance(float distance)
    {
        currentDistance = distance;
    }

    /// <summary>
    /// Set virtual bike speed to send to trainer (for ERG mode)
    /// </summary>
    public void SetVirtualBikeSpeed(float speed)
    {
        virtualBikeSpeed = speed;
        
        // If trainer supports ERG mode, send speed back
        // This would control trainer resistance based on virtual terrain
        if (isConnected && realBike != null)
        {
            // Use reflection to call SetSlope method
            System.Type realBikeType = realBike.GetType();
            var setSlopeMethod = realBikeType.GetMethod("SetSlope");
            if (setSlopeMethod != null)
            {
                // Convert virtual speed to slope/resistance (simplified)
                // In reality, this would depend on trainer capabilities
                setSlopeMethod.Invoke(realBike, new object[] { terrainSlope });
            }
        }
    }

    /// <summary>
    /// Set terrain slope to send to trainer
    /// </summary>
    public void SetTerrainSlope(float slope)
    {
        terrainSlope = slope;
        
        // Send slope to trainer if it supports simulation mode
        if (isConnected && realBike != null)
        {
            // Use reflection to call SetSlope method
            System.Type realBikeType = realBike.GetType();
            var setSlopeMethod = realBikeType.GetMethod("SetSlope");
            if (setSlopeMethod != null)
            {
                setSlopeMethod.Invoke(realBike, new object[] { slope });
            }
        }
    }

    // Public getters
    public float GetPower() => currentPower;
    public float GetCadence() => currentCadence;
    public float GetSpeed() => currentSpeed;
    public float GetDistance() => currentDistance;
    public bool IsConnected() => isConnected;
}
