using System.Collections.Generic;
using UnityEngine;



public enum RoadUsageType
{
    Pedestrian,     // Пешеходная
    Automotive,     // Автомобильная
    Train,          // Для поездов
    Tram,           // Для трамваев
    Bicycle,        // Велосипедная
    Bus,            // Автобусная
    Emergency,      // Для спецтранспорта (скорая, пожарная)
    Taxi,           // Такси
    SharedLane,     // Совмещенное движение (например, вело+авто)
    HighOccupancy   // Для транспорта с большим количеством пассажиров
}

[RequireComponent(typeof(RoadMarkings))]

public class Road : BaseDataObject
{
    public int lanes;
    public int layersLevel;

    public float speedLimit = 15.0f;

    public RoadUsageType typeUsage;

    public List<Vector3> leftPoints;
    public List<Vector3> rightPoints;

    public List<Vector3> corners;

    public bool isArea;

    // Новые поля для системы полос
    private List<List<Vector3>> laneCenterPoints;
    private List<List<Vector3>> laneLeftPoints;
    private List<List<Vector3>> laneRightPoints;

    private bool isLinesInit = false;

    RoadMarkings roadmaker;

    /// <summary>
    /// Инициализирует систему полос дороги
    /// </summary>
    public void InitializeLanes()
    {
        if (isLinesInit)
            return;

        if(!roadmaker)
        {
            roadmaker = GetComponent<RoadMarkings>();
        }

        if (leftPoints == null || rightPoints == null || leftPoints.Count == 0)
        {
            Debug.LogWarning("Road edges not calculated. Call CalculateRoadEdges() first.");
            return;
        }

        laneCenterPoints = new List<List<Vector3>>();
        laneLeftPoints = new List<List<Vector3>>();
        laneRightPoints = new List<List<Vector3>>();

        for (int laneIndex = 0; laneIndex < lanes; laneIndex++)
        {
            List<Vector3> centerPoints = new List<Vector3>();
            List<Vector3> leftEdgePoints = new List<Vector3>();
            List<Vector3> rightEdgePoints = new List<Vector3>();

            for (int i = 0; i < leftPoints.Count; i++)
            {
                float t_center = (laneIndex + 0.5f) / lanes;
                float t_left = laneIndex / (float)lanes;
                float t_right = (laneIndex + 1) / (float)lanes;

                Vector3 center = Vector3.Lerp(rightPoints[i], leftPoints[i], t_center);
                Vector3 leftEdge = Vector3.Lerp(rightPoints[i], leftPoints[i], t_left);
                Vector3 rightEdge = Vector3.Lerp(rightPoints[i], leftPoints[i], t_right);

                centerPoints.Add(transform.TransformPoint(center));
                leftEdgePoints.Add(transform.TransformPoint(leftEdge));
                rightEdgePoints.Add(transform.TransformPoint(rightEdge));
            }

            laneCenterPoints.Add(centerPoints);
            laneLeftPoints.Add(leftEdgePoints);
            laneRightPoints.Add(rightEdgePoints);
        }

        if(isCanUseAutomobile())
        {
            roadmaker.CreateMakersForRoad(this);
        }

        isLinesInit = true;
    }

    public RoadMarkings GetRoadMarkings()
    {
        if(roadmaker != null)
        {
            return roadmaker;
        }

        return null;
    }

    public bool isLineForward(int laneid)
    {
        return roadmaker.isLineBackward(laneid) != true;
    }

    public float GetLaneWidth()
    {
        return width / lanes;
    }

    public Vector3 GetCenterPointByIdAndLane(int laneIndex, int pointid )
    {
        List<Vector3> points = GetLaneCenterPoints(laneIndex);

        if(pointid < 0 ||  pointid >= points.Count)
        {
            return Vector3.zero;
        }

        return points[pointid];
    }

    /// <summary>
    /// Возвращает глобальные координаты центра указанной полосы
    /// </summary>
    public List<Vector3> GetLaneCenterPoints(int laneIndex)
    {
        if (laneCenterPoints == null || laneCenterPoints.Count == 0)
        {
            InitializeLanes();
        }

        if (laneIndex < 0 || laneIndex >= laneCenterPoints.Count)
        {
            Debug.LogError($"Invalid lane index: {laneIndex}. Road has {laneCenterPoints.Count} lanes.");
            return new List<Vector3>();
        }

        return laneCenterPoints[laneIndex];
    }

