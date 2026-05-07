using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3D mini-map showing route overview with current position
/// Shows elevation profile and upcoming terrain
/// </summary>
public class MiniMapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Terrain terrain;
    [SerializeField] private Camera miniMapCamera;
    
    [Header("UI Elements")]
    [SerializeField] private RawImage miniMapDisplay;
    [SerializeField] private RectTransform positionIndicator;
    
    [Header("Camera Settings")]
    [SerializeField] private float cameraHeight = 100f;
    [SerializeField] private float cameraDistance = 50f;
    [SerializeField] private float cameraAngle = 45f;
    [SerializeField] private bool followBike = true;
    [SerializeField] private float followSmoothness = 5f;
    
    [Header("Display Settings")]
    [SerializeField] private int renderTextureWidth = 512;
    [SerializeField] private int renderTextureHeight = 256;
    [SerializeField] private float viewDistance = 200f;
    
    private RenderTexture miniMapRenderTexture;
    
    private void Start()
    {
        if (bikeController == null)
            bikeController = FindObjectOfType<BikeController>();
        
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
        
        SetupMiniMapCamera();
        SetupRenderTexture();
    }
    
    private void SetupMiniMapCamera()
    {
        if (miniMapCamera == null)
        {
            GameObject camObj = new GameObject("MiniMapCamera");
            miniMapCamera = camObj.AddComponent<Camera>();
            miniMapCamera.transform.SetParent(transform);
        }
        
        miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
        miniMapCamera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        miniMapCamera.cullingMask = LayerMask.GetMask("Default", "Terrain");
        miniMapCamera.orthographic = false;
        miniMapCamera.fieldOfView = 60f;
        miniMapCamera.nearClipPlane = 1f;
        miniMapCamera.farClipPlane = viewDistance * 2f;
    }
    
    private void SetupRenderTexture()
    {
        miniMapRenderTexture = new RenderTexture(renderTextureWidth, renderTextureHeight, 16);
        miniMapRenderTexture.antiAliasing = 2;
        
        if (miniMapCamera != null)
        {
            miniMapCamera.targetTexture = miniMapRenderTexture;
        }
        
        if (miniMapDisplay != null)
        {
            miniMapDisplay.texture = miniMapRenderTexture;
        }
    }
    
    private void Update()
    {
        if (followBike && bikeController != null && miniMapCamera != null)
        {
            UpdateCameraPosition();
        }
    }
    
    private void UpdateCameraPosition()
    {
        Vector3 bikePos = bikeController.transform.position;
        Vector3 bikeForward = bikeController.transform.forward;
        
        Vector3 targetPos = bikePos - bikeForward * cameraDistance + Vector3.up * cameraHeight;
        
        miniMapCamera.transform.position = Vector3.Lerp(
            miniMapCamera.transform.position,
            targetPos,
            Time.deltaTime * followSmoothness
        );
        
        miniMapCamera.transform.LookAt(bikePos + bikeForward * 20f);
    }
    
    private void OnDestroy()
    {
        if (miniMapRenderTexture != null)
        {
            miniMapRenderTexture.Release();
        }
    }
}
