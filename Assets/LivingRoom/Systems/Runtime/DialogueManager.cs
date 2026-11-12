using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TeamRoulette.UI;

namespace TeamRoulette.Room3
{
    /// <summary>
    /// Room3 대사는 이 파일에 직접 작성합니다. GameUI의 기존 하단 대화창으로 재생됩니다.
    /// 배열 안에 "문장", 형태로 한 줄씩 추가하세요. 문장 사이에는 쉼표가 필요합니다.
    /// 예: public static readonly string[] Opening = { "첫 번째 문장", "두 번째 문장" };
    /// 빈 배열은 재생하지 않습니다. 저장하고 Unity 컴파일이 끝나면 Play를 다시 시작하세요.
    /// 문장당 2초 / 대사 중 이동 가능 / ESC 설정창을 열면 재생 일시정지.
    /// </summary>
    public static class Room3DialogueScript
    {
        // 거실에 처음 들어왔을 때 (계단 앞). 커튼 게임 시작 대사와는 별개입니다.
        public static readonly string[] Opening =
        {
            "거실은 오랜만이네...", "커튼을 열어볼까?"
        };

        // 1. CurtainGame — 커튼 시작 / 완료
        public static readonly string[] CurtainStart =
        {
            "겨울이...?", "겨울이야...?", "겨울아...", "겨울아!"
        };
        public static readonly string[] CurtainFinish =
        {
            "겨울아...", "밝아...", "날씨 좋다..."
        };

        // 2. 창문 열기 — 시작 / 완료
        public static readonly string[] WindowStart =
        {
            "창문 시작"
        };
        public static readonly string[] WindowFinish =
        {
            "창문 끝"
        };

        // 3. CalGame — 달력 시작 / 완료
        public static readonly string[] CalendarStart =
        {
            "달력 시작"
        };
        public static readonly string[] CalendarFinish =
        {
            "달력 끝"
        };

        // 4. WashGame — 세안 시작 / 완료
        public static readonly string[] WashStart =
        {
            "세안 시작"
        };
        public static readonly string[] WashFinish =
        {
            "세안 끝"
        };

        // 5. MealGame — 식사 시작 / 완료
        public static readonly string[] MealStart =
        {
            "식사 시작"
        };
        public static readonly string[] MealFinish =
        {
            "식사 끝"
        };

        // 6. BoardGame — 게시판 시작 / 완료
        public static readonly string[] BoardStart =
        {
            "게시판 시작"
        };
        public static readonly string[] BoardFinish =
        {
            "게시판 끝"
        };

        // 7. 장난감 정리 — 시작 / 완료
        public static readonly string[] ToysStart =
        {
            "장난감 정리 시작"
        };
        public static readonly string[] ToysFinish =
        {
            "장난감 정리 끝"
        };

        // 8. TV / 어질리티 — 시작 / 완료
        public static readonly string[] TVStart =
        {
            "TV 시작"
        };
        public static readonly string[] TVFinish =
        {
            "TV 끝"
        };

        // 9. 현관 — 시작 / 완료
        public static readonly string[] EntranceStart =
        {
            "현관 시작"
        };
        public static readonly string[] EntranceFinish =
        {
            "현관 끝"
        };

        // 아래는 연결 코드입니다. 대사 수정은 위 배열에서만 하면 됩니다.
        public static string[] GetStart(GameStage stage) => stage switch
        {
            GameStage.Curtain => CurtainStart, GameStage.Window => WindowStart,
            GameStage.Calendar => CalendarStart, GameStage.Wash => WashStart,
            GameStage.Meal => MealStart, GameStage.Board => BoardStart,
            GameStage.Toys => ToysStart, GameStage.TV => TVStart,
            GameStage.Entrance => EntranceStart, _ => System.Array.Empty<string>()
        };
        public static string[] GetFinish(GameStage stage) => stage switch
        {
            GameStage.Curtain => CurtainFinish, GameStage.Window => WindowFinish,
            GameStage.Calendar => CalendarFinish, GameStage.Wash => WashFinish,
            GameStage.Meal => MealFinish, GameStage.Board => BoardFinish,
            GameStage.Toys => ToysFinish, GameStage.TV => TVFinish,
            GameStage.Entrance => EntranceFinish, _ => System.Array.Empty<string>()
        };
    }
}

namespace TeamRoulette.Room3
{
    [RequireComponent(typeof(GameUI))]
    public sealed class DialogueManager : MonoBehaviour
    {
        readonly Queue<DialogueSequence> pending = new Queue<DialogueSequence>();
        readonly HashSet<DialogueSequence> generated = new HashSet<DialogueSequence>();
        GameManager manager;
        GameUI ui;
        void Awake() { ui = GetComponent<GameUI>(); }
        void Start() { Bind(); }
        void OnEnable()
        {
            if (!ui) ui = GetComponent<GameUI>();
            ui.DialogueFinished += ReleaseGenerated;
            Bind();
        }
        void Bind()
        {
            if (manager || !GameManager.Instance) return;
            manager = GameManager.Instance;
            manager.Began += OnBegin;
            manager.Completed += OnComplete;
            if (SceneManager.GetActiveScene().name == "Room3_LivingRoom" && manager.TryQueueOpeningDialogue())
                PlayLines(Room3DialogueScript.Opening);
        }
        void OnBegin(GameStage stage) { PlayLines(Room3DialogueScript.GetStart(stage)); }
        void OnComplete(GameStage stage) { PlayLines(Room3DialogueScript.GetFinish(stage)); }
        public void PlayLines(string[] lines)
        {
            var text = lines?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (text == null || text.Length == 0) return;
            var sequence = ScriptableObject.CreateInstance<DialogueSequence>();
            sequence.name = "Room3 script dialogue";
            sequence.hideFlags = HideFlags.DontSave;
            sequence.lines = text;
            generated.Add(sequence);
            Play(sequence);
        }
        void ReleaseGenerated(DialogueSequence sequence)
        {
            if (generated.Remove(sequence)) Destroy(sequence);
        }
        public void Play(DialogueSequence sequence)
        {
            if (sequence && sequence.lines != null && sequence.lines.Length > 0) pending.Enqueue(sequence);
        }
        void Update()
        {
            if (!ui || ui.IsPaused || ui.IsDialogueOpen || Time.timeScale <= 0 || pending.Count == 0) return;
            var next = pending.Peek();
            ui.ShowDialogue(next);
            if (ui.IsDialogueOpen) pending.Dequeue();
        }
        void OnDisable()
        {
            if (ui) ui.DialogueFinished -= ReleaseGenerated;
            if (manager) { manager.Began -= OnBegin; manager.Completed -= OnComplete; }
            manager = null;
            // Keep queued lines while the living room is suspended for a temporary visit.
        }
        void OnDestroy()
        {
            foreach (var sequence in generated) if (sequence) Destroy(sequence);
            generated.Clear();
        }
    }
}
