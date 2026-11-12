using System.Collections.Generic;
using UnityEngine;
using TeamRoulette.UI;

namespace TeamRoulette.Room3
{
    public enum ObjectiveStep
    {
        Default, FollowDog, OpeningCurtains, WetFace, SelectSoap, ApplySoap, RinseFace,
        Rinsing, SelectTowel, WipeFace, WashComplete, GetToast, PutToastInToaster,
        GetJam, PutJamOnCounter, WaitForToast, TakeToast, PutToastOnPlate, ApplyJam,
        PlacePlate, GetJuice, PourJuice, PlaceCup, EatMeal, ArrangeBoard, ResolveOverlap,
        FinishBoard, MarkCalendar
    }

    [RequireComponent(typeof(SceneUIContent))]
    public sealed class ObjectiveManager : MonoBehaviour
    {
        // 기본 목표: 문구 수정 후 저장하고 Unity 컴파일이 끝나면 Play를 다시 시작하세요.
        public static string GetObjective(GameStage stage) => stage switch
        {
            GameStage.Curtain => "커튼을 열자",
            GameStage.Window => "창문을 열자",
            GameStage.Wash => "세수하자",
            GameStage.Meal => "식사를 준비하자",
            GameStage.Board => "게시판을 정리하자",
            GameStage.Toys => "장난감을 정리하자",
            GameStage.Calendar => "달력을 넘기자",
            GameStage.TV => "TV를 켜자",
            GameStage.Entrance => "현관으로 가자",
            _ => ""
        };

        // 세부 목표: 게임은 아래의 ID만 요청하며, 표시 문구는 여기서만 수정합니다.
        public static string GetDetailObjective(GameStage stage, ObjectiveStep step) => (stage, step) switch
        {
            (GameStage.Curtain, ObjectiveStep.FollowDog) => "겨울이를 따라가자",
            (GameStage.Curtain, ObjectiveStep.OpeningCurtains) => "커튼이 열린다",
            (GameStage.Wash, ObjectiveStep.WetFace) => "물로 얼굴을 적시자",
            (GameStage.Wash, ObjectiveStep.SelectSoap) => "비누를 집자",
            (GameStage.Wash, ObjectiveStep.ApplySoap) => "얼굴에 비누칠을 하자",
            (GameStage.Wash, ObjectiveStep.RinseFace) => "물로 거품을 헹구자",
            (GameStage.Wash, ObjectiveStep.Rinsing) => "거품이 씻겨 내려간다",
            (GameStage.Wash, ObjectiveStep.SelectTowel) => "수건을 집자",
            (GameStage.Wash, ObjectiveStep.WipeFace) => "수건으로 얼굴을 닦자",
            (GameStage.Wash, ObjectiveStep.WashComplete) => "세안을 마쳤다",
            (GameStage.Meal, ObjectiveStep.GetToast) => "냉장고에서 토스트를 꺼내자",
            (GameStage.Meal, ObjectiveStep.PutToastInToaster) => "토스트를 토스터기에 넣자",
            (GameStage.Meal, ObjectiveStep.GetJam) => "냉장고에서 잼을 꺼내자",
            (GameStage.Meal, ObjectiveStep.PutJamOnCounter) => "잼을 조리대에 놓자",
            (GameStage.Meal, ObjectiveStep.WaitForToast) => "토스트가 구워지기를 기다리자",
            (GameStage.Meal, ObjectiveStep.TakeToast) => "구워진 토스트를 꺼내자",
            (GameStage.Meal, ObjectiveStep.PutToastOnPlate) => "토스트를 접시에 놓자",
            (GameStage.Meal, ObjectiveStep.ApplyJam) => "토스트에 잼을 바르자",
            (GameStage.Meal, ObjectiveStep.PlacePlate) => "완성한 토스트를 식탁에 놓자",
            (GameStage.Meal, ObjectiveStep.GetJuice) => "냉장고에서 주스를 꺼내자",
            (GameStage.Meal, ObjectiveStep.PourJuice) => "빈 컵에 주스를 따르자",
            (GameStage.Meal, ObjectiveStep.PlaceCup) => "주스컵을 식탁에 놓자",
            (GameStage.Meal, ObjectiveStep.EatMeal) => "식사한다",
            (GameStage.Board, ObjectiveStep.ArrangeBoard) => "사진들을 게시판에 정리하자",
            (GameStage.Board, ObjectiveStep.ResolveOverlap) => "겹친 사진들을 정리하자",
            (GameStage.Board, ObjectiveStep.FinishBoard) => "게시판 정리를 마무리하자",
            (GameStage.Calendar, ObjectiveStep.MarkCalendar) => "달력에 날짜를 표시하자",
            _ => null
        };
        public const string CompletedMessage = "거실에서 할 일을 모두 마쳤다";

        static readonly Dictionary<GameStage, ObjectiveStep> details = new Dictionary<GameStage, ObjectiveStep>();
        static GameManager owner;
        static ObjectiveManager active;
        GameManager manager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { details.Clear(); owner = null; active = null; }

        static void EnsureOwner(GameManager current)
        {
            if (ReferenceEquals(owner, current)) return;
            details.Clear(); owner = current;
        }

        // 예: ObjectiveManager.Request(GameStage.Wash, ObjectiveStep.SelectSoap);
        // 다른 게임의 요청이나 완료된 게임의 늦은 요청은 현재 목표를 덮어쓰지 않습니다.
        public static bool Request(GameStage stage, ObjectiveStep step)
        {
            var current = GameManager.Instance;
            if (!current || current.CurrentStage != stage || current.IsComplete(stage)) return false;
            if (step != ObjectiveStep.Default && GetDetailObjective(stage, step) == null) return false;
            EnsureOwner(current);
            if (details.TryGetValue(stage, out var previous) && previous == step) return true;
            if (step == ObjectiveStep.Default) details.Remove(stage); else details[stage] = step;
            if (active) { active.Bind(); active.Refresh(); }
            return true;
        }
        void Start() { Bind(); }
        void OnEnable() { active = this; Bind(); }
        void Bind()
        {
            if (manager || !GameManager.Instance) return;
            manager = GameManager.Instance;
            EnsureOwner(manager);
            manager.Changed += Refresh;
            Refresh();
        }
        void OnDisable()
        {
            if (manager) manager.Changed -= Refresh;
            manager = null;
            if (active == this) active = null;
        }
        public void Refresh()
        {
            if (!manager) return;
            var content = GetComponent<SceneUIContent>();
            content.objectives.Clear();
            foreach (var stage in GameManager.Progression)
            {
                var text = details.TryGetValue(stage, out var step) ? GetDetailObjective(stage, step) : null;
                if (stage == GameStage.Toys && text == null)
                    text = $"{GetObjective(stage)} ({manager.CompletedToyCount}/{Room3FlowController.ToyIds.Length})";
                content.objectives.Add(new SceneUIContent.Objective {
                    id = stage.ToString(), text = text ?? GetObjective(stage),
                    completed = manager.IsComplete(stage), visible = stage == manager.CurrentStage
                });
            }
            content.completedMessage = CompletedMessage;
            content.Refresh();
        }
    }
}
