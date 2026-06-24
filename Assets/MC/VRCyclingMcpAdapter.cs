using UnityEngine;

namespace Hapticare.MCP
{
    public class VRCyclingMcpAdapter : MonoBehaviour
    {
        [Header("Bridge")]
        public HapticareMcpUdpBridge bridge;

        [Header("MCP Opt-In")]
        [Tooltip("Master switch. When off, this adapter will not publish Unity state or apply MCP messages.")]
        public bool enableMcpBridge = false;

        [Tooltip("When enabled with the master switch, Unity publishes slope/game state to Python MCP.")]
        public bool publishUnityState = true;

        [Tooltip("When enabled with the master switch, Unity applies MCP difficulty/adaptation feedback.")]
        public bool receiveMcpAdaptation = true;

        [Header("Outgoing Game State")]
        public string gameId = "vr_cycling";
        public float sendHz = 10f;
        public float slopeGradePct;
        public float cadenceRpm;
        public float speedKmh;
        public float powerWatts;
        public int difficultyLevel = 1;
        public string terrain = "flat";

        [Header("Experiment Events")]
        [Tooltip("Current trial phase. Examples: baseline, hill, post_block_questionnaire, end.")]
        public string trialPhase = "baseline";

        [Tooltip("Condition label for GameData, for example coins, no_coins, hill_01.")]
        public string conditionId = "";

        [Tooltip("Distance from trial start in meters. Bind this from BikeController/route manager if available.")]
        public float distanceM;

        [Tooltip("Distance from trial start as 0-100 percent. Bind this from progress UI/route manager if available.")]
        public float distancePct;

        [Tooltip("Current cumulative coin/reward count, if the condition uses coins.")]
        public int coins;

        [Tooltip("When enabled, emits a prompt-due marker every effortPromptIntervalS. Actual answers must call SendEffortRating.")]
        public bool publishEffortPromptEvents = true;

        [Tooltip("Seconds between effort/pain questionnaire prompt markers.")]
        public float effortPromptIntervalS = 30f;

        [Header("Incoming MCP Adaptation")]
        public int lastDifficultyDelta;
        public float lastSlopeBiasPct;
        public float lastFatigueScore;
        public string lastMcpMessage = "";

        [Header("Incoming MCP Bike Telemetry")]
        public BikeController bikeController;

        [Tooltip("Opt-in live control. When enabled with the master switch, fused Tacx/Garmin telemetry from Python drives BikeController movement.")]
        public bool applyLiveTelemetryToBikeController = false;
        public float lastBikeCadenceRpm;
        public float lastBikeSpeedKmh;
        public float lastBikePowerWatts;
        public float lastBikeResistanceLevel;
        public float lastBikeTelemetryUnityTime;

        float nextSendTime;
        float trialStartRealtime;
        float nextEffortPromptTime;
        int effortPromptIndex;

        void Awake()
        {
            if (!bridge) bridge = HapticareMcpUdpBridge.Instance;
            if (!bikeController) bikeController = FindObjectOfType<BikeController>();
        }

        void OnEnable()
        {
            if (!bridge) bridge = HapticareMcpUdpBridge.Instance;
            if (!bikeController) bikeController = FindObjectOfType<BikeController>();
            if (bridge)
            {
                bridge.gameId = gameId;
                bridge.OnMessage += HandleMcpMessage;
            }
            trialStartRealtime = Time.realtimeSinceStartup;
            nextEffortPromptTime = Time.realtimeSinceStartup + Mathf.Max(5f, effortPromptIntervalS);
        }

        void OnDisable()
        {
            if (bridge) bridge.OnMessage -= HandleMcpMessage;
        }

        void Update()
        {
            if (!enableMcpBridge || !bridge) return;
            if (publishUnityState && Time.time >= nextSendTime)
            {
                nextSendTime = Time.time + 1f / Mathf.Max(1f, sendHz);
                SendState();
            }
            if (publishEffortPromptEvents && Time.realtimeSinceStartup >= nextEffortPromptTime)
            {
                SendEffortPromptDue("Pain/effort rating due");
                nextEffortPromptTime = Time.realtimeSinceStartup + Mathf.Max(5f, effortPromptIntervalS);
            }
        }

