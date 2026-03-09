using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadFollower : MonoBehaviour
{
    public float laneOffsetStrength = 1.0f;

    private Road currentRoad;
    private int laneIndex;

    public bool TryProjectToRightLane(Vector3 routeTargetPoint, out Vector3 laneTarget)
    {
        laneTarget = routeTargetPoint;

        // 1. Находим дорогу под точкой маршрута
        if (!TryGetRoadUnderPoint(routeTargetPoint, out currentRoad))
            return false;

        RoadMarkings markings = currentRoad.GetRoadMarkings();
        if (markings == null)
            return false;

        // 2. Определяем направление движения автобуса
        Vector3 busDir = transform.forward;

        // 3. Определяем индексы полос
        if (markings.IsOneWay)
        {
            laneIndex = 0; // правая крайняя
        }
        else
        {
            float dot = Vector3.Dot(busDir, currentRoad.GetForwardDirection(0, 0));

            if (dot > 0)
            {
                // движение вперёд по дороге
                laneIndex = Mathf.Clamp(markings.LanesForward - 1, 0, 0);
            }
            else
            {
                // движение назад по дороге
                laneIndex = 0;
            }
        }

        // 4. Находим ближайшую точку этой полосы
        int nearestPointID;
        Vector3 nearestPoint;
        float dist;

        if (!currentRoad.FindNearestLane(routeTargetPoint, out int ln, out nearestPointID, out nearestPoint, out dist))
            return false;

        // Берём центр правой полосы
        var lanePoints = currentRoad.GetLaneCenterPoints(laneIndex);

        int clamped = Mathf.Clamp(nearestPointID, 0, lanePoints.Count - 1);
        laneTarget = lanePoints[clamped];

        return true;
    }

    private bool TryGetRoadUnderPoint(Vector3 pos, out Road road)
    {
        road = null;
        Collider[] hits = Physics.OverlapSphere(pos, 3.0f); // радиус поиска дороги

        foreach (var h in hits)
        {
            var r = h.GetComponentInParent<Road>();
            if (r != null)
            {
                road = r;
                return true;
            }
        }
        return false;
    }
}
