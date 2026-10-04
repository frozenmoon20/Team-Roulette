using System;
using TMPro;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    public Camera playerCamera;
    public float InteractRange = 2f;
    public float aimRadius = 0.03f;          // 조준 판정 두께 (작은 물건도 잘 맞게)

    public TMP_Text nameText;                // 화면 아래 이름 (비워도 동작은 함)

    public GameObject crosshairDot;          // 기본 회색 점
    public GameObject handIcon;              // 잡을 수 있을 때 손 아이콘

    public string handBusyMessage = "먼저 내려놓자.";
    public float hintDuration = 1.5f;

    static PlayerInteractor instance;
    string hintText;
    float hintUntil;

    IInteractable hovered;                   // 지금 조준 중인 대상 (없으면 null)
    Component hoveredComponent;
    bool lockedLastFrame;

    void Awake()     { instance = this; }
    void OnDestroy() { if (instance == this) instance = null; }

    void LateUpdate()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked;

        // 매 프레임 어떤거 조준하는지 찾기
        UpdateHover(locked);

        // 조준 중인 대상이 있으면 실행
        if (locked && lockedLastFrame)
        {
            PlayerHand hand = PlayerHand.Instance;
            bool holding = hand != null && hand.Held != null;

            // 좌클릭: 들고 있으면 막고 안내, 아니면 실행
            if (Input.GetMouseButtonDown(0) && hovered != null)
            {
                if (holding && !(hovered is PlaceSpot)) ShowHint(handBusyMessage);   // 들고 있으면 놓을 자리만 클릭 가능
                else hovered.Interact();
            }

            // 우클릭: 들고 있는 물건을 원래 자리로
            if (Input.GetMouseButtonDown(1) && holding) hand.ReturnHeld();
        }
        lockedLastFrame = locked;

        // 이름 표시
        UpdateUI();

    }

    
    void UpdateHover(bool locked)
    {
        hovered = null;
        hoveredComponent = null;
        
        if(!locked || playerCamera == null) { return; }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.SphereCast(ray, aimRadius, out RaycastHit hit, InteractRange)) return;

        IInteractable target = hit.collider.GetComponentInParent<IInteractable>();
        if (target == null) return;

        Behaviour behaviour = target as Behaviour;
        if (behaviour != null && !behaviour.isActiveAndEnabled) return;   // 꺼진 것(이미 놓인 물건, 다 찬 자리)은 무시

        hovered = target;
        hoveredComponent = target as Component;


    }
    void UpdateUI()
    {
        string label = "";
        bool showHand = false;

        if (hoveredComponent != null)
        {
            IHasName named = hoveredComponent.GetComponentInParent<IHasName>();
            if (named != null) label = named.DisplayName;

            // 잡을 수 있는 물건, 또는 들고 있는 물건을 받아 줄 자리면 손 아이콘
            GrabbableItem item = hovered as GrabbableItem;
            PlaceSpot spot = hovered as PlaceSpot;
            PlayerHand hand = PlayerHand.Instance;
            GrabbableItem held = hand != null ? hand.Held : null;
            showHand = (item != null && item.CanGrab) || (spot != null && spot.CanAccept(held));
        }

        if (Time.time < hintUntil) label = hintText;   // 안내 문구가 이름보다 우선
        if (nameText != null) nameText.text = label;
        if (crosshairDot != null) crosshairDot.SetActive(!showHand);
        if (handIcon != null) handIcon.SetActive(showHand);
    }
    
    // 화면 아래에 짧은 안내 문구 표시. 어느 스크립트에서나 호출 가능.
    // 사용 예: PlayerInteractor.ShowHint("잠겨 있다.");
    public static void ShowHint(string message)
    {
        if (instance == null || string.IsNullOrEmpty(message)) return;
        instance.hintText = message;
        instance.hintUntil = Time.time + instance.hintDuration;
    }

}
