using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TeamRoulette.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamRoulette.Room3
{
    public sealed class PorchGameController : MonoBehaviour
    {
        public const string CreditsFileName = "EndingCredits.md";
        public const string CreditsSourcePath = "LivingRoom/PorchGame/" + CreditsFileName;

        [Header("엔딩 크레딧")]
        [SerializeField, HideInInspector] string creditsText = "게임이 끝났습니다.\n엔딩 크레딧이 내려갑니다.";
        public TMP_FontAsset font;
        [Min(1)] public float fontSize = 48;
        [Tooltip("크레딧이 1초에 올라가는 거리입니다.")]
        [Min(1)] public float scrollSpeed = 70;
        [Tooltip("크레딧 중 마우스 왼쪽 버튼을 누르고 있을 때 적용하는 속도 배율입니다.")]
        [Min(1)] public float heldScrollMultiplier = 4;
        [Tooltip("크레딧이 지나간 뒤 타이틀로 이동하기 전 대기 시간입니다. 단위는 초입니다.")]
        [Min(0)] public float endDelay = 2;
        [Header("크레딧 이후 이동할 씬")]
        [Tooltip("타이틀로 이동하려면 빌드 목록에 있는 씬 이름(예: Title) 또는 경로를 입력하세요.")]
        public string titleSceneName = "Title";
        [TextArea(2, 5)] public string missingTitleMessage = "타이틀 화면 준비 중입니다.";
        public bool IsRunning { get; private set; }
        public bool CreditsFinished { get; private set; }
        readonly List<AudioSource> pausedAudio = new List<AudioSource>();
        TextMeshProUGUI label;
        Canvas canvas;
        DialogueSequence endingDialogue;
        bool creditsStarted;
        bool transitioningToTitle;
        float savedTimeScale;
        CursorLockMode savedCursor;
        bool savedVisible;

        public bool CanStart => GameManager.Instance && !IsRunning &&
            GameManager.Instance.CurrentStage == GameStage.Entrance &&
            GameManager.Progression.Where(s => s != GameStage.Entrance).All(GameManager.Instance.IsComplete);

        public void Interact()
        {
            if (!isActiveAndEnabled || !CanStart || Time.timeScale <= 0) return;
            var ui = FindFirstObjectByType<GameUI>();
            if ((ui && ui.IsPaused) || (PlayerHand.Instance && PlayerHand.Instance.Held)) return;
            if (!GameManager.TryBegin(GameStage.Entrance)) return;
            IsRunning = true;
            StartCoroutine(PlayEnding(ui));
        }

        IEnumerator PlayEnding(GameUI ui)
        {
            var dialogue = ui ? ui.GetComponent<DialogueManager>() : null;
            while ((dialogue && dialogue.HasDialogue) || (ui && ui.IsPaused))
                yield return null;

            if (!GameManager.ReportComplete(GameStage.Entrance))
            {
                IsRunning = false;
                yield break;
            }
            GameManager.Instance.Flow.BeginEnding();
            savedTimeScale = Time.timeScale;
            savedCursor = Cursor.lockState; savedVisible = Cursor.visible;
            creditsStarted = true;
            if (!font) font = ui && ui.objectiveText ? ui.objectiveText.font : TMP_Settings.defaultFontAsset;
            var roomAudio = RoomAudio.For(this);
            roomAudio?.StopAll();
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source.isPlaying) { pausedAudio.Add(source); source.Pause(); }
            // Keep the credits root alive while suspending the room and all its input/UI.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root != transform.root.gameObject && (!roomAudio || root != roomAudio.transform.root.gameObject) && root.activeSelf)
                    root.SetActive(false);
            BuildScreen();
            roomAudio?.Play("Porch Door");
            roomAudio?.Play("Credits Music");
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            yield return RollCredits(ui);
        }

        void BuildScreen()
        {
            var cameraObject = new GameObject("Ending Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 0;
            var screen = new GameObject("Ending Credits", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            screen.transform.SetParent(transform, false);
            canvas = screen.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
            var scaler = screen.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var background = new GameObject("Black", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            background.transform.SetParent(screen.transform, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = Color.black;
            var text = new GameObject("Credits Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(background.transform, false);
            label = text.GetComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = fontSize; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            var textRect = label.rectTransform;
            textRect.anchorMin = new Vector2(.1f, 0); textRect.anchorMax = new Vector2(.9f, 0);
            textRect.pivot = new Vector2(.5f, 0); textRect.sizeDelta = Vector2.zero;
        }

        public string ReadCredits()
        {
#if UNITY_EDITOR
            var path = Path.Combine(Application.dataPath, CreditsSourcePath);
#else
            var path = Path.Combine(Application.streamingAssetsPath, CreditsFileName);
#endif
            try
            {
                var markdown = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(markdown))
                    return FormatCreditsMarkdown(markdown);
            }
            catch (IOException error)
            {
                Debug.LogWarning($"Could not read ending credits: {error.Message}", this);
            }
            catch (System.UnauthorizedAccessException error)
            {
                Debug.LogWarning($"Could not read ending credits: {error.Message}", this);
            }
            return creditsText;
        }

        static string FormatCreditsMarkdown(string markdown)
        {
            var lines = markdown.TrimEnd().Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = Regex.Replace(lines[i], @"\*\*(.+?)\*\*", "<b>$1</b>");
                var heading = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
                if (heading.Success)
                {
                    int size = heading.Groups[1].Length == 1 ? 160 :
                        heading.Groups[1].Length == 2 ? 130 : 112;
                    line = $"<size={size}%><b>{heading.Groups[2].Value}</b></size>";
                }
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                    line = "• " + line.Substring(2);
                lines[i] = line;
            }
            return string.Join("\n", lines);
        }

        IEnumerator RollCredits(GameUI ui)
        {
            Canvas.ForceUpdateCanvases();
            var viewport = (RectTransform)label.transform.parent;
            string displayText = ReadCredits();
            float height = Mathf.Max(fontSize, label.GetPreferredValues(displayText, viewport.rect.width * .8f, Mathf.Infinity).y);
            label.rectTransform.sizeDelta = new Vector2(0, height);
            label.text = displayText;
            float y = -height - 40;
            while (y < viewport.rect.height + 40)
            {
                label.rectTransform.anchoredPosition = new Vector2(0, y);
                yield return null;
                float multiplier = Input.GetMouseButton(0) ? Mathf.Max(1, heldScrollMultiplier) : 1;
                y += Mathf.Max(1, scrollSpeed) * multiplier * Time.unscaledDeltaTime;
            }
            label.text = "";
            var roomAudio = RoomAudio.For(this);
            roomAudio?.Stop("Credits Music");
            float endSoundStartedAt = Time.realtimeSinceStartup;
            roomAudio?.Play("Credits End Door");
            if (ui && ui.canvas && ui.dialoguePanel && ui.dialoguePanel.transform.IsChildOf(ui.canvas.transform))
            {
                ui.transform.root.gameObject.SetActive(true);
                // Keep the dialogue panel in its original layout while hiding the rest of the HUD.
                for (var branch = ui.dialoguePanel.transform; branch != ui.canvas.transform; branch = branch.parent)
                    foreach (Transform sibling in branch.parent)
                        if (sibling != branch) sibling.gameObject.SetActive(false);
                canvas.sortingOrder = ui.canvas.sortingOrder - 1;
                endingDialogue = ScriptableObject.CreateInstance<DialogueSequence>();
                endingDialogue.lines = Room3DialogueScript.EntranceFinish;
                ui.ShowDialogueWhileFrozen(endingDialogue);
                while (ui && ui.isActiveAndEnabled && ui.IsDialogueOpen) yield return null;
                Destroy(endingDialogue);
                endingDialogue = null;
            }
            float remainingDelay = Mathf.Max(endDelay, roomAudio?.ClipDuration("Credits End Door") ?? 0f) -
                (Time.realtimeSinceStartup - endSoundStartedAt);
            if (remainingDelay > 0f) yield return new WaitForSecondsRealtime(remainingDelay);
            CreditsFinished = true;
            if (!string.IsNullOrWhiteSpace(titleSceneName) &&
                titleSceneName != gameObject.scene.name && titleSceneName != gameObject.scene.path &&
                Application.CanStreamedLevelBeLoaded(titleSceneName))
            {
                AsyncOperation load = null;
                try { load = SceneManager.LoadSceneAsync(titleSceneName, LoadSceneMode.Single); }
                catch (System.Exception error) { Debug.LogException(error, this); }
                if (load != null)
                {
                    transitioningToTitle = true;
                    Time.timeScale = 1;
                    Cursor.visible = true;
                    // A new playthrough must receive a fresh Room3 progression manager.
                    if (GameManager.Instance) Destroy(GameManager.Instance.gameObject);
                    yield break;
                }
            }
            label.text = missingTitleMessage;
            label.alignment = TextAlignmentOptions.Center;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = new Vector2(80, 80); rect.offsetMax = new Vector2(-80, -80);
        }

        void LateUpdate()
        {
            if (!creditsStarted || transitioningToTitle) return;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
        }

        void OnDestroy()
        {
            if (endingDialogue) Destroy(endingDialogue);
            if (!creditsStarted) return;
            Time.timeScale = transitioningToTitle ? 1 : savedTimeScale;
            Cursor.lockState = transitioningToTitle ? CursorLockMode.None : savedCursor;
            Cursor.visible = transitioningToTitle || savedVisible;
            // The credits end the room permanently. Reactivating scene roots here
            // can run their OnEnable callbacks after sibling objects are destroyed.
            foreach (var source in pausedAudio) if (source) source.UnPause();
        }
    }
}
