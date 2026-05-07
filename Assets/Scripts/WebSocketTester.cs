using UnityEngine;

/// <summary>
/// Test WebSocket connection - attach to any GameObject
/// </summary>
public class WebSocketTester : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("=== WebSocket Connection Test ===");
        
        // Find WebSocket client
        WebSocketClient client = FindObjectOfType<WebSocketClient>();
        
        if (client == null)
        {
            Debug.LogError("❌ WebSocketClient not found in scene!");
            Debug.LogError("Create a GameObject with WebSocketClient component");
            return;
        }
        
        Debug.Log("✓ WebSocketClient found");
        Debug.Log($"Server URL: {client.GetType().GetField("serverUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(client)}");
        Debug.Log($"Is Connected: {client.IsConnected()}");
        
        // Try to connect
        if (!client.IsConnected())
        {
            Debug.Log("Attempting manual connection...");
            client.Connect();
        }
        
        // Check again after 2 seconds
        Invoke("CheckConnection", 2f);
    }
    
    private void CheckConnection()
    {
        WebSocketClient client = FindObjectOfType<WebSocketClient>();
        if (client != null)
        {
            Debug.Log($"Connection status after 2s: {client.IsConnected()}");
            
            if (client.IsConnected())
            {
                Debug.Log("✓✓✓ WebSocket CONNECTED! ✓✓✓");
                float hr = client.GetHeartRateBpm();
                Debug.Log($"Heart Rate: {hr} BPM");
            }
            else
            {
                Debug.LogError("❌ WebSocket still NOT connected");
                Debug.LogError("Check:");
                Debug.LogError("1. Python backend running?");
                Debug.LogError("2. See 'WebSocket server listening' in terminal?");
                Debug.LogError("3. Port 8765 not blocked by firewall?");
            }
        }
    }
}
