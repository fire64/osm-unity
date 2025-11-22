using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SideOrientation
{
    Left,
    Right,
    LeftForward,  // ¬перед относительно левой стороны
    LeftBackward, // Ќазад относительно левой стороны
    RightForward, // ¬перед относительно правой стороны
    RightBackward // Ќазад относительно правой стороны
};


public class RoadOrientation : MonoBehaviour
{
    public SideOrientation sideOrientation;
    public LayerMask roadMask;

    private void OnEnable()
    {
        CompleteLoadingBroadCast.OnAllModulesLoaded += HandleAllModulesLoaded;
        if (CompleteLoadingBroadCast.IsAllLoaded)
        {
            HandleAllModulesLoaded();
        }
    }

    private void OnDisable()
    {
        CompleteLoadingBroadCast.OnAllModulesLoaded -= HandleAllModulesLoaded;
    }

    Road GetCurrentRoad()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out hit, 5.0f, roadMask))
        {
            return hit.collider.GetComponent<Road>();
        }
        return null;
    }

    private void HandleAllModulesLoaded()
    {
        Road road = GetCurrentRoad();
        if (!road) return;

        if (road.GetEdgesAtPoint(transform.position, out Vector3 leftEdge, out Vector3 rightEdge, out Vector3 roadDirection))
        {
            Vector3 targetDirection = CalculateTargetDirection(leftEdge, rightEdge, roadDirection);
            targetDirection.y = transform.position.y;
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
    }

    private Vector3 CalculateTargetDirection(Vector3 leftEdge, Vector3 rightEdge, Vector3 roadDirection)
    {
        switch (sideOrientation)
        {
            case SideOrientation.Left:
                return (leftEdge - transform.position).normalized;

            case SideOrientation.Right:
                return (rightEdge - transform.position).normalized;

            case SideOrientation.LeftForward:
                return CalculateParallelDirection(leftEdge, roadDirection, 90f);

            case SideOrientation.LeftBackward:
                return CalculateParallelDirection(leftEdge, roadDirection, -90f);

            case SideOrientation.RightForward:
                return CalculateParallelDirection(rightEdge, roadDirection, 90f);

            case SideOrientation.RightBackward:
                return CalculateParallelDirection(rightEdge, roadDirection, -90f);

            default:
                return transform.forward;
        }
    }

    private Vector3 CalculateParallelDirection(Vector3 edgePoint, Vector3 roadDirection, float angleOffset)
    {
        // ¬ычисл€ем перпендикул€рное направление от кра€ дороги
        Vector3 perpendicularDir = (edgePoint - transform.position).normalized;

        // ¬ычисл€ем параллельное направление с учетом смещени€ угла
        Vector3 parallelDir = Quaternion.Euler(0, angleOffset, 0) * perpendicularDir;

        // ”читываем направление дороги дл€ корректной ориентации вперед/назад
        if (Vector3.Dot(parallelDir, roadDirection) < 0)
        {
            parallelDir = -parallelDir;
        }

        return parallelDir;
    }
}
