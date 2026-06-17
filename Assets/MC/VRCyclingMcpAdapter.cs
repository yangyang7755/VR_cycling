using UnityEngine;

namespace Hapticare.MCP
{
    public class VRCyclingMcpAdapter : MonoBehaviour
    {
        [Header("Bridge")]
        public HapticareMcpUdpBridge bridge;

        [Header("Outgoing Game State")]
        public string gameId = "vr_cycling";
        public float sendHz = 10f;
        public float slopeGradePct;
        public float cadenceRpm;
        public float speedKmh;
        public float powerWatts;
        public int difficultyLevel = 1;
        public string terrain = "flat";

        [Header("Incoming MCP Adaptation")]
        public int lastDifficultyDelta;
        public float lastSlopeBiasPct;
        public float lastFatigueScore;
        public string lastMcpMessage = "";

        float nextSendTime;

        void Awake()
        {
            if (!bridge) bridge = HapticareMcpUdpBridge.Instance;
        }

        void OnEnable()
        {
            if (!bridge) bridge = HapticareMcpUdpBridge.Instance;
            if (bridge)
            {
                bridge.gameId = gameId;
                bridge.OnMessage += HandleMcpMessage;
            }
        }

        void OnDisable()
        {
            if (bridge) bridge.OnMessage -= HandleMcpMessage;
        }

        void Update()
        {
            if (!bridge) return;
            if (Time.time < nextSendTime) return;
            nextSendTime = Time.time + 1f / Mathf.Max(1f, sendHz);
            SendState();
        }

        public void SendState()
        {
            bridge.Send("vr.cycling.state",
                "\"slope_grade_pct\":" + HapticareMcpUdpBridge.F(slopeGradePct)
                + ",\"cadence_rpm\":" + HapticareMcpUdpBridge.F(cadenceRpm)
                + ",\"speed_kmh\":" + HapticareMcpUdpBridge.F(speedKmh)
                + ",\"power_watts\":" + HapticareMcpUdpBridge.F(powerWatts)
                + ",\"difficulty_level\":" + difficultyLevel
                + ",\"terrain\":\"" + HapticareMcpUdpBridge.Escape(terrain) + "\""
            );
        }

        void HandleMcpMessage(string type, string json)
        {
            lastMcpMessage = json;
            if (type == "unity.adaptation")
            {
                ApplyUnityAdaptation(json);
            }
            else if (type == "hapticare.feedback" && json.Contains("\"unity_adaptation\""))
            {
                ApplyUnityAdaptation(json);
            }
        }

        void ApplyUnityAdaptation(string json)
        {
            lastDifficultyDelta = ExtractInt(json, "difficulty_delta", 0);
            lastSlopeBiasPct = ExtractFloat(json, "slope_bias_pct", 0f);
            lastFatigueScore = ExtractFloat(json, "fatigue_score", 0f);

            difficultyLevel = Mathf.Clamp(difficultyLevel + lastDifficultyDelta, 1, 10);

            // Hook point: apply lastSlopeBiasPct to terrain/slope generator here.
            // Keep this as a small adapter boundary; do not put clinical policy in Unity.
        }

        static int ExtractInt(string json, string key, int fallback)
        {
            return Mathf.RoundToInt(ExtractFloat(json, key, fallback));
        }

        static float ExtractFloat(string json, string key, float fallback)
        {
            string marker = "\"" + key + "\"";
            int i = json.IndexOf(marker, System.StringComparison.Ordinal);
            if (i < 0) return fallback;
            int colon = json.IndexOf(':', i);
            if (colon < 0) return fallback;
            int end = colon + 1;
            while (end < json.Length && " \t\r\n".IndexOf(json[end]) >= 0) end++;
            int start = end;
            while (end < json.Length && "-+.0123456789".IndexOf(json[end]) >= 0) end++;
            if (start == end) return fallback;
            if (float.TryParse(json.Substring(start, end - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
            return fallback;
        }
    }
}
