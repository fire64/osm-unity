using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public class MapReader : MonoBehaviour
{
    [HideInInspector]
    public Dictionary<ulong, OsmNode> nodes;

    [HideInInspector]
    public List<OsmWay> ways;

    [HideInInspector]
    public List<OsmRelation> relations;

    [HideInInspector]
    public OsmBounds bounds;

    [Tooltip("The resource file that contains the OSM map data")]
    public string resourceFile;

    public bool isDebugDraw;
    public bool IsReady {get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        nodes = new Dictionary<ulong, OsmNode>();
        ways = new List<OsmWay>();
        relations = new List<OsmRelation>();

        var txtAsset = Resources.Load<TextAsset>(resourceFile);

        XmlDocument doc = new XmlDocument();
        doc.LoadXml(txtAsset.text);

        SetBounds(doc.SelectSingleNode("/osm/bounds"));
        GetNodes(doc.SelectNodes("/osm/node"));
        GetWays(doc.SelectNodes("osm/way"));
        GetRelations(doc.SelectNodes("osm/relation"));

        IsReady = true;
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
        }
    }

    void SetBounds(XmlNode xmlNode) 
    {
        bounds = new OsmBounds(xmlNode);
    }
}
 
