using System;
using UnityEngine;

public class TrafficLightController : MonoBehaviour
{
    [System.Serializable]
    public class LightState
    {
        public bool carRed;
        public bool carYellow;
        public bool carGreen;
        public bool pedestrianTop;
        public bool pedestrianBottom;
    }

    [Header("Material Indices")]
    [SerializeField] private int carRedIndex = 0;
    [SerializeField] private int carYellowIndex = 1;
    [SerializeField] private int carGreenIndex = 2;
    [SerializeField] private int pedestrianTopIndex = 3;
    [SerializeField] private int pedestrianBottomIndex = 4;

    [Header("Light Colors")]
    [SerializeField] private Color carRedColor = Color.red;
    [SerializeField] private Color carYellowColor = Color.yellow;
    [SerializeField] private Color carGreenColor = Color.green;
    [SerializeField] private Color pedestrianOnColor = Color.white;
    [SerializeField] private Color offColor = Color.black;

    [Header("Emission Settings")]
    [SerializeField] private bool useEmission = true;
    [SerializeField] private float emissionIntensity = 2f;
    [SerializeField] private bool dynamicEmission = true;
    [SerializeField] private float maxEmissionIntensity = 3f;

    [Header("Current State")]
    [SerializeField] private LightState currentState = new LightState();

    private MeshRenderer meshRenderer;
    private Material[] materialInstances;

    private void Awake()
    {
        InitializeMaterials();
    }

    private void InitializeMaterials()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        // Создаем инстансы материалов
        materialInstances = new Material[meshRenderer.sharedMaterials.Length];
        for (int i = 0; i < materialInstances.Length; i++)
        {
            materialInstances[i] = new Material(meshRenderer.sharedMaterials[i]);
            // Включаем эмиссию для всех материалов изначально
            if (useEmission)
            {
                materialInstances[i].EnableKeyword("_EMISSION");
            }
        }

        // Применяем инстансы
        meshRenderer.materials = materialInstances;

