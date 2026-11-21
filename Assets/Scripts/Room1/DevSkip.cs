using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// [테스트용] 미니게임1·2를 건너뛰고 바로 침실에서 시작한다. 에디터에서만 동작하고, 빌드된 게임에서는 아무것도 안 한다.
// 원리: 모든 Awake가 Start보다 먼저 실행된다는 점을 이용한다.
//   1) Awake: 오프닝 대사(DialogueStep)의 Play On Start를 꺼서 MG1이 시작되지 않게 한다.
//   2) Start: 플레이어를 시작 지점으로 옮기고, 골라 둔 옵션을 해금하고, onSkip 이벤트를 실행한다.
// 이어하기: 타이틀에서 "이어하기"로 침실에 들어왔고 저장에 시점 회전(RotateCamera)이 해금돼 있으면
//   (= 미니게임1·2를 이미 끝냄) Skip Intro 체크와 상관없이 똑같이 건너뛴다. 빌드에서도 동작.
//   이때 옵션은 저장에서 이미 되살아나 있으므로 unlockOnSkip은 하지 않는다 (해금 알림이 다시 뜨지 않게).
// 사용: 빈 오브젝트(DevSkip)에 붙이고 칸을 채운다. 테스트할 때만 Skip Intro를 체크한다.
//       ※ 커밋 전에 Skip Intro 체크를 꼭 끌 것 (켜져 있어도 빌드에선 무시되지만, 팀원이 헷갈림)
public class DevSkip : MonoBehaviour
{
    public bool skipIntro = false;            // 체크하면 MG1·MG2를 건너뜀

    [Header("건너뛰기")]
    public DialogueStep openingDialogue;      // 오프닝 대사 (Dialogue_Opening) → 자동 재생을 끔
    public GameObject[] hideOnSkip;           // 시작할 때 켜져 있는 미니게임 화면 등 (검은 화면 등)

    [Header("시작 상태")]
    public Transform startPoint;              // 플레이어를 옮길 자리 (비워 두면 안 옮김)
    [Tooltip("이어하기 때 아직 이동이 해금 전이면(침대에 누워 있던 상태) Start Point 대신 침대에 눕힘")]
    public BedLie bedLie;
    public List<OptionType> unlockOnSkip;     // 미리 해금할 옵션 (SFX, RotateCamera, Move, Hand …)
    public UnityEvent onSkip;                 // 그 밖에 할 일 (목표창 보이기, 목표 표시 등)

    bool active;
    bool continued;                           // 이어하기로 들어와서 건너뛰는 중

    void Awake()
    {
        continued = SaveSystem.ContinuedInto(gameObject.scene.name) && SaveSystem.ContinueUnlocked(OptionType.RotateCamera);
        active = continued || (skipIntro && Application.isEditor);
        SaveGuard.introSkippedScene = active ? gameObject.scene.name : "";   // 건너뛰면 인트로의 SaveGuard가 저장을 멈추지 않게
        if (!active) return;

        if (openingDialogue != null) openingDialogue.playOnStart = false;
        foreach (GameObject g in hideOnSkip) if (g != null) g.SetActive(false);
    }

    void Start()
    {
        if (!active) return;
        if (continued) Debug.Log("DevSkip: 이어하기 → 미니게임1·2 건너뜀", this);
        else Debug.LogWarning("DevSkip: 미니게임1·2를 건너뛰었습니다 (테스트용). 커밋 전에 Skip Intro를 끄세요.", this);

        // 이어하기인데 아직 이동 전(꿈에서 깨어나 침대에 누워 있던 상태)이면 → 침대에 다시 눕힘
        //   (Start Point에 세우면 이동이 잠겨 있어서 강아지 계단까지 못 가고 갇힘)
        bool moveLocked = OptionState.Instance == null || !OptionState.Instance.IsUnlocked(OptionType.Move);
        if (continued && moveLocked && bedLie != null)
        {
            StartCoroutine(LieNextFrame());
        }
        // 플레이어 옮기기 (CharacterController는 켜진 채로 위치를 바꾸면 무시되므로 잠깐 끈다)
        else if (startPoint != null)
        {
            FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
                if (cc != null) cc.enabled = true;
            }
        }

        // 옵션 해금 (이어하기면 저장에서 이미 되살아났으므로 안 함)
        if (!continued && OptionState.Instance != null)
            foreach (OptionType opt in unlockOnSkip) OptionState.Instance.Unlock(opt);

        onSkip.Invoke();
    }

    // 플레이어의 다른 스크립트가 처음 자세를 다 잡은 뒤(한 프레임 뒤)에 눕혀야 덮어써지지 않음
    System.Collections.IEnumerator LieNextFrame()
    {
        yield return null;
        bedLie.LieDown();
    }
}