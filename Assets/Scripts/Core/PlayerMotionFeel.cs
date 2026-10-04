using System;
using UnityEngine;

// 걷는 느낌: 걸을 때 카메라가 살짝 출렁이고, 한 걸음마다 OnFootstep 이벤트를 보낸다.
// Player(CharacterController가 있는 오브젝트)에 붙인다.
public class PlayerMotionFeel : MonoBehaviour
{
    public Transform cameraRoot;          // PlayerCameraRoot
    public float bobHeight = 0.03f;       // 위아래 흔들림 (m)
    public float bobSide = 0.015f;        // 좌우 흔들림 (m)
    public float stepDistance = 1.6f;     // 한 걸음 거리 (m)
    public bool logFootstep;              // 테스트용: 걸음마다 Console에 출력

    public event Action OnFootstep;                  
    public float BobAmount { get; private set; }     // 0~1, 흔들림 세기 (손 흔들림에서 사용)
    public float Phase { get; private set; }         // 걸음 진행 (손 흔들림에서 사용)

    CharacterController controller;
    Vector3 restPos;
    int lastHalf;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        restPos = cameraRoot.localPosition;
    }

    void LateUpdate()
    {
        Vector3 v = controller.velocity;
        float speed = new Vector2(v.x, v.z).magnitude;
        bool moving = controller.isGrounded && speed > 0.1f;

        // 걷기 시작/멈춤에 따라 흔들림이 서서히 켜지고 꺼짐
        BobAmount = Mathf.MoveTowards(BobAmount, moving ? 1f : 0f, 6f * Time.deltaTime);

        if (moving)
        {
            // 한 걸음 = Phase가 π만큼 진행
            Phase = Mathf.Repeat(Phase + speed / stepDistance * Mathf.PI * Time.deltaTime, Mathf.PI * 2f);

            int half = Phase >= Mathf.PI ? 1 : 0;
            if (half != lastHalf)
            {
                lastHalf = half;
                if (logFootstep) Debug.Log("Footstep");
                OnFootstep?.Invoke();
            }
        }

        // 위아래: 발 디딜 때 가장 낮음 / 좌우: 두 걸음에 한 번 왕복
        float y = (Mathf.Abs(Mathf.Sin(Phase)) - 0.64f) * bobHeight;
        float x = Mathf.Cos(Phase) * bobSide;
        cameraRoot.localPosition = restPos + new Vector3(x, y, 0f) * BobAmount;
    }
}