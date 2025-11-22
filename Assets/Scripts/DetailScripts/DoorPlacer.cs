using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class DoorPlacer : DetailBase
{
    public GameObject doorPrefab;
    public float checkRadius = 3.0f;
    [Tooltip("Offset to prevent raycast inside collider")]
    public float surfaceOffset = 0.1f;

    public new void ActivateObject()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, checkRadius);

        foreach (Collider col in colliders)
        {
            if (col.GetComponent<Building>() != null && col != GetComponent<Collider>() && !col.GetComponent<Building>().isModelSet)
            {
                PlaceDoorOnBuilding(col);
            }
        }
    }

    void PlaceDoorOnBuilding(Collider buildingCollider)
    {
        Vector3 doorPosition;
        Vector3 surfaceNormal;

        // Пытаемся найти точку через raycast в двух направлениях
        if (TryFindSurfacePoint(buildingCollider, out doorPosition, out surfaceNormal))
        {
            SpawnDoor(doorPosition, surfaceNormal, buildingCollider.transform);
        }
        else
        {
   //         Debug.LogWarning($"Failed to place door on {buildingCollider.name}");
        }
    }

    bool TryFindSurfacePoint(Collider targetCollider, out Vector3 point, out Vector3 normal)
    {
        // Первый raycast: от этого объекта к зданию
        Vector3 directionToBuilding = (targetCollider.bounds.center - transform.position).normalized;
        if (CastWithOffset(transform.position, directionToBuilding, targetCollider, out point, out normal))
        {
            return true;
        }

        // Второй raycast: от центра здания к этому объекту
        Vector3 directionFromCenter = (transform.position - targetCollider.bounds.center).normalized;
        if (CastWithOffset(targetCollider.bounds.center, directionFromCenter, targetCollider, out point, out normal))
        {
            return true;
        }

        // Fallback: используем ближайшую точку границ
 //     point = targetCollider.ClosestPoint(transform.position);
        point = GetClosestPointOnCollider(targetCollider, transform.position);
        normal = (transform.position - point).normalized;
        return false;
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
            if (Physics.Raycast(point, direction, out hit, 100f))
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
            if (Physics.Raycast(point, -direction, out hit, 100f))
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

    private Vector3 GetClosestPointOnNonConvexMesh(MeshCollider meshCollider, Vector3 point)
    {
        // Альтернативный метод для невыпуклых мешей - используем Raycast
        Vector3 directionToCollider = meshCollider.bounds.center - point;
        RaycastHit hit;

        if (Physics.Raycast(point, directionToCollider.normalized, out hit, 100f))
        {
            if (hit.collider == meshCollider)
            {
                return hit.point;
            }
        }

        // Если Raycast не сработал, используем ближайшую точку на bounding box
        return meshCollider.bounds.ClosestPoint(point);
    }

    bool CastWithOffset(Vector3 origin, Vector3 direction, Collider target, out Vector3 point, out Vector3 normal)
    {
        // Смещаем начало луча, чтобы избежать внутреннего попадания
        Ray ray = new Ray(origin - direction * surfaceOffset, direction);
        RaycastHit hit;

        if (target.Raycast(ray, out hit, checkRadius + surfaceOffset))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        point = Vector3.zero;
        normal = Vector3.up;
        return false;
    }

    void SpawnDoor(Vector3 position, Vector3 normal, Transform parent)
    {
        if (doorPrefab == null) return;

        // Корректируем поворот для двери
        Quaternion rotation = Quaternion.LookRotation(normal);
        Instantiate(doorPrefab, position, rotation, parent);

//      Debug.Log($"Door placed at {position} with normal {normal}");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, checkRadius);
    }
}
