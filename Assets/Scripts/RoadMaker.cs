using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using static GR;
using static RoadTypesInfo;

/// <summary>
/// ОПТИМИЗИРОВАННЫЙ RoadMaker
/// 
/// Ключевые улучшения:
/// 1. Генерация данных мешей в фоновых потоках
/// 2. Создание GameObject в главном потоке после подготовки данных
/// 3. Batch обработка дорог
/// 4. Параллельная обработка независимых дорог
/// </summary>
public class RoadMaker : InfrstructureBehaviour
{
    public Material roadMaterial;
    public static GameContentSelector contentselector;

    public RoadTypesInfo roadTypes;
    public RoadSurfacesMaterials roadSurfacesMaterials;

    public RoadTypesInfo roadAreaTypes;
    public RoadSurfacesMaterials roadAreaSurfacesMaterials;

    public bool isCreateColision = false;
    public int MaxNodes = 150;
    public TileSystem tileSystem;
    public Material baseroadMaterial;

    public bool isDebugNotFoundMaterials = false;

    // ОПТИМИЗАЦИЯ: Настройки параллельной обработки
    [Header("Performance Settings")]
    [Tooltip("Количество дорог для batch обработки")]
    public int batchSize = 10;

    [Tooltip("Включить параллельную генерацию мешей")]
    public bool useParallelMeshGeneration = true;

    [Tooltip("Включить детальное логирование")]
    public bool enableDebugLogging = false;

    private int m_countProcessing = 0;
    private HashSet<ulong> processedIDs = new HashSet<ulong>();

    // ОПТИМИЗАЦИЯ: Очередь для асинхронного создания GameObject
    private ConcurrentQueue<RoadCreationResult> _roadCreationQueue = new ConcurrentQueue<RoadCreationResult>();

    // Структура для хранения результата
    private class RoadCreationResult
    {
        public RoadGenerationData Data;
        public BaseOsm Geo;
        public bool Success;
    }

    private float GetReoadWith(BaseOsm geo)
    {
        float roadwith = 0f;

        if (geo.HasField("width"))
        {
            roadwith = geo.GetValueFloatByKey("width");
        }
        else if (geo.HasField("width:carriageway"))
        {
            roadwith = geo.GetValueFloatByKey("width:carriageway");
        }
        else if (geo.HasField("width:lanes"))
        {
            string lanesWidthValue = geo.GetValueStringByKey("width:lanes");
            roadwith = CalculateWidthFromLanes(lanesWidthValue);
        }

        return roadwith;
    }

    private float CalculateWidthFromLanes(string lanesWidthValue)
    {
        try
        {
            string[] laneWidths = lanesWidthValue.Split('|', ';');
            float totalWidth = 0f;
            int validCount = 0;

            foreach (string laneWidth in laneWidths)
            {
                if (float.TryParse(laneWidth.Trim(), out float width))
                {
                    totalWidth += width;
                    validCount++;
                }
            }
            return validCount > 0 ? totalWidth : 2.0f;
        }
        catch
        {
            return 2.0f;
        }
    }

    private int GetLaneCount(BaseOsm geo)
    {
        int countlanes = 1;

        if (geo.HasField("lanes"))
        {
            countlanes = geo.GetValueIntByKey("lanes");
        }
        else if (geo.HasField("lanes:forward") && geo.HasField("lanes:backward"))
        {
            int forwadlines = geo.GetValueIntByKey("lanes:forward");
            int backwardlines = geo.GetValueIntByKey("lanes:backward");
            countlanes = forwadlines + backwardlines;
        }
        else
        {
            string highwayType = geo.GetValueStringByKey("highway", "");
            switch (highwayType)
            {
                case "motorway":
                case "trunk":
                    countlanes = 2;
                    break;
                case "primary":
                case "secondary":
                    countlanes = IsOneWayRoad(geo) ? 2 : 1;
                    break;
                case "tertiary":
                case "unclassified":
                case "residential":
                    countlanes = 1;
                    break;
                default:
                    countlanes = 1;
                    break;
            }
        }
        return Mathf.Max(1, countlanes);
    }

    private bool IsOneWayRoad(BaseOsm geo)
    {
        string onewayValue = geo.GetValueStringByKey("oneway", "no");
        return onewayValue == "yes" ||
               onewayValue == "true" ||
               onewayValue == "1" ||
               geo.HasField("junction") && geo.GetValueStringByKey("junction") == "roundabout";
    }

