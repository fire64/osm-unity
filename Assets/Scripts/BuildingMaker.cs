using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static GR;

public class BuildingMaker : InfrstructureBehaviour
{
    public Material building_material;
    public bool bNotCreateOSMParts;

    public float MinRandHeight = 3.0f;
    public float MaxRandHeight = 21.0f;

    public GenerateRoof generateRoof;

    public static GameContentSelector contentselector;

    public BuildingTypes buildingTypes;
    public BuildingMaterials buildingMaterials;
    public BuildingSeries buildingSeries;
    public bool bNotCreateNotClosedPolygon;

    public bool isUseOldTriangulation = false;
    public bool isCreateColision = false;

    public int MaxNodes = 150;

    public TileSystem tileSystem;

    public GameObject smokeprefab;
    public Material windowMaterialLit;
    public Material windowMaterialDark;
    public float tolerance = 0.1f;

    public bool isDebugNotFoundMaterials = false;

    public bool bCreateWindows = false;
    public bool bCreateAdresses = false;
    public bool bCreateRoofs = false;

    // ============================================
    // ОПТИМИЗАЦИЯ: batchSize для пакетной обработки
    // ============================================
    [Header("Optimization Settings")]
    [Tooltip("Количество зданий обрабатываемых за один кадр")]
    public int batchSize = 10;

    private int m_countProcessing = 0;
    private HashSet<ulong> processedIDs = new HashSet<ulong>();

    // ============================================
    // ОПТИМИЗАЦИЯ: Кэширование материалов
    // ============================================
    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

    // ============================================
    // ОПТИМИЗАЦИЯ: Object Pooling для MeshData
    // ============================================
    private Stack<MeshData> meshDataPool = new Stack<MeshData>();
    private List<MeshData> usedMeshData = new List<MeshData>();

    // ============================================
    // ОПТИМИЗАЦИЯ: Кэширование центров зданий
    // ============================================
    private Dictionary<ulong, Vector3> centerCache = new Dictionary<ulong, Vector3>();

    private float GetHeights(BaseOsm geo, Building building)
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
        else if (geo.HasField("building:max_level"))
        {
            height = geo.GetValueFloatByKey("building:max_level") * 3.0f;
        }
        else if (geo.HasField("max_level"))
        {
            height = geo.GetValueFloatByKey("max_level") * 3.0f;
        }
        else if (geo.HasField("building:levels"))
        {
            height = geo.GetValueFloatByKey("building:levels") * 3.0f;
        }
        else if (geo.HasField("man_made"))
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
        else if (geo.HasField("pipeline") && geo.GetValueStringByKey("pipeline") == "substation")
        {
            height = 2.0f;
        }
        else if (geo.HasField("power") && geo.GetValueStringByKey("power") == "substation")
        {
            height = 2.0f;
        }
        else if (building.curSettings.defaultHeight > 0.0f)
        {
            height = building.curSettings.defaultHeight;
        }
        else
        {
            height = UnityEngine.Random.Range(MinRandHeight, MaxRandHeight);
        }

        if (geo.GetValueStringByKey("kind") == "pier")
        {
            height = 0.1f;
        }
        else if (geo.GetValueStringByKey("kind") == "bridge")
        {
            height = 0.1f;
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
        else if (geo.HasField("min_level"))
        {
            min_height = geo.GetValueFloatByKey("min_level") * 3.0f;
        }

        return min_height;
    }

