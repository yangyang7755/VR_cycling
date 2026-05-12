using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Connects to the Python WebSocket server and exposes live bike + HR telemetry.
/// Attach this to any GameObject in your scene.
/// 
/// Requires NativeWebSocket: https://github.com/endel/NativeWebSocket
///   Install via Unity Package Manager → Add package from git URL:
///   https://github.com/endel/NativeWebSocket.git#upm
/// </summary>
public class BikeTelemetryClient : MonoBehaviour
{
    [Header("Connection")]
    [Tooltip("WebSocket URL of the Python server")]
    public string serverUrl = "ws://localhost:8765";
    public float reconnectDelay = 3f;

    [Header("Live Telemetry (read-only)")]
    public float SpeedKmh;
    public float PowerWatts;
    public float CadenceRpm;
    public float HeartRateBpm;
    public int ResistanceLevel;

    // Event fired on the main thread whenever new telemetry arrives
    public event Action<TelemetryData> OnTelemetryReceived;

    private NativeWebSocket.WebSocket _ws;
    private bool _shouldReconnect = true;
    private readonly Queue<TelemetryData> _incoming = new Queue<TelemetryData>();

    [Serializable]
    public class TelemetryData
    {
        public string type;
        public float speed_kmh;
        public float power_watts;
        public float cadence_rpm;
        public float heart_rate_bpm;
        public int resistance_level;
    }

    [Serializable]
    private class ResistanceCommand
    {
        public string type = "set_resistance";
        public int resistance;
    }

    async void Start()
    {
        await Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _ws?.DispatchMessageQueue();
#endif
        // Process incoming telemetry on main thread
        lock (_incoming)
        {
            while (_incoming.Count > 0)
            {
                var data = _incoming.Dequeue();
                SpeedKmh = data.speed_kmh;
                PowerWatts = data.power_watts;
                CadenceRpm = data.cadence_rpm;
                HeartRateBpm = data.heart_rate_bpm;
                ResistanceLevel = data.resistance_level;
                OnTelemetryReceived?.Invoke(data);
            }
        }
    }

    async System.Threading.Tasks.Task Connect()
    {
        _ws = new NativeWebSocket.WebSocket(serverUrl);

        _ws.OnOpen += () =>
        {
            Debug.Log($"[BikeTelemetry] Connected to {serverUrl}");
        };

        _ws.OnMessage += (bytes) =>
        {
            string msg = Encoding.UTF8.GetString(bytes);
            try
            {
                var data = JsonUtility.FromJson<TelemetryData>(msg);
                if (data != null && data.type == "telemetry")
                {
                    lock (_incoming)
                    {
                        _incoming.Enqueue(data);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BikeTelemetry] Parse error: {e.Message}");
            }
        };

        _ws.OnClose += (code) =>
        {
            Debug.Log($"[BikeTelemetry] Disconnected (code {code})");
            if (_shouldReconnect)
                StartCoroutine(ReconnectAfterDelay());
        };

        _ws.OnError += (err) =>
        {
            Debug.LogWarning($"[BikeTelemetry] Error: {err}");
        };

        await _ws.Connect();
    }

    IEnumerator ReconnectAfterDelay()
    {
        Debug.Log($"[BikeTelemetry] Reconnecting in {reconnectDelay}s...");
        yield return new WaitForSeconds(reconnectDelay);
        if (_shouldReconnect)
        {
            _ = Connect();
        }
    }

    /// <summary>
    /// Send a resistance level (0-100) to the bike via the Python server.
    /// Call this from your game logic, e.g. when terrain slope changes.
    /// </summary>
    public async void SetResistance(int level)
    {
        if (_ws == null || _ws.State != NativeWebSocket.WebSocketState.Open)
            return;

        var cmd = new ResistanceCommand { resistance = Mathf.Clamp(level, 0, 100) };
        string json = JsonUtility.ToJson(cmd);
        await _ws.SendText(json);
    }

    async void OnDestroy()
    {
        _shouldReconnect = false;
        if (_ws != null)
            await _ws.Close();
    }
}
