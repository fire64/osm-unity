using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine.Networking;
using System;

class TileSystem : InfrstructureBehaviour
{
    public int zoom = 16; // Zoom level for tiles

    public enum TileServices
    {
        OSM,
        Google_map,
        Mapbox,
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
        else
        {
            string tileServerURL = "https://a.tile.openstreetmap.org/{0}/{1}/{2}.png";
            url = string.Format(tileServerURL, z, x, y);
        }

        return url;
    }

    IEnumerator DownloadTile(int x, int y, int z, Vector3 tilePosition, Vector2 tileSize)
    {
        string url = GetTileURL(x, y, z);
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;

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

                    // Create the terrain data
                    TerrainData terrainData = new TerrainData();
                    terrainData.GetHeight(0, 0);
                    terrainData.heightmapResolution = 513;
                    terrainData.size = new Vector3(tileSize.x, 600, tileSize.y);
                    float[,] tileHeights = new float[terrainData.heightmapResolution, terrainData.heightmapResolution];

                    terrainData.SetHeights(0, 0, tileHeights);

                    //Create a terrain with the set terrain data
                    tileGO = Terrain.CreateTerrainGameObject(terrainData);

                    tileGO.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                    tileGO.transform.rotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));

                    //if Image Draw 
                    Terrain t = tileGO.GetComponent<Terrain>();
                    t.treeDistance = 500.0f;
                    t.treeBillboardDistance = 1000f;
                    t.allowAutoConnect = false;

                    TerrainCollider tCollider = tileGO.GetComponent<TerrainCollider>();

                    tCollider.terrainData = terrainData;

                    Material newMaterial = new Material(Shader.Find("Standard"));
                    newMaterial.mainTexture = new Texture2D(512, 512, TextureFormat.DXT5, false);
                    newMaterial.color = new Color(1f, 1f, 1f, 1f);
                    newMaterial.SetFloat("_Metallic", 1f);
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
    }
}