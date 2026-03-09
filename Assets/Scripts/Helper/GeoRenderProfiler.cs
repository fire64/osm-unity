using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Анализатор производительности GeoRender проекта
/// 
/// Специализированный инструмент для выявления узких мест:
/// - Генераторы (BuildingMaker, RoadMaker и т.д.)
/// - Спавнеры (TrafficSpawner)
/// - AI контроллеры
/// - Загрузка данных
/// </summary>
public class GeoRenderProfiler : MonoBehaviour
{
    public static GeoRenderProfiler Instance { get; private set; }

    [Header("Settings")]
    public bool autoProfile = true;
    public float profileInterval = 2f;
    public bool logWarnings = true;

    [Header("Thresholds")]
    public float warningThreshold = 5f; // ms
    public float criticalThreshold = 15f; // ms

    // Результаты профилирования
    private Dictionary<string, ProfilerResult> _results = new Dictionary<string, ProfilerResult>();
    private List<WarningInfo> _warnings = new List<WarningInfo>();

    // Счётчики объектов
    private int _activeVehicles;
    private int _activePedestrians;
    private int _pendingObjects;

    private class ProfilerResult
    {
        public string Category;
        public float LastTime;
        public float AvgTime;
        public float MaxTime;
        public int SampleCount;
        public float TotalTime;
    }

    private class WarningInfo
    {
        public string Source;
        public string Message;
        public float Time;
        public WarningLevel Level;
    }

    private enum WarningLevel
    {
        Warning,
        Critical
    }

    private void Awake()
    {
        Instance = this;
    }

    private IEnumerator Start()
    {
        if (autoProfile)
        {
            yield return new WaitForSeconds(2f); // Ждём инициализации
            StartCoroutine(AutoProfileCoroutine());
            StartCoroutine(ProfileSpawnedObjects());
        }
    }

