using UnityEngine;

public class CraneRotator : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 10f; // �������� ��������
    [SerializeField] private float minAngle = -45f;      // ����������� ����
    [SerializeField] private float maxAngle = 45f;       // ������������ ����

    private float currentAngle = 0f;
    private int direction = 1; // 1 ��� ���������� ����, -1 ��� ����������

    void Update()
    {
        // �������� ������� ���� � ������ ����������� � ��������
        currentAngle += direction * rotationSpeed * Time.deltaTime;

        // ���� �������� �������, ������ �����������
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

        // ��������� �������� ������ �� ��� Y
        transform.rotation = Quaternion.Euler(0f, currentAngle, 0f);
    }
}