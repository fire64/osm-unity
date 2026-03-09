using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using static GR;

class OthersMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
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
    [Tooltip(" оличество other объектов обрабатываемых за один кадр")]
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

    public enum other_type
    {
        notset = 0,
        power_wire,
    };

    private void SetProperties(BaseOsm geo, Undefined other)
    {
        other.name = "other " + geo.ID.ToString();

        if (geo.HasField("name"))
            other.Name = geo.GetValueStringByKey("name");

        other.Id = geo.ID.ToString();

        other.Kind = "other";

        if (geo.HasField("layer"))
        {
            other.layer = geo.GetValueIntByKey("layer");
        }

        if (geo.HasField("source_type"))
            other.Source = geo.GetValueStringByKey("source_type");

        other.isClosed = geo.IsClosedPolygon;
    }

    private other_type GetOtherType(BaseOsm geo)
    {
        other_type cur_type = other_type.notset;

        if (geo.HasField("power") && geo.GetValueStringByKey("power").Equals("line"))
        {
            cur_type = other_type.power_wire;
        }

        return cur_type;
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

    void CreateOtherObject(BaseOsm geo)
    {
        // «ащита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        var searchname = "other " + geo.ID.ToString();

        m_countProcessing++;

        // ќѕ“»ћ»«ј÷»я: Ѕезопасна€ проверка contentselector
        if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        other_type cur_type = GetOtherType(geo);

        if (cur_type == other_type.notset)
        {
            return;
        }

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var other = new GameObject(searchname).AddComponent<Undefined>();

        other.itemlist = geo.itemlist;

        SetProperties(geo, other);

        Vector3 localOrigin = GetCentre(geo);

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированный WorldOrigin
        other.transform.position = localOrigin - cachedWorldOrigin;

        other.transform.position += Vector3.up * (other.layer * BaseDataObject.layer_size);
        other.AddComponent<MeshFilter>();
        other.AddComponent<MeshRenderer>();

        // ќѕ“»ћ»«ј÷»я: ѕредварительное выделение пам€ти дл€ списка
        var otherCorners = new List<Vector3>(count);

        var countContour = geo.NodeIDs.Count;

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированную ссылку на nodes
        var nodes = cachedNodes ?? MapReader.Instance.nodes;

        for (int i = 0; i < countContour; i++)
        {
            // »«ћ≈Ќ≈Ќ»≈: Ѕезопасный доступ к нодам
            if (nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 coords = point - localOrigin;
                otherCorners.Add(coords);
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
                // »—ѕ–ј¬Ћ≈Ќ»≈: Ѕезопасный доступ к нодам (было map.nodes без проверки)
                if (nodes.TryGetValue(holeNodes[j], out OsmNode point))
                {
                    Vector3 coords = point - localOrigin;
                    holeContour.Add(coords);
                }
            }

            holesCorners.Add(holeContour);
        }

        var mesh = other.GetComponent<MeshFilter>().mesh;

        // ќѕ“»ћ»«ј÷»я: »спользуем пул дл€ MeshData
        var tb = GetMeshData();

        if (cur_type == other_type.power_wire)
        {
            // ќѕ“»ћ»«ј÷»я:  эшируем MeshRenderer
            var meshRenderer = other.GetComponent<MeshRenderer>();
            meshRenderer.material.SetColor("_Color", Color.gray);
            meshRenderer.material.SetColor("_BaseColor", Color.gray);
            GR.CreateMeshLineWithWidthAndHeight(otherCorners, 9.9f, 10.0f, 0.1f, tb);
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

        // Add colider
        if (isCreateColision)
        {
            other.transform.gameObject.AddComponent<MeshCollider>();
            other.transform.GetComponent<MeshCollider>().sharedMesh = other.GetComponent<MeshFilter>().mesh;
            other.transform.GetComponent<MeshCollider>().convex = false;
        }

        // ќѕ“»ћ»«ј÷»я: Ѕезопасна€ проверка tileSystem
        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                StartCoroutine(SpawnInHeight(other.gameObject, AlgorithmHeightSorting.AverageHeight));
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
                if (way.objectType == BaseOsm.ObjectType.Undefined && way.NodeIDs.Count > 1)
                {
                    way.AddField("source_type", "way");
                    CreateOtherObject(way);

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
                if (relation.objectType == BaseOsm.ObjectType.Undefined && relation.NodeIDs.Count > 1)
                {
                    relation.AddField("source_type", "relation");
                    CreateOtherObject(relation);

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

        Debug.Log("Others create at: " + (endtime - starttime) + " | Total: " + m_countProcessing);

        isFinished = true;
    }

    // ќбработчик событий
    private void OnGeoObjectLoaded(BaseOsm geo)
    {
        // ‘ильтраци€: обрабатываем только Undefined
        if (geo.objectType != BaseOsm.ObjectType.Undefined) return;

        // ѕроверка количества нод
        if (geo.NodeIDs.Count <= 1) return;

        StartCoroutine(ProcessOtherCoroutine(geo));
    }

    private IEnumerator ProcessOtherCoroutine(BaseOsm geo)
    {
        CreateOtherObject(geo);
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
