using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.IO;
using System.Linq;

/// <summary>
/// Расширенный MapReader с контролем динамической загрузки
/// 
/// Ключевые отличия от AsyncMapReader:
/// 1. Использует обычные коллекции (не Concurrent) - нет overhead на блокировки
/// 2. Не создаёт копии коллекций при обращении
/// 3. Динамическая загрузка включается/выключается галочкой isDynamicTileLoader
/// 4. Оптимизирован для стабильного FPS
/// </summary>
public class MapReader : MonoBehaviour
{
    public static MapReader Instance { get; private set; }

    public enum TypeMapLoad
    {
        TileOnly,
        OSMData,
        Overpass,
    };

    [Header("Main Settings")]
    [SerializeField] public LocationData LocationData;
    [SerializeField] public TypeMapLoad typeMapLoad;

    [Space]
    [SerializeField] public double latitude;
    [SerializeField] public double longitude;
    [SerializeField] public float radiusmeters; // Радиус ПЕРВОНАЧАЛЬНОЙ загрузки

    [Header("Dynamic Loading Settings")]
    [Tooltip("Включить динамическую подгрузку новых участков карты при движении игрока")]
    [SerializeField] public bool isDynamicTileLoader = false; // По умолчанию ВЫКЛЮЧЕНО

    [SerializeField] public Transform player;
    public int zoom = 15;
    public int radiusTiles = 2; // Радиус динамической подгрузки при движении
    public float dynamicLoadInterval = 1.0f; // Интервал проверки для динамической загрузки

    [Header("Cache Settings")]
    public string RelativeCachePath = "../CachedTileData/OSMData/";
    protected string CacheFolderPath;

    // Фиксированная точка отсчета мира
    public Vector3 WorldOrigin { get; private set; }
    public bool IsReady { get; private set; }

    // Прямой доступ к коллекциям БЕЗ создания копий
    [HideInInspector] public Dictionary<ulong, OsmNode> nodes;
    [HideInInspector] public List<OsmNode> nodeslist;
    [HideInInspector] public List<OsmWay> ways;
    [HideInInspector] public List<OsmRelation> relations;

    // Состояние загрузки
    private Dictionary<string, bool> loadedTiles = new Dictionary<string, bool>();

    // Флаг для остановки корутины динамической загрузки
    private Coroutine dynamicLoadCoroutine;
    private bool isDynamicLoadRunning;

    // События для генераторов
    public event Action<OsmNode> OnNodeLoaded;
    public event Action<OsmWay> OnWayLoaded;
    public event Action<OsmRelation> OnRelationLoaded;

    private void Awake()
    {
        Instance = this;

        // Инициализация коллекций
        nodes = new Dictionary<ulong, OsmNode>();
        nodeslist = new List<OsmNode>();
        ways = new List<OsmWay>();
        relations = new List<OsmRelation>();

        double originX = MercatorProjection.lonToX(longitude);
        double originY = MercatorProjection.latToY(latitude);
        WorldOrigin = new Vector3((float)originX, 0, (float)originY);
    }

    IEnumerator Start()
    {
#if UNITY_ANDROID || UNITY_IPHONE
        CacheFolderPath = Path.Combine(Application.persistentDataPath, RelativeCachePath);
#else
        CacheFolderPath = Path.Combine(Application.dataPath, RelativeCachePath);
#endif
        if (!Directory.Exists(CacheFolderPath))
            Directory.CreateDirectory(CacheFolderPath);

        if (LocationData != null)
        {
            latitude = LocationData.latitude;
            longitude = LocationData.longitude;
            radiusmeters = LocationData.radiusmeters;
        }

        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // --- ЭТАП 1: ПЕРВОНАЧАЛЬНАЯ ЗАГРУЗКА ---
        if (typeMapLoad != TypeMapLoad.TileOnly)
        {
            yield return StartCoroutine(InitialLoad());
            MarkInitialAreaAsLoaded();
        }

        IsReady = true;

        // --- ЭТАП 2: ДИНАМИЧЕСКАЯ ПОДГРУЗКА (только если включена) ---
        if (isDynamicTileLoader && typeMapLoad != TypeMapLoad.TileOnly)
        {
            StartDynamicLoading();
        }
    }

