using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static GrassSettings;

public class GrassPlacer : MonoBehaviour
{
    [Header("Grass Settings")]
    public Collider groundCollider;
    [Range(0.1f, 100f)] public float grassDensity = 200f; // Травинок на 100 кв. единиц
    [Range(0f, 60f)] public float maxSlopeAngle = 45f;
    public float minHeight = 0f;
    public float maxHeight = 200f;

    [Header("Optimization")]
    public int maxGrassPerFrame = 1000;
    public int maxAttemptsFactor = 10;

    private Bounds meshBounds;
    private float surfaceArea;
    private int targetGrassCount;
    private int placedGrass;
    private int totalAttempts;
    private int maxAttempts;
    private bool isStartPlacing = false;

    // Компоненты для меша травы
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh grassMesh;

    // Данные для генерации меша
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Color> colors = new List<Color>();

    // Настройки травы
    private GrassSettingsInfo[] grassSettings;

    void Update()
    {
        if (isStartPlacing)
        {
            if (placedGrass >= targetGrassCount || totalAttempts >= maxAttempts)
            {
                FinishGrassGeneration();
                isStartPlacing = false;
                return;
            }

            GenerateGrassPerFrame();
        }
    }

    public void GenerateGrass(Landuse curLanduse, GrassSettingsInfo[] grassSettings)
    {
        groundCollider = curLanduse.GetComponent<Collider>();
        this.grassSettings = grassSettings;

        if (groundCollider == null)
        {
            Debug.LogError("Ground collider not assigned on landuse: " + curLanduse.Id);
            return;
        }

        InitializeGeneration();
        isStartPlacing = true;
    }

    void InitializeGeneration()
    {
        meshBounds = groundCollider.bounds;
        surfaceArea = CalculateSurfaceArea(meshBounds);

        targetGrassCount = Mathf.RoundToInt(surfaceArea * grassDensity / 100f);
        Debug.Log($"Generating grass: {targetGrassCount} blades on {surfaceArea:F0} sq units");

        placedGrass = 0;
        totalAttempts = 0;
        maxAttempts = targetGrassCount * maxAttemptsFactor;

        // Очистка данных меша
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        colors.Clear();

        // Настройка компонентов меша
        meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        grassMesh = new Mesh();
    }

    void GenerateGrassPerFrame()
    {
        int grassPlacedThisFrame = 0;
        int attemptsThisFrame = 0;
        int maxAttemptsThisFrame = maxGrassPerFrame * 3;

        while (grassPlacedThisFrame < maxGrassPerFrame &&
               attemptsThisFrame < maxAttemptsThisFrame &&
               placedGrass < targetGrassCount &&
               totalAttempts < maxAttempts)
        {
            attemptsThisFrame++;
            totalAttempts++;

            if (TryPlaceGrassBlade())
                grassPlacedThisFrame++;
        }
    }

    bool TryPlaceGrassBlade()
    {
        Vector3 rayOrigin = new Vector3(
            Random.Range(meshBounds.min.x, meshBounds.max.x),
            meshBounds.max.y + Random.Range(minHeight, maxHeight),
            Random.Range(meshBounds.min.z, meshBounds.max.z)
        );

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxHeight * 2))
        {
            if (IsValidPosition(hit) && hit.transform.gameObject == groundCollider.gameObject)
            {
                PlaceGrassBlade(hit.point);
                return true;
            }
        }
        return false;
    }

    bool IsValidPosition(RaycastHit hit)
    {
        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        return slopeAngle <= maxSlopeAngle;
    }

    void PlaceGrassBlade(Vector3 position)
    {
        if (grassSettings == null || grassSettings.Length == 0) return;
        
        GrassSettingsInfo settings = grassSettings[Random.Range(0, grassSettings.Length)];
        
        float width = Random.Range(settings.m_MinWidth, settings.m_MaxWidth);
        float height = Random.Range(settings.m_MinHeight, settings.m_MaxHeight);
        Color color = Color.Lerp(settings.m_DryColor, settings.m_HealthyColor, Random.value);
        float rotation = Random.Range(0f, 360f);

        CreateGrassQuad(position, width, height, color, rotation);
        CreateGrassQuad(position, width, height, color, rotation + 90f); // Перпендикулярный квад

        placedGrass++;
    }

    void CreateGrassQuad(Vector3 position, float width, float height, Color color, float rotation)
    {
        int vertexIndex = vertices.Count;
        
        // Матрица трансформации для поворота
        Quaternion rot = Quaternion.Euler(0, rotation, 0);
        
        // Вершины квада (локальные координаты)
        Vector3[] localVertices = new Vector3[4]
        {
            new Vector3(-width/2, 0, 0),
            new Vector3(width/2, 0, 0),
            new Vector3(width/2, height, 0),
            new Vector3(-width/2, height, 0)
        };

        // Преобразование и добавление вершин
        for (int i = 0; i < 4; i++)
        {
            vertices.Add(position + rot * localVertices[i]);
            colors.Add(color);
        }

        // UV координаты
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        // Треугольники (два треугольника на квад)
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
        
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 3);
    }

    void FinishGrassGeneration()
    {
        // Применение данных к мешу
        grassMesh.SetVertices(vertices);
        grassMesh.SetTriangles(triangles, 0);
        grassMesh.SetUVs(0, uvs);
        grassMesh.SetColors(colors);

        grassMesh.RecalculateNormals();
        grassMesh.RecalculateBounds();
        
        meshFilter.mesh = grassMesh;

        // Настройка материала
        if (grassSettings.Length > 0 && grassSettings[0].m_PrototypeTexture != null)
        {
            Material grassMaterial = new Material(Shader.Find("Standard"));
            grassMaterial.mainTexture = grassSettings[0].m_PrototypeTexture;

            grassMaterial.SetOverrideTag("RenderType", "Transparent");
            grassMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            grassMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            grassMaterial.SetInt("_ZWrite", 0);
            grassMaterial.DisableKeyword("_ALPHATEST_ON");
            grassMaterial.EnableKeyword("_ALPHABLEND_ON");
            grassMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            grassMaterial.SetFloat("_BlendModePreserveSpecular", 0);
            grassMaterial.SetFloat("_AlphaToMask", 0);
            grassMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            meshRenderer.material = grassMaterial;

        }

        Debug.Log($"Grass generation complete: {placedGrass} blades placed");
    }

    float CalculateSurfaceArea(Bounds bounds)
    {
        Vector3 size = bounds.size;
        return (size.x * size.z) * 2f; // Учет только горизонтальных поверхностей
    }
}