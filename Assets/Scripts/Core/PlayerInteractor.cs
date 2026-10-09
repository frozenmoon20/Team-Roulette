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

    void Awake()
    {
        instance = this;
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

            if (Input.GetMouseButtonDown(1) && holding)
            {
                CapsuleToy capsule =
                    hand.Held.GetComponent<CapsuleToy>();

                if (capsule != null && capsule.TryOpen())
                {
                    return;
                }

                hand.ReturnHeld();
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
            nameText.text = label;

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