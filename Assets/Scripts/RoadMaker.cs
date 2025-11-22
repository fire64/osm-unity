using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using static RoadTypesInfo;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEngine.UI.GridLayoutGroup;

class RoadMaker : InfrstructureBehaviour
{
    public Material roadMaterial;
    public static GameContentSelector contentselector;

    public RoadTypesInfo roadTypes;
    public RoadSurfacesMaterials roadSurfacesMaterials;

    public RoadTypesInfo roadAreaTypes;
    public RoadSurfacesMaterials roadAreaSurfacesMaterials;

    public bool isCreateColision = false;
    public int MaxNodes = 150;
    public TileSystem tileSystem;
    public Material baseroadMaterial;

    private int m_countProcessing = 0;

    private float GetReoadWith(BaseOsm geo)
    {
        float roadwith = 0f;

        if (geo.HasField("width"))
        {
            roadwith = geo.GetValueFloatByKey("width");
        }
        else if (geo.HasField("width:carriageway"))
        {
            roadwith = geo.GetValueFloatByKey("width:carriageway");
        }
        else if (geo.HasField("width:lanes"))
        {
            string lanesWidthValue = geo.GetValueStringByKey("width:lanes");
            roadwith = CalculateWidthFromLanes(lanesWidthValue);
        }
        else
        {

        }

        return roadwith;
    }

    private float CalculateWidthFromLanes(string lanesWidthValue)
    {
        try
        {
            // Разделяем значения ширины полос (обычно разделены |)
            string[] laneWidths = lanesWidthValue.Split('|', ';');
            float totalWidth = 0f;
            int validCount = 0;

            foreach (string laneWidth in laneWidths)
            {
                if (float.TryParse(laneWidth.Trim(), out float width))
                {
                    totalWidth += width;
                    validCount++;
                }
            }

            // Если нашли валидные значения, возвращаем сумму
            return validCount > 0 ? totalWidth : 2.0f;
        }
        catch
        {
            // В случае ошибки возвращаем значение по умолчанию
            return 2.0f;
        }
    }

    private int GetLaneCount(BaseOsm geo)
    {
        int countlanes = 1;

        if (geo.HasField("lanes"))
        {
            countlanes = geo.GetValueIntByKey("lanes");
        }
        else if(geo.HasField("lanes:forward") && geo.HasField("lanes:backward"))
        {
            int forwadlines = geo.GetValueIntByKey("lanes:forward");
            int backwardlines = geo.GetValueIntByKey("lanes:backward");

            countlanes = forwadlines + backwardlines;
        }
        else
        {
            string highwayType = geo.GetValueStringByKey("highway", "");

            // Определение количества полос по умолчанию в зависимости от типа дороги
            switch (highwayType)
            {
                case "motorway":
                case "trunk":
                    countlanes = 2;
                    break;
                case "primary":
                case "secondary":
                    countlanes = IsOneWayRoad(geo) ? 2 : 1;
                    break;
                case "tertiary":
                case "unclassified":
                case "residential":
                    countlanes = 1;
                    break;
                default:
                    countlanes = 1;
                    break;
            }
        }

        // Гарантируем, что количество полос не меньше 1
        return Mathf.Max(1, countlanes);
    }

    private bool IsOneWayRoad(BaseOsm geo)
    {
        string onewayValue = geo.GetValueStringByKey("oneway", "no");

        // Проверка различных вариантов обозначения одностороннего движения
        return onewayValue == "yes" ||
               onewayValue == "true" ||
               onewayValue == "1" ||
               geo.HasField("junction") && geo.GetValueStringByKey("junction") == "roundabout";
    }

    private void SetProperties(BaseOsm geo, Road road)
    {
        road.name = "road " + geo.ID.ToString();

        if (geo.HasField("name"))
            road.Name = geo.GetValueStringByKey("name");

        road.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("area:highway"))
        {
            road.isArea = true;
        }
        else if (geo.HasField("type") && geo.GetValueStringByKey("type") == "multipolygon")
        {
            road.isArea = true;
        }

