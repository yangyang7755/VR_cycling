using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Hapticare.MCP
{
    public class HapticareMcpUdpBridge : MonoBehaviour
    {
        public static HapticareMcpUdpBridge Instance { get; private set; }

        [Header("MCP UDP")]
        public string mcpHost = "127.0.0.1";
        public int mcpReceivePort = 8876;  // Unity -> MCP
        public int unityListenPort = 8877; // MCP -> Unity

        [Header("Session")]
        public string gameId = "vr_cycling";
        public string sessionId = "unity_session";
        public string trialId = "";
        public bool sendReadyOnStart = true;

        public event Action<string, string> OnMessage;

        UdpClient sender;
        UdpClient listener;
        IPEndPoint mcpEndpoint;
        Thread listenerThread;
        volatile bool running;
        readonly ConcurrentQueue<string> inbound = new ConcurrentQueue<string>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            StartBridge();
            if (sendReadyOnStart)
            {
                Send("unity.ready", "\"scene\":\"" + Escape(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) + "\"");
            }
        }

        void OnDestroy()
        {
            StopBridge();
        }

        void Update()
        {
            while (inbound.TryDequeue(out var text))
            {
                OnMessage?.Invoke(ExtractType(text), text);
            }
        }

        public void StartBridge()
        {
            if (running) return;
            running = true;
            sender = new UdpClient();
            mcpEndpoint = new IPEndPoint(IPAddress.Parse(mcpHost), mcpReceivePort);
            listener = new UdpClient(unityListenPort);
            listener.Client.ReceiveTimeout = 250;
            listenerThread = new Thread(ListenLoop) { IsBackground = true, Name = "hapticare-mcp-udp-listener" };
            listenerThread.Start();
        }

        public void StopBridge()
        {
            running = false;
            try { listener?.Close(); } catch { }
            try { sender?.Close(); } catch { }
            if (listenerThread != null && listenerThread.IsAlive) listenerThread.Join(500);
        }

        public void Send(string type, string jsonFields)
        {
            if (sender == null) return;
            string body = "{"
                + "\"type\":\"" + Escape(type) + "\""
                + ",\"game_id\":\"" + Escape(gameId) + "\""
                + ",\"session_id\":\"" + Escape(sessionId) + "\""
                + ",\"trial_id\":\"" + Escape(trialId) + "\""
                + ",\"unity_time\":" + F(Time.realtimeSinceStartup)
                + (string.IsNullOrEmpty(jsonFields) ? "" : "," + jsonFields)
                + "}";
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            sender.Send(bytes, bytes.Length, mcpEndpoint);
        }

        void ListenLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = listener.Receive(ref any);
                    inbound.Enqueue(Encoding.UTF8.GetString(data));
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
            }
        }

        public static string ExtractType(string json)
        {
            const string marker = "\"type\"";
            int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0) return "";
            int colon = json.IndexOf(':', markerIndex);
            int firstQuote = json.IndexOf('"', colon + 1);
            int secondQuote = json.IndexOf('"', firstQuote + 1);
            if (colon < 0 || firstQuote < 0 || secondQuote < 0) return "";
            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        public static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public static string F(float value)
        {
            return value.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
