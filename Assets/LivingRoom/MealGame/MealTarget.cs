using UnityEngine;

// The shared interactor is left unchanged; MealInput routes these targets.
public enum MealAction { None, Fridge, Toaster, Counter, RawToast, Jam, Juice, CookedToast, Plate, JamToast, EmptyCup, JuiceCup, Table }

public sealed class MealTarget : MonoBehaviour, IInteractable, IHasName
{
    public MealGameController game;
    public MealAction action;
    public string displayName;
    public string DisplayName
    {
        get
        {
            string name = action switch {
                MealAction.Fridge => "냉장고", MealAction.Toaster => "토스터기",
                MealAction.Counter => "조리대", MealAction.RawToast => "식빵",
                MealAction.Jam => "잼", MealAction.Juice => "주스",
                MealAction.CookedToast => "구운 토스트", MealAction.Plate => "접시",
                MealAction.JamToast => "잼 토스트", MealAction.EmptyCup => "빈 컵",
                MealAction.JuiceCup => "주스가 담긴 컵", MealAction.Table => "식탁",
                _ => displayName
            };
            return name;
        }
    }
    // MealInput owns the actual sequence, including clicks on empty space.
    public void Interact() { }
}
