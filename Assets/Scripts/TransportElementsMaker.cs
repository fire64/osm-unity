using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GR;

class TransportElementsMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
    public TileSystem tileSystem;

    public GameObject platformPrefab;

    public List<TransportPlatform> platforms;
    public List<TransportStopPosition> stoppositions;

    private int m_countProcessing = 0;

    // —писок дл€ отслеживани€ уже обработанных ID
    private HashSet<ulong> processedIDs = new HashSet<ulong>();

    // ============================================
    // ќѕ“»ћ»«ј÷»я: batchSize дл€ пакетной обработки
    // ============================================
    [Header("Optimization Settings")]
    [Tooltip(" оличество transport элементов обрабатываемых за один кадр")]
    public int batchSize = 20;

    // ============================================
    // ќѕ“»ћ»«ј÷»я:  эширование ссылок
    // ============================================
    private Vector3 cachedWorldOrigin;
    private bool cachedIsUseElevation;
    private bool isTerrainType;

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
        cachedWorldOrigin = MapReader.Instance.WorldOrigin;

        // ќѕ“»ћ»«ј÷»я:  эшируем настройки terrain
        isTerrainType = tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain;
        cachedIsUseElevation = isTerrainType && tileSystem.isUseElevation;

        // ќѕ“»ћ»«ј÷»я: ѕредварительное выделение пам€ти дл€ списков
        platforms = new List<TransportPlatform>();
        stoppositions = new List<TransportStopPosition>();

        // 1. ѕодписываемс€ на новые событи€
        MapReader.Instance.OnNodeLoaded += OnNodeLoaded;

        float starttime = Time.time;

        // ============================================
        // ќѕ“»ћ»«ј÷»я: ѕакетна€ обработка объектов
        // ============================================
        int processedInBatch = 0;

        // 2. ќбрабатываем уже загруженные данные
        var nodesList = MapReader.Instance.nodeslist;
        if (nodesList != null)
        {
            int nodesCount = nodesList.Count;
            for (int i = 0; i < nodesCount; i++)
            {
                var node = nodesList[i];
                if (node.objectType == BaseOsm.ObjectType.Detail)
                {
                    node.AddField("source_type", "node");
                    CreateTransportElement(node);

                    processedInBatch++;
                    if (processedInBatch >= batchSize)
                    {
                        processedInBatch = 0;
                        yield return null; // ѕауза только после обработки batchSize объектов
                    }
                }
            }
        }

        float endtime = Time.time;

        Debug.Log("Transport elements create at: " + (endtime - starttime) + " | Total: " + m_countProcessing);

        isFinished = true;
    }

    // ќбработчик событий
    private void OnNodeLoaded(OsmNode node)
    {
        // ‘ильтраци€: обрабатываем только детали
        if (node.objectType != BaseOsm.ObjectType.Detail) return;

        // «апускаем создание (метод сам проверит дубликаты)
        StartCoroutine(ProcessTransportCoroutine(node));
    }

    private IEnumerator ProcessTransportCoroutine(OsmNode node)
    {
        node.AddField("source_type", "node");
        CreateTransportElement(node);
        yield return null;
    }

    private void OnDestroy()
    {
        if (MapReader.Instance != null)
        {
            MapReader.Instance.OnNodeLoaded -= OnNodeLoaded;
        }
    }

    private void CreateTransportElement(OsmNode geo)
    {
        // «ащита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        if (!geo.HasField("public_transport"))
        {
            return;
        }

        m_countProcessing++;

        var obj_type = geo.GetValueStringByKey("public_transport");

        var searchname = obj_type + " " + geo.ID.ToString();

        // ќѕ“»ћ»«ј÷»я: Ѕезопасна€ проверка contentselector
        if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        if (obj_type.Equals("stop_position"))
        {
            CreateStopPosition(geo, searchname);
        }
        else if (obj_type.Equals("platform"))
        {
            CreatePlatform(geo, searchname);
        }
        else
        {
            Debug.Log("Unsupported transport type: " + searchname);
        }
    }

    private void SetProperties(BaseOsm geo, BaseDataObject curObject)
    {
        var obj_type = geo.GetValueStringByKey("public_transport");

        curObject.name = obj_type + " " + geo.ID.ToString();

        if (geo.HasField("name"))
            curObject.Name = geo.GetValueStringByKey("name");

        curObject.Id = geo.ID.ToString();

        if (geo.HasField("source_type"))
            curObject.Source = geo.GetValueStringByKey("source_type");

        if (geo.HasField("layer"))
        {
            curObject.layer = geo.GetValueIntByKey("layer");
        }

        if (geo.HasField("direction"))
        {
            float direction = geo.GetValueFloatByKey("direction");

            curObject.transform.Rotate(0, direction, 0);
        }
    }

    private void CreatePlatform(OsmNode geo, string objName)
    {
        var platform = new GameObject(objName).AddComponent<TransportPlatform>();

        platform.itemlist = geo.itemlist;

        SetProperties(geo, platform);

        // ќѕ“»ћ»«ј÷»я: OsmNode - это одна точка, приводим к Vector3 напр€мую
        Vector3 nodeWorldPos = (Vector3)geo;

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированный WorldOrigin
        platform.transform.position = nodeWorldPos - cachedWorldOrigin;

        var transport_platform = Instantiate(platformPrefab, platform.transform.position, Quaternion.identity);

        transport_platform.transform.SetParent(platform.transform);

        platform.transform.position += Vector3.up * (platform.layer * BaseDataObject.layer_size);

        foreach (Transform child in platform.transform)
        {
            child.SendMessage("ActivateObject", null, SendMessageOptions.DontRequireReceiver);
        }

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированные настройки terrain
        if (cachedIsUseElevation)
        {
            StartCoroutine(SpawnInHeight(platform.gameObject, AlgorithmHeightSorting.AverageHeight));
        }

        platforms.Add(platform);
    }

    private void CreateStopPosition(OsmNode geo, string objName)
    {
        var stopposition = new GameObject(objName).AddComponent<TransportStopPosition>();

        stopposition.itemlist = geo.itemlist;

        SetProperties(geo, stopposition);

        // ќѕ“»ћ»«ј÷»я: OsmNode - это одна точка, приводим к Vector3 напр€мую
        Vector3 nodeWorldPos = (Vector3)geo;

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированный WorldOrigin
        stopposition.transform.position = nodeWorldPos - cachedWorldOrigin;

        stopposition.transform.position += Vector3.up * (stopposition.layer * BaseDataObject.layer_size);

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированные настройки terrain
        if (cachedIsUseElevation)
        {
            StartCoroutine(SpawnInHeight(stopposition.gameObject, AlgorithmHeightSorting.AverageHeight));
        }

        foreach (Transform child in stopposition.transform)
        {
            child.SendMessage("ActivateObject", null, SendMessageOptions.DontRequireReceiver);
        }

        stoppositions.Add(stopposition);
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }
}