    /// <summary>
    /// Возвращает глобальные координаты левого края указанной полосы
    /// </summary>
    public List<Vector3> GetLaneLeftPoints(int laneIndex)
    {
        if (laneLeftPoints == null || laneLeftPoints.Count == 0)
        {
            InitializeLanes();
        }

        if (laneIndex < 0 || laneIndex >= laneLeftPoints.Count)
        {
            Debug.LogError($"Invalid lane index: {laneIndex}. Road has {laneLeftPoints.Count} lanes. in Road {Id}");
            return new List<Vector3>();
        }

        return laneLeftPoints[laneIndex];
    }

    /// <summary>
    /// Возвращает глобальные координаты правого края указанной полосы
    /// </summary>
    public List<Vector3> GetLaneRightPoints(int laneIndex)
    {
        if (laneRightPoints == null || laneRightPoints.Count == 0)
        {
            InitializeLanes();
        }

        if (laneIndex < 0 || laneIndex >= laneRightPoints.Count)
        {
            Debug.LogError($"Invalid lane index: {laneIndex}. Road has {laneRightPoints.Count} lanes.");
            return new List<Vector3>();
        }

        return laneRightPoints[laneIndex];
    }

    /// <summary>
    /// Находит ближайшую полосу к указанной точке в мировых координатах
    /// </summary>
    public bool FindNearestLane(Vector3 worldPoint, out int laneIndex, out int pointIndex, out Vector3 nearestPoint, out float distance)
    {
        laneIndex = -1;
        pointIndex = -1;
        nearestPoint = Vector3.zero;
        distance = float.MaxValue;

        if (laneCenterPoints == null || laneCenterPoints.Count == 0)
        {
            InitializeLanes();
        }

        // Преобразуем мировые координаты в локальные
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        for (int i = 0; i < laneCenterPoints.Count; i++)
        {
            for (int j = 0; j < laneCenterPoints[i].Count - 1; j++)
            {
                // Проецируем точку на сегмент полосы
                Vector3 projectedPoint = ProjectPointOnSegment(
                    transform.InverseTransformPoint(laneCenterPoints[i][j]),
                    transform.InverseTransformPoint(laneCenterPoints[i][j + 1]),
                    localPoint
                );

                float currentDistance = Vector3.Distance(localPoint, projectedPoint);

                if (currentDistance < distance)
                {
                    distance = currentDistance;
                    laneIndex = i;
                    pointIndex = j;
                    nearestPoint = transform.TransformPoint(projectedPoint);
                }
            }
        }

        return laneIndex >= 0;
    }

    /// <summary>
    /// Возвращает направление движения для указанной полосы в указанной точке
    /// </summary>
    public Vector3 GetLaneDirection(int laneIndex, int pointIndex)
    {
        if (laneCenterPoints == null || laneCenterPoints.Count == 0)
        {
            InitializeLanes();
        }

        if (laneIndex < 0 || laneIndex >= laneCenterPoints.Count ||
            pointIndex < 0 || pointIndex >= laneCenterPoints[laneIndex].Count - 1)
        {
            Debug.LogError($"Invalid lane index or point index");
            return Vector3.forward;
        }

        return (laneCenterPoints[laneIndex][pointIndex + 1] - laneCenterPoints[laneIndex][pointIndex]).normalized;
    }

    public bool isCanUseAutomobile()
    {
        if (typeUsage == RoadUsageType.Automotive || typeUsage == RoadUsageType.SharedLane)
        {
            return true;
        }

        return false;
    }

    public bool isCanUsePedestrian()
    {
        if (typeUsage == RoadUsageType.Pedestrian || typeUsage == RoadUsageType.SharedLane)
        {
            return true;
        }

        return false;
    }

    public bool isCanUseTrain()
    {
        if (typeUsage == RoadUsageType.Train || typeUsage == RoadUsageType.SharedLane)
        {
            return true;
        }

        return false;
    }

    public bool isCanUseTram()
    {
        if (typeUsage == RoadUsageType.Tram || typeUsage == RoadUsageType.SharedLane)
        {
            return true;
        }

        return false;
    }

