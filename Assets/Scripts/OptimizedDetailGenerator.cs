using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// GPU-instanced detail generator for maximum performance and immersion
/// Uses simple meshes with randomized scale/rotation for grass lumps, road decals, and bushes
/// Optimized for perceived speed and effort feedback
/// </summary>
public class OptimizedDetailGenerator : MonoBehaviour
{
    [Header("Terrain References")]
    [Tooltip("Terrain to place elements on")]
    [SerializeField] private Terrain terrain;
    
    [Tooltip("SimpleTerrainBuilder for road information")]
    [SerializeField] private SimpleTerrainBuilder terrainBuilder;

    [Header("Road Settings")]
    [Tooltip("Road center position (Z coordinate)")]
    [SerializeField] private float roadCenterZ = 0f;
    
    [Tooltip("Road width in meters")]
    [SerializeField] private float roadWidth = 15f;
    
    [Tooltip("Safe distance from road edge (meters)")]
    [SerializeField] private float safeDistanceFromRoad = 0.5f;

    [Header("Grass Lumps Settings")]
    [Tooltip("Enable grass lumps")]
    [SerializeField] private bool enableGrassLumps = true;
    
    [Tooltip("Spacing between grass lumps (meters) - every 1-2m as requested")]
    [SerializeField] private Vector2 grassSpacing = new Vector2(1f, 2f);
    
    [Tooltip("Distance from road edge to place grass (meters)")]
    [SerializeField] private float grassDistanceFromRoad = 1f;
    
    [Tooltip("Maximum distance from road for grass placement (meters)")]
    [SerializeField] private float grassMaxDistance = 10f;
    
    [Tooltip("Grass textures (for simple quads)")]
    [SerializeField] private Texture2D[] grassTextures;
    
    [Tooltip("Grass material (with GPU instancing enabled)")]
    [SerializeField] private Material grassMaterial;
    
    [Tooltip("Grass size range (meters)")]
    [SerializeField] private Vector2 grassSizeRange = new Vector2(0.3f, 0.6f);
    