    private void SetProperties(BaseOsm geo, Road road)
    {
        road.name = "road " + geo.ID.ToString();

        if (geo.HasField("name"))
            road.Name = geo.GetValueStringByKey("name");

        road.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("area:highway"))
        {
            road.isArea = true;
        }
        else if (geo.HasField("type") && geo.GetValueStringByKey("type") == "multipolygon")
        {
            road.isArea = true;
        }

        if (geo.HasField("footway") && geo.GetValueStringByKey("footway").Equals("crossing"))
        {
            kind = "crossing";
        }
        else if (geo.HasField("highway"))
        {
            kind = geo.GetValueStringByKey("highway");
        }
        else if (geo.HasField("area:highway"))
        {
            kind = geo.GetValueStringByKey("area:highway");
        }
        else if (geo.HasField("railway"))
        {
            kind = "railway";
        }
        else
        {
            kind = "yes";
        }

        road.Kind = kind;

        if (geo.HasField("source_type"))
            road.Source = geo.GetValueStringByKey("source_type");

        road.lanes = GetLaneCount(geo);

        if (geo.HasField("layer"))
        {
            road.layer = geo.GetValueIntByKey("layer");
        }

        RoadTypeInfoItem roadInfo;

        if (road.isArea)
        {
            roadInfo = roadAreaTypes.GetRoadTypeInfoByName(road.Kind);
        }
        else
        {
            roadInfo = roadTypes.GetRoadTypeInfoByName(road.Kind);
        }

        Material surfaceMaterial = null;

        if (geo.HasField("surface"))
        {
            var surfaceName = geo.GetValueStringByKey("surface");

            if (road.isArea)
            {
                surfaceMaterial = roadAreaSurfacesMaterials.GetRoadSurfacesMaterialByName(surfaceName);
            }
            else
            {
                surfaceMaterial = roadSurfacesMaterials.GetRoadSurfacesMaterialByName(surfaceName);
            }

            if (surfaceMaterial == null && surfaceName != "asphalt" && isDebugNotFoundMaterials)
            {
                Debug.Log("Can' found surface: " + surfaceName + " for road");
            }
        }

        if (surfaceMaterial != null)
        {
            road.GetComponent<MeshRenderer>().material = surfaceMaterial;
        }
        else if (roadInfo.roadMaterial)
        {
            road.GetComponent<MeshRenderer>().material = roadInfo.roadMaterial;
        }
        else
        {
            road.GetComponent<MeshRenderer>().material = roadMaterial;
        }

        float width = GetReoadWith(geo);

        if (width != 0.0f)
        {
            road.width = width;
        }
        else if (roadInfo.roadWidth != 0.0f)
        {
            road.width = road.lanes * roadInfo.roadWidth;
        }
        else
        {
            road.width = road.lanes * 2.0f;
        }

