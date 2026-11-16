using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// [테스트용] 미니게임1·2를 건너뛰고 바로 침실에서 시작한다. 에디터에서만 동작하고, 빌드된 게임에서는 아무것도 안 한다.
// 원리: 모든 Awake가 Start보다 먼저 실행된다는 점을 이용한다.
//   1) Awake: 오프닝 대사(DialogueStep)의 Play On Start를 꺼서 MG1이 시작되지 않게 한다.
//   2) Start: 플레이어를 시작 지점으로 옮기고, 골라 둔 옵션을 해금하고, onSkip 이벤트를 실행한다.
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
    public List<OptionType> unlockOnSkip;     // 미리 해금할 옵션 (SFX, RotateCamera, Move, Hand …)
    public UnityEvent onSkip;                 // 그 밖에 할 일 (목표창 보이기, 목표 표시 등)

    bool active;

    void Awake()
    {
        active = skipIntro && Application.isEditor;
        if (!active) return;

        if (openingDialogue != null) openingDialogue.playOnStart = false;
        foreach (GameObject g in hideOnSkip) if (g != null) g.SetActive(false);
    }

    void Start()
    {
        if (!active) return;
        Debug.LogWarning("DevSkip: 미니게임1·2를 건너뛰었습니다 (테스트용). 커밋 전에 Skip Intro를 끄세요.", this);

        // 플레이어 옮기기 (CharacterController는 켜진 채로 위치를 바꾸면 무시되므로 잠깐 끈다)
        if (startPoint != null)
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

        // 옵션 해금
        if (OptionState.Instance != null)
            foreach (OptionType opt in unlockOnSkip) OptionState.Instance.Unlock(opt);

        onSkip.Invoke();
    }
}