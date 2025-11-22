using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// --- 1. Перечисление типов погоды ---
public enum WeatherType
{
    Clear,      // Ясно
    Cloudy,     // Облачно
    Rain,       // Дождь
    Storm       // Гроза (дождь + гром/молнии)
}

// --- 2. Класс параметров погоды ---
[System.Serializable]
public class WeatherParameters
{
    [Header("Lighting")]
    public float sunIntensityMultiplier = 1.0f; // Множитель интенсивности солнца
    public float moonIntensityMultiplier = 1.0f; // Множитель интенсивности луны
    public Color ambientColor = Color.white; // Цвет окружающего освещения
    public float fogDensity = 0.0f; // Плотность тумана

    [Header("Skybox")]
    public Color nightSkyColor = new Color(0.0f, 0.0f, 0.0f); // Цвет неба ночью
    public Color dawnDuskColor = new Color(0.4f, 0.3f, 0.2f); // Цвет рассвета/заката
    public Color daySkyColor = new Color(0.4f, 0.7f, 1.0f); // Цвет неба днём
    [Range(0, 1)] public float starThreshold = 0.946f; // Порог появления звёзд
    public float starBrightness = 0.49f; // Яркость звёзд

    [Header("Clouds")]
    public float cloudAlpha = 0.6f; // Плотность облаков
    public float cloudHardness = 0.8f; // Твёрдость облаков
    public float cloudBrightness = 1.4f; // Яркость облаков
    public float cloudScattering = 1.5f; // Рассеивание в облаках
    public float cloudHeightScatter = 1.5f; // Рассеивание по высоте
    public float cloudSubtract = 0.6f; // "Тонкость" облаков (меньше = толще)
    public float cloudScale = 0.3f; // Масштаб облаков
    public float cloudMovementSpeed = 0.1f; // Скорость движения облаков
    public float cloudWindSpeed = 0.01f; // Скорость движения облаков
    public Vector3 cloudWindDirection = Vector3.right; // Направление ветра для облаков

    [Header("Audio")]
    public AudioClip weatherAudioClip; // Звук погоды (дождь, гром)
    [Range(0, 1)] public float weatherAudioVolume = 0.5f; // Громкость звука

    // Можно добавить параметры для частиц (дождь, снег)
    [Header("Particles (Optional)")]
    public GameObject weatherParticleSystemPrefab; // Префаб системы частиц (дождь, снег)
    public Vector3 particleEmissionRate = new Vector3(10, 10, 10); // Пример (может быть сложнее)
}

[System.Serializable]
public class WeatherDataEntry
{
    public WeatherType weatherType;
    public WeatherParameters parameters;
}

public class WeatherSystem : MonoBehaviour
{
    [Header("References")]
    public DayNightCycle dayNightCycle; // Ссылка на вашу систему дня/ночи
    public Material skyboxMaterial; // Ссылка на ваш материал неба
    public Material cloudMaterial; // Ссылка на ваш материал облаков

    [Header("Weather Data")] // <-- Теперь это будет отображаться
    public WeatherDataEntry[] weatherEntries; // <-- Заменяем Dictionary
    private Dictionary<WeatherType, WeatherParameters> weatherDataDict = new Dictionary<WeatherType, WeatherParameters>();

    public WeatherType currentWeather = WeatherType.Clear;
    public AudioSource audioSource; // Для воспроизведения звуков погоды
    public GameObject currentWeatherParticles; // Для систем частиц (дождь и т.д.)
    public float cloudTime = 0f; // Для анимации движения облаков
    public float cloudWindTime = 0f; // Для анимации движения облаков

