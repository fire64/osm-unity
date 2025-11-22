using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.LookDev;

public enum LaneMarkingType
{
    None,
    Solid,
    Dashed,
    DoubleSolid,
    DoubleDashed,
    ZigZag, // Для автобусных остановок
    Dotted  // Для слияния/разделения полос
}

public class RoadMarkings : MonoBehaviour
{
    public string HighwayType = "unclassified";
    public int Lanes = 1;
    public bool IsOneWay;

    public int LanesForward;
    public int LanesBackward;

    public Material solidLineMaterial;
    public Material dashedLineMaterial;
    public Material doubleSolidMaterial;
    public Material doubleDashedMaterial;
    public Material zigZagMaterial;
    public Material dottedLineMaterial;

    public static GameContentSelector contentselector;

    private Road curroad;
    private List<GameObject> markingObjects = new List<GameObject>();
    private List<Material> dynamicMaterials = new List<Material>(); // Для хранения созданных материалов

    public bool isInitialised = false;

    public bool IsMarking()
    {
        return isInitialised;
    }

    public void CreateMakersForRoad(Road road)
    {
        if (isInitialised)
            return;

        contentselector = FindObjectOfType<GameContentSelector>();
        if (contentselector == null)
        {
            Debug.LogError("GameContentSelector not found!");
            return;
        }

        solidLineMaterial = contentselector.solidLineMaterial;
        dashedLineMaterial = contentselector.dashedLineMaterial;
        doubleSolidMaterial = contentselector.doubleSolidMaterial;
        doubleDashedMaterial = contentselector.doubleDashedMaterial;
        zigZagMaterial = contentselector.zigZagMaterial;
        dottedLineMaterial = contentselector.dottedLineMaterial;

        this.curroad = road;
        ClearMarkings();
        ParsingGeoTags();
        ProcessingLines();

        isInitialised = true;
    }

    private void ClearMarkings()
    {
        foreach (GameObject obj in markingObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        markingObjects.Clear();

        foreach (Material mat in dynamicMaterials)
        {
            if (mat != null)
            {
                DestroyImmediate(mat);
            }
        }
        dynamicMaterials.Clear();
    }

    private void ProcessingLines()
    {
        // Проверка наличия разметки
        if (curroad.HasField("lane_markings") && curroad.GetValueStringByKey("lane_markings") == "no")
        {
            return;
        }

        int countlines = Lanes;

        for (int i = 0; i < countlines; i++)
        {
            LaneMarkingType LeftMarking = LaneMarkingType.None;
            LaneMarkingType RightMarking = LaneMarkingType.None;

            // Левая разметка
            if (i == 0)
            {
                LeftMarking = LaneMarkingType.Solid;
            }

            // Правая разметка
            if (i == countlines - 1)
            {
                RightMarking = LaneMarkingType.Solid;
            }
            else if (IsOneWay)
            {
                RightMarking = LaneMarkingType.Dashed;
            }
            else if (i + 1 == LanesForward)
            {
                RightMarking = Lanes < 4 ? LaneMarkingType.Solid : LaneMarkingType.DoubleSolid;
            }
            else
            {
                RightMarking = LaneMarkingType.Dashed;
            }

            RenderLine(i, LeftMarking, RightMarking);
        }
    }

    private void RenderLine(int laneIndex, LaneMarkingType leftMarking, LaneMarkingType rightMarking)
    {
        if (curroad == null) return;

        List<Vector3> leftEdgePoints = curroad.GetLaneLeftPoints(laneIndex);
        List<Vector3> rightEdgePoints = curroad.GetLaneRightPoints(laneIndex);

        if (leftMarking != LaneMarkingType.None && leftEdgePoints != null && leftEdgePoints.Count > 1)
        {
            CreateLineRenderer(leftEdgePoints, leftMarking, $"Lane_{laneIndex}_LeftMarking");
        }

        if (rightMarking != LaneMarkingType.None && rightEdgePoints != null && rightEdgePoints.Count > 1)
        {
            CreateLineRenderer(rightEdgePoints, rightMarking, $"Lane_{laneIndex}_RightMarking");
        }

        if (leftMarking == LaneMarkingType.DoubleSolid || leftMarking == LaneMarkingType.DoubleDashed)
        {
            CreateDoubleLine(leftEdgePoints, leftMarking, laneIndex, true);
        }

        if (rightMarking == LaneMarkingType.DoubleSolid || rightMarking == LaneMarkingType.DoubleDashed)
        {
            CreateDoubleLine(rightEdgePoints, rightMarking, laneIndex, false);
        }
    }

    private void CreateLineRenderer(List<Vector3> points, LaneMarkingType markingType, string name)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(transform);
        lineObject.transform.localPosition = Vector3.zero;

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        markingObjects.Add(lineObject);

        ConfigureLineRenderer(lineRenderer, points, markingType);
    }

