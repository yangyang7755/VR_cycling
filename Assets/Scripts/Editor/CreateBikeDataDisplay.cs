using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Editor script to quickly create a BikeDataDisplay UI
/// </summary>
public class CreateBikeDataDisplay : EditorWindow
{
    [MenuItem("Tools/Create Bike Data Display UI")]
    public static void CreateBikeDataDisplayUI()
    {
        // Check if one already exists
        BikeDataDisplay existing = FindObjectOfType<BikeDataDisplay>();
        if (existing != null)
        {
            if (EditorUtility.DisplayDialog("BikeDataDisplay Exists", 
                "A BikeDataDisplay already exists in the scene. Do you want to select it?", 
                "Select", "Cancel"))
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
            }
            return;
        }

        // Find or create Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Debug.Log("✓ Created Canvas");
        }

        // Create main panel
        GameObject panelObj = new GameObject("BikeDataPanel");
        panelObj.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0, 0);
        panelRect.pivot = new Vector2(0, 0);
        panelRect.anchoredPosition = new Vector2(20, 20);
        panelRect.sizeDelta = new Vector2(300, 200);

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f); // Semi-transparent black background

        // Add BikeDataDisplay component
        BikeDataDisplay dataDisplay = panelObj.AddComponent<BikeDataDisplay>();

        // Create Distance display
        CreateDataRow(panelObj, "Distance", 0, dataDisplay, "distance");
        
        // Create Power display
        CreateDataRow(panelObj, "Power", 1, dataDisplay, "power");
        
        // Create Gradient display
        CreateDataRow(panelObj, "Gradient", 2, dataDisplay, "gradient");

        // Find BikeController and connect
        BikeController bikeController = FindObjectOfType<BikeController>();
        if (bikeController != null)
        {
            SerializedObject so = new SerializedObject(dataDisplay);
            so.FindProperty("bikeController").objectReferenceValue = bikeController;
            so.ApplyModifiedProperties();
            Debug.Log("✓ Connected to BikeController");
        }

        // Select the new panel
        Selection.activeGameObject = panelObj;
        EditorGUIUtility.PingObject(panelObj);

        Debug.Log("✓ BikeDataDisplay UI created!");
    }

    private static void CreateDataRow(GameObject parent, string label, int rowIndex, BikeDataDisplay dataDisplay, string dataType)
    {
        float rowHeight = 50f;
        float yPos = 150f - (rowIndex * rowHeight);

        // Create label
        GameObject labelObj = new GameObject($"{label}Label");
        labelObj.transform.SetParent(parent.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0, 0);
        labelRect.pivot = new Vector2(0, 0);
        labelRect.anchoredPosition = new Vector2(10, yPos);
        labelRect.sizeDelta = new Vector2(80, 30);

        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = label + ":";
        labelText.fontSize = 14;
        labelText.color = Color.white;

        // Create value text
        GameObject valueObj = new GameObject($"{label}Value");
        valueObj.transform.SetParent(parent.transform, false);
        RectTransform valueRect = valueObj.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0, 0);
        valueRect.anchorMax = new Vector2(0, 0);
        valueRect.pivot = new Vector2(0, 0);
        valueRect.anchoredPosition = new Vector2(100, yPos);
        valueRect.sizeDelta = new Vector2(100, 30);

        TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
        valueText.text = "0";
        valueText.fontSize = 14;
        valueText.color = Color.yellow;
        valueText.alignment = TextAlignmentOptions.Left;

        // Create progress bar
        GameObject sliderObj = new GameObject($"{label}ProgressBar");
        sliderObj.transform.SetParent(parent.transform, false);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0, 0);
        sliderRect.anchorMax = new Vector2(0, 0);
        sliderRect.pivot = new Vector2(0, 0);
        sliderRect.anchoredPosition = new Vector2(10, yPos - 20);
        sliderRect.sizeDelta = new Vector2(280, 20);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 0;

        // Create background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f);
        slider.targetGraphic = bgImage;

        // Create fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(sliderObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.sizeDelta = Vector2.zero;
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = GetColorForDataType(dataType);
        slider.fillRect = fillRect;

        // Connect to BikeDataDisplay
        SerializedObject so = new SerializedObject(dataDisplay);
        if (dataType == "distance")
        {
            so.FindProperty("distanceText").objectReferenceValue = valueText;
            so.FindProperty("distanceProgressBar").objectReferenceValue = slider;
        }
        else if (dataType == "power")
        {
            so.FindProperty("powerText").objectReferenceValue = valueText;
            so.FindProperty("powerProgressBar").objectReferenceValue = slider;
        }
        else if (dataType == "gradient")
        {
            so.FindProperty("gradientText").objectReferenceValue = valueText;
            so.FindProperty("gradientProgressBar").objectReferenceValue = slider;
        }
        so.ApplyModifiedProperties();
    }

    private static Color GetColorForDataType(string dataType)
    {
        switch (dataType)
        {
            case "distance": return Color.green;
            case "power": return Color.red;
            case "gradient": return Color.blue;
            default: return Color.white;
        }
    }
}
