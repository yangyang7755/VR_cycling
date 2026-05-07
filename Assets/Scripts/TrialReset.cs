using UnityEngine;

/// <summary>
/// Resets the scene to the correct starting state for each trial
/// Call this at the start of each trial
/// </summary>
public class TrialReset : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikePlacement bikePlacement;
    [SerializeField] private GameObject bikeObject;
    [SerializeField] private GameObject bikerModel;

    [Header("Reset Settings")]
    [Tooltip("Reset bike position on start")]
    [SerializeField] private bool resetOnStart = false;

    private void Start()
    {
        if (resetOnStart)
        {
            ResetToStartPosition();
        }
    }

    [ContextMenu("Reset to Start Position")]
    public void ResetToStartPosition()
    {
        Debug.Log("Resetting scene to start position...");

        // Find bike placement component
        if (bikePlacement == null)
        {
            bikePlacement = FindObjectOfType<BikePlacement>();
        }

        if (bikePlacement != null)
        {
            // Use BikePlacement to set up bike
            bikePlacement.SetupBike();
            Debug.Log("✓ Bike reset using BikePlacement");
        }
        else
        {
            // Manual reset if BikePlacement not found
            if (bikeObject == null)
            {
                bikeObject = GameObject.Find("Bike");
            }

            if (bikeObject != null)
            {
                // Safely set exact position
                if (PrefabSafetyHelper.SafeSetPosition(bikeObject, new Vector3(247.8f, 4.9f, 0f)))
                {
                    PrefabSafetyHelper.SafeSetRotation(bikeObject, Quaternion.Euler(0, 0, 0));
                    bikeObject.SetActive(true);
                    Debug.Log($"✓ Bike reset to: {bikeObject.transform.position}");
                }
            }

            // Reset biker model
            if (bikerModel == null && bikeObject != null)
            {
                foreach (Transform child in bikeObject.transform)
                {
                    if (child.name.ToLower().Contains("biker"))
                    {
                        bikerModel = child.gameObject;
                        break;
                    }
                }
            }

            if (bikerModel != null)
            {
                if (PrefabSafetyHelper.CanModifyTransform(bikerModel))
                {
                    bikerModel.transform.localPosition = Vector3.zero;
                    bikerModel.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    PrefabSafetyHelper.SafeSetLocalScale(bikerModel, new Vector3(3f, 3f, 3f));
                    bikerModel.SetActive(true);
                    Debug.Log($"✓ Biker reset: Position={bikerModel.transform.localPosition}, Scale={bikerModel.transform.localScale}");
                }
            }
        }

        Debug.Log("✓ Scene reset complete!");
    }
}
