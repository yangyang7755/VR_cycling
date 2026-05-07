using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Visual feedback system for cycling events
/// Shows notifications, achievements, and motivational messages
/// Provides visual cues for terrain changes and performance milestones
/// </summary>
public class VisualFeedbackSystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private Image notificationIcon;
    [SerializeField] private Animator notificationAnimator;
    
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    
    [Header("Notification Settings")]
    [SerializeField] private float notificationDuration = 3f;
    [SerializeField] private bool enableSoundEffects = true;
    
    [Header("Terrain Change Notifications")]
    [SerializeField] private bool notifyOnSteepClimb = true;
    [SerializeField] private float steepClimbThreshold = 8f;
    [SerializeField] private bool notifyOnDescent = true;
    [SerializeField] private float descentThreshold = -5f;
    
    [Header("Performance Notifications")]
    [SerializeField] private bool notifyOnDistanceMilestones = true;
    [SerializeField] private float[] distanceMilestones = { 100f, 250f, 500f, 1000f };
    [SerializeField] private bool notifyOnPowerZoneChange = true;
    
    [Header("Motivational Messages")]
    [SerializeField] private bool enableMotivationalMessages = true;
    [SerializeField] private float motivationalMessageInterval = 60f;
    
    [Header("Colors")]
    [SerializeField] private Color infoColor = new Color(0.3f, 0.7f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.7f, 0f);
    [SerializeField] private Color successColor = new Color(0.3f, 1f, 0.3f);
    [SerializeField] private Color challengeColor = new Color(1f, 0.3f, 0.3f);
    
    private float lastGradient = 0f;
    private int lastPowerZone = 0;
    private float lastMotivationalTime = 0f;
    private bool[] milestoneReached;
    private Coroutine currentNotification;
    
    private string[] motivationalMessages = {
        "Keep pushing!",
        "You've got this!",
        "Strong work!",
        "Feeling good!",
        "Nice pace!",
        "Stay focused!",
        "Almost there!",
        "Great effort!",
        "Smooth pedaling!",
        "Excellent form!"
    };
    
    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
        
        milestoneReached = new bool[distanceMilestones.Length];
        lastMotivationalTime = Time.time;
    }
    
    private void Update()
    {
        if (bikeController == null) return;
        
        CheckTerrainChanges();
        CheckDistanceMilestones();
        CheckPowerZoneChanges();
        CheckMotivationalMessages();
    }
    
    private void CheckTerrainChanges()
    {
        float currentGradient = bikeController.GetGradient();
        
        // Steep climb notification
        if (notifyOnSteepClimb && currentGradient >= steepClimbThreshold && lastGradient < steepClimbThreshold)
        {
            ShowNotification($"Steep Climb Ahead! {currentGradient:F1}%", challengeColor);
        }
        
        // Descent notification
        if (notifyOnDescent && currentGradient <= descentThreshold && lastGradient > descentThreshold)
        {
            ShowNotification($"Descent! {currentGradient:F1}%", infoColor);
        }
        
        lastGradient = currentGradient;
    }
    
    private void CheckDistanceMilestones()
    {
        if (!notifyOnDistanceMilestones) return;
        
        float distance = bikeController.GetDistance();
        
        for (int i = 0; i < distanceMilestones.Length; i++)
        {
            if (!milestoneReached[i] && distance >= distanceMilestones[i])
            {
                milestoneReached[i] = true;
                ShowNotification($"{distanceMilestones[i]:F0}m Complete!", successColor);
            }
        }
    }
    
    private void CheckPowerZoneChanges()
    {
        if (!notifyOnPowerZoneChange) return;
        
        float power = bikeController.GetPower();
        int currentZone = GetPowerZone(power);
        
        if (currentZone != lastPowerZone && currentZone >= 4)
        {
            string zoneName = GetPowerZoneName(currentZone);
            ShowNotification($"Power Zone: {zoneName}", warningColor);
        }
        
        lastPowerZone = currentZone;
    }
    
    private void CheckMotivationalMessages()
    {
        if (!enableMotivationalMessages) return;
        
        if (Time.time - lastMotivationalTime >= motivationalMessageInterval)
        {
            string message = motivationalMessages[Random.Range(0, motivationalMessages.Length)];
            ShowNotification(message, successColor);
            lastMotivationalTime = Time.time;
        }
    }
    
    public void ShowNotification(string message, Color color)
    {
        if (currentNotification != null)
        {
            StopCoroutine(currentNotification);
        }
        
        currentNotification = StartCoroutine(DisplayNotification(message, color));
    }
    
    private IEnumerator DisplayNotification(string message, Color color)
    {
        if (notificationPanel == null || notificationText == null)
            yield break;
        
        notificationText.text = message;
        notificationText.color = color;
        
        if (notificationIcon != null)
        {
            notificationIcon.color = color;
        }
        
        notificationPanel.SetActive(true);
        
        if (notificationAnimator != null)
        {
            notificationAnimator.SetTrigger("Show");
        }
        
        yield return new WaitForSeconds(notificationDuration);
        
        if (notificationAnimator != null)
        {
            notificationAnimator.SetTrigger("Hide");
            yield return new WaitForSeconds(0.5f);
        }
        
        notificationPanel.SetActive(false);
    }
    
    private int GetPowerZone(float power)
    {
        if (power <= 100f) return 1;
        if (power <= 150f) return 2;
        if (power <= 200f) return 3;
        if (power <= 250f) return 4;
        if (power <= 300f) return 5;
        return 6;
    }
    
    private string GetPowerZoneName(int zone)
    {
        switch (zone)
        {
            case 1: return "Recovery";
            case 2: return "Endurance";
            case 3: return "Tempo";
            case 4: return "Threshold";
            case 5: return "VO2 Max";
            case 6: return "Anaerobic";
            default: return "Unknown";
        }
    }
    
    public void ResetMilestones()
    {
        for (int i = 0; i < milestoneReached.Length; i++)
        {
            milestoneReached[i] = false;
        }
    }
}
