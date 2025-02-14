using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeFlyCamera : MonoBehaviour
{
    public float movementSpeed = 10f;
    public float mouseSensitivity = 2f;
    public float fastMovementMultiplier = 5f;

    private float yaw = 0f;
    private float pitch = 0f;

    void Update()
    {
        // Управление движением
        float speedMultiplier = Input.GetKey(KeyCode.LeftShift) ? fastMovementMultiplier : 1f;

        Vector3 movement = new Vector3();

        if (Input.GetKey(KeyCode.W))
            movement += transform.forward;
        if (Input.GetKey(KeyCode.S))
            movement -= transform.forward;
        if (Input.GetKey(KeyCode.A))
            movement -= transform.right;
        if (Input.GetKey(KeyCode.D))
            movement += transform.right;
        if (Input.GetKey(KeyCode.Space))
            movement += transform.up;
        if (Input.GetKey(KeyCode.LeftControl))
            movement -= transform.up;


        transform.position += movement * movementSpeed * speedMultiplier * Time.deltaTime;


        // Управление вращением мышью
        if (Input.GetMouseButton(1)) // Правая кнопка мыши
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Ограничение вращения по вертикали
            pitch = Mathf.Clamp(pitch, -90f, 90f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Альтернативное управление вращением с помощью стрелок (если нужно)
        //if (Input.GetKey(KeyCode.UpArrow))
        //    pitch -= mouseSensitivity * Time.deltaTime;
        //if (Input.GetKey(KeyCode.DownArrow))
        //    pitch += mouseSensitivity * Time.deltaTime;
        //if (Input.GetKey(KeyCode.LeftArrow))
        //    yaw -= mouseSensitivity * Time.deltaTime;
        //if (Input.GetKey(KeyCode.RightArrow))
        //    yaw += mouseSensitivity * Time.deltaTime;

        //transform.rotation = Quaternion.Euler(pitch, yaw, 0f); //  Если используется управление стрелками
    }
}
