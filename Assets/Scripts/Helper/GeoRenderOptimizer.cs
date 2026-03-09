using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Автоматический оптимизатор производительности GeoRender
/// 
/// Решает проблемы:
/// - Слишком много рендереров
/// - Слишком много коллайдеров  
/// - Высокое потребление памяти
/// - Низкий FPS
/// </summary>
public class GeoRenderOptimizer : MonoBehaviour
{
    public static GeoRenderOptimizer Instance { get; private set; }

    [Header("Auto Optimize Settings")]
    public bool autoOptimize = true;
    public float checkInterval = 5f;
    public Transform player;

    [Header("Limits")]
    [Tooltip("Максимальное количество активных рендереров")]
    public int maxActiveRenderers = 3000;

    [Tooltip("Максимальное количество активных коллайдеров")]
    public int maxActiveColliders = 500;

    [Tooltip("Максимальное количество AI агентов")]
    public int maxAIAgents = 20;

    [Tooltip("Максимальное количество зданий")]
    public int maxBuildings = 800;

    [Tooltip("Максимальное количество дорог")]
    public int maxRoads = 300;

    [Header("Culling Settings")]
    [Tooltip("Расстояние от игрока для отключения коллайдеров")]
    public float colliderCullDistance = 100f;

    [Tooltip("Расстояние от игрока для отключения рендереров")]
    public float rendererCullDistance = 500f;

    [Tooltip("Расстояние для упрощения геометрии")]
    public float lodDistance = 200f;

    [Header("Memory Settings")]
    [Tooltip("Порог памяти для агрессивной очистки (MB)")]
    public int memoryThresholdMB = 2000;

    [Tooltip("Интервал очистки памяти (сек)")]
    public float memoryCleanupInterval = 30f;

    [Header("Debug")]
    public bool showDebug = false;
    public KeyCode optimizeKey = KeyCode.F4;

    // Кэши для оптимизации
    private List<Renderer> _allRenderers = new List<Renderer>();
    private List<Collider> _allColliders = new List<Collider>();
    private List<Building> _allBuildings = new List<Building>();
    private List<Road> _allRoads = new List<Road>();
    private Dictionary<Collider, float> _colliderDistances = new Dictionary<Collider, float>();
    private Dictionary<Renderer, float> _rendererDistances = new Dictionary<Renderer, float>();

    // Статистика
    private int _renderersDisabled;
    private int _collidersDisabled;
    private int _objectsDestroyed;
    private long _memoryBefore;
    private long _memoryAfter;

    // События
    public event Action<string> OnOptimizationComplete;

    private void Awake()
    {
        Instance = this;
    }

    private IEnumerator Start()
    {
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // Ждём загрузки сцены
        yield return new WaitForSeconds(5f);

        if (autoOptimize)
        {
            StartCoroutine(AutoOptimizeCoroutine());
            StartCoroutine(MemoryCleanupCoroutine());
            StartCoroutine(ColliderCullingCoroutine());
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(optimizeKey))
        {
            StartCoroutine(FullOptimize());
        }
    }

    #region Auto Optimization

    private IEnumerator AutoOptimizeCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            // Проверяем состояние
            int rendererCount = CountActiveRenderers();
            int colliderCount = CountActiveColliders();
            int aiCount = CountAIAgents();
            long memoryMB = GetUsedMemoryMB();

            bool needsOptimization =
                rendererCount > maxActiveRenderers ||
                colliderCount > maxActiveColliders ||
                aiCount > maxAIAgents ||
                memoryMB > memoryThresholdMB;