    /// <summary>
    /// Метод первоначальной загрузки (синхронный парсинг, как в стандартном MapReader)
    /// </summary>
    private IEnumerator InitialLoad()
    {
        var document = "mapdata_" + longitude + "_" + latitude + "_" + radiusmeters + ".txt";
        var tilePath = Path.Combine(CacheFolderPath, document);

        string result = "";

        if (File.Exists(tilePath))
        {
            byte[] fileData = File.ReadAllBytes(tilePath);
            result = Encoding.UTF8.GetString(fileData);
        }
        else
        {
            string url = "";

            double[] bbox = MercatorProjection.GetBoundingBox(longitude, latitude, radiusmeters);
            var minLon_str = bbox[0].ToString().Replace(',', '.');
            var minLat_str = bbox[1].ToString().Replace(',', '.');
            var maxLon_str = bbox[2].ToString().Replace(',', '.');
            var maxLat_str = bbox[3].ToString().Replace(',', '.');

            var centerLat_str = latitude.ToString().Replace(',', '.');
            var centerLon_str = longitude.ToString().Replace(',', '.');

            if (typeMapLoad == TypeMapLoad.OSMData)
            {
                url = "https://www.openstreetmap.org/api/0.6/map?bbox=" + minLon_str + "," + minLat_str + "," + maxLon_str + "," + maxLat_str;
            }
            else if (typeMapLoad == TypeMapLoad.Overpass)
            {
                url = "https://maps.mail.ru/osm/tools/overpass/api/interpreter?data=(nwr(around:" + (int)radiusmeters + "," + centerLat_str + "," + centerLon_str + ");>;);out;";
                Debug.Log("URL: " + url);
            }

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.SetRequestHeader("User-Agent", "UnityMapProject/1.0");
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    byte[] fileData = Encoding.UTF8.GetBytes(www.downloadHandler.text);
                    File.WriteAllBytes(tilePath, fileData);
                    result = www.downloadHandler.text;
                }
                else
                {
                    Debug.LogError($"Initial Load Failed: {www.error}");
                    yield break;
                }
            }
        }

        // Парсим полученный файл
        if (!string.IsNullOrEmpty(result))
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(result);
            ParseOsmData(doc);
        }
    }

    /// <summary>
    /// Помечаем тайлы, попавшие в начальный радиус, как загруженные
    /// </summary>
    private void MarkInitialAreaAsLoaded()
    {
        double[] bbox = MercatorProjection.GetBoundingBox(longitude, latitude, radiusmeters);
        double minLon = bbox[0];
        double maxLon = bbox[2];
        double minLat = bbox[1];
        double maxLat = bbox[3];

        int xMin = (int)MercatorProjection.lonToTileX(minLon, zoom);
        int xMax = (int)MercatorProjection.lonToTileX(maxLon, zoom);
        int yMax = (int)MercatorProjection.latToTileY(minLat, zoom);
        int yMin = (int)MercatorProjection.latToTileY(maxLat, zoom);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                string tileKey = $"{x}_{y}_{zoom}";
                if (!loadedTiles.ContainsKey(tileKey))
                {
                    loadedTiles.Add(tileKey, true);
                }
            }
        }
    }

    /// <summary>
    /// Парсинг OSM данных (вызывает события для генераторов)
    /// </summary>
    private void ParseOsmData(XmlDocument doc)
    {
        // Парсим ноды
        XmlNodeList nodeList = doc.SelectNodes("/osm/node");
        if (nodeList != null)
        {
            foreach (XmlNode n in nodeList)
            {
                OsmNode node = new OsmNode(n);
                if (!nodes.ContainsKey(node.ID))
                {
                    nodes[node.ID] = node;
                    nodeslist.Add(node);
                    OnNodeLoaded?.Invoke(node);
                }
            }
        }

        // Парсим ways
        XmlNodeList wayList = doc.SelectNodes("osm/way");
        if (wayList != null)
        {
            foreach (XmlNode node in wayList)
            {
                OsmWay way = new OsmWay(node);
                if (!ways.Any(w => w.ID == way.ID))
                {
                    ways.Add(way);
                    OnWayLoaded?.Invoke(way);
                }
            }
        }

        // Парсим relations
        XmlNodeList relationList = doc.SelectNodes("osm/relation");
        if (relationList != null)
        {
            foreach (XmlNode node in relationList)
            {
                OsmRelation relation = new OsmRelation(node, ways);
                if (!relations.Any(r => r.ID == relation.ID))
                {
                    relations.Add(relation);
                    OnRelationLoaded?.Invoke(relation);
                }
            }
        }
    }

    #region Динамическая загрузка

    /// <summary>
    /// Запустить динамическую загрузку
    /// </summary>
    public void StartDynamicLoading()
    {
        if (isDynamicLoadRunning) return;

        isDynamicTileLoader = true;
        isDynamicLoadRunning = true;
        dynamicLoadCoroutine = StartCoroutine(UpdateTilesLoop());
        Debug.Log("[MapReaderEx] Dynamic tile loading STARTED");
    }

    /// <summary>
    /// Остановить динамическую загрузку
    /// </summary>
    public void StopDynamicLoading()
    {
        isDynamicTileLoader = false;
        isDynamicLoadRunning = false;

        if (dynamicLoadCoroutine != null)
        {
            StopCoroutine(dynamicLoadCoroutine);
            dynamicLoadCoroutine = null;
        }
        Debug.Log("[MapReaderEx] Dynamic tile loading STOPPED");
    }

    /// <summary>
    /// Включить/выключить динамическую загрузку
    /// </summary>
    public void SetDynamicLoading(bool enabled)
    {
        if (enabled)
        {
            StartDynamicLoading();
        }
        else
        {
            StopDynamicLoading();
        }
    }

    /// <summary>
    /// Проверить, работает ли динамическая загрузка
    /// </summary>
    public bool IsDynamicLoadingActive() => isDynamicLoadRunning;

    /// <summary>
    /// Корутина динамической подгрузки тайлов
    /// </summary>
    private IEnumerator UpdateTilesLoop()
    {
        while (isDynamicTileLoader && isDynamicLoadRunning)
        {
            if (player != null)
            {
                yield return StartCoroutine(UpdateTilesCoroutine());
            }
            yield return new WaitForSeconds(dynamicLoadInterval);
        }
    }

    /// <summary>
    /// Проверка и загрузка нужных тайлов
    /// </summary>
    private IEnumerator UpdateTilesCoroutine()
    {
        Vector3 playerGlobalPos = player.position + WorldOrigin;

        double playerLon = MercatorProjection.xToLon(playerGlobalPos.x);
        double playerLat = MercatorProjection.yToLat(playerGlobalPos.z);

        int centerTileX = (int)MercatorProjection.lonToTileX(playerLon, zoom);
        int centerTileY = (int)MercatorProjection.latToTileY(playerLat, zoom);

        List<IEnumerator> loadCoroutines = new List<IEnumerator>();

        for (int x = -radiusTiles; x <= radiusTiles; x++)
        {
            for (int y = -radiusTiles; y <= radiusTiles; y++)
            {
                int tileX = centerTileX + x;
                int tileY = centerTileY + y;

                string tileKey = $"{tileX}_{tileY}_{zoom}";

                if (!loadedTiles.ContainsKey(tileKey))
                {
                    loadedTiles.Add(tileKey, true);
                    yield return StartCoroutine(LoadTileCoroutine(tileX, tileY, zoom));
                }
            }
        }
    }

    /// <summary>
    /// Загрузка отдельного тайла
    /// </summary>
    private IEnumerator LoadTileCoroutine(int x, int y, int zoom)
    {
        double minLon = MercatorProjection.tileXToLon(x, zoom);
        double maxLon = MercatorProjection.tileXToLon(x + 1, zoom);
        double minLat = MercatorProjection.tileYToLat(y + 1, zoom);
        double maxLat = MercatorProjection.tileYToLat(y, zoom);

        var minLon_str = minLon.ToString().Replace(',', '.');
        var maxLon_str = maxLon.ToString().Replace(',', '.');
        var minLat_str = minLat.ToString().Replace(',', '.');
        var maxLat_str = maxLat.ToString().Replace(',', '.');

        string fileName = $"tile_{typeMapLoad}_{x}_{y}_{zoom}.osm";
        string filePath = Path.Combine(CacheFolderPath, fileName);
        string url = "";

        if (typeMapLoad == TypeMapLoad.OSMData)
        {
            url = "https://www.openstreetmap.org/api/0.6/map?bbox=" + minLon_str + "," + minLat_str + "," + maxLon_str + "," + maxLat_str;
        }
        else if (typeMapLoad == TypeMapLoad.Overpass)
        {
            url = "https://maps.mail.ru/osm/tools/overpass/api/interpreter?data=(nwr(" + minLat_str + "," + minLon_str + "," + maxLat_str + "," + maxLon_str + ");>;);out;";
        }

        string xmlData = null;

        if (File.Exists(filePath))
        {
            xmlData = File.ReadAllText(filePath);
        }
        else
        {
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.SetRequestHeader("User-Agent", "UnityMapProject/1.0");
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    xmlData = www.downloadHandler.text;
                    File.WriteAllText(filePath, xmlData);
                }
                else
                {
                    Debug.LogWarning($"Failed to load dynamic tile {x},{y}. Error: {www.error}");
                    loadedTiles.Remove($"{x}_{y}_{zoom}");
                    yield break;
                }
            }
        }

        if (!string.IsNullOrEmpty(xmlData))
        {
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.LoadXml(xmlData);
                ParseOsmData(doc);
            }
            catch (Exception e)
            {
                Debug.LogError($"Parse Error dynamic tile {x},{y}: {e.Message}");
            }
        }
    }

    #endregion

    #region Утилиты

    /// <summary>
    /// Получить статистику загрузки
    /// </summary>
    public void LogStatistics()
    {
        Debug.Log($"[MapReaderEx] Statistics:\n" +
                  $"  Dynamic Loading: {isDynamicTileLoader} (running: {isDynamicLoadRunning})\n" +
                  $"  Nodes: {nodes.Count}\n" +
                  $"  Ways: {ways.Count}\n" +
                  $"  Relations: {relations.Count}\n" +
                  $"  Tiles Loaded: {loadedTiles.Count}");
    }

    /// <summary>
    /// Получить количество загруженных тайлов
    /// </summary>
    public int GetLoadedTilesCount() => loadedTiles.Count;

    /// <summary>
    /// Очистить данные динамически загруженных тайлов (кроме первоначальной области)
    /// </summary>
    public void ClearDynamicData()
    {
        // Сохраняем ключи начальной области
        HashSet<string> initialTiles = new HashSet<string>();
        double[] bbox = MercatorProjection.GetBoundingBox(longitude, latitude, radiusmeters);

        int xMin = (int)MercatorProjection.lonToTileX(bbox[0], zoom);
        int xMax = (int)MercatorProjection.lonToTileX(bbox[2], zoom);
        int yMax = (int)MercatorProjection.latToTileY(bbox[1], zoom);
        int yMin = (int)MercatorProjection.latToTileY(bbox[3], zoom);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                initialTiles.Add($"{x}_{y}_{zoom}");
            }
        }

        // Удаляем тайлы, не входящие в начальную область
        List<string> toRemove = new List<string>();
        foreach (var key in loadedTiles.Keys)
        {
            if (!initialTiles.Contains(key))
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            loadedTiles.Remove(key);
        }

        Debug.Log($"[MapReaderEx] Cleared {toRemove.Count} dynamic tiles");
    }

    /// <summary>
    /// Принудительно загрузить область по координатам
    /// </summary>
    public void ForceLoadArea(double minLon, double minLat, double maxLon, double maxLat)
    {
        StartCoroutine(ForceLoadAreaCoroutine(minLon, minLat, maxLon, maxLat));
    }

    private IEnumerator ForceLoadAreaCoroutine(double minLon, double minLat, double maxLon, double maxLat)
    {
        int xMin = (int)MercatorProjection.lonToTileX(minLon, zoom);
        int xMax = (int)MercatorProjection.lonToTileX(maxLon, zoom);
        int yMin = (int)MercatorProjection.latToTileY(maxLat, zoom);
        int yMax = (int)MercatorProjection.latToTileY(minLat, zoom);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                string tileKey = $"{x}_{y}_{zoom}";
                if (!loadedTiles.ContainsKey(tileKey))
                {
                    loadedTiles.Add(tileKey, true);
                    yield return StartCoroutine(LoadTileCoroutine(x, y, zoom));
                }
            }
        }
    }

    #endregion
}
