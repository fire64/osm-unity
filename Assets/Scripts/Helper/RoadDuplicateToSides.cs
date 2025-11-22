using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadDuplicateToSides : MonoBehaviour
{
    public LayerMask roadMask;
    public bool destroyOriginal = true;
    public GameObject origGameObject;

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

        if (road.GetEdgesAtPoint(origGameObject.transform.position, out Vector3 leftEdge, out Vector3 rightEdge, out Vector3 roadDirection))
        {
            // Создаем копии по краям дороги с помощью Instantiate
            GameObject leftCopy = Instantiate(origGameObject.gameObject, leftEdge, origGameObject.transform.rotation, origGameObject.transform.parent);
            GameObject rightCopy = Instantiate(origGameObject.gameObject, rightEdge, origGameObject.transform.rotation, origGameObject.transform.parent);

            // Переименовываем копии
            leftCopy.name = origGameObject.gameObject.name + "_Left";
            rightCopy.name = origGameObject.gameObject.name + "_Right";

            // Удаляем оригинальный объект, если нужно
            if (destroyOriginal)
            {
                Destroy(origGameObject.gameObject);
            }
        }
    }

    // Визуализация для отладки
    private void OnDrawGizmosSelected()
    {
        Road road = GetCurrentRoad();
        if (!road) return;

        if (road.GetEdgesAtPoint(origGameObject.transform.position, out Vector3 leftEdge, out Vector3 rightEdge, out Vector3 roadDirection))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(leftEdge, 0.1f);
            Gizmos.DrawSphere(rightEdge, 0.1f);
            Gizmos.DrawLine(origGameObject.transform.position, leftEdge);
            Gizmos.DrawLine(origGameObject.transform.position, rightEdge);
        }
    }
}