            if (needsOptimization)
            {
                yield return StartCoroutine(FullOptimize());
            }
        }
    }

    private IEnumerator MemoryCleanupCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(memoryCleanupInterval);

            long memoryBefore = GetUsedMemoryMB();

            if (memoryBefore > memoryThresholdMB)
            {
                // Принудительная сборка мусора
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Очистка кэшей
                Resources.UnloadUnusedAssets();

                long memoryAfter = GetUsedMemoryMB();

                if (showDebug)
                {
                    Debug.Log($"[Optimizer] Memory cleanup: {memoryBefore}MB -> {memoryAfter}MB (saved: {memoryBefore - memoryAfter}MB)");
                }
            }
        }
    }

    private IEnumerator ColliderCullingCoroutine()
    {
        while (true)
        {
            if (player == null)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            Vector3 playerPos = player.position;
            float sqrColliderDist = colliderCullDistance * colliderCullDistance;

            // Обновляем список коллайдеров каждые 2 секунды
            _allColliders.Clear();
            _allColliders.AddRange(FindObjectsOfType<Collider>());

            foreach (var collider in _allColliders)
            {
                if (collider == null) continue;

                // Пропускаем триггеры и важные коллайдеры
                if (collider.isTrigger) continue;
                if (collider.gameObject.CompareTag("Player")) continue;
                if (collider.gameObject.layer == LayerMask.NameToLayer("Roads")) continue;

                float sqrDist = (collider.transform.position - playerPos).sqrMagnitude;
                bool shouldEnable = sqrDist < sqrColliderDist;

                if (collider.enabled != shouldEnable)
                {
                    collider.enabled = shouldEnable;
                    if (!shouldEnable) _collidersDisabled++;
                }
            }

            yield return new WaitForSeconds(2f);
        }
    }

    #endregion

    #region Full Optimization

    public IEnumerator FullOptimize()
    {
        _memoryBefore = GetUsedMemoryMB();
        _renderersDisabled = 0;
        _collidersDisabled = 0;
        _objectsDestroyed = 0;

        if (showDebug)
            Debug.Log("[Optimizer] Starting full optimization...");

        // 1. Удаляем лишние объекты
        yield return StartCoroutine(RemoveExcessObjects());

        // 2. Оптимизируем рендереры
        yield return StartCoroutine(OptimizeRenderers());

        // 3. Оптимизируем коллайдеры
        yield return StartCoroutine(OptimizeColliders());

        // 4. Оптимизируем AI
        yield return StartCoroutine(OptimizeAI());

        // 5. Очистка памяти
        yield return StartCoroutine(CleanupMemory());

        _memoryAfter = GetUsedMemoryMB();

        string report = $"Optimization complete:\n" +
                       $"  Renderers disabled: {_renderersDisabled}\n" +
                       $"  Colliders disabled: {_collidersDisabled}\n" +
                       $"  Objects destroyed: {_objectsDestroyed}\n" +
                       $"  Memory: {_memoryBefore}MB -> {_memoryAfter}MB";

        if (showDebug)
            Debug.Log($"[Optimizer] {report}");

        OnOptimizationComplete?.Invoke(report);
    }

    private IEnumerator RemoveExcessObjects()
    {
        // Удаляем лишние здания
        _allBuildings.Clear();
        _allBuildings.AddRange(FindObjectsOfType<Building>());

        if (_allBuildings.Count > maxBuildings)
        {
            // Сортируем по расстоянию до игрока
            Vector3 playerPos = player != null ? player.position : Vector3.zero;
            _allBuildings.Sort((a, b) =>
            {
                float distA = (a.transform.position - playerPos).sqrMagnitude;
                float distB = (b.transform.position - playerPos).sqrMagnitude;
                return distB.CompareTo(distA); // Дальние первыми на удаление
            });

            int toRemove = _allBuildings.Count - maxBuildings;
            for (int i = 0; i < toRemove; i++)
            {
                if (_allBuildings[i] != null)
                {
                    Destroy(_allBuildings[i].gameObject);
                    _objectsDestroyed++;
                }

                if (i % 10 == 0) yield return null;
            }
        }

        // Удаляем лишние дороги (аккуратно, чтобы не разорвать сеть)
        _allRoads.Clear();
        _allRoads.AddRange(FindObjectsOfType<Road>());

        if (_allRoads.Count > maxRoads)
        {
            Vector3 playerPos = player != null ? player.position : Vector3.zero;
            _allRoads.Sort((a, b) =>
            {
                float distA = (a.transform.position - playerPos).sqrMagnitude;
                float distB = (b.transform.position - playerPos).sqrMagnitude;
                return distB.CompareTo(distA);
            });

            int toRemove = Mathf.Min(_allRoads.Count - maxRoads, _allRoads.Count / 4); // Не более 25%
            for (int i = 0; i < toRemove; i++)
            {
                if (_allRoads[i] != null && !_allRoads[i].isArea)
                {
                    Destroy(_allRoads[i].gameObject);
                    _objectsDestroyed++;
                }

                if (i % 5 == 0) yield return null;
            }
        }

        yield return null;
    }

    private IEnumerator OptimizeRenderers()
    {
        if (player == null) yield break;

        Vector3 playerPos = player.position;
        float sqrRendererDist = rendererCullDistance * rendererCullDistance;

        _allRenderers.Clear();
        _allRenderers.AddRange(FindObjectsOfType<Renderer>());

        int processed = 0;
        foreach (var renderer in _allRenderers)
        {
            if (renderer == null) continue;

            float sqrDist = (renderer.transform.position - playerPos).sqrMagnitude;

            // Отключаем дальние рендереры
            if (sqrDist > sqrRendererDist && renderer.enabled)
            {
                renderer.enabled = false;
                _renderersDisabled++;
            }
            // Для средних расстояний - упрощаем
            else if (sqrDist > lodDistance * lodDistance)
            {
                // Можно добавить LOD логику здесь
            }

            processed++;
            if (processed % 100 == 0) yield return null;
        }

        yield return null;
    }

    private IEnumerator OptimizeColliders()
    {
        if (player == null) yield break;

        Vector3 playerPos = player.position;
        float sqrColliderDist = colliderCullDistance * colliderCullDistance;

        _allColliders.Clear();
        _allColliders.AddRange(FindObjectsOfType<Collider>());

        int activeColliders = 0;
        int processed = 0;

        foreach (var collider in _allColliders)
        {
            if (collider == null) continue;

            if (collider.isTrigger || collider.gameObject.CompareTag("Player"))
            {
                activeColliders++;
                continue;
            }

            float sqrDist = (collider.transform.position - playerPos).sqrMagnitude;
            bool shouldEnable = sqrDist < sqrColliderDist;

            // Если превышен лимит - отключаем даже близкие коллайдеры
            if (activeColliders >= maxActiveColliders)
            {
                shouldEnable = false;
            }

            if (collider.enabled != shouldEnable)
            {
                collider.enabled = shouldEnable;
                if (!shouldEnable) _collidersDisabled++;
                else activeColliders++;
            }

            processed++;
            if (processed % 50 == 0) yield return null;
        }

        yield return null;
    }

    private IEnumerator OptimizeAI()
    {
        // Оптимизируем TrafficSpawner
        var trafficSpawner = FindObjectOfType<TrafficSpawner>();
        if (trafficSpawner != null)
        {
            // Возвращаем лишние автомобили в пул
            while (trafficSpawner.activeVehicles.Count > maxAIAgents / 2)
            {
                var vehicle = trafficSpawner.activeVehicles[trafficSpawner.activeVehicles.Count - 1];
                trafficSpawner.ReturnVehicleToPool(vehicle);
            }

            // Возвращаем лишних пешеходов
            while (trafficSpawner.activePedestrians.Count > maxAIAgents / 2)
            {
                var pedestrian = trafficSpawner.activePedestrians[trafficSpawner.activePedestrians.Count - 1];
                trafficSpawner.ReturnPedestrianToPool(pedestrian);
            }

            // Обновляем лимиты
            trafficSpawner.maxVehicles = Mathf.Min(trafficSpawner.maxVehicles, maxAIAgents / 2);
            trafficSpawner.maxPedestrians = Mathf.Min(trafficSpawner.maxPedestrians, maxAIAgents / 2);
        }

        yield return null;
    }

    private IEnumerator CleanupMemory()
    {
        // Очистка ресурсов
        Resources.UnloadUnusedAssets();

        // GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        yield return null;
    }

    #endregion

    #region Utility Methods

    private int CountActiveRenderers()
    {
        var renderers = FindObjectsOfType<Renderer>();
        int count = 0;
        foreach (var r in renderers)
        {
            if (r.enabled) count++;
        }
        return count;
    }

    private int CountActiveColliders()
    {
        var colliders = FindObjectsOfType<Collider>();
        int count = 0;
        foreach (var c in colliders)
        {
            if (c.enabled) count++;
        }
        return count;
    }

    private int CountAIAgents()
    {
        var cars = FindObjectsOfType<AICarController>();
        var peds = FindObjectsOfType<AdvancedAIPedestrianController>();
        return cars.Length + peds.Length;
    }

    private long GetUsedMemoryMB()
    {
        return UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Получить текущее состояние
    /// </summary>
    public static string GetStatus()
    {
        if (Instance == null) return "Optimizer not initialized";

        return $"Renderers: {Instance.CountActiveRenderers()}/{Instance.maxActiveRenderers}\n" +
               $"Colliders: {Instance.CountActiveColliders()}/{Instance.maxActiveColliders}\n" +
               $"AI Agents: {Instance.CountAIAgents()}/{Instance.maxAIAgents}\n" +
               $"Memory: {Instance.GetUsedMemoryMB()}MB";
    }

    /// <summary>
    /// Запустить оптимизацию
    /// </summary>
    public static void Optimize()
    {
        if (Instance != null)
        {
            Instance.StartCoroutine(Instance.FullOptimize());
        }
    }

    /// <summary>
    /// Установить агрессивные настройки для слабых ПК
    /// </summary>
    public static void SetLowPerformanceMode()
    {
        if (Instance == null) return;

        Instance.maxActiveRenderers = 1500;
        Instance.maxActiveColliders = 200;
        Instance.maxAIAgents = 10;
        Instance.maxBuildings = 400;
        Instance.maxRoads = 150;
        Instance.colliderCullDistance = 50f;
        Instance.rendererCullDistance = 300f;

        Debug.Log("[Optimizer] Low performance mode enabled");
    }

    /// <summary>
    /// Установить сбалансированные настройки
    /// </summary>
    public static void SetBalancedMode()
    {
        if (Instance == null) return;

        Instance.maxActiveRenderers = 3000;
        Instance.maxActiveColliders = 500;
        Instance.maxAIAgents = 20;
        Instance.maxBuildings = 800;
        Instance.maxRoads = 300;
        Instance.colliderCullDistance = 100f;
        Instance.rendererCullDistance = 500f;

        Debug.Log("[Optimizer] Balanced mode enabled");
    }

    #endregion
}