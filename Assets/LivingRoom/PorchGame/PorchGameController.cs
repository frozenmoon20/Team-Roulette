using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TeamRoulette.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamRoulette.Room3
{
    public sealed class PorchGameController : MonoBehaviour
    {
        [Header("엔딩 크레딧")]
        [TextArea(5, 30)] public string creditsText = "게임이 끝났습니다.\n엔딩 크레딧이 내려갑니다.";
        public TMP_FontAsset font;
        [Min(1)] public float fontSize = 48;
        [Tooltip("크레딧이 1초에 올라가는 거리입니다.")]
        [Min(1)] public float scrollSpeed = 70;
        [Tooltip("크레딧이 지나간 뒤 타이틀로 이동하기 전 대기 시간입니다. 단위는 초입니다.")]
        [Min(0)] public float endDelay = 2;
        [Header("크레딧 이후 이동할 씬")]
        [Tooltip("타이틀로 이동하려면 빌드 목록에 있는 씬 이름(예: Title) 또는 경로를 입력하세요.")]
        public string titleSceneName = "";
        [TextArea(2, 5)] public string missingTitleMessage = "타이틀 화면 준비 중입니다.";
        public bool IsRunning { get; private set; }
        public bool CreditsFinished { get; private set; }
        readonly List<AudioSource> pausedAudio = new List<AudioSource>();
        TextMeshProUGUI label;
        Canvas canvas;
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
            if (!GameManager.ReportComplete(GameStage.Entrance)) { IsRunning = false; return; }
            GameManager.Instance.Flow.BeginEnding();
            savedTimeScale = Time.timeScale;
            savedCursor = Cursor.lockState; savedVisible = Cursor.visible;
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
            StartCoroutine(RollCredits());
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
            label.text = creditsText;
            var textRect = label.rectTransform;
            textRect.anchorMin = new Vector2(.1f, 0); textRect.anchorMax = new Vector2(.9f, 0);
            textRect.pivot = new Vector2(.5f, 0); textRect.sizeDelta = Vector2.zero;
        }

        IEnumerator RollCredits()
        {
            Canvas.ForceUpdateCanvases();
            var viewport = (RectTransform)label.transform.parent;
            float height = Mathf.Max(fontSize, label.GetPreferredValues(creditsText, viewport.rect.width * .8f, Mathf.Infinity).y);
            label.rectTransform.sizeDelta = new Vector2(0, height);
            float y = -height - 40;
            while (y < viewport.rect.height + 40)
            {
                label.rectTransform.anchoredPosition = new Vector2(0, y);
                yield return null;
                y += Mathf.Max(1, scrollSpeed) * Time.unscaledDeltaTime;
            }
            label.text = "";
            var roomAudio = RoomAudio.For(this);
            roomAudio?.Stop("Credits Music");
            roomAudio?.Play("Credits End Door");
            yield return new WaitForSecondsRealtime(Mathf.Max(endDelay, roomAudio?.ClipDuration("Credits End Door") ?? 0f));
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
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = new Vector2(80, 80); rect.offsetMax = new Vector2(-80, -80);
        }

        void LateUpdate()
        {
            if (!IsRunning) return;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
        }

        void OnDestroy()
        {
            if (!IsRunning) return;
            Time.timeScale = transitioningToTitle ? 1 : savedTimeScale;
            Cursor.lockState = transitioningToTitle ? CursorLockMode.None : savedCursor;
            Cursor.visible = transitioningToTitle || savedVisible;
            // The credits end the room permanently. Reactivating scene roots here
            // can run their OnEnable callbacks after sibling objects are destroyed.
            foreach (var source in pausedAudio) if (source) source.UnPause();
        }
    }
}
