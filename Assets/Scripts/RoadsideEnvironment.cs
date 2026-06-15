using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns roadside objects (poles, posts, simple trees) along the bike's path.
/// These vertical objects provide strong visual cues for gradient perception —
/// when going uphill, the poles tilt away from you; downhill, they tilt toward you.
/// This is the same visual trick Zwift uses to make gradients feel real.
/// 
/// SETUP: Add to any GameObject. Auto-finds BikeController and Terrain.
/// </summary>
public class RoadsideEnvironment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private Terrain terrain;
    
    [Header("Roadside Posts")]
    [Tooltip("Spawn distance posts along the road")]
    [SerializeField] private bool spawnPosts = true;
    
    [Tooltip("Spacing between posts (meters)")]
    [SerializeField] private float postSpacing = 25f;
    
    [Tooltip("Distance from road center to posts")]
    [SerializeField] private float postOffset = 7f;
    
    [Tooltip("Road width (for barrier placement)")]
    [SerializeField] private float roadWidth = 12f;
    
    [Tooltip("Post height (meters)")]
    [SerializeField] private float postHeight = 1.5f;
    
    [Tooltip("Post color")]
    [SerializeField] private Color postColor = new Color(0.9f, 0.9f, 0.9f);
    
    [Header("Simple Trees")]
    [Tooltip("Spawn simple trees along the road")]
    [SerializeField] private bool spawnTrees = true;
    
    [Tooltip("Tree prefabs to use (if empty, uses procedural trees)")]
    [SerializeField] private GameObject[] treePrefabs;
    
    [Tooltip("Spacing between trees (meters)")]
    [SerializeField] private float treeSpacing = 25f;
    
    [Tooltip("Distance from road center to trees")]
    [SerializeField] private float treeOffset = 16f;
    
    [Tooltip("Random offset variation")]
    [SerializeField] private float treeRandomOffset = 8f;
    
    [Tooltip("Tree height range")]
    [SerializeField] private Vector2 treeHeightRange = new Vector2(4f, 10f);
    
    [Header("Background Forest")]
    [Tooltip("Spawn a second row of larger trees further back")]
    [SerializeField] private bool spawnBackgroundForest = true;
    
    [Tooltip("Spacing between background trees")]
    [SerializeField] private float forestSpacing = 30f;
    
    [Tooltip("Distance from road to background forest")]
    [SerializeField] private float forestOffset = 45f;
    
    [Tooltip("Background tree height range")]
    [SerializeField] private Vector2 forestHeightRange = new Vector2(10f, 22f);
    
    [Header("Grass/Bushes")]
    [Tooltip("Spawn grass bushes along the road")]
    [SerializeField] private bool spawnGrass = true;
    
    [Tooltip("Spacing between grass clumps")]
    [SerializeField] private float grassSpacing = 5f;
    
    [Tooltip("Distance from road to grass")]
    [SerializeField] private float grassOffset = 6f;
    
    [Header("Curve Barriers")]
    [Tooltip("Place barriers on sharp curves")]
    [SerializeField] private bool spawnBarriers = true;
    
    [Header("Distance Markers")]
    [Tooltip("Show distance markers every N meters")]
    [SerializeField] private bool showDistanceMarkers = true;
    [SerializeField] private float markerInterval = 100f;
    
    [Header("Generation")]
    [Tooltip("How far ahead to generate objects")]
    [SerializeField] private float generateAhead = 300f;
    
    [Tooltip("How far behind to keep objects")]
    [SerializeField] private float keepBehind = 50f;
    
    [Tooltip("Update interval (seconds)")]
    [SerializeField] private float updateInterval = 1f;
    
    private float lastUpdateTime;
    private Dictionary<int, GameObject> activeObjects = new Dictionary<int, GameObject>();
    private Transform objectParent;
    private Material postMat;
    private Material treeTrunkMat;
    private Material treeLeafMat;
    private Material grassMat;
    private Material barrierMat;
    private SplinePath cachedSpline;

    private void Start()
    {
        if (bikeController == null) bikeController = FindObjectOfType<BikeController>();
        if (terrain == null) terrain = FindObjectOfType<Terrain>();
        
        objectParent = new GameObject("SideAssets").transform;
        objectParent.SetParent(transform);
        
        postMat = CreateMat(postColor);
        treeTrunkMat = CreateMat(new Color(0.4f, 0.25f, 0.1f));
        treeLeafMat = CreateMat(new Color(0.2f, 0.55f, 0.15f));
        grassMat = CreateMat(new Color(0.25f, 0.5f, 0.12f));
        barrierMat = CreateMat(new Color(0.9f, 0.9f, 0.9f));
        
        UpdateObjects();
    }

    /// <summary>
    /// Clear all spawned roadside objects. Call this when a new route is generated
    /// so stale objects from the previous trial are removed before the bike resets.
    /// </summary>
    public void ClearAll()
    {
        foreach (var kvp in activeObjects)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        activeObjects.Clear();
        cachedSpline = null;
    }
    
    private void Update()
    {
        if (Time.time - lastUpdateTime > updateInterval)
        {
            UpdateObjects();
            lastUpdateTime = Time.time;
        }
    }
    
    private void UpdateObjects()
    {
        if (bikeController == null || terrain == null) return;

        float bikeDistance = bikeController.GetDistance();

        // Cache spline reference (refreshed on ClearAll)
        if (cachedSpline == null)
        {
            CurvedRouteGenerator routeGen = FindObjectOfType<CurvedRouteGenerator>();
            if (routeGen != null) cachedSpline = routeGen.GetSplinePath();
            if (cachedSpline == null) cachedSpline = FindObjectOfType<SplinePath>();
        }
        SplinePath spline = cachedSpline;
        bool useSpline = spline != null && spline.GetTotalLength() > 0f;

        Vector3 bikePos = bikeController.transform.position;
        Vector3 forward = bikeController.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.right;
        forward.Normalize();

        float startDist = bikeDistance - keepBehind;
        float endDist = Mathf.Min(bikeDistance + generateAhead, 
            useSpline ? spline.GetTotalLength() : bikeDistance + generateAhead);

        // Remove objects that are too far behind
        List<int> toRemove = new List<int>();
        foreach (var kvp in activeObjects)
        {
            if (kvp.Key < Mathf.FloorToInt(startDist))
            {
                if (kvp.Value != null) Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (int key in toRemove) activeObjects.Remove(key);

        // Spawn posts along the road
        if (spawnPosts)
        {
            for (float d = Mathf.Ceil(Mathf.Max(0, startDist) / postSpacing) * postSpacing; d < endDist; d += postSpacing)
            {
                int key = Mathf.RoundToInt(d * 10f);
                if (activeObjects.ContainsKey(key)) continue;

                Vector3 worldPos;
                Vector3 right;
                if (useSpline)
                {
                    SplinePath.SplineSample s = spline.SampleAtDistance(d);
                    worldPos = s.position;
                    right = s.right;
                }
                else
                {
                    float distFromBike = d - bikeDistance;
                    worldPos = bikePos + forward * distFromBike;
                    right = Vector3.Cross(Vector3.up, forward).normalized;
                }

                SpawnPost(worldPos - right * postOffset, key);
                SpawnPost(worldPos + right * postOffset, key + 1);
            }
        }

        // Spawn trees (close row)
        if (spawnTrees)
        {
            for (float d = Mathf.Ceil(Mathf.Max(0, startDist) / treeSpacing) * treeSpacing; d < endDist; d += treeSpacing)
            {
                int key = Mathf.RoundToInt(d * 10f) + 100000;
                if (activeObjects.ContainsKey(key)) continue;

                Vector3 worldPos;
                Vector3 right;
                if (useSpline)
                {
                    SplinePath.SplineSample s = spline.SampleAtDistance(d);
                    worldPos = s.position;
                    right = s.right;
                }
                else
                {
                    float distFromBike = d - bikeDistance;
                    worldPos = bikePos + forward * distFromBike;
                    right = Vector3.Cross(Vector3.up, forward).normalized;
                }

                float seed = d * 0.1f;
                float randOffset = (Mathf.PerlinNoise(seed, 0f) - 0.5f) * treeRandomOffset * 2f;
                float side = Mathf.PerlinNoise(seed, 10f) > 0.5f ? 1f : -1f;
                float lateralDist = treeOffset + randOffset;

                Vector3 treePos = worldPos + right * side * lateralDist;
                
                // Skip if this position lands on or too close to the road (tight curves)
                if (useSpline && IsPositionOnRoad(treePos, spline, 2f))
                    continue;
                
                float height = Mathf.Lerp(treeHeightRange.x, treeHeightRange.y, Mathf.PerlinNoise(seed, 20f));
                SpawnTree(treePos, height, key);
                
                // Also spawn on the other side sometimes
                if (Mathf.PerlinNoise(seed, 60f) > 0.4f)
                {
                    Vector3 otherPos = worldPos - right * side * (treeOffset + (Mathf.PerlinNoise(seed + 1f, 0f) - 0.5f) * treeRandomOffset * 2f);
                    
                    if (!useSpline || !IsPositionOnRoad(otherPos, spline, 2f))
                    {
                        float otherHeight = Mathf.Lerp(treeHeightRange.x, treeHeightRange.y, Mathf.PerlinNoise(seed + 1f, 20f));
                        SpawnTree(otherPos, otherHeight, key + 2);
                    }
                }
            }
        }
        
        // Spawn background forest (second row, larger trees further back)
        if (spawnBackgroundForest)
        {
            for (float d = Mathf.Ceil(Mathf.Max(0, startDist) / forestSpacing) * forestSpacing; d < endDist; d += forestSpacing)
            {
                int key = Mathf.RoundToInt(d * 10f) + 500000;
                if (activeObjects.ContainsKey(key)) continue;

                Vector3 worldPos;
                Vector3 right;
                if (useSpline)
                {
                    SplinePath.SplineSample s = spline.SampleAtDistance(d);
                    worldPos = s.position;
                    right = s.right;
                }
                else
                {
                    float distFromBike = d - bikeDistance;
                    worldPos = bikePos + forward * distFromBike;
                    right = Vector3.Cross(Vector3.up, forward).normalized;
                }

                float seed = d * 0.07f + 999f;
                // Spawn on both sides for forest feel
                for (int side = -1; side <= 1; side += 2)
                {
                    float lateralDist = forestOffset + (Mathf.PerlinNoise(seed + side, 0f) - 0.5f) * 15f;
                    Vector3 treePos = worldPos + right * side * lateralDist;
                    
                    // Skip if lands on road (unlikely at forest distance but safety check)
                    if (useSpline && IsPositionOnRoad(treePos, spline, 2f))
                        continue;
                    
                    float height = Mathf.Lerp(forestHeightRange.x, forestHeightRange.y, Mathf.PerlinNoise(seed + side, 30f));
                    SpawnTree(treePos, height, key + (side == -1 ? 0 : 1));
                }
            }
        }

        // Distance markers
        if (showDistanceMarkers)
        {
            for (float d = Mathf.Ceil(Mathf.Max(0, startDist) / markerInterval) * markerInterval; d < endDist; d += markerInterval)
            {
                int key = Mathf.RoundToInt(d * 10f) + 200000;
                if (activeObjects.ContainsKey(key)) continue;

                Vector3 worldPos;
                Vector3 right;
                if (useSpline)
                {
                    SplinePath.SplineSample s = spline.SampleAtDistance(d);
                    worldPos = s.position;
                    right = s.right;
                }
                else
                {
                    float distFromBike = d - bikeDistance;
                    worldPos = bikePos + forward * distFromBike;
                    right = Vector3.Cross(Vector3.up, forward).normalized;
                }

                SpawnDistanceMarker(worldPos + right * (postOffset + 1f), d, key);
            }
        }
        
        // Spawn grass bushes
        if (spawnGrass)
        {
            for (float d = Mathf.Ceil(Mathf.Max(0, startDist) / grassSpacing) * grassSpacing; d < endDist; d += grassSpacing)
            {
                int key = Mathf.RoundToInt(d * 10f) + 300000;
                if (activeObjects.ContainsKey(key)) continue;
                
                Vector3 worldPos;
                Vector3 right;
                if (useSpline)
                {
                    SplinePath.SplineSample s = spline.SampleAtDistance(d);
                    worldPos = s.position;
                    right = s.right;
                }
                else
                {
                    float distFromBike = d - bikeDistance;
                    worldPos = bikePos + forward * distFromBike;
                    right = Vector3.Cross(Vector3.up, forward).normalized;
                }
                
                float seed = d * 0.13f;
                float side = Mathf.PerlinNoise(seed, 30f) > 0.5f ? 1f : -1f;
                float offset = grassOffset + (Mathf.PerlinNoise(seed, 40f) - 0.5f) * 6f;
                
                Vector3 grassPos = worldPos + right * side * offset;
                if (!useSpline || !IsPositionOnRoad(grassPos, spline, 1f))
                    SpawnGrassBush(grassPos, key);
                
                // Sometimes spawn on both sides
                if (Mathf.PerlinNoise(seed, 50f) > 0.6f)
                {
                    Vector3 otherGrassPos = worldPos - right * side * offset;
                    if (!useSpline || !IsPositionOnRoad(otherGrassPos, spline, 1f))
                        SpawnGrassBush(otherGrassPos, key + 1);
                }
            }
        }
        
        // Spawn curve barriers
        if (spawnBarriers && useSpline)
        {
            for (float d = Mathf.Ceil(Mathf.Max(0, startDist) / 5f) * 5f; d < endDist; d += 5f)
            {
                // Check if this is a curve by comparing directions
                float lookAhead = 20f;
                if (d + lookAhead > spline.GetTotalLength()) continue;
                
                SplinePath.SplineSample s1 = spline.SampleAtDistance(d);
                SplinePath.SplineSample s2 = spline.SampleAtDistance(d + lookAhead);
                float turnAngle = Vector3.Angle(s1.forward, s2.forward);
                
                if (turnAngle > 8f) // significant curve
                {
                    int key = Mathf.RoundToInt(d * 10f) + 400000;
                    if (activeObjects.ContainsKey(key)) continue;
                    
                    // Place barrier on outside of curve
                    Vector3 turnDir = Vector3.Cross(s1.forward, s2.forward);
                    float side = turnDir.y > 0 ? 1f : -1f;
                    Vector3 barrierPos = s1.position + s1.right * side * (roadWidth * 0.5f + 1f);
                    
                    SpawnBarrier(barrierPos, s1.forward, key);
                }
            }
        }
    }
    
    /// <summary>
    /// Check whether a world position is safely away from the road surface.
    /// On tight curves, a perpendicular offset from one spline point may still
    /// overlap with the road at a nearby section. This does a coarse distance check.
    /// </summary>
    private bool IsPositionOnRoad(Vector3 worldPos, SplinePath spline, float safetyMargin)
    {
        if (spline == null) return false;
        
        float routeLength = spline.GetTotalLength();
        float halfRoad = (roadWidth * 0.5f) + safetyMargin;
        
        // Coarse scan: check every 15m
        float bestSqDist = float.MaxValue;
        float coarseStep = 15f;
        
        for (float d = 0f; d <= routeLength; d += coarseStep)
        {
            SplinePath.SplineSample s = spline.SampleAtDistance(d);
            float sq = (new Vector3(worldPos.x, 0f, worldPos.z) - new Vector3(s.position.x, 0f, s.position.z)).sqrMagnitude;
            if (sq < bestSqDist) bestSqDist = sq;
            
            // Early out: if already clearly far from road, no need to keep checking
            if (bestSqDist > halfRoad * halfRoad * 4f) continue;
        }
        
        return Mathf.Sqrt(bestSqDist) < halfRoad;
    }

    private void SpawnPost(Vector3 worldPos, int key)
    {
        float terrainY = terrain.SampleHeight(worldPos);
        worldPos.y = terrainY;
        
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "RoadPost";
        post.transform.SetParent(objectParent);
        post.transform.position = worldPos + Vector3.up * (postHeight * 0.5f);
        post.transform.localScale = new Vector3(0.08f, postHeight * 0.5f, 0.08f);
        post.GetComponent<Renderer>().material = postMat;
        post.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        
        Collider col = post.GetComponent<Collider>();
        if (col != null) Destroy(col);
        
        activeObjects[key] = post;
    }
    
    private void SpawnTree(Vector3 worldPos, float height, int key)
    {
        // Ground to sculpted terrain height at this XZ position.
        // The terrain has been sculpted to follow the road elevation and blend
        // outward, so terrain.SampleHeight gives the correct ground level for
        // roadside objects regardless of their distance from the road.
        float terrainY = terrain != null ? terrain.SampleHeight(worldPos) : 0f;
        worldPos.y = terrainY;

        GameObject tree;

        // Use prefabs if available, otherwise fall back to procedural
        if (treePrefabs != null && treePrefabs.Length > 0)
        {
            // Pick a random prefab based on position (deterministic)
            int prefabIndex = Mathf.Abs((int)(worldPos.x * 7f + worldPos.z * 13f)) % treePrefabs.Length;
            GameObject prefab = treePrefabs[prefabIndex];
            
            if (prefab != null)
            {
                tree = Instantiate(prefab, worldPos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                tree.transform.SetParent(objectParent);
                
                // Scale based on height parameter
                float scale = height / 6f; // Normalize around 6m as "standard" tree height
                scale = Mathf.Clamp(scale, 0.5f, 2.5f);
                tree.transform.localScale = Vector3.one * scale;
                
                activeObjects[key] = tree;
                return;
            }
        }

        // Fallback: procedural tree (sphere + cylinder)
        tree = new GameObject("Tree");
        tree.transform.SetParent(objectParent);
        tree.transform.position = worldPos;

        float styleSeed = worldPos.x * 0.1f + worldPos.z * 0.07f;
        int style = Mathf.FloorToInt(Mathf.PerlinNoise(styleSeed, 0f) * 3f);

        float trunkHeight = height * UnityEngine.Random.Range(0.25f, 0.4f);
        float trunkWidth = UnityEngine.Random.Range(0.15f, 0.35f);

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(tree.transform);
        trunk.transform.localPosition = Vector3.up * trunkHeight;
        trunk.transform.localScale = new Vector3(trunkWidth, trunkHeight, trunkWidth);
        trunk.GetComponent<Renderer>().material = treeTrunkMat;
        trunk.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Collider tc = trunk.GetComponent<Collider>();
        if (tc != null) Destroy(tc);

        if (style == 0)
        {
            GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.transform.SetParent(tree.transform);
            float canopySize = height * UnityEngine.Random.Range(0.4f, 0.6f);
            canopy.transform.localPosition = Vector3.up * (trunkHeight * 2f + canopySize * 0.3f);
            canopy.transform.localScale = new Vector3(canopySize, canopySize * 0.75f, canopySize);
            canopy.GetComponent<Renderer>().material = treeLeafMat;
            canopy.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Collider cc = canopy.GetComponent<Collider>();
            if (cc != null) Destroy(cc);
        }
        else if (style == 1)
        {
            int layers = UnityEngine.Random.Range(2, 4);
            for (int i = 0; i < layers; i++)
            {
                GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cone.transform.SetParent(tree.transform);
                float layerY = trunkHeight * 2f + i * height * 0.2f;
                float layerSize = height * (0.45f - i * 0.1f);
                cone.transform.localPosition = Vector3.up * layerY;
                cone.transform.localScale = new Vector3(layerSize, layerSize * 0.5f, layerSize);
                Material coniferMat = CreateMat(new Color(0.1f, 0.35f, 0.1f));
                cone.GetComponent<Renderer>().material = coniferMat;
                cone.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Collider lc = cone.GetComponent<Collider>();
                if (lc != null) Destroy(lc);
            }
        }
        else
        {
            GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.transform.SetParent(tree.transform);
            float canopyW = height * UnityEngine.Random.Range(0.5f, 0.8f);
            float canopyH = height * UnityEngine.Random.Range(0.3f, 0.5f);
            canopy.transform.localPosition = Vector3.up * (trunkHeight * 2f + canopyH * 0.2f);
            canopy.transform.localScale = new Vector3(canopyW, canopyH, canopyW);
            canopy.GetComponent<Renderer>().material = treeLeafMat;
            canopy.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Collider cc = canopy.GetComponent<Collider>();
            if (cc != null) Destroy(cc);
        }

        activeObjects[key] = tree;
    }
    
    private void SpawnDistanceMarker(Vector3 worldPos, float distance, int key)
    {
        float terrainY = terrain.SampleHeight(worldPos);
        worldPos.y = terrainY + 2f;
        
        GameObject marker = new GameObject($"Marker_{distance:F0}m");
        marker.transform.SetParent(objectParent);
        marker.transform.position = worldPos;
        
        // Create a small sign
        GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.transform.SetParent(marker.transform);
        sign.transform.localPosition = Vector3.zero;
        sign.transform.localScale = new Vector3(1.5f, 0.8f, 0.05f);
        
        Material signMat = CreateMat(new Color(0.1f, 0.4f, 0.1f));
        sign.GetComponent<Renderer>().material = signMat;
        sign.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Collider sc = sign.GetComponent<Collider>();
        if (sc != null) Destroy(sc);
        
        // Sign pole
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.transform.SetParent(marker.transform);
        pole.transform.localPosition = new Vector3(0f, -1f, 0f);
        pole.transform.localScale = new Vector3(0.05f, 1f, 0.05f);
        pole.GetComponent<Renderer>().material = postMat;
        pole.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Collider pc = pole.GetComponent<Collider>();
        if (pc != null) Destroy(pc);
        
        // Make sign face the road
        marker.transform.LookAt(new Vector3(bikeController.transform.position.x, worldPos.y, bikeController.transform.position.z));
        
        activeObjects[key] = marker;
    }
    
    private void SpawnGrassBush(Vector3 worldPos, int key)
    {
        float terrainY = terrain.SampleHeight(worldPos);
        worldPos.y = terrainY;
        
        GameObject bush = new GameObject("GrassBush");
        bush.transform.SetParent(objectParent);
        bush.transform.position = worldPos;
        
        // Create 2-3 overlapping flattened spheres for a bush look
        int clumps = UnityEngine.Random.Range(2, 4);
        for (int i = 0; i < clumps; i++)
        {
            GameObject clump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            clump.transform.SetParent(bush.transform);
            
            float size = UnityEngine.Random.Range(0.4f, 1.0f);
            float offsetX = UnityEngine.Random.Range(-0.3f, 0.3f);
            float offsetZ = UnityEngine.Random.Range(-0.3f, 0.3f);
            
            clump.transform.localPosition = new Vector3(offsetX, size * 0.3f, offsetZ);
            clump.transform.localScale = new Vector3(size, size * 0.5f, size);
            
            // Vary green shade
            Color bushColor = new Color(
                0.2f + UnityEngine.Random.Range(-0.05f, 0.05f),
                0.45f + UnityEngine.Random.Range(-0.1f, 0.1f),
                0.1f + UnityEngine.Random.Range(-0.03f, 0.03f)
            );
            clump.GetComponent<Renderer>().material = CreateMat(bushColor);
            clump.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Collider c = clump.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
        
        activeObjects[key] = bush;
    }
    
    private void SpawnBarrier(Vector3 worldPos, Vector3 roadForward, int key)
    {
        float terrainY = terrain.SampleHeight(worldPos);
        worldPos.y = terrainY;
        
        GameObject barrier = new GameObject("CurveBarrier");
        barrier.transform.SetParent(objectParent);
        barrier.transform.position = worldPos;
        
        // Chevron post: vertical post with angled stripe
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.transform.SetParent(barrier.transform);
        post.transform.localPosition = Vector3.up * 0.6f;
        post.transform.localScale = new Vector3(0.8f, 1.2f, 0.08f);
        post.GetComponent<Renderer>().material = barrierMat;
        post.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Collider pc = post.GetComponent<Collider>();
        if (pc != null) Destroy(pc);
        
        // Red stripe
        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.transform.SetParent(barrier.transform);
        stripe.transform.localPosition = new Vector3(0f, 0.7f, -0.05f);
        stripe.transform.localScale = new Vector3(0.7f, 0.3f, 0.05f);
        stripe.GetComponent<Renderer>().material = CreateMat(new Color(0.9f, 0.15f, 0.1f));
        stripe.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Collider sc = stripe.GetComponent<Collider>();
        if (sc != null) Destroy(sc);
        
        // Face the road
        if (roadForward.sqrMagnitude > 0.01f)
        {
            barrier.transform.rotation = Quaternion.LookRotation(roadForward, Vector3.up);
        }
        
        activeObjects[key] = barrier;
    }
    
    private Material CreateMat(Color color)
    {
        return RuntimeMaterialHelper.CreateMaterial(color);
    }
}
