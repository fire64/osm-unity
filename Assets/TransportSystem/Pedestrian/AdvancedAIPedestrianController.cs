using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.AI;

public enum PedestrianState
{
    FollowingPath,
    AvoidingObstacle,
    Waiting,
    CrossingRoad,
    Talking,
    Interacting,
    ApproachingInteraction
}

public class AdvancedAIPedestrianController : MonoBehaviour
{
    [Header("Base Movement Settings")]
    public float maxSpeed = 2.0f;
    public float acceleration = 2.0f;
    public float deceleration = 4.0f;
    public float rotationSpeed = 2.0f;
    public float stoppingDistance = 1.0f;

    [Header("Sensor Settings")]
    public float frontSensorRange = 5.0f;
    public float sideSensorRange = 3.0f;
    public float sensorAngle = 30.0f;
    public int sensorCount = 3;
    public LayerMask obstacleMask;
    public LayerMask roadMask;
    public LayerMask vehicleMask;

    [Header("Path Following")]
    public float waypointThreshold = 1.0f;
    public float predictionDistance = 1.0f;

    [Header("Crossing Settings")]
    public float crossingCheckDistance = 10.0f;
    public float minCrossingWaitTime = 2.0f;
    public float maxCrossingWaitTime = 5.0f;
    public float crossingSpeedMultiplier = 1.5f;

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float collisionDamageMultiplier = 5f;

    [Header("Visual Effects")]
    public GameObject walkingEffect;
    public GameObject injuredEffect;

    [Header("Audio Effects")]
    public AudioClip footstepSound;
    public AudioClip collisionSound;
    public float footstepInterval = 0.5f;

    [Header("UMA 2 Settings")]
    public UMA2PedestrianGenerator umaGenerator;
    public bool regenerateOnSpawn = true;

    // Добавляем новые переменные для системы разговоров
    [Header("Conversation Settings")]
    public float conversationRange = 3.0f;
    public float minConversationTime = 5.0f;
    public float maxConversationTime = 15.0f;
    public float conversationCooldown = 30.0f;
    public float conversationProbability = 0.3f;

    private AdvancedAIPedestrianController conversationPartner;
    private float lastConversationTime = -Mathf.Infinity;
    private float conversationEndTime;

    [Header("Interaction System")]
    public float interactionNeed = 0f;
    public float interactionNeedIncreaseRate = 0.01f;
    public float interactionDetectionRange = 10f;
    public float interactionProbability = 0.7f; // Перенесено из InteractiveObject

    private InteractiveObject currentInteractiveObject;
    private int interactionSpotIndex = -1;
    private float interactionStartTime;
    private float interactionDuration;
    private Vector3 interactionApproachTarget;
    private bool isApproachingInteraction = false;

    [Header("Other Settings")]
    // Internal variables
    public List<Vector3> waypoints;
    public int currentWaypointIndex = 0;
    private bool isMovingForward = true;
    public PedestrianState currentState = PedestrianState.FollowingPath;
    public float currentSpeed = 0f;
    private Vector3 avoidanceVector;
    public Road currentRoad;
    public Rigidbody pedestrianRigidbody;
    public AudioSource audioSource;
    public Animator animator;

    // Health and damage
    private float currentHealth;
    private bool isInjured = false;
    private float footstepTimer = 0f;
    private float crossingWaitTimer = 0f;

    // Debug
    public bool showDebug = true;

    [Header("Spawn Settings")]
    public bool isSpawned = false;

    [Header("Collision Settings")]
    public string[] ignoreCollisionTags = { "Pedestrian" };
    public string[] ignoreCollisionNames = { "Tile_", "Terrain", "Sidewalk" };

    private float collisionCooldown = 0f;

    void AdnimatorSetBool(string command, bool value)
    {

    }