        road.layersLevel = roadInfo.layersLevel;
        road.typeUsage = roadInfo.typeUsage;
    }

    Vector3 GetRoadHeight(Road road, ulong roadid)
    {
        double height = 0.001f * road.layersLevel;
        double idBasedOffset = 0.0001f + (float)((double)roadid / 1000000000 * 0.0008f);
        height += idBasedOffset;
        return new Vector3(0f, (float)height, 0f);
    }

    public void CalculateRoadEdges(List<Vector3> corners, float width, Road road)
    {
        if (corners.Count < 2)
            return;

        road.leftPoints = new List<Vector3>();
        road.rightPoints = new List<Vector3>();

        for (int i = 0; i < corners.Count; i++)
        {
            Vector3 dirPrev, dirNext;
            Vector3 cross;

            if (i == 0)
            {
                dirNext = (corners[i + 1] - corners[i]).normalized;
                cross = Vector3.Cross(dirNext, Vector3.up) * width;
            }
            else if (i == corners.Count - 1)
            {
                dirPrev = (corners[i] - corners[i - 1]).normalized;
                cross = Vector3.Cross(dirPrev, Vector3.up) * width;
            }
            else
            {
                dirPrev = (corners[i] - corners[i - 1]).normalized;
                dirNext = (corners[i + 1] - corners[i]).normalized;

                Vector3 bisectorDir = dirPrev + dirNext;
                if (bisectorDir.magnitude < 0.001f)
                {
                    cross = Vector3.Cross(dirPrev, Vector3.up) * width;
                }
                else
                {
                    bisectorDir.Normalize();
                    cross = Vector3.Cross(bisectorDir, Vector3.up) * width;
                }
            }

            road.leftPoints.Add(corners[i] + cross);
            road.rightPoints.Add(corners[i] - cross);
        }
    }

    // ОПТИМИЗАЦИЯ: Подготовка данных дороги в фоновом потоке
    private RoadGenerationData PrepareRoadData(BaseOsm geo)
    {
        var data = new RoadGenerationData
        {
            Id = geo.ID.ToString(),
            Name = geo.HasField("name") ? geo.GetValueStringByKey("name") : "",
            RoadCorners = new List<Vector3>(),
            LeftPoints = new List<Vector3>(),
            RightPoints = new List<Vector3>(),
            HolesCorners = new List<List<Vector3>>()
        };

        // Сбор координат
        Vector3 localOrigin = GetCentre(geo);

        for (int i = 0; i < geo.NodeIDs.Count; i++)
        {
            if (MapReader.Instance.nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 coords = point - localOrigin;
                data.RoadCorners.Add(coords);
            }
        }

        // Обработка отверстий
        for (int i = 0; i < geo.HolesNodeListsIDs.Count; i++)
        {
            var holeNodes = geo.HolesNodeListsIDs[i];
            var holeContour = new List<Vector3>();

            for (int j = 0; j < holeNodes.Count; j++)
            {
                if (MapReader.Instance.nodes.TryGetValue(holeNodes[j], out OsmNode point))
                {
                    Vector3 coords = point - localOrigin;
                    holeContour.Add(coords);
                }
            }
            data.HolesCorners.Add(holeContour);
        }

        // Определение свойств
        data.IsArea = geo.HasField("area:highway") ||
                      (geo.HasField("type") && geo.GetValueStringByKey("type") == "multipolygon");

        data.Lanes = GetLaneCount(geo);
        data.Layer = geo.HasField("layer") ? geo.GetValueIntByKey("layer") : 0;
        data.Width = GetReoadWith(geo);

        // ОПРЕДЕЛЯЕМ KIND (для материала)
        data.Kind = "yes";
        if (geo.HasField("footway") && geo.GetValueStringByKey("footway").Equals("crossing"))
            data.Kind = "crossing";
        else if (geo.HasField("highway"))
            data.Kind = geo.GetValueStringByKey("highway");
        else if (geo.HasField("area:highway"))
            data.Kind = geo.GetValueStringByKey("area:highway");
        else if (geo.HasField("railway"))
            data.Kind = "railway";

        // Имя поверхности
        data.SurfaceName = geo.HasField("surface") ? geo.GetValueStringByKey("surface") : null;

        // layersLevel и typeUsage определим в главном потоке при создании GameObject
        // (доступ к roadTypes невозможен из фонового потока)

        if (data.Width == 0f)
        {
            // Дефолтная ширина
            data.Width = data.Lanes * 2.0f;
        }

        // Генерация меша
        var meshData = new MeshData();

        if (data.IsArea)
        {
            GR.CreateMeshWithHeight(data.RoadCorners, 0.0f, 0.0001f, meshData, data.HolesCorners, true, true);
        }
        else if (data.RoadCorners.Count >= 2)
        {
            GR.CreateMeshLineWithWidth(data.RoadCorners, data.Width, meshData);
        }

        data.MeshData = new MeshGenerationData
        {
            Vertices = meshData.Vertices.ToArray(),
            Triangles = meshData.Indices.ToArray(),
            Normals = meshData.Normals.ToArray(),
            UV = meshData.UV.ToArray()
        };

        // Вычисление краев
        if (data.RoadCorners.Count >= 2)
        {
            CalculateRoadEdgesData(data);
        }

        // Позиция
        data.Position = localOrigin - MapReader.Instance.WorldOrigin;
        data.Position += Vector3.up * 0.001f;

        double height = 0.001f * (data.IsArea ? 0 : data.Layer);
        double idBasedOffset = 0.0001f + ((double)geo.ID / 1000000000 * 0.0008f);
        height += idBasedOffset;
        data.Position += new Vector3(0f, (float)height, 0f);
        data.Position += Vector3.up * (data.Layer * BaseDataObject.layer_size);

        return data;
    }

    // ОПТИМИЗАЦИЯ: Вычисление краев без зависимости от Road компонента
    private void CalculateRoadEdgesData(RoadGenerationData data)
    {
        if (data.RoadCorners.Count < 2) return;

        for (int i = 0; i < data.RoadCorners.Count; i++)
        {
            Vector3 dirPrev, dirNext;
            Vector3 cross;

            if (i == 0)
            {
                dirNext = (data.RoadCorners[i + 1] - data.RoadCorners[i]).normalized;
                cross = Vector3.Cross(dirNext, Vector3.up) * data.Width;
            }
            else if (i == data.RoadCorners.Count - 1)
            {
                dirPrev = (data.RoadCorners[i] - data.RoadCorners[i - 1]).normalized;
                cross = Vector3.Cross(dirPrev, Vector3.up) * data.Width;
            }
            else
            {
                dirPrev = (data.RoadCorners[i] - data.RoadCorners[i - 1]).normalized;
                dirNext = (data.RoadCorners[i + 1] - data.RoadCorners[i]).normalized;

                Vector3 bisectorDir = dirPrev + dirNext;
                if (bisectorDir.magnitude < 0.001f)
                {
                    cross = Vector3.Cross(dirPrev, Vector3.up) * data.Width;
                }
                else
                {
                    bisectorDir.Normalize();
                    cross = Vector3.Cross(bisectorDir, Vector3.up) * data.Width;
                }
            }

            data.LeftPoints.Add(data.RoadCorners[i] + cross);
            data.RightPoints.Add(data.RoadCorners[i] - cross);
        }
    }

    // ОПТИМИЗАЦИЯ: Создание GameObject из подготовленных данных (в главном потоке)
    private void CreateRoadFromData(RoadGenerationData data, BaseOsm geo, Item[] itemlist)
    {
        var road = new GameObject("road " + data.Id).AddComponent<Road>();
        road.AddComponent<MeshFilter>();
        road.AddComponent<MeshRenderer>();
        road.itemlist = itemlist;

        road.name = "road " + data.Id;
        road.Name = data.Name;
        road.Id = data.Id;
        road.isArea = data.IsArea;
        road.lanes = data.Lanes;
        road.layer = data.Layer;
        road.width = data.Width;
        road.Kind = data.Kind;

        if (geo.HasField("source_type"))
            road.Source = geo.GetValueStringByKey("source_type");

        // Получаем roadInfo ОДИН РАЗ - используем для материала, layersLevel, typeUsage
        RoadTypeInfoItem roadInfo = data.IsArea
            ? roadAreaTypes.GetRoadTypeInfoByName(data.Kind)
            : roadTypes.GetRoadTypeInfoByName(data.Kind);

        // Устанавливаем свойства из roadInfo
        road.layersLevel = roadInfo.layersLevel;
        road.typeUsage = roadInfo.typeUsage;

        // МАТЕРИАЛ - назначаем в главном потоке
        Material surfaceMaterial = null;
        if (!string.IsNullOrEmpty(data.SurfaceName))
        {
            surfaceMaterial = data.IsArea
                ? roadAreaSurfacesMaterials.GetRoadSurfacesMaterialByName(data.SurfaceName)
                : roadSurfacesMaterials.GetRoadSurfacesMaterialByName(data.SurfaceName);
        }

        // Назначаем материал: surface -> roadInfo.roadMaterial -> default roadMaterial
        if (surfaceMaterial != null)
        {
            road.GetComponent<MeshRenderer>().material = surfaceMaterial;
        }
        else if (roadInfo.roadMaterial != null)
        {
            road.GetComponent<MeshRenderer>().material = roadInfo.roadMaterial;
        }
        else
        {
            road.GetComponent<MeshRenderer>().material = roadMaterial;
        }

        road.transform.position = data.Position;

        // Присваиваем координаты
        road.coordpoints = new List<Vector3>();
        for (int i = 0; i < geo.NodeIDs.Count; i++)
        {
            if (MapReader.Instance.nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 globalcoord = MercatorProjection.ConvertGeoToUntyCoord(
                    point.Latitude, point.Longitude, MapReader.Instance.WorldOrigin);
                road.coordpoints.Add(globalcoord);
            }
        }

        // Применяем сгенерированный меш
        var mesh = road.GetComponent<MeshFilter>().mesh;
        mesh.vertices = data.MeshData.Vertices;
        mesh.triangles = data.MeshData.Triangles;
        mesh.normals = data.MeshData.Normals;
        mesh.SetUVs(0, data.MeshData.UV);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();

        road.corners = data.RoadCorners;
        road.leftPoints = data.LeftPoints;
        road.rightPoints = data.RightPoints;

        if (data.RoadCorners.Count >= 2)
        {
            road.InitializeLanes();
        }

        if (isCreateColision)
        {
            road.transform.gameObject.AddComponent<MeshCollider>();
            road.transform.GetComponent<MeshCollider>().sharedMesh = mesh;
            road.transform.GetComponent<MeshCollider>().convex = false;
        }

        if (geo.HasField("footway") && geo.GetValueStringByKey("footway") == "crossing")
        {
            road.gameObject.layer = LayerMask.NameToLayer("Crossing");
        }
        else
        {
            road.gameObject.layer = LayerMask.NameToLayer("Roads");
        }

        // Корректировка под Terrain
        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain && tileSystem.isUseElevation)
        {
            var settings = new TessellationSettings
            {
                maxEdgeLength = 0.001f,
                heightSensitivity = 0.001f,
                maxVertexCount = 50000,
                heightfix = 0.1f
            };
            StartCoroutine(AdjustMeshToTerrainCorutine(road.gameObject, settings));
        }
    }

    // Оригинальный метод CreateRoads для совместимости
    void CreateRoads(BaseOsm geo)
    {
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        var searchname = "road " + geo.ID.ToString();
        m_countProcessing++;

        if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            return;
        }

        var road = new GameObject(searchname).AddComponent<Road>();
        road.AddComponent<MeshFilter>();
        road.AddComponent<MeshRenderer>();
        road.itemlist = geo.itemlist;

        SetProperties(geo, road);

        var roadsCorners = new List<Vector3>();
        Vector3 roadlayerHeight = GetRoadHeight(road, geo.ID);
        Vector3 localOrigin = GetCentre(geo);

        road.transform.position = localOrigin - MapReader.Instance.WorldOrigin;
        road.transform.position += Vector3.up * 0.001f;
        road.transform.position += roadlayerHeight;
        road.transform.position += Vector3.up * (road.layer * BaseDataObject.layer_size);

        road.coordpoints = new List<Vector3>();

        for (int i = 0; i < count; i++)
        {
            if (MapReader.Instance.nodes.TryGetValue(geo.NodeIDs[i], out OsmNode point))
            {
                Vector3 coords = point - localOrigin;
                roadsCorners.Add(coords);
                Vector3 globalcoord = MercatorProjection.ConvertGeoToUntyCoord(point.Latitude, point.Longitude, MapReader.Instance.WorldOrigin);
                road.coordpoints.Add(globalcoord);
            }
        }

        var holesCorners = new List<List<Vector3>>();
        var countHoles = geo.HolesNodeListsIDs.Count;

        for (int i = 0; i < countHoles; i++)
        {
            var holeNodes = geo.HolesNodeListsIDs[i];
            var countHoleContourPoints = holeNodes.Count;
            var holeContour = new List<Vector3>();

            for (int j = 0; j < countHoleContourPoints; j++)
            {
                if (MapReader.Instance.nodes.TryGetValue(holeNodes[j], out OsmNode point))
                {
                    Vector3 coords = point - localOrigin;
                    holeContour.Add(coords);
                }
            }
            holesCorners.Add(holeContour);
        }

        var mesh = road.GetComponent<MeshFilter>().mesh;
        var tb = new MeshData();

        if (road.isArea)
        {
            GR.CreateMeshWithHeight(roadsCorners, 0.0f, 0.0001f, tb, holesCorners, true, true);
        }
        else
        {
            if (roadsCorners.Count >= 2)
            {
                GR.CreateMeshLineWithWidth(roadsCorners, road.width, tb);
            }
        }

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.normals = tb.Normals.ToArray();
        mesh.SetUVs(0, tb.UV);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();

        road.corners = roadsCorners;

        if (roadsCorners.Count >= 2)
        {
            CalculateRoadEdges(roadsCorners, road.width, road);
            road.InitializeLanes();
        }

        if (isCreateColision)
        {
            road.transform.gameObject.AddComponent<MeshCollider>();
            road.transform.GetComponent<MeshCollider>().sharedMesh = road.GetComponent<MeshFilter>().mesh;
            road.transform.GetComponent<MeshCollider>().convex = false;
        }

        if (geo.HasField("footway") && geo.GetValueStringByKey("footway") == "crossing")
        {
            road.gameObject.layer = LayerMask.NameToLayer("Crossing");
        }
        else
        {
            road.gameObject.layer = LayerMask.NameToLayer("Roads");
        }

        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain && tileSystem.isUseElevation)
        {
            var settings = new TessellationSettings
            {
                maxEdgeLength = 0.001f,
                heightSensitivity = 0.001f,
                maxVertexCount = 50000,
                heightfix = 0.1f
            };
            StartCoroutine(AdjustMeshToTerrainCorutine(road.gameObject, settings));
        }
    }

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

    // ОПТИМИЗИРОВАННАЯ корутина Start
    IEnumerator Start()
    {
        while (MapReader.Instance == null || !MapReader.Instance.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();
        tileSystem = FindObjectOfType<TileSystem>();

        MapReader.Instance.OnWayLoaded += OnGeoObjectLoaded;
        MapReader.Instance.OnRelationLoaded += OnGeoObjectLoaded;

        float starttime = Time.time;

        // ОПТИМИЗАЦИЯ: Параллельная обработка ways
        var ways = MapReader.Instance.ways;
        if (ways != null)
        {
            if (useParallelMeshGeneration && ThreadedDataProcessor.Instance != null)
            {
                if (enableDebugLogging) Debug.Log("[RoadMaker] Starting PARALLEL processing for ways");
                yield return StartCoroutine(ProcessRoadsParallel(ways));
            }
            else
            {
                if (enableDebugLogging) Debug.Log("[RoadMaker] Starting SEQUENTIAL processing for ways");
                foreach (var way in ways)
                {
                    if (way.objectType == BaseOsm.ObjectType.Road)
                    {
                        way.AddField("source_type", "way");
                        CreateRoads(way);
                        yield return null;
                    }
                }
            }
        }

        // ОПТИМИЗАЦИЯ: Параллельная обработка relations
        var relations = MapReader.Instance.relations;
        if (relations != null)
        {
            if (useParallelMeshGeneration && ThreadedDataProcessor.Instance != null)
            {
                if (enableDebugLogging) Debug.Log("[RoadMaker] Starting PARALLEL processing for relations");
                yield return StartCoroutine(ProcessRelationsParallel(relations));
            }
            else
            {
                if (enableDebugLogging) Debug.Log("[RoadMaker] Starting SEQUENTIAL processing for relations");
                foreach (var relation in relations)
                {
                    if (relation.objectType == BaseOsm.ObjectType.Road && relation.IsClosedPolygon)
                    {
                        relation.AddField("source_type", "relation");
                        CreateRoads(relation);
                        yield return null;
                    }
                }
            }
        }

        float endtime = Time.time;
        Debug.Log($"[RoadMaker] Roads created: {m_countProcessing} at: {(endtime - starttime):F2}s");

        isFinished = true;
    }

    // ОПТИМИЗАЦИЯ: Параллельная обработка roads
    private IEnumerator ProcessRoadsParallel(List<OsmWay> ways)
    {
        var roadsToProcess = new List<BaseOsm>();

        foreach (var way in ways)
        {
            if (way.objectType == BaseOsm.ObjectType.Road)
            {
                way.AddField("source_type", "way");
                roadsToProcess.Add(way);
            }
        }

        if (enableDebugLogging) Debug.Log($"[RoadMaker] Found {roadsToProcess.Count} roads to process");

        yield return StartCoroutine(ProcessGeoObjectsParallel(roadsToProcess));
    }

    private IEnumerator ProcessRelationsParallel(List<OsmRelation> relations)
    {
        var relationsToProcess = new List<BaseOsm>();

        foreach (var relation in relations)
        {
            if (relation.objectType == BaseOsm.ObjectType.Road && relation.IsClosedPolygon)
            {
                relation.AddField("source_type", "relation");
                relationsToProcess.Add(relation);
            }
        }

        yield return StartCoroutine(ProcessGeoObjectsParallel(relationsToProcess));
    }

    // ИСПРАВЛЕННАЯ параллельная обработка
    private IEnumerator ProcessGeoObjectsParallel(List<BaseOsm> geoObjects)
    {
        if (geoObjects.Count == 0) yield break;

        int totalCreated = 0;
        int totalSkipped = 0;

        // Обрабатываем батчами
        for (int batchStart = 0; batchStart < geoObjects.Count; batchStart += batchSize)
        {
            int batchEnd = Mathf.Min(batchStart + batchSize, geoObjects.Count);

            // Собираем дорогИ для этого батча
            var batchGeoObjects = new List<BaseOsm>();
            for (int i = batchStart; i < batchEnd; i++)
            {
                var geo = geoObjects[i];

                // Проверки ДО добавления в батч
                if (processedIDs.Contains(geo.ID))
                {
                    totalSkipped++;
                    continue;
                }
                processedIDs.Add(geo.ID);

                if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
                {
                    totalSkipped++;
                    continue;
                }

                if (geo.NodeIDs == null || geo.NodeIDs.Count > MaxNodes)
                {
                    totalSkipped++;
                    continue;
                }

                batchGeoObjects.Add(geo);
            }

            if (batchGeoObjects.Count == 0)
            {
                yield return null;
                continue;
            }

            if (enableDebugLogging)
                Debug.Log($"[RoadMaker] Batch {batchStart / batchSize + 1}: processing {batchGeoObjects.Count} roads");

            // Очищаем очередь перед началом
            while (_roadCreationQueue.TryDequeue(out _)) { }

            int tasksQueued = 0;

            // Запускаем подготовку данных в фоновых потоках
            foreach (var geo in batchGeoObjects)
            {
                m_countProcessing++;
                tasksQueued++;

                var geoCopy = geo;

                // Используем Task.Run напрямую для гарантированного запуска
                Task.Run(() =>
                {
                    try
                    {
                        var data = PrepareRoadData(geoCopy);
                        _roadCreationQueue.Enqueue(new RoadCreationResult
                        {
                            Data = data,
                            Geo = geoCopy,
                            Success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RoadMaker] Error preparing road {geoCopy.ID}: {ex.Message}");
                        _roadCreationQueue.Enqueue(new RoadCreationResult
                        {
                            Geo = geoCopy,
                            Success = false
                        });
                    }
                });
            }

            if (enableDebugLogging)
                Debug.Log($"[RoadMaker] Queued {tasksQueued} background tasks");

            // Ждем завершения всех задач
            int created = 0;
            int failed = 0;
            float waitStart = Time.time;
            float timeout = Mathf.Max(5f, tasksQueued * 0.5f); // Динамический таймаут

            while ((created + failed) < tasksQueued && (Time.time - waitStart) < timeout)
            {
                // Обрабатываем готовые результаты
                while (_roadCreationQueue.TryDequeue(out var result))
                {
                    if (result.Success && result.Data != null)
                    {
                        try
                        {
                            CreateRoadFromData(result.Data, result.Geo, result.Geo.itemlist);
                            created++;
                            totalCreated++;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[RoadMaker] Error creating road: {ex.Message}");
                            failed++;
                        }
                    }
                    else
                    {
                        failed++;
                    }
                }

                // Даем время фоновым потокам
                yield return null;
            }

            // Обрабатываем оставшиеся в очереди
            while (_roadCreationQueue.TryDequeue(out var result))
            {
                if (result.Success && result.Data != null)
                {
                    try
                    {
                        CreateRoadFromData(result.Data, result.Geo, result.Geo.itemlist);
                        created++;
                        totalCreated++;
                    }
                    catch
                    {
                        failed++;
                    }
                }
                else
                {
                    failed++;
                }
            }

            if (enableDebugLogging)
                Debug.Log($"[RoadMaker] Batch complete: created={created}, failed={failed}, total={totalCreated}");
        }

        Debug.Log($"[RoadMaker] All batches complete: created={totalCreated}, skipped={totalSkipped}");
    }

    private void OnGeoObjectLoaded(BaseOsm geo)
    {
        if (geo.objectType != BaseOsm.ObjectType.Road) return;
        if (geo is OsmRelation && !geo.IsClosedPolygon) return;

        StartCoroutine(ProcessRoadCoroutine(geo));
    }

    private IEnumerator ProcessRoadCoroutine(BaseOsm geo)
    {
        CreateRoads(geo);
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
