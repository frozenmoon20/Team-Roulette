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
            "환기 좀 하자..."
        };
        public static readonly string[] WindowFinish =
        {
            "바람 좋네...", "정신 좀 차리자. 얼굴부터 씻어야겠어."
        };

        // 4. WashGame — 세안 시작 / 완료
        public static readonly string[] WashStart =
        {
            "찬물로 씻으면 조금 낫겠지."
        };
        public static readonly string[] WashFinish =
        {
            "이제 좀 개운하네.", "제대로 밥을 먹은 게 언제였더라. 간단하게라도 먹자."
        };

        // 5. MealGame — 식사 시작 / 완료
        public static readonly string[] MealStart =
        {
            "토스트와 주스면 되겠지."
        };
        public static readonly string[] MealFinish =
        {
            "이렇게 앉아서 먹는 것도 오랜만이네.", "장난감들이 어지러져있네... 장난감들도 이젠 정리해야지."
        };

        // 6. BoardGame — 게시판 시작 / 완료
        public static readonly string[] BoardStart =
        {
            "겨울이 사진들이랑 친구와의 추억들...", "하나씩 다시 걸어 두자."
        };
        public static readonly string[] BoardFinish =
        {
            "함께 보낸 시간이 이렇게 많았구나.", "...이제 친구를 보러가자."
        };

        // 8. TV / 어질리티 — 시작 / 완료
        public static readonly string[] TVStart =
        {
            "너랑 어질리티하던 영상. 친구가 찍어줬었지.", 
        };
        public static readonly string[] TVFinish =
        {
            "저렇게 같이 신나게 뛰었었지...", "맞아, 그 때 메달도 받았었어.", "사진들이 떨어져있네..."
        };

        // 9. 현관 — 시작 / 완료
        public static readonly string[] EntranceStart =
        {
            "다녀올게, 겨울아."
        };
        public static readonly string[] EntranceFinish =
        {
            "우리 또 만나자."
        };

        // BoneGame 대사
        public const string BoneStart = "겨울아 물어와!";
        public const string BoneMiss = "앗, 다시 던져줄게.";
        public const string BoneSuccess = "잘했어, 겨울아!";
        public const string BoneFinish = "매일 해줄걸.";

        // TugGame 대사
        public const string TugStart = "겨울이 물어!";
        public const string TugSuccess = "좋아!";
        public const string TugMiss = "겨울아 반대로 당기면...!";
        public const string TugRoundBreak = "겨울이 재밌었어?";
        public const string TugLookAtSky = "아, 날씨 좋다!";
        public const string TugPlayAgain = "또 놀자고?";
        public const string TugFinish = "겨울이는 힘도 셌지.";

        // DollGame 대사
        public const string DollStart = "겨울아 인형 빨아야지...!";
        public const string DollTaken = "금방 빨아줄게.";
        public const string DollWashing = "금방 될거야.";
        public const string DollReturned = "자, 여기 겨울이 오리!";
        public const string DollFinish = "겨울이가 맨날 물고 다녔었지...";

        // 아래는 연결 코드입니다. 대사 수정은 위 배열에서만 하면 됩니다.
        public static string[] GetStart(GameStage stage) => stage switch
        {
            GameStage.Curtain => CurtainStart, GameStage.Window => WindowStart,
            GameStage.Wash => WashStart,
            GameStage.Meal => MealStart, GameStage.Board => BoardStart,
            GameStage.TV => TVStart,
            GameStage.Entrance => EntranceStart, _ => System.Array.Empty<string>()
        };
        public static string[] GetFinish(GameStage stage) => stage switch
        {
            GameStage.Curtain => CurtainFinish, GameStage.Window => WindowFinish,
            GameStage.Wash => WashFinish,
            GameStage.Meal => MealFinish, GameStage.Board => BoardFinish,
            GameStage.TV => TVFinish,
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
        void OnBegin(GameStage stage)
        {
            var lines = Room3DialogueScript.GetStart(stage);
            // Curtain lines continue one at a time when Winter reaches each guide stop.
            PlayLines(stage == GameStage.Curtain ? lines.Take(1).ToArray() : lines);
        }
        void OnComplete(GameStage stage) { PlayLines(Room3DialogueScript.GetFinish(stage)); }
        public bool HasDialogue => pending.Count > 0 || (ui && ui.IsDialogueOpen);

        public static DialogueManager Speak(GameUI ui, params string[] lines)
        {
            if (!ui) return null;
            var dialogue = ui.GetComponent<DialogueManager>();
            if (!dialogue) dialogue = ui.gameObject.AddComponent<DialogueManager>();
            dialogue.PlayLines(lines);
            return dialogue;
        }

        public void PlayLines(string[] lines, float secondsPerLine = 2f)
        {
            var text = lines?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (text == null || text.Length == 0) return;
            var sequence = ScriptableObject.CreateInstance<DialogueSequence>();
            sequence.name = "Room3 script dialogue";
            sequence.hideFlags = HideFlags.DontSave;
            sequence.lines = text;
            sequence.secondsPerLine = secondsPerLine;
            generated.Add(sequence);
            Play(sequence);
        }
        // Environmental remarks wait until the existing story dialogue has finished.
        public bool TryPlayAmbient(string line)
        {
            if (!isActiveAndEnabled || !ui || ui.IsPaused || ui.IsDialogueOpen || pending.Count > 0 || Time.timeScale <= 0) return false;
            PlayLines(new[] { line });
            return true;
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

