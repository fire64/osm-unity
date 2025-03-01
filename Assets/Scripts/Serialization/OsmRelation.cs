using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public class OsmRelation : BaseOsm
{
    List<OsmWay> ways;

    OsmWay GetOsmWay(ulong WayId)
    {
        foreach (var geo in ways)
        {
            if (geo.ID == WayId)
            {
                return geo;
            }
        }

        return null;
    }

    public OsmRelation(XmlNode node, List<OsmWay> waysset)
    {
        NodeIDs = new List<ulong>();
        HolesNodeListsIDs = new List<List<ulong>>();

        ID = GetAttribute<ulong>("id", node.Attributes);
        Visible = GetAttribute<bool>("visible", node.Attributes);

        ways = waysset;

        XmlNodeList members = node.SelectNodes("member");

        foreach (XmlNode member in members)
        {
            string type = GetAttribute<string>("type", member.Attributes);
            ulong refNo = GetAttribute<ulong>("ref", member.Attributes);
            string role = GetAttribute<string>("role", member.Attributes);

            if (type == "way" && role == "outer")
            {
                OsmWay curWay = GetOsmWay(refNo);

                if (curWay != null)
                {
                    foreach (ulong nodeId in curWay.NodeIDs)
                    {
                        NodeIDs.Add(nodeId);
                    }
                }
            }
            else if (type == "way" && role == "inner")  //holes
            {
                OsmWay curWay = GetOsmWay(refNo);

                if (curWay != null)
                {
                    var holesNodesCur = new List<ulong>();

                    foreach (ulong nodeId in curWay.NodeIDs)
                    {
                        holesNodesCur.Add(nodeId);
                    }

                    HolesNodeListsIDs.Add(holesNodesCur);
                }
            }

        }

        // TODO: Determine what type of way this is; is it a road? / boudary etc.

        if (NodeIDs.Count > 1)
        {
            IsClosedPolygon = NodeIDs[0] == NodeIDs[NodeIDs.Count - 1];
        }

        XmlNodeList tags = node.SelectNodes("tag");

        itemlist = new Item[tags.Count];

        int i = 0;

        objectType = ObjectType.Undefined;

        foreach (XmlNode t in tags)
        {
            string key = GetAttribute<string>("k", t.Attributes);

            itemlist[i].key = key;
            itemlist[i].value = GetAttribute<string>("v", t.Attributes);

            DetectObjectType(t);

            i++;
        }
    }

}
