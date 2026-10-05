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

    public float acceleration = 22f;    // 가속
    public float deceleration = 28f;    // 감속
    [Range(0f, 1f)] public float airControl = 0.5f;     // 공중에서 방향을 바꾸는 힘

    [System.NonSerialized] public bool canMove = true;
    [System.NonSerialized] public bool canLook = true;
    [System.NonSerialized] public bool canJump = true;
    [System.NonSerialized] public bool canSprint = true;


    CharacterController controller;
    float pitch;                // 위아래 각도
    float verticalVelocity;     // 떨어지는 속도

    Vector3 horizontalVelocity; // 수평 속도

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

        // 입력 → 목표 속도
        Vector3 targetVelocity = Vector3.zero;
        if(Allowed(canMove, OptionType.Move))
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");

            Vector3 dir = transform.right * x + transform.forward * z;
            if(dir.sqrMagnitude > 1f) dir.Normalize();

            bool sprint = canSprint && Input.GetKey(KeyCode.LeftShift);
            targetVelocity = dir * (sprint ? sprintSpeed : moveSpeed);

            if (canJump && grounded && Input.GetButtonDown("Jump"))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

        }

        // 현재 속도가 목표 속도로 서서히 다가가게

        bool hasInput = targetVelocity.sqrMagnitude > 0.0001f;
        float rate = hasInput ? acceleration : deceleration;
        if (!grounded) rate *= airControl;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);

        // 중력, 이동
        verticalVelocity += gravity * Time.deltaTime;
        Vector3 motion = horizontalVelocity;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);

        // 머리를 부딪히면 상승 중단
        if ((controller.collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;



    }

    // 미니게임/연출 시작 시 미끄러짐 없이 즉시 멈추고 싶을 때 호출
    public void StopMovement(ControllerColliderHit hit)
    {
        if (Mathf.Abs(hit.normal.y) > 0.3f) return;    // 바닥/천장은 무시

        Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
        float into = Vector3.Dot(horizontalVelocity, n);
        if (into < 0f) horizontalVelocity -= n * into;
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

