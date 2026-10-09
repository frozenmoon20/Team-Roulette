using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamRoulette.UI
{
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(SceneUIContent))]
    public class GameUI : MonoBehaviour
    {
        [Serializable]
        public class KeyGuide
        {
            public string action;
            public string key;
            public bool alwaysVisible;
            public OptionType unlock;
        }

        [Header("조작키 안내 (키 변경 기능 없음)")]
        public List<KeyGuide> keyGuides = new List<KeyGuide>();
        [Header("기존 게임 연결 (비워두면 현재 씬에서 자동 탐색)")]
        public FirstPersonPlayer player;
        public PlayerMotionFeel motion;
        [Tooltip("독백 / 설정 중 추가로 멈출 스크립트")]
        public Behaviour[] extraInputToBlock = Array.Empty<Behaviour>();
        [Tooltip("배경음악 재생용 AudioSource를 연결하세요.")]
        public AudioSource[] musicSources = Array.Empty<AudioSource>();
        [Tooltip("AudioManager는 자동 연결됩니다. 추가 효과음 소스만 연결하세요.")]
        public AudioSource[] effectSources = Array.Empty<AudioSource>();

        [Header("UI 연결")]
        public Canvas canvas;
        public GameObject hud;
        public GameObject dialoguePanel;
        public TMP_Text dialogueText;
        public TMP_Text dialoguePage;
        public RectTransform objectiveBody;
        public CanvasGroup objectiveGroup;
        public RectTransform objectiveList;
        public ObjectiveRow objectiveTemplate;
        public TMP_Text objectiveCount;
        public TMP_Text objectiveArrow;
        public GameObject settings;
        public GameObject[] settingsPages;
        public Button[] tabButtons;
        public Button backButton;
        public Slider brightnessSlider, musicSlider, effectsSlider, sensitivitySlider;
        public TMP_Text brightnessValue, musicValue, effectsValue, sensitivityValue;
        public Toggle shakeToggle;
        public TMP_Text[] guideActions, guideKeys;
        public Image brightnessOverlay;
        public RawImage blurredBackground;
        public Material blurMaterial;

        readonly Dictionary<Behaviour, bool> blocked = new Dictionary<Behaviour, bool>();
        readonly Dictionary<GameObject, bool> hidden = new Dictionary<GameObject, bool>();
        readonly List<ObjectiveRow> rows = new List<ObjectiveRow>();
        static GameUI inputOwner;
        SceneUIContent content;
        DialogueSequence currentDialogue;
        int line, earliestAdvanceFrame, releaseAfterFrame;
        bool initialized;
        bool pause, capturedInput, expanded = true, releasePending;
        float savedTimeScale, targetHeight = 248f;
        CursorLockMode savedCursor;
        bool savedCursorVisible;
        float originalBobHeight, originalBobSide;
        bool capturedBob;
        Coroutine captureRoutine;
        RenderTexture backdrop;
        Material runtimeBlur;
        ScrollRect objectiveScroll;
        public bool IsPaused => pause;
        public bool IsDialogueOpen => currentDialogue != null;

        void Awake()
        {
            if (DisableDuplicate()) return;
            content = GetComponent<SceneUIContent>();
            if (!player) player = FindFirstObjectByType<FirstPersonPlayer>();
            if (!motion) motion = FindFirstObjectByType<PlayerMotionFeel>();
            if (motion) { originalBobHeight = motion.bobHeight; originalBobSide = motion.bobSide; capturedBob = true; }
            settings.SetActive(false);
            dialoguePanel.SetActive(false);
            blurredBackground.gameObject.SetActive(false);
            objectiveTemplate.gameObject.SetActive(false);
            objectiveScroll = objectiveList.GetComponentInParent<ScrollRect>();
            blurredBackground.color = new Color(.45f, .45f, .45f, 1f);
            backButton.onClick.AddListener(CloseSettings);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int page = i;
                tabButtons[i].onClick.AddListener(() => SelectPage(page));
            }
            brightnessSlider.onValueChanged.AddListener(SetBrightness);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            effectsSlider.onValueChanged.AddListener(SetEffectsVolume);
            sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
            shakeToggle.onValueChanged.AddListener(SetShake);
            brightnessSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Roulette.UI.Brightness", .5f));
            musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Roulette.UI.Music", 1f));
            effectsSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Roulette.UI.Effects", 1f));
            sensitivitySlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Roulette.UI.Sensitivity", 2f));
            shakeToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("Roulette.UI.Shake", 1) == 1);
            if (blurMaterial) runtimeBlur = new Material(blurMaterial);
            initialized = true;
        }

        bool DisableDuplicate()
        {
            foreach (var other in FindObjectsByType<GameUI>(FindObjectsSortMode.None))
                if (other != this && other.initialized && other.isActiveAndEnabled && other.gameObject.scene == gameObject.scene)
                {
                    Debug.LogWarning("이 씬에 GameUI가 이미 있어 중복 UI를 비활성화했습니다.", this);
                    gameObject.SetActive(false);
                    return true;
                }
            return false;
        }

        void OnEnable()
        {
            if (!initialized || DisableDuplicate()) return;
            content.Changed += RefreshObjectives;
            OptionState.OnOptionChanged += OptionChanged;
            RefreshObjectives();
            RefreshGuides();
        }

        void Start()
        {
            if (!EventSystem.current)
            {
                var events = new GameObject("UI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform);
            }
            RefreshObjectives();
            RefreshGuides();
            SelectPage(0);
            ApplySettings();
        }

        void Update()
        {
            if (inputOwner && inputOwner != this) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (pause) CloseSettings(); else OpenSettings();
                return;
            }
            if (pause) return;
            if (Input.GetKeyDown(KeyCode.Tab)) ToggleObjectives();
            // The cursor stays locked during play; the wheel scrolls the open list.
            if (expanded && !currentDialogue && objectiveScroll && Input.mouseScrollDelta.y != 0)
            {
                float overflow = objectiveList.rect.height - objectiveScroll.viewport.rect.height;
                if (overflow > 0) objectiveScroll.verticalNormalizedPosition = Mathf.Clamp01(objectiveScroll.verticalNormalizedPosition + Input.mouseScrollDelta.y * 54 / overflow);
            }
            if (currentDialogue && Time.frameCount > earliestAdvanceFrame && Input.GetKeyDown(KeyCode.Space)) AdvanceDialogue();
        }

        void LateUpdate()
        {
            // Keep gameplay blocked for the entire closing frame, including
            // PlayerInteractor.LateUpdate, so the back-button click cannot pick up an item.
            if (releasePending && Time.frameCount > releaseAfterFrame && !pause && !currentDialogue) { ReleaseInput(); releasePending = false; }
            float h = Mathf.MoveTowards(objectiveBody.sizeDelta.y, expanded ? targetHeight : 0, 1400 * Time.unscaledDeltaTime);
            objectiveBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            objectiveGroup.alpha = Mathf.Clamp01(h / targetHeight);
            objectiveGroup.blocksRaycasts = expanded;
            if (pause || currentDialogue)
            {
                Cursor.lockState = pause ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = pause;
            }
        }

        public void ToggleObjectives()
        {
            expanded = !expanded;
            objectiveArrow.text = expanded ? "TAB  −" : "TAB  +";
        }

        public void RefreshObjectives()
        {
            foreach (var row in rows) { if (row) { row.gameObject.SetActive(false); Destroy(row.gameObject); } }
            rows.Clear();
            var seen = new HashSet<string>();
            var active = new List<SceneUIContent.Objective>();
            foreach (var item in content.objectives)
                if (item != null && item.visible && !string.IsNullOrWhiteSpace(item.id) && seen.Add(item.id)) active.Add(item);
            int completed = 0;
            for (int pass = 0; pass < 2; pass++)
                foreach (var item in active)
                {
                    if (item.completed != (pass == 1)) continue;
                    var row = Instantiate(objectiveTemplate, objectiveList);
                    row.gameObject.SetActive(true);
                    row.Set(item.text, item.completed);
                    rows.Add(row);
                    if (item.completed) completed++;
                }
            objectiveCount.text = completed + " / " + active.Count;
        }

        public void ShowDialogue(DialogueSequence sequence)
        {
            if (!isActiveAndEnabled || (inputOwner && inputOwner != this)) return;
            if (!sequence || sequence.lines == null || sequence.lines.Length == 0 || currentDialogue) return;
            currentDialogue = sequence;
            line = 0;
            earliestAdvanceFrame = Time.frameCount;
            dialoguePanel.SetActive(true);
            RenderLine();
            BlockInput();
        }

        void RenderLine()
        {
            dialogueText.text = currentDialogue.lines[line];
            dialoguePage.text = "SPACE    " + (line + 1) + " / " + currentDialogue.lines.Length;
        }

        public void AdvanceDialogue()
        {
            if (pause || !currentDialogue) return;
            if (++line < currentDialogue.lines.Length) { RenderLine(); return; }
            currentDialogue = null;
            dialoguePanel.SetActive(false);
            releasePending = true;
            releaseAfterFrame = Time.frameCount;
            content.onDialogueFinished.Invoke();
        }

        public void OpenSettings()
        {
            if (pause || !isActiveAndEnabled || (inputOwner && inputOwner != this)) return;
            pause = true;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0;
            BlockInput();
            hud.SetActive(false);
            settings.SetActive(true);
            RefreshGuides();
            SelectPage(0);
            captureRoutine = StartCoroutine(CaptureBackground());
        }

        IEnumerator CaptureBackground()
        {
            canvas.enabled = false;
            yield return new WaitForEndOfFrame();
            var screen = ScreenCapture.CaptureScreenshotAsTexture();
            canvas.enabled = true;
            if (screen)
            {
                ReleaseBackdrop();
                int w = Mathf.Max(1, screen.width / 4), h = Mathf.Max(1, screen.height / 4);
                backdrop = new RenderTexture(w, h, 0) { filterMode = FilterMode.Bilinear };
                var temp = RenderTexture.GetTemporary(w, h, 0);
                Graphics.Blit(screen, backdrop);
                if (runtimeBlur)
                    for (int i = 0; i < 3; i++) { Graphics.Blit(backdrop, temp, runtimeBlur); Graphics.Blit(temp, backdrop, runtimeBlur); }
                RenderTexture.ReleaseTemporary(temp);
                Destroy(screen);
                blurredBackground.texture = backdrop;
                blurredBackground.gameObject.SetActive(true);
            }
            captureRoutine = null;
        }

        public void CloseSettings()
        {
            if (!pause) return;
            if (captureRoutine != null) { StopCoroutine(captureRoutine); captureRoutine = null; }
            canvas.enabled = true;
            pause = false;
            Time.timeScale = savedTimeScale;
            settings.SetActive(false);
            hud.SetActive(true);
            blurredBackground.gameObject.SetActive(false);
            ReleaseBackdrop();
            earliestAdvanceFrame = Time.frameCount;
            if (!currentDialogue) { releasePending = true; releaseAfterFrame = Time.frameCount; }
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            PlayerPrefs.Save();
        }

        void Remember(Behaviour component)
        {
            if (!component || component == this || blocked.ContainsKey(component)) return;
            blocked.Add(component, component.enabled);
            component.enabled = false;
        }

        void Hide(GameObject item)
        {
            if (!item || hidden.ContainsKey(item)) return;
            hidden.Add(item, item.activeSelf);
            item.SetActive(false);
        }

        void BlockInput()
        {
            if (capturedInput) return;
            capturedInput = true;
            inputOwner = this;
            savedCursor = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            Remember(player);
            Remember(motion);
            foreach (var interactor in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
            {
                Remember(interactor);
                Hide(interactor.crosshairDot);
                Hide(interactor.handIcon);
                if (interactor.nameText) Hide(interactor.nameText.gameObject);
            }
            foreach (var game in FindObjectsByType<EyeOpen>(FindObjectsSortMode.None)) Remember(game);
            foreach (var game in FindObjectsByType<SoundSearchMinigame>(FindObjectsSortMode.None)) Remember(game);
            foreach (var component in extraInputToBlock) Remember(component);
        }

        void ReleaseInput()
        {
            if (!capturedInput) return;
            foreach (var pair in blocked) if (pair.Key) pair.Key.enabled = pair.Value;
            foreach (var pair in hidden) if (pair.Key) pair.Key.SetActive(pair.Value);
            blocked.Clear();
            hidden.Clear();
            Cursor.lockState = savedCursor;
            Cursor.visible = savedCursorVisible;
            capturedInput = false;
            if (inputOwner == this) inputOwner = null;
        }

        public void SelectPage(int page)
        {
            for (int i = 0; i < settingsPages.Length; i++)
            {
                settingsPages[i].SetActive(i == page);
                tabButtons[i].image.color = i == page ? new Color(.30f, .33f, .36f) : new Color(.16f, .18f, .20f);
            }
        }

        void OptionChanged(OptionType type, bool unlocked) => RefreshGuides();
        public void RefreshGuides()
        {
            for (int i = 0; i < guideKeys.Length; i++)
            {
                bool exists = i < keyGuides.Count;
                guideKeys[i].transform.parent.gameObject.SetActive(exists);
                if (!exists) continue;
                var guide = keyGuides[i];
                bool open = guide.alwaysVisible || (OptionState.Instance && OptionState.Instance.IsUnlocked(guide.unlock));
                guideActions[i].text = guide.action;
                guideKeys[i].text = open ? guide.key : "?";
                guideKeys[i].color = open ? Color.white : new Color(.55f, .57f, .60f);
            }
        }

        void ApplySettings()
        {
            SetBrightness(brightnessSlider.value);
            SetMusicVolume(musicSlider.value);
            SetEffectsVolume(effectsSlider.value);
            SetSensitivity(sensitivitySlider.value);
            SetShake(shakeToggle.isOn);
        }

        public void SetBrightness(float value)
        {
            brightnessOverlay.color = value < .5f ? new Color(0, 0, 0, (.5f - value) * .75f) : new Color(1, 1, 1, (value - .5f) * .45f);
            brightnessValue.text = Mathf.RoundToInt(value * 100) + "%";
            PlayerPrefs.SetFloat("Roulette.UI.Brightness", value);
        }
        public void SetMusicVolume(float value)
        {
            foreach (var source in musicSources) if (source) source.volume = value;
            musicValue.text = Mathf.RoundToInt(value * 100) + "%";
            PlayerPrefs.SetFloat("Roulette.UI.Music", value);
        }
        public void SetEffectsVolume(float value)
        {
            foreach (var source in effectSources) if (source) source.volume = value;
            if (AudioManager.Instance && AudioManager.Instance.TryGetComponent<AudioSource>(out var sourceFromManager)) sourceFromManager.volume = value;
            effectsValue.text = Mathf.RoundToInt(value * 100) + "%";
            PlayerPrefs.SetFloat("Roulette.UI.Effects", value);
        }
        public void SetSensitivity(float value)
        {
            if (player) player.mouseSensitivity = value;
            sensitivityValue.text = value.ToString("0.0");
            PlayerPrefs.SetFloat("Roulette.UI.Sensitivity", value);
        }
        public void SetShake(bool value)
        {
            if (motion && capturedBob) { motion.bobHeight = value ? originalBobHeight : 0; motion.bobSide = value ? originalBobSide : 0; }
            PlayerPrefs.SetInt("Roulette.UI.Shake", value ? 1 : 0);
        }

        void ReleaseBackdrop()
        {
            blurredBackground.texture = null;
            if (backdrop) { backdrop.Release(); Destroy(backdrop); backdrop = null; }
        }

        void OnDisable()
        {
            if (!initialized) return;
            content.Changed -= RefreshObjectives;
            OptionState.OnOptionChanged -= OptionChanged;
            if (pause) { Time.timeScale = savedTimeScale; pause = false; }
            if (captureRoutine != null) { StopCoroutine(captureRoutine); captureRoutine = null; }
            if (canvas) canvas.enabled = true;
            ReleaseInput();
            releasePending = false;
            currentDialogue = null;
            if (settings) settings.SetActive(false);
            if (dialoguePanel) dialoguePanel.SetActive(false);
            if (hud) hud.SetActive(true);
            if (blurredBackground) blurredBackground.gameObject.SetActive(false);
            if (blurredBackground) ReleaseBackdrop();
        }
        void OnDestroy()
        {
            if (content) content.Changed -= RefreshObjectives;
            if (runtimeBlur) Destroy(runtimeBlur);
        }
    }
}
