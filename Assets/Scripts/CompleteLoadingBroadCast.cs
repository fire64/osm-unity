using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CompleteLoadingBroadCast : MonoBehaviour
{
    // Статическое событие для уведомления о завершении загрузки
    public static event System.Action OnAllModulesLoaded;
    private static bool _isAllLoaded = false;

    // Статическое свойство для проверки состояния загрузки
    public static bool IsAllLoaded => _isAllLoaded;

    IEnumerator Start()
    {
        while (!IsAllModulesLoaded())
        {
            yield return null;
        }

        _isAllLoaded = true;
        Debug.Log("All modules loaded...");
        // Вызов события для всех подписчиков
        OnAllModulesLoaded?.Invoke();
    }

    private bool IsAllModulesLoaded()
    {
        var foundInfrstructureObjects = FindObjectsOfType<InfrstructureBehaviour>();

        foreach (InfrstructureBehaviour infrastructureObject in foundInfrstructureObjects)
        {
            if (!infrastructureObject.isFinished && infrastructureObject.isActiveAndEnabled)
            {
                return false;
            }
        }
        return true;
    }
}
