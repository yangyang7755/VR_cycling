using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Always-visible horizontal Pain VAS (1-10) in the top-right corner.
/// Controlled by Left/Right arrow keys. Value displayed as a number.
/// Logged continuously with telemetry data.
/// </summary>
public class PersistentPainVAS : MonoBehaviour
{
    [Header("Scale")]
    [SerializeField] private int minValue = 1;
    [SerializeField] private int maxValue = 10;
    
    [Header("Appearance")]
    [SerializeField] private float barWidth = 250f;
    [SerializeField] private float barHeight = 25f;
    [SerializeField] private Color lowColor = new Color(0.3f, 0.8f, 0.3f);
    [SerializeField] private Color highColor = new Color(1f, 0.2f, 0.2f);
    
    [Header("Input")]
    [SerializeField] private KeyCode increaseKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode decreaseKey = KeyCode.LeftArrow;
    [SerializeField] private float keyRepeatDelay = 0.2f;
    
    private Canvas canvas;
    private GameObject vasPanel;
    private Image fillBar;
    private TextMeshProUGUI valueText;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI[] numberLabels;
    private Image markerImage;
    
    private int currentValue = 1;
    private bool isVisible = false;
    private float lastKeyTime = 0f;

    private void Start()
    {
        CreateUI();
        Show();
    }
    
    private void CreateUI()
    {
        GameObject canvasObj = new GameObject("PersistentPainVAS_Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Panel (top-right)
        vasPanel = new GameObject("PainPanel");
        vasPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = vasPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-15f, -15f);
        panelRect.sizeDelta = new Vector2(barWidth + 30f, 85f);
        Image panelBg = vasPanel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.05f, 0.1f, 0.75f);
        
        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(vasPanel.transform, false);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "PAIN  ← →";
        titleText.fontSize = 13;
        titleText.color = new Color(1f, 0.4f, 0.4f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -4f);
        titleRect.sizeDelta = new Vector2(0f, 18f);
        
        // Bar background
        GameObject barBg = new GameObject("BarBg");
        barBg.transform.SetParent(vasPanel.transform, false);
        RectTransform barBgRect = barBg.AddComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        barBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        barBgRect.pivot = new Vector2(0.5f, 0.5f);
        barBgRect.anchoredPosition = new Vector2(0f, 2f);
        barBgRect.sizeDelta = new Vector2(barWidth, barHeight);
        Image barBgImg = barBg.AddComponent<Image>();
        barBgImg.color = new Color(0.15f, 0.15f, 0.2f);
        
        // Fill bar
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barBg.transform, false);
        fillBar = fillObj.AddComponent<Image>();
        fillBar.color = lowColor;
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(0f, 0f);
        
        // Marker
        GameObject markerObj = new GameObject("Marker");
        markerObj.transform.SetParent(barBg.transform, false);
        markerImage = markerObj.AddComponent<Image>();
        markerImage.color = Color.white;
        RectTransform markerRect = markerObj.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0f, 0f);
        markerRect.anchorMax = new Vector2(0f, 1f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(4f, barHeight + 8f);
        
        // Number labels (1-10)
        numberLabels = new TextMeshProUGUI[maxValue - minValue + 1];
        for (int i = minValue; i <= maxValue; i++)
        {
            GameObject numObj = new GameObject($"Num_{i}");
            numObj.transform.SetParent(vasPanel.transform, false);
            TextMeshProUGUI numText = numObj.AddComponent<TextMeshProUGUI>();
            numText.text = i.ToString();
            numText.fontSize = 11;
            numText.color = new Color(0.6f, 0.6f, 0.7f);
            numText.alignment = TextAlignmentOptions.Center;
            RectTransform numRect = numObj.GetComponent<RectTransform>();
            numRect.anchorMin = new Vector2(0.5f, 0f);
            numRect.anchorMax = new Vector2(0.5f, 0f);
            numRect.pivot = new Vector2(0.5f, 0f);
            float xPos = -barWidth * 0.5f + ((float)(i - minValue) / (maxValue - minValue)) * barWidth;
            numRect.anchoredPosition = new Vector2(xPos, 5f);
            numRect.sizeDelta = new Vector2(25f, 15f);
            numberLabels[i - minValue] = numText;
        }
        
        // Current value display (large number)
        GameObject valObj = new GameObject("ValueDisplay");
        valObj.transform.SetParent(vasPanel.transform, false);
        valueText = valObj.AddComponent<TextMeshProUGUI>();
        valueText.text = "1";
        valueText.fontSize = 22;
        valueText.color = Color.white;
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.fontStyle = FontStyles.Bold;
        RectTransform valRect = valObj.GetComponent<RectTransform>();
        valRect.anchorMin = new Vector2(1f, 0.5f);
        valRect.anchorMax = new Vector2(1f, 0.5f);
        valRect.pivot = new Vector2(1f, 0.5f);
        valRect.anchoredPosition = new Vector2(-2f, 2f);
        valRect.sizeDelta = new Vector2(30f, 30f);
    }
    
    private void Update()
    {
        if (!isVisible) return;
        
        // Arrow key input with repeat delay
        if (Time.time - lastKeyTime > keyRepeatDelay)
        {
            if (Input.GetKey(increaseKey))
            {
                currentValue = Mathf.Min(maxValue, currentValue + 1);
                lastKeyTime = Time.time;
            }
            else if (Input.GetKey(decreaseKey))
            {
                currentValue = Mathf.Max(minValue, currentValue - 1);
                lastKeyTime = Time.time;
            }
        }
        
        UpdateVisuals();
    }
    
    private void UpdateVisuals()
    {
        float normalized = (float)(currentValue - minValue) / (maxValue - minValue);
        
        // Fill bar width
        if (fillBar != null)
        {
            RectTransform fillRect = fillBar.GetComponent<RectTransform>();
            fillRect.sizeDelta = new Vector2(barWidth * normalized, 0f);
            fillBar.color = Color.Lerp(lowColor, highColor, normalized);
        }
        
        // Marker position
        if (markerImage != null)
        {
            markerImage.rectTransform.anchoredPosition = new Vector2(barWidth * normalized, 0f);
        }
        
        // Value text
        if (valueText != null)
            valueText.text = currentValue.ToString();
        
        // Highlight current number label
        if (numberLabels != null)
        {
            for (int i = 0; i < numberLabels.Length; i++)
            {
                if (numberLabels[i] != null)
                {
                    bool isCurrent = (i + minValue) == currentValue;
                    numberLabels[i].color = isCurrent ? Color.white : new Color(0.5f, 0.5f, 0.6f);
                    numberLabels[i].fontStyle = isCurrent ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }
    }
    
    public void Show()
    {
        isVisible = true;
        if (vasPanel != null) vasPanel.SetActive(true);
        UpdateVisuals();
    }
    
    public void Hide()
    {
        isVisible = false;
        if (vasPanel != null) vasPanel.SetActive(false);
    }
    
    public float GetCurrentValue() => currentValue;
    public bool IsVisible() => isVisible;
    
    public void ResetValue()
    {
        currentValue = minValue;
        UpdateVisuals();
    }
    
    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
