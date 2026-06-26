using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// Sends timestamped event markers from Unity to external systems via UDP.
/// Used for synchronizing EEG, EMG, and other recording systems with Unity events.
/// 
/// Events sent:
///   TRIAL_START      — trial begins (includes trial number, gradient)
///   TRIAL_END        — trial ends (includes duration, avg speed)
///   CLIMB_START      — gradient exceeds threshold
///   CLIMB_END        — gradient drops below threshold
///   PAIN_VAS_SHOW    — pain rating scale appears
///   PAIN_VAS_RESPONSE — pain rating submitted (includes value 0-100)
///   REWARD_VAS_SHOW  — reward rating scale appears
///   REWARD_VAS_RESPONSE — reward rating submitted (includes value 0-100)
///   GRADIENT_CHANGE  — significant gradient change detected
///   CUSTOM           — any custom event
/// 
/// Protocol: UDP packets, JSON format, sent to configurable IP:port.
/// Also logs all events to a local CSV file.
/// 
/// SETUP: Add to any GameObject. The ExperimentController and VASScale
/// call SendEvent() automatically if this component exists.
/// </summary>
public class EventMarkerSender : MonoBehaviour
{
    [Header("Network")]
    [Tooltip("Target IP for event markers (localhost for same machine)")]
    [SerializeField] private string targetIP = "127.0.0.1";
    
    [Tooltip("Target UDP port")]
    [SerializeField] private int targetPort = 9000;
    
    [Tooltip("Enable UDP sending")]
    [SerializeField] private bool enableUDP = true;

    [Header("BrainVision Recorder (TCP)")]
    [Tooltip("Enable TCP markers to BrainVision Recorder")]
    [SerializeField] private bool enableBrainVision = false;
    
    [Tooltip("BrainVision Recorder IP (same machine = 127.0.0.1)")]
    [SerializeField] private string bvRecorderIP = "127.0.0.1";
    
    [Tooltip("BrainVision Recorder Remote Control port (default: 6700)")]
    [SerializeField] private int bvRecorderPort = 6700;
    
    [Header("Local Logging")]
    [SerializeField] private bool logToFile = true;
    [SerializeField] private string logFileName = "event_markers.csv";
    
    [Header("Gradient Events")]
    [Tooltip("Send event when gradient exceeds this threshold")]
    [SerializeField] private float climbThreshold = 3f;
    
    [Tooltip("Minimum gradient change to trigger GRADIENT_CHANGE event")]
    [SerializeField] private float gradientChangeThreshold = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool logToConsole = true;
    
    private UdpClient udpClient;
    private IPEndPoint endPoint;
    private TcpClient bvTcpClient;
    private NetworkStream bvStream;
    private string logFilePath;
    private float lastGradient = 0f;
    private bool isClimbing = false;
    private BikeController bikeController;
    private int eventCounter = 0;
    
    // Singleton for easy access
    private static EventMarkerSender _instance;
    public static EventMarkerSender Instance => _instance;
    
    [Serializable]
    public class EventMarker
    {
        public int id;
        public string type;
        public float timestamp;
        public float unity_time;
        public float distance;
        public float gradient;
        public string data;
    }

    private void Awake()
    {
        _instance = this;
    }
    