    // ============================================
    // ОПТИМИЗАЦИЯ: Получение материала с кэшированием
    // ============================================
    private Material GetCachedMaterial(string materialName)
    {
        if (string.IsNullOrEmpty(materialName)) return null;

        if (materialCache.TryGetValue(materialName, out Material cached))
            return cached;

        Material mat = buildingMaterials.GetBuildingMaterialByName(materialName);
        materialCache[materialName] = mat;
        return mat;
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

    private void SetProperties(BaseOsm geo, Building building)
    {
        building.name = "building " + geo.ID.ToString();

        if (geo.HasField("name"))
            building.Name = geo.GetValueStringByKey("name");

        building.Id = geo.ID.ToString();

        string kind;

        if (geo.HasField("building"))
        {
            kind = geo.GetValueStringByKey("building");
        }
        else
        {
            kind = "yes";
        }

        building.Kind = kind;

        building.curSettings = buildingTypes.GetBuildingTypeInfoByName(building.Kind);

        if (geo.HasField("source_type"))
            building.Source = geo.GetValueStringByKey("source_type");

        Material mat_by_type = null;
        Material mat_by_tag = null;

        if (!kind.Equals("yes"))
        {
            mat_by_type = building.curSettings.buildingMaterial;
        }

        if (geo.HasField("building:material"))
        {
            var mat_name = geo.GetValueStringByKey("building:material");

            // ОПТИМИЗАЦИЯ: Используем кэшированный поиск материала
            mat_by_tag = GetCachedMaterial(mat_name);

            if (mat_by_tag == null && isDebugNotFoundMaterials)
            {
                Debug.Log("Can' found building:material: " + mat_name + " for bulidnig");
            }
        }

        if (geo.HasField("material") && isDebugNotFoundMaterials)
        {
            var mat_name = geo.GetValueStringByKey("material");
            Debug.Log("Can' found material: " + mat_name + " for bulidnig");
        }

        if (geo.HasField("surface") && isDebugNotFoundMaterials)
        {
            var mat_name = geo.GetValueStringByKey("surface");
            Debug.Log("Can' found surface: " + mat_name + " for bulidnig");
        }

        if (geo.HasField("layer"))
        {
            building.layer = geo.GetValueIntByKey("layer");
        }

        // ОПТИМИЗАЦИЯ: Кэшируем MeshRenderer
        var meshRenderer = building.GetComponent<MeshRenderer>();

        if (mat_by_type != null)
        {
            meshRenderer.material = mat_by_type;
        }
        else if (geo.HasField("building:material") && mat_by_tag != null)
        {
            meshRenderer.material = mat_by_tag;
        }
        else if (geo.HasField("building:material") && mat_by_tag == null)
        {
            //not set for debug
        }
        else
        {
            meshRenderer.material = building_material;
        }

        UnityEngine.Color curColor = GR.SetOSMColour(geo);

        if (curColor != UnityEngine.Color.white)
        {
            meshRenderer.material.SetColor("_Color", curColor);
            meshRenderer.material.SetColor("_BaseColor", curColor);
        }
    }

    void CreateBuilding(BaseOsm geo)
    {
        // Защита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        m_countProcessing++;

        var searchname = "building " + geo.ID.ToString();

        if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        if (geo.HasField("building:part") && geo.GetValueStringByKey("building:part").Equals("yes") && bNotCreateOSMParts)
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

        var building = new GameObject(searchname).AddComponent<Building>();

        building.AddComponent<MeshFilter>();
        building.AddComponent<MeshRenderer>();

        building.itemlist = geo.itemlist;
        building.count = count;

        SetProperties(geo, building);

        var height = GetHeights(geo, building);
        var minHeight = GetMinHeight(geo);

        building.height = height;
        building.min_height = minHeight;

        var buildingCorners = new List<Vector3>(count);

        float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;

        // ОПТИМИЗАЦИЯ: Кэшируем центр здания
        Vector3 localOrigin;
        if (!centerCache.TryGetValue(geo.ID, out localOrigin))
        {
            localOrigin = GetCentre(geo);
            centerCache[geo.ID] = localOrigin;
        }

        // ИЗМЕНЕНИЕ: Используем WorldOrigin
        building.transform.position = localOrigin - MapReader.Instance.WorldOrigin;

        building.transform.position += Vector3.up * (building.layer * BaseDataObject.layer_size);

        // ОПТИМИЗАЦИЯ: Кэшируем ссылку на nodes
        var nodes = MapReader.Instance.nodes;

        for (int i = 0; i < count; i++)
        {
            if (nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 coords = point - localOrigin;

                if (coords.x < minx) minx = (float)coords.x;
                if (coords.z < miny) miny = (float)coords.z;
                if (coords.x > maxx) maxx = (float)coords.x;
                if (coords.z > maxy) maxy = (float)coords.z;

                buildingCorners.Add(coords);
            }
        }

        var holesCorners = new List<List<Vector3>>();

        var countHoles = geo.HolesNodeListsIDs.Count;

        for (int i = 0; i < countHoles; i++)
        {
            var holeNodes = geo.HolesNodeListsIDs[i];
            var countHoleContourPoints = holeNodes.Count;
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

        var mesh = building.GetComponent<MeshFilter>().mesh;

        // ОПТИМИЗАЦИЯ: Используем пул для MeshData
        var tb = GetMeshData();

        GR.CreateMeshWithHeight(buildingCorners, minHeight, height, tb, holesCorners);

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.normals = tb.Normals.ToArray();
        mesh.SetUVs(0, tb.UV);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();

        // ОПТИМИЗАЦИЯ: Возвращаем MeshData в пул
        ReturnMeshData(tb);

        if (geo.HasField("design:ref"))
        {
            var series = geo.GetValueStringByKey("design:ref");
            var floors = 0.0f;

            if (geo.HasField("building:levels"))
            {
                floors = geo.GetValueFloatByKey("building:levels");
            }
            else if (geo.HasField("building:height"))
            {
                floors = geo.GetValueFloatByKey("building:height") / 3.0f;
            }

            var entrances = 0.0f;

            if (geo.HasField("building:flats"))
            {
                var flats = geo.GetValueFloatByKey("building:flats");
                entrances = flats / floors / 4.0f;
            }

            BuildingSeries.BuildingSeriesReplace curSeries = buildingSeries.GetBuildingSeriesInfo(series, (int)floors, (int)entrances);

            string curpredstavlenie = "et_" + (int)floors + "_pod_" + (int)entrances + "_" + series;
            building.series_filter = curpredstavlenie;

            if (curSeries.buildingmodel != null)
            {
                GameObject model = Instantiate(curSeries.buildingmodel, Vector3.zero, Quaternion.identity);
                model.name = "AlignedBuildingModel";

                MeshFilter mf = MeshAlignTools.CenterPivot_CreateMeshRoot(model);
                MeshAlignTools.AlignMesh(model.transform, mf, building.GetComponent<MeshFilter>());
                model.transform.SetParent(building.transform, true);

                building.isModelSet = true;
            }
        }

        if (isCreateColision)
        {
            building.transform.gameObject.AddComponent<MeshCollider>();
            building.transform.GetComponent<MeshCollider>().sharedMesh = building.GetComponent<MeshFilter>().mesh;
            building.transform.GetComponent<MeshCollider>().convex = false;
        }

        bool isGenerateRoof = true;

        if (isGenerateRoof && bCreateRoofs)
        {
            if (contentselector != null && contentselector.isRoofDisabled(geo.ID) || building.isModelSet)
            {
                isGenerateRoof = false;
            }
            else if (geo.HasField("man_made"))
            {
                if (!geo.HasField("roof:shape") && !geo.HasField("roof:colour") && !geo.HasField("roof:height"))
                {
                    isGenerateRoof = false;
                }
            }
        }

        if (isGenerateRoof && bCreateRoofs)
        {
            generateRoof.GenerateRoofForObject(building, buildingCorners, holesCorners, minHeight, height, new Vector2(minx, miny), new Vector2(maxx - minx, maxy - miny), geo, isUseOldTriangulation);
        }

        if (geo.HasField("man_made") && geo.GetValueStringByKey("man_made").Equals("chimney"))
        {
            var go = Instantiate(smokeprefab, building.transform.position + (Vector3.up * (building.height - 0.20f)), Quaternion.identity);
            go.transform.localScale = new Vector3(25.0f, 25.0f, 25.0f);
            go.transform.Rotate(new Vector3(-90, 0, 0));
            go.transform.SetParent(building.transform);
        }

        if (geo.HasField("man_made"))
        {
            var man_made_type = geo.GetValueStringByKey("man_made");

            if (man_made_type == "bridge")
            {
                building.transform.position = new Vector3(building.transform.position.x, building.transform.position.y - building.height, building.transform.position.z);
            }
        }

        if (windowMaterialLit != null && windowMaterialDark != null && building.curSettings.isUseWindows && !geo.HasField("man_made") && !building.isModelSet && bCreateWindows)
        {
            var WindowPlacer = new GameObject("WindowPlacer");
            WindowPlacer.transform.position = building.transform.position;
            WindowPlacer.transform.SetParent(building.transform);
            WindowPlacer.transform.localScale = new Vector3(1.001f, 1f, 1.001f);

            WindowPlacer.AddComponent<MeshFilter>().mesh = building.GetComponent<MeshFilter>().mesh;
            WindowPlacer.AddComponent<MeshRenderer>().sharedMaterials = new Material[] { windowMaterialLit, windowMaterialDark };
            WindowPlacer.AddComponent<WindowPlacerOptimized>();
        }

        if (building.isModelSet)
        {
            building.GetComponent<MeshRenderer>().enabled = false;
        }

        if (building.HasField("addr:housenumber") && bCreateAdresses)
        {
            if (building.GetComponent<HouseNumberPlacer>() == null)
            {
                building.gameObject.AddComponent<HouseNumberPlacer>();
            }
        }

        // Корректировка высоты для Terrain
        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                StartCoroutine(SpawnInHeight(building.gameObject, AlgorithmHeightSorting.MinimumHeight));
            }
        }
    }

