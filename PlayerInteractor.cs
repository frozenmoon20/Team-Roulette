
using System;
using TMPro;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    public Camera playerCamera;
    public float InteractRange = 2f;
    public float aimRadius = 0.03f;

    public TMP_Text nameText;

    public GameObject crosshairDot;
    public GameObject handIcon;

    public string handBusyMessage = "먼저 내려놓자.";
    public float hintDuration = 1.5f;

    static PlayerInteractor instance;
    string hintText;
    float hintUntil;

    IInteractable hovered;
    Component hoveredComponent;
    bool lockedLastFrame;

    float originalNameFontSize;
    const float SofaNameScale = 1.5f;

    void Awake()
    {
        instance = this;

        if (nameText != null)
            originalNameFontSize = nameText.fontSize;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void LateUpdate()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked;

        UpdateHover(locked);

        if (locked && lockedLastFrame)
        {
            PlayerHand hand = PlayerHand.Instance;
            bool holding = hand != null && hand.Held != null;

            // 좌클릭 상호작용
            if (Input.GetMouseButtonDown(0) && hovered != null)
            {
                if (holding && !(hovered is PlaceSpot))
                {
                    ShowHint(handBusyMessage);
                }
                else
                {
                    hovered.Interact();
                }
            }

            // 우클릭 상호작용
            if (Input.GetMouseButtonDown(1) && holding)
            {
                // [추가] 클릭커는 우클릭으로 내려놓을 수 없음
                ClickerPocketController pocketController =
                    FindFirstObjectByType<ClickerPocketController>();

                if (pocketController != null &&
                    hand.Held == pocketController.clicker)
                {
                    ShowHint("클릭커는 E키를 눌러 주머니에 넣자.");
                }
                else
                {
                    // 기존 캡슐 장난감 기능 유지
                    CapsuleToy capsule =
                        hand.Held.GetComponent<CapsuleToy>();

                    if (capsule != null && capsule.TryOpen())
                    {
                        return;
                    }

                    // 클릭커 이외의 물건은 기존처럼 내려놓기
                    hand.ReturnHeld();
                }
            }
        }

        lockedLastFrame = locked;

        UpdateUI();
    }

    void UpdateHover(bool locked)
    {
        hovered = null;
        hoveredComponent = null;

        if (!locked || playerCamera == null)
            return;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        if (!Physics.SphereCast(
            ray,
            aimRadius,
            out RaycastHit hit,
            InteractRange
        ))
            return;

        IInteractable target =
            hit.collider.GetComponentInParent<IInteractable>();

        if (target == null)
            return;

        Behaviour behaviour = target as Behaviour;

        if (behaviour != null && !behaviour.isActiveAndEnabled)
            return;

        hovered = target;
        hoveredComponent = target as Component;
    }

    void UpdateUI()
    {
        string label = "";
        bool showHand = false;

        if (hoveredComponent != null)
        {
            IHasName named =
                hoveredComponent.GetComponentInParent<IHasName>();

            if (named != null)
                label = named.DisplayName;

            GrabbableItem item = hovered as GrabbableItem;
            PlaceSpot spot = hovered as PlaceSpot;

            PlayerHand hand = PlayerHand.Instance;

            GrabbableItem held =
                hand != null ? hand.Held : null;

            showHand =
                (item != null && item.CanGrab) ||
                (spot != null && spot.CanAccept(held));
        }

        if (Time.time < hintUntil)
            label = hintText;

        if (nameText != null)
        {
            bool isSofaName =
                hovered is SofaPeekInteraction &&
                Time.time >= hintUntil;

            nameText.fontSize =
                originalNameFontSize *
                (isSofaName ? SofaNameScale : 1f);

            nameText.text = label;
        }

        if (crosshairDot != null)
            crosshairDot.SetActive(!showHand);

        if (handIcon != null)
            handIcon.SetActive(showHand);
    }

    public static void ShowHint(string message)
    {
        if (instance == null || string.IsNullOrEmpty(message))
            return;

        instance.hintText = message;
        instance.hintUntil =
            Time.time + instance.hintDuration;
    }
}
