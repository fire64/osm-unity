using UnityEngine;

public class CraneRotator : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 10f; // Скорость вращения
    [SerializeField] private float minAngle = -45f;      // Минимальный угол
    [SerializeField] private float maxAngle = 45f;       // Максимальный угол

    private float currentAngle = 0f;
    private int direction = 1; // 1 для увеличения угла, -1 для уменьшения

    void Update()
    {
        // Изменяем текущий угол с учетом направления и скорости
        currentAngle += direction * rotationSpeed * Time.deltaTime;

        // Если достигли границы, меняем направление
        if (currentAngle >= maxAngle)
        {
            currentAngle = maxAngle;
            direction = -1;
        }
        else if (currentAngle <= minAngle)
        {
            currentAngle = minAngle;
            direction = 1;
        }

        // Применяем вращение только по оси Y
        transform.rotation = Quaternion.Euler(0f, currentAngle, 0f);
    }
}