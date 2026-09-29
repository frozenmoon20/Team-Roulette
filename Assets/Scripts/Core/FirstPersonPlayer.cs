using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonPlayer : MonoBehaviour
{
    public Transform cameraRoot;            // PlayerCameraRoot (카메라의 부모)

    public float moveSpeed = 4f;
    public float sprintSpeed = 6f;
    public float jumpHeight = 1.2f;
    public float gravity = -15f;

    public float mouseSensitivity = 2f;
    public float maxPitch = 89f;            // 위아래로 볼 수 있는 최대 각도

    [System.NonSerialized] public bool canMove = true;
    [System.NonSerialized] public bool canLook = true;
    [System.NonSerialized] public bool canJump = true;
    [System.NonSerialized] public bool canSprint = true;


    CharacterController controller;
    float pitch;                // 위아래 각도
    float verticalVelocity;     // 떨어지는 속도

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        LockCursor(true);
    }

    void Update()
    {
        HandleCursor();
        Look();
        Move();
    }

    void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
        else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
    }

    void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (!Allowed(canLook, OptionType.RotateCamera)) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mx, 0f);                                   // 좌우는 몸 전체
        pitch = Mathf.Clamp(pitch - my, -maxPitch, maxPitch);
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);     // 위아래는 머리만
    }

    void Move()
    {
        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;  // 바닥에 붙어 있게

        Vector3 move = Vector3.zero;
        if (Allowed(canMove, OptionType.Move))
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            move = transform.right * x + transform.forward * z;
            if (move.sqrMagnitude > 1f) move.Normalize();               // 대각선이 더 빠르지 않게

            bool sprint = canSprint && Input.GetKey(KeyCode.LeftShift);
            move *= sprint ? sprintSpeed : moveSpeed;

            if (canJump && grounded && Input.GetButtonDown("Jump"))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    // 스위치가 켜져 있고, OptionState에서 해금되어 있어야 허용
    bool Allowed(bool flag, OptionType type)
    {
        return flag && OptionState.Instance != null && OptionState.Instance.IsUnlocked(type);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}