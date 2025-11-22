using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrientToRoad : MonoBehaviour
{
    public LayerMask roadMask;

    public float searchRadius = 10f;

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

    private void HandleAllModulesLoaded()
    {
        OrientToNearestRoad();
    }

    public void OrientToNearestRoad()
    {
        // Ищем все коллайдеры в заданном радиусе и слое
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, searchRadius, roadMask);

        if (hitColliders.Length == 0)
        {
            Debug.LogWarning("No colliders found on target layer");
            return;
        }

        // Находим ближайший коллайдер и ближайшую точку
        Collider nearestCollider = null;
        Vector3 nearestPoint = Vector3.zero;
        float minDistance = Mathf.Infinity;

        foreach (Collider col in hitColliders)
        {
            // Используем Physics.ClosestPoint который работает для всех коллайдеров
            Vector3 closestPoint = GetClosestPointOnCollider(col, transform.position);
            float distance = Vector3.Distance(transform.position, closestPoint);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestCollider = col;
                nearestPoint = closestPoint;
            }
        }

        // Ориентируем объект в направлении точки
        OrientTowardsPoint(nearestPoint);
    }

    private Vector3 GetClosestPointOnCollider(Collider collider, Vector3 point)
    {
        // Для примитивных коллайдеров используем встроенный метод
        if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
        {
            return collider.ClosestPoint(point);
        }

        // Для MeshCollider проверяем выпуклость
        if (collider is MeshCollider meshCollider)
        {
            if (meshCollider.convex)
            {
                return collider.ClosestPoint(point);
            }
            else
            {
                // Для невыпуклых мешей используем альтернативный метод
                return GetClosestPointOnNonConvexMesh(meshCollider, point);
            }
        }

        // Для других типов коллайдеров (TerrainCollider и т.д.) используем альтернативный метод
        return GetClosestPointFallback(collider, point);
    }

    private Vector3 GetClosestPointOnNonConvexMesh(MeshCollider meshCollider, Vector3 point)
    {
        // Альтернативный метод для невыпуклых мешей - используем Raycast
        Vector3 directionToCollider = meshCollider.bounds.center - point;
        RaycastHit hit;

        if (Physics.Raycast(point, directionToCollider.normalized, out hit, searchRadius * 2, roadMask))
        {
            if (hit.collider == meshCollider)
            {
                return hit.point;
            }
        }

        // Если Raycast не сработал, используем ближайшую точку на bounding box
        return meshCollider.bounds.ClosestPoint(point);
    }

    private Vector3 GetClosestPointFallback(Collider collider, Vector3 point)
    {
        // Используем несколько лучей в разных направлениях для поиска ближайшей точки
        Vector3[] directions = {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            Vector3.up, Vector3.down,
            new Vector3(1, 1, 1).normalized, new Vector3(-1, 1, 1).normalized,
            new Vector3(1, -1, 1).normalized, new Vector3(-1, -1, 1).normalized
        };

        Vector3 closestPoint = collider.bounds.ClosestPoint(point);
        float minDistance = Vector3.Distance(point, closestPoint);

        foreach (Vector3 direction in directions)
        {
            RaycastHit hit;
            if (Physics.Raycast(point, direction, out hit, searchRadius * 2, roadMask))
            {
                if (hit.collider == collider)
                {
                    float distance = Vector3.Distance(point, hit.point);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPoint = hit.point;
                    }
                }
            }

            // Также проверяем в противоположном направлении
            if (Physics.Raycast(point, -direction, out hit, searchRadius * 2, roadMask))
            {
                if (hit.collider == collider)
                {
                    float distance = Vector3.Distance(point, hit.point);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPoint = hit.point;
                    }
                }
            }
        }

        return closestPoint;
    }

    private void OrientTowardsPoint(Vector3 targetPoint)
    {
        // Вычисляем направление к точке
        Vector3 direction = targetPoint - transform.position;

        // Игнорируем разницу по высоте (выравниваем по горизонтали)
        direction.y = 0;

        // Проверяем, чтобы направление не было нулевым
        if (direction != Vector3.zero)
        {
            // Создаем вращение к цели
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = targetRotation;
        }
    }

    // Опционально: визуализация радиуса поиска в редакторе
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }

    // Метод для ручного вызова ориентации (например, по кнопке)
    [ContextMenu("Orient To Nearest Road")]
    private void OrientManually()
    {
        OrientToNearestRoad();
    }
}
