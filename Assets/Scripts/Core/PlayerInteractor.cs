using TMPro;
using UnityEngine;
using TeamRoulette.UI;

// 화면 정중앙으로 조준해서 상호작용한다. PlayerCameraRoot에 붙어 있음.
// - 좌클릭: 조준한 오브젝트의 Interact() 실행
// - 우클릭: 들고 있는 물건을 원래 자리로
// - 물건을 들고 있으면 놓을 자리(PlaceSpot)만 클릭 가능
// - 조준 중인 물건의 이름을 화면 아래에 표시, 잡을 수 있는 물건이면 손 아이콘
// - 대사가 떠 있는 동안은 이름을 숨김 (화면 아래 대사창과 겹치지 않게)
public class PlayerInteractor : MonoBehaviour
{
    public Camera playerCamera;
    public float InteractRange = 3f;
    public float aimRadius = 0.03f;          // 조준 판정 두께 (작은 물건도 잘 맞게)

    [Header("조준 UI (비워 둬도 동작은 함)")]
    public GameObject crosshairDot;          // 기본 회색 점
    public GameObject handIcon;              // 잡을 수 있을 때 손 아이콘
    public TMP_Text nameText;                // 화면 아래 이름 / 짧은 안내 문구

    [Header("안내 문구")]
    public string handBusyMessage = "먼저 내려놓자.";
    public float hintDuration = 1.5f;

    // 대화창, 미니게임 UI가 떠 있을 때 true로 하면 조준과 클릭이 모두 멈춘다.
    // 사용 예: PlayerInteractor.Blocked = true;  (끝나면 false)
    public static bool Blocked;

    static PlayerInteractor instance;
    IInteractable hovered;
    Component hoveredComponent;
    string hintText;
    float hintUntil;
    bool lockedLastFrame;
    GameUI ui;

    // (복도) 소파 이름은 글자를 크게 보여 주기 위해 원래 글자 크기를 기억해 둔다
    float originalNameFontSize;
    const float SofaNameScale = 1.5f;

    void Awake()
    {
        instance = this;

        if (nameText != null)
            originalNameFontSize = nameText.fontSize;
    }

    // (거실) 씬을 겹쳐 열었다 돌아올 때 플레이어가 꺼졌다 켜지므로, 켜질 때 다시 '나'로 등록
    void OnEnable() { instance = this; lockedLastFrame = false; }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void LateUpdate()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        UpdateHover(locked);

        // Esc로 커서를 풀었다가 클릭해서 다시 잠근 그 클릭은 상호작용으로 치지 않음
        if (!Blocked && locked && lockedLastFrame)
        {
            if (Input.GetMouseButtonDown(0)) TryInteract();
            if (Input.GetMouseButtonDown(1)) TryReturnHeld();
        }
        lockedLastFrame = locked;

