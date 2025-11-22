using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

[System.Serializable]
public class EmissiveMaterial
{
    public Material material;
    public Color emissionColor = Color.white;
    public float emissionIntensity = 1f;
}

public class DayNightAtmospheric : MonoBehaviour
{
    public PostProcessVolume postProcessVolume;
    public ColorGrading colorGrading;
    public Bloom bloom;

    public DayNightCycle dayNightCycle;

    public TimeOfDay TimeOfDay;

    [Header("Emissive Materials")]
    public List<EmissiveMaterial> emissiveMaterials = new List<EmissiveMaterial>();

    // Update is called once per frame
    void Update()
    {
        TimeOfDay = dayNightCycle.GetTimeOfDay();

        UpdateFog();
        UpdateEnvironment();
        UpdateDayNightVisuals();
    }

    void UpdateFog()
    {
        float fogDensity = 0.01f;
        if(TimeOfDay == TimeOfDay.ENight) // ночь
        {
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.1f);
            RenderSettings.fogDensity = fogDensity * 1.5f;
        }
        else if (TimeOfDay == TimeOfDay.EDawnOrSunset) // рассвет/закат
        {
            RenderSettings.fogColor = new Color(0.8f, 0.5f, 0.3f);
            RenderSettings.fogDensity = fogDensity * 1.2f;
        }
        else // день
        {
            RenderSettings.fogColor = new Color(0.8f, 0.9f, 1.0f);
            RenderSettings.fogDensity = fogDensity;
        }
    }

    void UpdateEnvironment()
    {
        if (TimeOfDay == TimeOfDay.ENight) // ночь
        {
            foreach (var emissiveMat in emissiveMaterials)
            {
                if (emissiveMat.material != null)
                {
                    emissiveMat.material.EnableKeyword("_EMISSION");
                    emissiveMat.material.SetColor("_EmissionColor", emissiveMat.emissionColor * emissiveMat.emissionIntensity);
                }
            }
        }
        else // день или рассвет/закат
        {
            foreach (var emissiveMat in emissiveMaterials)
            {
                if (emissiveMat.material != null)
                {
                    emissiveMat.material.DisableKeyword("_EMISSION");
                    emissiveMat.material.SetColor("_EmissionColor", Color.black);
                }
            }
        }
    }

    void UpdateDayNightVisuals()
    {
        if (postProcessVolume != null)
        {
            bloom = postProcessVolume.profile.GetSetting<Bloom>();
            colorGrading = postProcessVolume.profile.GetSetting<ColorGrading>();

            // Пример изменения цветокоррекции
            if (TimeOfDay == TimeOfDay.ENight) // ночь
            {
                colorGrading.temperature.value = -10f;
                colorGrading.saturation.value = -20f;
                bloom.intensity.value = 0.3f;
            }
            else if (TimeOfDay == TimeOfDay.EDawnOrSunset) // рассвет/закат
            {
                colorGrading.temperature.value = 20f;
                colorGrading.saturation.value = 15f;
                bloom.intensity.value = 0.8f;
            }
            else // день
            {
                colorGrading.temperature.value = 0f;
                colorGrading.saturation.value = 10f;
                bloom.intensity.value = 0.5f;
            }
        }
    }
}
