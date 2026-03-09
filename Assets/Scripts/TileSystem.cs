using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine.Networking;
using System;
using System.IO;

// Если у вас есть базовый класс InfrstructureBehaviour, замените : MonoBehaviour на : InfrstructureBehaviour
public class TileSystem : MonoBehaviour
{
    public int zoom = 16; // Уровень детализации тайлов

    public enum TileServices
    {
        OSM,
        Google_map,
        Mapbox,
        Hightmap,
    }

    public enum TileType
    {
        Plane,
        Mesh,
        Terrain
    }

    public TileServices TileService = TileServices.OSM;
    public TileType tileType = TileType.Plane;
    public Material terrainMaterial;

    [Header("Dynamic Loading Settings")]
    public Transform player; // Ссылка на игрока
    public int radiusTiles = 3; // Радиус прорисовки (3 = сетка 7x7 вокруг игрока)
    public float updateInterval = 0.5f; // Как часто проверять позицию игрока

    public bool isUseElevation = false;
    public string RelativeCachePath = "../CachedTileData/Images/";
    protected string CacheFolderPath;

    public float height_scale = 1.0f;
    public float fake_height = 9921.5f;

    public GrassSettings grassSettings;
    public int m_DetailRes = 1024;
    public int m_DetailPerPath = 16;

    // Словарь для хранения активных тайлов: "x_y" -> GameObject
    private Dictionary<string, GameObject> activeTiles = new Dictionary<string, GameObject>();

    public string GetTileURL(int x, int y, int z)
    {
        string url = "";

        if (TileService == TileServices.Google_map)
        {
            string tileServerURL = "https://mt1.google.com/vt/lyrs=s&x={0}&y={1}&z={2}";
            url = string.Format(tileServerURL, x, y, z);
        }
        else if (TileService == TileServices.Mapbox)
        {
            string tileServerURL = "https://a.tiles.mapbox.com/v4/mapbox.satellite/{0}/{1}/{2}.png?events=true&access_token=pk.eyJ1IjoiaGlnaGNsaWNrZXJzIiwiYSI6ImNrZHdveTAxZjQxOXoyenJvcjlldmpoejEifQ.0LKYqSO1cCQoVCWObvVB5w";
            url = string.Format(tileServerURL, z, x, y);
        }
        else if (TileService == TileServices.Hightmap)
        {
            string tileServerURL = "https://api.mapbox.com/v4/mapbox.terrain-rgb/{0}/{1}/{2}.pngraw?access_token=pk.eyJ1Ijoib2xlb3RpZ2VyIiwiYSI6ImZ2cllZQ3cifQ.2yDE9wUcfO_BLiinccfOKg";
            url = string.Format(tileServerURL, z, x, y);
        }
        else
        {
            string tileServerURL = "https://a.tile.openstreetmap.org/{0}/{1}/{2}.png";
            url = string.Format(tileServerURL, z, x, y);
        }

        return url;
    }

    /// <summary>
    /// Декодирование значений пикселей в высоту (в метрах)
    /// </summary>
    private double MapboxHeightFromColor(Color color)
    {
        float R = color.r * 255;
        float G = color.g * 255;
        float B = color.b * 255;

        return -10000 + ((R * 256 * 256 + G * 256 + B) * 0.1);
    }

    IEnumerator Start()
    {
        // 1. Ждем инициализации MapReader
        while (MapReader.Instance == null)
        {
            yield return null;
        }

        // 2. Инициализация путей
#if UNITY_ANDROID || UNITY_IPHONE
        CacheFolderPath = Path.Combine(Application.persistentDataPath, RelativeCachePath);
#else
        CacheFolderPath = Path.Combine(Application.dataPath, RelativeCachePath);
#endif
        if (!Directory.Exists(CacheFolderPath))
            Directory.CreateDirectory(CacheFolderPath);

        // 3. Поиск игрока, если не назначен вручную
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
            else
                Debug.LogWarning("TileSystem: Player not found. Assign manually or tag player as 'Player'.");
        }