        // Изначально все выключены
        SetAllLightsOff();
    }

    /// <summary>
    /// Выключить все огни
    /// </summary>
    public void SetAllLightsOff()
    {
        currentState.carRed = false;
        currentState.carYellow = false;
        currentState.carGreen = false;
        currentState.pedestrianTop = false;
        currentState.pedestrianBottom = false;

        UpdateMaterials();
    }

    /// <summary>
    /// Установить автомобильные огни
    /// </summary>
    public void SetCarLights(bool red, bool yellow, bool green)
    {
        currentState.carRed = red;
        currentState.carYellow = yellow;
        currentState.carGreen = green;

        UpdateMaterials();
    }

    /// <summary>
    /// Установить пешеходные огни
    /// </summary>
    public void SetPedestrianLights(bool top, bool bottom)
    {
        currentState.pedestrianTop = top;
        currentState.pedestrianBottom = bottom;

        UpdateMaterials();
    }

    /// <summary>
    /// Обновить все материалы с учетом эмиссии
    /// </summary>
    private void UpdateMaterials()
    {
        // Автомобильные огни
        UpdateLightMaterial(carRedIndex, currentState.carRed, carRedColor);
        UpdateLightMaterial(carYellowIndex, currentState.carYellow, carYellowColor);
        UpdateLightMaterial(carGreenIndex, currentState.carGreen, carGreenColor);

        // Пешеходные огни
        UpdateLightMaterial(pedestrianTopIndex, currentState.pedestrianTop, pedestrianOnColor);
        UpdateLightMaterial(pedestrianBottomIndex, currentState.pedestrianBottom, pedestrianOnColor);
    }

    /// <summary>
    /// Обновить материал одного огня с эмиссией
    /// </summary>
    private void UpdateLightMaterial(int materialIndex, bool isOn, Color lightColor)
    {
        if (materialIndex >= materialInstances.Length || materialIndex < 0)
            return;


        var material = materialInstances[materialIndex];

        // Устанавливаем основной цвет
        material.color = isOn ? lightColor : offColor;

        if (materialIndex == pedestrianTopIndex || materialIndex == pedestrianBottomIndex)
            return;

        // Обрабатываем эмиссию
        if (useEmission)
        {
            if (isOn)
            {
                // Включаем эмиссию
                material.EnableKeyword("_EMISSION");

                // Рассчитываем интенсивность эмиссии
                float intensity = emissionIntensity;
                if (dynamicEmission)
                {
                    // Динамическая интенсивность в зависимости от цвета
                    intensity = CalculateEmissionIntensity(lightColor);
                }

                // Устанавливаем цвет эмиссии
                Color emissionColor = lightColor * intensity;
                material.SetColor("_EmissionColor", emissionColor);

                // Для Global Illumination
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                // Выключаем эмиссию
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    /// <summary>
    /// Рассчитать интенсивность эмиссии на основе цвета
    /// </summary>
    private float CalculateEmissionIntensity(Color color)
    {
        // Разная интенсивность для разных цветов
        float brightness = (color.r + color.g + color.b) / 3f;
        float intensity = emissionIntensity;

        if (dynamicEmission)
        {
            // Красный свет обычно менее яркий
            if (color.r > 0.8f && color.g < 0.3f) // Красный
                intensity = emissionIntensity * 1.5f;
            // Зеленый - средняя яркость
            else if (color.g > 0.8f && color.r < 0.3f) // Зеленый
                intensity = emissionIntensity * 1.2f;
            // Желтый - самый яркий
            else if (color.r > 0.8f && color.g > 0.8f) // Желтый
                intensity = emissionIntensity * 2f;
        }

        return Mathf.Clamp(intensity, 0.1f, maxEmissionIntensity);
    }

    /// <summary>
    /// Установить интенсивность эмиссии
    /// </summary>
    public void SetEmissionIntensity(float intensity)
    {
        emissionIntensity = Mathf.Clamp(intensity, 0.1f, maxEmissionIntensity);
        UpdateMaterials();
    }

    /// <summary>
    /// Включить/выключить эмиссию
    /// </summary>
    public void SetEmissionEnabled(bool enabled)
    {
        useEmission = enabled;

        if (materialInstances != null)
        {
            foreach (var material in materialInstances)
            {
                if (enabled)
                    material.EnableKeyword("_EMISSION");
                else
                    material.DisableKeyword("_EMISSION");
            }
        }

        UpdateMaterials();
    }

    /// <summary>
    /// Типовые состояния светофора
    /// </summary>
    public void SetCarStop()
    {
        SetCarLights(true, false, false);
    }

    public void SetCarReady()
    {
        SetCarLights(false, true, false);
    }

    public void SetCarGo()
    {
        SetCarLights(false, false, true);
    }

    public void SetPedestrianStop()
    {
        SetPedestrianLights(true, false);
    }

    public void SetPedestrianGo()
    {
        SetPedestrianLights(false, true);
    }

    /// <summary>
    /// Полностью выключить светофор
    /// </summary>
    public void TurnOff()
    {
        SetAllLightsOff();
    }

    /// <summary>
    /// Мигать конкретным светом
    /// </summary>
    public void BlinkLight(int materialIndex, float interval = 0.5f, int cycles = 10)
    {
        StartCoroutine(BlinkRoutine(materialIndex, interval, cycles));
    }

    private System.Collections.IEnumerator BlinkRoutine(int materialIndex, float interval, int cycles)
    {
        bool originalState = GetLightState(materialIndex);

        for (int i = 0; i < cycles * 2; i++)
        {
            ToggleSingleLight(materialIndex);
            yield return new WaitForSeconds(interval);
        }

        // Восстанавливаем оригинальное состояние
        SetSingleLight(materialIndex, originalState);
    }

    /// <summary>
    /// Получить состояние конкретного света
    /// </summary>
    private bool GetLightState(int materialIndex)
    {
        if (materialIndex == carRedIndex) return currentState.carRed;
        if (materialIndex == carYellowIndex) return currentState.carYellow;
        if (materialIndex == carGreenIndex) return currentState.carGreen;
        if (materialIndex == pedestrianTopIndex) return currentState.pedestrianTop;
        if (materialIndex == pedestrianBottomIndex) return currentState.pedestrianBottom;
        return false;
    }

    /// <summary>
    /// Переключить один свет
    /// </summary>
    private void ToggleSingleLight(int materialIndex)
    {
        if (materialIndex == carRedIndex) currentState.carRed = !currentState.carRed;
        else if (materialIndex == carYellowIndex) currentState.carYellow = !currentState.carYellow;
        else if (materialIndex == carGreenIndex) currentState.carGreen = !currentState.carGreen;
        else if (materialIndex == pedestrianTopIndex) currentState.pedestrianTop = !currentState.pedestrianTop;
        else if (materialIndex == pedestrianBottomIndex) currentState.pedestrianBottom = !currentState.pedestrianBottom;

        UpdateMaterials();
    }

    /// <summary>
    /// Установить один свет
    /// </summary>
    private void SetSingleLight(int materialIndex, bool state)
    {
        if (materialIndex == carRedIndex) currentState.carRed = state;
        else if (materialIndex == carYellowIndex) currentState.carYellow = state;
        else if (materialIndex == carGreenIndex) currentState.carGreen = state;
        else if (materialIndex == pedestrianTopIndex) currentState.pedestrianTop = state;
        else if (materialIndex == pedestrianBottomIndex) currentState.pedestrianBottom = state;

        UpdateMaterials();
    }

    /// <summary>
    /// Получить текущую интенсивность эмиссии
    /// </summary>
    public float GetCurrentEmissionIntensity()
    {
        return emissionIntensity;
    }

    /// <summary>
    /// Очистка инстансов при уничтожении
    /// </summary>
    private void OnDestroy()
    {
        if (materialInstances != null)
        {
            foreach (var mat in materialInstances)
            {
                if (mat != null)
                    Destroy(mat);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Метод для редактора: предпросмотр эмиссии
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying && materialInstances != null)
        {
            UpdateMaterials();
        }
    }
#endif
}