        if (geo.HasField("footway") && geo.GetValueStringByKey("footway").Equals("crossing"))
        {
            kind = "crossing";
        }
        else if (geo.HasField("highway"))
        {
            kind = geo.GetValueStringByKey("highway");
        }
        else if (geo.HasField("area:highway"))
        {
            kind = geo.GetValueStringByKey("area:highway");
        }
        else if (geo.HasField("railway"))
        {
            kind = "railway";
        }
        else
        {
            kind = "yes";
        }

        road.Kind = kind;

        if (geo.HasField("source_type"))
            road.Source = geo.GetValueStringByKey("source_type");

        road.lanes = GetLaneCount(geo);

        if (geo.HasField("layer"))
        {
            road.layer = geo.GetValueIntByKey("layer");
        }

        RoadTypeInfoItem roadInfo;

        if(road.isArea)
        {
            roadInfo = roadAreaTypes.GetRoadTypeInfoByName(road.Kind);
        }
        else
        {
            roadInfo = roadTypes.GetRoadTypeInfoByName(road.Kind);
        }

        Material surfaceMaterial = null;

        if (geo.HasField("surface"))
        {
            var surfaceName = geo.GetValueStringByKey("surface");

            if(road.isArea)
            {
                surfaceMaterial = roadAreaSurfacesMaterials.GetRoadSurfacesMaterialByName(surfaceName);
            }
            else
            {
                surfaceMaterial = roadSurfacesMaterials.GetRoadSurfacesMaterialByName(surfaceName);
            }
        }

        if (surfaceMaterial != null)
        {
            road.GetComponent<MeshRenderer>().material = surfaceMaterial;
        }
        else if(roadInfo.roadMaterial)
        {
            road.GetComponent<MeshRenderer>().material = roadInfo.roadMaterial;
        }
        else
        {
            road.GetComponent<MeshRenderer>().material = roadMaterial;
        }

        float width = GetReoadWith(geo);

        if(width != 0.0f)
        {
            road.width = width;
        }
        else if (roadInfo.roadWidth != 0.0f)
        {
            road.width = road.lanes * roadInfo.roadWidth;
        }
        else
        {
            road.width = road.lanes * 2.0f;
        }

        road.layersLevel = roadInfo.layersLevel;

        road.typeUsage = roadInfo.typeUsage;

