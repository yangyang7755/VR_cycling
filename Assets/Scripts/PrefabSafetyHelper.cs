using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper class to safely check if a GameObject is a prefab asset before modifying transforms
/// </summary>
public static class PrefabSafetyHelper
{
    /// <summary>
    /// Check if a GameObject is safe to modify (not a prefab asset)
    /// </summary>
    public static bool CanModifyTransform(GameObject obj)
    {
        if (obj == null) return false;
        
        // In play mode, always allow modification (prefabs are instantiated)
        if (Application.isPlaying) return true;
        
        #if UNITY_EDITOR
        // In edit mode, check if it's a prefab asset
        if (PrefabUtility.IsPartOfPrefabAsset(obj))
        {
            return false;
        }
        
        // Also check if it's a prefab instance that's not connected
        if (PrefabUtility.IsPartOfPrefabInstance(obj))
        {
            // Prefab instances can be modified, but we should check if it's connected
            PrefabInstanceStatus status = PrefabUtility.GetPrefabInstanceStatus(obj);
            if (status == PrefabInstanceStatus.Connected)
            {
                // Connected prefab instances can be modified, but changes will be lost
                // Return true but log a warning
                Debug.LogWarning($"[PrefabSafetyHelper] {obj.name} is a connected prefab instance. Modifications may be lost.");
                return true;
            }
        }
        #endif
        
        return true;
    }
    
    /// <summary>
    /// Safely set position, only if GameObject is not a prefab asset
    /// </summary>
    public static bool SafeSetPosition(GameObject obj, Vector3 position)
    {
        if (!CanModifyTransform(obj))
        {
            Debug.LogError($"[PrefabSafetyHelper] Cannot modify {obj.name} - it is a prefab asset! Use a scene instance instead.");
            return false;
        }
        
        obj.transform.position = position;
        return true;
    }
    
    /// <summary>
    /// Safely set rotation, only if GameObject is not a prefab asset
    /// </summary>
    public static bool SafeSetRotation(GameObject obj, Quaternion rotation)
    {
        if (!CanModifyTransform(obj))
        {
            Debug.LogError($"[PrefabSafetyHelper] Cannot modify {obj.name} - it is a prefab asset! Use a scene instance instead.");
            return false;
        }
        
        obj.transform.rotation = rotation;
        return true;
    }
    
    /// <summary>
    /// Safely set local scale, only if GameObject is not a prefab asset
    /// </summary>
    public static bool SafeSetLocalScale(GameObject obj, Vector3 scale)
    {
        if (!CanModifyTransform(obj))
        {
            Debug.LogError($"[PrefabSafetyHelper] Cannot modify {obj.name} - it is a prefab asset! Use a scene instance instead.");
            return false;
        }
        
        obj.transform.localScale = scale;
        return true;
    }
}