    // Singleton (опционально, для глобального доступа)
    public static WeatherSystem Instance { get { return _instance; } }
    private static WeatherSystem _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this); // Сохраняем объект между сценами, если нужно
        }
    }

    void Start()
    {
        // Инициализация звукового источника
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true; // Для звуков типа дождя

        // --- НОВОЕ: Заполняем словарь из массива ---
        weatherDataDict.Clear(); // На всякий случай
        if (weatherEntries != null) // Проверяем, что массив не null
        {
            foreach (var entry in weatherEntries)
            {
                // Проверяем на дубликаты (по желанию)
                if (!weatherDataDict.ContainsKey(entry.weatherType))
                {
                    weatherDataDict[entry.weatherType] = entry.parameters;
                }
                else
                {
                    Debug.LogWarning($"Дубликат типа погоды '{entry.weatherType}' в массиве weatherEntries. Используется первый найденный.");
                }
            }
        }

        if (weatherDataDict.ContainsKey(currentWeather))
        {
            SetWeather(currentWeather);
        }
        else
        {
            Debug.LogWarning("Тип погоды по умолчанию '" + currentWeather + "' не найден в массиве weatherEntries. Установите погоду вручную.");
        }
    }

    void Update()
    {
        // Анимация движения облаков (обновление времени для шейдера)
        if (weatherDataDict.ContainsKey(currentWeather))
        {
            var paramsForCurrentWeather = weatherDataDict[currentWeather];
            cloudTime += paramsForCurrentWeather.cloudMovementSpeed * Time.deltaTime;

            cloudWindTime += paramsForCurrentWeather.cloudMovementSpeed / 100 * Time.deltaTime;

            if (cloudMaterial != null)
            {
                cloudMaterial.SetFloat("_CloudTime", cloudTime);

                // Обновляем направление ветра (если нужно)
                cloudMaterial.SetFloat("_XShift", paramsForCurrentWeather.cloudWindDirection.x * cloudWindTime);
                cloudMaterial.SetFloat("_YShift", paramsForCurrentWeather.cloudWindDirection.y * cloudWindTime);
                cloudMaterial.SetFloat("_ZShift", paramsForCurrentWeather.cloudWindDirection.z * cloudWindTime); 
            }
        }
    }


    // Called in editor when values are changed in Inspector
    private void OnValidate()
    {
        // Обновление только если словарь уже заполнен (например, после первого запуска)
        // Иначе используем Start для инициализации
        if (weatherDataDict.ContainsKey(currentWeather))
        {
            SetWeather(currentWeather);
        }
    }

    /// <summary>
    /// Устанавливает текущую погоду.
    /// </summary>
    /// <param name="newWeather">Тип новой погоды.</param>
    public void SetWeather(WeatherType newWeather)
    {
        if (!weatherDataDict.ContainsKey(newWeather)) // <-- Используем словарь
        {
            Debug.LogWarning("Погода " + newWeather + " не настроена в массиве weatherEntries.");
            return;
        }

        Debug.LogWarning("Погода " + newWeather + " установлена.");

        // Останавливаем предыдущий звук
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        // Удаляем предыдущие частицы
        if (currentWeatherParticles != null)
        {
            Destroy(currentWeatherParticles);
        }

        currentWeather = newWeather;
        ApplyWeatherParameters();
    }

    /// <summary>
    /// Применяет параметры текущей погоды к шейдерам, освещению и аудио.
    /// </summary>
    private void ApplyWeatherParameters()
    {
        // var parameters = weatherData[currentWeather]; // <-- Старая строка
        var parameters = weatherDataDict[currentWeather]; // <-- Используем словарь

        if (skyboxMaterial != null)
        {
            skyboxMaterial.SetColor("_NightSkyColor", parameters.nightSkyColor);
            skyboxMaterial.SetColor("_DawnDuskColor", parameters.dawnDuskColor);
            skyboxMaterial.SetColor("_DaySkyColor", parameters.daySkyColor);
            skyboxMaterial.SetFloat("_StarThreshold", parameters.starThreshold);
            skyboxMaterial.SetFloat("_StarBrightness", parameters.starBrightness);
        }

        if (cloudMaterial != null)
        {
            cloudMaterial.SetFloat("_CloudAlpha", parameters.cloudAlpha);
            cloudMaterial.SetFloat("_CloudHardness", parameters.cloudHardness);
            cloudMaterial.SetFloat("_CloudBrightness", parameters.cloudBrightness);
            cloudMaterial.SetFloat("_CloudScattering", parameters.cloudScattering);
            cloudMaterial.SetFloat("_CloudHeightScatter", parameters.cloudHeightScatter);
            cloudMaterial.SetFloat("_CloudSubtract", parameters.cloudSubtract);
            cloudMaterial.SetFloat("_CloudScale", parameters.cloudScale);
        }

        if (dayNightCycle != null)
        {
            // Обратите внимание: умножение интенсивности может накапливаться
            // Лучше внести это изменение в DayNightCycle, как было в предыдущем примере
            dayNightCycle.sunLight.intensity *= parameters.sunIntensityMultiplier;
            dayNightCycle.moonLight.intensity *= parameters.moonIntensityMultiplier;
            RenderSettings.ambientLight = parameters.ambientColor;
            RenderSettings.fogDensity = parameters.fogDensity;
        }

        if (parameters.weatherAudioClip != null)
        {
            audioSource.clip = parameters.weatherAudioClip;
            audioSource.volume = parameters.weatherAudioVolume;
            audioSource.Play();
        }
        else
        {
            audioSource.Stop();
        }

        // --- 5. Применение к частицам (если используются) ---
        if (parameters.weatherParticleSystemPrefab != null)
        {
            if (currentWeatherParticles != null) Destroy(currentWeatherParticles); // Удаляем старые
            currentWeatherParticles = Instantiate(parameters.weatherParticleSystemPrefab, transform.position, Quaternion.identity);
            currentWeatherParticles.transform.SetParent(this.transform);
        }
    }

    /// <summary>
    /// Возвращает текущий тип погоды.
    /// </summary>
    /// <returns>Текущий тип погоды.</returns>
    public WeatherType GetCurrentWeather()
    {
        return currentWeather;
    }
}