        UpdateUI();
    }

    // 매 프레임: 화면 정중앙에 있는 상호작용 오브젝트 찾기
    void UpdateHover(bool locked)
    {
        hovered = null;
        hoveredComponent = null;
        if (Blocked || !locked || playerCamera == null) return;

        if (!TryAimHit(out var hit)) return;

        // 콜라이더가 자식에 있어도 찾도록 부모까지 확인
        // Food items also have a disabled GrabbableItem; their MealTarget owns the label.
        var mealTarget = hit.collider.GetComponentInParent<MealTarget>();
        IInteractable target = mealTarget ? mealTarget : hit.collider.GetComponentInParent<IInteractable>();
        if (target == null) return;

        Behaviour behaviour = target as Behaviour;
        if (behaviour != null && !behaviour.isActiveAndEnabled) return;   // 꺼진 컴포넌트는 무시

        hovered = target;
        hoveredComponent = target as Component;
    }

    // Both the meal input and shared interactor use this exact hit. A central ray
    // avoids the wide cast switching between adjacent counter/ingredient colliders.
    public bool TryAimHit(out RaycastHit hit)
    {
        hit = default;
        if (!playerCamera) return false;
        var ray = playerCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
        if (FindAimHit(Physics.RaycastAll(ray, InteractRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide), out hit)) return true;
        return FindAimHit(Physics.SphereCastAll(ray, aimRadius, InteractRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide), out hit);
    }
    static bool FindAimHit(RaycastHit[] hits, out RaycastHit hit)
    {
        hit = default;
        float distance = float.PositiveInfinity;
        foreach (var candidate in hits)
        {
            // The counter's generous placement trigger is only for putting down
            // jam. It must not cover the thin plate or cup when preparing food.
            var meal = candidate.collider.GetComponentInParent<MealTarget>();
            if (candidate.collider.isTrigger && meal && meal.action == MealAction.Counter &&
                (!PlayerHand.Instance || !meal.game || PlayerHand.Instance.Held != meal.game.jam)) continue;
            // Area/quest triggers must not hide physical objects. Wash tap hitboxes
            // intentionally are triggers, so retain triggers that can be interacted with.
            if (candidate.collider.isTrigger && candidate.collider.GetComponentInParent<IInteractable>() == null) continue;
            if (candidate.distance >= distance) continue;
            distance = candidate.distance; hit = candidate;
        }
        return hit.collider;
    }

    void TryInteract()
    {
        if (hovered == null) return;

        // 물건을 들고 있으면 놓을 자리(PlaceSpot)만 클릭 가능
        PlayerHand hand = PlayerHand.Instance;
        if (hand != null && hand.Held != null && !(hovered is PlaceSpot))
        {
            ShowHint(handBusyMessage);
            return;
        }

        hovered.Interact();
    }

    void TryReturnHeld()
    {
        PlayerHand hand = PlayerHand.Instance;
        if (hand == null || hand.Held == null) return;

        // (복도 미니게임3) 클릭커는 우클릭으로 내려놓을 수 없고, E키로 주머니에 넣는다
        ClickerPocketController pocketController = FindFirstObjectByType<ClickerPocketController>();
        if (pocketController != null && hand.Held == pocketController.clicker)
        {
            ShowHint("클릭커는 E키를 눌러 주머니에 넣자.");
            return;
        }

        // (복도 미니게임1) 들고 있는 게 캡슐 장난감이면, 열 수 있을 때는 돌려놓지 않고 연다
        CapsuleToy capsule = hand.Held.GetComponent<CapsuleToy>();
        if (capsule != null && capsule.TryOpen()) return;

        hand.ReturnHeld();
    }

    void UpdateUI()
    {
        bool showHand = false;
        string label = "";

        if (hovered != null)
        {
            GrabbableItem item = hovered as GrabbableItem;
            showHand = item != null && item.CanGrab;

            IHasName named = hovered as IHasName;
            if (named == null && hoveredComponent != null) named = hoveredComponent.GetComponentInParent<IHasName>();
            if (named != null) label = named.DisplayName;
        }
        if (Time.time < hintUntil) label = hintText;   // 안내 문구가 이름보다 우선

        // 대사 중에는 이름·안내 문구를 숨김 (대사창과 같은 자리라 겹침)
        if (ui == null) ui = FindFirstObjectByType<GameUI>();
        if (ui != null && ui.IsDialogueOpen) label = "";

        if (crosshairDot != null) crosshairDot.SetActive(!Blocked && !showHand);
        if (handIcon != null) handIcon.SetActive(!Blocked && showHand);

        if (nameText != null)
        {
            // (복도) 소파를 조준했을 때만 이름을 크게 (안내 문구가 떠 있을 땐 보통 크기)
            if (originalNameFontSize <= 0f) originalNameFontSize = nameText.fontSize;
            bool isSofaName = hovered is SofaPeekInteraction && Time.time >= hintUntil;
            nameText.fontSize = originalNameFontSize * (isSofaName ? SofaNameScale : 1f);
            nameText.text = label;
        }
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
