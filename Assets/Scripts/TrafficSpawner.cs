using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class TrafficSpawner : MonoBehaviour
{
    [Header("Vehicle Spawn Settings")]
    public GameObject[] vehiclePrefabs;
    public int maxVehicles = 20;
    public int vehiclePoolSize = 30;
    public float vehicleSpawnInterval = 3f;
    public float minVehicleSpawnDistance = 50f;
    public float maxVehicleSpawnDistance = 500f;
    public float vehicleDespawnDistance = 1000f;

    [Header("Pedestrian Spawn Settings")]
    public GameObject[] pedestrianPrefabs;
    public int maxPedestrians = 15;
    public int pedestrianPoolSize = 25;
    public float pedestrianSpawnInterval = 5f;
    public float minPedestrianSpawnDistance = 50f;
    public float maxPedestrianSpawnDistance = 500f;
    public float pedestrianDespawnDistance = 1000f;

    [Header("Road Settings")]
    public LayerMask roadMask;
    public float spawnHeightOffset = 0.5f;

    // Пул объектов
    private Queue<GameObject> vehiclePool = new Queue<GameObject>();
    private Queue<GameObject> pedestrianPool = new Queue<GameObject>();

    // Активные объекты
    public List<GameObject> activeVehicles = new List<GameObject>();
    public List<GameObject> activePedestrians = new List<GameObject>();

    // Все созданные объекты для деактивации
    private List<GameObject> allVehicles = new List<GameObject>();
    private List<GameObject> allPedestrians = new List<GameObject>();

    public List<Road> vehicleRoads = new List<Road>();
    public List<Road> pedestrianRoads = new List<Road>();
    private Transform playerTransform;
    public float vehicleSpawnTimer = 0f;
    public float pedestrianSpawnTimer = 0f;

    private bool isSpawnerInit = false;
    private bool poolsInitialized = false;

    private void Start()
    {
        // Создаем пулы объектов сразу при старте
        InitializePools();
    }

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
//      CreateNavMesh();
        InitSpawner();
    }

    private void CreateNavMesh()
    {
        var navmanager = new GameObject("navmanager").AddComponent<NavMeshSurface>();
        navmanager.transform.localPosition = new Vector3(0, 0, 0);

        navmanager.collectObjects = CollectObjects.Volume;

        var map = FindObjectOfType<MapReader>();

        float radiusfixed = map.radiusmeters * 4;

        navmanager.size = new Vector3(radiusfixed, 20, radiusfixed);
        navmanager.center = Vector3.zero;

        navmanager.overrideTileSize = true;
        navmanager.tileSize = (int)radiusfixed;

        navmanager.useGeometry = NavMeshCollectGeometry.RenderMeshes;

        navmanager.BuildNavMesh();
    }

    public void InitSpawner()
    {
        // Деактивируем все объекты из пулов
        DeactivateAllPools();

        // Найти все дороги на сцене
        FindAllSuitableRoads();

        // Найти трансформ игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("Player not found! Objects will spawn regardless of player position.");
        }

        // Начать корутины для деспавна
        StartCoroutine(DespawnVehiclesRoutine());
        StartCoroutine(DespawnPedestriansRoutine());

        isSpawnerInit = true;
    }

    void InitializePools()
    {
        if (poolsInitialized) return;

        if(vehiclePrefabs != null && vehiclePrefabs.Length > 0)
        {
            // Инициализация пула транспортных средств
            for (int i = 0; i < vehiclePoolSize; i++)
            {
                GameObject vehiclePrefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
                GameObject vehicle = Instantiate(vehiclePrefab, Vector3.zero, Quaternion.identity);
                vehicle.SetActive(true); // Активируем при создании
                vehicle.transform.SetParent(transform);
                vehiclePool.Enqueue(vehicle);
                allVehicles.Add(vehicle);
            }
        }

        if(pedestrianPrefabs != null && pedestrianPrefabs.Length > 0)
        {
            // Инициализация пула пешеходов
            for (int i = 0; i < pedestrianPoolSize; i++)
            {
                GameObject pedestrianPrefab = pedestrianPrefabs[Random.Range(0, pedestrianPrefabs.Length)];
                GameObject pedestrian = Instantiate(pedestrianPrefab, Vector3.zero, Quaternion.identity);
                pedestrian.SetActive(true); // Активируем при создании
                pedestrian.transform.SetParent(transform);
                pedestrianPool.Enqueue(pedestrian);
                allPedestrians.Add(pedestrian);
            }
        }

        poolsInitialized = true;
        Debug.Log($"Initialized pools: {vehiclePool.Count} vehicles, {pedestrianPool.Count} pedestrians");
    }

    void DeactivateAllPools()
    {
        // Деактивируем все транспортные средства
        foreach (GameObject vehicle in allVehicles)
        {
            if (vehicle != null)
            {
                vehicle.SetActive(false);
            }
        }

        // Деактивируем всех пешеходов
        foreach (GameObject pedestrian in allPedestrians)
        {
            if (pedestrian != null)
            {
                pedestrian.SetActive(false);
            }
        }

        // Очищаем списки активных объектов
        activeVehicles.Clear();
        activePedestrians.Clear();

        // Перестраиваем очереди пулов
        vehiclePool.Clear();
        pedestrianPool.Clear();

        foreach (GameObject vehicle in allVehicles)
        {
            if (vehicle != null)
            {
                vehiclePool.Enqueue(vehicle);
            }
        }

        foreach (GameObject pedestrian in allPedestrians)
        {
            if (pedestrian != null)
            {
                pedestrianPool.Enqueue(pedestrian);
            }
        }

        Debug.Log($"Deactivated all pools: {vehiclePool.Count} vehicles, {pedestrianPool.Count} pedestrians ready for spawning");
    }

    void Update()
    {
        if (!isSpawnerInit)
            return;

        // Спавн транспортных средств
        if (activeVehicles.Count < maxVehicles)
        {
            vehicleSpawnTimer += Time.deltaTime;
            if (vehicleSpawnTimer >= vehicleSpawnInterval)
            {
                vehicleSpawnTimer = 0f;
                TrySpawnVehicle();
            }
        }

        // Спавн пешеходов
        if (activePedestrians.Count < maxPedestrians)
        {
            pedestrianSpawnTimer += Time.deltaTime;
            if (pedestrianSpawnTimer >= pedestrianSpawnInterval)
            {
                pedestrianSpawnTimer = 0f;
                TrySpawnPedestrian();
            }
        }
    }

    void FindAllSuitableRoads()
    {
        Road[] allRoads = FindObjectsOfType<Road>();

        foreach (Road road in allRoads)
        {
            // Проверяем, можно ли использовать дорогу для автомобилей
            if (road.isCanUseAutomobile())
            {
                vehicleRoads.Add(road);
            }

            // Проверяем, можно ли использовать дорогу для пешеходов
            if (road.isCanUsePedestrian())
            {
                pedestrianRoads.Add(road);
            }
        }

        Debug.Log($"Found {vehicleRoads.Count} roads for vehicles and {pedestrianRoads.Count} roads for pedestrians (out of {allRoads.Length} total roads).");
    }

    void TrySpawnVehicle()
    {
        if (vehiclePool.Count == 0)
        {
            Debug.Log("No vehicles available in pool!");
            return;
        }

        if (vehicleRoads.Count == 0)
        {
            Debug.LogWarning("No roads found for spawning vehicles!");
            return;
        }

        // Выбрать случайную дорогу
        Road randomRoad = vehicleRoads[Random.Range(0, vehicleRoads.Count)];

        if (randomRoad.coordpoints == null || randomRoad.coordpoints.Count < 2)
        {
            Debug.LogWarning("Selected road has no waypoints!");
            return;
        }

        // Выбрать случайную точку на дороге
        int pointIndex = Random.Range(0, randomRoad.coordpoints.Count);
        Vector3 spawnPoint = randomRoad.coordpoints[pointIndex];

        float distanceToPlayerSpawnPint = Vector3.Distance(spawnPoint, playerTransform.position);

        // Проверить расстояние до игрока
        if (playerTransform != null && ( distanceToPlayerSpawnPint < minVehicleSpawnDistance || distanceToPlayerSpawnPint > maxVehicleSpawnDistance) )
        {
            return; // Не спавнить слишком близко или далеко к игроку
        }

        // Выбрать случайную полосу
        int laneCount = randomRoad.lanes;
        int lane = Random.Range(0, laneCount);

        // Вычислить позицию спавна с учетом полосы
        Vector3 spawnPosition = CalculateVehicleSpawnPosition(randomRoad, pointIndex, lane);

        // Создать автомобиль из пула
        SpawnVehicleFromPool(spawnPosition, randomRoad, pointIndex, lane);
    }

    void TrySpawnPedestrian()
    {
        if (pedestrianPool.Count == 0)
        {
            Debug.Log("No pedestrians available in pool!");
            return;
        }

        if (pedestrianRoads.Count == 0)
        {
            Debug.LogWarning("No roads found for spawning pedestrians!");
            return;
        }

        // Выбрать случайную дорогу
        Road randomRoad = pedestrianRoads[Random.Range(0, pedestrianRoads.Count)];
        if (randomRoad.coordpoints == null || randomRoad.coordpoints.Count < 2)
        {
            Debug.LogWarning("Selected road has no waypoints!");
            return;
        }

        // Выбрать случайную точку на дороге
        int pointIndex = Random.Range(0, randomRoad.coordpoints.Count);
        Vector3 spawnPoint = randomRoad.coordpoints[pointIndex];

        float distanceToPlayer = Vector3.Distance(spawnPoint, playerTransform.position);

        // Проверить расстояние до игрока
        if (playerTransform != null && (distanceToPlayer < minPedestrianSpawnDistance || distanceToPlayer > maxPedestrianSpawnDistance) )
        {
            return; // Не спавнить слишком близко к игроку
        }

        // Определить направление движения
        bool moveForward;
        if (pointIndex == 0)
        {
            moveForward = true; // В начале дороги - только вперед
        }
        else if (pointIndex == randomRoad.coordpoints.Count - 1)
        {
            moveForward = false; // В конце дороги - только назад
        }
        else
        {
            // Для точек в середине дороги случайно выбираем направление
            moveForward = Random.Range(0, 2) == 0;
        }

        // Вычислить позицию спавна
        Vector3 spawnPosition = CalculatePedestrianSpawnPosition(randomRoad, pointIndex, moveForward);

        // Создать пешехода из пула
        SpawnPedestrianFromPool(spawnPosition, randomRoad, pointIndex, moveForward);
    }

    Vector3 CalculateVehicleSpawnPosition(Road road, int pointIndex, int lane)
    {
        List<Vector3> positions = road.GetLaneCenterPoints(lane);

        if(positions.Count <= pointIndex)
        {
            Debug.LogError( "Can't get point for road " + road.Id + " for lane: " + lane);
            return Vector3.zero;
        }

        return positions[pointIndex];
    }

    Vector3 CalculatePedestrianSpawnPosition(Road road, int pointIndex, bool moveForward)
    {
        Vector3 point = road.coordpoints[pointIndex];
        Vector3 direction;

        // Определить направление дороги в этой точке
        if (moveForward)
        {
            int nextIndex = Mathf.Min(pointIndex + 1, road.coordpoints.Count - 1);
            direction = (road.coordpoints[nextIndex] - point).normalized;
        }
        else
        {
            int prevIndex = Mathf.Max(pointIndex - 1, 0);
            direction = (road.coordpoints[prevIndex] - point).normalized;
        }

        // Вычислить перпендикуляр к направлению движения (поперек дороги)
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;

        // Для пешеходов используем фиксированное смещение к краю дороги
        float laneWidth = road.GetLaneWidth() > 0 ? road.GetLaneWidth() : 3f;
        float laneOffset = (road.lanes * laneWidth) / 2f + 1f; // Смещение за пределы дороги

        // Случайно выбираем сторону дороги
        if (Random.Range(0, 2) == 0)
        {
            laneOffset = -laneOffset; // Другая сторона дороги
        }

        // Вычислить итоговую позицию
        Vector3 spawnPosition = point + perpendicular * laneOffset;
        spawnPosition.y += spawnHeightOffset; // Небольшое смещение по высоте

        return spawnPosition;
    }

    void SpawnVehicleFromPool(Vector3 position, Road road, int waypointIndex, int laneIndex)
    {
        RoadMarkings roadMarkings = road.GetRoadMarkings();

        if (roadMarkings != null && roadMarkings.IsMarking())
        {
            // Получить автомобиль из пула
            GameObject vehicle = vehiclePool.Dequeue();
            vehicle.SetActive(true);
            activeVehicles.Add(vehicle);

            // Установить позицию и поворот
            vehicle.transform.position = position;

            // Настроить AI контроллер автомобиля
            AICarController vehicleController = vehicle.GetComponent<AICarController>();

            if (vehicleController != null)
            {
                vehicleController.ResetAI(road, waypointIndex, laneIndex);
            }
            else
            {
                Debug.LogWarning("Spawned vehicle doesn't have AICarController component!");
            }
        }
    }

    void SpawnPedestrianFromPool(Vector3 position, Road road, int waypointIndex, bool moveForward)
    {
        // Получить пешехода из пула
        GameObject pedestrian = pedestrianPool.Dequeue();
        pedestrian.SetActive(true);
        activePedestrians.Add(pedestrian);

        // Определить rotation на основе направления движения
        Vector3 direction;
        if (moveForward)
        {
            int nextIndex = Mathf.Min(waypointIndex + 1, road.coordpoints.Count - 1);
            direction = (road.coordpoints[nextIndex] - position).normalized;
        }
        else
        {
            int prevIndex = Mathf.Max(waypointIndex - 1, 0);
            direction = (road.coordpoints[prevIndex] - position).normalized;
        }

        Quaternion rotation = Quaternion.LookRotation(direction);

        // Установить позицию и поворот
        pedestrian.transform.position = position;
        pedestrian.transform.rotation = rotation;

        // Настроить AI контроллер пешехода
        AdvancedAIPedestrianController pedestrianController = pedestrian.GetComponent<AdvancedAIPedestrianController>();

        if (pedestrianController != null)
        {
            pedestrianController.ResetAI(road, waypointIndex, moveForward);
        }
        else
        {
            Debug.LogWarning("Spawned pedestrian doesn't have AdvancedAIPedestrianController component!");
        }
    }

    public void ReturnVehicleToPool(GameObject vehicle)
    {
        if (activeVehicles.Contains(vehicle))
        {
            vehicle.SetActive(false);
            activeVehicles.Remove(vehicle);
            vehiclePool.Enqueue(vehicle);
        }
    }

    public void ReturnPedestrianToPool(GameObject pedestrian)
    {
        if (activePedestrians.Contains(pedestrian))
        {
            pedestrian.SetActive(false);
            activePedestrians.Remove(pedestrian);
            pedestrianPool.Enqueue(pedestrian);
        }
    }

    IEnumerator DespawnVehiclesRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // Проверять каждые 5 секунд

            if (playerTransform == null) continue;

            for (int i = activeVehicles.Count - 1; i >= 0; i--)
            {
                GameObject vehicle = activeVehicles[i];
                if (vehicle == null)
                {
                    activeVehicles.RemoveAt(i);
                    continue;
                }

                // Деспавнить машины, которые слишком далеко от игрока
                if (Vector3.Distance(vehicle.transform.position, playerTransform.position) > vehicleDespawnDistance)
                {
                    ReturnVehicleToPool(vehicle);
                }

                // Проверить, не упал ли автомобиль (высота меньше -5)
                if (vehicle.transform.position.y < -5f)
                {
                    ReturnVehicleToPool(vehicle);
                }
            }
        }
    }

    IEnumerator DespawnPedestriansRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // Проверять каждые 5 секунд

            if (playerTransform == null) continue;

            for (int i = activePedestrians.Count - 1; i >= 0; i--)
            {
                GameObject pedestrian = activePedestrians[i];
                if (pedestrian == null)
                {
                    activePedestrians.RemoveAt(i);
                    continue;
                }

                // Деспавнить пешеходов, которые слишком далеко от игрока
                if (Vector3.Distance(pedestrian.transform.position, playerTransform.position) > pedestrianDespawnDistance)
                {
                    ReturnPedestrianToPool(pedestrian);
                }

                // Проверить, не упал ли пешеход (высота меньше -5)
                if (pedestrian.transform.position.y < -5f)
                {
                    ReturnPedestrianToPool(pedestrian);
                }
            }
        }
    }

    // Метод для обновления списка дорог
    public void RefreshRoadList()
    {
        vehicleRoads.Clear();
        pedestrianRoads.Clear();
        FindAllSuitableRoads();
    }

    void OnDrawGizmos()
    {
        // Визуализация точек спавна для транспортных средств
        Gizmos.color = Color.magenta;
        foreach (Road road in vehicleRoads)
        {
            if (road.coordpoints == null) continue;

            for (int i = 0; i < road.coordpoints.Count; i++)
            {
                for (int lane = 0; lane < road.lanes; lane++)
                {
                    // Определить направление движения
                    bool moveForward = i < road.coordpoints.Count - 1;
                    Vector3 spawnPos = CalculateVehicleSpawnPosition(road, i, lane);
                    Gizmos.DrawWireSphere(spawnPos, 0.3f);
                }
            }
        }

        // Визуализация точек спавна для пешеходов
        Gizmos.color = Color.cyan;
        foreach (Road road in pedestrianRoads)
        {
            if (road.coordpoints == null) continue;

            for (int i = 0; i < road.coordpoints.Count; i++)
            {
                // Определить направление движения
                bool moveForward = i < road.coordpoints.Count - 1;
                Vector3 spawnPos = CalculatePedestrianSpawnPosition(road, i, moveForward);
                Gizmos.DrawWireSphere(spawnPos, 0.3f);
            }
        }
    }
}