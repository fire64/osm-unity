using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GrassGenerator : MonoBehaviour
{
    [Tooltip("Define different grass types here.")]
    public List<GrassType> grassTypes = new List<GrassType>();

    [Header("Optimization")]
    public float maxDrawDistance = 100f;
    public int instancesPerBatch = 1000;

    [Header("Frustum Culling")]
    public bool useFrustumCulling = true;
    public float cullingMargin = 1.0f; // Запас для каллинга

    private Mesh targetMesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector3[] normals;

    public bool isInit = false;
    public bool isFirstGenerated = true;

    // Для procedural rendering
    private List<Matrix4x4>[] allMatrices;
    private List<bool>[] visibleInstances; // Отслеживаем видимость каждого инстанса
    private MaterialPropertyBlock propertyBlock;
    private Bounds renderBounds;
    private Camera mainCamera;
    private Plane[] cameraFrustumPlanes;

    // Для вычисления bounding sphere травинки
    private Vector3 grassBoundsCenter = new Vector3(0, 0.5f, 0);
    private float grassBoundsRadius = 0.8f;

    void Start()
    {
        targetMesh = GetComponent<MeshFilter>().mesh;
        if (targetMesh == null)
        {
            Debug.LogError("No MeshFilter or Mesh found on " + gameObject.name);
            return;
        }

        vertices = targetMesh.vertices;
        triangles = targetMesh.triangles;
        normals = targetMesh.normals;

        mainCamera = Camera.main;
        propertyBlock = new MaterialPropertyBlock();
        renderBounds = new Bounds(transform.position, Vector3.one * maxDrawDistance * 2);
    }

    public void InitGrassGeneration(List<GrassType> setgrassTypes)
    {
        if (targetMesh == null)
        {
            targetMesh = GetComponent<MeshFilter>().mesh;
            if (targetMesh == null)
            {
                Debug.LogError("No MeshFilter or Mesh found on " + gameObject.name);
                return;
            }
            vertices = targetMesh.vertices;
            triangles = targetMesh.triangles;
            normals = targetMesh.normals;
        }

        grassTypes.Clear();
        grassTypes.AddRange(setgrassTypes);
        isInit = true;
    }

    void GenerateGrassPositions()
    {
        if (!isInit) return;

        allMatrices = new List<Matrix4x4>[grassTypes.Count];
        visibleInstances = new List<bool>[grassTypes.Count];

        for (int typeIdx = 0; typeIdx < grassTypes.Count; typeIdx++)
        {
            var grassType = grassTypes[typeIdx];
            if (grassType.grassTexture == null)
            {
                Debug.LogWarning($"Grass type '{grassType.name}' has no texture assigned. Skipping.");
                continue;
            }

            grassTypes[typeIdx].generatedMaterial = CreateMaterialForGrassType(grassType);
            grassTypes[typeIdx].generatedGrassMesh = GenerateUniqueGrassMesh();

            List<Matrix4x4> positions = new List<Matrix4x4>();
            List<bool> visibility = new List<bool>();

            int uniqueSeed = gameObject.GetInstanceID() * 1000 + typeIdx;
            Random.InitState(uniqueSeed);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];

                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;
                Vector3 normal = Vector3.Cross(edge1, edge2).normalized;

                if (Vector3.Angle(normal, Vector3.up) <= grassTypes[typeIdx].maxSlope)
                {
                    float area = CalculateTriangleArea(v0, v1, v2);
                    int grassCount = Mathf.RoundToInt(area * grassTypes[typeIdx].density);

                    for (int j = 0; j < grassCount; j++)
                    {
                        Vector3 position = GetRandomPointInTriangle(v0, v1, v2);
                        float randomYRotation = Random.Range(0f, 360f);
                        Quaternion rotation = Quaternion.Euler(0, randomYRotation, 0);
                        float randomSize = Random.Range(grassTypes[typeIdx].sizeRange.x, grassTypes[typeIdx].sizeRange.y);
                        Vector3 scale = Vector3.one * randomSize;

                        Matrix4x4 instanceMatrix = Matrix4x4.TRS(
                            transform.TransformPoint(position),
                            transform.rotation * rotation,
                            new Vector3(
                                scale.x * transform.lossyScale.x,
                                scale.y * transform.lossyScale.y,
                                scale.z * transform.lossyScale.z
                            )
                        );

                        positions.Add(instanceMatrix);
                        visibility.Add(true); // По умолчанию все видимы
                    }
                }
            }

            allMatrices[typeIdx] = positions;
            visibleInstances[typeIdx] = visibility;
        }
    }

    private Mesh GenerateUniqueGrassMesh()
    {
        Mesh mesh = new Mesh();
        Vector3[] verts = new Vector3[4]
        {
            new Vector3(-0.5f, 0, 0),
            new Vector3(0.5f, 0, 0),
            new Vector3(-0.5f, 1, 0),
            new Vector3(0.5f, 1, 0)
        };

        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        int[] tris = new int[6] { 0, 2, 1, 2, 3, 1 };

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.name = $"GrassMesh_{gameObject.GetInstanceID()}";
        return mesh;
    }

    Material CreateMaterialForGrassType(GrassType grassType)
    {
        Shader grassShader = Shader.Find("Custom/Grass");
        if (grassShader == null)
        {
            grassShader = Shader.Find("Standard");
        }

        Material newMaterial = new Material(grassShader);
        newMaterial.SetTexture("_MainTex", grassType.grassTexture);
        newMaterial.color = grassType.grassColor;
        newMaterial.enableInstancing = true;

        if (grassType.grassTexture != null && HasTransparency(grassType.grassTexture))
        {
            newMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            newMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            newMaterial.SetInt("_ZWrite", 0);
            newMaterial.DisableKeyword("_ALPHATEST_ON");
            newMaterial.EnableKeyword("_ALPHABLEND_ON");
            newMaterial.renderQueue = 3000;
        }

        newMaterial.name = $"GrassMaterial_{gameObject.GetInstanceID()}_{grassType.name}";
        return newMaterial;
    }

    private bool HasTransparency(Texture2D texture)
    {
        return true;
    }

    void Update()
    {
        if (!isInit || mainCamera == null)
        {
            mainCamera = Camera.main;
            return;
        }

        float distToPlayer = Vector3.Distance(mainCamera.transform.position, transform.position);
        if (distToPlayer > maxDrawDistance)
        {
            return;
        }

        if(isFirstGenerated)
        {
            GenerateGrassPositions();
            isFirstGenerated = false;
        }

        // Обновляем фрустум плоскости камеры
        if (useFrustumCulling)
        {
            cameraFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            UpdateFrustumCulling();
        }

        RenderGrassInstances();
    }

    void UpdateFrustumCulling()
    {
        if (cameraFrustumPlanes == null || allMatrices == null) return;

        for (int typeIdx = 0; typeIdx < grassTypes.Count; typeIdx++)
        {
            if (allMatrices[typeIdx] == null || visibleInstances[typeIdx] == null) continue;

            var matrices = allMatrices[typeIdx];
            var visibility = visibleInstances[typeIdx];

            for (int i = 0; i < matrices.Count; i++)
            {
                // Получаем позицию и масштаб из матрицы
                Vector3 position = matrices[i].GetPosition();
                Vector3 scale = matrices[i].lossyScale;

                // Создаем bounding sphere для травинки с учетом масштаба
                float scaledRadius = grassBoundsRadius * Mathf.Max(scale.x, scale.y, scale.z);
                Vector3 worldCenter = position + grassBoundsCenter * scale.y;

                // Проверяем видимость
                bool isVisible = true;
                for (int j = 0; j < cameraFrustumPlanes.Length; j++)
                {
                    if (cameraFrustumPlanes[j].GetDistanceToPoint(worldCenter) < -scaledRadius - cullingMargin)
                    {
                        isVisible = false;
                        break;
                    }
                }

                visibility[i] = isVisible;
            }
        }
    }

    void RenderGrassInstances()
    {
        if (allMatrices == null) return;

        for (int typeIdx = 0; typeIdx < grassTypes.Count; typeIdx++)
        {
            if (grassTypes[typeIdx].generatedMaterial == null ||
                grassTypes[typeIdx].generatedGrassMesh == null ||
                allMatrices[typeIdx] == null ||
                visibleInstances[typeIdx] == null) continue;

            var matrices = allMatrices[typeIdx];
            var visibility = visibleInstances[typeIdx];

            // Создаем временный список для видимых инстансов
            List<Matrix4x4> visibleMatrices = new List<Matrix4x4>();

            for (int i = 0; i < matrices.Count; i++)
            {
                if (visibility[i])
                {
                    visibleMatrices.Add(matrices[i]);
                }
            }

            if (visibleMatrices.Count == 0) continue;

            // Разбиваем на батчи с учетом лимита
            for (int i = 0; i < visibleMatrices.Count; i += instancesPerBatch)
            {
                int count = Mathf.Min(instancesPerBatch, visibleMatrices.Count - i);
                Matrix4x4[] batch = new Matrix4x4[count];

                for (int j = 0; j < count; j++)
                {
                    batch[j] = visibleMatrices[i + j];
                }

                Graphics.DrawMeshInstanced(
                    grassTypes[typeIdx].generatedGrassMesh,
                    0,
                    grassTypes[typeIdx].generatedMaterial,
                    batch,
                    count,
                    propertyBlock,
                    ShadowCastingMode.Off,
                    false,
                    gameObject.layer,
                    mainCamera,
                    LightProbeUsage.Off,
                    null
                );
            }
        }
    }

    float CalculateTriangleArea(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        return Vector3.Cross(ab, ac).magnitude * 0.5f;
    }

    Vector3 GetRandomPointInTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        float r1 = Random.Range(0f, 1f);
        float r2 = Random.Range(0f, 1f);
        if (r1 + r2 > 1)
        {
            r1 = 1 - r1;
            r2 = 1 - r2;
        }
        return a + r1 * (b - a) + r2 * (c - a);
    }

    void OnValidate()
    {
        if (Application.isPlaying && isInit && mainCamera != null)
        {
            float distToPlayer = Vector3.Distance(mainCamera.transform.position, transform.position);
            if (distToPlayer <= maxDrawDistance)
            {
                GenerateGrassPositions();
            }
        }
    }

    void OnDestroy()
    {
        if (grassTypes != null)
        {
            foreach (var grassType in grassTypes)
            {
                if (grassType.generatedMaterial != null)
                {
                    DestroyImmediate(grassType.generatedMaterial);
                }
                if (grassType.generatedGrassMesh != null)
                {
                    DestroyImmediate(grassType.generatedGrassMesh);
                }
            }
        }
    }
}