    void AnimatorReset()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null)
        {
            animator.SetTrigger("Reset");
        }
    }

    void AnimatorSetBool(string key, bool values)
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null)
        {
            animator.SetBool(key, values);
        }
    }

    void AnimatorSetFloat(string key, float values)
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null)
        {
            animator.SetFloat(key, values);
        }
    }

    public void InitializeFromSpawner(Road road, int startWaypointIndex, bool startMovingForward)
    {
        this.currentRoad = road;
        this.waypoints = road.coordpoints;
        this.currentWaypointIndex = startWaypointIndex;
        this.isMovingForward = startMovingForward;
        this.isSpawned = true;

        // Генерируем UMA 2 аватар при инициализации
        if (regenerateOnSpawn && umaGenerator != null)
        {
            // Ждем немного перед генерацией, чтобы избежать конфликтов
            StartCoroutine(GenerateUMAWithDelay(0.1f));
        }

        FindClosestWaypoint();
    }

    public void ResetAI(Road road, int waypointIndex, bool moveForward)
    {
        this.currentRoad = road;
        this.waypoints = road.coordpoints;
        this.currentWaypointIndex = waypointIndex;
        this.isMovingForward = moveForward;
        this.isSpawned = true;

        this.isInjured = false;
        this.currentHealth = maxHealth;

        // Отключить визуальные эффекты травм
        if (injuredEffect != null)
            injuredEffect.SetActive(false);

        // Включить визуальные эффекты ходьбы
        if (walkingEffect != null)
            walkingEffect.SetActive(true);

        FindClosestWaypoint();
    }

    IEnumerator GenerateUMAWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        umaGenerator.Regenerate();
    }

    void Start()
    {
        pedestrianRigidbody = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        umaGenerator = GetComponent<UMA2PedestrianGenerator>();

        if (pedestrianRigidbody == null)
        {
            pedestrianRigidbody = gameObject.AddComponent<Rigidbody>();
            pedestrianRigidbody.mass = 70f;
            pedestrianRigidbody.drag = 2f;
            pedestrianRigidbody.angularDrag = 2f;
            pedestrianRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        currentHealth = maxHealth;
    }

    void Update()
    {
        if (!isSpawned || isInjured)
            return;

        collisionCooldown -= Time.deltaTime;
        footstepTimer -= Time.deltaTime;

        // Проверяем, не упал ли пешеход (высота меньше -5)
        if (transform.position.y < -5f)
        {
            RemoveFallenPedestrian();
            return;
        }

        // Обновляем потребность во взаимодействии
        if (currentState != PedestrianState.Interacting)
        {
            interactionNeed += interactionNeedIncreaseRate * Time.deltaTime;
        }
        else
        {
            interactionNeed = Mathf.Max(0, interactionNeed - Time.deltaTime * 0.1f);
        }

        // Проверяем, не пора ли закончить взаимодействие
        if (currentState == PedestrianState.Interacting &&
            Time.time - interactionStartTime >= interactionDuration)
        {
            StopInteraction();
        }

        // Если потребность во взаимодействии высокая и мы не уже взаимодействуем, ищем объекты
        if (currentState == PedestrianState.FollowingPath &&
            interactionNeed > 0.7f &&
            currentInteractiveObject == null && // Важное условие!
            Random.value < interactionProbability * Time.deltaTime) // Используем общую вероятность
        {
            TryFindInteractiveObject();
        }

        CheckRoadBelow();
        UpdateState();
        HandleState();
        UpdateVisualEffects();
        UpdateAnimation();
        UpdateConversation();

        if (footstepTimer <= 0 && currentSpeed > 0.1f)
        {
            PlayFootstepSound();
            footstepTimer = footstepInterval;
        }
    }

    void TryFindInteractiveObject()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionDetectionRange);
        List<InteractiveObject> availableObjects = new List<InteractiveObject>();

        foreach (Collider col in colliders)
        {
            InteractiveObject interactiveObj = col.GetComponent<InteractiveObject>();
            if (interactiveObj != null && interactiveObj.IsAvailable())
            {
                availableObjects.Add(interactiveObj);
            }
        }

        if (availableObjects.Count > 0)
        {
            InteractiveObject closestObject = null;
            float closestDistance = Mathf.Infinity;

            foreach (InteractiveObject obj in availableObjects)
            {
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestObject = obj;
                }
            }

            StartInteraction(closestObject);
        }
    }

    public void StartInteraction(InteractiveObject interactiveObject, int spotIndex = -1)
    {
        currentInteractiveObject = interactiveObject;
        interactionSpotIndex = spotIndex;
        interactionApproachTarget = currentInteractiveObject.GetApproachPosition();
        isApproachingInteraction = true;
        currentState = PedestrianState.ApproachingInteraction;
    }

    void StopInteraction()
    {
        if (currentState != PedestrianState.Interacting || currentInteractiveObject == null) return;

        currentInteractiveObject.StopInteract(this);

        AnimatorReset();

        currentInteractiveObject = null;
        interactionSpotIndex = -1;
        currentState = PedestrianState.FollowingPath;
        FindClosestWaypoint();
    }

    void InterruptInteraction()
    {
        if (currentState == PedestrianState.Interacting ||
            currentState == PedestrianState.ApproachingInteraction)
        {
            StopAllCoroutines();

            if (currentInteractiveObject != null)
            {
                currentInteractiveObject.StopInteract(this);
            }

            currentInteractiveObject = null;
            interactionSpotIndex = -1;

            AnimatorReset();

        }
    }

    // Новый метод для движения к цели с обходом препятствий
    void MoveToTargetWithAvoidance(Vector3 target, float speedMultiplier = 1.0f)
    {
        // Проверяем препятствия
        ObstacleInfo obstacleInfo = CheckForObstacles();

        if (obstacleInfo.detected && obstacleInfo.distance < frontSensorRange * 0.5f)
        {
            // Рассчитываем направление для обхода
            CalculateAvoidancePath(obstacleInfo);

            // Двигаемся в направлении обхода
            Vector3 avoidDirection = avoidanceVector.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(avoidDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // Двигаемся вперед
            currentSpeed = Mathf.Lerp(currentSpeed, maxSpeed * 0.7f * speedMultiplier, acceleration * Time.deltaTime);
            transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
        }
        else
        {
            // Двигаемся напрямую к цели
            Vector3 direction = (target - transform.position).normalized;
            direction.y = 0;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            float distanceToTarget = Vector3.Distance(transform.position, target);
            float targetSpeed = maxSpeed * speedMultiplier;

            if (distanceToTarget < stoppingDistance)
            {
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed * (distanceToTarget / stoppingDistance), deceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            }

            transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
        }
    }

    void UpdateConversation()
    {
        switch (currentState)
        {
            case PedestrianState.Talking:
                // Проверяем, не закончился ли разговор
                if (Time.time >= conversationEndTime)
                {
                    EndConversation();
                }
                // Проверяем, не прервался ли разговор (партнер ушел или сменил состояние)
                else if (conversationPartner == null ||
                         conversationPartner.currentState != PedestrianState.Talking ||
                         Vector3.Distance(transform.position, conversationPartner.transform.position) > conversationRange * 1.5f)
                {
                    EndConversation();
                }
                break;

            case PedestrianState.FollowingPath:
                // Периодически проверяем возможность начать разговор
                if (Time.time - lastConversationTime > conversationCooldown &&
                    Random.value < conversationProbability * Time.deltaTime)
                {
                    TryStartConversation();
                }
                break;
        }
    }

    void TryStartConversation()
    {
        // Ищем nearby пешеходов
        Collider[] nearbyPedestrians = Physics.OverlapSphere(transform.position, conversationRange);
        foreach (Collider col in nearbyPedestrians)
        {
            AdvancedAIPedestrianController otherPedestrian = col.GetComponent<AdvancedAIPedestrianController>();

            // Проверяем, подходит ли пешеход для разговора
            if (IsSuitableConversationPartner(otherPedestrian))
            {
                StartConversation(otherPedestrian);
                return;
            }
        }
    }

    bool IsSuitableConversationPartner(AdvancedAIPedestrianController otherPedestrian)
    {
        // Проверяем основные условия
        if (otherPedestrian == null ||
            otherPedestrian == this ||
            otherPedestrian.isInjured ||
            otherPedestrian.currentState != PedestrianState.FollowingPath ||
            Time.time - otherPedestrian.lastConversationTime < otherPedestrian.conversationCooldown)
        {
            return false;
        }

        // Проверяем, смотрит ли пешеход в нашу сторону (опционально)
        Vector3 directionToOther = (otherPedestrian.transform.position - transform.position).normalized;
        float dotProduct = Vector3.Dot(transform.forward, directionToOther);

        // Если пешеход смотрит в нашу сторону (примерно), увеличиваем вероятность
        return dotProduct > -0.5f; // -0.5f позволяет начать разговор даже если не смотрят прямо друг на друга
    }

    void StartConversation(AdvancedAIPedestrianController partner)
    {
        // Устанавливаем состояние разговора
        currentState = PedestrianState.Talking;
        partner.currentState = PedestrianState.Talking;

        // Сохраняем ссылки на партнеров
        conversationPartner = partner;
        partner.conversationPartner = this;

        // Устанавливаем время окончания разговора
        float conversationDuration = Random.Range(minConversationTime, maxConversationTime);
        conversationEndTime = Time.time + conversationDuration;
        partner.conversationEndTime = conversationEndTime;

        // Обновляем время последнего разговора
        lastConversationTime = Time.time;
        partner.lastConversationTime = Time.time;

        // Останавливаем движение
        currentSpeed = 0f;
        partner.currentSpeed = 0f;

        // Запускаем анимацию разговора
        AnimatorReset();
        AnimatorSetBool("Talk", true);

        partner.AnimatorReset();
        partner.AnimatorSetBool("Talk", true);

        // Поворачиваемся друг к другу
        StartCoroutine(RotateTowardsEachOther());
    }

    IEnumerator RotateTowardsEachOther()
    {
        float rotationTime = 1.0f;
        float elapsedTime = 0f;

        Vector3 initialRotation = transform.rotation.eulerAngles;
        Vector3 targetDirection = (conversationPartner.transform.position - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(new Vector3(targetDirection.x, 0, targetDirection.z));

        while (elapsedTime < rotationTime && currentState == PedestrianState.Talking)
        {
            elapsedTime += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, elapsedTime / rotationTime);

            if (conversationPartner != null)
            {
                Vector3 partnerTargetDirection = (transform.position - conversationPartner.transform.position).normalized;
                Quaternion partnerTargetRotation = Quaternion.LookRotation(new Vector3(partnerTargetDirection.x, 0, partnerTargetDirection.z));
                conversationPartner.transform.rotation = Quaternion.Slerp(
                    conversationPartner.transform.rotation,
                    partnerTargetRotation,
                    elapsedTime / rotationTime);
            }

            yield return null;
        }
    }

    void EndConversation()
    {
        if (currentState == PedestrianState.Talking)
        {
            // Возвращаемся к обычному состоянию
            currentState = PedestrianState.FollowingPath;

            // Запускаем анимацию разговора
            AnimatorSetBool("Talk", false);
            conversationPartner.AnimatorSetBool("Talk", false);

            // Сбрасываем ссылку на партнера
            if (conversationPartner != null && conversationPartner.currentState == PedestrianState.Talking)
            {
                conversationPartner.currentState = PedestrianState.FollowingPath;
                conversationPartner.conversationPartner = null;
            }

            conversationPartner = null;

            // Можно добавить небольшую задержку перед возобновлением движения
            StartCoroutine(ResumeMovementAfterDelay(1.0f));
        }
    }

    IEnumerator ResumeMovementAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // После разговора продолжаем движение
        if (currentState == PedestrianState.FollowingPath)
        {
            // Можно добавить небольшую случайную задержку перед началом движения
            yield return new WaitForSeconds(Random.Range(0.5f, 2.0f));

            // Возобновляем движение
            FindClosestWaypoint();
        }
    }

    // Добавляем метод для удаления упавших пешеходов
    void RemoveFallenPedestrian()
    {
        // Найти спавнер и вернуть пешехода в пул
        TrafficSpawner spawner = FindObjectOfType<TrafficSpawner>();
        if (spawner != null)
        {
            spawner.ReturnPedestrianToPool(gameObject);
        }
        else
        {
            // Если спавнер не найден, просто деактивировать
            gameObject.SetActive(false);
        }
    }

    bool ShouldIgnoreCollision(GameObject collisionObject)
    {
        foreach (string tag in ignoreCollisionTags)
        {
            if (collisionObject.CompareTag(tag)) return true;
        }

        foreach (string name in ignoreCollisionNames)
        {
            if (collisionObject.name.StartsWith(name)) return true;
        }

        if (collisionObject.TryGetComponent<Road>(out Road road)) return true;
        if (collisionObject.TryGetComponent<Landuse>(out Landuse landuse)) return true;

        return false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isInjured) return;

        if (ShouldIgnoreCollision(collision.gameObject))
        {
            return;
        }

        float impactForce = collision.relativeVelocity.magnitude;

        if (impactForce < 1f)
            return;

        if (collisionCooldown > 0)
            return;

        collisionCooldown = 0.5f;

        float damage = impactForce * collisionDamageMultiplier;
        TakeDamage(damage);

        if (impactForce > 2f && collisionSound != null)
        {
            audioSource.PlayOneShot(collisionSound);
        }
    }

    void TakeDamage(float damage)
    {
        // Прерываем взаимодействие при получении повреждений
        if (currentState == PedestrianState.Interacting ||
            currentState == PedestrianState.ApproachingInteraction)
        {
            InterruptInteraction();
        }

        currentHealth -= damage;

        if (currentHealth <= 0 && !isInjured)
        {
            InjurePedestrian();
        }
    }

    void InjurePedestrian()
    {
        isInjured = true;
        currentSpeed = 0f;

        if (injuredEffect != null)
        {
            Instantiate(injuredEffect, transform.position, Quaternion.identity);
        }

        AnimatorSetBool("IsInjured", true);

        // Для UMA аватаров можно добавить специальные анимации травм
        if (umaGenerator != null)
        {
            // Можно изменить позу аватара или добавить визуальные эффекты травм
        }

        StartCoroutine(RecoverAfterDelay(10f));
    }

    IEnumerator RecoverAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isInjured = false;
        currentHealth = maxHealth;
        AnimatorSetBool("IsInjured", false);
    }

    void PlayFootstepSound()
    {
        if (footstepSound != null)
        {
            audioSource.PlayOneShot(footstepSound);
        }
    }

    void UpdateVisualEffects()
    {
        if (walkingEffect != null)
        {
            walkingEffect.SetActive(currentSpeed > 0.1f);
        }
    }

    void UpdateAnimation()
    {
        AnimatorSetFloat("Speed", currentSpeed);

        // Добавляем анимацию для UMA аватаров
        if (umaGenerator != null)
        {
            // Можно добавить дополнительную анимационную логику для UMA
            //           AnimatorSetBool("IsWalking", currentSpeed > 0.1f);
            //           AnimatorSetBool("IsRunning", currentSpeed > maxSpeed * 0.7f);
        }
    }

    void CheckRoadBelow()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 2.0f, roadMask))
        {
            Road road = hit.collider.GetComponent<Road>();

            if (road != null && road != currentRoad && road.isCanUsePedestrian())
            {
                currentRoad = road;
                waypoints = road.coordpoints;
                FindClosestWaypoint();
            }
        }
        else if (currentRoad != null)
        {
            currentRoad = null;
        }
    }

    void UpdateState()
    {
        ObstacleInfo obstacleInfo = CheckForObstacles();
        VehicleInfo vehicleInfo = CheckForVehicles();

        switch (currentState)
        {
            case PedestrianState.FollowingPath:
                if (obstacleInfo.detected && obstacleInfo.distance < frontSensorRange * 0.5f)
                {
                    currentState = PedestrianState.AvoidingObstacle;
                    CalculateAvoidancePath(obstacleInfo);
                }
                else if (vehicleInfo.detected && vehicleInfo.distance < crossingCheckDistance)
                {
                    currentState = PedestrianState.Waiting;
                    crossingWaitTimer = Random.Range(minCrossingWaitTime, maxCrossingWaitTime);
                }
                break;

            case PedestrianState.AvoidingObstacle:
                if (!obstacleInfo.detected || obstacleInfo.distance > frontSensorRange * 0.8f)
                {
                    currentState = PedestrianState.FollowingPath;
                }
                break;

            case PedestrianState.Waiting:
                crossingWaitTimer -= Time.deltaTime;
                if (crossingWaitTimer <= 0 && !vehicleInfo.detected)
                {
                    currentState = PedestrianState.CrossingRoad;
                }
                break;

            case PedestrianState.CrossingRoad:
                if (!vehicleInfo.detected || Vector3.Distance(transform.position, waypoints[currentWaypointIndex]) < waypointThreshold)
                {
                    currentState = PedestrianState.FollowingPath;
                }
                break;

            case PedestrianState.ApproachingInteraction:
                if (currentInteractiveObject != null &&
                    Vector3.Distance(transform.position, interactionApproachTarget) < currentInteractiveObject.approachDistance)
                {
                    currentState = PedestrianState.Interacting;
                    currentInteractiveObject.Interact(this);

                    // Устанавливаем длительность взаимодействия
                    interactionDuration = currentInteractiveObject.GetDurationAction();
                    interactionStartTime = Time.time;
                }
                break;
        }
    }

    void HandleState()
    {
        switch (currentState)
        {
            case PedestrianState.FollowingPath:
                FollowWaypoints();
                break;

            case PedestrianState.AvoidingObstacle:
                AvoidObstacle();
                break;

            case PedestrianState.Waiting:
                StopMoving();
                break;

            case PedestrianState.CrossingRoad:
                CrossRoad();
                break;

            case PedestrianState.ApproachingInteraction:
                MoveToTargetWithAvoidance(interactionApproachTarget);
                break;

            case PedestrianState.Interacting:
                currentSpeed = 0f;
                break;
        }
    }

    /*
        ObstacleInfo CheckForObstacles()
        {
            ObstacleInfo info = new ObstacleInfo();
            info.detected = false;
            info.distance = Mathf.Infinity;

            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, frontSensorRange, obstacleMask))
            {
                info.detected = true;
                info.distance = hit.distance;
                info.direction = transform.forward;
                info.hitPoint = hit.point;
            }

            for (int i = 0; i < sensorCount; i++)
            {
                float angle = -sensorAngle / 2 + (sensorAngle / (sensorCount - 1)) * i;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

                if (Physics.Raycast(transform.position, direction, out hit, sideSensorRange, obstacleMask))
                {
                    if (hit.distance < info.distance)
                    {
                        info.detected = true;
                        info.distance = hit.distance;
                        info.direction = direction;
                        info.hitPoint = hit.point;
                    }
                }
            }

            return info;
        }
    */


    ObstacleInfo CheckForObstacles()
    {
        ObstacleInfo info = new ObstacleInfo();
        info.detected = false;
        info.distance = Mathf.Infinity;

        // Добавляем смещение по высоте
        Vector3 sensorHeight = Vector3.up * 0.15f;
        Vector3 adjustedPosition = transform.position + sensorHeight;

        RaycastHit hit;

        // Передний датчик
        if (Physics.Raycast(adjustedPosition, transform.forward, out hit, frontSensorRange, obstacleMask))
        {
            UpdateObstacleInfo(ref info, hit, transform.forward);
        }

        // Боковые датчики
        for (int i = 0; i < sensorCount; i++)
        {
            float angle = -sensorAngle / 2 + (sensorAngle / (sensorCount - 1)) * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.Raycast(adjustedPosition, direction, out hit, sideSensorRange, obstacleMask))
            {
                if (hit.distance < info.distance)
                {
                    UpdateObstacleInfo(ref info, hit, direction);
                }
            }
        }

        return info;
    }

    // Вспомогательный метод для обновления информации о препятствии
    void UpdateObstacleInfo(ref ObstacleInfo info, RaycastHit hit, Vector3 direction)
    {
        info.detected = true;
        info.distance = hit.distance;
        info.direction = direction;
        info.hitPoint = hit.point;
    }
    VehicleInfo CheckForVehicles()
    {
        VehicleInfo info = new VehicleInfo();
        info.detected = false;
        info.distance = Mathf.Infinity;

        Collider[] vehicles = Physics.OverlapSphere(transform.position, crossingCheckDistance, vehicleMask);
        foreach (Collider vehicle in vehicles)
        {
            float distance = Vector3.Distance(transform.position, vehicle.transform.position);
            if (distance < info.distance)
            {
                info.detected = true;
                info.distance = distance;
                info.vehicle = vehicle.gameObject;
            }
        }

        return info;
    }

    void CalculateAvoidancePath(ObstacleInfo obstacleInfo)
    {
        Vector3 hitPoint = obstacleInfo.hitPoint;
        Vector3 avoidDirection = Vector3.zero;

        Vector3 rightPerpendicular = Vector3.Cross(transform.forward, Vector3.up).normalized;
        Vector3 leftPerpendicular = -rightPerpendicular;

        if (!Physics.Raycast(transform.position, rightPerpendicular, sideSensorRange, obstacleMask))
        {
            avoidDirection = rightPerpendicular;
        }
        else if (!Physics.Raycast(transform.position, leftPerpendicular, sideSensorRange, obstacleMask))
        {
            avoidDirection = leftPerpendicular;
        }
        else
        {
            currentState = PedestrianState.Waiting;
            return;
        }

        avoidanceVector = avoidDirection * sideSensorRange;
    }

    void FollowWaypoints()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        int targetIndex = isMovingForward ?
            Mathf.Min(currentWaypointIndex + 1, waypoints.Count - 1) :
            Mathf.Max(currentWaypointIndex - 1, 0);

        Vector3 target = waypoints[targetIndex];
        Vector3 predictedTarget = target + (target - waypoints[targetIndex]).normalized * predictionDistance;

        MoveTowardsTarget(predictedTarget, 1.0f);

        if (Vector3.Distance(transform.position, target) < waypointThreshold)
        {
            if (isMovingForward)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Count)
                {
                    currentWaypointIndex = waypoints.Count - 1;
                    isMovingForward = false;
                }
            }
            else
            {
                currentWaypointIndex--;
                if (currentWaypointIndex < 0)
                {
                    currentWaypointIndex = 0;
                    isMovingForward = true;
                }
            }
        }
    }

    void AvoidObstacle()
    {
        MoveTowardsTarget(transform.position + avoidanceVector, 0.7f);
    }

    void CrossRoad()
    {
        FollowWaypoints();
        currentSpeed *= crossingSpeedMultiplier;
    }

    void MoveTowardsTarget(Vector3 target, float speedFactor = 1.0f)
    {
        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0;

        Quaternion rotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);

        float targetSpeed = maxSpeed * speedFactor;
        float distanceToTarget = Vector3.Distance(transform.position, target);

        if (distanceToTarget < stoppingDistance)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed * (distanceToTarget / stoppingDistance), deceleration * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }

        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
    }

    void StopMoving()
    {
        currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * Time.deltaTime);
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
    }

    void FindClosestWaypoint()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        float closestDistance = Mathf.Infinity;
        int closestIndex = 0;

        for (int i = 0; i < waypoints.Count; i++)
        {
            float distance = Vector3.Distance(transform.position, waypoints[i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        currentWaypointIndex = closestIndex;

        Vector3 toNextWaypoint = waypoints[GetNextWaypointIndex()] - transform.position;
        isMovingForward = Vector3.Dot(transform.forward, toNextWaypoint.normalized) > 0;
    }

    int GetNextWaypointIndex()
    {
        if (isMovingForward)
        {
            return Mathf.Min(currentWaypointIndex + 1, waypoints.Count - 1);
        }
        else
        {
            return Mathf.Max(currentWaypointIndex - 1, 0);
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebug) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < sensorCount; i++)
        {
            float angle = -sensorAngle / 2 + (sensorAngle / (sensorCount - 1)) * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            Gizmos.DrawRay(transform.position, direction * sideSensorRange);
        }

        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                Gizmos.color = i == currentWaypointIndex ? Color.red : Color.green;
                Gizmos.DrawSphere(waypoints[i], 0.3f);

                if (i < waypoints.Count - 1)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
                }
            }
        }

        // Радиус разговора
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, conversationRange);

        // Текущий партнер по разговору
        if (conversationPartner != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, conversationPartner.transform.position);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, crossingCheckDistance);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2,
            $"State: {currentState}\nHealth: {currentHealth}/{maxHealth}");
#endif
    }

    void OnDrawGizmosSelected()
    {

    }

    struct ObstacleInfo
    {
        public bool detected;
        public float distance;
        public Vector3 direction;
        public Vector3 hitPoint;
    }

    struct VehicleInfo
    {
        public bool detected;
        public float distance;
        public GameObject vehicle;
    }
}