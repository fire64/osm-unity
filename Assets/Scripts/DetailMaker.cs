using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

class DetailMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;

    private void SetProperties(BaseOsm geo, Detail detail)
    {
        detail.name = "detail " + geo.ID.ToString();

        if (geo.HasField("name"))
            detail.Name = geo.GetValueStringByKey("name");

        detail.Id = geo.ID.ToString();
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
    }

    // Start is called before the first frame update
    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        foreach (var node in map.nodeslist.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Detail; }))
        {
            node.AddField("source_type", "node");
            CreateDetails(node);
            yield return null;
        }
    }
}
