using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using static GR;

class DetailMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;

    public GameObject tempMarker;
    public DetailsTypes detailsTypes;
    public bool isAlwaysShowTempMarker;
    public bool isShowTempMarkerForeNotSetPrefab;
    public bool isClearUnusedData = false;
    public bool isDebugNotSet = true;
    public TileSystem tileSystem;

    private int m_countProcessing = 0;
    // —писок дл€ отслеживани€ уже обработанных ID
    private HashSet<ulong> processedIDs = new HashSet<ulong>();

    // Ќовый массив дл€ исключени€ ключей
    public string[] excludeKeysForTextMarkers = Array.Empty<string>();

    // ============================================
    // ќѕ“»ћ»«ј÷»я: batchSize дл€ пакетной обработки
    // ============================================
    [Header("Optimization Settings")]
    [Tooltip(" оличество detail объектов обрабатываемых за один кадр")]
    public int batchSize = 20;

    // ============================================
    // ќѕ“»ћ»«ј÷»я:  эширование ссылок
    // ============================================
    private Vector3 cachedWorldOrigin;

    // ============================================
    // ќѕ“»ћ»«ј÷»я:  эширование HashSet дл€ исключений
    // ============================================
    private HashSet<string> excludeKeysSet;

    void CreateTempMarker(Detail detail)
    {
        // ќѕ“»ћ»«ј÷»я: »спользуем HashSet вместо Array.Exists
        if (excludeKeysSet != null && excludeKeysSet.Contains(detail.Description))
        {
            return;
        }

        string Text = detail.Description + ": " + detail.Type;

        if (isDebugNotSet)
            Debug.LogWarning("Not set detail: " + Text);

        var go = Instantiate(tempMarker, detail.transform.position, Quaternion.identity);

        go.GetComponentInChildren<TMPro.TextMeshPro>().text = Text;

        go.transform.SetParent(detail.transform);
    }

    void CreateDetailPrefab(Detail detail, GameObject detailPrefab)
    {
        var go = Instantiate(detailPrefab, detail.transform.position, Quaternion.identity);

        go.transform.SetParent(detail.transform);
    }

    private void CheckAndAddCategory(BaseOsm geo, Detail detail, string keyword)
    {
        //Type parser
        if (geo.HasField(keyword) && detail.Type == "Undefined") //for sorting by order
        {
            detail.Description = keyword;
            detail.Type = geo.GetValueStringByKey(keyword);
        }
    }

    private void SetProperties(BaseOsm geo, Detail detail)
    {
        detail.name = "detail " + geo.ID.ToString();

        if (geo.HasField("name"))
            detail.Name = geo.GetValueStringByKey("name");

        detail.Id = geo.ID.ToString();

        if (geo.HasField("source_type"))
            detail.Source = geo.GetValueStringByKey("source_type");

        detail.Description = "Undefined";
        detail.Type = "Undefined";

        if (geo.HasField("layer"))
        {
            detail.layer = geo.GetValueIntByKey("layer");
        }

        if (geo.HasField("direction"))
        {
            float direction = geo.GetValueFloatByKey("direction");

            detail.transform.Rotate(0, direction, 0);
        }

        //Type parser
        CheckAndAddCategory(geo, detail, "attraction");
        CheckAndAddCategory(geo, detail, "advertising");
        CheckAndAddCategory(geo, detail, "information");
        CheckAndAddCategory(geo, detail, "disused:amenity");
        CheckAndAddCategory(geo, detail, "disused:shop");
        CheckAndAddCategory(geo, detail, "playground");
        CheckAndAddCategory(geo, detail, "cemetery");
        CheckAndAddCategory(geo, detail, "natural");
        CheckAndAddCategory(geo, detail, "man_made");
        CheckAndAddCategory(geo, detail, "memorial");
        CheckAndAddCategory(geo, detail, "power");
        CheckAndAddCategory(geo, detail, "emergency");
        CheckAndAddCategory(geo, detail, "amenity");
        CheckAndAddCategory(geo, detail, "highway");
        CheckAndAddCategory(geo, detail, "traffic_calming");
        CheckAndAddCategory(geo, detail, "railway");
        CheckAndAddCategory(geo, detail, "barrier");
        CheckAndAddCategory(geo, detail, "shop");
        CheckAndAddCategory(geo, detail, "place");
        CheckAndAddCategory(geo, detail, "office");
        CheckAndAddCategory(geo, detail, "public_transport");
        CheckAndAddCategory(geo, detail, "noexit");
        CheckAndAddCategory(geo, detail, "entrance");
        CheckAndAddCategory(geo, detail, "was:shop"); //not use, old shop
        CheckAndAddCategory(geo, detail, "artwork_type");
        CheckAndAddCategory(geo, detail, "historic");
        CheckAndAddCategory(geo, detail, "tourism");
        CheckAndAddCategory(geo, detail, "leisure");
        CheckAndAddCategory(geo, detail, "traffic_sign");
        CheckAndAddCategory(geo, detail, "xmas:feature");

        var typeName = detail.Description + ":" + detail.Type;

        // ќѕ“»ћ»«ј÷»я: »спользуем оптимизированный метод поиска
        var detailsInfo = detailsTypes.GetDetailsTypeInfoByName(typeName);

        if (detailsInfo.isTempMarkerEnable || isAlwaysShowTempMarker)
        {
            CreateTempMarker(detail);
        }

        if (detailsInfo.detailsPrefab != null)
        {
            CreateDetailPrefab(detail, detailsInfo.detailsPrefab);
        }
        else if (isShowTempMarkerForeNotSetPrefab)
        {
            CreateTempMarker(detail);
        }
    }

    void CreateDetails(OsmNode geo)
    {
        // «ащита от дублей
        if (processedIDs.Contains(geo.ID)) return;
        processedIDs.Add(geo.ID);

        m_countProcessing++;

        // ѕропускаем остановки транспорта (как в оригинале)
        if (geo.HasField("public_transport"))
        {
            return;
        }

        var searchname = "detail " + geo.ID.ToString();

        if (contentselector != null && contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var detail = new GameObject(searchname).AddComponent<Detail>();

        detail.itemlist = geo.itemlist;

        SetProperties(geo, detail);

        // »«ћ≈Ќ≈Ќ»≈: ¬ычисл€ем позицию
        // OsmNode имеет не€вное приведение к Vector3 (X, 0, Y в координатах ћеркатора)
        Vector3 nodeWorldPos = (Vector3)geo;

        // ќѕ“»ћ»«ј÷»я: »спользуем кэшированный WorldOrigin
        detail.transform.position = nodeWorldPos - cachedWorldOrigin;

        detail.transform.position += Vector3.up * (detail.layer * BaseDataObject.layer_size);

        //  орректировка под Terrain
        if (tileSystem != null && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                StartCoroutine(SpawnInHeight(detail.gameObject, AlgorithmHeightSorting.CenterHeight));
            }
        }

        foreach (Transform child in detail.transform)
        {
            child.SendMessage("ActivateObject", null, SendMessageOptions.DontRequireReceiver);
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

        // ќѕ“»ћ»«ј÷»я:  эшируем WorldOrigin один раз при старте
        cachedWorldOrigin = MapReader.Instance.WorldOrigin;

        // ќѕ“»ћ»«ј÷»я: —оздаем HashSet дл€ быстрого поиска исключений
        if (excludeKeysForTextMarkers != null && excludeKeysForTextMarkers.Length > 0)
        {
            excludeKeysSet = new HashSet<string>(excludeKeysForTextMarkers);
        }

        // ќѕ“»ћ»«ј÷»я: »нициализируем словарь типов деталей
        if (detailsTypes != null)
        {
  //          detailsTypes.InitializeCache();
        }

        if (isClearUnusedData)
        {
            detailsTypes.DeleteUnused();
        }

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
                    CreateDetails(node);

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

        Debug.Log("Details create at: " + (endtime - starttime) + " | Total: " + m_countProcessing);

        isFinished = true;
    }

    // ќбработчик событий
    private void OnNodeLoaded(OsmNode node)
    {
        // ‘ильтраци€: обрабатываем только детали
        if (node.objectType != BaseOsm.ObjectType.Detail) return;

        // «апускаем создание (метод сам проверит дубликаты)
        StartCoroutine(ProcessDetailCoroutine(node));
    }

    private IEnumerator ProcessDetailCoroutine(OsmNode node)
    {
        node.AddField("source_type", "node");
        CreateDetails(node);
        yield return null;
    }

    private void OnDestroy()
    {
        if (MapReader.Instance != null)
        {
            MapReader.Instance.OnNodeLoaded -= OnNodeLoaded;
        }
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }
}
