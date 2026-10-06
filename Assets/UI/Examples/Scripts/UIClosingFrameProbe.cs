using UnityEngine;

namespace TeamRoulette.UI
{
    // Regression test helper, instantiated only by the editor validation runner.
    [DefaultExecutionOrder(30000)]
    public class UIClosingFrameProbe : MonoBehaviour
    {
        public GameUI ui;
        public PlayerInteractor interactor;
        public bool blockedThroughoutClosingFrame;
        public bool restoredOnFollowingFrame;
        public bool finished;
        int closeFrame = -1;

        void Update()
        {
            if (closeFrame >= 0 || !ui || !interactor) return;
            closeFrame = Time.frameCount;
            ui.CloseSettings();
        }
        void LateUpdate()
        {
            if (finished || closeFrame < 0) return;
            if (Time.frameCount == closeFrame) blockedThroughoutClosingFrame = !interactor.enabled;
            else { restoredOnFollowingFrame = interactor.enabled; finished = true; }
        }
    }
}
