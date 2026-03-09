using UnityEngine;
using System.Collections;

public class TrafficLightSequenceController : MonoBehaviour
{
    [Header("Sequence Timing")]
    [SerializeField] private float redDuration = 5f;
    [SerializeField] private float yellowDuration = 2f;
    [SerializeField] private float greenDuration = 5f;
    [SerializeField] private float pedestrianWalkDuration = 4f;
    [SerializeField] private float pedestrianBlinkDuration = 3f;
    [SerializeField] private float blinkInterval = 0.5f;

    [Header("Emission Effects")]
    [SerializeField] private bool useEmissionEffects = true;
    [SerializeField] private float warningEmissionIntensity = 3f;
    [SerializeField] private float transitionEmissionPulse = 0.3f;

    [Header("References")]
    [SerializeField] private TrafficLightController trafficLight;
    [SerializeField] private bool autoStart = true;

    private Coroutine sequenceCoroutine;
    private float originalEmissionIntensity;

    private void Start()
    {
        if (trafficLight != null)
        {
            originalEmissionIntensity = trafficLight.GetCurrentEmissionIntensity();
        }

        if (autoStart)
            StartSequence();
    }

    public void StartSequence()
    {
        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);

        sequenceCoroutine = StartCoroutine(SequenceRoutine());
    }

    public void StopSequence()
    {
        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);

        trafficLight.TurnOff();

        // Восстанавливаем оригинальную интенсивность эмиссии
        if (useEmissionEffects)
        {
            trafficLight.SetEmissionIntensity(originalEmissionIntensity);
        }
    }

    private IEnumerator SequenceRoutine()
    {
        while (true)
        {
            // Фаза 1: Автомобилям красный, пешеходам зеленый
            trafficLight.SetCarStop();
            trafficLight.SetPedestrianGo();

            if (useEmissionEffects)
            {
                // Плавное включение эмиссии для пешеходного зеленого
                yield return StartCoroutine(PulseEmission(trafficLight, 1.5f, 0.5f));
            }

            yield return new WaitForSeconds(pedestrianWalkDuration);

            // Мигание пешеходного зеленого с эффектом эмиссии
            yield return StartCoroutine(BlinkPedestrianBottomWithEmission(pedestrianBlinkDuration));

            // Фаза 2: Автомобилям желтый (предупреждение)
            trafficLight.SetCarReady();
            trafficLight.SetPedestrianStop();

            if (useEmissionEffects)
            {
                // Увеличиваем эмиссию желтого как предупреждение
                trafficLight.SetEmissionIntensity(warningEmissionIntensity);
                yield return StartCoroutine(PulseEmission(trafficLight, warningEmissionIntensity, yellowDuration));
            }
            else
            {
                yield return new WaitForSeconds(yellowDuration);
            }

            // Фаза 3: Автомобилям зеленый
            trafficLight.SetCarGo();
            trafficLight.SetPedestrianStop();

            if (useEmissionEffects)
            {
                // Возвращаем нормальную эмиссию
                trafficLight.SetEmissionIntensity(originalEmissionIntensity);
            }

            yield return new WaitForSeconds(greenDuration);

            // Фаза 4: Автомобилям желтый снова
            trafficLight.SetCarReady();

            if (useEmissionEffects)
            {
                // Снова увеличиваем эмиссию для предупреждения
                trafficLight.SetEmissionIntensity(warningEmissionIntensity);
                yield return StartCoroutine(PulseEmission(trafficLight, warningEmissionIntensity, yellowDuration));
            }
            else
            {
                yield return new WaitForSeconds(yellowDuration);
            }

            // Возвращаем нормальную эмиссию
            if (useEmissionEffects)
            {
                trafficLight.SetEmissionIntensity(originalEmissionIntensity);
            }
        }
    }

    private IEnumerator BlinkPedestrianBottomWithEmission(float duration)
    {
        float timer = 0f;
        bool isOn = true;
        float originalIntensity = trafficLight.GetCurrentEmissionIntensity();

        while (timer < duration)
        {
            trafficLight.SetPedestrianLights(false, isOn);

            if (useEmissionEffects && isOn)
            {
                // При мигании увеличиваем эмиссию для лучшей видимости
                trafficLight.SetEmissionIntensity(originalIntensity * 1.3f);
            }

            yield return new WaitForSeconds(blinkInterval);
            timer += blinkInterval;

            if (!isOn && useEmissionEffects)
            {
                // Восстанавливаем при выключении
                trafficLight.SetEmissionIntensity(originalIntensity);
            }

            isOn = !isOn;
        }

        // В конце выключаем и восстанавливаем
        trafficLight.SetPedestrianLights(false, false);
        trafficLight.SetEmissionIntensity(originalIntensity);
    }

    private IEnumerator PulseEmission(TrafficLightController light, float targetIntensity, float duration)
    {
        float startTime = Time.time;
        float startIntensity = light.GetCurrentEmissionIntensity();

        while (Time.time - startTime < duration)
        {
            float t = (Time.time - startTime) / duration;

            // Плавное изменение интенсивности
            float currentIntensity = Mathf.Lerp(startIntensity, targetIntensity,
                Mathf.SmoothStep(0f, 1f, t));

            light.SetEmissionIntensity(currentIntensity);
            yield return null;
        }

        // Гарантируем точное значение в конце
        light.SetEmissionIntensity(targetIntensity);
    }

    /// <summary>
    /// Экстренное мигание желтым
    /// </summary>
    public void EmergencyYellowBlink()
    {
        StartCoroutine(EmergencyBlinkRoutine());
    }

    private IEnumerator EmergencyBlinkRoutine()
    {
        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);

        float originalIntensity = trafficLight.GetCurrentEmissionIntensity();

        while (true)
        {
            trafficLight.SetCarLights(false, true, false);

            if (useEmissionEffects)
            {
                trafficLight.SetEmissionIntensity(warningEmissionIntensity * 1.5f);
            }

            yield return new WaitForSeconds(0.7f);

            trafficLight.SetCarLights(false, false, false);

            if (useEmissionEffects)
            {
                trafficLight.SetEmissionIntensity(originalIntensity);
            }

            yield return new WaitForSeconds(0.3f);
        }
    }

    private void OnDisable()
    {
        StopSequence();
    }
}