using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static GR;

class LanduseMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
    public bool bNotCreateNotClosedPolygon;

    public Material grassMaterial;
    public LanduseTypes landuseTypes;
    public bool isCreateColision = false;
    public int MaxNodes = 150;
    public TileSystem tileSystem;

    private int m_countProcessing = 0;
    // —писок дл€ отслеживани€ уже обработанных ID
    private HashSet<ulong> processedIDs = new HashSet<ulong>();

    // ============================================
    // ќѕ“»ћ»«ј÷»я: batchSize дл€ пакетной обработки
    // ============================================
    [Header("Optimization Settings")]
    [Tooltip(" оличество landuse объектов обрабатываемых за один кадр")]
    public int batchSize = 10;

    // ============================================
    // ќѕ“»ћ»«ј÷»я: Object Pooling дл€ MeshData
    // ============================================
    private Stack<MeshData> meshDataPool = new Stack<MeshData>();

    // ============================================
    // ќѕ“»ћ»«ј÷»я:  эширование ссылок
    // ============================================
    private Dictionary<ulong, OsmNode> cachedNodes;
    private Vector3 cachedWorldOrigin;

    private void SetProperties(BaseOsm geo, Landuse landuse)
    {
        landuse.name = "landuse " + geo.ID.ToString();

        if (geo.HasField("name"))
            landuse.Name = geo.GetValueStringByKey("name");

        landuse.Id = geo.ID.ToString();

        if (geo.HasField("source_type"))
            landuse.Source = geo.GetValueStringByKey("source_type");

        var kind = "";

        if (geo.HasField("natural"))
        {
            kind = geo.GetValueStringByKey("natural");
        }
        else if (geo.HasField("leisure"))
        {
            kind = geo.GetValueStringByKey("leisure");
        }
        else if (geo.HasField("landuse"))
        {
            kind = geo.GetValueStringByKey("landuse");
        }
        else if (geo.HasField("amenity"))
        {
            kind = geo.GetValueStringByKey("amenity");
        }
        else if (geo.HasField("boundary"))
        {
            kind = geo.GetValueStringByKey("boundary");
        }
        else if (geo.HasField("fire_boundary"))
        {
            kind = "fire_boundary";
        }
        else
        {
            kind = "yes";
        }

        if (geo.HasField("garden:style"))
        {
            var garden_style = geo.GetValueStringByKey("garden:style");

            if (garden_style == "flower_garden")
            {
                kind = "flowerbed";
            }
        }

        landuse.Kind = kind;

        var landuseInfo = landuseTypes.GetLanduseTypeInfoByName(landuse.Kind);

        if (geo.HasField("source_type"))
        {
            landuse.Source = geo.GetValueStringByKey("source_type");
        }

        if (geo.HasField("layer"))
        {
            landuse.layer = geo.GetValueIntByKey("layer");
        }

        landuse.isEnableRender = landuseInfo.isRenderEnable;
        landuse.isGrassGenerate = landuseInfo.isGrassGenerate;
        landuse.isTreesGenerate = landuseInfo.isTreesGenerate;
        landuse.isFlatUV = landuseInfo.isFlatUV;
        landuse.fHeightLayer = landuseInfo.fHeightLayer;
        landuse.grassTypes = landuseInfo.grassTypes;

        // ќѕ“»ћ»«ј÷»я:  эшируем MeshRenderer
        var meshRenderer = landuse.GetComponent<MeshRenderer>();

        if (landuseInfo.groundMaterial != null)
        {
            meshRenderer.material = landuseInfo.groundMaterial;
        }
        else
        {
            meshRenderer.material = grassMaterial;
        }
    }

    // ============================================
    // ќѕ“»ћ»«ј÷»я: Object Pooling дл€ MeshData
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

    // ¬спомогательный метод дл€ безопасного получени€ центра
    private Vector3 GetCentre(BaseOsm geo)
    {
        Vector3 total = Vector3.zero;
        int count = 0;

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированную ссылку на nodes
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

    void CreateLanduse(BaseOsm geo)
    {
        // «ащита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        var searchname = "landuse " + geo.ID.ToString();

        m_countProcessing++;

        if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        if (!geo.IsClosedPolygon && bNotCreateNotClosedPolygon)
        {
            return;
        }

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var landuse = new GameObject(searchname).AddComponent<Landuse>();

        landuse.AddComponent<MeshFilter>();
        landuse.AddComponent<MeshRenderer>();

        landuse.itemlist = geo.itemlist;

        SetProperties(geo, landuse);

        // ќѕ“»ћ»«ј÷»я: ѕредварительное выделение пам€ти дл€ списка
        var landuseCorners = new List<Vector3>(count);

        Vector3 localOrigin = GetCentre(geo);

        // »«ћ≈Ќ≈Ќ»≈: »спользуем кэшированный WorldOrigin
        landuse.transform.position = localOrigin - cachedWorldOrigin;

        landuse.transform.position += Vector3.up * (landuse.layer * BaseDataObject.layer_size);

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированную ссылку на nodes
        var nodes = cachedNodes ?? MapReader.Instance.nodes;

        for (int i = 0; i < count; i++)
        {
            // »«ћ≈Ќ≈Ќ»≈: Ѕезопасный доступ к нодам
            if (nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 coords = point - localOrigin;
                landuseCorners.Add(coords);
            }
        }

        var holesCorners = new List<List<Vector3>>();

        var countHoles = geo.HolesNodeListsIDs.Count;

        for (int i = 0; i < countHoles; i++)
        {
            var holeNodes = geo.HolesNodeListsIDs[i];
            var countHoleContourPoints = holeNodes.Count;
            // ќѕ“»ћ»«ј÷»я: ѕредварительное выделение пам€ти
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

        var mesh = landuse.GetComponent<MeshFilter>().mesh;

        // ќѕ“»ћ»«ј÷»я: »спользуем пул дл€ MeshData
        var tb = GetMeshData();

        if (landuse.isEnableRender)
        {
            GR.CreateMeshWithHeight(landuseCorners, 0.0f, 0.00001f, tb, holesCorners, landuse.isFlatUV);
        }

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.normals = tb.Normals.ToArray();
        mesh.SetUVs(0, tb.UV);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();

        // ќѕ“»ћ»«ј÷»я: ¬озвращаем MeshData в пул
        ReturnMeshData(tb);

        if (isCreateColision)
        {
            landuse.transform.gameObject.AddComponent<MeshCollider>();
            landuse.transform.GetComponent<MeshCollider>().sharedMesh = landuse.GetComponent<MeshFilter>().mesh;
            landuse.transform.GetComponent<MeshCollider>().convex = false;
        }

        landuse.transform.position += Vector3.up * landuse.fHeightLayer;

        //  орректировка под Terrain
        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                var settings = new TessellationSettings
                {
                    maxEdgeLength = 0.001f,
                    heightSensitivity = 0.001f,
                    maxVertexCount = 50000,
                    heightfix = 0.1f
                };
                StartCoroutine(AdjustMeshToTerrainCorutine(landuse.gameObject, settings));
            }
        }

        landuse.Activate();
    }

    IEnumerator Start()
    {
        // ∆дем готовности MapReader
        while (MapReader.Instance == null || !MapReader.Instance.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();
        tileSystem = FindObjectOfType<TileSystem>();

        // ќѕ“»ћ»«ј÷»я:  эшируем ссылки один раз при старте
        cachedNodes = MapReader.Instance.nodes;
        cachedWorldOrigin = MapReader.Instance.WorldOrigin;

        // 1. ѕодписываемс€ на новые событи€
        MapReader.Instance.OnWayLoaded += OnGeoObjectLoaded;
        MapReader.Instance.OnRelationLoaded += OnGeoObjectLoaded;

        float starttime = Time.time;

        // ============================================
        // ќѕ“»ћ»«ј÷»я: ѕакетна€ обработка объектов
        // ============================================
        int processedInBatch = 0;

        // 2. ќбрабатываем уже загруженные данные
        var ways = MapReader.Instance.ways;
        if (ways != null)
        {
            foreach (var way in ways)
            {
                if (way.objectType == BaseOsm.ObjectType.Landuse && way.NodeIDs.Count > 1)
                {
                    way.AddField("source_type", "way");
                    CreateLanduse(way);

                    processedInBatch++;
                    if (processedInBatch >= batchSize)
                    {
                        processedInBatch = 0;
                        yield return null; // ѕауза только после обработки batchSize объектов
                    }
                }
            }
        }

        var relations = MapReader.Instance.relations;
        if (relations != null)
        {
            foreach (var relation in relations)
            {
                if (relation.objectType == BaseOsm.ObjectType.Landuse && relation.NodeIDs.Count > 1)
                {
                    relation.AddField("source_type", "relation");
                    CreateLanduse(relation);

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

        Debug.Log("Landuses create at: " + (endtime - starttime) + " | Total: " + m_countProcessing);

        isFinished = true;
    }

    // ќбработчик событий
    private void OnGeoObjectLoaded(BaseOsm geo)
    {
        // ‘ильтраци€: обрабатываем только Landuse
        if (geo.objectType != BaseOsm.ObjectType.Landuse) return;

        // ѕроверка количества нод
        if (geo.NodeIDs.Count <= 1) return;

        StartCoroutine(ProcessLanduseCoroutine(geo));
    }

    private IEnumerator ProcessLanduseCoroutine(BaseOsm geo)
    {
        CreateLanduse(geo);
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
