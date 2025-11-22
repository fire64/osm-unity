using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SelectSide
{
    Left,
    Right
};

public class RoadMover : MonoBehaviour
{
    public LayerMask roadMask;
    public SelectSide selectSide;

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
        if (Physics.Raycast(transform.position + Vector3.up * 3f, Vector3.down, out hit, 5.0f, roadMask))
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
            if (selectSide == SelectSide.Left)
            {
                transform.position = leftEdge;
            }
            else if (selectSide == SelectSide.Right)
            {
                transform.position = rightEdge;
            }
        }
    }
}