    private void ConfigureLineRenderer(LineRenderer lineRenderer, List<Vector3> points, LaneMarkingType markingType)
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());

        switch (markingType)
        {
            case LaneMarkingType.Solid:
                lineRenderer.material = solidLineMaterial;
                lineRenderer.startWidth = 0.15f;
                lineRenderer.endWidth = 0.15f;
                lineRenderer.textureMode = LineTextureMode.Tile;
                break;

            case LaneMarkingType.Dashed:
                Material dashedMat = new Material(dashedLineMaterial);
                dynamicMaterials.Add(dashedMat);
                lineRenderer.material = dashedMat;
                lineRenderer.startWidth = 0.30f;
                lineRenderer.endWidth = 0.30f;
                lineRenderer.textureMode = LineTextureMode.Stretch;

                float dashLength = CalculateLineLength(points);
                float cycleLength = 6f;
                dashedMat.mainTextureScale = new Vector2(dashLength / cycleLength, 1f);
                break;

            case LaneMarkingType.Dotted:
                lineRenderer.material = dottedLineMaterial;
                lineRenderer.startWidth = 0.1f;
                lineRenderer.endWidth = 0.1f;
                lineRenderer.textureMode = LineTextureMode.Tile;
                break;

            case LaneMarkingType.ZigZag:
                lineRenderer.material = zigZagMaterial;
                lineRenderer.startWidth = 0.2f;
                lineRenderer.endWidth = 0.2f;
                lineRenderer.textureMode = LineTextureMode.Tile;
                break;

            case LaneMarkingType.DoubleSolid:
                lineRenderer.material = solidLineMaterial;
                lineRenderer.startWidth = 0.15f;
                lineRenderer.endWidth = 0.15f;
                lineRenderer.textureMode = LineTextureMode.Tile;
                break;

            case LaneMarkingType.DoubleDashed:
                Material doubleDashedMat = new Material(dashedLineMaterial);
                dynamicMaterials.Add(doubleDashedMat);
                lineRenderer.material = doubleDashedMat;
                lineRenderer.startWidth = 0.15f;
                lineRenderer.endWidth = 0.15f;
                lineRenderer.textureMode = LineTextureMode.Stretch;
                float doubleDashLength = CalculateLineLength(points);
                doubleDashedMat.mainTextureScale = new Vector2(doubleDashLength / 6f, 1f);
                break;
        }

        // Смещение для предотвращения z-fighting
        Vector3[] positions = new Vector3[lineRenderer.positionCount];
        lineRenderer.GetPositions(positions);
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] += Vector3.up * 0.02f;
        }
        lineRenderer.SetPositions(positions);
    }

    private void CreateDoubleLine(List<Vector3> points, LaneMarkingType markingType, int laneIndex, bool isLeft)
    {
        if (points == null || points.Count < 2) return;

        List<Vector3> offsetPoints = new List<Vector3>();
        float offset = isLeft ? 0.2f : -0.2f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 direction;
            if (i > 0 && i < points.Count - 1)
            {
                Vector3 prevToCurrent = (points[i] - points[i - 1]).normalized;
                Vector3 currentToNext = (points[i + 1] - points[i]).normalized;
                direction = (prevToCurrent + currentToNext).normalized;
            }
            else if (i == 0)
            {
                direction = (points[i + 1] - points[i]).normalized;
            }
            else
            {
                direction = (points[i] - points[i - 1]).normalized;
            }

            Vector3 perpendicular = new Vector3(-direction.z, 0, direction.x).normalized;
            offsetPoints.Add(points[i] + perpendicular * offset);
        }

        string side = isLeft ? "Left" : "Right";
        CreateLineRenderer(offsetPoints, markingType, $"Lane_{laneIndex}_{side}Marking_Second");
    }

    private float CalculateLineLength(List<Vector3> points)
    {
        float length = 0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            length += Vector3.Distance(points[i], points[i + 1]);
        }
        return length;
    }

    public bool isLaneValid(int laneid)
    {
        return laneid >= 0 && laneid < Lanes;
    }

    public bool isLineBackward(int laneid)
    {
        if (!isLaneValid(laneid)) return false;
        return (laneid + 1) > LanesForward;
    }

    private void ParsingGeoTags()
    {
        Lanes = curroad.GetValueIntByKey("lanes", 1);

        if (curroad.HasField("oneway"))
        {
            IsOneWay = curroad.GetValueStringByKey("oneway").Equals("yes");
        }

        LanesForward = curroad.GetValueIntByKey("lanes:forward", -1);
        LanesBackward = curroad.GetValueIntByKey("lanes:backward", -1);

        if (LanesForward == -1 && LanesBackward == -1)
        {
            if (IsOneWay)
            {
                LanesForward = Lanes;
                LanesBackward = 0;
            }
            else
            {
                LanesForward = Lanes / 2;
                LanesBackward = Lanes - LanesForward;
            }
        }
        else if (LanesForward == -1)
        {
            LanesForward = Lanes - LanesBackward;
        }
        else if (LanesBackward == -1)
        {
            LanesBackward = Lanes - LanesForward;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (curroad == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < Lanes; i++)
        {
            DrawGizmosForLane(curroad.GetLaneLeftPoints(i));
            DrawGizmosForLane(curroad.GetLaneRightPoints(i));
        }
    }

    private void DrawGizmosForLane(List<Vector3> points)
    {
        if (points == null || points.Count < 2) return;
        for (int j = 0; j < points.Count - 1; j++)
        {
            Gizmos.DrawLine(points[j], points[j + 1]);
        }
    }
}