using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

class DetailMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;

    public GameObject tempMarker;
    public DetailsTypes detailsTypes;
    public bool isAlwaysShowTempMarker;
    public bool isShowTempMarkerForeNotSetPrefab;
    public bool isClearUnusedData = false;
    public TileSystem tileSystem;

    void CreateTempMarker(Detail detail)
    {
        string Text = detail.Description + ": " + detail.Type;

        var go = Instantiate(tempMarker, detail.transform.position, Quaternion.identity);

        go.GetComponentInChildren<TMPro.TextMeshPro>().text = Text;

        go.transform.SetParent(detail.transform);
    }

    void CreateDetailPrefab(Detail detail, GameObject detailPrefab)
    {
        string Text = detail.Description + ": " + detail.Type;

        var go = Instantiate(detailPrefab, detail.transform.position, Quaternion.identity);

        go.transform.SetParent(detail.transform);
    }

    private void CheckAndAddCategory(BaseOsm geo, Detail detail, string keyword)
    {
        //Type parser
        if (geo.HasField(keyword) && detail.Type == "Undefined") //for sorting by order
        {
            detail.Description = keyword;
            detail.Type = geo.GetValueStringByKey(keyword);
        }
    }

    private void SetProperties(BaseOsm geo, Detail detail)
    {
        detail.name = "detail " + geo.ID.ToString();

        if (geo.HasField("name"))
            detail.Name = geo.GetValueStringByKey("name");

        detail.Id = geo.ID.ToString();

        if (geo.HasField("source_type"))
            detail.Source = geo.GetValueStringByKey("source_type");

        detail.Description = "Undefined";
        detail.Type = "Undefined";

        if (geo.HasField("layer"))
        {
            detail.layer = geo.GetValueIntByKey("layer");
        }

        //Type parser
        CheckAndAddCategory(geo, detail, "attraction");
        CheckAndAddCategory(geo, detail, "information");
        CheckAndAddCategory(geo, detail, "disused:amenity");
        CheckAndAddCategory(geo, detail, "disused:shop");
        CheckAndAddCategory(geo, detail, "playground");

        CheckAndAddCategory(geo, detail, "natural");
        CheckAndAddCategory(geo, detail, "man_made");
        CheckAndAddCategory(geo, detail, "historic");
        CheckAndAddCategory(geo, detail, "power");
        CheckAndAddCategory(geo, detail, "emergency");
        CheckAndAddCategory(geo, detail, "amenity");
        CheckAndAddCategory(geo, detail, "highway");
        CheckAndAddCategory(geo, detail, "traffic_calming");
        CheckAndAddCategory(geo, detail, "railway");
        CheckAndAddCategory(geo, detail, "barrier");
        CheckAndAddCategory(geo, detail, "shop");
        CheckAndAddCategory(geo, detail, "place");
        CheckAndAddCategory(geo, detail, "office");
        CheckAndAddCategory(geo, detail, "public_transport");
        CheckAndAddCategory(geo, detail, "noexit");
        CheckAndAddCategory(geo, detail, "entrance");
        CheckAndAddCategory(geo, detail, "was:shop"); //not use, old shop
        CheckAndAddCategory(geo, detail, "tourism");
        CheckAndAddCategory(geo, detail, "leisure");

        var typeName = detail.Description + ":" + detail.Type;

        var detailsInfo = detailsTypes.GetDetailsTypeInfoByName(typeName);

        if(detailsInfo.isTempMarkerEnable || isAlwaysShowTempMarker)
        {
            CreateTempMarker(detail);
        }

        if (detailsInfo.detailsPrefab != null)
        {
            CreateDetailPrefab(detail, detailsInfo.detailsPrefab);
        }
        else if(isShowTempMarkerForeNotSetPrefab)
        {
            CreateTempMarker(detail);
        }
    }

    void CreateDetails(OsmNode geo)
    {
        var searchname = "detail " + geo.ID.ToString();

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var detail = new GameObject(searchname).AddComponent<Detail>();

        detail.itemlist = geo.itemlist;

        SetProperties(geo, detail);

        Vector3 localOrigin = GetCentre(geo);
        detail.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                detail.transform.position = GR.getHeightPosition(detail.transform.position);
            }
            else
            {
                detail.transform.position += Vector3.up * tileSystem.fake_height;
            }
        }

        detail.transform.position += Vector3.up * (detail.layer * BaseDataObject.layer_size);

        foreach (Transform child in detail.transform)
        {
            child.SendMessage("ActivateObject", null, SendMessageOptions.DontRequireReceiver);
        }
    }

    // Start is called before the first frame update
    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        tileSystem = FindObjectOfType<TileSystem>();

        if (isClearUnusedData)
        {
            detailsTypes.DeleteUnused();
        }

        foreach (var node in map.nodeslist.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Detail; }))
        {
            node.AddField("source_type", "node");
            CreateDetails(node);
            yield return null;
        }

        isFinished = true;
    }
}