    // Вспомогательный метод для безопасного получения центра
    private Vector3 GetCentre(BaseOsm geo)
    {
        Vector3 total = Vector3.zero;
        int count = 0;
        foreach (var id in geo.NodeIDs)
        {
            if (MapReader.Instance.nodes.TryGetValue(id, out OsmNode node))
            {
                total += (Vector3)node;
                count++;
            }
        }
        return count > 0 ? total / count : Vector3.zero;
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

        // 1. Подписываемся на новые события
        MapReader.Instance.OnWayLoaded += OnGeoObjectLoaded;
        MapReader.Instance.OnRelationLoaded += OnGeoObjectLoaded;

        float starttime = Time.time;

        // ============================================
        // ОПТИМИЗАЦИЯ: Пакетная обработка зданий
        // ============================================
        int processedInBatch = 0;

        var ways = MapReader.Instance.ways;
        if (ways != null)
        {
            foreach (var way in ways)
            {
                if (way.objectType == BaseOsm.ObjectType.Building && way.NodeIDs.Count > 1)
                {
                    way.AddField("source_type", "way");
                    CreateBuilding(way);

                    processedInBatch++;
                    if (processedInBatch >= batchSize)
                    {
                        processedInBatch = 0;
                        yield return null; // Пауза только после обработки batchSize зданий
                    }
                }
            }
        }

        var relations = MapReader.Instance.relations;
        if (relations != null)
        {
            foreach (var relation in relations)
            {
                if (relation.objectType == BaseOsm.ObjectType.Building && relation.NodeIDs.Count > 1)
                {
                    relation.AddField("source_type", "relation");
                    CreateBuilding(relation);

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

        Debug.Log("Buildings create at: " + (endtime - starttime) + " | Total: " + m_countProcessing);

        isFinished = true;
    }

    // Обработчик событий
    private void OnGeoObjectLoaded(BaseOsm geo)
    {
        if (geo.objectType != BaseOsm.ObjectType.Building) return;
        if (geo.NodeIDs.Count <= 1) return;

        StartCoroutine(ProcessBuildingCoroutine(geo));
    }

    private IEnumerator ProcessBuildingCoroutine(BaseOsm geo)
    {
        CreateBuilding(geo);
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
