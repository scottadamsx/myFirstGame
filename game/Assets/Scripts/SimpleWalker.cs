using UnityEngine;
using UnityEngine.InputSystem;

/// Third-person controller: visible articulated body, orbit camera on a boom
/// (mouse), WASD/arrows move relative to the camera, Shift sprint, Space jump.
[RequireComponent(typeof(CharacterController))]
public class SimpleWalker : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float sprintSpeed = 12f;
    public float jumpVelocity = 6.5f;
    public float mouseSensitivity = 0.12f;
    public Transform cameraPivot;   // the Camera's transform (assigned by the scene builder)

    CharacterController controller;
    Transform boom;                 // orbit pivot at shoulder height
    Transform body;                 // visible person
    Vector3 spawnPosition;
    float yaw, pitch = 14f, verticalVelocity;
    const float CamDistance = 4.4f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        spawnPosition = transform.position;
        yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;

        if (cameraPivot == null && Camera.main != null)
            cameraPivot = Camera.main.transform;

        boom = new GameObject("CameraBoom").transform;
        boom.SetParent(transform, false);
        boom.localPosition = new Vector3(0, 1.6f, 0);
        if (cameraPivot != null)
        {
            cameraPivot.SetParent(boom, false);
            cameraPivot.localPosition = new Vector3(0, 0, -CamDistance);
            cameraPivot.localRotation = Quaternion.identity;
        }

        // the player: green jacket, red toque — dressed for the hill
        var person = ArticulatedPerson.Build(
            new Color(0.16f, 0.45f, 0.30f),
            new Color(0.22f, 0.22f, 0.26f),
            new Color(0.85f, 0.66f, 0.50f),
            new Color(0.20f, 0.15f, 0.10f),
            0, true, new Color(0.80f, 0.28f, 0.20f));
        body = person.transform;
        body.SetParent(transform, false);
        body.localPosition = new Vector3(0, 0.02f, 0);
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
            pitch = Mathf.Clamp(pitch - look.y, -28f, 65f);
        }
        boom.rotation = Quaternion.Euler(pitch, yaw, 0);

        // camera boom collision: pull in when something's between pivot and camera
        if (cameraPivot != null)
        {
            Vector3 back = boom.rotation * Vector3.back;
            float dist = CamDistance;
            if (Physics.Raycast(boom.position, back, out var hit, CamDistance,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.6f, hit.distance - 0.25f);
            cameraPivot.localPosition = new Vector3(0, 0, -dist);
        }

        float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
        float z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
        float speed = kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
        Vector3 move = Quaternion.Euler(0, yaw, 0) * new Vector3(x, 0, z);
        move = move.sqrMagnitude > 1f ? move.normalized : move;
        move *= speed;

        // body faces where it's going
        Vector3 flat = new Vector3(move.x, 0, move.z);
        if (body != null && flat.sqrMagnitude > 0.5f)
            body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);

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
