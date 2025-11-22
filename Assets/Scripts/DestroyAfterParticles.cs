using UnityEngine;
using System.Collections;

public class DestroyAfterParticles : MonoBehaviour
{
    [Header("Particle System Settings")]
    public bool destroyImmediatelyIfNoParticles = true;
    public float additionalDelay = 0f;

    private ParticleSystem[] particleSystems;
    private bool hasStartedChecking = false;

    void Start()
    {
        // Получаем все системы частиц на этом объекте и его дочерних объектах
        particleSystems = GetComponentsInChildren<ParticleSystem>();

        // Если нет систем частиц и нужно уничтожить сразу
        if (particleSystems.Length == 0 && destroyImmediatelyIfNoParticles)
        {
            Destroy(gameObject);
            return;
        }

        // Запускаем проверку
        if (!hasStartedChecking)
        {
            StartCoroutine(CheckAndDestroy());
        }
    }

    IEnumerator CheckAndDestroy()
    {
        hasStartedChecking = true;

        // Ждем один кадр, чтобы частицы начали проигрываться
        yield return null;

        // Ждем, пока все системы частиц не завершатся
        bool anyAlive = true;
        while (anyAlive)
        {
            anyAlive = false;

            foreach (ParticleSystem ps in particleSystems)
            {
                if (ps != null && ps.IsAlive())
                {
                    anyAlive = true;
                    break;
                }
            }

            yield return new WaitForSeconds(0.1f); // Проверяем каждые 0.1 секунды
        }

        // Добавляем дополнительную задержку, если нужно
        if (additionalDelay > 0)
        {
            yield return new WaitForSeconds(additionalDelay);
        }

        // Удаляем объект
        Destroy(gameObject);
    }

    // Метод для принудительной проверки (можно вызвать извне)
    public void ForceCheck()
    {
        if (!hasStartedChecking)
        {
            StartCoroutine(CheckAndDestroy());
        }
    }

    // Метод для немедленного уничтожения
    public void DestroyNow()
    {
        StopAllCoroutines();
        Destroy(gameObject);
    }
}