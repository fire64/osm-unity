using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using static GR;

class RouteMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
    public TileSystem tileSystem;

    public List<Route> routes;

    private int m_countProcessing = 0;
    // Список для отслеживания уже обработанных ID
    private HashSet<ulong> processedIDs = new HashSet<ulong>();

    // ============================================
    // ОПТИМИЗАЦИЯ: batchSize для пакетной обработки
    // ============================================
    [Header("Optimization Settings")]
    [Tooltip("Количество маршрутов обрабатываемых за один кадр")]
    public int batchSize = 10;

    // ============================================
    // ОПТИМИЗАЦИЯ: Кэширование ссылок
    // ============================================
    private Dictionary<ulong, OsmNode> cachedNodes;
    private Vector3 cachedWorldOrigin;

    private void SetProperties(BaseOsm geo, Route route)
    {
        route.name = "route " + geo.ID.ToString();

        if (geo.HasField("name"))
            route.Name = geo.GetValueStringByKey("name");

        route.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("route"))
        {
            kind = geo.GetValueStringByKey("route");
        }
        else
        {
            kind = "yes";
        }

        route.Kind = kind;

        if (geo.HasField("source_type"))
            route.Source = geo.GetValueStringByKey("source_type");
    }

    void CreateRoutes(OsmRelation geo)
    {
        // Защита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        var searchname = "route " + geo.ID.ToString();

        m_countProcessing++;

        if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var route = new GameObject(searchname).AddComponent<Route>();

        route.AddComponent<MeshFilter>();
        route.AddComponent<MeshRenderer>();

        route.itemlist = geo.itemlist;
        route.memberslist = geo.memberslist;

        SetProperties(geo, route);

        // ОПТИМИЗАЦИЯ: Предварительное выделение памяти для списков
        int membersCount = geo.memberslist.Count;
        route.stoppoints = new List<Vector3>(membersCount);
        route.coordpoints = new List<Vector3>(membersCount * 8); // Примерно 8 точек на way
        route.platformsid = new List<ulong>(membersCount);

        // ОПТИМИЗАЦИЯ: Используем кэшированные ссылки
        var nodes = cachedNodes;
        var worldOrigin = cachedWorldOrigin;

        int memberslistCount = geo.memberslist.Count;

        for (int i = 0; i < memberslistCount; i++)
        {
            var member = geo.memberslist[i];

            // Обработка остановок (Nodes)
            if (member.type == OSMTypes.Node && (member.Role.Equals("stop_entry_only") || member.Role.Equals("stop") || member.Role.Equals("stop_exit_only")))
            {
                // ИЗМЕНЕНИЕ: Безопасный доступ к нодам через кэшированный словарь
                if (nodes.TryGetValue(member.GeoId, out OsmNode point))
                {
                    // ИЗМЕНЕНИЕ: Используем кэшированный WorldOrigin
                    Vector3 globalcoord = MercatorProjection.ConvertGeoToUntyCoord(point.Latitude, point.Longitude, worldOrigin);
                    route.stoppoints.Add(globalcoord);
                }
            }

            // Обработка платформ (Nodes)
            if (member.type == OSMTypes.Node && (member.Role.Equals("platform_entry_only") || member.Role.Equals("platform") || member.Role.Equals("platform_exit_only")))
            {
                route.platformsid.Add(member.GeoId);
            }

            // Обработка путей (Ways)
            if (member.type == OSMTypes.Way)
            {
                // Метод GetOsmWay внутри OsmRelation ищет способ в списке ways, переданном при создании.
                // В MapReader мы передаем актуальный список ways, поэтому это должно работать.
                OsmWay way = geo.GetOsmWay(member.GeoId);

                if (way != null)
                {
                    var nodeIds = way.NodeIDs;
                    int nodeIdsCount = nodeIds.Count;

                    for (int j = 0; j < nodeIdsCount; j++)
                    {
                        // ИЗМЕНЕНИЕ: Безопасный доступ к нодам
                        if (nodes.TryGetValue(nodeIds[j], out OsmNode point))
                        {
                            // ИЗМЕНЕНИЕ: Используем кэшированный WorldOrigin
                            Vector3 globalcoord = MercatorProjection.ConvertGeoToUntyCoord(point.Latitude, point.Longitude, worldOrigin);
                            route.coordpoints.Add(globalcoord);
                        }
                    }
                }
            }
        }

        // Корректировка высоты для Terrain
        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                StartCoroutine(SpawnInHeight(route.gameObject, AlgorithmHeightSorting.AverageHeight));
            }
        }

        routes.Add(route);
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

        routes = new List<Route>();

        // 1. Подписываемся на новые события (для динамической подгрузки)
        MapReader.Instance.OnRelationLoaded += OnRelationLoaded;

        float starttime = Time.time;

        // ============================================
        // ОПТИМИЗАЦИЯ: Пакетная обработка маршрутов
        // ============================================
        int processedInBatch = 0;

        // 2. Обрабатываем то, что УЖЕ загружено (первоначальная загрузка)
        var relations = MapReader.Instance.relations;
        if (relations != null)
        {
            foreach (var relation in relations)
            {
                if (relation.objectType == BaseOsm.ObjectType.Route)
                {
                    relation.AddField("source_type", "relation");
                    CreateRoutes(relation);

                    processedInBatch++;
                    if (processedInBatch >= batchSize)
                    {
                        processedInBatch = 0;
                        yield return null; // Пауза только после обработки batchSize маршрутов
                    }
                }
            }
        }

        float endtime = Time.time;

        Debug.Log("Routes create at: " + (endtime - starttime) + " | Total: " + m_countProcessing);

        isFinished = true;
    }

    // Обработчик событий
    private void OnRelationLoaded(BaseOsm geo)
    {
        // Фильтрация: обрабатываем только маршруты
        if (geo.objectType != BaseOsm.ObjectType.Route) return;

        // Маршруты - это всегда отношения
        if (geo is OsmRelation relation)
        {
            StartCoroutine(ProcessRouteCoroutine(relation));
        }
    }

    private IEnumerator ProcessRouteCoroutine(OsmRelation relation)
    {
        relation.AddField("source_type", "relation");
        CreateRoutes(relation);
        yield return null;
    }

    private void OnDestroy()
    {
        if (MapReader.Instance != null)
        {
            MapReader.Instance.OnRelationLoaded -= OnRelationLoaded;
        }
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }
}
