using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AxisScale
{
    None,
    X, Y, Z,
};

public class RoadScaller : MonoBehaviour
{
    public AxisScale axisScale = AxisScale.None;

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
            float roadWidth = Vector3.Distance(leftEdge, rightEdge);
            ScaleObjectToRoadWidth(roadWidth, leftEdge, rightEdge);
        }
    }

    private void ScaleObjectToRoadWidth(float roadWidth, Vector3 leftEdge, Vector3 rightEdge)
    {
        if (axisScale == AxisScale.None) return;

        Renderer renderer = GetComponent<Renderer>();
        if (!renderer) return;

        // Получаем текущий размер объекта в мировых координатах
        Vector3 currentSize = renderer.bounds.size;

        // Определяем направление поперек дороги
        Vector3 roadPerpendicular = (rightEdge - leftEdge).normalized;

        // Вычисляем текущую ширину объекта в направлении поперек дороги
        float currentWidth = CalculateObjectWidthInDirection(renderer, roadPerpendicular);

        if (currentWidth == 0) return;

        // Вычисляем коэффициент масштабирования
        float scaleFactor = roadWidth / currentWidth;

        // Применяем масштабирование к выбранной оси
        Vector3 newScale = transform.localScale;

        switch (axisScale)
        {
            case AxisScale.X:
                newScale.x *= scaleFactor;
                break;
            case AxisScale.Y:
                newScale.y *= scaleFactor;
                break;
            case AxisScale.Z:
                newScale.z *= scaleFactor;
                break;
        }

        transform.localScale = newScale;
    }

    private float CalculateObjectWidthInDirection(Renderer renderer, Vector3 direction)
    {
        // Получаем bounds объекта в мировых координатах
        Bounds bounds = renderer.bounds;

        // Вычисляем проекцию размера bounds на заданное направление
        Vector3 absDirection = new Vector3(
            Mathf.Abs(direction.x),
            Mathf.Abs(direction.y),
            Mathf.Abs(direction.z)
        );

        return Vector3.Dot(bounds.size, absDirection);
    }

    // Визуализация для отладки
    private void OnDrawGizmosSelected()
    {
        Road road = GetCurrentRoad();
        if (!road) return;

        if (road.GetEdgesAtPoint(transform.position, out Vector3 leftEdge, out Vector3 rightEdge, out Vector3 roadDirection))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(leftEdge, 0.1f);
            Gizmos.DrawSphere(rightEdge, 0.1f);
            Gizmos.DrawLine(leftEdge, rightEdge);

            // Показываем направление поперек дороги
            Gizmos.color = Color.blue;
            Vector3 center = (leftEdge + rightEdge) / 2;
            Gizmos.DrawLine(center, center + (rightEdge - leftEdge).normalized * 2f);
        }
    }
}
