using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamRoulette.UI
{
    // Keep the operating system's window size. Fit rendering and HUD into a centered viewport.
    internal sealed class GameViewport
    {
        internal static readonly string[] Names = { "자동(기본)", "16:9", "16:10", "21:9" };
        static readonly float[] Ratios = { 0f, 16f / 9f, 16f / 10f, 21f / 9f };
        const string Preference = "Roulette.UI.Aspect";
        static readonly Rect Full = new Rect(0, 0, 1, 1);

        static GameViewport renderingOwner;
        static int currentChoice = -1;

        readonly Canvas canvas;
        readonly RectTransform content;
        readonly Canvas barCanvas;
        readonly RectTransform[] bars = new RectTransform[4];
        readonly Dictionary<Camera, Rect> cameraRects = new Dictionary<Camera, Rect>();
        Camera[] cameras = new Camera[0];
        RectTransform mapPanel;
        Vector2 mapAnchorMin, mapAnchorMax;
        int layoutWidth, layoutHeight, layoutChoice = -1;
        float layoutCanvasScale;
        Rect barArea = Full;

        public int Choice => currentChoice;
        public Rect Area { get; private set; } = Full;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            renderingOwner = null;
            currentChoice = -1;
        }

        public GameViewport(Canvas canvas)
        {
            this.canvas = canvas;
            var root = (RectTransform)canvas.transform;
            int children = root.childCount;
            content = MakeRect("Game Viewport", root);
            for (int i = 0; i < children; i++)
                root.GetChild(0).SetParent(content, false);
            Stretch(content, Full);

            var overlay = new GameObject("Screen Aspect Bars", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(overlay, canvas.gameObject.scene);
            barCanvas = overlay.GetComponent<Canvas>();
            // Cover the final screen, including when the settings backdrop is being captured.
            barCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            barCanvas.sortingOrder = 31999;
            barCanvas.targetDisplay = canvas.targetDisplay;
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = MakeRect("Black Bar " + i, barCanvas.transform);
                bars[i].gameObject.AddComponent<Image>().color = Color.black;
            }
            barCanvas.gameObject.SetActive(false);
            if (currentChoice < 0)
                currentChoice = Mathf.Clamp(PlayerPrefs.GetInt(Preference), 0, Ratios.Length - 1);
        }

        public void Select(int index)
        {
            currentChoice = Mathf.Clamp(index, 0, Ratios.Length - 1);
            PlayerPrefs.SetInt(Preference, Choice);
            PlayerPrefs.Save();
        }

        public void Update()
        {
            if (Choice == 0)
            {
                if (Area != Full || renderingOwner == this) Restore();
                return;
            }

            int width = Mathf.Max(Screen.width, 1);
            int height = Mathf.Max(Screen.height, 1);
            float canvasScale = canvas.scaleFactor;
            if (layoutWidth != width || layoutHeight != height || layoutChoice != Choice || layoutCanvasScale != canvasScale)
            {
                layoutWidth = width;
                layoutHeight = height;
                layoutChoice = Choice;
                layoutCanvasScale = canvasScale;
                Area = Fit(width, height, Ratios[Choice]);

                // Keep fixed-size HUD widgets inside the viewport on narrow or very wide displays.
                float fittedWidth = Area.width * width;
                float fittedHeight = Area.height * height;
                float scale = Mathf.Min(fittedWidth / 1600f, fittedHeight / 900f);
                content.anchorMin = content.anchorMax = new Vector2(.5f, .5f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(fittedWidth, fittedHeight) / scale;
                content.localScale = Vector3.one * (scale / canvasScale);
            }

            // An additive minigame can have a GameUI of its own. Only one instance owns cameras and bars.
            if (renderingOwner != null && renderingOwner != this) return;
            renderingOwner = this;

            if (barArea != Area)
            {
                Stretch(bars[0], Rect.MinMaxRect(0, 0, Area.xMin, 1));
                Stretch(bars[1], Rect.MinMaxRect(Area.xMax, 0, 1, 1));
                Stretch(bars[2], Rect.MinMaxRect(Area.xMin, 0, Area.xMax, Area.yMin));
                Stretch(bars[3], Rect.MinMaxRect(Area.xMin, Area.yMax, Area.xMax, 1));
                barCanvas.gameObject.SetActive(Area != Full);
                barArea = Area;
            }

            int count = Camera.allCamerasCount;
            if (cameras.Length < count)
                cameras = new Camera[count];
            count = Camera.GetAllCameras(cameras);
            for (int i = 0; i < count; i++)
            {
                var camera = cameras[i];
                // Auxiliary cameras that render into textures own their viewport.
                if (camera.cameraType != CameraType.Game || camera.targetTexture || camera.targetDisplay != canvas.targetDisplay)
                    continue;

                if (!cameraRects.TryGetValue(camera, out Rect original))
                    cameraRects.Add(camera, original = camera.rect);

                var fittedRect = new Rect(
                    Area.x + original.x * Area.width, Area.y + original.y * Area.height,
                    original.width * Area.width, original.height * Area.height
                );
                if (camera.rect != fittedRect)
                    camera.rect = fittedRect;
            }

            if (!mapPanel)
            {
                var map = Object.FindFirstObjectByType<HouseMap>(FindObjectsInactive.Include);
                var panel = map ? map.transform.Find("HouseMapCanvas/Panel") as RectTransform : null;
                if (panel)
                {
                    mapPanel = panel;
                    mapAnchorMin = panel.anchorMin;
                    mapAnchorMax = panel.anchorMax;
                }
            }

            if (mapPanel)
            {
                mapPanel.anchorMin = Area.min + Vector2.Scale(mapAnchorMin, Area.size);
                mapPanel.anchorMax = Area.min + Vector2.Scale(mapAnchorMax, Area.size);
            }
        }

        public void Restore()
        {
            if (renderingOwner == this) renderingOwner = null;
            foreach (var item in cameraRects)
                if (item.Key) item.Key.rect = item.Value;
            cameraRects.Clear();

            if (mapPanel)
            {
                mapPanel.anchorMin = mapAnchorMin;
                mapPanel.anchorMax = mapAnchorMax;
                mapPanel = null;
            }

            layoutChoice = -1;
            barArea = Area = Full;
            if (barCanvas) barCanvas.gameObject.SetActive(false);
            if (content)
            {
                content.localScale = Vector3.one;
                Stretch(content, Full);
            }
        }

        public void Destroy()
        {
            Restore();
            if (barCanvas) Object.Destroy(barCanvas.gameObject);
        }

        internal static Rect Fit(int width, int height, float ratio)
        {
            float actual = (float)width / height;
            if (actual > ratio)
            {
                float fraction = ratio / actual;
                return new Rect((1f - fraction) * .5f, 0, fraction, 1);
            }
            float vertical = actual / ratio;
            return new Rect(0, (1f - vertical) * .5f, 1, vertical);
        }

        public static RectTransform MakeRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, Rect anchors)
        {
            rect.anchorMin = anchors.min;
            rect.anchorMax = anchors.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
