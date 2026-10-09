using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TeamRoulette.UI.Editor
{
    [InitializeOnLoad]
    public static class UIValidation
    {
        const string Request = "Assets/UI/Editor/Validate.request";
        const string Report = "Assets/UI/Examples/Reports/ValidationReport.txt";
        static double next;
        static UIValidation()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetInt("RouletteUI.ValidationStage", 0) > 0 && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    SessionState.SetInt("RouletteUI.ValidationErrors", SessionState.GetInt("RouletteUI.ValidationErrors", 0) + 1);
            };
        }
        static void Require(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }
        [MenuItem("Tools/Team Roulette UI/Validate Workbench")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != "Assets/UI/Examples/UI_Workbench.unity") return;
            File.WriteAllText(Request, "validate");
            SessionState.SetInt("RouletteUI.ValidationStage", 0);
        }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next || !File.Exists(Request)) return;
            next = EditorApplication.timeSinceStartup + .3;
            int stage = SessionState.GetInt("RouletteUI.ValidationStage", 0);
            try
            {
                if (stage == 0)
                {
                    if (SceneManager.GetActiveScene().path != "Assets/UI/Examples/UI_Workbench.unity" || EditorApplication.isPlayingOrWillChangePlaymode) return;
                    File.WriteAllText(Report, "UI Workbench runtime validation\n");
                    SessionState.SetInt("RouletteUI.ValidationErrors", 0);
                    SessionState.SetInt("RouletteUI.ValidationStage", 1);
                    EditorApplication.isPlaying = true;
                    return;
                }
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                var ui = UnityEngine.Object.FindFirstObjectByType<GameUI>();
                if (!ui || Time.frameCount < 10) return;
                var content = ui.GetComponent<SceneUIContent>();
                if (stage == 1)
                {
                    Require(ui.objectiveCount.text == "0 / 3", "initial objective count");
                    content.AddObjective("curtain", "duplicate");
                    Require(content.objectives.Count == 3, "duplicate objective rejected");
                    content.CompleteObjective("curtain"); content.CompleteObjective("curtain");
                    Require(ui.objectiveCount.text == "1 / 3", "repeated completion counted once");
                    var rows = ui.objectiveList.GetComponentsInChildren<ObjectiveRow>();
                    Require(rows.Last().label.text == "커튼을 열어보자" && rows.Last().check.activeSelf, "completed objective moved to bottom with check");
                    Require(ui.guideKeys[0].text == "?", "locked guide displays question mark");
                    OptionState.Instance.Unlock(OptionType.Hand);
                    Require(ui.guideKeys[0].text == "마우스 왼쪽", "unlock updates key guide");
                    ui.ShowDialogue(content.dialogue);
                    Require(ui.IsDialogueOpen && !ui.player.enabled, "dialogue blocks player controller");
                    Time.timeScale = .75f;
                    ui.OpenSettings();
                    Require(ui.IsPaused && Time.timeScale == 0, "settings pauses simulation");
                    SessionState.SetInt("RouletteUI.ValidationStage", 2); next += 2;
                }
                else if (stage == 2)
                {
                    Require(ui.blurredBackground.texture != null, "paused background screenshot captured and blurred");
                    Require(Cursor.visible && Cursor.lockState == CursorLockMode.None, "settings cursor available");
                    ui.CloseSettings();
                    Require(ui.IsDialogueOpen && !ui.player.enabled && Mathf.Approximately(Time.timeScale, .75f), "closing settings restores dialogue and previous timescale");
                    ui.AdvanceDialogue(); ui.AdvanceDialogue();
                    SessionState.SetInt("RouletteUI.ValidationStage", 3);
                }
                else if (stage == 3)
                {
                    Require(!ui.IsDialogueOpen && ui.player.enabled, "dialogue restores previous controller state");
                    ui.player.enabled = false;
                    ui.OpenSettings();
                    SessionState.SetInt("RouletteUI.ValidationStage", 4); next += 1;
                }
                else if (stage == 4)
                {
                    ui.CloseSettings();
                    SessionState.SetInt("RouletteUI.ValidationStage", 5);
                }
                else if (stage == 5)
                {
                    Require(!ui.player.enabled, "pre-disabled controller remains disabled");
                    ui.ToggleObjectives();
                    SessionState.SetInt("RouletteUI.ValidationStage", 6); next += 1;
                }
                else if (stage == 6)
                {
                    Require(ui.objectiveBody.sizeDelta.y < 1, "objective panel collapses");
                    ui.ToggleObjectives();
                    SessionState.SetInt("RouletteUI.ValidationStage", 7); next += 1;
                }
                else if (stage == 7)
                {
                    Require(ui.objectiveBody.sizeDelta.y > 240, "objective panel expands");
                    Require(ui.brightnessSlider && ui.musicSlider && ui.effectsSlider && ui.sensitivitySlider && ui.shakeToggle, "basic setting controls wired");
                    float oldMusic = ui.musicSlider.value, oldEffect = ui.effectsSlider.value, oldSensitivity = ui.sensitivitySlider.value, oldBrightness = ui.brightnessSlider.value;
                    bool oldShake = ui.shakeToggle.isOn;
                    ui.musicSlider.value = .3f;
                    ui.effectsSlider.value = .4f;
                    ui.sensitivitySlider.value = 3f;
                    ui.brightnessSlider.value = .2f;
                    ui.shakeToggle.isOn = false;
                    Require(Mathf.Approximately(ui.musicSources[0].volume, .3f) && Mathf.Approximately(ui.effectSources[0].volume, .4f), "separate music and effects sliders applied");
                    Require(Mathf.Approximately(ui.player.mouseSensitivity, 3f), "mouse sensitivity applied");
                    Require(ui.motion.bobHeight == 0 && ui.motion.bobSide == 0, "camera shake disabled");
                    Require(ui.brightnessOverlay.color.a > 0 && ui.brightnessValue.text == "20%", "brightness control applied");
                    ui.musicSlider.value = oldMusic; ui.effectsSlider.value = oldEffect; ui.sensitivitySlider.value = oldSensitivity; ui.brightnessSlider.value = oldBrightness; ui.shakeToggle.isOn = oldShake;
                    ui.player.enabled = true;
                    var interactor = ui.player.gameObject.AddComponent<PlayerInteractor>();
                    interactor.playerCamera = ui.player.GetComponentInChildren<Camera>();
                    ui.OpenSettings();
                    var probe = new GameObject("Closing frame regression probe").AddComponent<UIClosingFrameProbe>();
                    probe.ui = ui; probe.interactor = interactor;
                    SessionState.SetInt("RouletteUI.ValidationStage", 8);
                    next += 1;
                }
                else if (stage == 8)
                {
                    var probe = UnityEngine.Object.FindFirstObjectByType<UIClosingFrameProbe>();
                    Require(probe && probe.finished && probe.blockedThroughoutClosingFrame, "world interaction stays blocked through closing-frame LateUpdate");
                    Require(probe.restoredOnFollowingFrame, "world interaction restored on following frame");
                    ui.OpenSettings();
                    ui.enabled = false;
                    Require(!ui.IsPaused && Mathf.Approximately(Time.timeScale, .75f) && !ui.settings.activeSelf, "disabling UI while paused restores time and hides settings");
                    ui.enabled = true;
                    Require(!ui.IsDialogueOpen && !ui.settings.activeSelf && ui.hud.activeSelf, "reenabling UI starts in clean HUD state");
                    content.CompleteObjective("wash");
                    Require(ui.objectiveCount.text == "2 / 3", "objective events remain connected after reenable");
                    OptionState.Instance.Lock(OptionType.Hand);
                    Require(ui.guideKeys[0].text == "?", "option event remains connected after reenable");
                    ui.ShowDialogue(content.dialogue);
                    ui.enabled = false;
                    Require(!ui.IsDialogueOpen && !ui.dialoguePanel.activeSelf && ui.player.enabled, "disabling dialogue UI restores player and hides dialogue");
                    ui.enabled = true;
                    var duplicate = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/GameUI.prefab"));
                    Require(!duplicate.activeSelf, "duplicate UI automatically disabled");
                    UnityEngine.Object.Destroy(duplicate);
                    Require(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "single active EventSystem");
                    SessionState.SetInt("RouletteUI.ValidationStage", 9);
                }
                else if (stage == 9)
                {
                    ui.OpenSettings();
                    Require(Time.timeScale == 0, "scene transition begins while paused");
                    EditorSceneManager.LoadSceneInPlayMode("Assets/UI/Examples/UI_Workbench.unity", new LoadSceneParameters(LoadSceneMode.Single));
                    SessionState.SetInt("RouletteUI.ValidationStage", 10);
                    next += 2;
                }
                else if (stage == 10)
                {
                    Require(!ui.IsPaused && Mathf.Approximately(Time.timeScale, .75f), "scene transition restores previous time scale");
                    Require(ui.objectiveCount.text == "0 / 3", "scene objectives reset without carrying previous state");
                    Require(UnityEngine.Object.FindObjectsByType<GameUI>(FindObjectsSortMode.None).Length == 1, "one active UI after scene transition");
                    Require(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "one active EventSystem after scene transition");
                    Require(SessionState.GetInt("RouletteUI.ValidationErrors", 0) == 0, "no runtime errors or exceptions during validation");
                    File.AppendAllText(Report, "ALL CHECKS PASSED\n");
                    Finish();
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(Report, "FAIL " + ex + "\n");
                Finish();
            }
        }
        static void Finish()
        {
            AssetDatabase.DeleteAsset(Request);
            SessionState.SetInt("RouletteUI.ValidationStage", 0);
            Time.timeScale = 1;
            EditorApplication.isPlaying = false;
        }
    }
}
