using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class TransportElementsMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
    public TileSystem tileSystem;

    public GameObject platformPrefab;

    public List<TransportPlatform> platforms;
    public List<TransportStopPosition> stoppositions;

    private int m_countProcessing = 0;

    // Start is called before the first frame update
    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        tileSystem = FindObjectOfType<TileSystem>();

        platforms = new List<TransportPlatform>();
        stoppositions = new List<TransportStopPosition>();

        foreach (var node in map.nodeslist.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Detail; }))
        {
            node.AddField("source_type", "node");
            CreateTransportElement(node);
            yield return null;
        }

        isFinished = true;
    }

    private void CreateTransportElement(OsmNode geo)
    {
        if (!geo.HasField("public_transport"))
        {
            return;
        }

        m_countProcessing++;

        var obj_type = geo.GetValueStringByKey("public_transport");

        var searchname = obj_type + " " + geo.ID.ToString();

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        if(obj_type.Equals("stop_position"))
        {
            CreateStopPosition(geo, searchname);
        }
        else if(obj_type.Equals("platform"))
        {
            CreatePlatform(geo, searchname);
        }
        else
        {
            Debug.Log("Unsupported transport type: " + searchname);
        }
    }

    private void SetProperties(BaseOsm geo, BaseDataObject curObject)
    {
        var obj_type = geo.GetValueStringByKey("public_transport");

        curObject.name = obj_type + " " + geo.ID.ToString();

        if (geo.HasField("name"))
            curObject.Name = geo.GetValueStringByKey("name");

        curObject.Id = geo.ID.ToString();

        if (geo.HasField("source_type"))
            curObject.Source = geo.GetValueStringByKey("source_type");

        if (geo.HasField("layer"))
        {
            curObject.layer = geo.GetValueIntByKey("layer");
        }

        if (geo.HasField("direction"))
        {
            float direction = geo.GetValueFloatByKey("direction");

            curObject.transform.Rotate(0, direction, 0);
        }
    }

    private void CreatePlatform(OsmNode geo, string objName)
    {
        var platform = new GameObject(objName).AddComponent<TransportPlatform>();

        platform.itemlist = geo.itemlist;

        SetProperties(geo, platform);

        Vector3 localOrigin = GetCentre(geo);
        platform.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                platform.transform.position = GR.getHeightPosition(platform.transform.position);
            }
        }

        var transport_platform = Instantiate(platformPrefab, platform.transform.position, Quaternion.identity);

        transport_platform.transform.SetParent(platform.transform);

        platform.transform.position += Vector3.up * (platform.layer * BaseDataObject.layer_size);

        foreach (Transform child in platform.transform)
        {
            child.SendMessage("ActivateObject", null, SendMessageOptions.DontRequireReceiver);
        }

        platforms.Add(platform);
    }

    private void CreateStopPosition(OsmNode geo, string objName)
    {
        var stopposition = new GameObject(objName).AddComponent<TransportStopPosition>();

        stopposition.itemlist = geo.itemlist;

        SetProperties(geo, stopposition);

        Vector3 localOrigin = GetCentre(geo);
        stopposition.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                stopposition.transform.position = GR.getHeightPosition(stopposition.transform.position);
            }
        }

        stopposition.transform.position += Vector3.up * (stopposition.layer * BaseDataObject.layer_size);

        foreach (Transform child in stopposition.transform)
        {
            child.SendMessage("ActivateObject", null, SendMessageOptions.DontRequireReceiver);
        }

        stoppositions.Add(stopposition);
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }
}
