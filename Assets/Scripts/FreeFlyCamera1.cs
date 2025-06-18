using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeFlyCamera1 : MonoBehaviour
{
    public float baseSpeed = 5.0f;
    public float lookSpeed = 2.0f;
    public float sprintMultiplier = 3.0f;

    private float rotationX = 0.0f;
    private float rotationY = 0.0f;
    private float currentSpeed;

    void Start()
    {
        LockCursor(true);
    }

    void Update()
    {
        HandleMouseLook();
        HandleKeyboardMovement();
        HandleCursorToggle();
    }

    void HandleMouseLook()
    {
        rotationY += Input.GetAxis("Mouse X") * lookSpeed;
        rotationX -= Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f);

        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);
    }

    void HandleKeyboardMovement()
    {
        Vector3 moveDirection = new Vector3(
            Input.GetAxis("Horizontal"),
            GetVerticalInput(),
            Input.GetAxis("Vertical")
        );

        currentSpeed = Input.GetKey(KeyCode.LeftShift) ?
            baseSpeed * sprintMultiplier :
            baseSpeed;

        transform.Translate(moveDirection.normalized * currentSpeed * Time.deltaTime);
    }

    float GetVerticalInput()
    {
        if (Input.GetKey(KeyCode.Space)) return 1.0f;
        if (Input.GetKey(KeyCode.LeftControl)) return -1.0f;
        return 0.0f;
    }

    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(Cursor.lockState != CursorLockMode.Locked);
        }
    }

    void LockCursor(bool shouldLock)
    {
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }
}
