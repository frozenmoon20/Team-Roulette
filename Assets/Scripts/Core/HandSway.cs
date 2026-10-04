using UnityEngine;

// 손 흔들림: 걸을 때 걸음에 맞춰 살짝 흔들리고, 시점을 돌리면 손이 살짝 늦게 따라온다.
// HandRoot에 붙인다.
public class HandSway : MonoBehaviour
{
    [Header("걸을 때")]
    public float walkHeight = 0.008f;   // 위아래 (m)
    public float walkSide = 0.006f;     // 좌우 (m)

    [Header("시점 돌릴 때")]
    public float lookAmount = 0.3f;     // 돌린 각도의 몇 배만큼 손이 밀릴지
    public float maxLookAngle = 6f;     // 최대로 밀리는 각도
    public float returnSpeed = 8f;      // 제자리로 돌아오는 빠르기

    // 숨쉬는 애니메이션 구현을 위한 변수목록
    [Header("숨쉬기")]
    public float breathHeight = 0.004f;   // 숨쉴 때 오르내림 (m)
    public float breathSpeed = 0.25f;     // 1초에 몇 번 (0.25 = 4초에 한 번)
    public float driftAngle = 1.5f;       // 손목이 저절로 흔들리는 각도
    public float driftSpeed = 0.3f;       // 흔들림 빠르기

    PlayerMotionFeel motion;
    Vector3 restPos;
    Quaternion restRot;
    Quaternion lastCamRot;
    Vector2 sway;                       // x = 위아래 밀림, y = 좌우 밀림

    void Awake()
    {
        motion = GetComponentInParent<PlayerMotionFeel>();   // 없으면 걸음 흔들림만 빠짐
        restPos = transform.localPosition;
        restRot = transform.localRotation;
    }

    void Start()
    {
        lastCamRot = transform.parent.rotation;
    }

    void LateUpdate()
    {
        // 1) 시점 회전: 이번 프레임에 카메라가 돈 만큼 손을 반대로 밀고, 서서히 복귀
        Quaternion camRot = transform.parent.rotation;
        Vector3 d = (Quaternion.Inverse(lastCamRot) * camRot).eulerAngles;
        lastCamRot = camRot;

        sway.x -= Mathf.DeltaAngle(0f, d.x) * lookAmount;
        sway.y -= Mathf.DeltaAngle(0f, d.y) * lookAmount;
        sway = Vector2.ClampMagnitude(sway, maxLookAngle);
        sway = Vector2.Lerp(sway, Vector2.zero, 1f - Mathf.Exp(-returnSpeed * Time.deltaTime));

        // 2) 걸음: PlayerMotionFeel의 걸음 박자에 맞춰 흔들림
        Vector3 bob = Vector3.zero;
        if (motion != null)
        {
            float p = motion.Phase;
            bob = new Vector3(Mathf.Cos(p) * walkSide,
                              (Mathf.Abs(Mathf.Sin(p)) - 0.64f) * walkHeight,
                              0f) * motion.BobAmount;
        }

        // 3) 숨쉬기: 천천히 오르내림 / 손목: 불규칙하게 미세하게 흔들림
        float t = Time.time;
        Vector3 breath = new Vector3(0f, Mathf.Sin(t * breathSpeed * Mathf.PI * 2f) * breathHeight, 0f);
        float driftX = (Mathf.PerlinNoise(t * driftSpeed, 0f) - 0.5f) * 2f * driftAngle;
        float driftY = (Mathf.PerlinNoise(0f, t * driftSpeed) - 0.5f) * 2f * driftAngle;

        transform.localPosition = restPos + bob + breath;
        transform.localRotation = restRot * Quaternion.Euler(sway.x + driftX, sway.y + driftY, 0f);
    }
}