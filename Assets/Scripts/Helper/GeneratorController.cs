using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Контроллер генераторов - предотвращает накопление очередей
/// 
/// Решает проблему:
/// [GeoRenderProfiler] CRITICAL: BuildingMaker - Large pending queue: 737
/// [GeoRenderProfiler] CRITICAL: RoadMaker - Large pending queue: 1527
/// </summary>
public class GeneratorController : MonoBehaviour
{
    public static GeneratorController Instance { get; private set; }

    [Header("Limits")]
    [Tooltip("Максимальная очередь зданий")]
    public int maxBuildingQueue = 100;

    [Tooltip("Максимальная очередь дорог")]
    public int maxRoadQueue = 100;

    [Tooltip("Максимальное количество зданий в сцене")]
    public int maxBuildingsInScene = 500;

    [Tooltip("Максимальное количество дорог в сцене")]
    public int maxRoadsInScene = 200;

    [Header("Processing Speed")]
    [Tooltip("Ускорить обработку batchSize")]
    public int buildingBatchSize = 20;

    public int roadBatchSize = 20;

    [Header("Auto Control")]
    public bool autoControl = true;
    public float checkInterval = 2f;
    public bool pauseGeneratorsWhenQueueFull = true;

    [Header("Distance Filtering")]
    public Transform player;
    public float maxGenerationDistance = 500f;

    [Header("Debug")]
    public bool showDebug = true;

    // Ссылки на генераторы
    private BuildingMaker _buildingMaker;
    private RoadMaker _roadMaker;
    private MapReader _mapReader;

    // Состояние
    private bool _generatorsPaused = false;

    private void Awake()
    {
        Instance = this;
    }

    private IEnumerator Start()
    {
        // Ждём инициализации
        yield return new WaitForSeconds(3f);

        // Находим генераторы
        _buildingMaker = FindObjectOfType<BuildingMaker>();
        _roadMaker = FindObjectOfType<RoadMaker>();
        _mapReader = FindObjectOfType<MapReader>();

        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // Применяем настройки
        ApplyOptimizations();

        if (autoControl)
        {
            StartCoroutine(MonitorAndControl());
        }
    }

    private void ApplyOptimizations()
    {
        // BuildingMaker оптимизации
        if (_buildingMaker != null)
        {
            _buildingMaker.batchSize = buildingBatchSize;

            // Ограничиваем макс. узлы для ускорения
            _buildingMaker.MaxNodes = 60;

            if (showDebug)
                Debug.Log($"[GeneratorController] BuildingMaker batch size: {_buildingMaker.batchSize}");
        }

        // RoadMaker оптимизации
        if (_roadMaker != null)
        {
            _roadMaker.batchSize = roadBatchSize;
            _roadMaker.MaxNodes = 60;

            if (showDebug)
                Debug.Log($"[GeneratorController] RoadMaker batch size: {_roadMaker.batchSize}");
        }
    }

    private IEnumerator MonitorAndControl()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            int buildingQueue = GetBuildingQueue();
            int roadQueue = GetRoadQueue();
            int buildingCount = CountBuildings();
            int roadCount = CountRoads();

            bool needAction = false;
            string actions = "";

            // Проверяем очереди
            if (buildingQueue > maxBuildingQueue)
            {
                actions += $"Building queue too high: {buildingQueue}\n";
                needAction = true;
            }

            if (roadQueue > maxRoadQueue)
            {
                actions += $"Road queue too high: {roadQueue}\n";
                needAction = true;
            }

            // Проверяем количество объектов
            if (buildingCount > maxBuildingsInScene)
            {
                actions += $"Too many buildings: {buildingCount}\n";
                RemoveDistantBuildings();
                needAction = true;
            }

            if (roadCount > maxRoadsInScene)
            {
                actions += $"Too many roads: {roadCount}\n";
                needAction = true;
            }

            if (needAction)
            {
                if (showDebug)
                    Debug.LogWarning($"[GeneratorController] Taking action:\n{actions}");

                // Принимаем меры
                if (pauseGeneratorsWhenQueueFull && !_generatorsPaused)
                {
                    PauseGenerators();
                }

                // Ускоряем обработку
                SpeedUpProcessing();
            }
            else if (_generatorsPaused)
            {
                ResumeGenerators();
            }

