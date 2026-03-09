using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Патчи для BuildingMaker - применение оптимизаций
/// 
/// Добавьте этот скрипт на тот же GameObject что и BuildingMaker,
/// либо скопируйте нужные методы в BuildingMaker.cs
/// </summary>
public class BuildingMakerOptimizations : MonoBehaviour
{
    [Header("Optimization Settings")]
    [Tooltip("Максимальное количество зданий")]
    public int maxBuildings = 500;

    [Tooltip("Размер batch для обработки")]
    public int batchSize = 20;

    [Tooltip("Максимальное время на кадр (мс)")]
    public float maxFrameTimeMs = 5f;

    [Tooltip("Максимальное количество узлов в здании")]
    public int maxNodes = 60;

    [Tooltip("Расстояние от игрока для генерации")]
    public float maxGenerationDistance = 500f;

    [Header("Distance Check")]
    public Transform player;
    public bool useDistanceFilter = true;

    [Header("Simplification")]
    [Tooltip("Упрощать здания дальше этой дистанции")]
    public float simplifyDistance = 200f;

    [Tooltip("Не генерировать крыши для далёких зданий")]
    public bool skipRoofForDistant = true;

    private BuildingMaker _buildingMaker;
    private Stopwatch _frameStopwatch = new Stopwatch();
    private int _processedThisFrame = 0;
    private float _frameStartTime = 0f;

    private void Awake()
    {
        _buildingMaker = GetComponent<BuildingMaker>();
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(2f);

        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // Применяем настройки к BuildingMaker
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (_buildingMaker == null) return;

        // Применяем batchSize
        _buildingMaker.batchSize = batchSize;

        // Применяем MaxNodes
        _buildingMaker.MaxNodes = maxNodes;
    }

    /// <summary>
    /// Проверка - нужно ли генерировать это здание
    /// Вызывать в начале CreateBuilding()
    /// </summary>
    public bool ShouldGenerateBuilding(Vector3 position)
    {
        // Проверка лимита
        int currentCount = FindObjectsOfType<Building>().Length;
        if (currentCount >= maxBuildings)
        {
            return false;
        }

        // Проверка расстояния
        if (useDistanceFilter && player != null)
        {
            float dist = Vector3.Distance(position, player.position);
            if (dist > maxGenerationDistance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проверка - нужно ли генерировать крышу
    /// </summary>
    public bool ShouldGenerateRoof(Vector3 position)
    {
        if (!skipRoofForDistant || player == null)
            return true;

        float dist = Vector3.Distance(position, player.position);
        return dist < simplifyDistance;
    }

    /// <summary>
    /// Начало кадра обработки
    /// </summary>
    public void BeginFrame()
    {
        _frameStopwatch.Restart();
        _processedThisFrame = 0;
        _frameStartTime = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// Проверка - можно ли продолжать обработку
    /// </summary>
    public bool CanContinueProcessing()
    {
        _processedThisFrame++;

        // Проверка по количеству
        if (_processedThisFrame >= batchSize)
            return false;

        // Проверка по времени
        float elapsedMs = (float)_frameStopwatch.Elapsed.TotalMilliseconds;
        return elapsedMs < maxFrameTimeMs;
    }

    /// <summary>
    /// Конец кадра обработки
    /// </summary>
    public void EndFrame()
    {
        _frameStopwatch.Stop();
    }

    #region Static API для быстрого доступа

    /// <summary>
    /// Установить агрессивные настройки
    /// </summary>
    public static void SetAggressiveSettings()
    {
        var instance = FindObjectOfType<BuildingMakerOptimizations>();
        if (instance == null)
        {
            Debug.LogWarning("BuildingMakerOptimizations not found");
            return;
        }

        instance.maxBuildings = 300;
        instance.batchSize = 30;
        instance.maxFrameTimeMs = 3f;
        instance.maxNodes = 40;
        instance.maxGenerationDistance = 300f;
        instance.simplifyDistance = 150f;
        instance.skipRoofForDistant = true;
        instance.useDistanceFilter = true;

        instance.ApplySettings();
        Debug.Log("[BuildingMaker] Aggressive settings applied");
    }

    /// <summary>
    /// Установить сбалансированные настройки
    /// </summary>
    public static void SetBalancedSettings()
    {
        var instance = FindObjectOfType<BuildingMakerOptimizations>();
        if (instance == null) return;

        instance.maxBuildings = 500;
        instance.batchSize = 20;
        instance.maxFrameTimeMs = 5f;
        instance.maxNodes = 60;
        instance.maxGenerationDistance = 500f;
        instance.simplifyDistance = 200f;

        instance.ApplySettings();
        Debug.Log("[BuildingMaker] Balanced settings applied");
    }

    #endregion
}

/// <summary>
/// Расширения для BuildingMaker
/// </summary>
public static class BuildingMakerExtensions
{
    /// <summary>
    /// Оптимизированная генерация здания с проверками
    /// </summary>
    public static void CreateBuildingOptimized(this BuildingMaker maker, BaseOsm geo,
        BuildingMakerOptimizations optimizations = null)
    {
        if (optimizations == null)
            optimizations = BuildingMakerOptimizations.FindObjectOfType<BuildingMakerOptimizations>();

        // Проверка на начало кадра
        if (optimizations != null)
        {
            // Проверка расстояния и лимита
            Vector3 center = CalculateCenter(geo);
            if (!optimizations.ShouldGenerateBuilding(center))
                return;
        }

        // Вызываем оригинальный метод
        // maker.CreateBuilding(geo);
    }

    private static Vector3 CalculateCenter(BaseOsm geo)
    {
        Vector3 total = Vector3.zero;
        int count = 0;

        foreach (var id in geo.NodeIDs)
        {
            if (MapReader.Instance != null &&
                MapReader.Instance.nodes.TryGetValue(id, out OsmNode node))
            {
                total += (Vector3)node;
                count++;
            }
        }

        return count > 0 ? total / count : Vector3.zero;
    }
}
