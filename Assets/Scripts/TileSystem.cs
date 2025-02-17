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

    public TileServices TileService = TileServices.OSM;

    public float GetZoomScale(int zoomLevel)
    {
        double baseScale = 61.15;

        int referenceLevel = 16;

        return (float)(baseScale / Math.Pow(2, zoomLevel - referenceLevel));
    }

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

    IEnumerator DownloadTile(int x, int y, int z, Vector3 tilePosition)
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
            float zoomScale = GetZoomScale(zoom);

            Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            GameObject tileGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            tileGO.name = $"Tile_{x}_{y}_{z}";
            tileGO.transform.position = tilePosition;
            tileGO.transform.localScale = new Vector3(zoomScale, 1, zoomScale); //TODO: Add calculating for autoresizing...
            tileGO.GetComponent<Renderer>().material.mainTexture = texture;
            tileGO.transform.Rotate(0, 180, 0);
        }
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

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                // Calculate tile center in world coordinates
                double centerLon = MercatorProjection.tileXToLon(x + 0.5, zoom);
                double centerLat = MercatorProjection.tileYToLat(y + 0.5, zoom);

                double[] worldPos = MercatorProjection.toPixel(centerLon, centerLat);
                Vector3 tilePosition = new Vector3((float)worldPos[0], -0.01f, (float)worldPos[1]) - map.bounds.Centre;

                StartCoroutine(DownloadTile(x, y, zoom, tilePosition));
            }
        }
    }
}