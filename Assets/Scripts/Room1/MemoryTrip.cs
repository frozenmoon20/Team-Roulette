using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 회상(기억) 속으로 들어갔다가 돌아오기 — 미니게임5(겨울이 허들 훈련)의 앞뒤 연출 담당
// Enter(): 화면이 하얗게 덮임 → 그 사이 플레이어를 기억 공간으로 순간이동, 하프톤을 깨끗하게(기억 속은 선명),
//          기억 속에서만 점프 허용(FirstPersonPlayer.memoryJump) → 하얀 화면이 걷힘 → onEntered (허들 훈련 시작)
// Restart(): (허들 훈련 실패 시) 짧게 하얗게 깜빡이며 기억 속 처음 자리로
// Exit():  화면이 하얗게 덮임 → 원래 서 있던 자리로 순간이동, 하프톤 원래대로, 점프 허용 끔
//          → 하얀 화면이 걷힘 → 독백(returnDialogue) → onFinished (점프 해금, 목표 완료 등)
// 원리: 미니게임2(꿈 공간)와 같은 방식. 기억 공간은 같은 씬의 멀리 떨어진 곳에 만들어 두고 평소엔 꺼 둔다.
//       하얀 화면은 화면 UI(Screen Space - Overlay)라서 하프톤에 묻히지 않고 진짜 하얗게 보인다.
// 사용: 빈 오브젝트 "MemoryTrip"에 붙이고 칸을 채운다. 회상 오브젝트(DialogueInteractable)의 On First Finished에 Enter()를 연결.
public class MemoryTrip : MonoBehaviour
{
    [Header("연결")]
    public GameObject memorySpace;           // 기억 공간 전체 (평소엔 꺼 둠)
    public Transform restartPoint;           // 실패해서 처음부터 다시 할 때 눈이 갈 자리 (비우면 Memory Start). 첫 장면이 앉은 자세라면 서 있는 자리를 넣는다
    public Transform memoryStart;            // 기억 공간에서 "눈"이 갈 자리 + 바라볼 방향 (Scene 창에서 Align With View로 맞추면 그 화면 그대로 시작)
    public Graphic whiteScreen;              // 화면 전체를 덮는 하얀 Image (Screen Space - Overlay 캔버스 안, 처음엔 투명)

    [Header("시간 (초)")]
    public float fadeInTime = 1.5f;          // 하얗게 덮이는 시간
    public float holdTime = 0.8f;            // 완전히 하얀 채로 머무는 시간 (이때 순간이동)
    public float fadeOutTime = 2f;           // 하얀 화면이 걷히는 시간
    public float restartFadeTime = 0.6f;     // 실패해서 처음 자리로 돌아갈 때 하얗게 깜빡이는 시간

    [Header("하프톤 (0 = 가장 짙음, 1 = 깨끗함)")]
    [Range(0f, 1f)] public float memoryHalftone = 1f;   // 기억 속: 선명하게

    [Header("돌아온 뒤")]
    public DialogueStep returnDialogue;      // 깨어난 뒤 독백 ("…겨울이한테 뛰는 법을 알려 준 건 나였는데.")

    [Header("들어갈 때")]
    public bool releasePlayerOnEnter = true; // 체크: 하얀 화면이 걷히면 바로 조작 가능 / 해제: 연출 스크립트(MemorySequence)가 풀어 줄 때까지 잠금

    [Header("이벤트")]
    public UnityEvent onWhite;               // 들어갈 때 화면이 완전히 하얀 순간 (순간이동 직후). 첫 장면 준비 (손을 겨울이 위에 올리기 등)
    public UnityEvent onEntered;             // 기억 공간에 들어와 하얀 화면이 다 걷힌 뒤
    public UnityEvent onExitWhite;           // 나올 때 화면이 완전히 하얀 순간 (순간이동 직전). 손을 원래대로 돌려놓기 등
    public UnityEvent onFinished;            // 돌아와서 독백까지 끝난 뒤 (OptionUnlocker.Unlock = Jump 등)

    public bool InMemory { get; private set; }
    public bool Busy => busy;                // 하얘지거나 걷히는 중이면 true

    Transform player;
    CharacterController cc;
    FirstPersonPlayer fpp;
    Vector3 homePos;                         // 들어가기 전 서 있던 자리
    Quaternion homeRot;
    float homePitch;                         // 들어가기 전 고개 위아래 각도
    bool busy;

    void Awake()
    {
        if (whiteScreen != null) SetWhite(0f);
        if (memorySpace != null) memorySpace.SetActive(false);
    }

    [ContextMenu("테스트: 들어가기 (Enter)")]
    public void Enter()
    {
        if (busy || InMemory) return;
        if (!FindPlayer()) return;
        StartCoroutine(EnterRoutine());
    }

    [ContextMenu("테스트: 나오기 (Exit)")]
    public void Exit()
    {
        if (busy || !InMemory) return;
        StartCoroutine(ExitRoutine());
    }

    IEnumerator EnterRoutine()
    {
        busy = true;
        fpp.enabled = false;                 // 하얘지는 동안엔 못 움직이게
        fpp.StopMovement();

        yield return Fade(0f, 1f, fadeInTime);
        yield return new WaitForSeconds(holdTime * 0.5f);

        // 완전히 하얀 동안: 순간이동 + 기억 공간 켜기 + 하프톤 깨끗하게 + 점프 허용
        homePos = player.position;
        homeRot = player.rotation;
        homePitch = fpp.Pitch;
        if (memorySpace != null) memorySpace.SetActive(true);
        PlaceAtStart();
        HalftoneController.SetOverride(memoryHalftone);
        FirstPersonPlayer.memoryJump = true;
        InMemory = true;
        onWhite.Invoke();                    // 화면이 하얀 동안 첫 장면 준비 (보이기 전에 끝나야 어색하지 않음)

        yield return new WaitForSeconds(holdTime * 0.5f);
        yield return Fade(1f, 0f, fadeOutTime);

        if (releasePlayerOnEnter) fpp.enabled = true;
        busy = false;
        onEntered.Invoke();
    }