    [Tooltip("Grass scale variation (percentage)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float grassScaleVariation = 0.2f;
    
    [Tooltip("Grass rotation variation (degrees)")]
    [Range(0f, 15f)]
    [SerializeField] private float grassRotationVariation = 10f;

    [Header("Road Edge Decals (Cracks and Dirt)")]
    [Tooltip("Enable road edge decals")]
    [SerializeField] private bool enableRoadDecals = true;
    
    [Tooltip("Spacing between decals along road edge (meters)")]
    [SerializeField] private Vector2 decalSpacing = new Vector2(2f, 5f);
    
    [Tooltip("Distance from road edge for decals (meters)")]
    [SerializeField] private float decalDistanceFromEdge = 0.1f;
    
    [Tooltip("Decal textures (cracks and dirt)")]
    [SerializeField] private Texture2D[] decalTextures;
    
    [Tooltip("Decal material (with GPU instancing enabled)")]
    [SerializeField] private Material decalMaterial;
    
    [Tooltip("Decal size range (meters)")]
    [SerializeField] private Vector2 decalSizeRange = new Vector2(0.2f, 0.5f);
    
    [Tooltip("Decal scale variation (percentage)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float decalScaleVariation = 0.3f;
    
    [Tooltip("Decal rotation variation (degrees)")]
    [Range(0f, 360f)]
    [SerializeField] private float decalRotationVariation = 45f;

    [Header("Roadside Bushes")]
    [Tooltip("Enable roadside bushes")]
    [SerializeField] private bool enableBushes = true;
    
    [Tooltip("Spacing between bushes (meters)")]
    [SerializeField] private Vector2 bushSpacing = new Vector2(3f, 8f);
    
    [Tooltip("Distance from road edge to place bushes (meters)")]
    [SerializeField] private float bushDistanceFromRoad = 2f;
    
    [Tooltip("Maximum distance from road for bush placement (meters)")]
    [SerializeField] private float bushMaxDistance = 8f;
    
    [Tooltip("Bush textures (for simple quads)")]
    [SerializeField] private Texture2D[] bushTextures;
    
    [Tooltip("Bush material (with GPU instancing enabled)")]
    [SerializeField] private Material bushMaterial;
    
    [Tooltip("Bush size range (meters)")]
    [SerializeField] private Vector2 bushSizeRange = new Vector2(0.5f, 1.2f);
    
    [Tooltip("Bush scale variation (percentage)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float bushScaleVariation = 0.25f;
    
    [Tooltip("Bush rotation variation (degrees)")]
    [Range(0f, 360f)]
    [SerializeField] private float bushRotationVariation = 360f;

    [Header("Course Settings")]
    [Tooltip("Total course length (meters)")]
    [SerializeField] private float courseLength = 500f;
    
    [Tooltip("Start offset from beginning (meters)")]
    [SerializeField] private float startOffset = 5f;
    
    [Tooltip("End offset from end (meters)")]
    [SerializeField] private float endOffset = 5f;

    [Header("GPU Instancing Settings")]
    [Tooltip("Maximum instances per batch (Unity limit: 1023)")]
    [Range(100, 1023)]
    [SerializeField] private int maxInstancesPerBatch = 500;
    
    [Tooltip("Use simple quad mesh for all elements")]
    [SerializeField] private bool useSimpleQuadMesh = true;

    [Header("Random Seed")]
    [Tooltip("Random seed for reproducible generation")]
    [SerializeField] private int randomSeed = 0;
    
    [Tooltip("Use random seed each time")]
    [SerializeField] private bool useRandomSeed = true;

    // GPU Instancing data
    private Mesh simpleQuadMesh;
    private System.Random random;
    
    // Instance data for GPU rendering
    private List<GrassInstance> grassInstances = new List<GrassInstance>();
    private List<DecalInstance> decalInstances = new List<DecalInstance>();
    private List<BushInstance> bushInstances = new List<BushInstance>();
    
    // Matrices for GPU instancing
    private Matrix4x4[] grassMatrices;
    private Matrix4x4[] decalMatrices;
    private Matrix4x4[] bushMatrices;
    
    // Material property blocks for GPU instancing
    private MaterialPropertyBlock grassPropertyBlock;
    private MaterialPropertyBlock decalPropertyBlock;
    private MaterialPropertyBlock bushPropertyBlock;

    // Instance data structures
    private struct GrassInstance
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public int textureIndex;
    }
    
    private struct DecalInstance
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public int textureIndex;
    }
    
    private struct BushInstance
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public int textureIndex;
    }

    private void Start()
    {
        // Auto-find components
        if (terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
        }

        if (terrainBuilder != null)
        {
            roadWidth = terrainBuilder.GetRoadWidth();
            roadCenterZ = terrainBuilder.GetRoadCenterZ();
        }
        else if (terrain != null)
        {
            roadCenterZ = terrain.terrainData.size.z / 2f;
        }

        // Create simple quad mesh if needed
        if (useSimpleQuadMesh && simpleQuadMesh == null)
        {
            simpleQuadMesh = CreateSimpleQuadMesh();
        }

        // Initialize materials with GPU instancing support
        InitializeMaterials();

        // Generate detail elements
        GenerateDetails();
    }

    private void Update()
    {
        // Render all instances using GPU instancing
        RenderInstances();
    }

    /// <summary>
    /// Create a simple quad mesh for instancing
    /// </summary>
    private Mesh CreateSimpleQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "SimpleQuad";
        
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-0.5f, 0f, 0f),
            new Vector3(0.5f, 0f, 0f),
            new Vector3(-0.5f, 1f, 0f),
            new Vector3(0.5f, 1f, 0f)
        };
        
        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        
        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
        
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }

