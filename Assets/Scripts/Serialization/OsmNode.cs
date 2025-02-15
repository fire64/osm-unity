// removed unnecessary usings
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using static BaseOsm;

public class OsmNode : BaseOsm
{
    public float Latitude { get; private set; }

    public float Longitude { get; private set; }

    public float X { get; private set; }

    public float Y {get; private set; }

    // implicit conversion between OsmNode and Vector3
    public static implicit operator Vector3 (OsmNode node)
    {
        return new Vector3(node.X, 0, node.Y);
    }
    
    public OsmNode(XmlNode node)
    {
        NodeIDs = new List<ulong>();

        ID = GetAttribute<ulong>("id", node.Attributes);
        Latitude = GetAttribute<float>("lat", node.Attributes);
        Longitude = GetAttribute<float>("lon", node.Attributes);

        NodeIDs.Add(ID); //Hack for coordinates parser

        X = (float)MercatorProjection.lonToX(Longitude);
        Y = (float)MercatorProjection.latToY(Latitude);

        XmlNodeList tags = node.SelectNodes("tag");

        itemlist = new Item[tags.Count];

        int i = 0;

        objectType = ObjectType.Undefined;

        foreach (XmlNode t in tags)
        {
            string key = GetAttribute<string>("k", t.Attributes);

            itemlist[i].key = key;
            itemlist[i].value = GetAttribute<string>("v", t.Attributes);

            objectType = ObjectType.Detail;

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
