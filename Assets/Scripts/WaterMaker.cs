using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static GR;
using static UnityEngine.UI.GridLayoutGroup;

class WaterMaker : InfrstructureBehaviour
{
    public Material waterMaterial;
    public static GameContentSelector contentselector;
    public bool isCreateColision = false;
    public int MaxNodes = 150;
    public TileSystem tileSystem;

    private int m_countProcessing = 0;

    // Список для отслеживания уже обработанных ID
    private HashSet<ulong> processedIDs = new HashSet<ulong>();

    // ============================================
    // ОПТИМИЗАЦИЯ: batchSize для пакетной обработки
    // ============================================
    [Header("Optimization Settings")]
    [Tooltip("Количество water объектов обрабатываемых за один кадр")]
    public int batchSize = 10;

    // ============================================
    // ОПТИМИЗАЦИЯ: Object Pooling для MeshData
    // ============================================
    private Stack<MeshData> meshDataPool = new Stack<MeshData>();

    // ============================================
    // ОПТИМИЗАЦИЯ: Кэширование ссылок
    // ============================================
    private Dictionary<ulong, OsmNode> cachedNodes;
    private Vector3 cachedWorldOrigin;

    private void SetProperties(BaseOsm geo, Water water)
    {
        water.name = "water " + geo.ID.ToString();

        if (geo.HasField("name"))
            water.Name = geo.GetValueStringByKey("name");

        water.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("water"))
        {
            kind = geo.GetValueStringByKey("water");
        }
        else
        {
            kind = "yes";
        }

        water.Kind = kind;

        if (geo.HasField("layer"))
        {
            water.layer = geo.GetValueIntByKey("layer");
        }

        if (geo.HasField("source_type"))
            water.Source = geo.GetValueStringByKey("source_type");

        float waterwidth = 0f;

        if (geo.HasField("width"))
        {
            waterwidth = geo.GetValueFloatByKey("width");
        }
        else
        {
            waterwidth = 2.0f;
        }

        water.width = waterwidth;

        // ОПТИМИЗАЦИЯ: Кэшируем MeshRenderer
        var meshRenderer = water.GetComponent<MeshRenderer>();
        meshRenderer.material = waterMaterial;