    /// <summary>
    /// Initialize materials with GPU instancing support
    /// </summary>
    private void InitializeMaterials()
    {
        // Create grass material if not assigned
        if (grassMaterial == null && grassTextures != null && grassTextures.Length > 0)
        {
            grassMaterial = new Material(Shader.Find("Unlit/Texture"));
            grassMaterial.enableInstancing = true;
            grassMaterial.mainTexture = grassTextures[0];
        }
        else if (grassMaterial != null)
        {
            grassMaterial.enableInstancing = true;
        }

        // Create decal material if not assigned
        if (decalMaterial == null && decalTextures != null && decalTextures.Length > 0)
        {
            decalMaterial = new Material(Shader.Find("Unlit/Texture"));
            decalMaterial.enableInstancing = true;
            decalMaterial.mainTexture = decalTextures[0];
        }
        else if (decalMaterial != null)
        {
            decalMaterial.enableInstancing = true;
        }

        // Create bush material if not assigned
        if (bushMaterial == null && bushTextures != null && bushTextures.Length > 0)
        {
            bushMaterial = new Material(Shader.Find("Unlit/Texture"));
            bushMaterial.enableInstancing = true;
            bushMaterial.mainTexture = bushTextures[0];
        }
        else if (bushMaterial != null)
        {
            bushMaterial.enableInstancing = true;
        }

        // Initialize property blocks
        grassPropertyBlock = new MaterialPropertyBlock();
        decalPropertyBlock = new MaterialPropertyBlock();
        bushPropertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Set random seed for reproducible generation
    /// </summary>
    public void SetRandomSeed(int seed)
    {
        randomSeed = seed;
        if (random != null)
        {
            random = new System.Random(seed);
        }
    }

    /// <summary>
    /// Generate all detail elements
    /// </summary>
    public void GenerateDetails()
    {
        if (terrain == null)
        {
            Debug.LogError("[OptimizedDetailGenerator] Terrain is null!");
            return;
        }

        // Initialize random
        if (useRandomSeed)
        {
            randomSeed = Random.Range(0, 100000);
        }
        random = new System.Random(randomSeed);

        // Clear existing instances
        grassInstances.Clear();
        decalInstances.Clear();
        bushInstances.Clear();

        // Generate grass lumps (every 1-2m)
        if (enableGrassLumps)
        {
            GenerateGrassLumps();
        }

        // Generate road edge decals (cracks and dirt)
        if (enableRoadDecals)
        {
            GenerateRoadDecals();
        }

        // Generate roadside bushes
        if (enableBushes)
        {
            GenerateRoadsideBushes();
        }

        // Prepare matrices for GPU instancing
        PrepareInstanceMatrices();

        Debug.Log($"[OptimizedDetailGenerator] Generated: {grassInstances.Count} grass lumps, {decalInstances.Count} decals, {bushInstances.Count} bushes");
    }

    /// <summary>
    /// Generate grass lumps every 1-2m along the roadside
    /// </summary>
    private void GenerateGrassLumps()
    {
        float currentX = startOffset;
        
        while (currentX < courseLength - endOffset)
        {
            // Random spacing (1-2m)
            float spacing = (float)(random.NextDouble() * (grassSpacing.y - grassSpacing.x) + grassSpacing.x);
            currentX += spacing;

            if (currentX >= courseLength - endOffset) break;

            // Place on both sides of road
            for (int side = 0; side < 2; side++)
            {
                bool leftSide = (side == 0);
                
                // Random distance from road edge
                float distance = grassDistanceFromRoad + (float)(random.NextDouble() * (grassMaxDistance - grassDistanceFromRoad));
                float zOffset = (roadWidth / 2f) + distance;
                if (leftSide) zOffset = -zOffset;

                float worldZ = roadCenterZ + zOffset;
                Vector3 worldPos = new Vector3(currentX, 0f, worldZ);

                // Get terrain height
                float height = terrain.SampleHeight(worldPos);
                worldPos.y = height;

                // Randomize scale (with variation)
                float baseScale = (float)(random.NextDouble() * (grassSizeRange.y - grassSizeRange.x) + grassSizeRange.x);
                float scaleVariation = 1f + ((float)random.NextDouble() - 0.5f) * 2f * grassScaleVariation;
                Vector3 scale = Vector3.one * baseScale * scaleVariation;

                // Randomize rotation
                float yRotation = (float)(random.NextDouble() * 360f);
                float rotationVariation = ((float)random.NextDouble() - 0.5f) * 2f * grassRotationVariation;
                Quaternion rotation = Quaternion.Euler(0f, yRotation + rotationVariation, 0f);

                // Random texture index
                int textureIndex = grassTextures != null && grassTextures.Length > 0 ? random.Next(grassTextures.Length) : 0;

                grassInstances.Add(new GrassInstance
                {
                    position = worldPos,
                    rotation = rotation,
                    scale = scale,
                    textureIndex = textureIndex
                });
            }
        }
    }

    /// <summary>
    /// Generate road edge decals (cracks and dirt)
    /// </summary>
    private void GenerateRoadDecals()
    {
        float currentX = startOffset;
        
        while (currentX < courseLength - endOffset)
        {
            // Random spacing (2-5m)
            float spacing = (float)(random.NextDouble() * (decalSpacing.y - decalSpacing.x) + decalSpacing.x);
            currentX += spacing;

            if (currentX >= courseLength - endOffset) break;

            // Place on both edges of road
            for (int edge = 0; edge < 2; edge++)
            {
                bool leftEdge = (edge == 0);
                
                float zOffset = (roadWidth / 2f) + decalDistanceFromEdge;
                if (leftEdge) zOffset = -zOffset;

                float worldZ = roadCenterZ + zOffset;
                Vector3 worldPos = new Vector3(currentX, 0f, worldZ);

                // Get terrain height (slightly above road surface)
                float height = terrain.SampleHeight(worldPos);
                worldPos.y = height + 0.01f;

                // Randomize scale
                float baseScale = (float)(random.NextDouble() * (decalSizeRange.y - decalSizeRange.x) + decalSizeRange.x);
                float scaleVariation = 1f + ((float)random.NextDouble() - 0.5f) * 2f * decalScaleVariation;
                Vector3 scale = Vector3.one * baseScale * scaleVariation;

                // Randomize rotation
                float yRotation = (float)(random.NextDouble() * decalRotationVariation);
                Quaternion rotation = Quaternion.Euler(0f, yRotation, 0f);

                // Random texture index
                int textureIndex = decalTextures != null && decalTextures.Length > 0 ? random.Next(decalTextures.Length) : 0;

                decalInstances.Add(new DecalInstance
                {
                    position = worldPos,
                    rotation = rotation,
                    scale = scale,
                    textureIndex = textureIndex
                });
            }
        }
    }

    /// <summary>
    /// Generate roadside bushes
    /// </summary>
    private void GenerateRoadsideBushes()
    {
        float currentX = startOffset;
        
        while (currentX < courseLength - endOffset)
        {
            // Random spacing (3-8m)
            float spacing = (float)(random.NextDouble() * (bushSpacing.y - bushSpacing.x) + bushSpacing.x);
            currentX += spacing;

            if (currentX >= courseLength - endOffset) break;

            // Place on both sides of road
            for (int side = 0; side < 2; side++)
            {
                bool leftSide = (side == 0);
                
                // Random distance from road edge
                float distance = bushDistanceFromRoad + (float)(random.NextDouble() * (bushMaxDistance - bushDistanceFromRoad));
                float zOffset = (roadWidth / 2f) + distance;
                if (leftSide) zOffset = -zOffset;

                float worldZ = roadCenterZ + zOffset;
                Vector3 worldPos = new Vector3(currentX, 0f, worldZ);

                // Get terrain height
                float height = terrain.SampleHeight(worldPos);
                worldPos.y = height;

                // Randomize scale
                float baseScale = (float)(random.NextDouble() * (bushSizeRange.y - bushSizeRange.x) + bushSizeRange.x);
                float scaleVariation = 1f + ((float)random.NextDouble() - 0.5f) * 2f * bushScaleVariation;
                Vector3 scale = Vector3.one * baseScale * scaleVariation;

                // Randomize rotation
                float yRotation = (float)(random.NextDouble() * bushRotationVariation);
                Quaternion rotation = Quaternion.Euler(0f, yRotation, 0f);

                // Random texture index
                int textureIndex = bushTextures != null && bushTextures.Length > 0 ? random.Next(bushTextures.Length) : 0;

                bushInstances.Add(new BushInstance
                {
                    position = worldPos,
                    rotation = rotation,
                    scale = scale,
                    textureIndex = textureIndex
                });
            }
        }
    }

    /// <summary>
    /// Prepare transformation matrices for GPU instancing
    /// </summary>
    private void PrepareInstanceMatrices()
    {
        // Prepare grass matrices
        grassMatrices = new Matrix4x4[grassInstances.Count];
        for (int i = 0; i < grassInstances.Count; i++)
        {
            GrassInstance instance = grassInstances[i];
            grassMatrices[i] = Matrix4x4.TRS(instance.position, instance.rotation, instance.scale);
        }

        // Prepare decal matrices
        decalMatrices = new Matrix4x4[decalInstances.Count];
        for (int i = 0; i < decalInstances.Count; i++)
        {
            DecalInstance instance = decalInstances[i];
            decalMatrices[i] = Matrix4x4.TRS(instance.position, instance.rotation, instance.scale);
        }

        // Prepare bush matrices
        bushMatrices = new Matrix4x4[bushInstances.Count];
        for (int i = 0; i < bushInstances.Count; i++)
        {
            BushInstance instance = bushInstances[i];
            bushMatrices[i] = Matrix4x4.TRS(instance.position, instance.rotation, instance.scale);
        }
    }

    /// <summary>
    /// Render all instances using GPU instancing (called every frame)
    /// </summary>
    private void RenderInstances()
    {
        if (simpleQuadMesh == null) return;

        // Render grass instances in batches
        if (enableGrassLumps && grassMaterial != null && grassMatrices != null && grassMatrices.Length > 0)
        {
            RenderInstancesBatched(grassMatrices, grassMaterial, grassPropertyBlock, grassTextures);
        }

        // Render decal instances in batches
        if (enableRoadDecals && decalMaterial != null && decalMatrices != null && decalMatrices.Length > 0)
        {
            RenderInstancesBatched(decalMatrices, decalMaterial, decalPropertyBlock, decalTextures);
        }

        // Render bush instances in batches
        if (enableBushes && bushMaterial != null && bushMatrices != null && bushMatrices.Length > 0)
        {
            RenderInstancesBatched(bushMatrices, bushMaterial, bushPropertyBlock, bushTextures);
        }
    }

    /// <summary>
    /// Render instances in batches (GPU instancing has a limit of 1023 per batch)
    /// </summary>
    private void RenderInstancesBatched(Matrix4x4[] matrices, Material material, MaterialPropertyBlock propertyBlock, Texture2D[] textures)
    {
        int totalInstances = matrices.Length;
        int batchSize = Mathf.Min(maxInstancesPerBatch, totalInstances);
        int batches = Mathf.CeilToInt((float)totalInstances / batchSize);

        for (int batch = 0; batch < batches; batch++)
        {
            int startIndex = batch * batchSize;
            int endIndex = Mathf.Min(startIndex + batchSize, totalInstances);
            int count = endIndex - startIndex;

            // Create batch array
            Matrix4x4[] batchMatrices = new Matrix4x4[count];
            System.Array.Copy(matrices, startIndex, batchMatrices, 0, count);

            // Set texture if available (you may want to use texture arrays for multiple textures)
            if (textures != null && textures.Length > 0 && propertyBlock != null)
            {
                // For simplicity, use first texture. For multiple textures, use texture arrays or separate batches
                propertyBlock.SetTexture("_MainTex", textures[0]);
                Graphics.DrawMeshInstanced(simpleQuadMesh, 0, material, batchMatrices, count, propertyBlock);
            }
            else
            {
                Graphics.DrawMeshInstanced(simpleQuadMesh, 0, material, batchMatrices, count);
            }
        }
    }
}
