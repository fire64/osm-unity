using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;

class RouteMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
    public TileSystem tileSystem;

    public List<Route> routes;

    private int m_countProcessing = 0;

    private void SetProperties(BaseOsm geo, Route route)
    {
        route.name = "route " + geo.ID.ToString();

        if (geo.HasField("name"))
            route.Name = geo.GetValueStringByKey("name");

        route.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("route"))
        {
            kind = geo.GetValueStringByKey("route");
        }
        else
        {
            kind = "yes";
        }

        route.Kind = kind;

        if (geo.HasField("source_type"))
            route.Source = geo.GetValueStringByKey("source_type");
     }

    void CreateRoutes(OsmRelation geo)
    {
        var searchname = "route " + geo.ID.ToString();

        m_countProcessing++;

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var route = new GameObject(searchname).AddComponent<Route>();

        route.AddComponent<MeshFilter>();
        route.AddComponent<MeshRenderer>();

        route.itemlist = geo.itemlist;
        route.memberslist = geo.memberslist;

        SetProperties(geo, route);

        route.stoppoints = new List<Vector3>();
        route.coordpoints = new List<Vector3>();
        route.platformsid = new List<ulong>();

        foreach (var member in geo.memberslist)
        {
            if(member.type == OSMTypes.Node && ( member.Role.Equals("stop_entry_only") || member.Role.Equals("stop") || member.Role.Equals("stop_exit_only")) )
            {
                OsmNode point = null;
                map.nodes.TryGetValue(member.GeoId, out point);

                if(point != null)
                {
                    Vector3 globalcoord = MercatorProjection.ConvertGeoToUntyCoord(point.Latitude, point.Longitude, map.bounds.Centre);

                    route.stoppoints.Add(globalcoord);
                }
            }

            if (member.type == OSMTypes.Node && (member.Role.Equals("platform_entry_only") || member.Role.Equals("platform") || member.Role.Equals("platform_exit_only")))
            {
                route.platformsid.Add(member.GeoId);
            }

            if (member.type == OSMTypes.Way)
            {
                OsmWay way = geo.GetOsmWay(member.GeoId);

                if(way != null)
                {
                    foreach (var nodeId in way.NodeIDs)
                    {
                        OsmNode point = null;
                        map.nodes.TryGetValue(nodeId, out point);

                        if (point != null)
                        {
                            Vector3 globalcoord = MercatorProjection.ConvertGeoToUntyCoord(point.Latitude, point.Longitude, map.bounds.Centre);

                            route.coordpoints.Add(globalcoord);
                        }
                    }
                }

            }
        }

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                route.transform.position = GR.getHeightPosition(route.transform.position);
            }
        }

        routes.Add(route);
    }

    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        tileSystem = FindObjectOfType<TileSystem>();

        routes = new List<Route>();

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Route; }))
        {
            relation.AddField("source_type", "relation");
            CreateRoutes(relation);
            yield return null;
        }

        isFinished = true;
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }
}
