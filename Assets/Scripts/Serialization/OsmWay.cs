using System.Collections.Generic;
// new
using System.Xml;

public class OsmWay : BaseOsm
{
    public OsmWay(XmlNode node)
    {
        NodeIDs = new List<ulong>();

        ID = GetAttribute<ulong>("id", node.Attributes);
        Visible = GetAttribute<bool>("visible", node.Attributes);

        XmlNodeList nds = node.SelectNodes("nd");
        foreach (XmlNode n in nds)
        {
            ulong refNo = GetAttribute<ulong>("ref", n.Attributes);
            NodeIDs.Add(refNo);
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

            if (key == "building" || key == "building:part" || key == "building:levels" || key == "building:min_level")
            {
                objectType = ObjectType.Building;
            }
            else if (key == "highway")
            {
                objectType = ObjectType.Road;
            }

            /** would preferably like to use only: 
            ** trunk roads
            ** primary roads
            ** secondary roads
            ** service roads
            */

            i++;
        }

    }
   
}
