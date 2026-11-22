using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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

        [Tooltip("설정 중 추가로 멈출 스크립트")]
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
        public TMP_Text objectiveText;
        [SerializeField] Texture2D hudTape;
        public GameObject settings;
        [SerializeField] internal Texture2D settingsPaper, selectedTabMark;
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

        readonly Dictionary<Behaviour, bool> blocked =
            new Dictionary<Behaviour, bool>();

        readonly Dictionary<GameObject, bool> hidden =
            new Dictionary<GameObject, bool>();

        static GameUI inputOwner;

        SceneUIContent content;
        DialogueSequence currentDialogue;
        bool dialoguePlaysWhileFrozen;

        int line, lineStartedFrame, releaseAfterFrame;
        float lineElapsed;

        bool initialized;
        bool pause, capturedInput, releasePending;

        float savedTimeScale;
        CursorLockMode savedCursor;
        bool savedCursorVisible;

        float originalBobHeight, originalBobSide;
        bool capturedBob;

        Coroutine captureRoutine;
        GameViewport viewport;
        GameUISettingsControls settingsControls;
        bool returningToTitle;
        Canvas houseMapCanvas;
        RenderTexture backdrop;
        Material runtimeBlur;
        Sprite tapeSprite;

        public event Action<DialogueSequence> DialogueFinished;

        public bool IsPaused => pause;
        public bool IsDialogueOpen => currentDialogue != null;

        void Awake()
        {
            if (DisableDuplicate())
                return;

            content = GetComponent<SceneUIContent>();

            if (!player)
                player = FindFirstObjectByType<FirstPersonPlayer>();

            if (!motion)
                motion = FindFirstObjectByType<PlayerMotionFeel>();

            if (motion)
            {
                originalBobHeight = motion.bobHeight;
                originalBobSide = motion.bobSide;
                capturedBob = true;
            }

            settings.SetActive(false);
            dialoguePanel.SetActive(false);
            blurredBackground.gameObject.SetActive(false);
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

            brightnessSlider.SetValueWithoutNotify(
                PlayerPrefs.GetFloat("Roulette.UI.Brightness", .5f)
            );

            musicSlider.SetValueWithoutNotify(
                PlayerPrefs.GetFloat("Roulette.UI.Music", 1f)
            );

            effectsSlider.SetValueWithoutNotify(
                PlayerPrefs.GetFloat("Roulette.UI.Effects", 1f)
            );

            sensitivitySlider.SetValueWithoutNotify(
                PlayerPrefs.GetFloat("Roulette.UI.Sensitivity", 2f)
            );

            shakeToggle.SetIsOnWithoutNotify(
                PlayerPrefs.GetInt("Roulette.UI.Shake", 1) == 1
            );

            if (blurMaterial)
                runtimeBlur = new Material(blurMaterial);

            ApplyHudTape();
            viewport = new GameViewport(canvas);
            settingsControls = new GameUISettingsControls(this, SelectAspect, RequestTitle);
            settingsControls.MarkRatio(viewport.Choice);
            BuildGuideList();
            initialized = true;
        }

        bool DisableDuplicate()
        {
            foreach (
                var other in FindObjectsByType<GameUI>(
                    FindObjectsSortMode.None
                )
            )
            {
                if (
                    other != this &&
                    other.initialized &&
                    other.isActiveAndEnabled &&
                    other.gameObject.scene == gameObject.scene
                )
                {
                    Debug.LogWarning(
                        "이 씬에 GameUI가 이미 있어 중복 UI를 비활성화했습니다.",
                        this
                    );

                    gameObject.SetActive(false);
                    return true;
                }
            }

            return false;
        }

        void OnEnable()
        {
            if (!initialized || DisableDuplicate())
                return;

            content.Changed += RefreshObjectives;
            OptionState.OnOptionChanged += OptionChanged;

            RefreshObjectives();
            RefreshGuides();
            if (currentDialogue) dialoguePanel.SetActive(true);
        }

        void Start()
        {
            if (!EventSystem.current)
            {
                var events = new GameObject(
                    "UI EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule)
                );

                events.transform.SetParent(transform);
            }

            RefreshObjectives();
            RefreshGuides();
            SelectPage(0);
            ApplySettings();
        }

        void Update()
        {
            if (returningToTitle || (inputOwner && inputOwner != this))
                return;

            if (!dialoguePlaysWhileFrozen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (
                    ObjectInspectionManager.Instance != null &&
                    ObjectInspectionManager.Instance.IsInspecting
                )
                {
                    return;
                }

                if (pause && settingsControls.IsConfirming)
                    settingsControls.SetConfirmation(false);
                else if (pause)
                    CloseSettings();
                else
                    OpenSettings();

                return;
            }

            if (pause || (Time.timeScale == 0 && !dialoguePlaysWhileFrozen))
                return;

            if (
                currentDialogue &&
                Time.frameCount > lineStartedFrame
            )
            {
                lineElapsed += Time.unscaledDeltaTime;

                if (lineElapsed >= (currentDialogue.secondsPerLine > 0 ? currentDialogue.secondsPerLine : 2f))
                    AdvanceDialogue();
            }
        }

        void LateUpdate()
        {
            if (!initialized || returningToTitle) return;

            viewport.Update();
            blurredBackground.uvRect = viewport.Area;
            if (
                releasePending &&
                Time.frameCount > releaseAfterFrame &&
                !pause
            )
            {
                ReleaseInput();
                releasePending = false;
            }

            if (pause)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void RefreshObjectives()
        {
            if (!content || !objectiveText)
                return;

            var seen = new HashSet<string>();
            bool hasIncomplete = false;

            foreach (var item in content.objectives)
            {
                if (
                    item == null ||
                    string.IsNullOrWhiteSpace(item.id) ||
                    !seen.Add(item.id) ||
                    item.completed
                )
                {
                    continue;
                }

                hasIncomplete = true;

                if (!item.visible)
                    continue;

                objectiveText.text = item.text;
                return;
            }

            objectiveText.text =
                hasIncomplete ? "" : content.completedMessage;
        }

        void ApplyHudTape()
        {
            if (!hudTape) return;

            // Trim the transparent space around the tape and preserve its torn ends when resizing.
            var source = new Rect(hudTape.width * .025f, hudTape.height * .14f,
                hudTape.width * .95f, hudTape.height * .64f);
            float cap = source.width * .075f;
            tapeSprite = Sprite.Create(hudTape, source, new Vector2(.5f, .5f),
                source.width / 5f, 0, SpriteMeshType.FullRect, new Vector4(cap, 0, cap, 0));

            SetTapeImage(dialoguePanel.GetComponent<Image>());
            if (objectiveText)
                SetTapeImage(objectiveText.transform.parent.GetComponent<Image>());
        }

        void SetTapeImage(Image image)
        {
            if (!image) return;
            image.sprite = tapeSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        static void FitDialoguePanelToText(TMP_Text label, RectTransform panel, float maxTextWidth, float minHeight)
        {
            Vector2 size = label.GetPreferredValues(label.text, maxTextWidth, Mathf.Infinity);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Clamp(size.x + 40f, 80f, maxTextWidth + 40f));
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(minHeight, size.y + 20f));
        }

        public void ShowDialogue(DialogueSequence sequence)
        {
            if (
                !isActiveAndEnabled ||
                pause ||
                (inputOwner && inputOwner != this)
            )
                return;

            if (
                !sequence ||
                sequence.lines == null ||
                sequence.lines.Length == 0 ||
                currentDialogue
            )
                return;

            currentDialogue = sequence;
            line = 0;
            dialoguePanel.SetActive(true);

            RenderLine();
        }

        public void ShowDialogueWhileFrozen(DialogueSequence sequence)
        {
            ShowDialogue(sequence);
            if (sequence && currentDialogue == sequence)
                dialoguePlaysWhileFrozen = true;
        }

        void RenderLine()
        {
            dialogueText.text = currentDialogue.lines[line];

            dialogueText.color =
                currentDialogue.GetLineColor(line);

            lineElapsed = 0;
            lineStartedFrame = Time.frameCount;

            FitDialoguePanelToText(dialogueText, (RectTransform)dialoguePanel.transform, 880f, 56f);
        }

        public void AdvanceDialogue()
        {
            if (pause || !currentDialogue)
                return;

            if (++line < currentDialogue.lines.Length)
            {
                RenderLine();
                return;
            }

            var finished = currentDialogue;

            currentDialogue = null;
            dialoguePlaysWhileFrozen = false;
            dialoguePanel.SetActive(false);

            content.onDialogueFinished.Invoke();
            DialogueFinished?.Invoke(finished);
        }

        public void OpenSettings()
        {
            if (
                pause ||
                !isActiveAndEnabled ||
                (inputOwner && inputOwner != this)
            )
                return;

            pause = true;

            savedTimeScale = Time.timeScale;
            Time.timeScale = 0;

            BlockInput();

            hud.SetActive(false);
            settings.SetActive(true);

            RefreshGuides();
            SelectPage(0);
            settingsControls.MarkRatio(viewport.Choice);

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

                int w = Mathf.Max(1, screen.width / 4);
                int h = Mathf.Max(1, screen.height / 4);

                backdrop = new RenderTexture(w, h, 0)
                {
                    filterMode = FilterMode.Bilinear
                };

                var temp = RenderTexture.GetTemporary(w, h, 0);

                Graphics.Blit(screen, backdrop);

                if (runtimeBlur)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Graphics.Blit(backdrop, temp, runtimeBlur);
                        Graphics.Blit(temp, backdrop, runtimeBlur);
                    }
                }

                RenderTexture.ReleaseTemporary(temp);
                Destroy(screen);

                blurredBackground.texture = backdrop;
                blurredBackground.gameObject.SetActive(true);
            }

            captureRoutine = null;
        }

        public void CloseSettings()
        {
            if (!pause || returningToTitle)
                return;

            if (captureRoutine != null)
            {
                StopCoroutine(captureRoutine);
                captureRoutine = null;
            }

            canvas.enabled = true;
            settingsControls.SetConfirmation(false);
            pause = false;

            Time.timeScale = savedTimeScale;

            settings.SetActive(false);
            hud.SetActive(true);
            blurredBackground.gameObject.SetActive(false);

            ReleaseBackdrop();

            lineStartedFrame = Time.frameCount;

            releasePending = true;
            releaseAfterFrame = Time.frameCount;

            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);

            PlayerPrefs.Save();
        }

        void SelectAspect(int choice)
        {
            if (returningToTitle || choice == viewport.Choice) return;

            viewport.Select(choice);
            viewport.Update();
            settingsControls.MarkRatio(choice);
            // The pause image was captured at the old ratio. Retake it instead of stretching it.
            if (captureRoutine != null) StopCoroutine(captureRoutine);
            blurredBackground.gameObject.SetActive(false);
            ReleaseBackdrop();
            captureRoutine = StartCoroutine(CaptureBackground());
        }

        void RequestTitle()
        {
            if (!pause || returningToTitle || SceneFader.IsBusy ||
                !Application.CanStreamedLevelBeLoaded(SaveSystem.TitleScene))
                return;

            StartCoroutine(ReturnToTitle());
        }

        IEnumerator ReturnToTitle()
        {
            returningToTitle = true;
            settingsControls.SetLoading(true);

            // 먼저 화면을 까맣게 덮음 (새 게임/이어하기와 같은 전환). 로딩 방식(Additive)은 그대로라 엔딩 판정·저장은 전과 같음
            yield return SceneFader.CoverIn(1f);

            var gameplayScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) gameplayScenes.Add(scene);
            }

            // The title supplies its own EventSystem when the additive scene activates.
            var previousEventSystems = new List<EventSystem>();
            foreach (var events in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (!events.isActiveAndEnabled) continue;
                previousEventSystems.Add(events);
                Remember(events);
            }

            // SaveSystem interprets a Single load of Title from the living room as the story ending.
            // An additive load followed by unloading gameplay preserves the checkpoint and ending flag.
            var load = SceneManager.LoadSceneAsync(SaveSystem.TitleScene, LoadSceneMode.Additive);
            if (load != null) yield return load;
            Scene title = SceneManager.GetSceneByName(SaveSystem.TitleScene);
            if (!title.isLoaded || !SceneManager.SetActiveScene(title))
            {
                foreach (var events in previousEventSystems)
                {
                    if (events && blocked.Remove(events, out bool enabled))
                        events.enabled = enabled;
                }

                returningToTitle = false;
                settingsControls.SetLoading(false);
                SceneFader.CoverOut(0.3f, 0f);   // 실패하면 덮개를 바로 걷음
                yield break;
            }

            // Scene cleanup can raise objective changes. Suppress writes until all gameplay scenes have unloaded.
            SaveSystem.Hold();
            int unloadingScenes = 0;

            // Discard the live living room stage before TitleMenu can start another run.
            var roomManager = TeamRoulette.Room3.GameManager.Instance;
            if (roomManager) Destroy(roomManager.gameObject);

            // Stop gameplay roots before resuming time on the title screen.
            var uiRoot = transform.root.gameObject;
            foreach (Scene scene in gameplayScenes)
            {
                if (!scene.isLoaded || scene == title) continue;
                foreach (var root in scene.GetRootGameObjects())
                    if (root != uiRoot && root.activeSelf) root.SetActive(false);
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload == null) continue;
                unloadingScenes++;
                unload.completed += _ =>
                {
                    if (--unloadingScenes == 0) SaveSystem.Release(false);
                };
            }

            if (unloadingScenes == 0) SaveSystem.Release(false);

            pause = false;
            viewport.Restore();
            canvas.enabled = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 타이틀이 준비되면 덮개가 스스로 걷힘 (이 GameUI가 사라져도 덮개는 남아서 처리)
            SceneFader.CoverOut(1.2f, 0.3f);
        }

        void Remember(Behaviour component)
        {
            if (
                !component ||
                component == this ||
                blocked.ContainsKey(component)
            )
                return;

            blocked.Add(component, component.enabled);
            component.enabled = false;
        }

        void Hide(GameObject item)
        {
            if (!item || hidden.ContainsKey(item))
                return;

            hidden.Add(item, item.activeSelf);
            item.SetActive(false);
        }

        void BlockInput()
        {
            if (capturedInput)
                return;

            capturedInput = true;
            inputOwner = this;

            savedCursor = Cursor.lockState;
            savedCursorVisible = Cursor.visible;

            Remember(player);
            Remember(motion);
            var map = FindFirstObjectByType<HouseMap>();
            if (map)
            {
                Remember(map);
                var mapVisuals = map.transform.Find("HouseMapCanvas");
                houseMapCanvas = mapVisuals ? mapVisuals.GetComponent<Canvas>() : null;
                Remember(houseMapCanvas);
            }

            foreach (var component in extraInputToBlock)
            {
                Remember(component);
            }

            foreach (
                var interactor in FindObjectsByType<PlayerInteractor>(
                    FindObjectsSortMode.None
                )
            )
            {
                Remember(interactor);

                Hide(interactor.crosshairDot);
                Hide(interactor.handIcon);

                if (interactor.nameText)
                    Hide(interactor.nameText.gameObject);
            }

            foreach (
                var game in FindObjectsByType<EyeOpen>(
                    FindObjectsSortMode.None
                )
            )
            {
                Remember(game);
            }

            foreach (
                var game in FindObjectsByType<SoundSearchMinigame>(
                    FindObjectsSortMode.None
                )
            )
            {
                Remember(game);
            }
        }

        void ReleaseInput()
        {
            if (!capturedInput)
                return;

            foreach (var pair in blocked)
            {
                if (pair.Key && !(returningToTitle &&
                    (pair.Key is EventSystem || pair.Key is HouseMap || pair.Key == houseMapCanvas)))
                    pair.Key.enabled = pair.Value;
            }

            foreach (var pair in hidden)
            {
                if (pair.Key)
                    pair.Key.SetActive(pair.Value);
            }

            blocked.Clear();
            hidden.Clear();

            if (!returningToTitle)
            {
                Cursor.lockState = savedCursor;
                Cursor.visible = savedCursorVisible;
            }

            capturedInput = false;

            if (inputOwner == this)
                inputOwner = null;
        }

        public void SelectPage(int page)
        {
            for (int i = 0; i < settingsPages.Length; i++)
            {
                settingsPages[i].SetActive(i == page);

                settingsControls.MarkTab(i, i == page);
            }
        }

        void OptionChanged(OptionType type, bool unlocked)
        {
            RefreshGuides();
        }

        void BuildGuideList()
        {
            if (guideKeys.Length == 0 || guideActions.Length == 0) return;
            var template = (RectTransform)guideKeys[0].transform.parent;
            var guideArea = GameViewport.MakeRect("Key Guide Scroll", template.parent);
            guideArea.anchorMin = guideArea.anchorMax = template.anchorMin;
            guideArea.pivot = new Vector2(.5f, 1f);
            guideArea.anchoredPosition = template.anchoredPosition + Vector2.up * (template.sizeDelta.y / 2f);
            guideArea.sizeDelta = new Vector2(template.sizeDelta.x, 310);
            guideArea.gameObject.AddComponent<Image>().color = Color.clear;
            guideArea.gameObject.AddComponent<RectMask2D>();

            var list = GameViewport.MakeRect("Rows", guideArea);
            list.anchorMin = new Vector2(0, 1);
            list.anchorMax = Vector2.one;
            list.pivot = new Vector2(.5f, 1);
            list.anchoredPosition = Vector2.zero;

            var scroll = guideArea.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = guideArea;
            scroll.content = list;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = BuildGuideScrollbar(guideArea);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var actions = new List<TMP_Text>(guideActions);
            var keys = new List<TMP_Text>(guideKeys);
            float rowHeight = template.sizeDelta.y;
            float step = rowHeight + 2f;
            for (int i = 0; i < Mathf.Max(keyGuides.Count, guideKeys.Length); i++)
            {
                RectTransform row;
                if (i < guideKeys.Length)
                {
                    row = (RectTransform)guideKeys[i].transform.parent;
                    row.SetParent(list, false);
                }
                else
                {
                    row = Instantiate(template, list, false);
                    row.name = "Guide " + i;
                    actions.Add(row.Find(guideActions[0].name).GetComponent<TMP_Text>());
                    keys.Add(row.Find(guideKeys[0].name).GetComponent<TMP_Text>());
                }
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1f);
                row.anchoredPosition = new Vector2(0, -rowHeight * .5f - i * step);
            }

            list.sizeDelta = new Vector2(0, keyGuides.Count * step);
            guideActions = actions.ToArray();
            guideKeys = keys.ToArray();
            settingsControls.StyleGuides(scroll.verticalScrollbar, guideActions, guideKeys);
        }

        static Scrollbar BuildGuideScrollbar(RectTransform guideArea)
        {
            // A sibling stays outside the mask that clips the guide rows.
            var track = GameViewport.MakeRect("Key Guide Scrollbar", guideArea.parent);
            track.anchorMin = track.anchorMax = guideArea.anchorMax;
            track.pivot = new Vector2(0f, 1f);
            track.anchoredPosition = guideArea.anchoredPosition
                + Vector2.right * (guideArea.sizeDelta.x * .5f + 8f);
            track.sizeDelta = new Vector2(14f, guideArea.sizeDelta.y);
            track.gameObject.AddComponent<Image>().color = new Color(.24f, .27f, .30f);

            var slidingArea = GameViewport.MakeRect("Sliding Area", track);
            GameViewport.Stretch(slidingArea, new Rect(0, 0, 1, 1));
            slidingArea.offsetMin = new Vector2(2f, 2f);
            slidingArea.offsetMax = new Vector2(-2f, -2f);

            var handle = GameViewport.MakeRect("Handle", slidingArea);
            GameViewport.Stretch(handle, new Rect(0, 0, 1, 1));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(.67f, .71f, .75f);

            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.SetValueWithoutNotify(1f);
            return scrollbar;
        }

        public void RefreshGuides()
        {
            for (int i = 0; i < guideKeys.Length; i++)
            {
                bool exists = i < keyGuides.Count;

                guideKeys[i].transform.parent.gameObject.SetActive(exists);

                if (!exists)
                    continue;

                var guide = keyGuides[i];

                bool open =
                    guide.alwaysVisible ||
                    (
                        OptionState.Instance &&
                        OptionState.Instance.IsUnlocked(guide.unlock)
                    );

                guideActions[i].text = guide.action;
                guideKeys[i].text = open ? guide.key : "?";

                guideKeys[i].color =
                    open
                        ? settingsControls.GuideInk
                        : settingsControls.GuideLockedInk;
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
            brightnessOverlay.color =
                value < .5f
                    ? new Color(0, 0, 0, (.5f - value) * .75f)
                    : new Color(1, 1, 1, (value - .5f) * .45f);

            brightnessValue.text =
                Mathf.RoundToInt(value * 100) + "%";

            PlayerPrefs.SetFloat(
                "Roulette.UI.Brightness",
                value
            );
        }

        public void SetMusicVolume(float value)
        {
            foreach (var source in musicSources)
            {
                if (source)
                    source.volume = value;
            }

            musicValue.text =
                Mathf.RoundToInt(value * 100) + "%";

            PlayerPrefs.SetFloat(
                "Roulette.UI.Music",
                value
            );
        }

        public void SetEffectsVolume(float value)
        {
            foreach (var source in effectSources)
            {
                if (source)
                    source.volume = value;
            }

            if (
                AudioManager.Instance &&
                AudioManager.Instance.TryGetComponent<AudioSource>(
                    out var sourceFromManager
                )
            )
            {
                sourceFromManager.volume = value;
            }

            effectsValue.text =
                Mathf.RoundToInt(value * 100) + "%";

            PlayerPrefs.SetFloat(
                "Roulette.UI.Effects",
                value
            );
        }

        public void SetSensitivity(float value)
        {
            if (player)
                player.mouseSensitivity = value;

            sensitivityValue.text = value.ToString("0.0");

            PlayerPrefs.SetFloat(
                "Roulette.UI.Sensitivity",
                value
            );
        }

        public void SetShake(bool value)
        {
            if (motion && capturedBob)
            {
                motion.bobHeight =
                    value ? originalBobHeight : 0;

                motion.bobSide =
                    value ? originalBobSide : 0;
            }

            PlayerPrefs.SetInt(
                "Roulette.UI.Shake",
                value ? 1 : 0
            );
        }

        void ReleaseBackdrop()
        {
            blurredBackground.texture = null;

            if (backdrop)
            {
                backdrop.Release();
                Destroy(backdrop);
                backdrop = null;
            }
        }

        void OnDisable()
        {
            if (!initialized)
                return;

            content.Changed -= RefreshObjectives;
            OptionState.OnOptionChanged -= OptionChanged;

            viewport.Restore();
            settingsControls.SetConfirmation(false);

            if (pause)
            {
                Time.timeScale = savedTimeScale;
                pause = false;
            }

            if (captureRoutine != null)
            {
                StopCoroutine(captureRoutine);
                captureRoutine = null;
            }

            if (canvas)
                canvas.enabled = true;

            ReleaseInput();

            releasePending = false;

            if (settings)
                settings.SetActive(false);

            if (dialoguePanel)
                dialoguePanel.SetActive(false);

            if (hud)
                hud.SetActive(true);

            if (blurredBackground)
                blurredBackground.gameObject.SetActive(false);

            if (blurredBackground)
                ReleaseBackdrop();
        }

        void OnDestroy()
        {
            if (content)
                content.Changed -= RefreshObjectives;

            viewport?.Destroy();
            if (tapeSprite) Destroy(tapeSprite);
            if (runtimeBlur)
                Destroy(runtimeBlur);
        }
    }
}