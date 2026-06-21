using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;

/// <summary>
/// WebSocket client for connecting to Python WebSocket server
/// Receives real-time telemetry data (speed, power, cadence, heart rate, etc.)
/// </summary>
public class WebSocketClient : MonoBehaviour
{
    [Header("Connection Settings")]
    [Tooltip("WebSocket server URL (default: ws://localhost:8765)")]
    [SerializeField] private string serverUrl = "ws://localhost:8765";
    
    [Tooltip("Auto-connect on start")]
    [SerializeField] private bool autoConnectOnStart = true;
    
    [Tooltip("Reconnect automatically on disconnect")]
    [SerializeField] private bool autoReconnect = true;
    
    [Tooltip("Reconnect delay in seconds")]
    [SerializeField] private float reconnectDelay = 5f;
    
    [Tooltip("Max reconnection attempts (0 = infinite)")]
    [SerializeField] private int maxReconnectAttempts = 10;
    
    [Tooltip("Connection timeout in seconds")]
    [SerializeField] private float connectionTimeoutSeconds = 5f;
    
    [Tooltip("Suppress repeated connection error logs")]
    [SerializeField] private bool suppressRepeatedErrors = true;
    
    private int reconnectAttempts = 0;
    private bool hasLoggedInitialError = false;

    [Header("Status")]
    [SerializeField] private bool isConnected = false;
    [SerializeField] private bool isConnecting = false;

    [Header("Received Data")]
    [SerializeField] private float speedKmh = 0f;
    [SerializeField] private float powerWatts = 0f;
    [SerializeField] private float cadenceRpm = 0f;
    [SerializeField] private float heartRateBpm = 0f;
    [SerializeField] private int resistanceLevel = 0;

    [Header("Data Freshness")]
    [Tooltip("Seconds after which telemetry is considered stale (returns 0)")]
    [SerializeField] private float dataStaleTimeout = 2.0f;
    private float lastTelemetryTime = 0f;

    // Events
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<TelemetryData> OnTelemetryReceived;

    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellationTokenSource;
    private Task receiveTask;

    [System.Serializable]
    public class TelemetryData
    {
        public string type;
        public float? speed_kmh;
        public float? power_watts;
        public float? cadence_rpm;
        public float? heart_rate_bpm;
        public int? resistance_level;
        public double? speed_timestamp;
        public double? speed_monotonic;
    }

    private void Start()
    {
        if (autoConnectOnStart)
        {
            Connect();
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    /// <summary>
    /// Connect to WebSocket server
    /// </summary>
    public async void Connect()
    {
        if (isConnecting || isConnected)
        {
            Debug.Log("[WebSocketClient] Already connected or connecting");
            return;
        }

        isConnecting = true;
        
        if (!hasLoggedInitialError || !suppressRepeatedErrors)
        {
            Debug.Log($"[WebSocketClient] Connecting to {serverUrl}...");
        }

        try
        {
            webSocket = new ClientWebSocket();
            cancellationTokenSource = new CancellationTokenSource();
            
            // Add connection timeout
            CancellationTokenSource timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(connectionTimeoutSeconds));
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationTokenSource.Token, timeoutCts.Token);
            
            Uri serverUri = new Uri(serverUrl);
            await webSocket.ConnectAsync(serverUri, linkedCts.Token);

            isConnected = true;
            isConnecting = false;
            reconnectAttempts = 0;
            hasLoggedInitialError = false;
            Debug.Log("[WebSocketClient] ✓ Connected to WebSocket server");

            OnConnected?.Invoke();

            // Start receiving messages
            receiveTask = ReceiveMessages();
            
            // Clean up timeout token
            timeoutCts.Dispose();
            linkedCts.Dispose();
        }
        catch (OperationCanceledException)
        {
            isConnecting = false;
            
            if (!hasLoggedInitialError)
            {
                Debug.LogWarning($"[WebSocketClient] Connection timed out after {connectionTimeoutSeconds}s. " +
                    "Is the Python WebSocket server running? Start it with: python websocket_server.py");
                hasLoggedInitialError = true;
            }
            else if (!suppressRepeatedErrors)
            {
                Debug.LogWarning($"[WebSocketClient] Reconnect attempt #{reconnectAttempts} timed out");
            }
            
            if (autoReconnect)
            {
                StartCoroutine(ReconnectCoroutine());
            }
        }
        catch (Exception e)
        {
            isConnecting = false;
            
            if (!hasLoggedInitialError)
            {
                Debug.LogWarning($"[WebSocketClient] Connection failed: {e.Message}");
                Debug.LogWarning("[WebSocketClient] Make sure the Python WebSocket server is running on " + serverUrl);
                Debug.LogWarning("[WebSocketClient] Will keep retrying in the background...");
                hasLoggedInitialError = true;
            }
            else if (!suppressRepeatedErrors)
            {
                Debug.LogWarning($"[WebSocketClient] Reconnect attempt #{reconnectAttempts} failed: {e.Message}");
            }
            
            if (autoReconnect)
            {
                StartCoroutine(ReconnectCoroutine());
            }
        }
    }

