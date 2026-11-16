using System.Collections;
using UnityEngine;

// 꿈(미니게임2)에서 깨어날 때 침대에 "누운" 시점으로 시작하고, 이동이 해금되면(강아지 계단) 침대에서 일어나 옆에 서게 한다.
// 원리:
//   - 1인칭이라 몸은 안 보이므로 몸을 실제로 눕히지 않고 "눈 높이와 시선"만 누운 자리로 옮긴다.
//     몸은 Lie View 바로 아래(침대 위)에 세우고, 눈(PlayerCameraRoot)을 Lie View 높이까지 내리고, 고개를 Lie View 방향으로.
//     눈이 몸 바로 위에 있으므로 카메라 회전이 해금된 뒤 고개를 돌려도 눈 자리는 그대로다.
//   - PlayerMotionFeel(걸음 흔들림)은 매 프레임 눈 높이를 원래(1.4)로 되돌려 버리므로, 누워 있는 동안과 일어나는 동안은 꺼 둔다.
//   - 일어나기: OptionState에서 Move가 해금되는 순간(강아지 계단 = MoveTrigger) 자동으로 시작한다. (MoveTrigger는 안 고침)
//       1) 상체 일으키기: 눈이 조금 올라가며 몸이 Stand Point 쪽으로 돌고, 고개는 살짝 아래(발밑)로
//       2) 침대에서 내려서기: 몸이 Stand Point로 옮겨 가며 눈이 원래 높이로, 고개는 정면으로
//     그동안은 조작을 막고(FirstPersonPlayer를 잠깐 끔), 끝나면 돌려준다.
// 사용: 침실 씬에 빈 오브젝트 "BedLie"를 만들어 붙이고 칸을 채운 뒤, Minigame2의 On Bed Arrive → BedLie.LieDown 연결.
//   Lie View: 베개 위 눈 자리. Scene 창에서 누운 구도(천장 + 창문)로 화면을 맞추고, 이 오브젝트를 선택한 채
//             GameObject > Align With View (Ctrl+Shift+F) → Scene 창 시점이 그대로 이 오브젝트의 자리·방향이 됨
//   Stand Point: 침대 옆 바닥, 일어난 뒤 설 자리 (파란 화살표 = 바라볼 방향, 높이는 상관없음 → 바닥을 자동으로 찾음)
public class BedLie : MonoBehaviour
{
    public Transform lieView;                // 누웠을 때 눈 자리·방향 (베개 위 빈 오브젝트)
    public Transform standPoint;             // 일어난 뒤 설 자리 (침대 옆 바닥 빈 오브젝트)

    [Header("일어나기")]
    public float sitRise = 0.35f;            // 1) 상체를 일으킬 때 눈이 올라가는 높이 (m)
    public float sitPitch = 15f;             // 1) 상체를 일으킬 때 고개 각도 (+ = 아래, 발밑을 보는 느낌)
    public float sitTime = 0.8f;             // 1) 상체 일으키는 시간 (초)
    public float standTime = 1.2f;           // 2) 침대에서 내려서는 시간 (초)

    FirstPersonPlayer fpp;
    CharacterController cc;
    PlayerMotionFeel feel;
    Vector3 restCamPos;                      // 원래 눈 자리 (PlayerCameraRoot의 Position, 보통 0, 1.4, 0)
    bool lying, busy;

    void OnEnable() { OptionState.OnOptionChanged += OnOptionChanged; }
    void OnDisable() { OptionState.OnOptionChanged -= OnOptionChanged; }

    // 누운 시점으로 (Minigame2의 On Bed Arrive에 연결)
    public void LieDown()
    {
        if (lieView == null || !FindPlayer()) return;
        // 이미 이동이 해금돼 있으면(테스트 건너뛰기 등) 눕지 않음 → 일어날 계기가 없어서 갇히지 않게
        if (OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Move)) return;

        if (feel != null) feel.enabled = false;
        Transform root = fpp.cameraRoot;
        if (!lying) restCamPos = root.localPosition;

