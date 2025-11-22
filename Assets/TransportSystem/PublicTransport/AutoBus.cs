using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AutoBus : MonoBehaviour
{
    public Route route;
    public float speed = 5.0f;
    public float stopTime = 3.0f;
    public float rotationSpeed = 2.0f;
    public float arrivalThreshold = 0.1f;
    public bool loopRoute = false;

    public ScrollingText m_RouteName;

    private int currentPathIndex = 0;
    private int nextStopIndex = 0;
    private bool isMoving = true;
    private bool isWaitingAtStop = false;
    private bool isReversing = false;


    // Событие для уведомления о прибытии на остановку
    public event Action<int> OnBusArrivedAtStop;

    // Ссылки на компоненты для анимации дверей
    [SerializeField] private Animator doorAnimator;
    private static readonly int OpenDoors = Animator.StringToHash("OpenDoors");

    // Кэш для расстояний между точками пути
    private List<float> pathSegmentLengths = new List<float>();
    private float totalPathLength = 0f;

    void Start()
    {
        if (route != null)
        {
            SetRoute(route);
        }
    }

    void Update()
    {
        if (route == null || route.coordpoints.Count < 2) return;

        if (isMoving && !isWaitingAtStop)
        {
            MoveAlongPath();
            CheckForStop();
        }
    }

    // Метод для установки маршрута
    public void SetRoute(Route newRoute)
    {
        this.route = newRoute;
        CalculatePathData();

        // Находим ближайшую точку пути для начала движения
        currentPathIndex = FindNearestPathPointIndex();

        // Находим следующую остановку
        nextStopIndex = FindNextStopIndex();

        // Сбрасываем состояние движения
        isMoving = true;
        isWaitingAtStop = false;

        if (route.HasField("ref") && m_RouteName != null)
        {
            var numbertrans = route.GetValueStringByKey("ref");

            var endstation = "";

            if (route.HasField("to"))
            {
                endstation = route.GetValueStringByKey("to");
            }

            m_RouteName.SetText(numbertrans + " " + endstation + " ");
        }
    }

    // Найти индекс ближайшей точки пути
    private int FindNearestPathPointIndex()
    {
        if (route == null || route.coordpoints.Count == 0) return 0;

        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < route.coordpoints.Count; i++)
        {
            float distance = Vector3.Distance(transform.position, route.coordpoints[i]);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        // Если мы близко к концу пути, начинаем с начала
        if (nearestIndex >= route.coordpoints.Count - 2 && loopRoute)
        {
            nearestIndex = 0;
        }

        return nearestIndex;
    }

    // Найти индекс следующей остановки
    private int FindNextStopIndex()
    {
        if (route == null || route.stoppoints.Count == 0) return 0;

        // Находим ближайшую остановку, которая еще впереди
        for (int i = 0; i < route.stoppoints.Count; i++)
        {
            // Проверяем, находится ли остановка впереди по маршруту
            if (IsStopAhead(route.stoppoints[i]))
            {
                return i;
            }
        }

        // Если не нашли остановку впереди, начинаем с первой
        return 0;
    }

    // Проверить, находится ли остановка впереди по маршруту
    private bool IsStopAhead(Vector3 stopPosition)
    {
        // Находим ближайшую точку пути к остановке
        int stopPathIndex = FindNearestPathPointToStop(stopPosition);

        // Остановка впереди, если ее индекс пути больше текущего
        return stopPathIndex >= currentPathIndex;
    }

    // Найти ближайшую точку пути к остановке
    private int FindNearestPathPointToStop(Vector3 stopPosition)
    {
        if (route == null || route.coordpoints.Count == 0) return 0;

        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < route.coordpoints.Count; i++)
        {
            float distance = Vector3.Distance(stopPosition, route.coordpoints[i]);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    // Расчет данных о пути
    void CalculatePathData()
    {
        pathSegmentLengths.Clear();
        totalPathLength = 0f;

        if (route == null || route.coordpoints.Count < 2) return;

        for (int i = 0; i < route.coordpoints.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(route.coordpoints[i], route.coordpoints[i + 1]);
            pathSegmentLengths.Add(segmentLength);
            totalPathLength += segmentLength;
        }
    }

    // Движение по пути
    void MoveAlongPath()
    {
        if (currentPathIndex >= route.coordpoints.Count - 1)
        {
            if (loopRoute)
            {
                currentPathIndex = 0;
                transform.position = route.coordpoints[0];
            }
            else
            {
                isMoving = false;
                return;
            }
        }

        Vector3 targetPoint = route.coordpoints[currentPathIndex + 1];
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint,
            speed * Time.deltaTime
        );

        // Плавное вращение в направлении движения
        Vector3 direction = (targetPoint - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // Проверка достижения следующей точки пути
        if (Vector3.Distance(transform.position, targetPoint) < arrivalThreshold)
        {
            currentPathIndex++;
        }
    }

    // Проверка необходимости остановки
    void CheckForStop()
    {
        if (nextStopIndex >= route.stoppoints.Count) return;

        // Проверяем, близко ли мы к следующей остановке
        float distanceToStop = Vector3.Distance(transform.position, route.stoppoints[nextStopIndex]);

        if (distanceToStop < arrivalThreshold * 2f)
        {
            StartCoroutine(StopAtStation());
        }
    }

    // Остановка на станции
    IEnumerator StopAtStation()
    {
        isWaitingAtStop = true;
        isMoving = false;

        // Уведомляем о прибытии на остановку
        OnBusArrivedAtStop?.Invoke(nextStopIndex);

        // Анимация открытия дверей
        if (doorAnimator != null)
        {
            doorAnimator.SetBool(OpenDoors, true);
        }

        yield return new WaitForSeconds(stopTime);

        // Анимация закрытия дверей
        if (doorAnimator != null)
        {
            doorAnimator.SetBool(OpenDoors, false);
        }

        // Переходим к следующей остановке
        nextStopIndex++;

        // Если это последняя остановка и маршрут не зациклен
        if (nextStopIndex >= route.stoppoints.Count && !loopRoute)
        {
            isMoving = false;
        }
        else
        {
            isMoving = true;
            isWaitingAtStop = false;

            // Если зацикливаем маршрут и дошли до конца
            if (nextStopIndex >= route.stoppoints.Count && loopRoute)
            {
                nextStopIndex = 0;
            }
        }
    }

    // Метод для принудительного открытия/закрытия дверей
    public void SetDoorsOpen(bool open)
    {
        if (doorAnimator != null)
        {
            doorAnimator.SetBool(OpenDoors, open);
        }
    }

    // Получение прогресса по маршруту (0-1)
    public float GetRouteProgress()
    {
        if (route == null || route.coordpoints.Count < 2) return 0f;

        float traveledDistance = 0f;

        // Суммируем длину пройденных сегментов
        for (int i = 0; i < currentPathIndex; i++)
        {
            if (i < pathSegmentLengths.Count)
            {
                traveledDistance += pathSegmentLengths[i];
            }
        }

        // Добавляем длину текущего сегмента
        if (currentPathIndex < route.coordpoints.Count - 1)
        {
            traveledDistance += Vector3.Distance(
                route.coordpoints[currentPathIndex],
                transform.position
            );
        }

        return traveledDistance / totalPathLength;
    }

    // Получение текущего индекса остановки
    public int GetCurrentStopIndex()
    {
        return nextStopIndex;
    }

    // Получение состояния движения
    public bool IsMoving()
    {
        return isMoving && !isWaitingAtStop;
    }

    // Получение состояния ожидания на остановке
    public bool IsWaitingAtStop()
    {
        return isWaitingAtStop;
    }

    // Визуализация в редакторе
    private void OnDrawGizmosSelected()
    {
        if (route == null) return;

        // Рисуем путь
        Gizmos.color = Color.blue;
        for (int i = 0; i < route.coordpoints.Count - 1; i++)
        {
            Gizmos.DrawLine(route.coordpoints[i], route.coordpoints[i + 1]);
            Gizmos.DrawSphere(route.coordpoints[i], 0.2f);
        }

        if (route.coordpoints.Count > 0)
        {
            Gizmos.DrawSphere(route.coordpoints[route.coordpoints.Count - 1], 0.2f);
        }

        // Рисуем остановки
        Gizmos.color = Color.red;
        foreach (Vector3 stop in route.stoppoints)
        {
            Gizmos.DrawCube(stop, Vector3.one * 0.5f);

            // Подписываем остановки
#if UNITY_EDITOR
            UnityEditor.Handles.Label(stop + Vector3.up * 0.5f, $"Stop {route.stoppoints.IndexOf(stop)}");
#endif
        }

        // Рисуем текущее положение и направление автобуса
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.7f);
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        // Рисуем линию к следующей точке пути
        if (Application.isPlaying && route.coordpoints.Count > currentPathIndex + 1)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, route.coordpoints[currentPathIndex + 1]);
        }
    }
}