        public void SendState()
        {
            if (!enableMcpBridge || !publishUnityState || !bridge) return;

            bridge.Send("vr.cycling.state",
                "\"slope_grade_pct\":" + HapticareMcpUdpBridge.F(slopeGradePct)
                + ",\"cadence_rpm\":" + HapticareMcpUdpBridge.F(cadenceRpm)
                + ",\"speed_kmh\":" + HapticareMcpUdpBridge.F(speedKmh)
                + ",\"power_watts\":" + HapticareMcpUdpBridge.F(powerWatts)
                + ",\"difficulty_level\":" + difficultyLevel
                + ",\"terrain\":\"" + HapticareMcpUdpBridge.Escape(terrain) + "\""
                + ",\"phase\":\"" + HapticareMcpUdpBridge.Escape(trialPhase) + "\""
                + ",\"condition\":\"" + HapticareMcpUdpBridge.Escape(conditionId) + "\""
                + ",\"elapsed_s\":" + HapticareMcpUdpBridge.F(ElapsedS())
                + ",\"distance_m\":" + HapticareMcpUdpBridge.F(distanceM)
                + ",\"distance_pct\":" + HapticareMcpUdpBridge.F(distancePct)
                + ",\"coins\":" + coins
            );
        }

        public void SendGameStart(string phaseName = "baseline")
        {
            trialPhase = phaseName;
            trialStartRealtime = Time.realtimeSinceStartup;
            effortPromptIndex = 0;
            nextEffortPromptTime = Time.realtimeSinceStartup + Mathf.Max(5f, effortPromptIntervalS);
            SendVRGameEvent("trial_start", phaseName, distanceM, distancePct);
        }

        public void SendGameEnd(string phaseName = "end")
        {
            trialPhase = phaseName;
            SendVRGameEvent("trial_end", phaseName, distanceM, distancePct);
        }

        public void SendAbandoned(string phaseName = "abandoned")
        {
            trialPhase = phaseName;
            SendVRGameEvent("trial_abandoned", phaseName, distanceM, distancePct);
        }

        public void SendHillStart(string phaseName = "hill")
        {
            trialPhase = phaseName;
            SendVRGameEvent("hill_start", phaseName, distanceM, distancePct);
        }

        public void SendHillEnd(string phaseName = "post_block_questionnaire")
        {
            trialPhase = phaseName;
            SendVRGameEvent("hill_end", phaseName, distanceM, distancePct);
        }

        public void SendEffortPromptDue(string question = "Pain/effort rating due")
        {
            effortPromptIndex += 1;
            SendVRGameEvent(
                "effort_prompt_due",
                trialPhase,
                distanceM,
                distancePct,
                "\"prompt_id\":\"effort_" + effortPromptIndex.ToString("000") + "\""
                + ",\"question\":\"" + HapticareMcpUdpBridge.Escape(question) + "\""
            );
        }

        public void SendEffortRating(
            string question,
            string ratingName,
            float ratingValue,
            float scaleMin = 0f,
            float scaleMax = 10f,
            float responseTimeMs = -1f
        )
        {
            SendVRGameEvent(
                "effort_rating_answered",
                trialPhase,
                distanceM,
                distancePct,
                "\"prompt_id\":\"effort_" + Mathf.Max(1, effortPromptIndex).ToString("000") + "\""
                + ",\"question\":\"" + HapticareMcpUdpBridge.Escape(question) + "\""
                + ",\"rating_name\":\"" + HapticareMcpUdpBridge.Escape(ratingName) + "\""
                + ",\"rating_value\":" + HapticareMcpUdpBridge.F(ratingValue)
                + ",\"rating_scale_min\":" + HapticareMcpUdpBridge.F(scaleMin)
                + ",\"rating_scale_max\":" + HapticareMcpUdpBridge.F(scaleMax)
                + ",\"response_time_ms\":" + HapticareMcpUdpBridge.F(responseTimeMs)
            );
        }

        public void SendVRGameEvent(string eventName, string phaseName = "", float distanceMeters = -1f, float distancePercent = -1f, string extraJsonFields = "")
        {
            if (!enableMcpBridge || !bridge) return;
            string phase = string.IsNullOrEmpty(phaseName) ? trialPhase : phaseName;
            float dM = distanceMeters >= 0f ? distanceMeters : distanceM;
            float dPct = distancePercent >= 0f ? distancePercent : distancePct;
            bridge.Send("vr.cycling.event",
                "\"event\":\"" + HapticareMcpUdpBridge.Escape(eventName) + "\""
                + ",\"phase\":\"" + HapticareMcpUdpBridge.Escape(phase) + "\""
                + ",\"condition\":\"" + HapticareMcpUdpBridge.Escape(conditionId) + "\""
                + ",\"elapsed_s\":" + HapticareMcpUdpBridge.F(ElapsedS())
                + ",\"distance_m\":" + HapticareMcpUdpBridge.F(dM)
                + ",\"distance_pct\":" + HapticareMcpUdpBridge.F(dPct)
                + ",\"gradient_pct\":" + HapticareMcpUdpBridge.F(slopeGradePct)
                + ",\"cadence_rpm\":" + HapticareMcpUdpBridge.F(cadenceRpm)
                + ",\"speed_kmh\":" + HapticareMcpUdpBridge.F(speedKmh)
                + ",\"power_watts\":" + HapticareMcpUdpBridge.F(powerWatts)
                + ",\"coins\":" + coins
                + (string.IsNullOrEmpty(extraJsonFields) ? "" : "," + extraJsonFields)
            );
        }