        // 몸은 Lie View 바로 아래 (침대 위 또는 바닥 위)에 세운다. 바닥 찾기 전에 몸 충돌을 꺼야 자기 몸에 걸리지 않음
        if (cc != null) cc.enabled = false;
        Vector3 eye = lieView.position;
        Vector3 bodyPos = new Vector3(eye.x, BodyY(eye, eye.y - restCamPos.y), eye.z);
        float yaw = lieView.eulerAngles.y;
        float pitch = Mathf.DeltaAngle(0f, lieView.eulerAngles.x);
        fpp.Teleport(bodyPos, Quaternion.Euler(0f, yaw, 0f), Quaternion.Euler(pitch, 0f, 0f));   // 몸·고개 각도를 한 번에

        // 눈을 Lie View 높이까지 내림
        root.localPosition = new Vector3(restCamPos.x, eye.y - bodyPos.y, restCamPos.z);

        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();
        lying = true;
    }

    // 이동이 해금되면 일어나기 (강아지 계단 = MoveTrigger가 Move를 해금)
    void OnOptionChanged(OptionType type, bool unlocked)
    {
        if (type == OptionType.Move && unlocked && lying && !busy) StartCoroutine(GetUp());
    }

    IEnumerator GetUp()
    {
        busy = true;
        fpp.StopMovement();
        fpp.enabled = false;                     // 연출 동안 조작·중력 멈춤 (몸을 직접 옮기므로)
        if (cc != null) cc.enabled = false;
        Transform body = fpp.transform;

        // 1) 상체 일으키기: 눈이 조금 올라가며 Stand Point 쪽으로 몸을 돌림
        Vector3 dir = standPoint != null ? standPoint.position - body.position : body.forward;
        dir.y = 0f;
        float sitYaw = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir).eulerAngles.y : body.eulerAngles.y;
        yield return MoveBody(body.position, sitYaw, sitPitch, fpp.cameraRoot.localPosition.y + sitRise, sitTime);

        // 2) 침대에서 내려서기: Stand Point로 옮겨 가며 눈은 원래 높이, 고개는 정면
        Vector3 to = body.position;
        float toYaw = sitYaw;
        if (standPoint != null)
        {
            to = standPoint.position;
            to.y = BodyY(standPoint.position + Vector3.up * 1.5f, standPoint.position.y);
            toYaw = standPoint.eulerAngles.y;
        }
        yield return MoveBody(to, toYaw, 0f, restCamPos.y, standTime);

        // 끝: 원래대로
        fpp.cameraRoot.localPosition = restCamPos;
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();
        if (feel != null) feel.enabled = true;
        fpp.enabled = true;
        lying = false;
        busy = false;
    }

    // 몸 자리, 몸 방향(좌우), 고개 각도(위아래), 눈 높이를 함께 천천히
    IEnumerator MoveBody(Vector3 toPos, float toYaw, float toPitch, float toCamY, float time)
    {
        Transform body = fpp.transform;
        Transform root = fpp.cameraRoot;
        Vector3 fromPos = body.position;
        float fromYaw = body.eulerAngles.y;
        float fromPitch = fpp.Pitch;
        float fromCamY = root.localPosition.y;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            body.position = Vector3.Lerp(fromPos, toPos, k);
            body.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(fromYaw, toYaw, k), 0f);
            fpp.SetPitch(Mathf.Lerp(fromPitch, toPitch, k));
            Vector3 lp = root.localPosition;
            lp.y = Mathf.Lerp(fromCamY, toCamY, k);
            root.localPosition = lp;
            yield return null;
        }
        body.position = toPos;
        body.rotation = Quaternion.Euler(0f, toYaw, 0f);
        fpp.SetPitch(toPitch);
        Vector3 end = root.localPosition;
        end.y = toCamY;
        root.localPosition = end;
    }

    // from 바로 아래 바닥(침대 윗면 포함)을 찾아, 발이 그 위에 닿는 몸 높이를 돌려준다 (못 찾으면 fallback)
    float BodyY(Vector3 from, float fallback)
    {
        if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            return fallback;
        float feet = cc != null ? cc.center.y - cc.height * 0.5f : 0f;   // 몸 기준점에서 발바닥까지
        return hit.point.y - feet + 0.02f;
    }

    bool FindPlayer()
    {
        if (fpp == null) fpp = FindFirstObjectByType<FirstPersonPlayer>();
        if (fpp == null) return false;
        cc = fpp.GetComponent<CharacterController>();
        if (feel == null) feel = fpp.GetComponentInChildren<PlayerMotionFeel>();
        return true;
    }
}