    private IEnumerator AutoProfileCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(profileInterval);
            ProfileSystems();
            CheckForIssues();
        }
    }

    /// <summary>
    /// Профилирование всех систем
    /// </summary>
    private void ProfileSystems()
    {
        // MapReader / AsyncMapReader
        ProfileMapReader();

        // Генераторы
        ProfileGenerators();

        // TileSystem / AsyncTileLoader
        ProfileTileSystem();

        // TrafficSpawner
        ProfileTrafficSpawner();

        // AI Controllers
        ProfileAIControllers();

        // Выводим предупреждения
        if (logWarnings && _warnings.Count > 0)
        {
            foreach (var warning in _warnings)
            {
                if (warning.Level == WarningLevel.Critical)
                    Debug.LogError($"[GeoRenderProfiler] CRITICAL: {warning.Source} - {warning.Message} ({warning.Time:F2}ms)");
                else
                    Debug.LogWarning($"[GeoRenderProfiler] {warning.Source} - {warning.Message} ({warning.Time:F2}ms)");
            }
        }
    }

    #region System Profilers

    private void ProfileMapReader()
    {
        var mapReader = FindObjectOfType<MapReader>();
        var asyncMapReader = FindObjectOfType<MapReader>();

        if (mapReader != null)
        {
            RecordResult("MapReader.Nodes", "Data", mapReader.nodes?.Count ?? 0);
            RecordResult("MapReader.Ways", "Data", mapReader.ways?.Count ?? 0);
            RecordResult("MapReader.Relations", "Data", mapReader.relations?.Count ?? 0);

            // Предупреждение о синхронной загрузке
            if (!mapReader.IsReady)
            {
                AddWarning("MapReader", "Still loading synchronously - consider AsyncMapReader", 0);
            }
        }

        if (asyncMapReader != null)
        {
     //       RecordResult("AsyncMapReader.PendingNodes", "Data", asyncMapReader.GetPendingNodesCount());
     //       RecordResult("AsyncMapReader.PendingWays", "Data", asyncMapReader.GetPendingWaysCount());
    //        RecordResult("AsyncMapReader.QueuedTiles", "Data", asyncMapReader.GetQueuedTilesCount());
        }
    }

    private void ProfileGenerators()
    {
        // BuildingMaker
        var buildingMaker = FindObjectOfType<BuildingMaker>();
        if (buildingMaker != null)
        {
            var buildings = FindObjectsOfType<Building>();
            RecordResult("BuildingMaker.Count", "Generators", buildings.Length);

            // Проверяем есть ли отставание
            var pending = buildingMaker.GetCountProcessing();
            if (pending > 100)
            {
                AddWarning("BuildingMaker", $"Large pending queue: {pending}", pending * 0.1f);
            }
        }

        // RoadMaker
        var roadMaker = FindObjectOfType<RoadMaker>();
        if (roadMaker != null)
        {
            var roads = FindObjectsOfType<Road>();
            RecordResult("RoadMaker.Count", "Generators", roads.Length);

            var pending = roadMaker.GetCountProcessing();
            if (pending > 50)
            {
                AddWarning("RoadMaker", $"Large pending queue: {pending}", pending * 0.1f);
            }
        }

        // Другие генераторы
        var detailMaker = FindObjectOfType<DetailMaker>();
        if (detailMaker != null)
        {
            var details = FindObjectsOfType<Detail>();
            RecordResult("DetailMaker.Count", "Generators", details.Length);
        }

        var landuseMaker = FindObjectOfType<LanduseMaker>();
        if (landuseMaker != null)
        {
            var landuses = FindObjectsOfType<Landuse>();
            RecordResult("LanduseMaker.Count", "Generators", landuses.Length);
        }
    }

    private void ProfileTileSystem()
    {
        var tileSystem = FindObjectOfType<TileSystem>();
   //     var asyncTileLoader = FindObjectOfType<AsyncTileLoader>();

        if (tileSystem != null)
        {
            // TileSystem - синхронный, может быть причиной фризов
            AddWarning("TileSystem", "Consider using AsyncTileLoader for better performance", 0);
        }

  //      if (asyncTileLoader != null)
 //       {
  //          RecordResult("AsyncTileLoader.Active", "Tiles", asyncTileLoader.GetActiveTilesCount());
  //          RecordResult("AsyncTileLoader.Loading", "Tiles", asyncTileLoader.GetLoadingTilesCount());
  //          RecordResult("AsyncTileLoader.Queued", "Tiles", asyncTileLoader.GetQueuedTilesCount());
  //      }
    }

    private void ProfileTrafficSpawner()
    {
        var trafficSpawner = FindObjectOfType<TrafficSpawner>();
        if (trafficSpawner != null)
        {
            _activeVehicles = trafficSpawner.activeVehicles?.Count ?? 0;
            _activePedestrians = trafficSpawner.activePedestrians?.Count ?? 0;

            RecordResult("Traffic.Vehicles", "Spawn", _activeVehicles);
            RecordResult("Traffic.Pedestrians", "Spawn", _activePedestrians);

            // Предупреждения
            if (_activeVehicles > trafficSpawner.maxVehicles * 1.5f)
            {
                AddWarning("TrafficSpawner", $"Vehicle overflow: {_activeVehicles} > {trafficSpawner.maxVehicles}", _activeVehicles * 0.5f);
            }

            if (_activePedestrians > trafficSpawner.maxPedestrians * 1.5f)
            {
                AddWarning("TrafficSpawner", $"Pedestrian overflow: {_activePedestrians} > {trafficSpawner.maxPedestrians}", _activePedestrians * 0.5f);
            }
        }
    }

    private void ProfileAIControllers()
    {
        // AI Cars
        var carControllers = FindObjectsOfType<AICarController>();
        int activeCars = 0;
        foreach (var car in carControllers)
        {
            if (car.isActiveAndEnabled) activeCars++;
        }
        RecordResult("AI.Cars", "AI", activeCars);

        // AI Pedestrians
        var pedestrianControllers = FindObjectsOfType<AdvancedAIPedestrianController>();
        int activePedestrians = 0;
        foreach (var ped in pedestrianControllers)
        {
            if (ped.isActiveAndEnabled) activePedestrians++;
        }
        RecordResult("AI.Pedestrians", "AI", activePedestrians);

        // UMA Generators
        var umaGenerators = FindObjectsOfType<UMA2PedestrianGenerator>();
        RecordResult("UMA.Generators", "AI", umaGenerators.Length);

        // Предупреждение о большом количестве AI
        int totalAI = activeCars + activePedestrians;
        if (totalAI > 50)
        {
            AddWarning("AI", $"High AI count: {totalAI} active agents", totalAI * 0.2f);
        }
    }

    private IEnumerator ProfileSpawnedObjects()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);

            // Подсчёт объектов с рендерерами
            var renderers = FindObjectsOfType<Renderer>();
            int visibleRenderers = 0;
            int culledRenderers = 0;

            foreach (var r in renderers)
            {
                if (r.isVisible) visibleRenderers++;
                else culledRenderers++;
            }

            RecordResult("Renderers.Visible", "Objects", visibleRenderers);
            RecordResult("Renderers.Culled", "Objects", culledRenderers);

            // Проверка коллайдеров
            var colliders = FindObjectsOfType<Collider>();
            int activeColliders = 0;
            foreach (var c in colliders)
            {
                if (c.enabled && c.gameObject.activeInHierarchy) activeColliders++;
            }
            RecordResult("Colliders.Active", "Objects", activeColliders);

            // Предупреждение о слишком большом количестве
            if (renderers.Length > 5000)
            {
                AddWarning("Objects", $"Too many renderers: {renderers.Length}", renderers.Length * 0.01f);
            }

            if (activeColliders > 2000)
            {
                AddWarning("Objects", $"Too many colliders: {activeColliders}", activeColliders * 0.05f);
            }
        }
    }

    #endregion

    #region Helper Methods

    private void RecordResult(string key, string category, int value)
    {
        if (!_results.ContainsKey(key))
        {
            _results[key] = new ProfilerResult { Category = category };
        }
        // Для целых чисел храним как время (умножаем на 0.01 для визуализации)
        _results[key].LastTime = value;
    }

    private void RecordResult(string key, string category, float timeMs)
    {
        if (!_results.ContainsKey(key))
        {
            _results[key] = new ProfilerResult { Category = category };
        }

        var result = _results[key];
        result.LastTime = timeMs;
        result.TotalTime += timeMs;
        result.SampleCount++;
        result.AvgTime = result.TotalTime / result.SampleCount;
        if (timeMs > result.MaxTime) result.MaxTime = timeMs;
    }

    private void AddWarning(string source, string message, float time)
    {
        var level = time > criticalThreshold ? WarningLevel.Critical : WarningLevel.Warning;
        _warnings.Add(new WarningInfo
        {
            Source = source,
            Message = message,
            Time = time,
            Level = level
        });
    }

    private void CheckForIssues()
    {
        _warnings.Clear();

        // Проверка FPS
        float fps = PerformanceDiagnostics.GetCurrentFPS();
        if (fps < 20)
        {
            AddWarning("FPS", $"Critical FPS: {fps:F1}", 30 - fps);
        }
        else if (fps < 30)
        {
            AddWarning("FPS", $"Low FPS: {fps:F1}", 30 - fps);
        }

        // Проверка памяти
        long usedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        if (usedMemory > 2000 * 1024 * 1024) // > 2GB
        {
         //   AddWarning("Memory", $"High memory usage: {usedMemory / 1024 / 1024}MB", usedMemory / 1024f / 1024f);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Замерить время выполнения действия
    /// </summary>
    public static float Measure(Action action, string name = "")
    {
        if (Instance == null || action == null)
        {
            action?.Invoke();
            return 0;
        }

        var sw = Stopwatch.StartNew();
        action.Invoke();
        sw.Stop();

        float timeMs = (float)sw.Elapsed.TotalMilliseconds;
        Instance.RecordResult(name, "Measured", timeMs);

        if (timeMs > Instance.warningThreshold)
        {
            Instance.AddWarning(name, $"Slow execution: {timeMs:F2}ms", timeMs);
        }

        return timeMs;
    }

    /// <summary>
    /// Получить отчёт
    /// </summary>
    public static string GetReport()
    {
        if (Instance == null) return "GeoRenderProfiler not initialized";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== GeoRender Performance Report ===");
        sb.AppendLine();

        // Группируем по категориям
        var categories = new Dictionary<string, List<KeyValuePair<string, ProfilerResult>>>();
        foreach (var kvp in Instance._results)
        {
            var category = kvp.Value.Category;
            if (!categories.ContainsKey(category))
                categories[category] = new List<KeyValuePair<string, ProfilerResult>>();
            categories[category].Add(kvp);
        }

        foreach (var cat in categories)
        {
            sb.AppendLine($"[{cat.Key}]");
            foreach (var item in cat.Value)
            {
                var result = item.Value;
                if (result.SampleCount > 0)
                {
                    sb.AppendLine($"  {item.Key}: {result.LastTime:F2}ms (avg: {result.AvgTime:F2}, max: {result.MaxTime:F2})");
                }
                else
                {
                    sb.AppendLine($"  {item.Key}: {result.LastTime}");
                }
            }
            sb.AppendLine();
        }

        if (Instance._warnings.Count > 0)
        {
            sb.AppendLine("=== Warnings ===");
            foreach (var warning in Instance._warnings)
            {
                sb.AppendLine($"[{warning.Level}] {warning.Source}: {warning.Message}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Вывести отчёт в консоль
    /// </summary>
    public static void LogReport()
    {
        Debug.Log(GetReport());
    }

    #endregion
}
