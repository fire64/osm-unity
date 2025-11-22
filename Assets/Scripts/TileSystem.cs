using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine.Networking;
using System;
using System.IO;
using static UnityEngine.Experimental.Rendering.RayTracingAccelerationStructure;

class TileSystem : InfrstructureBehaviour
{
    public int zoom = 16; // Zoom level for tiles

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
    public Material terrainMaterial; // Должен быть назначен материал для Terrain

    public bool isUseElevation = false;

    public string RelativeCachePath = "../CachedTileData/Images/";
    protected string CacheFolderPath;

    public float height_scale = 1.0f;

    public float fake_height = 9921.5f;

    public GrassSettings grassSettings;

    public int m_DetailRes = 1024;
    public int m_DetailPerPath = 16;

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
    /// Decode pixel values to height values. The height will be returned in meters
    /// </summary>
    /// <param name="color">The color with the encoded height</param>
    /// <returns>Height at location (meters)</returns>
    private double MapboxHeightFromColor(Color color)
    {
        // Convert from 0..1 to 0..255
        float R = color.r * 255;
        float G = color.g * 255;
        float B = color.b * 255;

        return -10000 + ( (R * 256 * 256 + G * 256 + B) * 0.1);
    }

    IEnumerator DownloadTile(int x, int y, int z, Vector3 tilePosition, Vector2 tileSize)
    {
        bool isTextureLoad = false;

        string url = GetTileURL(x, y, z);

        var image = "map_" + TileService.ToString() + "_" + x + "_" + y + "_" + z + ".jpg";

        var imagePath = Path.Combine(CacheFolderPath, image);

        Texture2D texture = new Texture2D(512, 512, TextureFormat.DXT5, false);

        if (File.Exists(imagePath))
        {
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(imagePath);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(www.error);
            }
            else
            {
                texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                isTextureLoad = true;
            }
        }
        else
        {
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(www.error);
            }
            else
            {
                texture = ((DownloadHandlerTexture)www.downloadHandler).texture;

                byte[] bytes = www.downloadHandler.data;
                File.WriteAllBytes(imagePath, bytes);

                isTextureLoad = true;
            }
        }

        if(isTextureLoad)
        {
            GameObject tileGO = null;
            Renderer renderer = null;

            switch (tileType)
            {
                case TileType.Plane:
                    tileGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    tileGO.transform.localScale = new Vector3(tileSize.x / 10.0f, 1, tileSize.y / 10.0f);
                    tileGO.transform.Rotate(0, 180, 0);
                    break;

                case TileType.Mesh:
                    tileGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    tileGO.transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);
                    tileGO.transform.Rotate(90, 0, 0);
                    break;

                case TileType.Terrain:

                    const int heghsize = 512;
                    const int heightmapResolution = heghsize + 1;
                    const int max_height_size = 10994 + 8849;

                    // Create the terrain data
                    TerrainData terrainData = new TerrainData();
                    terrainData.GetHeight(0, 0);
                    terrainData.heightmapResolution = heightmapResolution;
                    terrainData.size = new Vector3(tileSize.x, max_height_size, tileSize.y);
                    float[,] tileHeights = new float[terrainData.heightmapResolution, terrainData.heightmapResolution];

                    terrainData.SetHeights(0, 0, tileHeights);

                    terrainData.SetDetailResolution(m_DetailRes, m_DetailPerPath);

                    int detailcount = grassSettings.GetCountGrass();

                    DetailPrototype[] m_detailProtoTypes = new DetailPrototype[detailcount];

                    for (int i = 0; i < detailcount; i++)
                    {
                        m_detailProtoTypes[i] = grassSettings.GetGrassById(i);
                    }

                    terrainData.detailPrototypes = m_detailProtoTypes;

                    int count_treetype = grassSettings.GetCountTrees();

                    TreePrototype[] m_treeProtoTypes = new TreePrototype[count_treetype];

                    for (int ind = 0; ind < count_treetype; ind++)
                    {
                        m_treeProtoTypes[ind] = grassSettings.GetTreeById(ind);
                    }

                    terrainData.treePrototypes = m_treeProtoTypes;

                    if (isUseElevation)
                    {
                        string tileServerURL = "https://api.mapbox.com/v4/mapbox.terrain-rgb/{0}/{1}/{2}@2x.pngraw?access_token=pk.eyJ1Ijoib2xlb3RpZ2VyIiwiYSI6ImZ2cllZQ3cifQ.2yDE9wUcfO_BLiinccfOKg";
                        var url2 = string.Format(tileServerURL, z, x, y);

                        var height_image = "height_" + x + "_" + y + "_" + z + ".png";

                        var height_imagePath = Path.Combine(CacheFolderPath, height_image);

                        Texture2D heightmapTexture = new Texture2D(512, 512, TextureFormat.DXT5, false);

                        bool isHeightmapTextureLoad = false;

                        if (File.Exists(height_imagePath))
                        {
                            UnityWebRequest www = UnityWebRequestTexture.GetTexture(height_imagePath);
                            yield return www.SendWebRequest();

                            if (www.result != UnityWebRequest.Result.Success)
                            {
                                Debug.Log(www.error);
                            }
                            else
                            {
                                heightmapTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                                isHeightmapTextureLoad = true;
                            }
                        }
                        else
                        {
                            UnityWebRequest www = UnityWebRequestTexture.GetTexture(url2);
                            yield return www.SendWebRequest();

                            if (www.result != UnityWebRequest.Result.Success)
                            {
                                Debug.Log(www.error);
                            }
                            else
                            {
                                heightmapTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;

                                byte[] bytes = www.downloadHandler.data;
                                File.WriteAllBytes(height_imagePath, bytes);

                                isHeightmapTextureLoad = true;
                            }
                        }

                        if (isHeightmapTextureLoad)
                        {
                            Color[] pixels = heightmapTexture.GetPixels();

                            var heights = new double[heightmapResolution, heightmapResolution]; // +1 to prevent seams  

                            // Convert the encoded image into the respective heights
                            for (int xl = 0; xl < heightmapResolution; xl++)
                            {
                                for (int yl = 0; yl < heightmapResolution; yl++)
                                {
                                    int xlFix = xl;
                                    int ylFix = yl;

                                    if(xlFix == heghsize)
                                    {
                                        xlFix--;
                                    }

                                    if (ylFix == heghsize)
                                    {
                                        ylFix--;
                                    }

                                    heights[xl, yl] = MapboxHeightFromColor(pixels[xlFix + ylFix * heghsize]);
                                }
                            }

                            for (int yl = 0; yl < heightmapResolution; yl++)
                            {
                                for (int xl = 0; xl < heightmapResolution; xl++)
                                {
                                    // Get the elevation value and scale it to the 0..1 range
                                    tileHeights[yl, xl] = (float)((heights[xl, yl] - 0) / max_height_size) * height_scale;
                                }
                            }

                            terrainData.SetHeights(0, 0, tileHeights);
                        }
                    }
                    else
                    {
                    //    float[,] tileHeights = new float[terrainData.heightmapResolution, terrainData.heightmapResolution];

                        for( int xl = 0; xl < terrainData.heightmapResolution; xl++)
                        {
                            for (int yl = 0; yl < terrainData.heightmapResolution; yl++)
                            {
                                tileHeights[yl, xl] = 0.5f;
                            }
                        }

                        terrainData.SetHeights(0, 0, tileHeights);

                        fake_height = max_height_size * 0.5f;
                    }

                    //Create a terrain with the set terrain data
                    tileGO = Terrain.CreateTerrainGameObject(terrainData);

                    tileGO.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                    tileGO.transform.rotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));

                    //if Image Draw 
                    Terrain t = tileGO.GetComponent<Terrain>();
                    t.treeDistance = 500.0f;
                    t.treeBillboardDistance = 1000f;
                    t.allowAutoConnect = true;
                    t.heightmapMaximumLOD = 3;


                    TerrainCollider tCollider = tileGO.GetComponent<TerrainCollider>();

                    tCollider.terrainData = terrainData;

                    Material newMaterial = new Material(Shader.Find("Standard"));
                    newMaterial.mainTexture = new Texture2D(512, 512, TextureFormat.DXT5, false);
                    newMaterial.color = new Color(1f, 1f, 1f, 1f);
                    newMaterial.SetFloat("_Metallic", 0f);
                    newMaterial.SetFloat("_Glossiness", 0f);
                    t.materialTemplate = newMaterial;
                    t.materialTemplate.mainTexture = texture;

                    // Корректировка позиции для выравнивания
                    tilePosition -= new Vector3(terrainData.size.x * 0.5f, 0, terrainData.size.z * 0.5f);
                    break;
            } 

            if (tileGO != null)
            {
                tileGO.name = $"Tile_{x}_{y}_{z}";
                tileGO.transform.position = tilePosition;
                tileGO.transform.position += Vector3.down * fake_height;

                if (tileType != TileType.Terrain)
                {
                    renderer = tileGO.GetComponent<Renderer>();
                    renderer.material = new Material(Shader.Find("Standard"));
                    renderer.material.mainTexture = texture;
                }
            }
        }
    }

    Vector3 GetTilePosition(int x, int y, int zoom, Vector3 centr)
    {
        // Calculate tile center in world coordinates
        double centerLon = MercatorProjection.tileXToLon(x + 0.5, zoom);
        double centerLat = MercatorProjection.tileYToLat(y + 0.5, zoom);

        double[] worldPos = MercatorProjection.toPixel(centerLon, centerLat);
        Vector3 tilePosition = new Vector3((float)worldPos[0], -0.01f, (float)worldPos[1]) - centr;

        return tilePosition;
    }

    Vector2 GetTileSizeInUnits(int minX, int minY, int zoom, Vector3 centr)
    {
        Vector3 tilePositionXmin0 = GetTilePosition(minX, minY, zoom, centr);
        Vector3 tilePositionXmin1 = GetTilePosition(minX + 1, minY, zoom, centr);

        float xDistance = Vector3.Distance(tilePositionXmin0, tilePositionXmin1);

        Vector3 tilePositionYmin0 = GetTilePosition(minX, minY, zoom, centr);
        Vector3 tilePositionYmin1 = GetTilePosition(minX, minY + 1, zoom, centr);

        float YDistance = Vector3.Distance(tilePositionYmin0, tilePositionYmin1);

        return new Vector2(xDistance, YDistance);
    }

    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

