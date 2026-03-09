using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static GR;

class ManMadeMaker : InfrstructureBehaviour
{
    public Material manade_material;

    public GenerateRoof generateRoof;

    public static GameContentSelector contentselector;

    public ManMadeTypes manmadeTypes;

    public bool isUseOldTriangulation = false;
    public bool isCreateColision = false;

    public int MaxNodes = 150;

    private int m_countProcessing = 0;
    // —писок дл€ отслеживани€ уже обработанных ID
    private HashSet<ulong> processedIDs = new HashSet<ulong>();

    public TileSystem tileSystem;

    public GameObject smokeprefab;

    public bool isDebugNotFoundMaterials = false;

    // ============================================
    // ќѕ“»ћ»«ј÷»я: batchSize дл€ пакетной обработки
    // ============================================
    [Header("Optimization Settings")]
    [Tooltip(" оличество объектов обрабатываемых за один кадр")]
    public int batchSize = 10;

    // ============================================
    // ќѕ“»ћ»«ј÷»я:  эширование материалов
    // ============================================
    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

    // ============================================
    // ќѕ“»ћ»«ј÷»я: Object Pooling дл€ MeshData
    // ============================================
    private Stack<MeshData> meshDataPool = new Stack<MeshData>();
    private List<MeshData> usedMeshData = new List<MeshData>();

    // ============================================
    // ќѕ“»ћ»«ј÷»я:  эширование ссылки на nodes
    // ============================================
    private Dictionary<ulong, OsmNode> cachedNodes;