            // Выводим статус
            if (showDebug)
            {
                Debug.Log($"[GeneratorController] Status: Buildings={buildingCount} (queue={buildingQueue}), Roads={roadCount} (queue={roadQueue})");
            }
        }
    }

    #region Queue Management

    private int GetBuildingQueue()
    {
        if (_buildingMaker != null)
        {
            return _buildingMaker.GetCountProcessing();
        }
        return 0;
    }

    private int GetRoadQueue()
    {
        if (_roadMaker != null)
        {
            return _roadMaker.GetCountProcessing();
        }
        return 0;
    }

    private int CountBuildings()
    {
        return FindObjectsOfType<Building>().Length;
    }

    private int CountRoads()
    {
        return FindObjectsOfType<Road>().Length;
    }

    private void PauseGenerators()
    {
        _generatorsPaused = true;

        // Останавливаем обработку новых данных
        if (_mapReader != null)
        {
            _mapReader.enabled = false;
        }

        if (showDebug)
            Debug.Log("[GeneratorController] Generators PAUSED");
    }

    private void ResumeGenerators()
    {
        _generatorsPaused = false;

        if (_mapReader != null)
        {
            _mapReader.enabled = true;
        }

        if (showDebug)
            Debug.Log("[GeneratorController] Generators RESUMED");
    }

    private void SpeedUpProcessing()
    {
        // Временно увеличиваем batchSize
        if (_buildingMaker != null)
        {
            _buildingMaker.batchSize = buildingBatchSize * 3;
        }

        if (_roadMaker != null)
        {
            _roadMaker.batchSize = roadBatchSize * 3;
        }
    }

    private void RemoveDistantBuildings()
    {
        if (player == null) return;

        var buildings = FindObjectsOfType<Building>();
        Vector3 playerPos = player.position;
        float sqrDist = maxGenerationDistance * maxGenerationDistance;

        int removed = 0;
        foreach (var building in buildings)
        {
            if (building == null) continue;

            float d = (building.transform.position - playerPos).sqrMagnitude;
            if (d > sqrDist)
            {
                Destroy(building.gameObject);
                removed++;
            }
        }

        if (showDebug && removed > 0)
            Debug.Log($"[GeneratorController] Removed {removed} distant buildings");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Установить агрессивный режим (минимум объектов)
    /// </summary>
    public static void SetAggressiveMode()
    {
        if (Instance == null) return;

        Instance.maxBuildingQueue = 50;
        Instance.maxRoadQueue = 50;
        Instance.maxBuildingsInScene = 300;
        Instance.maxRoadsInScene = 100;
        Instance.buildingBatchSize = 30;
        Instance.roadBatchSize = 30;
        Instance.maxGenerationDistance = 300f;

        Instance.ApplyOptimizations();
        Debug.Log("[GeneratorController] Aggressive mode enabled");
    }

    /// <summary>
    /// Установить сбалансированный режим
    /// </summary>
    public static void SetBalancedMode()
    {
        if (Instance == null) return;

        Instance.maxBuildingQueue = 100;
        Instance.maxRoadQueue = 100;
        Instance.maxBuildingsInScene = 500;
        Instance.maxRoadsInScene = 200;
        Instance.buildingBatchSize = 20;
        Instance.roadBatchSize = 20;
        Instance.maxGenerationDistance = 500f;

        Instance.ApplyOptimizations();
        Debug.Log("[GeneratorController] Balanced mode enabled");
    }

    /// <summary>
    /// Очистить все очереди и сбросить генераторы
    /// </summary>
    public static void ClearQueues()
    {
        if (Instance == null) return;

        // Удаляем все объекты, которые ещё не созданы
        var buildings = FindObjectsOfType<Building>();
        var roads = FindObjectsOfType<Road>();

        // Оставляем только ближайшие к игроку
        Vector3 playerPos = Instance.player != null ? Instance.player.position : Vector3.zero;
        float keepDistance = 200f;

        foreach (var b in buildings)
        {
            if ((b.transform.position - playerPos).magnitude > keepDistance)
            {
                UnityEngine.Object.Destroy(b.gameObject);
            }
        }

        foreach (var r in roads)
        {
            if ((r.transform.position - playerPos).magnitude > keepDistance)
            {
                UnityEngine.Object.Destroy(r.gameObject);
            }
        }

        GC.Collect();
        Debug.Log("[GeneratorController] Queues cleared");
    }

    /// <summary>
    /// Получить статус генераторов
    /// </summary>
    public static string GetStatus()
    {
        if (Instance == null) return "Controller not initialized";

        return $"Building Queue: {Instance.GetBuildingQueue()}/{Instance.maxBuildingQueue}\n" +
               $"Road Queue: {Instance.GetRoadQueue()}/{Instance.maxRoadQueue}\n" +
               $"Buildings: {Instance.CountBuildings()}/{Instance.maxBuildingsInScene}\n" +
               $"Roads: {Instance.CountRoads()}/{Instance.maxRoadsInScene}\n" +
               $"Paused: {Instance._generatorsPaused}";
    }

    #endregion
}