    public bool GetEdgesAtPoint(Vector3 worldPoint, out Vector3 leftEdge, out Vector3 rightEdge, out Vector3 roadDirection)
    {
        leftEdge = Vector3.zero;
        rightEdge = Vector3.zero;
        roadDirection = Vector3.zero;

        if (leftPoints == null || rightPoints == null || leftPoints.Count == 0)
        {
            Debug.LogWarning("Road edges not calculated. Call CalculateRoadEdges() first.");
            return false;
        }

        // Преобразуем мировые координаты в локальные
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        localPoint.y = 0;

        // Находим ближайший сегмент дороги
        int closestSegment = FindClosestSegment(localPoint);
        if (closestSegment < 0 || closestSegment >= corners.Count - 1) return false;

        // После нахождения ближайшего сегмента, вычисляем направление дороги
        if (closestSegment >= 0 && closestSegment < corners.Count - 1)
        {
            Vector3 segmentStart = transform.TransformPoint(corners[closestSegment]);
            Vector3 segmentEnd = transform.TransformPoint(corners[closestSegment + 1]);
            roadDirection = (segmentEnd - segmentStart).normalized;
        }

        // Находим проекцию точки на сегмент
        Vector3 projectedPoint = ProjectPointOnSegment(
            corners[closestSegment],
            corners[closestSegment + 1],
            localPoint
        );

        // Вычисляем параметр t вдоль сегмента (0-1)
        float t = CalculateParameterT(
            corners[closestSegment],
            corners[closestSegment + 1],
            projectedPoint
        );

        // Интерполируем краевые точки
        leftEdge = transform.TransformPoint(Vector3.Lerp(
            leftPoints[closestSegment],
            leftPoints[closestSegment + 1],
            t
        ));

        rightEdge = transform.TransformPoint(Vector3.Lerp(
            rightPoints[closestSegment],
            rightPoints[closestSegment + 1],
            t
        ));

        return true;
    }

    private int FindClosestSegment(Vector3 point)
    {
        int closestSegment = -1;
        float minDistance = float.MaxValue;

        for (int i = 0; i < corners.Count - 1; i++)
        {
            float distance = DistanceToSegment(point, corners[i], corners[i + 1]);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestSegment = i;
            }
        }

