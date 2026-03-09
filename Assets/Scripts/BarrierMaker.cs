using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static GR;

class BarrierMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
    public BarriersTypes barrierTypes;
    public BarriersMaterials barrierMaterials;
    public bool isCreateColision = false;
    public int MaxNodes = 150;
    public TileSystem tileSystem;

    public bool isDebugNotFoundMaterials = false;

    private int m_countProcessing = 0;
    // —писок дл€ отслеживани€ уже обработанных ID
    private HashSet<ulong> processedIDs = new HashSet<ulong>();

    // ============================================
    // ќѕ“»ћ»«ј÷»я: batchSize дл€ пакетной обработки
    // ============================================
    [Header("Optimization Settings")]
    [Tooltip(" оличество барьеров обрабатываемых за один кадр")]
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

    // ============================================
    // ќѕ“»ћ»«ј÷»я:  эширование материалов
    // ============================================
    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

    private void SetProperties(BaseOsm geo, Barrier barrier)
    {
        barrier.name = "barrier " + geo.ID.ToString();

        if (geo.HasField("name"))
            barrier.Name = geo.GetValueStringByKey("name");

        barrier.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("barrier"))
        {
            kind = geo.GetValueStringByKey("barrier");
        }
        else
        {
            kind = "yes";
        }

        barrier.Kind = kind;

        if (geo.HasField("source_type"))
            barrier.Source = geo.GetValueStringByKey("source_type");

        var barrierInfo = barrierTypes.GetBarrierTypeInfoByName(barrier.Kind);

        if (geo.HasField("height"))
        {
            barrier.height = geo.GetValueFloatByKey("height");
        }
        else
        {
            barrier.height = barrierInfo.barrierHeight;
        }

        if (geo.HasField("min_height"))
        {
            barrier.min_height = geo.GetValueFloatByKey("min_height");
        }
        else
        {
            barrier.min_height = 0.0f;
        }

        if (geo.HasField("layer"))
        {
            barrier.layer = geo.GetValueIntByKey("layer");
        }

        barrier.width = barrierInfo.barrierWidth;

        var materal_type = barrier.Kind;

        if (geo.HasField("fence_type"))
        {
            barrier.fence_type = geo.GetValueStringByKey("fence_type");
            materal_type = materal_type + "_" + barrier.fence_type;
        }

        if (geo.HasField("material"))
        {
            barrier.material = geo.GetValueStringByKey("material");
            materal_type = materal_type + "_" + barrier.material;
        }

        if (geo.HasField("surface") && isDebugNotFoundMaterials)
        {
            var mat_name = geo.GetValueStringByKey("surface");
            Debug.Log("Can' found surface: " + mat_name + " for barrier");
        }

        // ќѕ“»ћ»«ј÷»я:  эширование материала
        Material barrier_mat = GetCachedMaterial(materal_type);

        // ќѕ“»ћ»«ј÷»я:  эшируем MeshRenderer
        var meshRenderer = barrier.GetComponent<MeshRenderer>();

        if (barrier_mat != null)
        {
            meshRenderer.material = barrier_mat;
        }
        else
        {
            if (geo.HasField("material") && isDebugNotFoundMaterials)
            {
                Debug.Log("Can' found material: " + materal_type + " for barrier");
            }

            meshRenderer.material = barrierInfo.barrierMaterial;
        }

        Color set_color = GR.SetOSMColour(geo);

        meshRenderer.material.SetColor("_Color", set_color);
        meshRenderer.material.SetColor("_BaseColor", set_color);
    }

    // ============================================
    // ќѕ“»ћ»«ј÷»я: ѕолучение материала с кэшированием
    // ============================================
    private Material GetCachedMaterial(string materialName)
    {
        if (string.IsNullOrEmpty(materialName)) return null;

        if (materialCache.TryGetValue(materialName, out Material cached))
            return cached;

        Material mat = barrierMaterials.GetBarrierMaterialByName(materialName);
        materialCache[materialName] = mat;
        return mat;
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

    void CreateBarriers(BaseOsm geo)
    {
        // «ащита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        var searchname = "barrier " + geo.ID.ToString();

        m_countProcessing++;

        if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var barrier = new GameObject(searchname).AddComponent<Barrier>();

        barrier.AddComponent<MeshFilter>();
        barrier.AddComponent<MeshRenderer>();

        barrier.itemlist = geo.itemlist;

        SetProperties(geo, barrier);

        // ќѕ“»ћ»«ј÷»я: ѕредварительное выделение пам€ти дл€ списка
        var barrierCorners = new List<Vector3>(count);

        Vector3 localOrigin = GetCentre(geo);

        // »«ћ≈Ќ≈Ќ»≈: »спользуем кэшированный WorldOrigin
        barrier.transform.position = localOrigin - cachedWorldOrigin;

        barrier.transform.position += Vector3.up * (barrier.layer * BaseDataObject.layer_size);

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированную ссылку на nodes
        var nodes = cachedNodes ?? MapReader.Instance.nodes;

        for (int i = 0; i < count; i++)
        {
            // »«ћ≈Ќ≈Ќ»≈: Ѕезопасный доступ к нодам
            if (nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 coords = point - localOrigin;
                barrierCorners.Add(coords);
            }
        }

        var mesh = barrier.GetComponent<MeshFilter>().mesh;

        // ќѕ“»ћ»«ј÷»я: »спользуем пул дл€ MeshData
        var tb = GetMeshData();

        GR.CreateMeshLineWithWidthAndHeight(barrierCorners, barrier.height, barrier.min_height, barrier.width, tb);

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
            barrier.transform.gameObject.AddComponent<MeshCollider>();
            barrier.transform.GetComponent<MeshCollider>().sharedMesh = barrier.GetComponent<MeshFilter>().mesh;
            barrier.transform.GetComponent<MeshCollider>().convex = false;
        }

        //  орректировка под Terrain
        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                StartCoroutine(SpawnInHeight(barrier.gameObject, AlgorithmHeightSorting.MinimumHeight));
            }
        }
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
        MapReader .Instance.OnWayLoaded += OnGeoObjectLoaded;
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
                if (way.IsBarrier)
                {
                    way.AddField("source_type", "way");
                    CreateBarriers(way);

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
                if (relation.IsBarrier)
                {
                    relation.AddField("source_type", "relation");
                    CreateBarriers(relation);

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

        Debug.Log("Barriers create at: " + (endtime - starttime) + " | Total: " + m_countProcessing);

        isFinished = true;
    }

    // ќбработчик событий
    private void OnGeoObjectLoaded(BaseOsm geo)
    {
        // ‘ильтраци€: обрабатываем только барьеры
        if (!geo.IsBarrier) return;

        StartCoroutine(ProcessBarrierCoroutine(geo));
    }

    private IEnumerator ProcessBarrierCoroutine(BaseOsm geo)
    {
        CreateBarriers(geo);
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
