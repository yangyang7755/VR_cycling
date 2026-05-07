using UnityEngine;
using TMPro;

/// <summary>
/// Displays system status for debugging and monitoring
/// Shows WebSocket connection, speed mode, and resistance control status
/// </summary>
public class SystemStatusDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WebSocketClient webSocketClient;
    [SerializeField] private BikeController bikeController;
    
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private bool showInConsole = true;
    
    private float updateInterval = 2f;
    private float lastUpdateTime = 0f;

    private void Start()
    {
        // Auto-find components if not assigned
        if (webSocketClient == null)
            webSocketClient = FindObjectOfType<WebSocketClient>();
        
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        string status = "=== VIRTUAL CYCLING SYSTEM STATUS ===\n\n";
        
        // WebSocket Status
        if (webSocketClient != null)
        {
            bool connected = webSocketClient.IsConnected();
            status += $"WebSocket: {(connected ? "✓ CONNECTED" : "✗ DISCONNECTED")}\n";
            
            if (connected)
            {
                status += $"  Speed: {webSocketClient.GetSpeedKmh():F1} km/h\n";
                status += $"  Power: {webSocketClient.GetPowerWatts():F0} W\n";
                status += $"  Cadence: {webSocketClient.GetCadenceRpm():F0} RPM\n";
                status += $"  Heart Rate: {webSocketClient.GetHeartRateBpm():F0} BPM\n";
            }
        }
        else
        {
            status += "WebSocket: NOT FOUND\n";
        }
        
        status += "\n";
        
        // BikeController Status
        if (bikeController != null)
        {
            status += $"Bike Controller: ✓ ACTIVE\n";
            status += $"  Virtual Speed: {bikeController.GetSpeedKmh():F1} km/h\n";
            status += $"  Gradient: {bikeController.GetGradient():F1}%\n";
            status += $"  Distance: {bikeController.GetDistance():F0} m\n";
        }
        else
        {
            status += "Bike Controller: NOT FOUND\n";
        }
        
        status += "\n=====================================";
        
        // Update UI if available
        if (statusText != null)
        {
            statusText.text = status;
        }
        
        // Log to console if enabled
        if (showInConsole)
        {
            Debug.Log(status);
        }
    }
}