        float ElapsedS()
        {
            return Mathf.Max(0f, Time.realtimeSinceStartup - trialStartRealtime);
        }

        void HandleMcpMessage(string type, string json)
        {
            if (!enableMcpBridge) return;

            lastMcpMessage = json;
            if (receiveMcpAdaptation && type == "unity.adaptation")
            {
                ApplyUnityAdaptation(json);
            }
            else if (receiveMcpAdaptation && type == "hapticare.feedback" && json.Contains("\"unity_adaptation\""))
            {
                ApplyUnityAdaptation(json);
            }
            else if (applyLiveTelemetryToBikeController && type == "unity.bike_telemetry")
            {
                ApplyMcpBikeTelemetry(json);
            }
        }

        void ApplyUnityAdaptation(string json)
        {
            lastDifficultyDelta = ExtractInt(json, "difficulty_delta", 0);
            lastSlopeBiasPct = ExtractFloat(json, "slope_bias_pct", 0f);
            lastFatigueScore = ExtractFloat(json, "fatigue_score", 0f);

            float cadence = ExtractFloat(json, "bike_cadence_rpm", float.NaN);
            float speed = ExtractFloat(json, "bike_speed_kmh", float.NaN);
            float power = ExtractFloat(json, "bike_power_watts", float.NaN);
            float resistance = ExtractFloat(json, "bike_resistance_level", float.NaN);
            if (applyLiveTelemetryToBikeController && (!float.IsNaN(cadence) || !float.IsNaN(speed) || !float.IsNaN(power)))
            {
                ApplyMcpBikeTelemetryValues(cadence, speed, power, resistance);
            }

            difficultyLevel = Mathf.Clamp(difficultyLevel + lastDifficultyDelta, 1, 10);

            // Hook point: apply lastSlopeBiasPct to terrain/slope generator here.
            // Keep this as a small adapter boundary; do not put clinical policy in Unity.
        }

        void ApplyMcpBikeTelemetry(string json)
        {
            float cadence = ExtractFloat(json, "cadence_rpm", ExtractFloat(json, "bike_cadence_rpm", float.NaN));
            float speed = ExtractFloat(json, "speed_kmh", ExtractFloat(json, "bike_speed_kmh", float.NaN));
            float power = ExtractFloat(json, "power_watts", ExtractFloat(json, "bike_power_watts", float.NaN));
            float resistance = ExtractFloat(json, "resistance_level", ExtractFloat(json, "bike_resistance_level", float.NaN));
            ApplyMcpBikeTelemetryValues(cadence, speed, power, resistance);
        }

        void ApplyMcpBikeTelemetryValues(float cadenceRpm, float speedKmh, float powerWatts, float resistanceLevel)
        {
            if (!float.IsNaN(cadenceRpm)) lastBikeCadenceRpm = Mathf.Max(0f, cadenceRpm);
            if (!float.IsNaN(speedKmh)) lastBikeSpeedKmh = Mathf.Max(0f, speedKmh);
            if (!float.IsNaN(powerWatts)) lastBikePowerWatts = Mathf.Max(0f, powerWatts);
            if (!float.IsNaN(resistanceLevel)) lastBikeResistanceLevel = Mathf.Max(0f, resistanceLevel);
            lastBikeTelemetryUnityTime = Time.time;

            cadenceRpm = float.IsNaN(cadenceRpm) ? lastBikeCadenceRpm : cadenceRpm;
            speedKmh = float.IsNaN(speedKmh) ? lastBikeSpeedKmh : speedKmh;
            powerWatts = float.IsNaN(powerWatts) ? lastBikePowerWatts : powerWatts;

            cadenceRpm = Mathf.Max(0f, cadenceRpm);
            speedKmh = Mathf.Max(0f, speedKmh);
            powerWatts = Mathf.Max(0f, powerWatts);

            if (applyLiveTelemetryToBikeController)
            {
                if (!bikeController) bikeController = FindObjectOfType<BikeController>();
                if (bikeController) bikeController.ApplyLiveMcpTelemetry(cadenceRpm, speedKmh, powerWatts);
            }
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