    IEnumerator ExitRoutine()
    {
        busy = true;
        fpp.enabled = false;
        fpp.StopMovement();

        yield return Fade(0f, 1f, fadeInTime);
        yield return new WaitForSeconds(holdTime * 0.5f);

        // 완전히 하얀 동안: 원래 자리로 + 기억 공간 끄기 + 하프톤 원래대로 + 점프 허용 끄기
        onExitWhite.Invoke();
        Teleport(homePos, homeRot);
        fpp.SetPitch(homePitch);             // 고개도 들어가기 전 각도로
        if (memorySpace != null) memorySpace.SetActive(false);
        HalftoneController.ClearOverride();
        FirstPersonPlayer.memoryJump = false;
        InMemory = false;

        yield return new WaitForSeconds(holdTime * 0.5f);
        yield return Fade(1f, 0f, fadeOutTime);

        fpp.enabled = true;

        // 독백이 끝날 때까지 기다린 뒤 해금 등
        if (returnDialogue != null)
        {
            returnDialogue.Play();
            while (returnDialogue.IsPlaying) yield return null;
        }

        busy = false;
        onFinished.Invoke();
    }

    // 기억 속에서 처음 자리로 되돌리기 (허들 훈련 실패 시). 화면이 짧게 하얗게 깜빡이는 동안 옮긴다.
    // atWhite: 화면이 완전히 하얀 순간에 실행할 일 (겨울이를 처음 자리로 등)
    public void Restart(System.Action atWhite = null)
    {
        if (busy || !InMemory) return;
        StartCoroutine(RestartRoutine(atWhite));
    }

    IEnumerator RestartRoutine(System.Action atWhite)
    {
        busy = true;
        fpp.enabled = false;
        fpp.StopMovement();

        yield return Fade(0f, 1f, restartFadeTime);
        PlaceAt(restartPoint != null ? restartPoint : memoryStart, true);   // 다시 할 때는 서 있는 자세로, 발을 땅 위에
        if (atWhite != null) atWhite();
        yield return new WaitForSeconds(0.3f);
        yield return Fade(1f, 0f, restartFadeTime);

        fpp.enabled = true;
        busy = false;
    }

    // MemoryStart = "눈"이 갈 자리와 바라볼 방향 (ViewFocus의 viewPoint와 같은 방식)
    //   눈이 정확히 그 자리에 오도록 몸 위치를 거꾸로 계산한다 (몸 기준점은 발밑, 눈은 그보다 위에 있으므로)
    void PlaceAtStart()
    {
        PlaceAt(memoryStart);
    }

    // 눈이 eye 자리에 오고 eye 방향을 바라보도록 몸을 옮긴다
    // snapToGround: 발을 땅 위에 맞춤 (서 있는 자리. 앉은 자세처럼 일부러 낮게 둔 자리는 false)
    void PlaceAt(Transform eye, bool snapToGround = false)
    {
        float yaw = eye.eulerAngles.y;
        Vector3 eyeOffset = player.InverseTransformPoint(fpp.cameraRoot.position);   // 몸 기준 눈 위치
        Vector3 bodyPos = eye.position - Quaternion.Euler(0f, yaw, 0f) * eyeOffset;
        if (snapToGround) bodyPos.y = GroundY(bodyPos, bodyPos.y);
        Teleport(bodyPos, Quaternion.Euler(0f, yaw, 0f));
        fpp.SetPitch(Mathf.DeltaAngle(0f, eye.eulerAngles.x));   // 위아래는 eye의 X 회전 그대로
    }

    // pos 바로 아래 땅을 찾아, 발이 땅 위에 딱 닿는 몸 높이를 돌려준다 (못 찾으면 fallback)
    float GroundY(Vector3 pos, float fallback)
    {
        if (cc != null) cc.enabled = false;  // 자기 몸에 맞지 않게
        Vector3 from = new Vector3(pos.x, pos.y + 3f, pos.z);
        bool found = Physics.Raycast(from, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore);
        if (!found) return fallback;
        float feet = cc != null ? cc.center.y - cc.height * 0.5f : 0f;   // 몸 기준점에서 발바닥까지
        return hit.point.y - feet + 0.02f;
    }

    // CharacterController가 켜져 있으면 위치를 직접 바꿔도 무시되므로 잠깐 끄고 옮긴다
    void Teleport(Vector3 pos, Quaternion rot)
    {
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(pos, rot);
        if (cc != null) cc.enabled = true;
    }

    IEnumerator Fade(float from, float to, float time)
    {
        if (whiteScreen == null) yield break;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
        {
            SetWhite(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        SetWhite(to);
    }

    void SetWhite(float a)
    {
        Color c = whiteScreen.color;
        c.a = a;
        whiteScreen.color = c;
        whiteScreen.raycastTarget = false;   // 하얀 화면이 클릭을 가로막지 않게
    }

    bool FindPlayer()
    {
        if (fpp == null) fpp = FindFirstObjectByType<FirstPersonPlayer>();
        if (fpp == null || memoryStart == null)
        {
            Debug.LogWarning("MemoryTrip: 플레이어(FirstPersonPlayer) 또는 Memory Start를 찾지 못했어요.", this);
            return false;
        }
        player = fpp.transform;
        cc = fpp.GetComponent<CharacterController>();
        return true;
    }

    void OnDisable()
    {
        FirstPersonPlayer.memoryJump = false;   // 씬이 바뀌거나 꺼질 때 스위치가 남지 않게
    }
}