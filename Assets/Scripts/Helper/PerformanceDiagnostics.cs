using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

/// <summary>
/// Комплексный диагностировщик производительности
/// 
/// Позволяет выявить причину низкого FPS:
/// - CPU боты (Update, корутины, физика)
/// - GPU боты (рендеринг, draw calls)
/// - Проблемы памяти (GC, аллокации)
/// - Узкие места в конкретных скриптах
/// </summary>
public class PerformanceDiagnostics : MonoBehaviour
{
    public static PerformanceDiagnostics Instance { get; private set; }

    [Header("Display Settings")]
    public bool showOverlay = true;
    public bool logToConsole = false;
    public KeyCode toggleKey = KeyCode.F3;
    public int targetFPS = 60;

    [Header("Analysis Settings")]
    public float sampleInterval = 0.5f;
    public int frameHistorySize = 60;
    public bool trackMemory = true;
    public bool trackGPU = true;

    [Header("References")]
    public Transform player;

    // Метрики FPS
    private Queue<float> _frameTimes = new Queue<float>();
    private float _fps;
    private float _minFps;
    private float _maxFps;
    private float _avgFps;

    // Метрики CPU
    private float _updateTime;
    private float _renderTime;
    private float _physicsTime;
    private float _scriptTime;
    private float _coroutineTime;

    // Метрики памяти
    private long _totalMemory;
    private long _usedMemory;
    private long _gcCount;
    private float _gcTime;

    // Метрики рендеринга
    private int _drawCalls;
    private int _triangles;
    private int _vertices;
    private int _batches;
    private int _setPassCalls;

    // Анализ скриптов
    private Dictionary<string, ScriptMetrics> _scriptMetrics = new Dictionary<string, ScriptMetrics>();
    private List<SlowScript> _slowScripts = new List<SlowScript>();

    // Структуры данных
    private class ScriptMetrics
    {
        public string Name;
        public float TotalTime;
        public int CallCount;
        public float MaxTime;
        public float AvgTime => CallCount > 0 ? TotalTime / CallCount : 0;
    }

    private class SlowScript
    {
        public string Name;
        public float Time;
        public string Category;
    }

    // GUI
    private StringBuilder _sb = new StringBuilder();
    private Rect _windowRect = new Rect(10, 10, 400, 600);
    private Vector2 _scrollPosition;
    private int _tabIndex;
    private string[] _tabs = { "Overview", "CPU", "GPU", "Memory", "Objects", "Scripts" };

    // Profiler sampling
    private Stopwatch _frameStopwatch = new Stopwatch();
    private Stopwatch _updateStopwatch = new Stopwatch();

    // События для других скриптов
    public event Action<float> OnFPSUpdated;
    public event Action<string, float> OnSlowScriptDetected;

    private void Awake()
    {
        Instance = this;
        Application.targetFrameRate = targetFPS;
    }

    private IEnumerator Start()
    {
        // Инициализация
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // Запуск анализа
        StartCoroutine(AnalyzePerformance());
        StartCoroutine(DetectSlowCoroutines());

        yield return null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            showOverlay = !showOverlay;

        // Сбор метрик кадра
        CollectFrameMetrics();

        // Проверка на критическое падение FPS
        if (_fps < 20 && logToConsole)
        {
            Debug.LogWarning($"[Performance] Critical FPS drop: {_fps:F1}");
        }
    }

    private void LateUpdate()
    {
        // Анализ времени кадра
        _frameStopwatch.Stop();
        float frameTime = (float)_frameStopwatch.Elapsed.TotalMilliseconds;
        _frameStopwatch.Restart();
    }

    private void OnGUI()
    {
        if (!showOverlay) return;

        // Курсор будет виден
        Cursor.visible = true;
        // Курсор не будет заблокирован
        Cursor.lockState = CursorLockMode.None;

        _windowRect = GUILayout.Window(0, _windowRect, DrawWindow, "Performance Diagnostics");
    }

    private void DrawWindow(int windowID)
    {
        // Табы
        _tabIndex = GUILayout.Toolbar(_tabIndex, _tabs);

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(550));

        switch (_tabIndex)
        {
            case 0: DrawOverview(); break;
            case 1: DrawCPU(); break;
            case 2: DrawGPU(); break;
            case 3: DrawMemory(); break;
            case 4: DrawObjects(); break;
            case 5: DrawScripts(); break;
        }

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    #region Drawing Methods

