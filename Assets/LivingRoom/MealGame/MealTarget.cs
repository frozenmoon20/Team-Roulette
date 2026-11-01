using UnityEngine;

// The shared interactor is left unchanged; MealInput routes these targets.
public enum MealAction { None, Fridge, Toaster, Counter, RawToast, Jam, Juice, CookedToast, Plate, JamToast, EmptyCup, JuiceCup, Table }

public sealed class MealTarget : MonoBehaviour
{
    public MealGameController game;
    public MealAction action;
    public string displayName;
}
