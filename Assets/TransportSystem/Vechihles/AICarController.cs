using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class AICarController : MonoBehaviour
{
    [Header("Navigation")]
    public Road currentRoad;
    public int waypointIndex;
    public int laneIndex;
    public bool isLaneForward;

    [Header("Physics & Movement")]
    public float maxSpeed = 15.0f;
    public float acceleration = 5.0f;
    public float brakingPower = 10.0f;
    public float steerSpeed = 5.0f;
    public float arrivalThreshold = 1.5f;

    [Header("Sensors (ACC)")]
    public float sensorLength = 10.0f;
    public float sideSensorLength = 5.0f;
    public LayerMask obstacleMask;
    public LayerMask pedestrianMask;

    [Header("Safety Settings")]
    public float emergencyBrakingMultiplier = 3.0f;
    public float minSafeDistance = 2.0f;

    [Header("Immersive - Visuals")]
    public Transform[] frontWheels;
    public GameObject leftTurnSignal;   // ������ ������ ����������� (���� ��� ���)
    public GameObject rightTurnSignal;  // ������ ������� �����������
    public GameObject brakeLights;      // ������ ����-��������
    public float blinkSpeed = 0.4f;     // �������� �������

    [Header("Immersive - Audio")]
    public AudioClip hornSound;         // ���� �����
    public AudioClip hitSound;          // ���� �����
    public AudioClip explosionSound;    // ���� ������

    [Header("Immersive - Health")]
    public float maxHealth = 100f;
    public GameObject explosionPrefab;  // ������ ������ (Particles)
    public GameObject firePrefab;       // ������ ���� (��� ������������ ������)
    public MeshRenderer carBodyRenderer; // ��� ����� ����� ��� ������ (�����������)
    private GameObject currentFireEffect; // ������ �� �������� �����

    // ���������� ����������
    private float currentHealth;
    private float currentSpeed = 0.0f;
    private float targetSpeed = 0.0f;
    private bool isMoving = false;
    private bool isDead = false;

    private Rigidbody rb;
    private AudioSource audioSource;

    // ��������� ��������
    private bool blockedByTraffic = false;
    private bool blockedByPedestrian = false;
    private float blockedTimer = 0f;    // ������ ��� �����

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        // ��������� ������
        rb.useGravity = true;
        rb.isKinematic = false; // ����� ��� ������ ������������!
        rb.mass = 1500f;        // ������������ �����
        rb.linearDamping = 0.5f;         // ������������� �������
        rb.angularDamping = 2f;    // ������������ ��������

        // ��������� ��������, ����� ������ �� �������������� �� ������ �����,
        // �� ����� ����������� �� ������ (Constraints ����� ������ � �������� ��� ������)
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        currentHealth = maxHealth;
        maxSpeed = Random.Range(maxSpeed * 0.9f, maxSpeed * 1.1f);

        if (currentRoad != null)
        {
            ResetAI(currentRoad, waypointIndex, laneIndex);
        }
    }

    void Update()
    {
        if (isDead) return; // ������� ������ �� �����
        if (!isMoving || currentRoad == null) return;

        SensorsLogic();
        MovementLogic();
        SteeringVisuals();
        ImmersiveVisualsLogic(); // ���� � �����������
        ImmersiveAudioLogic();   // �����
    }

    // ---------------------------------------------------------
    // ������ �������� (� ������� �������� + ���������� ������)
    // ---------------------------------------------------------
    void SensorsLogic()
    {
        blockedByTraffic = false;
        blockedByPedestrian = false;

        float dynamicSensorLength = Mathf.Max(sensorLength, currentSpeed * 1.5f);
        float obstacleDistance = dynamicSensorLength;

        Vector3 sensorStart = transform.position + Vector3.up * 0.5f + transform.forward * 1.5f;

        // 1. ������
        RaycastHit hit;
        if (Physics.Raycast(sensorStart, transform.forward, out hit, dynamicSensorLength, obstacleMask))
        {
            // ���������� ���� � ������
            if (hit.collider.gameObject != gameObject && !hit.collider.GetComponent<Road>())
            {
                blockedByTraffic = true;
                obstacleDistance = hit.distance;
            }
        }

        // 2. ��������
        if (Physics.SphereCast(sensorStart, 1.0f, transform.forward, out hit, sideSensorLength, pedestrianMask))
        {
            blockedByPedestrian = true;
            obstacleDistance = Mathf.Min(obstacleDistance, hit.distance);
        }

        // ������ ������� ��������
        if (blockedByTraffic || blockedByPedestrian)
        {
            if (obstacleDistance < 1.5f)
            {
                targetSpeed = 0;
                currentSpeed = 0;
            }
            else if (obstacleDistance < minSafeDistance)
            {
                targetSpeed = 0;
            }
            else
            {
                float factor = (obstacleDistance - minSafeDistance) / (dynamicSensorLength - minSafeDistance);
                targetSpeed = maxSpeed * Mathf.Clamp01(factor);
            }
        }
        else
        {
            targetSpeed = maxSpeed;
        }
    }

    // ---------------------------------------------------------
    // ��������
    // ---------------------------------------------------------
    void MovementLogic()
    {
        Vector3 targetPoint = GetTargetPoint();
        targetPoint.y = transform.position.y;

        // ������� (����� ������, ���� kinematic=false, ���� ����� Transform, ���� ���������)
        // ��� ���������� ���������� RotateTowards + MovePosition/Translate, 
        // �� ��� ��� �� �������� ������ (isKinematic=false), ����� ������� ����� Velocity, 
        // ����� ������������ �������� ���������.

        // 1. �������
        Vector3 directionToTarget = (targetPoint - transform.position).normalized;
        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, steerSpeed * 10f * Time.deltaTime);
        }

        // 2. ������ / ����������
        if (currentSpeed < targetSpeed)
        {
            currentSpeed += acceleration * Time.deltaTime;
        }
        else if (currentSpeed > targetSpeed)
        {
            float currentBrakingPower = brakingPower;
            if (targetSpeed < 0.1f) currentBrakingPower *= emergencyBrakingMultiplier;
            currentSpeed -= currentBrakingPower * Time.deltaTime;
        }
        currentSpeed = Mathf.Max(0, currentSpeed);

        // 3. ���������� �������� � Rigidbody (������ Translate)
        // ��� ��������� ������� �������� Unity ��������� ������������ �����
        Vector3 velocity = transform.forward * currentSpeed;
        velocity.y = rb.linearVelocity.y; // ��������� ����������
        rb.linearVelocity = velocity;

        // 4. ���������
        if (Vector3.Distance(transform.position, targetPoint) < arrivalThreshold)
        {
            AdvanceWaypoint();
        }
    }

    // ---------------------------------------------------------
    // �������������: ���� (����������� � �����)
    // ---------------------------------------------------------
    void ImmersiveVisualsLogic()
    {
        // --- ����������� ---
        // ����������, ���� ������������ ������, ��������� ������� ������� � ����������� � ����
        Vector3 targetPoint = GetTargetPoint();
        Vector3 relativeDir = transform.InverseTransformPoint(targetPoint);

        // ���� �������� (������� ������)
        float turnAngle = relativeDir.x;

        bool turningLeft = turnAngle < -0.5f;  // ����� ������������
        bool turningRight = turnAngle > 0.5f;

        // ������� (���/���� ������ blinkSpeed ������)
        bool blinkState = Mathf.Repeat(Time.time, blinkSpeed * 2) < blinkSpeed;

        if (leftTurnSignal) leftTurnSignal.SetActive(turningLeft && blinkState);
        if (rightTurnSignal) rightTurnSignal.SetActive(turningRight && blinkState);

        // --- ����-������� ---
        // �����, ���� �������� (current > target) ��� ����� (current < 0.1)
        bool isBraking = (currentSpeed > targetSpeed + 0.5f) || (currentSpeed < 0.1f && blockedByTraffic);

        if (brakeLights) brakeLights.SetActive(isBraking);
    }

    // ---------------------------------------------------------
    // �������������: ����� (�����)
    // ---------------------------------------------------------
    void ImmersiveAudioLogic()
    {
        if (blockedByTraffic || blockedByPedestrian)
        {
            // ���� ����� � �������������
            if (currentSpeed < 0.5f)
            {
                blockedTimer += Time.deltaTime;
                // ���� ���� ��� 2 ������� � ��� �� ���������
                if (blockedTimer > 2.0f && !audioSource.isPlaying)
                {
                    // ��������� ���� �������� (����� �� ������ ��� ����� ������ �������)
                    if (Random.value < 0.02f)
                    {
                        PlaySound(hornSound, 1.0f);
                        blockedTimer = 0f; // �����
                    }
                }
            }
        }
        else
        {
            blockedTimer = 0f;
        }
    }

    // ---------------------------------------------------------
    // ������� �������� � ������
    // ---------------------------------------------------------
    void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;

        // ���������� ����� � ������, ����� �� ���������� �� ������
        if (collision.gameObject.layer == LayerMask.NameToLayer("Roads") ||
            collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
            return;

        Debug.Log( "Colision on: " + collision.gameObject.name);

        // ���� �����
        float impactForce = collision.relativeVelocity.magnitude;

        // ���� ���� ������� (> 5 �/�)
        if (impactForce > 5.0f)
        {
            // ����
            float damage = impactForce * 2.0f; // ����������� �����
            currentHealth -= damage;

            // ���� ����� (��������� ������� �� ����)
            PlaySound(hitSound, Mathf.Clamp01(impactForce / 20f));

            // �������� ������
            if (currentHealth <= 0)
            {
                Explode();
            }
        }
    }

    void Explode()
    {
        if (isDead) return;
        isDead = true;

        // 1. ������ ������ (One-shot ������)
        if (explosionPrefab)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, transform.rotation);
            // �����: ������� ��� ����� ����� 5 ������, ����� �� �� ������� �����
            Destroy(explosion, 5.0f);
        }

        if (explosionSound) AudioSource.PlayClipAtPoint(explosionSound, transform.position);

        // 2. ������ ���� (Loop ������, �������� � ������)
        if (firePrefab)
        {
            // ��������� ������ �� ��������� �����!
            currentFireEffect = Instantiate(firePrefab, transform);
            currentFireEffect.transform.localPosition = Vector3.zero;
        }

        // ... (��������� ���: ����� �����, ���������� ���, ������) ...
        if (carBodyRenderer) carBodyRenderer.material.color = Color.black;
        if (leftTurnSignal) leftTurnSignal.SetActive(false);
        if (rightTurnSignal) rightTurnSignal.SetActive(false);
        if (brakeLights) brakeLights.SetActive(false);

        rb.constraints = RigidbodyConstraints.None;
        rb.AddExplosionForce(5000f, transform.position + Vector3.down, 5f);

        StartCoroutine(DespawnAfterDeath());
    }

    // ����� ����� ��� ������� ��������
    void CleanupEffects()
    {
        if (currentFireEffect != null)
        {
            Destroy(currentFireEffect);
            currentFireEffect = null;
        }
    }

    IEnumerator DespawnAfterDeath()
    {
        yield return new WaitForSeconds(10f); // ����� 10 ������

        // ������� ����� ��������� � ���
        CleanupEffects();

        TrafficSpawner spawner = FindObjectOfType<TrafficSpawner>();
        if (spawner) spawner.ReturnVehicleToPool(gameObject);
        else gameObject.SetActive(false);

        // ����� ���������
        isDead = false;
        currentHealth = maxHealth;
        if (carBodyRenderer) carBodyRenderer.material.color = Color.white;
    }

    // ---------------------------------------------------------
    // ��������������� ������
    // ---------------------------------------------------------
    void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    void SteeringVisuals()
    {
        if (frontWheels == null || frontWheels.Length == 0) return;
        Vector3 targetPoint = GetTargetPoint();
        Vector3 relativePos = transform.InverseTransformPoint(targetPoint);
        float steeringAngle = Mathf.Atan2(relativePos.x, relativePos.z) * Mathf.Rad2Deg;
        steeringAngle = Mathf.Clamp(steeringAngle, -45, 45);

        foreach (var wheel in frontWheels)
        {
            Vector3 currentEuler = wheel.localEulerAngles;
            // �������� ����� (velocity.magnitude, ��� ��� �� ���������� ������)
            float rotationAmount = rb.linearVelocity.magnitude * Time.deltaTime * 100f;
            wheel.localRotation = Quaternion.Euler(currentEuler.x + rotationAmount, steeringAngle, 0);
        }
    }

    // ... (��������� ������: GetTargetPoint, AdvanceWaypoint, ResetAI, GetTargetPoint, TryFindNextRoad 
    // ...  ��������� ��� � ���������� ������, ��� �� ��������)
    Vector3 GetTargetPoint()
    {
        if (currentRoad == null) return transform.position;
        return currentRoad.GetCenterPointByIdAndLane(laneIndex, waypointIndex);
    }

    void AdvanceWaypoint()
    {
        int nextIndex = isLaneForward ? waypointIndex + 1 : waypointIndex - 1;
        int limit = currentRoad.GetLaneCenterPoints(laneIndex).Count;

        if ((isLaneForward && nextIndex >= limit) || (!isLaneForward && nextIndex < 0))
        {
            if (!TryFindNextRoad()) RemoveCar();
        }
        else
        {
            waypointIndex = nextIndex;
        }
    }

    bool TryFindNextRoad()
    {
        // ���������� ������ ��� �������
        Vector3 searchPos = transform.position + transform.forward * 3.0f + Vector3.up * 2.0f;
        RaycastHit[] hits = Physics.SphereCastAll(searchPos, 2.0f, Vector3.down, 10.0f, LayerMask.GetMask("Roads"));
        foreach (var hit in hits)
        {
            Road nextRoad = hit.collider.GetComponent<Road>();
            if (nextRoad != null && nextRoad != currentRoad && nextRoad.isCanUseAutomobile())
            {
                // ������ ����� ������
                currentRoad = nextRoad;
                // (��������: ���� ��������� ������ � �����)
                int nl; int np; Vector3 p; float d;
                if (nextRoad.FindNearestLane(transform.position, out nl, out np, out p, out d))
                {
                    laneIndex = nl;
                    waypointIndex = np; // ��������
                    isLaneForward = nextRoad.isLineForward(nl);
                    return true;
                }
            }
        }
        return false;
    }

    void RemoveCar()
    {
        TrafficSpawner spawner = FindObjectOfType<TrafficSpawner>();
        if (spawner) spawner.ReturnVehicleToPool(gameObject);
        else gameObject.SetActive(false);
    }

    public void ResetAI(Road road, int pointIndex, int lane)
    {
        CleanupEffects(); // <-- ����������, ��� ������� ���� ���

        currentRoad = road;
        // ... (��������� ��� ResetAI ��� ���������)
        waypointIndex = pointIndex;
        laneIndex = lane;
        isLaneForward = road.isLineForward(lane);
        isMoving = true;
        isDead = false;
        currentHealth = maxHealth;
        currentSpeed = maxSpeed * 0.5f;

        if (carBodyRenderer) carBodyRenderer.material.color = Color.white;
        if (leftTurnSignal) leftTurnSignal.SetActive(false);
        if (rightTurnSignal) rightTurnSignal.SetActive(false);
        if (brakeLights) brakeLights.SetActive(false);

        Vector3 pos = GetTargetPoint();
        transform.position = pos;
        if (rb) rb.linearVelocity = Vector3.zero;
        if (rb) rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // ���������� ����������� ��������
    }

    // ����� ������� �������� ������� ��� ���������� �������
    void OnDisable()
    {
        CleanupEffects();
    }

    // Gizmos ��� ������� ��������
    void OnDrawGizmos()
    {
        // ������ ���� � ������� ����
        if (currentRoad != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 target = GetTargetPoint();
            Gizmos.DrawLine(transform.position, target);
            Gizmos.DrawWireSphere(target, 0.5f);
        }

        // --- ������������ �������� ---
        Vector3 sensorStart = transform.position + Vector3.up * 0.5f + transform.forward * 1.5f;

        // 1. ����������� ������ (��� �����)
        // ��������� ������������ ����� ������� (��. ������ ����)
        float effectiveSensorLength = Mathf.Max(sensorLength, currentSpeed * 1.5f); // ������������ �����

        bool isHitCentral = Physics.Raycast(sensorStart, transform.forward, out RaycastHit hitCentral, effectiveSensorLength, obstacleMask);

        if (isHitCentral)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(sensorStart, hitCentral.point);
            Gizmos.DrawSphere(hitCentral.point, 0.2f); // ����� �����
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(sensorStart, sensorStart + transform.forward * effectiveSensorLength);
        }

        // 2. ������ ��������� (SphereCast)
        // ������ "�����" �������
        bool isHitSide = Physics.SphereCast(sensorStart, 1.0f, transform.forward, out RaycastHit hitSide, sideSensorLength, pedestrianMask);

        Gizmos.color = isHitSide ? new Color(1, 0, 1, 0.5f) : new Color(1, 1, 0, 0.2f); // ��������� ��� �����, ������ ��� �����

        Vector3 endPoint = isHitSide ? hitSide.point : (sensorStart + transform.forward * sideSensorLength);

        // ������ ����� � ����� ���� (���������� ������������ SphereCast)
        Gizmos.DrawWireSphere(endPoint, 1.0f);
        Gizmos.DrawLine(sensorStart, endPoint);
    }
}