        return closestSegment;
    }

    private float DistanceToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        Vector3 toPoint = point - segmentStart;
        float segmentLength = segment.magnitude;
        Vector3 segmentDir = segment.normalized;

        // Проекция точки на сегмент
        float t = Mathf.Clamp01(Vector3.Dot(toPoint, segmentDir) / segmentLength);
        Vector3 projection = segmentStart + t * segment;

        // Возвращаем расстояние до проекции
        return Vector3.Distance(point, projection);
    }

    private Vector3 ProjectPointOnSegment(Vector3 segmentStart, Vector3 segmentEnd, Vector3 point)
    {
        Vector3 segment = segmentEnd - segmentStart;
        Vector3 toPoint = point - segmentStart;
        float segmentLength = segment.magnitude;
        Vector3 segmentDir = segment.normalized;

        float t = Mathf.Clamp01(Vector3.Dot(toPoint, segmentDir) / segmentLength);
        return segmentStart + t * segment;
    }

    private float CalculateParameterT(Vector3 segmentStart, Vector3 segmentEnd, Vector3 point)
    {
        if (segmentStart == segmentEnd) return 0;

        Vector3 segment = segmentEnd - segmentStart;
        Vector3 toPoint = point - segmentStart;

        return Vector3.Dot(toPoint, segment.normalized) / segment.magnitude;
    }

    // Для визуализации в редакторе
    void OnDrawGizmosSelected()
    {

    /*
    if (coordpoints == null || coordpoints.Count < 2) return;

    Gizmos.color = Color.cyan;
    for (int i = 0; i < coordpoints.Count - 1; i++)
    {
        Gizmos.DrawLine(coordpoints[i], coordpoints[i + 1]);
        Gizmos.DrawSphere(coordpoints[i], 0.3f);
    }
    Gizmos.DrawSphere(coordpoints[coordpoints.Count - 1], 0.3f);

    if (leftPoints != null && rightPoints != null)
    {
        Gizmos.color = Color.red;
        for (int i = 0; i < leftPoints.Count; i++)
        {
            Gizmos.DrawSphere(transform.TransformPoint(leftPoints[i]), 0.1f);
        }

        Gizmos.color = Color.blue;
        for (int i = 0; i < rightPoints.Count; i++)
        {
            Gizmos.DrawSphere(transform.TransformPoint(rightPoints[i]), 0.1f);
        }

        // Рисуем линии между точками
        Gizmos.color = Color.green;
        for (int i = 0; i < corners.Count - 1; i++)
        {
            Gizmos.DrawLine(transform.TransformPoint(leftPoints[i]), transform.TransformPoint(leftPoints[i + 1]));
            Gizmos.DrawLine(transform.TransformPoint(rightPoints[i]), transform.TransformPoint(rightPoints[i + 1]));
        }
    }*/

    // Визуализация полос
    DrawLanesGizmos();
    }

    /// <summary>
    /// Визуализирует полосы дороги с помощью Gizmos
    /// </summary>
    private void DrawLanesGizmos()
    {
        if (laneCenterPoints == null || laneCenterPoints.Count == 0)
        {
            // Попытаемся инициализировать полосы, если они еще не инициализированы
            if (leftPoints != null && rightPoints != null && leftPoints.Count > 0)
            {
                InitializeLanes();
            }
            else
            {
                return;
            }
        }

        // Цвета для визуализации полос
        Color[] laneColors = new Color[]
        {
        Color.white,
        Color.yellow,
        Color.magenta,
        Color.cyan,
        Color.green,
        new Color(1f, 0.5f, 0f), // оранжевый
        new Color(0.5f, 0f, 0.5f), // фиолетовый
        new Color(0f, 0.5f, 0.5f), // бирюзовый
        new Color(0.5f, 0.5f, 0f), // оливковый
        new Color(0.8f, 0.8f, 0.8f) // светло-серый
        };

        // Рисуем центральные линии каждой полосы
        for (int laneIndex = 0; laneIndex < laneCenterPoints.Count; laneIndex++)
        {
            Color laneColor = laneColors[laneIndex % laneColors.Length];
            Gizmos.color = laneColor;

            List<Vector3> centerPoints = laneCenterPoints[laneIndex];

            // Рисуем линии между точками центра полосы
            for (int i = 0; i < centerPoints.Count - 1; i++)
            {
                Gizmos.DrawLine(centerPoints[i] + Vector3.up * 2.0f, centerPoints[i + 1] + Vector3.up * 2.0f);
            }

            // Рисуем точки центра полосы
            for (int i = 0; i < centerPoints.Count; i++)
            {
                Gizmos.DrawSphere(centerPoints[i] + Vector3.up * 2.0f, 0.25f);
            }

            // Рисуем направление движения для каждой полосы (стрелки)
            if (centerPoints.Count > 1)
            {
                for (int i = 0; i < centerPoints.Count - 1; i += 3) // Рисуем стрелки каждые 3 точки
                {
                    Vector3 direction = (centerPoints[i + 1] - centerPoints[i]).normalized;
                    Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized * 0.3f;

                    // Рисуем стрелку
                    Vector3 arrowStart = centerPoints[i] + direction * 0.5f;
                    Vector3 arrowEnd = centerPoints[i] + direction * 1.5f;

                    Gizmos.DrawLine(arrowStart + Vector3.up * 2.0f, arrowEnd + Vector3.up * 2.0f);
                    Gizmos.DrawLine(arrowEnd + Vector3.up * 2.0f, arrowEnd - direction * 0.5f + perpendicular * 0.5f + Vector3.up * 2.0f);
                    Gizmos.DrawLine(arrowEnd + Vector3.up * 2.0f, arrowEnd - direction * 0.5f - perpendicular * 0.5f + Vector3.up * 2.0f);
                }
            }

            // Подписываем номер полосы
            if (centerPoints.Count > 0)
            {
#if UNITY_EDITOR
                UnityEditor.Handles.Label(centerPoints[0] + Vector3.up * 0.5f + Vector3.up * 3.0f, $"Lane {laneIndex}");
#endif
            }
        }

        // Рисуем границы между полосами
        Gizmos.color = Color.white;
        for (int laneIndex = 0; laneIndex < laneLeftPoints.Count; laneIndex++)
        {
            List<Vector3> leftEdgePoints = laneLeftPoints[laneIndex];

            // Рисуем левую границу полосы пунктирной линией
            for (int i = 0; i < leftEdgePoints.Count - 1; i++)
            {
                if (i % 2 == 0) // Пропускаем каждый второй сегмент для создания пунктира
                {
                    Gizmos.DrawLine(leftEdgePoints[i] + Vector3.up * 2.0f, leftEdgePoints[i + 1] + Vector3.up * 2.0f);
                }
            }
        }

        for (int laneIndex = 0; laneIndex < laneRightPoints.Count; laneIndex++)
        {
            List<Vector3> rightEdgePoints = laneRightPoints[laneIndex];

            // Рисуем правую границу полосы пунктирной линией
            for (int i = 0; i < rightEdgePoints.Count - 1; i++)
            {
                if (i % 2 == 0) // Пропускаем каждый второй сегмент для создания пунктира
                {
                    Gizmos.DrawLine(rightEdgePoints[i] + Vector3.up * 2.0f, rightEdgePoints[i + 1] + Vector3.up * 2.0f);
                }
            }
        }
    }
}
