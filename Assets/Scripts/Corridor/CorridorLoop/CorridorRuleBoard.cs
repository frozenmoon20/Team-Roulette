
using UnityEngine;
using TMPro;

public class CorridorRuleBoard : MonoBehaviour, IInteractable, IHasName
{
    [Header("상호작용 이름")]
    public string displayName = "규칙 읽기";

    [Header("규칙 설명 UI")]
    public GameObject rulePanel;
    public TMP_Text ruleText;

    [TextArea(8, 16)]
    public string ruleMessage =
        "반복 복도의 규칙\n\n" +
        "이 복도에서는 주변을 잘 살펴야 한다.\n\n" +
        "처음 마주하는 복도는 반드시 정상적인 복도다.\n" +
        "이때 바닥에 놓인 카펫을 기준점으로 기억하자.\n\n" +
        "평소와 다른 점이 없다면, 지금까지 가던 " +
        "방향으로 계속 나아가자.\n\n" +
        "하지만 이상한 점을 발견했다면, " +
        "반대 방향으로 되돌아가야 한다.\n\n" +
        "올바른 선택을 6번 연속 하면 " +
        "이 복도에서 벗어날 수 있다.\n\n" +
        "단, 한 번이라도 잘못 선택하면 " +
        "처음부터 다시 시작해야 한다.";

    [Header("플레이어")]
    public FirstPersonPlayer player;
    public PlayerInteractor interactor;

    private bool isOpen;
    private int openedFrame;

    private bool previousPlayerEnabled;
    private bool previousInteractorEnabled;

    public string DisplayName => displayName;

    private void Start()
    {
        if (rulePanel != null)
            rulePanel.SetActive(false);

        if (player == null)
            player = FindFirstObjectByType<FirstPersonPlayer>();

        if (interactor == null)
            interactor = FindFirstObjectByType<PlayerInteractor>();
    }

    public void Interact()
    {
        if (isOpen || rulePanel == null)
            return;

        isOpen = true;
        openedFrame = Time.frameCount;

        if (ruleText != null)
            ruleText.text = ruleMessage;

        // 원래 활성화 상태 기억
        if (player != null)
        {
            previousPlayerEnabled = player.enabled;
            player.enabled = false;
        }

        if (interactor != null)
        {
            previousInteractorEnabled = interactor.enabled;
            interactor.enabled = false;
        }

        rulePanel.SetActive(true);
    }

    private void Update()
    {
        if (!isOpen)
            return;

        // 창을 연 마우스 클릭으로 바로 닫히는 것 방지
        if (Time.frameCount <= openedFrame + 1)
            return;

        // 키보드 또는 마우스 입력으로 닫기
        if (Input.anyKeyDown ||
            Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1))
        {
            CloseRules();
        }
    }

    private void CloseRules()
    {
        if (!isOpen)
            return;

        isOpen = false;

        if (rulePanel != null)
            rulePanel.SetActive(false);

        // 원래 플레이어 상태 복구
        if (player != null)
            player.enabled = previousPlayerEnabled;

        if (interactor != null)
            interactor.enabled = previousInteractorEnabled;
    }

    private void OnDisable()
    {
        if (isOpen)
            CloseRules();
    }
}
