using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamRoulette.UI
{
    // Added only by the Editor validation command, never by the shipped prefab.
    public class UIRegressionProbe : MonoBehaviour
    {
        const string Report = "Assets/UI/Examples/Reports/ValidationReport.txt";
        int errors;
        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }
        void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += Log;
            var run = Run();
            while (true)
            {
                object next;
                try { if (!run.MoveNext()) break; next = run.Current; }
                catch (Exception e) { File.AppendAllText(Report, "FAIL " + e + "\n"); break; }
                yield return next;
            }
            Application.logMessageReceived -= Log;
        }
        IEnumerator Run()
        {
            var ui = FindFirstObjectByType<GameUI>();
            var content = ui.GetComponent<SceneUIContent>();
            var paw = ui.GetComponent<PawProgress>();
            Require(ui.objectiveText.text == content.objectives[0].text, "first scene objective displayed");
            Require(ui.keyGuides[0].key == "TAB" && ui.keyGuides[0].alwaysVisible &&
                ui.guideKeys[0].text == "TAB", "map is first and visible without an unlock");
            Require(ui.guideKeys.Length == ui.keyGuides.Count, "all key guides have rows");
            Require(ui.backButton.GetComponentInChildren<TMP_Text>().text == "돌아가기", "back button has no ESC suffix");
            int count = content.objectives.Count;
            content.AddObjective(content.objectives[0].id, "duplicate");
            Require(content.objectives.Count == count, "duplicate objective rejected");
            content.CompleteObjective(content.objectives[0].id);
            content.CompleteObjective(content.objectives[0].id);
            Require(ui.objectiveText.text == content.objectives[1].text, "completion advances once in registered order");
            foreach (var objective in content.objectives) content.CompleteObjective(objective.id);
            Require(ui.objectiveText.text == "목표 완료" && ui.objectiveText.gameObject.activeInHierarchy, "completed area remains visible");
            content.completedMessage = "이 방은 완료"; content.Refresh();
            Require(ui.objectiveText.text == "이 방은 완료", "completion message configurable");
            for (int i = 0; i < ui.keyGuides.Count; i++)
            {
                var guide = ui.keyGuides[i]; if (guide.alwaysVisible) continue;
                OptionState.Instance.Lock(guide.unlock);
                Require(ui.guideKeys[i].text == "?", guide.action + " locked guide");
                OptionState.Instance.Unlock(guide.unlock);
                Require(ui.guideKeys[i].text == guide.key, guide.action + " live unlock guide");
            }
            var dialogue = content.dialogue;
            Require(dialogue.GetLineColor(1) != Color.white, "per-line hex color parsed");
            var invalid = ScriptableObject.CreateInstance<DialogueSequence>();
            invalid.colorCodes = new[] { "invalid" };
            Require(invalid.GetLineColor(0) == Color.white, "invalid color safely defaults to white");
            Destroy(invalid);
            ui.ShowDialogue(dialogue);
            Require(ui.player.enabled && ui.motion.enabled, "dialogue leaves movement and camera enabled");
            Require(ui.dialogueText.text == dialogue.lines[0], "first dialogue line displayed immediately");
            yield return new WaitForSecondsRealtime(1.4f);
            Require(ui.dialogueText.text == dialogue.lines[0], "dialogue remains visible beyond one second");
            ui.OpenSettings();
            Require(Time.timeScale == 0 && !ui.player.enabled, "settings pauses gameplay");
            yield return new WaitForSecondsRealtime(1.3f);
            Require(ui.dialogueText.text == dialogue.lines[0] && ui.IsDialogueOpen, "settings pauses dialogue timer");
            Require(ui.blurredBackground.texture != null, "paused blurred background available");
            ui.CloseSettings();
            yield return null; yield return null;
            Require(ui.player.enabled && ui.IsDialogueOpen, "closing settings restores controls during dialogue");
            yield return new WaitForSecondsRealtime(.72f);
            Require(ui.dialogueText.text == dialogue.lines[1], "dialogue automatically advances after remaining time");
            Require(ui.dialogueText.color == dialogue.GetLineColor(1), "next line applies its own color");
            yield return new WaitForSecondsRealtime(2.05f);
            Require(ui.dialogueText.text == dialogue.lines[2], "third line displayed in order");
            yield return new WaitForSecondsRealtime(2.05f);
            Require(!ui.IsDialogueOpen && !ui.dialoguePanel.activeSelf, "last line disappears automatically");
            Require(ui.player.enabled, "controls remain enabled after dialogue");
            float music = ui.musicSlider.value, effects = ui.effectsSlider.value, sensitivity = ui.sensitivitySlider.value, brightness = ui.brightnessSlider.value;
            bool shake = ui.shakeToggle.isOn;
            ui.musicSlider.value = .31f; ui.effectsSlider.value = .43f; ui.sensitivitySlider.value = 3f; ui.brightnessSlider.value = .2f; ui.shakeToggle.isOn = false;
            Require(Mathf.Approximately(ui.musicSources[0].volume, .31f) && Mathf.Approximately(ui.effectSources[0].volume, .43f), "separate audio sliders applied");
            Require(Mathf.Approximately(ui.player.mouseSensitivity, 3f) && ui.motion.bobHeight == 0, "sensitivity and camera shake applied");
            Require(ui.brightnessValue.text == "20%", "brightness applied");
            ui.musicSlider.value = music; ui.effectsSlider.value = effects; ui.sensitivitySlider.value = sensitivity; ui.brightnessSlider.value = brightness; ui.shakeToggle.isOn = shake;
            paw.ResetProgress();
            Require(PawProgress.Count == 0 && paw.graphic.Progress == 0, "paw initially empty");
            for (int i = 1; i <= 5; i++)
            {
                paw.CompleteStage(i); paw.CompleteStage(i);
                Require(PawProgress.Count == i && paw.graphic.Progress == i, "paw stage " + i + " fills exactly once");
            }
            paw.CompleteStage(0); paw.CompleteStage(6);
            Require(PawProgress.Count == 5, "paw ignores invalid stages");
            ui.OpenSettings();
            yield return new WaitForSecondsRealtime(.2f);
            Canvas.ForceUpdateCanvases();
            Require(paw.graphic.canvasRenderer, "paw has a CanvasRenderer");
            var pawMesh = paw.graphic.canvasRenderer.GetMesh();
            Require(pawMesh.vertexCount == 325, "all five paw pads produce rendered geometry");

            var buttons = ui.settings.GetComponentsInChildren<Button>(true);
            Button FindButton(string name) => buttons.Single(button => button.name == name);
            var ratioDropdown = ui.settingsPages[0].GetComponentInChildren<TMP_Dropdown>();
            var originalRatio = PlayerPrefs.GetInt("Roulette.UI.Aspect");
            var camera = ui.player.GetComponentInChildren<Camera>();
            int width = Screen.width, height = Screen.height;
            Require(ratioDropdown && ratioDropdown.options.Select(option => option.text).SequenceEqual(GameViewport.Names),
                "aspect dropdown lists the four ratios in order");
            ratioDropdown.Show();
            Require(ratioDropdown.IsExpanded, "aspect dropdown opens");
            ratioDropdown.Hide();
            ratioDropdown.value = 2;
            Require(Screen.width == width && Screen.height == height, "aspect keeps window dimensions");
            Require(Mathf.Abs(camera.pixelWidth / (float)camera.pixelHeight - 1.6f) < .02f, "16:10 camera viewport");
            ratioDropdown.value = 0;
            Require(camera.rect == new Rect(0, 0, 1, 1), "automatic aspect restores full viewport");
            ratioDropdown.value = Mathf.Clamp(originalRatio, 0, 3);

            FindButton("Return To Title").onClick.Invoke();
            Require(FindButton("Yes").gameObject.activeInHierarchy &&
                ui.settings.GetComponentsInChildren<TMP_Text>().Any(t => t.text == GameUISettingsControls.ReturnMessage),
                "title return asks for confirmation");
            FindButton("No").onClick.Invoke();
            Require(!FindButton("Yes").gameObject.activeInHierarchy && ui.IsPaused && Time.timeScale == 0,
                "cancel keeps settings open and gameplay paused");

            ui.CloseSettings();
            yield return null; yield return null;
            ui.player.enabled = false; ui.OpenSettings(); ui.CloseSettings();
            yield return null; yield return null;
            Require(!ui.player.enabled, "previously disabled controller stays disabled");
            ui.player.enabled = true;
            var interactor = ui.player.gameObject.AddComponent<PlayerInteractor>();
            interactor.playerCamera = ui.player.GetComponentInChildren<Camera>();
            ui.OpenSettings();
            var closing = new GameObject("Closing frame probe").AddComponent<UIClosingFrameProbe>();
            closing.ui = ui; closing.interactor = interactor;
            yield return new WaitForSecondsRealtime(.2f);
            Require(closing.finished && closing.blockedThroughoutClosingFrame && closing.restoredOnFollowingFrame, "back click cannot leak into gameplay");
            ui.OpenSettings(); ui.enabled = false;
            Require(Time.timeScale == 1 && !ui.settings.activeSelf && ui.player.enabled, "disable paused UI restores gameplay");
            ui.enabled = true;
            OptionState.Instance.Lock(OptionType.Hand);
            int handGuide = ui.keyGuides.FindIndex(guide => !guide.alwaysVisible && guide.unlock == OptionType.Hand);
            Require(handGuide >= 0 && ui.guideKeys[handGuide].text == "?", "reenabled UI reconnects option events");
            var duplicate = Instantiate(ui.gameObject);
            Require(!duplicate.activeSelf, "duplicate UI disabled"); Destroy(duplicate);
            ui.OpenSettings();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/UI/Examples/UI_Workbench.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null; yield return null;
            ui = FindFirstObjectByType<GameUI>();
            Require(ui && !ui.IsPaused && Time.timeScale == 1, "scene change restores time");
            Require(ui.objectiveText.text == ui.GetComponent<SceneUIContent>().objectives[0].text, "new scene starts at its own first objective");
            Require(PawProgress.Count == 5 && ui.GetComponent<PawProgress>().graphic.Progress == 5, "paw survives scene changes");
            Require(FindObjectsByType<GameUI>(FindObjectsSortMode.None).Length == 1 && FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "one UI and EventSystem after scene change");
            ui.GetComponent<PawProgress>().ResetProgress();
            Require(errors == 0, "no runtime errors or exceptions");
            File.AppendAllText(Report, "ALL CHECKS PASSED\n");
        }
        void OnDestroy() { Application.logMessageReceived -= Log; }
    }
}
