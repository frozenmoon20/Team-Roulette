using System.Collections;
using System.Collections.Generic;
using TeamRoulette.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TeamRoulette.Room3
{
    public sealed class TVGameController : MonoBehaviour
    {
        public Transform screen;
        public VideoClip clip;
        public Texture thumbnail;
        public FirstPersonPlayer player;
        public Camera viewCamera;
        public TMP_FontAsset font;
        [Min(1)] public float prepareTimeout = 30;
        [Min(0)] public float surfaceOffset = .03f;
        [Header("영상 소리")]
        [Tooltip("영상 소리의 증폭 배율입니다. 0은 음소거, 1은 원래 음량, 2는 2배, 10은 10배입니다. 게임 내 효과음 설정도 함께 적용됩니다.")]
        [Range(0f, 10f)] public float videoVolume = 1f;

        enum State { Black, Thumbnail, Confirm, Preparing, Playing, Restoring }
        State state;
        bool completed, initialized, captured, savedVisible;
        CursorLockMode savedCursor;
        GameObject displayRoot, overlay, prompt, fullScreen;
        RawImage preview, movie;
        TVPlayIcon playIcon;
        VideoPlayer video;
        AudioSource audioSource;
        volatile float audioGain = 1f;
        RenderTexture movieTexture;
        float preparingSince;
        readonly Dictionary<Behaviour, bool> blocked = new Dictionary<Behaviour, bool>();
        readonly Dictionary<GameObject, bool> objects = new Dictionary<GameObject, bool>();
        readonly List<AudioSource> pausedAudio = new List<AudioSource>();
        public bool Ready => state != State.Black;
        public bool IsPlaying => state == State.Playing;
        public bool IsComplete => completed || (GameManager.Instance && GameManager.Instance.IsComplete(GameStage.TV));

        void Start()
        {
            if (!screen || !clip || !thumbnail)
            {
                Debug.LogError("TVGame: Screen, Winter_TV, Thumbnail 연결을 확인하세요.", this);
                enabled = false;
                return;
            }
            if (!player) player = FindFirstObjectByType<FirstPersonPlayer>();
            if (!viewCamera) viewCamera = player ? player.GetComponentInChildren<Camera>() : Camera.main;
            var ui = FindFirstObjectByType<GameUI>();
            if (!font) font = ui && ui.objectiveText ? ui.objectiveText.font : TMP_Settings.defaultFontAsset;
            BuildDisplay();
            BuildOverlay();
            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = false;
            video.waitForFirstFrame = true;
            video.skipOnDrop = true;
            video.source = VideoSource.VideoClip;
            video.clip = clip;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            ApplyVideoVolume();
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            for (ushort track = 0; track < clip.audioTrackCount; track++)
            {
                video.EnableAudioTrack(track, true);
                video.SetTargetAudioSource(track, audioSource);
            }
            video.prepareCompleted += Prepared;
            video.loopPointReached += Finished;
            video.errorReceived += Failed;
            initialized = true;
            SetStandby(IsComplete);
        }

        void Update()
        {
            if (!captured) return;
            ApplyVideoVolume();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = state == State.Confirm;
            if (state == State.Preparing && Time.realtimeSinceStartup - preparingSince > prepareTimeout)
                Failed(video, "영상 준비 시간이 초과되었습니다.");
            if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape();
        }

        void ApplyVideoVolume()
        {
            audioGain = Mathf.Clamp(videoVolume, 0f, 10f);
            if (audioSource)
                audioSource.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Roulette.UI.Effects", 1));
        }

        // AudioSource.volume is capped at 1. Amplify the decoded video samples
        // instead. This callback runs on the audio thread; use only the cached gain.
        void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = audioGain;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        void HandleEscape()
        {
            if (state == State.Confirm) Decline();
            else if ((state == State.Preparing || state == State.Playing) && IsComplete)
                StartCoroutine(ReturnToRoom(false));
        }

        public void Interact()
        {
            if (!initialized || !isActiveAndEnabled || captured || Time.timeScale <= 0) return;
            var ui = FindFirstObjectByType<GameUI>();
            if ((ui && ui.IsPaused) || (!IsComplete && !GameManager.CanPlay(GameStage.TV))) return;
            if (PlayerHand.Instance && PlayerHand.Instance.Held) return;
            if (state == State.Black) { SetStandby(true); return; }
            // The first-person ray must hit the central play button, not the surrounding thumbnail.
            if (viewCamera && !RectTransformUtility.RectangleContainsScreenPoint(playIcon.rectTransform,
                    viewCamera.ViewportToScreenPoint(new Vector3(.5f, .5f)), viewCamera)) return;
            CaptureInput();
            if (IsComplete) BeginPlayback();
            else
            {
                state = State.Confirm;
                overlay.SetActive(true);
                prompt.SetActive(true);
                Cursor.visible = true;
                EventSystem.current?.SetSelectedGameObject(null);
            }
        }

        public void Accept()
        {
            if (state != State.Confirm) return;
            // TryBegin runs while GameUI is blocked, before any screen is covered by video.
            if (!GameManager.TryBegin(GameStage.TV)) { Decline(); return; }
            BeginPlayback();
        }

        public void Decline()
        {
            if (state != State.Confirm) return;
            StartCoroutine(ReturnToRoom(false));
        }

        void BeginPlayback()
        {
            state = State.Preparing;
            prompt.SetActive(false);
            overlay.SetActive(true);
            fullScreen.SetActive(true);
            Cursor.visible = false;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (!canvas.transform.IsChildOf(transform) && !canvas.transform.IsChildOf(screen)) Block(canvas);
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source != audioSource && source.isPlaying) { source.Pause(); pausedAudio.Add(source); }
            movieTexture = new RenderTexture(Mathf.Max(1, (int)clip.width), Mathf.Max(1, (int)clip.height), 0)
            { name = "TVGame Video", filterMode = FilterMode.Bilinear };
            movieTexture.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = movieTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
            video.targetTexture = movieTexture;
            movie.texture = movieTexture;
            var fit = movie.GetComponent<AspectRatioFitter>();
            fit.aspectRatio = clip.width / (float)Mathf.Max(1, (int)clip.height);
            preparingSince = Time.realtimeSinceStartup;
            video.Prepare();
        }

        void Prepared(VideoPlayer source)
        {
            if (state != State.Preparing) return;
            state = State.Playing;
            source.Play();
        }

        void Finished(VideoPlayer source)
        {
            if (state == State.Playing) StartCoroutine(ReturnToRoom(true));
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugComplete()
        {
            if (!initialized || IsComplete) return;
            StopAllCoroutines();
            StartCoroutine(ReturnToRoom(true));
        }
#endif

        void Failed(VideoPlayer source, string error)
        {
            if (state != State.Preparing && state != State.Playing) return;
            Debug.LogError("TVGame: " + error, this);
            StartCoroutine(ReturnToRoom(false));
        }

        IEnumerator ReturnToRoom(bool reachedEnd)
        {
            state = State.Restoring;
            video.Stop();
            overlay.SetActive(false);
            fullScreen.SetActive(false);
            prompt.SetActive(false);
            // Do not pass a button click or replay Escape through to the room in this frame.
            yield return null;
            RestoreInput();
            ReleaseMovie();
            if (reachedEnd && !IsComplete)
            {
                completed = true;
                GameManager.ReportComplete(GameStage.TV);
            }
            SetStandby(true);
        }

        void SetStandby(bool showThumbnail)
        {
            state = showThumbnail ? State.Thumbnail : State.Black;
            preview.gameObject.SetActive(showThumbnail);
            playIcon.gameObject.SetActive(showThumbnail);
        }

        void Remember(GameObject obj)
        { if (obj && !objects.ContainsKey(obj)) objects.Add(obj, obj.activeSelf); }
        void Block(Behaviour component)
        {
            if (!component || blocked.ContainsKey(component)) return;
            blocked.Add(component, component.enabled);
            component.enabled = false;
        }
        void CaptureInput()
        {
            captured = true;
            savedCursor = Cursor.lockState;
            savedVisible = Cursor.visible;
            foreach (var ui in FindObjectsByType<GameUI>(FindObjectsSortMode.None))
            {
                // GameUI.OnDisable changes child visibility; save it before blocking its Escape handler.
                foreach (var node in ui.GetComponentsInChildren<Transform>(true)) Remember(node.gameObject);
                Block(ui);
            }
            Block(player);
            foreach (var component in FindObjectsByType<PlayerMotionFeel>(FindObjectsSortMode.None)) Block(component);
            foreach (var component in FindObjectsByType<DialogueManager>(FindObjectsSortMode.None)) Block(component);
            foreach (var interactor in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
            {
                foreach (var obj in new[] { interactor.crosshairDot, interactor.handIcon,
                    interactor.nameText ? interactor.nameText.gameObject : null })
                { Remember(obj); if (obj) obj.SetActive(false); }
                Block(interactor);
            }
            Block(PlayerHand.Instance);
            Cursor.lockState = CursorLockMode.None;
        }

        void RestoreInput()
        {
            if (!captured) return;
            foreach (var pair in blocked) if (pair.Key) pair.Key.enabled = pair.Value;
            foreach (var pair in objects) if (pair.Key) pair.Key.SetActive(pair.Value);
            foreach (var source in pausedAudio) if (source) source.UnPause();
            blocked.Clear(); objects.Clear(); pausedAudio.Clear();
            Cursor.lockState = savedCursor;
            Cursor.visible = savedVisible;
            captured = false;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        void BuildDisplay()
        {
            displayRoot = new GameObject("TV Display", typeof(RectTransform), typeof(Canvas));
            displayRoot.transform.SetParent(screen, false);
            var canvas = displayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = viewCamera;
            var rect = displayRoot.GetComponent<RectTransform>();
            var mesh = screen.GetComponent<MeshFilter>();
            if (screen is RectTransform screenRect)
            {
                rect.sizeDelta = screenRect.rect.size;
                rect.localPosition = new Vector3(0, 0, -.001f);
            }
            else
            {
                var bounds = mesh && mesh.sharedMesh ? mesh.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(1, 1, .001f));
                Vector3 right, up, normal;
                if (bounds.size.y < bounds.size.x && bounds.size.y < bounds.size.z)
                { right = Vector3.right; up = Vector3.forward; }
                else if (bounds.size.x < bounds.size.y && bounds.size.x < bounds.size.z)
                { right = Vector3.forward; up = Vector3.up; }
                else { right = Vector3.right; up = Vector3.up; }
                normal = Vector3.Cross(right, up);
                // Use the existing mesh's front face, independent of the player's spawn location.
                if (mesh && mesh.sharedMesh && mesh.sharedMesh.normals.Length > 0)
                {
                    if (Vector3.Dot(normal, mesh.sharedMesh.normals[0]) < 0) normal = -normal;
                }
                rect.localRotation = Quaternion.LookRotation(-normal, up);
                rect.localPosition = bounds.center + normal * (Mathf.Abs(Vector3.Dot(bounds.extents, normal)) + surfaceOffset);
                const float pixels = 1000;
                rect.sizeDelta = new Vector2(Mathf.Abs(Vector3.Dot(bounds.size, right)), Mathf.Abs(Vector3.Dot(bounds.size, up))) * pixels;
                rect.localScale = Vector3.one / pixels;
            }
            var black = Image("Black", rect, Color.black);
            Stretch(black.rectTransform);
            preview = Raw("Thumbnail", rect);
            preview.texture = thumbnail;
            Stretch(preview.rectTransform);
            playIcon = new GameObject("Play", typeof(RectTransform), typeof(CanvasRenderer), typeof(TVPlayIcon)).GetComponent<TVPlayIcon>();
            playIcon.transform.SetParent(rect, false);
            playIcon.color = new Color(1, 1, 1, .65f);
            playIcon.raycastTarget = false;
            float iconHeight = rect.sizeDelta.y * .23f;
            float ratio = Mathf.Abs(rect.lossyScale.y / Mathf.Max(.000001f, Mathf.Abs(rect.lossyScale.x)));
            playIcon.rectTransform.sizeDelta = new Vector2(iconHeight * .8f * ratio, iconHeight);
        }

        void BuildOverlay()
        {
            overlay = new GameObject("TV Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            // Transparent full-screen hit shield prevents clicks on unrelated room UI.
            Stretch(Image("Input Shield", overlay.transform, Color.clear).rectTransform);
            var panel = Image("Confirmation", overlay.transform, new Color(.07f, .07f, .07f, .94f));
            prompt = panel.gameObject;
            var r = panel.rectTransform;
            r.anchorMin = new Vector2(.06f, .06f); r.anchorMax = new Vector2(.94f, .29f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            var label = Label("Warning", r, "영상을 시작하면 그동안 중단할 수 없습니다. 영상을 보시겠습니까?\n(처음 시청 후, ESC로 중단 가능)", 32);
            label.rectTransform.anchorMin = new Vector2(.04f, .40f); label.rectTransform.anchorMax = new Vector2(.96f, .94f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            Button(r, "Yes", "예", new Vector2(.35f, .23f), Accept);
            Button(r, "No", "아니오", new Vector2(.65f, .23f), Decline);
            fullScreen = Image("Fullscreen Black", overlay.transform, Color.black).gameObject;
            Stretch((RectTransform)fullScreen.transform);
            movie = Raw("Winter_TV", fullScreen.transform);
            movie.rectTransform.anchorMin = movie.rectTransform.anchorMax = new Vector2(.5f, .5f);
            movie.rectTransform.pivot = new Vector2(.5f, .5f);
            movie.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fullScreen.SetActive(false);
            overlay.SetActive(false);
        }

        static Image Image(string name, Transform parent, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false); image.color = color; return image;
        }
        static RawImage Raw(string name, Transform parent)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent, false); image.raycastTarget = false; return image;
        }
        TMP_Text Label(string name, Transform parent, string value, float size)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false); label.font = font; label.text = value; label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false; return label;
        }
        void Button(Transform parent, string name, string text, Vector2 anchor, UnityEngine.Events.UnityAction action)
        {
            var image = Image(name, parent, new Color(.22f, .22f, .22f, 1));
            var r = image.rectTransform; r.anchorMin = r.anchorMax = anchor; r.sizeDelta = new Vector2(220, 62);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var colors = button.colors; colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1); button.colors = colors;
            Stretch(Label("Label", r, text, 30).rectTransform);
        }
        static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        void ReleaseMovie()
        {
            if (video) video.targetTexture = null;
            if (movie) movie.texture = null;
            if (movieTexture) { movieTexture.Release(); Destroy(movieTexture); movieTexture = null; }
        }
        void OnDisable()
        {
            if (!initialized) return;
            StopAllCoroutines();
            if (video) video.Stop();
            if (overlay) overlay.SetActive(false);
            RestoreInput(); ReleaseMovie(); SetStandby(Ready || IsComplete);
        }
        void OnDestroy()
        {
            if (video) { video.prepareCompleted -= Prepared; video.loopPointReached -= Finished; video.errorReceived -= Failed; }
            if (displayRoot) Destroy(displayRoot);
        }
    }
}
