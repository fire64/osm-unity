using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ОПТИМИЗИРОВАННЫЙ ThreadedDataProcessor v2
/// 
/// Исправлены проблемы с запуском фоновых задач
/// </summary>
public class ThreadedDataProcessor : MonoBehaviour
{
    public static ThreadedDataProcessor Instance { get; private set; }

    [Header("Performance Settings")]
    [Tooltip("Максимальное время на обработку задач главного потока за кадр (мс)")]
    public float mainThreadBudgetMs = 3.0f;

    [Tooltip("Включить детальное логирование")]
    public bool enableProfiling = false;

    // Очередь для главного потока
    private ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    // Счетчики
    private int _totalMainThreadTasksProcessed;
    private int _totalBackgroundTasksProcessed;

    private bool _isShuttingDown = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (enableProfiling)
            Debug.Log("[ThreadedDataProcessor] Initialized");
    }

    private void OnDestroy()
    {
        _isShuttingDown = true;
    }

    // ========== API ==========

    /// <summary>
    /// Добавить задачу в главный поток
    /// </summary>
    public void EnqueueMainThreadTask(Action task)
    {
        if (_isShuttingDown || task == null) return;
        _mainThreadQueue.Enqueue(task);
    }

    /// <summary>
    /// Запустить задачу в фоновом потоке
    /// </summary>
    public Task EnqueueBackgroundTask(Action task, TaskPriority priority = TaskPriority.Normal, string description = "")
    {
        if (_isShuttingDown || task == null) return Task.CompletedTask;

        _totalBackgroundTasksProcessed++;

        return Task.Run(() =>
        {
            try
            {
                task();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThreadedDataProcessor] Background task error '{description}': {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Запустить задачу с результатом в фоновом потоке
    /// </summary>
    public Task<T> EnqueueBackgroundTask<T>(Func<T> task, TaskPriority priority = TaskPriority.Normal, string description = "")
    {
        if (_isShuttingDown || task == null) return Task.FromResult(default(T));

        _totalBackgroundTasksProcessed++;

        return Task.Run(() =>
        {
            try
            {
                return task();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThreadedDataProcessor] Background task error '{description}': {ex.Message}");
                return default(T);
            }
        });
    }

    /// <summary>
    /// Устаревший метод для совместимости
    /// </summary>
    [Obsolete("Use EnqueueMainThreadTask instead")]
    public void EnqueueBuildTask(Action task)
    {
        EnqueueMainThreadTask(task);
    }

    // ========== UPDATE ==========

    private void Update()
    {
        ProcessMainThreadQueue();
    }

    private void ProcessMainThreadQueue()
    {
        if (_mainThreadQueue.IsEmpty) return;

        float startTime = Time.realtimeSinceStartup;
        int processedCount = 0;

        while (_mainThreadQueue.TryDequeue(out var task))
        {
            if (_isShuttingDown) break;

            try
            {
                task?.Invoke();
                processedCount++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThreadedDataProcessor] Main thread task error: {ex.Message}");
            }

            // Проверяем бюджет времени
            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            if (elapsedMs > mainThreadBudgetMs)
            {
                break;
            }
        }

        _totalMainThreadTasksProcessed += processedCount;
    }

    // ========== СТАТИСТИКА ==========

    public void LogStatistics()
    {
        Debug.Log($"[ThreadedDataProcessor] Statistics:\n" +
                  $"  Main Thread Tasks: {_totalMainThreadTasksProcessed} processed, {_mainThreadQueue.Count} pending\n" +
                  $"  Background Tasks: {_totalBackgroundTasksProcessed} processed\n");
    }

    public int GetPendingMainThreadTasks() => _mainThreadQueue.Count;

    public enum TaskPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}

// ========== ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ==========

/// <summary>
/// Данные для создания меша (thread-safe)
/// </summary>
public class MeshGenerationData
{
    public Vector3[] Vertices;
    public int[] Triangles;
    public Vector3[] Normals;
    public Vector2[] UV;
    public Vector4[] Tangents;
    public Color[] Colors;

    public void ApplyToMesh(Mesh mesh)
    {
        if (mesh == null) return;

        if (Vertices != null) mesh.vertices = Vertices;
        if (Triangles != null) mesh.triangles = Triangles;
        if (Normals != null) mesh.normals = Normals;
        if (UV != null) mesh.uv = UV;
        if (Tangents != null) mesh.tangents = Tangents;
        if (Colors != null) mesh.colors = Colors;

        mesh.RecalculateBounds();
    }
}

/// <summary>
/// Данные для дороги (thread-safe)
/// </summary>
public class RoadGenerationData
{
    public string Name;
    public string Id;
    public Vector3 Position;
    public List<Vector3> RoadCorners;
    public List<Vector3> LeftPoints;
    public List<Vector3> RightPoints;
    public List<List<Vector3>> HolesCorners;
    public float Width;
    public int Lanes;
    public bool IsArea;
    public int Layer;

    public MeshGenerationData MeshData;

    // Данные для материала (определяются в фоне, применяются в главном потоке)
    public string Kind;
    public string SurfaceName;
    public int LayersLevel;
    public RoadUsageType TypeUsage;
}