    private void Start()
    {
        bikeController = FindObjectOfType<BikeController>();
        
        // Setup UDP
        if (enableUDP)
        {
            try
            {
                udpClient = new UdpClient();
                endPoint = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
                Debug.Log($"[EventMarker] UDP sender ready → {targetIP}:{targetPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventMarker] UDP setup failed: {e.Message}");
                enableUDP = false;
            }
        }

        // Setup TCP for BrainVision Recorder
        if (enableBrainVision)
        {
            try
            {
                bvTcpClient = new TcpClient();
                bvTcpClient.Connect(bvRecorderIP, bvRecorderPort);
                bvStream = bvTcpClient.GetStream();
                Debug.Log($"[EventMarker] BrainVision Recorder connected → {bvRecorderIP}:{bvRecorderPort}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EventMarker] BrainVision Recorder connection failed: {e.Message}");
                Debug.LogWarning("[EventMarker] Make sure Remote Control is enabled in BrainVision Recorder (Configuration → Preferences → Remote Control)");
                enableBrainVision = false;
            }
        }
        
        // Setup log file
        if (logToFile)
        {
            logFilePath = System.IO.Path.Combine(ExperimentDataPath.GetPath(), logFileName);
            try
            {
                using (var w = new System.IO.StreamWriter(logFilePath, false))
                {
                    w.WriteLine("event_id,type,unity_time,system_timestamp,distance_m,gradient_pct,data");
                }
                Debug.Log($"[EventMarker] Log file: {logFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventMarker] Log file error: {e.Message}");
            }
        }
        
        // Send session start
        SendEvent("SESSION_START", $"unity_version={Application.unityVersion}");
    }
    
    private void Update()
    {
        if (bikeController == null) return;
        
        float gradient = bikeController.GetGradient();
        
        // Detect climb start/end
        if (!isClimbing && gradient >= climbThreshold)
        {
            isClimbing = true;
            SendEvent("CLIMB_START", $"gradient={gradient:F1}");
        }
        else if (isClimbing && gradient < climbThreshold - 1f) // hysteresis
        {
            isClimbing = false;
            SendEvent("CLIMB_END", $"gradient={gradient:F1}");
        }
        
        // Detect significant gradient changes
        if (Mathf.Abs(gradient - lastGradient) >= gradientChangeThreshold)
        {
            SendEvent("GRADIENT_CHANGE", $"from={lastGradient:F1},to={gradient:F1}");
            lastGradient = gradient;
        }
    }
    
    /// <summary>
    /// Send an event marker to all outputs (UDP + file + console).
    /// Call this from ExperimentController, VASScale, or any other script.
    /// </summary>
    public void SendEvent(string eventType, string data = "")
    {
        eventCounter++;
        
        float unityTime = Time.time;
        float distance = bikeController != null ? bikeController.GetDistance() : 0f;
        float gradient = bikeController != null ? bikeController.GetGradient() : 0f;
        string systemTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        // Build JSON message for UDP
        string json = $"{{\"id\":{eventCounter},\"type\":\"{eventType}\",\"unity_time\":{unityTime:F3}," +
                      $"\"timestamp\":\"{systemTimestamp}\",\"distance\":{distance:F1}," +
                      $"\"gradient\":{gradient:F1},\"data\":\"{data}\"}}";
        
        // Send UDP
        if (enableUDP && udpClient != null)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                udpClient.Send(bytes, bytes.Length, endPoint);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EventMarker] UDP send failed: {e.Message}");
            }
        }
        
        // Send TCP to BrainVision Recorder
        if (enableBrainVision && bvStream != null)
        {
            try
            {
                // BrainVision Recorder expects: "MK<number>\n" for numeric markers
                // or annotation format for text markers
                // Use event counter as numeric marker + annotation with details
                string bvMarker = $"MK{eventCounter}\n";
                byte[] bvBytes = Encoding.ASCII.GetBytes(bvMarker);
                bvStream.Write(bvBytes, 0, bvBytes.Length);
                bvStream.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EventMarker] BrainVision send failed: {e.Message}");
                enableBrainVision = false; // Disable on failure
            }
        }
        
        // Log to file
        if (logToFile && logFilePath != null)
        {
            try
            {
                using (var w = new System.IO.StreamWriter(logFilePath, true))
                {
                    w.WriteLine($"{eventCounter},{eventType},{unityTime:F3},{systemTimestamp},{distance:F1},{gradient:F1},{data}");
                }
            }
            catch { }
        }
        
        // Console
        if (logToConsole)
        {
            Debug.Log($"[EVENT] #{eventCounter} {eventType} t={unityTime:F2} d={distance:F0}m g={gradient:F1}% {data}");
        }
    }
    
    private void OnDestroy()
    {
        SendEvent("SESSION_END", $"total_events={eventCounter}");
        
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
    }
}