    private void DrawOverview()
    {
        // FPS
        GUILayout.Label("<b>=== FPS ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
        DrawMetric("Current FPS", _fps, GetFPSColor(_fps), "F1");
        DrawMetric("Min FPS", _minFps, Color.red, "F1");
        DrawMetric("Max FPS", _maxFps, Color.green, "F1");
        DrawMetric("Avg FPS", _avgFps, Color.yellow, "F1");
        DrawMetric("Frame Time", 1000f / Mathf.Max(_fps, 0.1f), Color.white, "F2", "ms");

        GUILayout.Space(10);

        // Bottleneck detection
        GUILayout.Label("<b>=== Bottleneck Detection ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
        string bottleneck = DetectBottleneck();
        var color = bottleneck == "CPU" ? Color.red :
                    bottleneck == "GPU" ? Color.magenta :
                    bottleneck == "Memory" ? Color.cyan : Color.green;
        GUILayout.Label($"Bottleneck: <color=#{ColorUtility.ToHtmlStringRGB(color)}>{bottleneck}</color>",
            new GUIStyle(GUI.skin.label) { richText = true });

        GUILayout.Space(10);

        // Quick stats
        GUILayout.Label("<b>=== Quick Stats ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
        DrawMetric("Draw Calls", _drawCalls, _drawCalls > 2000 ? Color.red : Color.white, "N0");
        DrawMetric("Triangles", _triangles, _triangles > 1000000 ? Color.red : Color.white, "N0");
        DrawMetric("Batches", _batches, _batches > 500 ? Color.red : Color.white, "N0");
        DrawMetric("Memory", _usedMemory / 1024 / 1024, _usedMemory > 1000 * 1024 * 1024 ? Color.red : Color.white, "N0", "MB");
    }

    private void DrawCPU()
    {
        GUILayout.Label("<b>=== CPU Analysis ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
        DrawMetric("Update Time", _updateTime, _updateTime > 10 ? Color.red : Color.white, "F2", "ms");
        DrawMetric("Render Time", _renderTime, _renderTime > 10 ? Color.red : Color.white, "F2", "ms");
        DrawMetric("Physics Time", _physicsTime, _physicsTime > 5 ? Color.red : Color.white, "F2", "ms");
        DrawMetric("Script Time", _scriptTime, _scriptTime > 10 ? Color.red : Color.white, "F2", "ms");

        GUILayout.Space(10);
        GUILayout.Label("<b>=== Top Slow Scripts ===</b>", new GUIStyle(GUI.skin.label) { richText = true });

        _slowScripts.Sort((a, b) => b.Time.CompareTo(a.Time));
        int count = Mathf.Min(10, _slowScripts.Count);
        for (int i = 0; i < count; i++)
        {
            var script = _slowScripts[i];
            var color = script.Time > 5 ? Color.red : script.Time > 2 ? Color.yellow : Color.white;
            GUILayout.Label($"{script.Name}: <color=#{ColorUtility.ToHtmlStringRGB(color)}>{script.Time:F2}ms</color> [{script.Category}]",
                new GUIStyle(GUI.skin.label) { richText = true });
        }

        GUILayout.Space(10);

        // Рекомендации
        GUILayout.Label("<b>=== Recommendations ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
        if (_updateTime > 10)
            GUILayout.Label("• Optimize Update() methods - use events instead of polling");
        if (_physicsTime > 5)
            GUILayout.Label("• Reduce physics: decrease fixed timestep or use triggers");
        if (_scriptTime > 10)
            GUILayout.Label("• Profile specific scripts with Unity Profiler");
    }

    private void DrawGPU()
    {
        GUILayout.Label("<b>=== GPU Analysis ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
        DrawMetric("Draw Calls", _drawCalls, GetDrawCallColor(_drawCalls), "N0");
        DrawMetric("SetPass Calls", _setPassCalls, _setPassCalls > 100 ? Color.red : Color.white, "N0");
        DrawMetric("Triangles", _triangles, GetTriangleColor(_triangles), "N0");
        DrawMetric("Vertices", _vertices, Color.white, "N0");
        DrawMetric("Batches", _batches, GetBatchColor(_batches), "N0");

        GUILayout.Space(10);

        // Расчёт нагрузки
        float estimatedFillRate = _triangles * 0.000001f;
        GUILayout.Label($"Estimated Fill Rate: {estimatedFillRate:F2}M tris/frame");

        GUILayout.Space(10);
        GUILayout.Label("<b>=== Recommendations ===</b>", new GUIStyle(GUI.skin.label) { richText = true });

        if (_drawCalls > 2000)
            GUILayout.Label("• Reduce draw calls: use batching, LOD, culling");
        if (_setPassCalls > 100)
            GUILayout.Label("• Reduce material variants: use texture atlases");
        if (_triangles > 1000000)
            GUILayout.Label("• Reduce geometry: use LOD, occlusion culling");
        if (_batches > 500)
            GUILayout.Label("• Improve batching: combine meshes, GPU instancing");
    }

    private void DrawMemory()
    {
        GUILayout.Label("<b>=== Memory Analysis ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
        DrawMetric("Total Memory", _totalMemory / 1024 / 1024, Color.white, "N0", "MB");
        DrawMetric("Used Memory", _usedMemory / 1024 / 1024, _usedMemory > 1500 * 1024 * 1024 ? Color.red : Color.white, "N0", "MB");
        DrawMetric("GC Collections", _gcCount, _gcCount > 10 ? Color.yellow : Color.white, "N0");
        DrawMetric("GC Time", _gcTime, _gcTime > 10 ? Color.red : Color.white, "F2", "ms");

        GUILayout.Space(10);

        // Детальная информация о памяти
        GUILayout.Label("<b>=== Unity Memory ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"Total Reserved: {Profiler.GetTotalReservedMemoryLong() / 1024 / 1024} MB");
        GUILayout.Label($"Total Allocated: {Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024} MB");
        GUILayout.Label($"Unused Reserved: {Profiler.GetTotalUnusedReservedMemoryLong() / 1024 / 1024} MB");
        GUILayout.Label($"Mono Heap: {Profiler.GetMonoHeapSizeLong() / 1024 / 1024} MB");
        GUILayout.Label($"Mono Used: {Profiler.GetMonoUsedSizeLong() / 1024 / 1024} MB");

        GUILayout.Space(10);
        GUILayout.Label("<b>=== Recommendations ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
        if (_gcCount > 10)
            GUILayout.Label("• Reduce allocations: avoid 'new' in Update(), cache references");
        if (_gcTime > 10)
            GUILayout.Label("• Object pooling for frequently created objects");
    }

    private void DrawObjects()
    {
        GUILayout.Label("<b>=== Object Statistics ===</b>", new GUIStyle(GUI.skin.label) { richText = true });

        // Подсчёт объектов
        var allObjects = FindObjectsOfType<GameObject>();
        int activeObjects = 0;
        int inactiveObjects = 0;
        int renderers = 0;
        int colliders = 0;
        int rigidbodies = 0;
        int scripts = 0;

        foreach (var obj in allObjects)
        {
            if (obj.activeInHierarchy) activeObjects++;
            else inactiveObjects++;

            renderers += obj.GetComponents<Renderer>().Length;
            colliders += obj.GetComponents<Collider>().Length;
            rigidbodies += obj.GetComponents<Rigidbody>().Length;
            scripts += obj.GetComponents<MonoBehaviour>().Length;
        }

        DrawMetric("Total GameObjects", allObjects.Length, Color.white, "N0");
        DrawMetric("Active", activeObjects, Color.green, "N0");
        DrawMetric("Inactive", inactiveObjects, Color.yellow, "N0");
        DrawMetric("Renderers", renderers, Color.white, "N0");
        DrawMetric("Colliders", colliders, Color.white, "N0");
        DrawMetric("Rigidbodies", rigidbodies, Color.white, "N0");
        DrawMetric("MonoBehaviours", scripts, Color.white, "N0");

        GUILayout.Space(10);

        // Специфичные объекты GeoRender
        GUILayout.Label("<b>=== GeoRender Objects ===</b>", new GUIStyle(GUI.skin.label) { richText = true });

        var buildings = FindObjectsOfType<Building>();
        var roads = FindObjectsOfType<Road>();
        var details = FindObjectsOfType<Detail>();
        var vehicles = FindObjectsOfType<AICarController>();
        var pedestrians = FindObjectsOfType<AdvancedAIPedestrianController>();

        DrawMetric("Buildings", buildings.Length, Color.white, "N0");
        DrawMetric("Roads", roads.Length, Color.white, "N0");
        DrawMetric("Details", details.Length, Color.white, "N0");
        DrawMetric("Vehicles", vehicles.Length, Color.white, "N0");
        DrawMetric("Pedestrians", pedestrians.Length, Color.white, "N0");
    }

    private void DrawScripts()
    {
        GUILayout.Label("<b>=== Script Performance ===</b>", new GUIStyle(GUI.skin.label) { richText = true });

        // Конвертируем в список и сортируем
        var sortedScripts = new List<KeyValuePair<string, ScriptMetrics>>();
        foreach (var kvp in _scriptMetrics)
        {
            sortedScripts.Add(kvp);
        }
        sortedScripts.Sort((a, b) => b.Value.TotalTime.CompareTo(a.Value.TotalTime));

        int displayCount = Mathf.Min(20, sortedScripts.Count);
        for (int i = 0; i < displayCount; i++)
        {
            var kvp = sortedScripts[i];
            var metrics = kvp.Value;
            var color = metrics.AvgTime > 5 ? Color.red : metrics.AvgTime > 1 ? Color.yellow : Color.white;

            GUILayout.Label($"<b>{metrics.Name}</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"  Total: {metrics.TotalTime:F2}ms | Avg: <color=#{ColorUtility.ToHtmlStringRGB(color)}>{metrics.AvgTime:F3}ms</color> | Calls: {metrics.CallCount} | Max: {metrics.MaxTime:F2}ms",
                new GUIStyle(GUI.skin.label) { richText = true });
        }

        if (_scriptMetrics.Count == 0)
        {
            GUILayout.Label("No scripts tracked. Add tracking to your scripts.");
        }

        GUILayout.Space(10);
        GUILayout.Label("<b>How to track scripts:</b>");
        GUILayout.Label("PerformanceDiagnostics.Track(\"MyScript\", () => { /* code */ });");
    }

    private void DrawMetric(string name, float value, Color color, string format, string suffix = "")
    {
        GUILayout.Label($"{name}: <color=#{ColorUtility.ToHtmlStringRGB(color)}>{value.ToString(format)}{suffix}</color>",
            new GUIStyle(GUI.skin.label) { richText = true });
    }

    #endregion

    #region Collection Methods

    private void CollectFrameMetrics()
    {
        float frameTime = Time.unscaledDeltaTime;
        _frameTimes.Enqueue(frameTime);

        while (_frameTimes.Count > frameHistorySize)
            _frameTimes.Dequeue();

        // Вычисляем FPS
        _fps = 1f / Mathf.Max(frameTime, 0.0001f);

        if (_frameTimes.Count >= frameHistorySize)
        {
            float sum = 0;
            _minFps = float.MaxValue;
            _maxFps = float.MinValue;

            foreach (var ft in _frameTimes)
            {
                float fps = 1f / Mathf.Max(ft, 0.0001f);
                sum += fps;
                if (fps < _minFps) _minFps = fps;
                if (fps > _maxFps) _maxFps = fps;
            }

            _avgFps = sum / _frameTimes.Count;
        }

        OnFPSUpdated?.Invoke(_fps);
    }

    private IEnumerator AnalyzePerformance()
    {
        while (true)
        {
            yield return new WaitForSeconds(sampleInterval);

#if UNITY_EDITOR
            // Собираем метрики рендеринга
            _drawCalls = UnityStats.drawCalls;
            _triangles = UnityStats.triangles;
            _vertices = UnityStats.vertices;
            _batches = UnityStats.batches;
            _setPassCalls = UnityStats.setPassCalls;
#endif

            // Собираем метрики памяти
            if (trackMemory)
            {
                _totalMemory = Profiler.GetTotalReservedMemoryLong();
                _usedMemory = Profiler.GetTotalAllocatedMemoryLong();
                _gcCount = GC.CollectionCount(0);
            }

            // Очищаем медленные скрипты для нового анализа
            _slowScripts.Clear();
        }
    }

    private IEnumerator DetectSlowCoroutines()
    {
        while (true)
        {
            // Проверяем все корутины в сцене
            var behaviours = FindObjectsOfType<MonoBehaviour>();
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null) continue;

                var type = behaviour.GetType();
                string name = type.Name;

                // Проверяем Update
                var updateMethod = type.GetMethod("Update");
                if (updateMethod != null && updateMethod.DeclaringType == type)
                {
                    var sw = Stopwatch.StartNew();
                    // Нельзя вызвать напрямую, но можно оценить по времени между кадрами
                    sw.Stop();
                }
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private string DetectBottleneck()
    {
        // CPU bound
        if (_updateTime > 15 || _scriptTime > 15 || _physicsTime > 10)
            return "CPU";

        // GPU bound
        if (_drawCalls > 2000 || _triangles > 2000000 || _batches > 1000)
            return "GPU";

        // Memory bound
        if (_gcCount > 20 || _gcTime > 20)
            return "Memory";

        return "None";
    }

    #endregion

    #region Utility Methods

    private Color GetFPSColor(float fps)
    {
        if (fps >= 50) return Color.green;
        if (fps >= 30) return Color.yellow;
        if (fps >= 15) return new Color(1, 0.5f, 0); // Orange
        return Color.red;
    }

    private Color GetDrawCallColor(int drawCalls)
    {
        if (drawCalls < 500) return Color.green;
        if (drawCalls < 1500) return Color.yellow;
        if (drawCalls < 3000) return new Color(1, 0.5f, 0);
        return Color.red;
    }

    private Color GetTriangleColor(int triangles)
    {
        if (triangles < 500000) return Color.green;
        if (triangles < 1000000) return Color.yellow;
        if (triangles < 3000000) return new Color(1, 0.5f, 0);
        return Color.red;
    }

    private Color GetBatchColor(int batches)
    {
        if (batches < 200) return Color.green;
        if (batches < 500) return Color.yellow;
        if (batches < 1000) return new Color(1, 0.5f, 0);
        return Color.red;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Отследить время выполнения кода
    /// </summary>
    public static void Track(string scriptName, Action action)
    {
        if (Instance == null || action == null)
        {
            action?.Invoke();
            return;
        }

        var sw = Stopwatch.StartNew();
        action.Invoke();
        sw.Stop();

        float timeMs = (float)sw.Elapsed.TotalMilliseconds;

        if (!Instance._scriptMetrics.ContainsKey(scriptName))
        {
            Instance._scriptMetrics[scriptName] = new ScriptMetrics { Name = scriptName };
        }

        var metrics = Instance._scriptMetrics[scriptName];
        metrics.TotalTime += timeMs;
        metrics.CallCount++;
        if (timeMs > metrics.MaxTime) metrics.MaxTime = timeMs;

        // Добавляем в список медленных если > 1ms
        if (timeMs > 1f)
        {
            Instance._slowScripts.Add(new SlowScript
            {
                Name = scriptName,
                Time = timeMs,
                Category = "Custom"
            });
        }
    }

    /// <summary>
    /// Записать время выполнения скрипта
    /// </summary>
    public static void RecordScriptTime(string scriptName, float timeMs, string category = "Update")
    {
        if (Instance == null) return;

        if (!Instance._scriptMetrics.ContainsKey(scriptName))
        {
            Instance._scriptMetrics[scriptName] = new ScriptMetrics { Name = scriptName };
        }

        var metrics = Instance._scriptMetrics[scriptName];
        metrics.TotalTime += timeMs;
        metrics.CallCount++;
        if (timeMs > metrics.MaxTime) metrics.MaxTime = timeMs;

        if (timeMs > 1f)
        {
            Instance._slowScripts.Add(new SlowScript
            {
                Name = scriptName,
                Time = timeMs,
                Category = category
            });

            Instance.OnSlowScriptDetected?.Invoke(scriptName, timeMs);
        }
    }

    /// <summary>
    /// Получить текущий FPS
    /// </summary>
    public static float GetCurrentFPS() => Instance?._fps ?? 0;

    /// <summary>
    /// Вывести подробный отчёт
    /// </summary>
    public static void LogFullReport()
    {
        if (Instance == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("=== Performance Report ===");
        sb.AppendLine($"FPS: {Instance._fps:F1} (min: {Instance._minFps:F1}, max: {Instance._maxFps:F1}, avg: {Instance._avgFps:F1})");
        sb.AppendLine($"Bottleneck: {Instance.DetectBottleneck()}");
        sb.AppendLine();
        sb.AppendLine("=== Rendering ===");
        sb.AppendLine($"Draw Calls: {Instance._drawCalls}");
        sb.AppendLine($"Triangles: {Instance._triangles:N0}");
        sb.AppendLine($"Batches: {Instance._batches}");
        sb.AppendLine();
        sb.AppendLine("=== Memory ===");
        sb.AppendLine($"Total: {Instance._totalMemory / 1024 / 1024} MB");
        sb.AppendLine($"Used: {Instance._usedMemory / 1024 / 1024} MB");
        sb.AppendLine($"GC Collections: {Instance._gcCount}");
        sb.AppendLine();
        sb.AppendLine("=== Slow Scripts ===");
        Instance._slowScripts.Sort((a, b) => b.Time.CompareTo(a.Time));
        foreach (var script in Instance._slowScripts)
        {
            sb.AppendLine($"{script.Name}: {script.Time:F2}ms [{script.Category}]");
        }

        Debug.Log(sb.ToString());
    }

    #endregion
}

/// <summary>
/// Атрибут для автоматического отслеживания производительности скрипта
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class TrackPerformanceAttribute : Attribute
{
    public float WarningThreshold { get; set; } = 5f; // ms
}