    /// <summary>
    /// Disconnect from WebSocket server
    /// </summary>
    public void Disconnect()
    {
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        if (webSocket != null)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait(1000);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WebSocketClient] Error during close: {e.Message}");
                }
            }
            webSocket.Dispose();
            webSocket = null;
        }

        if (isConnected)
        {
            isConnected = false;
            OnDisconnected?.Invoke();
            Debug.Log("[WebSocketClient] Disconnected from WebSocket server");
        }
    }

    private async Task ReceiveMessages()
    {
        byte[] buffer = new byte[4096];
        
        while (webSocket != null && webSocket.State == WebSocketState.Open && !cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    cancellationTokenSource.Token
                );

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("[WebSocketClient] Server closed connection");
                    break;
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    try
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessMessage(message);
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogWarning($"[WebSocketClient] Error parsing message: {parseEx.Message}");
                        // Don't break - continue receiving
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[WebSocketClient] Receive operation cancelled");
                break;
            }
            catch (WebSocketException wsEx)
            {
                Debug.LogError($"[WebSocketClient] WebSocket error: {wsEx.Message}");
                Debug.LogError($"[WebSocketClient] WebSocket state: {webSocket?.State}");
                break;
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketClient] Error receiving message: {e.Message}");
                Debug.LogError($"[WebSocketClient] Exception type: {e.GetType().Name}");
                Debug.LogError($"[WebSocketClient] WebSocket state: {webSocket?.State}");
                break;
            }
        }

        // If we exit the loop, we're disconnected
        Debug.LogWarning($"[WebSocketClient] Exited receive loop. WebSocket state: {webSocket?.State}");
        if (isConnected)
        {
            isConnected = false;
            OnDisconnected?.Invoke();
            
            if (autoReconnect)
            {
                Debug.Log("[WebSocketClient] Attempting to reconnect...");
                StartCoroutine(ReconnectCoroutine());
            }
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            // Unity's JsonUtility doesn't handle nullable types well, so we parse manually
            // or use a simple JSON parsing approach
            TelemetryData data = ParseTelemetryMessage(message);
            
            if (data != null && data.type == "telemetry")
            {
                // Mark that fresh data arrived
                lastTelemetryTime = Time.time;

                // Update local values
                if (data.speed_kmh.HasValue)
                    speedKmh = data.speed_kmh.Value;
                
                if (data.power_watts.HasValue)
                    powerWatts = data.power_watts.Value;
                
                if (data.cadence_rpm.HasValue)
                    cadenceRpm = data.cadence_rpm.Value;
                
                if (data.heart_rate_bpm.HasValue)
                {
                    heartRateBpm = data.heart_rate_bpm.Value;
                    if (heartRateBpm > 0 && Time.frameCount % 300 == 0)
                        Debug.Log($"[WebSocketClient] HR: {heartRateBpm:F0} bpm");
                }
                
                if (data.resistance_level.HasValue)
                    resistanceLevel = data.resistance_level.Value;

                // Trigger event
                OnTelemetryReceived?.Invoke(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocketClient] Error parsing message: {e.Message}\nMessage: {message}");
        }
    }

    private TelemetryData ParseTelemetryMessage(string json)
    {
        // Simple JSON parser for telemetry messages
        // Unity's JsonUtility doesn't handle nullable types, so we extract values manually
        TelemetryData data = new TelemetryData();
        
        try
        {
            // Use JsonUtility for basic structure, then manually parse nullable fields
            // Create a wrapper class without nullable types for JsonUtility
            SimpleTelemetryData simple = JsonUtility.FromJson<SimpleTelemetryData>(json);
            
            data.type = simple.type;
            data.speed_kmh = simple.speed_kmh > -999 ? (float?)simple.speed_kmh : null;
            data.power_watts = simple.power_watts > -999 ? (float?)simple.power_watts : null;
            data.cadence_rpm = simple.cadence_rpm > -999 ? (float?)simple.cadence_rpm : null;
            data.heart_rate_bpm = simple.heart_rate_bpm > -999 ? (float?)simple.heart_rate_bpm : null;
            data.resistance_level = simple.resistance_level > -999 ? (int?)simple.resistance_level : null;
            data.speed_timestamp = simple.speed_timestamp > -999 ? (double?)simple.speed_timestamp : null;
            data.speed_monotonic = simple.speed_monotonic > -999 ? (double?)simple.speed_monotonic : null;
        }
        catch
        {
            // Fallback: try to extract values using string parsing
            data.type = ExtractJsonValue(json, "type");
            
            float speed = ExtractJsonFloat(json, "speed_kmh");
            data.speed_kmh = speed > -999 ? (float?)speed : null;
            
            float power = ExtractJsonFloat(json, "power_watts");
            data.power_watts = power > -999 ? (float?)power : null;
            
            float cadence = ExtractJsonFloat(json, "cadence_rpm");
            data.cadence_rpm = cadence > -999 ? (float?)cadence : null;
            
            float hr = ExtractJsonFloat(json, "heart_rate_bpm");
            data.heart_rate_bpm = hr > -999 ? (float?)hr : null;
            
            int resistance = ExtractJsonInt(json, "resistance_level");
            data.resistance_level = resistance > -999 ? (int?)resistance : null;
        }
        
        return data;
    }

    private string ExtractJsonValue(string json, string key)
    {
        string searchKey = "\"" + key + "\"";
        int keyIndex = json.IndexOf(searchKey);
        if (keyIndex == -1) return "";
        
        int colonIndex = json.IndexOf(":", keyIndex);
        if (colonIndex == -1) return "";
        
        int startIndex = colonIndex + 1;
        while (startIndex < json.Length && (json[startIndex] == ' ' || json[startIndex] == '\t'))
            startIndex++;
        
        if (startIndex >= json.Length) return "";
        
        int endIndex = startIndex;
        if (json[startIndex] == '"')
        {
            endIndex = json.IndexOf('"', startIndex + 1);
            if (endIndex == -1) return "";
            return json.Substring(startIndex + 1, endIndex - startIndex - 1);
        }
        else
        {
            while (endIndex < json.Length && json[endIndex] != ',' && json[endIndex] != '}' && json[endIndex] != ' ')
                endIndex++;
            return json.Substring(startIndex, endIndex - startIndex);
        }
    }

    private float ExtractJsonFloat(string json, string key)
    {
        string value = ExtractJsonValue(json, key);
        if (string.IsNullOrEmpty(value) || value == "null") return -1000f;
        if (float.TryParse(value, out float result)) return result;
        return -1000f;
    }

    private int ExtractJsonInt(string json, string key)
    {
        string value = ExtractJsonValue(json, key);
        if (string.IsNullOrEmpty(value) || value == "null") return -1000;
        if (int.TryParse(value, out int result)) return result;
        return -1000;
    }

    [System.Serializable]
    private class SimpleTelemetryData
    {
        public string type;
        public float speed_kmh = -1000f;
        public float power_watts = -1000f;
        public float cadence_rpm = -1000f;
        public float heart_rate_bpm = -1000f;
        public int resistance_level = -1000;
        public double speed_timestamp = -1000.0;
        public double speed_monotonic = -1000.0;
    }

    /// <summary>
    /// Send resistance control command to server
    /// </summary>
    public async void SetResistance(int resistance)
    {
        if (!isConnected || webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("[WebSocketClient] Cannot send resistance command: not connected");
            return;
        }

        try
        {
            string message = $"{{\"type\":\"set_resistance\",\"resistance\":{resistance}}}";
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationTokenSource.Token
            );
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketClient] Error sending resistance command: {e.Message}");
        }
    }

    private IEnumerator ReconnectCoroutine()
    {
        // Exponential backoff: delay increases with each attempt (capped at 30s)
        float delay = Mathf.Min(reconnectDelay * Mathf.Pow(1.5f, reconnectAttempts), 30f);
        yield return new WaitForSeconds(delay);
        
        if (!isConnected && autoReconnect)
        {
            reconnectAttempts++;
            
            if (maxReconnectAttempts > 0 && reconnectAttempts > maxReconnectAttempts)
            {
                Debug.LogWarning($"[WebSocketClient] Max reconnection attempts ({maxReconnectAttempts}) reached. " +
                    "Stopping auto-reconnect. Call Connect() manually or restart the scene.");
                yield break;
            }
            
            if (!suppressRepeatedErrors)
            {
                Debug.Log($"[WebSocketClient] Reconnection attempt #{reconnectAttempts} (delay: {delay:F1}s)...");
            }
            
            Connect();
        }
    }

    // Public getters — return 0 if data is stale (no fresh telemetry for dataStaleTimeout seconds)
    private bool IsDataFresh() => (Time.time - lastTelemetryTime) < dataStaleTimeout;
    public float GetSpeedKmh() => IsDataFresh() ? speedKmh : 0f;
    public float GetPowerWatts() => IsDataFresh() ? powerWatts : 0f;
    public float GetCadenceRpm() => IsDataFresh() ? cadenceRpm : 0f;
    public float GetHeartRateBpm() => IsDataFresh() ? heartRateBpm : 0f;
    public int GetResistanceLevel() => resistanceLevel;
    public bool IsConnected() => isConnected;
}
