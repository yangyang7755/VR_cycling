using UnityEngine;

/// <summary>
/// Debug helper to check bike placement and visibility
/// </summary>
public class BikePlacementDebugger : MonoBehaviour
{
    [ContextMenu("Debug Bike Position")]
    public void DebugBikePosition()
    {
        GameObject bike = GameObject.Find("Bike");
        if (bike == null)
        {
            Debug.LogError("Bike GameObject not found!");
            return;
        }

        Debug.Log("=== BIKE DEBUG INFO ===");
        Debug.Log($"Bike position: {bike.transform.position}");
        Debug.Log($"Bike rotation: {bike.transform.rotation.eulerAngles}");
        Debug.Log($"Bike active: {bike.activeSelf}");
        Debug.Log($"Bike children count: {bike.transform.childCount}");

        // Check for models
        bool foundBiker = false;
        bool foundWheels = false;

        foreach (Transform child in bike.transform)
        {
            Debug.Log($"  Child: {child.name}, Active: {child.gameObject.activeSelf}, Position: {child.localPosition}");
            if (child.name.ToLower().Contains("biker"))
            {
                foundBiker = true;
            }
            if (child.name.ToLower().Contains("wheel"))
            {
                foundWheels = true;
            }
        }

        Debug.Log($"Biker model found: {foundBiker}");
        Debug.Log($"Wheels model found: {foundWheels}");

        // Check camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Debug.Log($"Main Camera position: {mainCam.transform.position}");
            Debug.Log($"Distance from bike: {Vector3.Distance(mainCam.transform.position, bike.transform.position)}");
        }
        else
        {
            Debug.LogWarning("No main camera found!");
        }

        // Check terrain
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain != null)
        {
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            Debug.Log($"Terrain position: {terrainPos}, Size: {terrainSize}");
            Debug.Log($"Bike is within terrain: {IsWithinTerrain(bike.transform.position, terrainPos, terrainSize)}");
        }
    }

    [ContextMenu("Focus Camera on Bike")]
    public void FocusCameraOnBike()
    {
        GameObject bike = GameObject.Find("Bike");
        Camera mainCam = Camera.main;

        if (bike == null)
        {
            Debug.LogError("Bike not found!");
            return;
        }

        if (mainCam == null)
        {
            Debug.LogError("Main camera not found!");
            return;
        }

        // Position camera to look at bike
        Vector3 bikePos = bike.transform.position;
        mainCam.transform.position = bikePos + new Vector3(-5, 3, 0);
        mainCam.transform.LookAt(bikePos);

        Debug.Log($"Camera focused on bike at {bikePos}");
    }

    private bool IsWithinTerrain(Vector3 position, Vector3 terrainPos, Vector3 terrainSize)
    {
        float minX = terrainPos.x - terrainSize.x / 2f;
        float maxX = terrainPos.x + terrainSize.x / 2f;
        float minZ = terrainPos.z - terrainSize.z / 2f;
        float maxZ = terrainPos.z + terrainSize.z / 2f;

        return position.x >= minX && position.x <= maxX && position.z >= minZ && position.z <= maxZ;
    }
}
