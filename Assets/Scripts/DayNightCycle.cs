using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TimeOfDay
{
    EDawnOrSunset,
    EDay,
    ENight
};

public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    public float fullCycleDuration = 120f;
    [Range(0, 24)] public float currentTime = 6f;

    [Header("References")]
    public Light sunLight;
    public Light moonLight;
    public Material skyboxMaterial;

    private float speedMultiplier = 1f;
    private float dayProgress = 0f;

    void Start()
    {
        if (sunLight == null) sunLight = RenderSettings.sun;
        if (moonLight == null)
        {
            GameObject moonObj = new GameObject("Moon");
            moonLight = moonObj.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = Color.gray;
        }
        UpdateTime();
    }

    void Update()
    {
        float timeToAdd = (24f / fullCycleDuration) * Time.deltaTime * speedMultiplier;
        currentTime += timeToAdd;
        if (currentTime >= 24f) currentTime -= 24f;

        UpdateTime();
    }

    void UpdateTime()
    {
        dayProgress = currentTime / 24f;
        float sunAngle = (dayProgress * 360f - 90f) % 360f;

        float sunY = sunAngle;
        sunLight.transform.localRotation = Quaternion.Euler(sunY, 0, 0);
        moonLight.transform.localRotation = Quaternion.Euler(sunY + 180f, 0, 0);

        UpdateLighting();
        UpdateSkybox();
    }

    void UpdateLighting()
    {
        float sunElevation = Mathf.Sin(Mathf.Deg2Rad * (float)(sunLight.transform.localRotation.eulerAngles.x));
        float sunIntensity = Mathf.Clamp01(sunElevation * 2f + 0.2f);
        float moonIntensity = Mathf.Clamp01((-sunElevation) * 0.5f + 0.1f);

        sunLight.intensity = sunIntensity;
        moonLight.intensity = moonIntensity;

        RenderSettings.ambientIntensity = Mathf.Max(sunIntensity, moonIntensity);
    }

    void UpdateSkybox()
    {
        if (skyboxMaterial != null)
        {
            skyboxMaterial.SetFloat("_DayProgress", dayProgress);
            skyboxMaterial.SetFloat("_SunIntensity", sunLight.intensity); // Передаём интенсивность солнца

            // Передаём направление солнца в шейдер
            Vector3 sunDirection = -sunLight.transform.forward; // Свет направлен в противоположную сторону от вектора вперёд
            Shader.SetGlobalVector("_SunDirection", sunDirection);
        }
    }

    public TimeOfDay GetTimeOfDay()
    {
        TimeOfDay timeOfDay = TimeOfDay.EDay;

        if (dayProgress > 0.75 || dayProgress < 0.25)
        {
            timeOfDay = TimeOfDay.ENight;
        }
        else if(dayProgress >= 0.25 && dayProgress < 0.35 || dayProgress > 0.65 && dayProgress <= 0.75)
        {
            timeOfDay = TimeOfDay.EDawnOrSunset;
        }

        return timeOfDay;
    }

    public void SetGameTime(float hour)
    {
        currentTime = Mathf.Clamp(hour, 0f, 24f);
    }

    public float GetGameTime()
    {
        return currentTime;
    }

    public void SetTimeSpeed(float speed)
    {
        speedMultiplier = Mathf.Max(0f, speed);
    }
}