    IEnumerator Start()
    {
        // ∆дем готовности MapReader
        while (MapReader.Instance == null || !MapReader.Instance.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();
        tileSystem = FindObjectOfType<TileSystem>();

        // ќѕ“»ћ»«ј÷»я:  эшируем ссылку на nodes один раз
        cachedNodes = MapReader.Instance.nodes;

        // 1. ѕодписываемс€ на новые событи€
        MapReader.Instance.OnWayLoaded += OnGeoObjectLoaded;
        MapReader.Instance.OnRelationLoaded += OnGeoObjectLoaded;

        float starttime = Time.time;

        // ============================================
        // ќѕ“»ћ»«ј÷»я: ѕакетна€ обработка объектов
        // ============================================
        int processedInBatch = 0;

        // 2. ќбрабатываем уже загруженные данные (первоначальна€ загрузка)
        var ways = MapReader.Instance.ways;
        if (ways != null)
        {
            foreach (var way in ways)
            {
                if (way.objectType == BaseOsm.ObjectType.ManMade && way.NodeIDs.Count > 1)
                {
                    way.AddField("source_type", "way");

                    if (way.IsClosedPolygon)
                    {
                        CreateManMadePolygon(way);
                    }
                    else
                    {
                        CreateManMadeLine(way);
                    }

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
                if (relation.objectType == BaseOsm.ObjectType.ManMade && relation.NodeIDs.Count > 1)
                {
                    relation.AddField("source_type", "relation");

                    if (relation.IsClosedPolygon)
                    {
                        CreateManMadePolygon(relation);
                    }
                    else
                    {
                        CreateManMadeLine(relation);
                    }

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

        Debug.Log("Manmade's create at: " + (endtime - starttime) + " | Total: " + m_countProcessing);

        isFinished = true;
    }

    // ќбработчик событий
    private void OnGeoObjectLoaded(BaseOsm geo)
    {
        // ‘ильтраци€: обрабатываем только ManMade
        if (geo.objectType != BaseOsm.ObjectType.ManMade) return;

        // ѕроверка количества нод
        if (geo.NodeIDs.Count <= 1) return;

        StartCoroutine(ProcessManMadeCoroutine(geo));
    }

    private IEnumerator ProcessManMadeCoroutine(BaseOsm geo)
    {
        if (geo.IsClosedPolygon)
        {
            CreateManMadePolygon(geo);
        }
        else
        {
            CreateManMadeLine(geo);
        }
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

    private float GetHeights(BaseOsm geo, ManMadeObj manmade)
    {
        float height;

        if (geo.HasField("height"))
        {
            height = geo.GetValueFloatByKey("height");
        }
        else if (geo.HasField("building:height"))
        {
            height = geo.GetValueFloatByKey("building:height");
        }
        else if (geo.HasField("building:levels"))
        {
            height = geo.GetValueFloatByKey("building:levels") * 3.0f;
        }
        else
        {
            var man_made_type = geo.GetValueStringByKey("man_made");

            if (man_made_type == "tower")
            {
                height = 100.0f;
            }
            else if (man_made_type == "reservoir_covered")
            {
                height = 10;
            }
            else if (man_made_type == "chimney")
            {
                height = 100.0f;
            }
            else if (man_made_type == "silo")
            {
                height = 1.75f;
            }
            else
            {
                height = 1.5f;
            }
        }

        return height;
    }

    private float GetMinHeight(BaseOsm geo)
    {
        var min_height = 0.0f;

        if (geo.HasField("min_height"))
        {
            min_height = geo.GetValueFloatByKey("min_height");
        }
        else if (geo.HasField("building:min_level"))
        {
            min_height = geo.GetValueFloatByKey("building:min_level") * 3.0f;
        }

        return min_height;
    }

    private void SetProperties(BaseOsm geo, ManMadeObj manmade, bool isPolygon)
    {
        if (isPolygon)
        {
            manmade.name = "manmade_polygon " + geo.ID.ToString();
        }
        else
        {
            manmade.name = "manmade_line " + geo.ID.ToString();
        }

        if (geo.HasField("name"))
            manmade.Name = geo.GetValueStringByKey("name");

        manmade.Id = geo.ID.ToString();

        string kind;

        if (geo.HasField("man_made"))
        {
            kind = geo.GetValueStringByKey("man_made");
        }
        else
        {
            kind = "yes";
        }

        manmade.Kind = kind;

        manmade.width = 2.0f;

        manmade.curSettings = manmadeTypes.GetTypeInfoByName(manmade.Kind);

        if (geo.HasField("source_type"))
            manmade.Source = geo.GetValueStringByKey("source_type");

        Material mat_by_type = null;

        if (!kind.Equals("yes"))
        {
            mat_by_type = manmade.curSettings.defaultMaterial;
        }

        if (geo.HasField("location"))
        {
            var location = geo.GetValueStringByKey("location");

            if (location == "overground")
            {
                manmade.layer = 1;
            }
            else if (location == "surface")
            {
                manmade.layer = 0;
            }
            else if (location == "underground")
            {
                manmade.layer = -1;
            }
            else if (geo.HasField("layer"))
            {
                manmade.layer = geo.GetValueIntByKey("layer");
            }
        }
        else if (geo.HasField("layer"))
        {
            manmade.layer = geo.GetValueIntByKey("layer");
        }

        // ќѕ“»ћ»«ј÷»я:  эшируем MeshRenderer
        var meshRenderer = manmade.GetComponent<MeshRenderer>();

        if (mat_by_type != null)
        {
            meshRenderer.material = mat_by_type;
        }
        else
        {
            meshRenderer.material = manade_material;
        }

        if (geo.HasField("building:material") && isDebugNotFoundMaterials)
        {
            var mat_name = geo.GetValueStringByKey("building:material");
            Debug.Log("Can' found building:material: " + mat_name + " for manmade");
        }

        if (geo.HasField("material") && isDebugNotFoundMaterials)
        {
            var mat_name = geo.GetValueStringByKey("material");
            Debug.Log("Can' found material: " + mat_name + " for manmade");
        }

        if (geo.HasField("surface") && isDebugNotFoundMaterials)
        {
            var mat_name = geo.GetValueStringByKey("surface");
            Debug.Log("Can' found surface: " + mat_name + " for manmade");
        }

        Color curColor = GR.SetOSMColour(geo);

        if (curColor != Color.white)
        {
            meshRenderer.material.SetColor("_Color", curColor);
            meshRenderer.material.SetColor("_BaseColor", curColor);
        }
    }

    void CreateManMadePolygon(BaseOsm geo)
    {
        // «ащита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        var searchname = "manmade_polygon " + geo.ID.ToString();

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

        var manmade = new GameObject(searchname).AddComponent<ManMadeObj>();

        manmade.AddComponent<MeshFilter>();
        manmade.AddComponent<MeshRenderer>();

        manmade.itemlist = geo.itemlist;
        manmade.count = count;

        SetProperties(geo, manmade, true);

        var height = GetHeights(geo, manmade);
        var minHeight = GetMinHeight(geo);

        manmade.height = height;
        manmade.min_height = minHeight;

        // ќѕ“»ћ»«ј÷»я: ѕредварительное выделение пам€ти дл€ списка
        var manmadeCorners = new List<Vector3>(count);

        float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;

        Vector3 localOrigin = GetCentre(geo);

        // »«ћ≈Ќ≈Ќ»≈: »спользуем WorldOrigin
        manmade.transform.position = localOrigin - MapReader.Instance.WorldOrigin;

        manmade.transform.position += Vector3.up * (manmade.layer * BaseDataObject.layer_size);

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированную ссылку на nodes
        var nodes = cachedNodes ?? MapReader.Instance.nodes;

        for (int i = 0; i < count; i++)
        {
            // »«ћ≈Ќ≈Ќ»≈: Ѕезопасный доступ
            if (nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 coords = point - localOrigin;

                if (coords.x < minx) minx = (float)coords.x;
                if (coords.z < miny) miny = (float)coords.z;
                if (coords.x > maxx) maxx = (float)coords.x;
                if (coords.z > maxy) maxy = (float)coords.z;

                manmadeCorners.Add(coords);
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

        var mesh = manmade.GetComponent<MeshFilter>().mesh;

        // ќѕ“»ћ»«ј÷»я: »спользуем пул дл€ MeshData
        var tb = GetMeshData();

        GR.CreateMeshWithHeight(manmadeCorners, minHeight, height, tb, holesCorners);

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
            manmade.transform.gameObject.AddComponent<MeshCollider>();
            manmade.transform.GetComponent<MeshCollider>().sharedMesh = manmade.GetComponent<MeshFilter>().mesh;
            manmade.transform.GetComponent<MeshCollider>().convex = false;
        }

        var man_made_type = geo.GetValueStringByKey("man_made");

        if (man_made_type.Equals("chimney"))
        {
            var go = Instantiate(smokeprefab, manmade.transform.position + (Vector3.up * (manmade.height - 0.20f)), Quaternion.identity);
            go.transform.localScale = new Vector3(25.0f, 25.0f, 25.0f);
            go.transform.Rotate(new Vector3(-90, 0, 0));
            go.transform.SetParent(manmade.transform);
        }
        else if (man_made_type.Equals("bridge"))
        {
            manmade.transform.position = new Vector3(manmade.transform.position.x, manmade.transform.position.y - manmade.height, manmade.transform.position.z);
        }

        bool isGenerateRoof = true;

        if (isGenerateRoof)
        {
            if (contentselector != null && contentselector.isRoofDisabled(geo.ID))
            {
                isGenerateRoof = false;
            }
            else
            {
                if (!geo.HasField("roof:shape") && !geo.HasField("roof:colour") && !geo.HasField("roof:height"))
                {
                    isGenerateRoof = false;
                }
            }
        }

        if (isGenerateRoof)
        {
            generateRoof.GenerateRoofForObject(manmade, manmadeCorners, holesCorners, minHeight, height, new Vector2(minx, miny), new Vector2(maxx - minx, maxy - miny), geo, isUseOldTriangulation);
        }

        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                StartCoroutine(SpawnInHeight(manmade.gameObject, AlgorithmHeightSorting.AverageHeight));
            }
        }
    }

    void CreateManMadeLine(BaseOsm geo)
    {
        // «ащита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        var searchname = "manmade_line " + geo.ID.ToString();

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

        var manmade = new GameObject(searchname).AddComponent<ManMadeObj>();

        manmade.AddComponent<MeshFilter>();
        manmade.AddComponent<MeshRenderer>();

        manmade.itemlist = geo.itemlist;
        manmade.count = count;

        SetProperties(geo, manmade, false);

        var height = GetHeights(geo, manmade);
        var minHeight = GetMinHeight(geo);

        manmade.height = height;
        manmade.min_height = minHeight;

        // ќѕ“»ћ»«ј÷»я: ѕредварительное выделение пам€ти дл€ списка
        var manmadeCorners = new List<Vector3>(count);

        Vector3 localOrigin = GetCentre(geo);

        // »«ћ≈Ќ≈Ќ»≈: »спользуем WorldOrigin
        manmade.transform.position = localOrigin - MapReader.Instance.WorldOrigin;

        manmade.transform.position += Vector3.up * (manmade.layer * BaseDataObject.layer_size);

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированную ссылку на nodes
        var nodes = cachedNodes ?? MapReader.Instance.nodes;

        for (int i = 0; i < count; i++)
        {
            // »«ћ≈Ќ≈Ќ»≈: Ѕезопасный доступ
            if (nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 coords = point - localOrigin;
                manmadeCorners.Add(coords);
            }
        }

        var mesh = manmade.GetComponent<MeshFilter>().mesh;

        // ќѕ“»ћ»«ј÷»я: »спользуем пул дл€ MeshData
        var tb = GetMeshData();

        GR.CreateMeshLineWithWidthAndHeight(manmadeCorners, manmade.height, manmade.min_height, manmade.width, tb);

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
            manmade.transform.gameObject.AddComponent<MeshCollider>();
            manmade.transform.GetComponent<MeshCollider>().sharedMesh = manmade.GetComponent<MeshFilter>().mesh;
            manmade.transform.GetComponent<MeshCollider>().convex = false;
        }

        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                StartCoroutine(SpawnInHeight(manmade.gameObject, AlgorithmHeightSorting.AverageHeight));
            }
        }
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }
}
