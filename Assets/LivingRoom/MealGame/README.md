# MealGame

Scene: `Assets/Scenes/Room3_LivingRoom.unity`.
All MealGame files live here. Existing kitchen furniture and ingredients are grouped below the scene's `MealGame` object. Shared player scripts are unchanged.

## Play

The existing Hand and Grab options must be unlocked. Movement and camera rotation use the project's existing unlock rules.

1. Click the fridge for toast, then the toaster. A 10-second countdown starts.
2. Click the fridge for jam, then either counter to place it at `JamOnCounter`.
3. After the ding, click the toaster or ready toast. Click the plate to place the toast.
4. Click jam then cooked toast, or cooked toast then jam. Any intervening left click, right click, or loss of cursor lock cancels the pair.
5. Click the plate or jam toast to carry the plate and its child toast. Click the table.
6. Click the fridge for juice, then the empty cup. The juice container disappears and the full cup appears.
7. Click the full cup and then the table. The screen is black for 0.5 seconds, followed by a 3-second eating sound. Only an empty plate and empty cup remain.

Right click cancels a pickup and restores that item's previous state. Fridge ingredients return hidden and can be obtained again. Cooked toast returns to the toaster, and a carried plate/full cup returns to its pickup position.

## Placement and sound

Move the child markers under `MealGame/PlacementPoints` to adjust jam placement, toast pop position, left plate/right cup positions, or the eating camera view. `ToastOnPlate` is a child of the plate. `잼 토스트` is already a child of the plate and starts inactive.

`ToasterTimer` is a world-space panel. `MealBlackScreen` is the ending overlay. The existing furniture positions and dimensions are preserved.

The three clips under `Audio` are original synthesized basic effects. Replace the controller's Ding Sound, Pour Sound, and Eat Sound references with final recordings if desired. Timings are editable on the MealGame controller.

## Verification

`Tools > MealGame > 2 - Verify full sequence` runs both jam/toast orders in separate Play Mode sessions, including ray targeting, wrong destinations, pickup cancellation, cooking duration, and the ending. Results are written to `Verification.txt`. Test unlocks and camera moves happen only in Play Mode.

`Tools > MealGame > 3 - Verify mouse pickup and return` opens a temporary fridge view for a real left click followed by a right click, then exits Play Mode automatically.

The setup command is one-time only and refuses to overwrite an existing MealGame setup.
