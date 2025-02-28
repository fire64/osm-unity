using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class UndefinedDebugMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;

    public GameObject tempMarker;
    public bool isFixHeight = true;
    private void SetProperties(BaseOsm geo, Undefined undefined)
    {
        undefined.name = "undefined " + geo.ID.ToString();

        if (geo.HasField("name"))
            undefined.Name = geo.GetValueStringByKey("name");

        undefined.Id = geo.ID.ToString();

        undefined.Kind = "undefined";

        if (geo.HasField("source_type"))
            undefined.Source = geo.GetValueStringByKey("source_type");

        undefined.isClosed = geo.IsClosedPolygon;
    }

    void CreateTempMarker(Undefined undefined)
    {
        var go = Instantiate(tempMarker, undefined.transform.position, Quaternion.identity);

        if(undefined.isClosed)
        {
            go.GetComponentInChildren<TMPro.TextMeshPro>().text = "Undefined Polygon";
        }
        else
        {
            go.GetComponentInChildren<TMPro.TextMeshPro>().text = "Undefined Line";
        }

        go.transform.SetParent(undefined.transform);
    }

    void CreateUndefinedDebugObject(BaseOsm geo)
    {
        var searchname = "undefined " + geo.ID.ToString();

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var undefined = new GameObject(searchname).AddComponent<Undefined>();

        undefined.itemlist = geo.itemlist;

        SetProperties(geo, undefined);

        Vector3 localOrigin = GetCentre(geo);
        undefined.transform.position = localOrigin - map.bounds.Centre;

        if (isFixHeight)
        {
            undefined.transform.position = GR.getHeightPosition(undefined.transform.position);
        }

        CreateTempMarker(undefined);
    }

    // Start is called before the first frame update
    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        foreach (var way in map.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Undefined && w.NodeIDs.Count > 1; }))
        {
            way.AddField("source_type", "way");
            CreateUndefinedDebugObject(way);
            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Undefined && w.NodeIDs.Count > 1; }))
        {
            relation.AddField("source_type", "relation");
            CreateUndefinedDebugObject(relation);
            yield return null;
        }
    }


}