        //       road.GetComponent<MeshRenderer>().material.SetColor("_Color", GR.SetOSMColour(geo)); //temporary disabe, for debug
    }

    Vector3 GetRoadHeight(Road road, ulong roadid)
    {
        // Базовая высота из уровня слоев дороги
        double height = 0.001f * road.layersLevel;

        // Генерация уникального смещения в диапазоне [0.0001, 0.0009]
        double idBasedOffset = 0.0001f + (float)((double)roadid / 1000000000 * 0.0008f);

        // Добавляем смещение к общей высоте
        height += idBasedOffset;

        Vector3 vec = new Vector3(0f, (float)height, 0f);

        return vec;
    }

    public void CalculateRoadEdges(List<Vector3> corners, float width, Road road)
    {
        if (corners.Count < 2)
            return;

        road.leftPoints = new List<Vector3>();
        road.rightPoints = new List<Vector3>();

        // Генерируем точки слева и справа с учётом соседних сегментов
        for (int i = 0; i < corners.Count; i++)
        {
            Vector3 dirPrev, dirNext;
            Vector3 cross;

            if (i == 0)
            {
                // Первая точка: используем следующий сегмент
                dirNext = (corners[i + 1] - corners[i]).normalized;
                cross = Vector3.Cross(dirNext, Vector3.up) * width;
            }
            else if (i == corners.Count - 1)
            {
                // Последняя точка: используем предыдущий сегмент
                dirPrev = (corners[i] - corners[i - 1]).normalized;
                cross = Vector3.Cross(dirPrev, Vector3.up) * width;
            }
            else
            {
                // Внутренние точки: вычисляем биссектрису направлений
                dirPrev = (corners[i] - corners[i - 1]).normalized;
                dirNext = (corners[i + 1] - corners[i]).normalized;

                Vector3 bisectorDir = dirPrev + dirNext;
                if (bisectorDir.magnitude < 0.001f)
                {
                    // Направления противоположны - используем перпендикуляр к dirPrev
                    cross = Vector3.Cross(dirPrev, Vector3.up) * width;
                }
                else
                {
                    bisectorDir.Normalize();
                    cross = Vector3.Cross(bisectorDir, Vector3.up) * width;
                }
            }

            road.leftPoints.Add(corners[i] + cross);
            road.rightPoints.Add(corners[i] - cross);
        }
    }

    void CreateRoads(BaseOsm geo)
    {
        var searchname = "road " + geo.ID.ToString();

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

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var road = new GameObject(searchname).AddComponent<Road>();

        road.AddComponent<MeshFilter>();
        road.AddComponent<MeshRenderer>();

        road.itemlist = geo.itemlist;

        SetProperties(geo, road);

        var roadsCorners = new List<Vector3>();

        Vector3 roadlayerHeight = GetRoadHeight(road, geo.ID);

        Vector3 localOrigin = GetCentre(geo);
        road.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                road.transform.position = GR.getHeightPosition(road.transform.position);
            }
        }

        road.transform.position += Vector3.up * 0.001f; //Fix for Landuse sort

        road.transform.position += roadlayerHeight;

        road.transform.position += Vector3.up * (road.layer * BaseDataObject.layer_size);

        road.coordpoints = new List<Vector3>();

        for (int i = 0; i < count; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            roadsCorners.Add(coords);

            Vector3 globalcoord = MercatorProjection.ConvertGeoToUntyCoord(point.Latitude, point.Longitude, map.bounds.Centre);

            road.coordpoints.Add(globalcoord);
        }

        var holesCorners = new List<List<Vector3>>();

        var countHoles = geo.HolesNodeListsIDs.Count;

        for (int i = 0; i < countHoles; i++)
        {
            var holeNodes = geo.HolesNodeListsIDs[i];

            var countHoleContourPoints = holeNodes.Count;

            // Создаем новый контур для каждого отверстия
            var holeContour = new List<Vector3>();

            for (int j = 0; j < countHoleContourPoints; j++)
            {
                OsmNode point = map.nodes[holeNodes[j]];
                Vector3 coords = point - localOrigin;
                holeContour.Add(coords);
            }

            holesCorners.Add(holeContour);
        }

        var mesh = road.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        if( road.isArea)
        {
            GR.CreateMeshWithHeight(roadsCorners, 0.0f, 0.0001f, tb, holesCorners, true, true);
        }
        else
        {
            GR.CreateMeshLineWithWidth(roadsCorners, road.width, tb);
        }

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.normals = tb.Normals.ToArray();
        mesh.SetUVs(0, tb.UV);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();

        road.corners = roadsCorners;

        CalculateRoadEdges(roadsCorners, road.width, road);
        road.InitializeLanes(); // Инициализируем систему полос

        //Add colider
        if (isCreateColision)
        {
            road.transform.gameObject.AddComponent<MeshCollider>();
            road.transform.GetComponent<MeshCollider>().sharedMesh = road.GetComponent<MeshFilter>().mesh;
            road.transform.GetComponent<MeshCollider>().convex = false;
        }

        if (geo.HasField("footway") && geo.GetValueStringByKey("footway") == "crossing")
        {
            int LayerIgnoreRaycast = LayerMask.NameToLayer("Crossing");
            road.gameObject.layer = LayerIgnoreRaycast;
        }
        else
        {
            int LayerIgnoreRaycast = LayerMask.NameToLayer("Roads");
            road.gameObject.layer = LayerIgnoreRaycast;
        }
    }

    IEnumerator Start()
    {        
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        tileSystem = FindObjectOfType<TileSystem>();

        foreach (var way in map.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Road; }))
        {
            way.AddField("source_type", "way");
            CreateRoads(way);
            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Road && w.IsClosedPolygon; }))
        {
            relation.AddField("source_type", "relation");
            CreateRoads(relation);
            yield return null;
        }

        isFinished = true;
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }

}
