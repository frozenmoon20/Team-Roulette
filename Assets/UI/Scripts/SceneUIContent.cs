using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TeamRoulette.UI
{
    // Scene-local content. Put this on the UI instance and edit it in the Inspector.
    public class SceneUIContent : MonoBehaviour
    {
        [Serializable]
        public class Objective
        {
            public string id;
            [TextArea] public string text;
            public bool visible = true;
            public bool completed;
        }

        [Header("이 씬의 목표")]
        public List<Objective> objectives = new List<Objective>();
        [Header("독백 (선택)")]
        public DialogueSequence dialogue;
        public bool playDialogueOnStart;
        public UnityEvent onDialogueFinished = new UnityEvent();
        public event Action Changed;

        void Start()
        {
            if (playDialogueOnStart) PlayDialogue();
        }

        public void PlayDialogue()
        {
            GetComponent<GameUI>()?.ShowDialogue(dialogue);
        }

        public void CompleteObjective(string id)
        {
            var item = objectives.Find(x => x != null && x.id == id);
            if (item == null || item.completed) return;
            item.visible = true;
            item.completed = true;
            Changed?.Invoke();
        }

        public void RevealObjective(string id)
        {
            var item = objectives.Find(x => x != null && x.id == id);
            if (item == null || item.visible) return;
            item.visible = true;
            Changed?.Invoke();
        }

        public void AddObjective(string id, string text)
        {
            if (string.IsNullOrWhiteSpace(id) || objectives.Exists(x => x != null && x.id == id)) return;
            objectives.Add(new Objective { id = id, text = text });
            Changed?.Invoke();
        }

        public void Refresh() => Changed?.Invoke();
    }
}
