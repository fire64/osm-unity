using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine.Networking;

class TileSystem : InfrstructureBehaviour
{
    public int zoom = 16; // Zoom level for tiles
    public string tileServerURL = "https://a.tile.openstreetmap.org/{0}/{1}/{2}.png"; // Example tile server URL

    IEnumerator DownloadTile(int x, int y, int z, Vector3 tilePosition)
    {
        string url = string.Format(tileServerURL, z, x, y);
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            GameObject tileGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            tileGO.name = $"Tile_{x}_{y}_{z}";
            tileGO.transform.position = tilePosition;
            tileGO.transform.localScale = new Vector3(61.15f, 1, 61.15f); //TODO: Add calculating for autoresizing...
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
                Vector3 tilePosition = new Vector3((float)worldPos[0], 0, (float)worldPos[1]) - map.bounds.Centre;


                StartCoroutine(DownloadTile(x, y, zoom, tilePosition));

            }
        }


    }


}