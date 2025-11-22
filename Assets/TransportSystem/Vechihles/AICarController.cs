using System;
using System.Collections.Generic;
using UnityEngine;
using static UMA.SlotDataAsset;

public class AICarController : MonoBehaviour
{
    public Road currentRoad;
    public int waypointIndex;
    public int laneIndex;
    public bool islaneForward;

    // Параметры движения
    public float speed = 0.0f;

    public float minspeed = 10.0f;
    public float maxspeed = 15.0f;

    public float rotationSpeed = 2.0f;
    public float reachThreshold = 0.5f;

    // Статус движения
    private bool isMoving = true;

    public LayerMask roadMask;

    void Start()
    {
        speed = UnityEngine.Random.Range(minspeed, maxspeed);

        // Инициализация начальной позиции
        if (currentRoad != null)
        {
            SetCarPosition();
        }
    }

    void Update()
    {
        if (currentRoad == null || !isMoving) return;

        MoveToNextPoint();
    }

    void MoveToNextPoint()
    {
        Vector3 targetPoint = GetNextPoint();

        // Направление к целевой точке
        Vector3 direction = targetPoint - transform.position;

        // Поворот в направлении движения
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Движение вперед
        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        // Проверка достижения вейпоинта
        if (Vector3.Distance(transform.position, targetPoint) < reachThreshold)
        {
            UpdateWaypointIndex();
        }
    }

    void RemoveFinishedCar()
    {
        // Найти спавнер и вернуть пешехода в пул
        TrafficSpawner spawner = FindObjectOfType<TrafficSpawner>();
        if (spawner != null)
        {
            spawner.ReturnVehicleToPool(gameObject);
        }
        else
        {
            // Если спавнер не найден, просто деактивировать
            gameObject.SetActive(false);
        }
    }

    bool TryFindNextRoad()
    {
        RaycastHit hit;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 2.0f, roadMask))
        {
            Road newroad = hit.collider.GetComponent<Road>();

            if (newroad != null && newroad != currentRoad && newroad.isCanUseAutomobile())
            {
                int newlaneIndex = 0;
                int newpointIndex = 0;
                Vector3 nearestPoint = Vector3.zero;
                float distance = 0.0f;

                bool ret = newroad.FindNearestLane(transform.position, out newlaneIndex, out newpointIndex, out nearestPoint, out distance);

                if (!ret)
                    return false;

                this.currentRoad = newroad;
                this.waypointIndex = newpointIndex;
                this.laneIndex = newlaneIndex;
                this.islaneForward = this.currentRoad.isLineForward(this.laneIndex);
                this.isMoving = true;
                SetCarPosition();


                return true;
            }
        }

        return false;

    }

    void UpdateWaypointIndex()
    {
        if (islaneForward)
        {
            waypointIndex++;

            // Проверка достижения конца пути
            if (waypointIndex >= GetCountWaypoints() - 1)
            {
                waypointIndex = GetCountWaypoints() - 1;
                isMoving = false; // Остановка в конце пути
                Debug.Log("Reached end of forward path");

                bool isSetNewRoad = TryFindNextRoad();

                if(isSetNewRoad)
                {
                    Debug.Log( "Set new road..." );
                }
                else
                {
                    RemoveFinishedCar();
                }

            }
        }
        else
        {
            waypointIndex--;

            // Проверка достижения начала пути
            if (waypointIndex <= 0)
            {
                waypointIndex = 0;
                isMoving = false; // Остановка в начале пути
                Debug.Log("Reached start of backward path");

                bool isSetNewRoad = TryFindNextRoad();

                if (isSetNewRoad)
                {
                    Debug.Log("Set new road...");
                }
                else
                {
                    RemoveFinishedCar();
                }
            }
        }
    }

    Vector3 GetCurrentPoint()
    {
        return currentRoad.GetCenterPointByIdAndLane(laneIndex, waypointIndex);
    }

    Vector3 GetNextPoint()
    {
        int nextwaypointIndex = waypointIndex;

        if (islaneForward)
        {
            nextwaypointIndex = waypointIndex + 1;
            // Ограничение индекса
            if (nextwaypointIndex >= GetCountWaypoints())
                nextwaypointIndex = GetCountWaypoints() - 1;
        }
        else
        {
            nextwaypointIndex = waypointIndex - 1;
            // Ограничение индекса
            if (nextwaypointIndex < 0)
                nextwaypointIndex = 0;
        }

        return currentRoad.GetCenterPointByIdAndLane(laneIndex, nextwaypointIndex);
    }

    Vector3 GetPointById(int pointid)
    {
        return currentRoad.GetCenterPointByIdAndLane(laneIndex, pointid);
    }

    int GetCountWaypoints()
    {
        List<Vector3> points = currentRoad.GetLaneCenterPoints(laneIndex);
        return points.Count;
    }

    void OnDrawGizmos()
    {
        if (currentRoad == null) return;

        // Отображение текущей позиции
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.3f);

        // Отображение направления движения
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }

    public void ResetAI(Road road, int waypointIndex, int laneIndex)
    {
        this.currentRoad = road;
        this.waypointIndex = waypointIndex;
        this.laneIndex = laneIndex;
        this.islaneForward = this.currentRoad.isLineForward(this.laneIndex);
        this.isMoving = true;

        SetCarPosition();
    }

    void SetCarPosition()
    {
        transform.position = GetCurrentPoint();

        // Плавный поворот к следующей точке
        Vector3 nextPoint = GetNextPoint();
        Vector3 direction = nextPoint - transform.position;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (currentRoad == null) return;

        Vector3 startpoint = GetCurrentPoint();

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(startpoint, 1.0f);

        int startwaypointid = waypointIndex;

        if (islaneForward)
        {
            Gizmos.color = Color.red;
            for (int i = startwaypointid + 1; i < GetCountWaypoints(); i++)
            {
                Vector3 coordpoint = GetPointById(i);
                Gizmos.DrawSphere(coordpoint, 0.5f);

                // Линии между точками
                if (i > startwaypointid + 1)
                {
                    Vector3 prevPoint = GetPointById(i - 1);
                    Gizmos.DrawLine(prevPoint, coordpoint);
                }
            }
        }
        else
        {
            Gizmos.color = Color.blue;
            for (int i = startwaypointid - 1; i >= 0; i--)
            {
                Vector3 coordpoint = GetPointById(i);
                Gizmos.DrawSphere(coordpoint, 0.5f);

                // Линии между точками
                if (i < startwaypointid - 1)
                {
                    Vector3 nextPoint = GetPointById(i + 1);
                    Gizmos.DrawLine(nextPoint, coordpoint);
                }
            }
        }
    }

    // Методы для внешнего управления
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }

    public void StopMovement()
    {
        isMoving = false;
    }

    public void StartMovement()
    {
        isMoving = true;
    }

    public void ChangeLane(int newLaneIndex)
    {
        laneIndex = newLaneIndex;
        islaneForward = currentRoad.isLineForward(laneIndex);
        SetCarPosition();
    }
}