        // 4. Запуск бесконечного цикла обновления тайлов
        StartCoroutine(UpdateTilesLoop());
    }

    /// <summary>
    /// Главный цикл динамической подгрузки
    /// </summary>
    IEnumerator UpdateTilesLoop()
    {
        while (true)
        {
            if (player != null && MapReader.Instance != null)
            {
                UpdateTileGrid();
            }

            // Ждем перед следующей проверкой
            yield return new WaitForSeconds(updateInterval);
        }
    }

    /// <summary>
    /// Определяет, какие тайлы должны быть видны, и запускает их загрузку/выгрузку
    /// </summary>
    private void UpdateTileGrid()
    {
        // 1. Определяем позицию игрока в мировых координатах Меркатора
        // player.position - это локальная позиция относительно WorldOrigin.
        // Чтобы получить абсолютные координаты Меркатора, прибавляем WorldOrigin.
        Vector3 playerGlobalPos = player.position + MapReader.Instance.WorldOrigin;

        // 2. Переводим в широту/долготу, а затем в индексы тайлов
        double playerLon = MercatorProjection.xToLon(playerGlobalPos.x);
        double playerLat = MercatorProjection.yToLat(playerGlobalPos.z);

        int currentTileX = (int)MercatorProjection.lonToTileX(playerLon, zoom);
        int currentTileY = (int)MercatorProjection.latToTileY(playerLat, zoom);

        // 3. Список тайлов, которые должны быть активны в этом кадре
        HashSet<string> tilesToKeep = new HashSet<string>();

        // 4. Проходим по сетке вокруг игрока
        for (int x = -radiusTiles; x <= radiusTiles; x++)
        {
            for (int y = -radiusTiles; y <= radiusTiles; y++)
            {
                int tileX = currentTileX + x;
                int tileY = currentTileY + y;
                string tileKey = tileX + "_" + tileY;

                tilesToKeep.Add(tileKey);

                // Если тайл еще не создан, запускаем загрузку
                if (!activeTiles.ContainsKey(tileKey))
                {
                    activeTiles.Add(tileKey, null); // Заглушка, чтобы не запускать загрузку дважды
                    StartCoroutine(LoadTileRoutine(tileX, tileY, zoom));
                }
            }
        }

        // 5. Удаляем тайлы, которые далеко от игрока
        List<string> keysToRemove = new List<string>();
        foreach (var kvp in activeTiles)
        {
            if (!tilesToKeep.Contains(kvp.Key))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (activeTiles[key] != null)
            {
                Destroy(activeTiles[key]);
            }
            activeTiles.Remove(key);
            // Debug.Log($"Unloaded tile: {key}");
        }
    }

    IEnumerator LoadTileRoutine(int x, int y, int z)
    {
        string tileKey = x + "_" + y;
        string url = GetTileURL(x, y, z);

        var image = "map_" + TileService.ToString() + "_" + x + "_" + y + "_" + z + ".jpg";
        var imagePath = Path.Combine(CacheFolderPath, image);

        Texture2D texture = new Texture2D(512, 512, TextureFormat.DXT5, false);
        bool isTextureLoad = false;

        // --- Загрузка текстуры ---
        if (File.Exists(imagePath))
        {
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(imagePath);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                isTextureLoad = true;
            }
        }
        else
        {
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                byte[] bytes = www.downloadHandler.data;
                File.WriteAllBytes(imagePath, bytes);
                isTextureLoad = true;
            }
        }

        if (isTextureLoad)
        {
            // Вычисляем позицию и размер
            Vector2 tileSize = GetTileSizeInUnits(x, y, zoom);
            Vector3 tilePosition = GetTilePosition(x, y, zoom);
            GameObject tileGO = null;

            // --- Создание объекта ---
            switch (tileType)
            {
                case TileType.Plane:
                    tileGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    tileGO.layer = LayerMask.NameToLayer("Ground");
                    tileGO.transform.localScale = new Vector3(tileSize.x / 10.0f, 1, tileSize.y / 10.0f);
                    tileGO.transform.Rotate(0, 180, 0);
                    break;

                case TileType.Mesh:
                    tileGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    tileGO.layer = LayerMask.NameToLayer("Ground");
                    tileGO.transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);
                    tileGO.transform.Rotate(90, 0, 0);
                    break;

                case TileType.Terrain:
                    const int heghsize = 512;
                    const int heightmapResolution = heghsize + 1;
                    const int max_height_size = 10994 + 8849;

                    TerrainData terrainData = new TerrainData();
                    terrainData.heightmapResolution = heightmapResolution;
                    terrainData.size = new Vector3(tileSize.x, max_height_size, tileSize.y);
                    float[,] tileHeights = new float[terrainData.heightmapResolution, terrainData.heightmapResolution];
                    terrainData.SetHeights(0, 0, tileHeights);

                    terrainData.SetDetailResolution(m_DetailRes, m_DetailPerPath);

                    // Настройка травы и деревьев (если GrassSettings доступен)
                    if (grassSettings != null)
                    {
                        int detailcount = grassSettings.GetCountGrass();
                        if (detailcount > 0)
                        {
                            DetailPrototype[] m_detailProtoTypes = new DetailPrototype[detailcount];
                            for (int i = 0; i < detailcount; i++)
                                m_detailProtoTypes[i] = grassSettings.GetGrassById(i);
                            terrainData.detailPrototypes = m_detailProtoTypes;
                        }

                        int count_treetype = grassSettings.GetCountTrees();
                        if (count_treetype > 0)
                        {
                            TreePrototype[] m_treeProtoTypes = new TreePrototype[count_treetype];
                            for (int ind = 0; ind < count_treetype; ind++)
                                m_treeProtoTypes[ind] = grassSettings.GetTreeById(ind);
                            terrainData.treePrototypes = m_treeProtoTypes;
                        }
                    }

                    if (isUseElevation)
                    {
                        // Загрузка карты высот (Mapbox Terrain RGB)
                        string tileServerURL = "https://api.mapbox.com/v4/mapbox.terrain-rgb/{0}/{1}/{2}@2x.pngraw?access_token=pk.eyJ1Ijoib2xlb3RpZ2VyIiwiYSI6ImZ2cllZQ3cifQ.2yDE9wUcfO_BLiinccfOKg";
                        var url2 = string.Format(tileServerURL, z, x, y);

                        var height_image = "height_" + x + "_" + y + "_" + z + ".png";
                        var height_imagePath = Path.Combine(CacheFolderPath, height_image);

                        Texture2D heightmapTexture = new Texture2D(512, 512, TextureFormat.DXT5, false);
                        bool isHeightmapTextureLoad = false;

                        if (File.Exists(height_imagePath))
                        {
                            UnityWebRequest wwwH = UnityWebRequestTexture.GetTexture(height_imagePath);
                            yield return wwwH.SendWebRequest();
                            if (wwwH.result == UnityWebRequest.Result.Success)
                            {
                                heightmapTexture = ((DownloadHandlerTexture)wwwH.downloadHandler).texture;
                                isHeightmapTextureLoad = true;
                            }
                        }
                        else
                        {
                            UnityWebRequest wwwH = UnityWebRequestTexture.GetTexture(url2);
                            yield return wwwH.SendWebRequest();
                            if (wwwH.result == UnityWebRequest.Result.Success)
                            {
                                heightmapTexture = ((DownloadHandlerTexture)wwwH.downloadHandler).texture;
                                File.WriteAllBytes(height_imagePath, wwwH.downloadHandler.data);
                                isHeightmapTextureLoad = true;
                            }
                        }

                        if (isHeightmapTextureLoad)
                        {
                            Color[] pixels = heightmapTexture.GetPixels();
                            var heights = new double[heightmapResolution, heightmapResolution];
                            for (int xl = 0; xl < heightmapResolution; xl++)
                            {
                                for (int yl = 0; yl < heightmapResolution; yl++)
                                {
                                    int xlFix = (xl == heghsize) ? xl - 1 : xl;
                                    int ylFix = (yl == heghsize) ? yl - 1 : yl;
                                    heights[xl, yl] = MapboxHeightFromColor(pixels[xlFix + ylFix * heghsize]);
                                }
                            }

                            for (int yl = 0; yl < heightmapResolution; yl++)
                            {
                                for (int xl = 0; xl < heightmapResolution; xl++)
                                {
                                    tileHeights[yl, xl] = (float)((heights[xl, yl] - 0) / max_height_size) * height_scale;
                                }
                            }
                            terrainData.SetHeights(0, 0, tileHeights);
                        }
                    }
                    else
                    {
                        // Плоский террейн
                        for (int xl = 0; xl < terrainData.heightmapResolution; xl++)
                            for (int yl = 0; yl < terrainData.heightmapResolution; yl++)
                                tileHeights[yl, xl] = 0.5f;

                        terrainData.SetHeights(0, 0, tileHeights);
                        fake_height = max_height_size * 0.5f;
                    }

                    tileGO = Terrain.CreateTerrainGameObject(terrainData);
                    tileGO.layer = LayerMask.NameToLayer("Ground");

                    // Вращение и масштаб террейна
                    tileGO.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                    tileGO.transform.rotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));

                    Terrain t = tileGO.GetComponent<Terrain>();
                    t.treeDistance = 500.0f;
                    t.treeBillboardDistance = 1000f;
                    t.allowAutoConnect = true;
                    t.heightmapMaximumLOD = 3;

                    TerrainCollider tCollider = tileGO.GetComponent<TerrainCollider>();
                    tCollider.terrainData = terrainData;

                    Material newMaterial = new Material(Shader.Find("HDRP/Lit"));
                    newMaterial.mainTexture = texture;
                    newMaterial.color = new Color(1f, 1f, 1f, 1f);
                    t.materialTemplate = newMaterial;

                    // Корректировка позиции для террейна (его пивот в углу, а не в центре)
                    tilePosition -= new Vector3(terrainData.size.x * 0.5f, 0, terrainData.size.z * 0.5f);
                    break;
            }

            // Финальная установка позиции и имени
            if (tileGO != null)
            {
                tileGO.name = $"Tile_{x}_{y}_{z}";

                // Центрируем тайл относительно WorldOrigin
                // tilePosition уже вычислен как "Меркатор - WorldOrigin"
                tileGO.transform.position = tilePosition + Vector3.down * fake_height;

                // Для Plane/Quad назначаем материал
                if (tileType != TileType.Terrain)
                {
                    Renderer renderer = tileGO.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = new Material(Shader.Find("HDRP/Lit"));
                        renderer.material.mainTexture = texture;
                    }
                }

                // Обновляем запись в словаре (заменяем null на реальный объект)
                if (activeTiles.ContainsKey(tileKey))
                {
                    activeTiles[tileKey] = tileGO;
                }
                else
                {
                    // На случай, если тайл был выгружен во время загрузки
                    Destroy(tileGO);
                }
            }
        }
        else
        {
            // Если загрузка не удалась, удаляем заглушку из словаря
            if (activeTiles.ContainsKey(tileKey) && activeTiles[tileKey] == null)
            {
                activeTiles.Remove(tileKey);
            }
        }
    }

    /// <summary>
    /// Вычисляет позицию центра тайла в локальных координатах Unity (относительно WorldOrigin).
    /// </summary>
    Vector3 GetTilePosition(int x, int y, int zoom)
    {
        // Центр тайла в координатах Меркатора
        double centerLon = MercatorProjection.tileXToLon(x + 0.5, zoom);
        double centerLat = MercatorProjection.tileYToLat(y + 0.5, zoom);

        double[] worldPos = MercatorProjection.toPixel(centerLon, centerLat);

        // Преобразуем в локальные координаты: Мерактор - Фиксированный Origin
        Vector3 tilePosition = new Vector3((float)worldPos[0], -0.01f, (float)worldPos[1]) - MapReader.Instance.WorldOrigin;

        return tilePosition;
    }

    /// <summary>
    /// Вычисляет размер тайла в юнитах Unity.
    /// </summary>
    Vector2 GetTileSizeInUnits(int x, int y, int zoom)
    {
        // Берем углы тайла
        Vector3 p1 = GetTilePosition(x, y, zoom);
        Vector3 p2 = GetTilePosition(x + 1, y + 1, zoom);

        // Возвращаем разницу координат
        return new Vector2(Mathf.Abs(p2.x - p1.x), Mathf.Abs(p2.z - p1.z));
    }
}