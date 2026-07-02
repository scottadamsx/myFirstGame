using UnityEngine;
using UnityEngine.InputSystem;

/// First-person walker: WASD + mouse look, Shift to sprint, Space to jump,
/// Esc to release the cursor (click to grab it back).
[RequireComponent(typeof(CharacterController))]
public class SimpleWalker : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float sprintSpeed = 12f;
    public float jumpVelocity = 6.5f;
    public float mouseSensitivity = 0.08f;
    public Transform cameraPivot;

    CharacterController controller;
    Vector3 spawnPosition;
    float yaw, pitch, verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        spawnPosition = transform.position;
        yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
            Cursor.lockState = CursorLockMode.None;
        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            Cursor.lockState = CursorLockMode.Locked;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
        float z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
        float speed = kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
        Vector3 move = (transform.right * x + transform.forward * z).normalized * speed;

        if (controller.isGrounded)
        {
            verticalVelocity = -2f;
            if (kb.spaceKey.wasPressedThisFrame)
                verticalVelocity = jumpVelocity;
        }
        else
        {
            verticalVelocity -= 20f * Time.deltaTime;
        }

        controller.Move((move + Vector3.up * verticalVelocity) * Time.deltaTime);

        if (transform.position.y < -60f)   // fell in the harbour
        {
            controller.enabled = false;
            transform.position = spawnPosition;
            controller.enabled = true;
            verticalVelocity = 0;
        }
    }
}
