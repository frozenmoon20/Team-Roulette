using System;
using UnityEngine;

namespace TeamRoulette.UI
{
    // Session progress survives scene changes, but starts fresh on a new play session.
    public class PawProgress : MonoBehaviour
    {
        public PawProgressGraphic graphic;
        static int completedStages;
        static event Action Changed;
        public static int Count
        {
            get { int count = 0; for (int i = 0; i < 5; i++) if ((completedStages & (1 << i)) != 0) count++; return count; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BeginSession() { completedStages = 0; Changed = null; }

        void OnEnable() { Changed += Refresh; Refresh(); }
        void OnDisable() { Changed -= Refresh; }
        void Refresh() { if (graphic) graphic.SetProgress(Count); }

        // Connect one unique number (1–5) to each room's completion event.
        // Repeating an event never adds progress twice.
        public void CompleteStage(int stage)
        {
            if (stage < 1 || stage > 5) return;
            int bit = 1 << (stage - 1);
            if ((completedStages & bit) != 0) return;
            completedStages |= bit;
            Changed?.Invoke();
        }

        public void ResetProgress() { completedStages = 0; Changed?.Invoke(); }
    }
}