        //      meshRenderer.material.SetColor("_Color", GR.SetOSMColour(geo)); //Not used color for water ))
    }

    // ============================================
    // ОПТИМИЗАЦИЯ: Object Pooling для MeshData
    // ============================================
    private MeshData GetMeshData()
    {
        if (meshDataPool.Count > 0)
        {
            var md = meshDataPool.Pop();
            md.Clear();
            return md;
        }
        return new MeshData();
    }

    private void ReturnMeshData(MeshData md)
    {
        if (md != null)
        {
            meshDataPool.Push(md);
        }
    }

    // Вспомогательный метод для безопасного получения центра
    private Vector3 GetCentre(BaseOsm geo)
    {
        Vector3 total = Vector3.zero;
        int count = 0;

        // ОПТИМИЗАЦИЯ: Используем кэшированную ссылку на nodes
        var nodes = cachedNodes ?? MapReader.Instance.nodes;

        foreach (var id in geo.NodeIDs)
        {
            if (nodes.TryGetValue(id, out OsmNode node))
            {
                total += (Vector3)node;
                count++;
            }
        }
        return count > 0 ? total / count : Vector3.zero;
    }

    void CreateWaterss(BaseOsm geo)
    {
        // Защита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        var searchname = "water " + geo.ID.ToString();

        m_countProcessing++;

        // ОПТИМИЗАЦИЯ: Безопасная проверка contentselector
        if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
        {
            Debug.LogError(searchname + " disabled.");
            return;
        }

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var water = new GameObject(searchname).AddComponent<Water>();

        water.AddComponent<MeshFilter>();
        water.AddComponent<MeshRenderer>();

        water.itemlist = geo.itemlist;

        SetProperties(geo, water);

        var countContour = geo.NodeIDs.Count;

        if (countContour < 2)
        {
            Debug.LogError(searchname + " haved " + countContour + " contours.");
            return;
        }

        // ОПТИМИЗАЦИЯ: Предварительное выделение памяти для списка
        var waterCorners = new List<Vector3>(countContour);

        Vector3 localOrigin = GetCentre(geo);

        // ОПТИМИЗАЦИЯ: Используем кэшированный WorldOrigin
        water.transform.position = localOrigin - cachedWorldOrigin;

        water.transform.position += Vector3.up * 0.025f;
        water.transform.position += Vector3.up * (water.layer * BaseDataObject.layer_size);

        // ОПТИМИЗАЦИЯ: Используем кэшированную ссылку на nodes
        var nodes = cachedNodes ?? MapReader.Instance.nodes;

        for (int i = 0; i < countContour; i++)
        {
            // ИЗМЕНЕНИЕ: Безопасный доступ к нодам
            if (nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 coords = point - localOrigin;
                waterCorners.Add(coords);
            }
        }

        var holesCorners = new List<List<Vector3>>();

        var countHoles = geo.HolesNodeListsIDs.Count;

        for (int i = 0; i < countHoles; i++)
        {
            var holeNodes = geo.HolesNodeListsIDs[i];
            var countHoleContourPoints = holeNodes.Count;
            // ОПТИМИЗАЦИЯ: Предварительное выделение памяти
            var holeContour = new List<Vector3>(countHoleContourPoints);

            for (int j = 0; j < countHoleContourPoints; j++)
            {
                if (nodes.TryGetValue(holeNodes[j], out OsmNode point))
                {
                    Vector3 coords = point - localOrigin;
                    holeContour.Add(coords);
                }
            }

            holesCorners.Add(holeContour);
        }

        var mesh = water.GetComponent<MeshFilter>().mesh;

        // ОПТИМИЗАЦИЯ: Используем пул для MeshData
        var tb = GetMeshData();

        if (geo.IsClosedPolygon)
        {
            GR.CreateMeshWithHeight(waterCorners, -10.0f, 0.0f, tb, holesCorners);
        }
        else if (geo.HasField("type") && geo.GetValueStringByKey("type") == "multipolygon")
        {
            GR.CreateMeshWithHeight(waterCorners, -10.0f, 0.0f, tb, holesCorners);
        }
        else
        {
            GR.CreateMeshLineWithWidthAndHeight(waterCorners, 0.01f, 0.0f, water.width, tb);
        }

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.normals = tb.Normals.ToArray();
        mesh.SetUVs(0, tb.UV);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();

        // ОПТИМИЗАЦИЯ: Возвращаем MeshData в пул
        ReturnMeshData(tb);

        // Add colider
        if (isCreateColision)
        {
            water.transform.gameObject.AddComponent<MeshCollider>();
            water.transform.GetComponent<MeshCollider>().sharedMesh = water.GetComponent<MeshFilter>().mesh;
            water.transform.GetComponent<MeshCollider>().convex = false;
        }

        // ОПТИМИЗАЦИЯ: Безопасная проверка tileSystem
        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                // Настройки тесселяции
                var settings = new TessellationSettings
                {
                    maxEdgeLength = 0.001f,      // Максимальное расстояние между вершинами в метрах
                    heightSensitivity = 0.001f,  // Минимальный перепад высот для разбиения
                    maxVertexCount = 50000,      // Максимальное количество вершин
                    heightfix = 0.3f             // Корректировка высоты
                };

                if (geo.IsClosedPolygon)
                {
                    StartCoroutine(SpawnInHeight(water.gameObject, AlgorithmHeightSorting.AverageHeight));
                }
                else
                {
                    StartCoroutine(AdjustMeshToTerrainCorutine(water.gameObject, settings));
                }
            }
        }

        water.Activate();
    }

    IEnumerator Start()
    {
        // Ждем готовности MapReader
        while (MapReader.Instance == null || !MapReader.Instance.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();
        tileSystem = FindObjectOfType<TileSystem>();

        // ОПТИМИЗАЦИЯ: Кэшируем ссылки один раз при старте
        cachedNodes = MapReader.Instance.nodes;
        cachedWorldOrigin = MapReader.Instance.WorldOrigin;

        // 1. Подписываемся на новые события
        MapReader.Instance.OnWayLoaded += OnGeoObjectLoaded;
        MapReader.Instance.OnRelationLoaded += OnGeoObjectLoaded;

        float starttime = Time.time;

        // ============================================
        // ОПТИМИЗАЦИЯ: Пакетная обработка объектов
        // ============================================
        int processedInBatch = 0;

        // 2. Обрабатываем уже загруженные данные
        var ways = MapReader.Instance.ways;
        if (ways != null)
        {
            foreach (var way in ways)
            {
                if (way.objectType == BaseOsm.ObjectType.Water && way.NodeIDs.Count > 1)
                {
                    way.AddField("source_type", "way");
                    CreateWaterss(way);

                    processedInBatch++;
                    if (processedInBatch >= batchSize)
                    {
                        processedInBatch = 0;
                        yield return null; // Пауза только после обработки batchSize объектов
                    }
                }
            }
        }

        var relations = MapReader.Instance.relations;
        if (relations != null)
        {
            foreach (var relation in relations)
            {
                if (relation.objectType == BaseOsm.ObjectType.Water && relation.NodeIDs.Count > 1)
                {
                    relation.AddField("source_type", "relation");
                    CreateWaterss(relation);

                    processedInBatch++;
                    if (processedInBatch >= batchSize)
                    {
                        processedInBatch = 0;
                        yield return null;
                    }
                }
            }
        }

        float endtime = Time.time;

        Debug.Log("Waters create at: " + (endtime - starttime) + " | Total: " + m_countProcessing);

        isFinished = true;
    }

    // Обработчик событий
    private void OnGeoObjectLoaded(BaseOsm geo)
    {
        // Фильтрация: обрабатываем только Water
        if (geo.objectType != BaseOsm.ObjectType.Water) return;

        // Проверка количества нод
        if (geo.NodeIDs.Count <= 1) return;

        StartCoroutine(ProcessWaterCoroutine(geo));
    }

    private IEnumerator ProcessWaterCoroutine(BaseOsm geo)
    {
        CreateWaterss(geo);
        yield return null;
    }

    private void OnDestroy()
    {
        if (MapReader.Instance != null)
        {
            MapReader.Instance.OnWayLoaded -= OnGeoObjectLoaded;
            MapReader.Instance.OnRelationLoaded -= OnGeoObjectLoaded;
        }
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }
}
