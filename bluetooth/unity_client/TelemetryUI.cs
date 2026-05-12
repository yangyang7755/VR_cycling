using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI overlay that shows live telemetry values.
/// 
/// Setup:
/// 1. Create a Canvas (UI → Canvas)
/// 2. Add a Text (UI → Text - TextMeshPro or Legacy Text) 
/// 3. Attach this script to the Canvas or any GameObject
/// 4. Drag the Text component into the "Display Text" field
/// 5. Drag the GameObject with BikeTelemetryClient into the "Telemetry" field
/// </summary>
public class TelemetryUI : MonoBehaviour
{
    public BikeTelemetryClient Telemetry;
    public Text DisplayText; // Use TMPro.TextMeshProUGUI if using TextMeshPro

    void Update()
    {
        if (Telemetry == null || DisplayText == null) return;

        DisplayText.text =
            $"Speed:      {Telemetry.SpeedKmh:F1} km/h\n" +
            $"Power:      {Telemetry.PowerWatts:F0} W\n" +
            $"Cadence:    {Telemetry.CadenceRpm:F0} RPM\n" +
            $"Heart Rate: {Telemetry.HeartRateBpm:F0} BPM\n" +
            $"Resistance: {Telemetry.ResistanceLevel}";
    }
}
