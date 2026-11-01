using UnityEngine;

// Runs before PlayerInteractor.LateUpdate, taking over only MealGame targets/items.
[DefaultExecutionOrder(-200)]
public sealed class MealInput : MonoBehaviour
{
    public MealGameController game;
    public PlayerInteractor shared;
    bool ownsInput, sharedWasEnabled, lockedLastFrame;

    public MealTarget FindTarget()
    {
        if (!shared || !shared.playerCamera) return null;
        Ray ray = shared.playerCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
        if (!Physics.SphereCast(ray, shared.aimRadius, out var hit, shared.InteractRange,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return null;
        var target = hit.collider.GetComponentInParent<MealTarget>();
        return target && target.game == game && target.isActiveAndEnabled ? target : null;
    }

    void LateUpdate()
    {
        if (!shared || !game) return;
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        MealTarget target = locked ? FindTarget() : null;
        bool scoped = TeamRoulette.Room3.GameManager.CanPlay(TeamRoulette.Room3.GameStage.Meal) && (game.Ending || target || game.IsHoldingMealItem);
        if (scoped && !ownsInput)
        {
            sharedWasEnabled = shared.enabled;
            shared.enabled = false;
            ownsInput = true;
        }
        else if (!scoped) RestoreShared();

        if (locked && lockedLastFrame && Time.timeScale > 0 && !game.Ending)
        {
            if (Input.GetMouseButtonDown(0)) game.Click(target ? target.action : MealAction.None);
            if (scoped && Input.GetMouseButtonDown(1)) game.ReturnHeld();
        }
        if (!locked) game.ClearSelection();
        lockedLastFrame = locked;
        if (ownsInput)
        {
            if (shared.nameText) shared.nameText.text = game.Ending ? "" : game.Label(target);
            bool usable = target && game.CanUse(target.action) && !game.Ending;
            if (shared.handIcon) shared.handIcon.SetActive(usable);
            if (shared.crosshairDot) shared.crosshairDot.SetActive(!usable && !game.Ending);
        }
    }

    void RestoreShared()
    {
        if (!ownsInput) return;
        if (shared) shared.enabled = sharedWasEnabled;
        ownsInput = false;
    }
    void OnDisable() { RestoreShared(); if (game) game.ClearSelection(); }
}
