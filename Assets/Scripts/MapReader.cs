using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using UnityEngine.TextCore.Text;
using System.Text;
using System.IO;
using UnityEditor.Build.Content;
using System.Net;


public class MapReader : MonoBehaviour
{
    [HideInInspector]
    public Dictionary<ulong, OsmNode> nodes;

    [HideInInspector]
    public List<OsmNode> nodeslist;

    [HideInInspector]
    public List<OsmWay> ways;

    [HideInInspector]
    public List<OsmRelation> relations;

    [HideInInspector]
    public OsmBounds bounds;

    public double latitude;
    public double longitude;
    public float radiusmeters;

    public bool isDebugDraw;
    public bool IsReady {get; private set; }

    public string RelativeCachePath = "../CachedTileData/";
    protected string CacheFolderPath;

    public async void LoadOSMData(string url, string tilePath)
    {
        if (File.Exists(tilePath))
        {
            // Получаем данные
            byte[] fileData = File.ReadAllBytes(tilePath);

            string result = Encoding.UTF8.GetString(fileData);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(result);

            SetBounds(doc.SelectSingleNode("/osm/bounds"));
            GetNodes(doc.SelectNodes("/osm/node"));
            GetWays(doc.SelectNodes("osm/way"));
            GetRelations(doc.SelectNodes("osm/relation"));

            IsReady = true;
        }
        else
        {
                // Создаем запрос
                using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
                {
                    // Отправляем запрос асинхронно
                    var operation = webRequest.SendWebRequest();

                    // Ожидаем завершения запроса
                    while (!operation.isDone)
                        await Task.Yield();

                    // Проверяем на ошибки
                    if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                        webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogError($"Error: {webRequest.error}  for url: {url}");
                        return;
                    }

                    // Получаем данные
                    byte[] fileData = webRequest.downloadHandler.data;

                    await File.WriteAllBytesAsync(tilePath, fileData);

                    string result = Encoding.UTF8.GetString(fileData);

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(result);

                    SetBounds(doc.SelectSingleNode("/osm/bounds"));
                    GetNodes(doc.SelectNodes("/osm/node"));
                    GetWays(doc.SelectNodes("osm/way"));
                    GetRelations(doc.SelectNodes("osm/relation"));

                    IsReady = true;
                }

        }
    }

    // Start is called before the first frame update
    void Start()
    {
        nodes = new Dictionary<ulong, OsmNode>();
        ways = new List<OsmWay>();
        relations = new List<OsmRelation>();
        nodeslist = new List<OsmNode>();

#if UNITY_ANDROID || UNITY_IPHONE
            CacheFolderPath = Path.Combine(Application.persistentDataPath, RelativeCachePath);
#else
        CacheFolderPath = Path.Combine(Application.dataPath, RelativeCachePath);
#endif

        if (!Directory.Exists(CacheFolderPath))
            Directory.CreateDirectory(CacheFolderPath);

        double[] bbox = MercatorProjection.GetBoundingBox(longitude, latitude, radiusmeters);

        String minLon = bbox[0].ToString().Replace(",", ".");
        String minLat = bbox[1].ToString().Replace(",", ".");

        String maxLon = bbox[2].ToString().Replace(",", ".");
        String maxLat = bbox[3].ToString().Replace(",", ".");

        string dataURL = "https://www.openstreetmap.org/api/0.6/map?bbox=" + minLon + "," + minLat + "," + maxLon + "," + maxLat;

        var document = "mapdata_" + longitude + "_" + latitude + "_" + radiusmeters + ".txt";

        var tilePath = Path.Combine(CacheFolderPath, document);

        LoadOSMData(dataURL, tilePath);
    }

    void Update()
    {
        if (!isDebugDraw)
            return;

        foreach ( OsmWay w in ways)
        {
            if (w.Visible)
            {
                Color c = Color.cyan; // cyan for buildings
                if (!w.IsClosedPolygon) c = Color.red; // red for roads

                for (int i =1; i < w.NodeIDs.Count; i++)
                {
                    OsmNode p1 = nodes[w.NodeIDs[i - 1 ]];
                    OsmNode p2 = nodes[w.NodeIDs[i]];

                    Vector3 v1 = p1 - bounds.Centre;
                    Vector3 v2 = p2 - bounds.Centre;

                    Debug.DrawLine(v1, v2, c);
                }

            }
        }

        foreach (OsmRelation r in relations)
        {
            if (r.Visible)
            {
                Color c = Color.yellow; // yellow for buildings
                if (!r.IsClosedPolygon) c = Color.magenta; // magenta for roads

                for (int i = 1; i < r.NodeIDs.Count; i++)
                {
                    OsmNode p1 = nodes[r.NodeIDs[i - 1]];
                    OsmNode p2 = nodes[r.NodeIDs[i]];

                    Vector3 v1 = p1 - bounds.Centre;
                    Vector3 v2 = p2 - bounds.Centre;

                    Debug.DrawLine(v1, v2, c);
                }

            }
        }
    }

    void GetRelations(XmlNodeList xmlNodeList)
    {
        foreach (XmlNode node in xmlNodeList)
        {
            OsmRelation relation = new OsmRelation(node, ways);
            relations.Add(relation);
        }

    }

    void GetWays(XmlNodeList xmlNodeList)
    {
        foreach (XmlNode node in xmlNodeList)
        {
            OsmWay way = new OsmWay(node);
            ways.Add(way);
        }

    }

    void GetNodes(XmlNodeList xmlNodeList)
    {
        foreach (XmlNode n in xmlNodeList)
        {
            OsmNode node = new OsmNode(n);
            nodes[node.ID] = node;
            nodeslist.Add(node);
        }
    }

    void SetBounds(XmlNode xmlNode) 
    {
        bounds = new OsmBounds(xmlNode);
    }
}
 
