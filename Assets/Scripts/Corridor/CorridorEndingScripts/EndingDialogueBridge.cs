using System;
using System.Collections;
using TeamRoulette.UI;
using UnityEngine;

namespace TeamRoulette.CorridorEnding
{
    public static class EndingDialogueBridge
    {
        public static IEnumerator Play(GameUI ui, DialogueSequence sequence)
        {
            if (ui == null || sequence == null || sequence.lines == null || sequence.lines.Length == 0)
            {
                Debug.LogError("엔딩 대사 UI 또는 DialogueSequence 연결을 확인하세요.");
                yield break;
            }

            while (ui != null && (ui.IsPaused || ui.IsDialogueOpen))
                yield return null;

            if (ui == null)
                yield break;

            bool finished = false;
            Action<DialogueSequence> handler = value =>
            {
                if (value == sequence)
                    finished = true;
            };

            ui.DialogueFinished += handler;
            ui.ShowDialogue(sequence);

            if (!ui.IsDialogueOpen)
            {
                ui.DialogueFinished -= handler;
                Debug.LogError("엔딩 대사를 시작하지 못했습니다. GameUI 상태를 확인하세요.");
                yield break;
            }

            while (ui != null && !finished)
                yield return null;

            if (ui != null)
                ui.DialogueFinished -= handler;

            yield return null;
        }
    }
}
