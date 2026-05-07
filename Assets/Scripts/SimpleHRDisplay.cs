using UnityEngine;
using TMPro;

/// <summary>
/// Simple heart rate display - shows HR from WebSocket
/// </summary>
public class SimpleHRDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WebSocketClient webSocketClient;
    [SerializeField] private TextMeshProUGUI hrText;
    
    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.5f;
    
    private float lastUpdateTime = 0f;

    private void Start()
    {
        // Auto-find WebSocket client
        if (webSocketClient == null)
        {
            webSocketClient = FindObjectOfType<WebSocketClient>();
            if (webSocketClient == null)
            {
                Debug.LogError("[SimpleHRDisplay] WebSocketClient not found!");
            }
            else
            {
                Debug.Log("[SimpleHRDisplay] WebSocketClient found!");
                // Try to connect if not connected
                if (!webSocketClient.IsConnected())
                {
                    Debug.Log("[SimpleHRDisplay] Attempting to connect WebSocket...");
                    webSocketClient.Connect();
                }
            }
        }
        
        // Check if text is assigned
        if (hrText == null)
        {
            Debug.LogError("[SimpleHRDisplay] HR Text not assigned!");
        }
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (hrText == null || webSocketClient == null)
            return;
        
        bool isConnected = webSocketClient.IsConnected();
        
        // Debug log every 2 seconds
        if (Time.frameCount % 120 == 0)
        {
            Debug.Log($"[SimpleHRDisplay] WebSocket connected: {isConnected}");
        }
        
        if (isConnected)
        {
            float hr = webSocketClient.GetHeartRateBpm();
            
            if (Time.frameCount % 120 == 0)
            {
                Debug.Log($"[SimpleHRDisplay] HR value: {hr}");
            }
            
            if (hr > 0f)
            {
                hrText.text = $"HR: {hr:F0} BPM";
                hrText.color = GetHRColor(hr);
            }
            else
            {
                hrText.text = "HR: -- BPM";
                hrText.color = Color.gray;
            }
        }
        else
        {
            hrText.text = "HR: Not Connected";
            hrText.color = Color.gray;
        }
    }
    
    private Color GetHRColor(float hr)
    {
        // Color code by HR zone
        if (hr < 100) return new Color(0.5f, 0.8f, 1f); // Blue - resting
        else if (hr < 140) return new Color(0.3f, 1f, 0.3f); // Green - fat burn
        else if (hr < 170) return new Color(1f, 0.8f, 0.2f); // Yellow - cardio
        else return new Color(1f, 0.3f, 0.3f); // Red - peak
    }
}
