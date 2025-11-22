using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bench : InteractiveObject
{
    [Header("Bench Settings")]
    public int maxUsers = 2;
    public float sitDurationMin = 10f;
    public float sitDurationMax = 30f;

    [Header("Sitting Points")]
    public Transform[] sittingPoints;
    public Transform[] approachPoints;

    private bool[] occupiedSpots;
    private List<AdvancedAIPedestrianController> currentUsers;

    void Start()
    {
        occupiedSpots = new bool[sittingPoints.Length];
        currentUsers = new List<AdvancedAIPedestrianController>();

        if (sittingPoints == null || sittingPoints.Length == 0)
            CreateDefaultSittingPoints();

        if (approachPoints == null || approachPoints.Length == 0)
            CreateDefaultApproachPoints();
    }

    public override bool IsAvailable()
    {
        for (int i = 0; i < occupiedSpots.Length; i++)
        {
            if (!occupiedSpots[i]) return true;
        }
        return false;
    }

    // Обновим корутину MoveToBench
    IEnumerator MoveToBench(AdvancedAIPedestrianController pedestrian)
    {
        int spotIndex = ReserveSpot(pedestrian);
        if (spotIndex == -1) yield return null;

        Vector3 targetPosition = GetSittingPosition(spotIndex);
        Quaternion targetRotation = GetSittingRotation(spotIndex);

        // Двигаемся непосредственно к точке сидения
        while (Vector3.Distance(pedestrian.transform.position, targetPosition) > 0.1f)
        {
            pedestrian.transform.position = Vector3.MoveTowards(pedestrian.transform.position, targetPosition, pedestrian.maxSpeed * Time.deltaTime);
            pedestrian.transform.rotation = Quaternion.Slerp(pedestrian.transform.rotation, targetRotation, pedestrian.rotationSpeed * Time.deltaTime);
            yield return null;
        }

        if (pedestrian.animator != null)
        {
            pedestrian.animator.SetTrigger("Reset");
            pedestrian.animator.SetInteger("SitStatus", 1);
        }
    }

    public override void Interact(AdvancedAIPedestrianController pedestrian)
    {
        StartCoroutine(MoveToBench(pedestrian));
    }

    public override void StopInteract(AdvancedAIPedestrianController pedestrian)
    {
        if (pedestrian.animator != null)
        {
            pedestrian.animator.SetTrigger("Reset");
            pedestrian.animator.SetInteger("SitStatus", 2);
        }

        int userIndex = currentUsers.IndexOf(pedestrian);

        if (userIndex != -1)
        {
            FreeSpot(userIndex, pedestrian);
        }
    }
    public override Vector3 GetApproachPosition()
    {
        // Возвращаем первую доступную точку подхода
        foreach (Transform point in approachPoints)
        {
            if (point != null)
                return point.position;
        }

        return transform.position + transform.forward * 2f;
    }

    public override Quaternion GetApproachRotation()
    {
        foreach (Transform point in approachPoints)
        {
            if (point != null)
                return point.rotation;
        }

        return transform.rotation;
    }

    public override float GetDurationAction()
    {
        return Random.Range(sitDurationMin, sitDurationMax);
    }

    private int ReserveSpot(AdvancedAIPedestrianController pedestrian)
    {
        for (int i = 0; i < occupiedSpots.Length; i++)
        {
            if (!occupiedSpots[i])
            {
                occupiedSpots[i] = true;
                currentUsers.Add(pedestrian);
                return i;
            }
        }

        return -1;
    }

    private void FreeSpot(int spotIndex, AdvancedAIPedestrianController pedestrian)
    {
        if (spotIndex >= 0 && spotIndex < occupiedSpots.Length)
        {
            occupiedSpots[spotIndex] = false;
            currentUsers.Remove(pedestrian);
        }
    }

    public Vector3 GetSittingPosition(int spotIndex)
    {
        return sittingPoints[spotIndex].position;
    }

    public Quaternion GetSittingRotation(int spotIndex)
    {
        return sittingPoints[spotIndex].rotation;
    }

    private void CreateDefaultSittingPoints() 
    {
        sittingPoints = new Transform[2];

        for (int i = 0; i < 2; i++)
        {
            GameObject point = new GameObject($"SittingPoint_{i}");
            point.transform.SetParent(transform);
            float offset = i == 0 ? -0.5f : 0.5f;
            point.transform.localPosition = new Vector3(offset, 0.5f, 0);
            sittingPoints[i] = point.transform;
        }
    }
    private void CreateDefaultApproachPoints()
    {
        approachPoints = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject point = new GameObject($"ApproachPoint_{i}");
            point.transform.SetParent(transform);

            // Располагаем точки подхода перед лавочкой
            float offset = i == 0 ? -0.8f : 0.8f;
            point.transform.localPosition = new Vector3(offset, 0, 1.5f);

            // Поворачиваем точки к лавочке
            point.transform.LookAt(transform.position);

            approachPoints[i] = point.transform;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 5.0f);

        if (sittingPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (Transform point in sittingPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawSphere(point.position, 0.1f);
                }
            }
        }

        // Визуализация точек подхода
        if (approachPoints != null)
        {
            Gizmos.color = Color.blue;
            foreach (Transform point in approachPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawSphere(point.position, 0.1f);
                    Gizmos.DrawLine(point.position, point.position + point.forward * 1f);
                }
            }
        }
    }
}