#if UNITY_ANDROID || UNITY_IPHONE
            CacheFolderPath = Path.Combine(Application.persistentDataPath, RelativeCachePath);
#else
        CacheFolderPath = Path.Combine(Application.dataPath, RelativeCachePath);
#endif

        if (!Directory.Exists(CacheFolderPath))
            Directory.CreateDirectory(CacheFolderPath);

        // Calculate tile coordinates based on bounds
        double minLon = map.bounds.MinLon;
        double maxLon = map.bounds.MaxLon;
        double minLat = map.bounds.MinLat;
        double maxLat = map.bounds.MaxLat;

        int xPreMin = (int)MercatorProjection.lonToTileX(minLon, zoom);
        int xPreMax = (int)MercatorProjection.lonToTileX(maxLon, zoom);

        int xMin = Mathf.Min(xPreMin, xPreMax);
        int xMax = Mathf.Max(xPreMin, xPreMax);

        int yPreMin = (int)MercatorProjection.latToTileY(minLat, zoom);
        int yPreMax = (int)MercatorProjection.latToTileY(maxLat, zoom);

        int yMin = Mathf.Min(yPreMin, yPreMax);
        int yMax = Mathf.Max(yPreMin, yPreMax);

        Vector2 tileSize = GetTileSizeInUnits(xMin, yMin, zoom, map.bounds.Centre);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                // Calculate tile center in world coordinates
                Vector3 tilePosition = GetTilePosition( x, y, zoom, map.bounds.Centre);

                StartCoroutine(DownloadTile(x, y, zoom, tilePosition, tileSize));
            }
        }

        isFinished = true;
    }
}