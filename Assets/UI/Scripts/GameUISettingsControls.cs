using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TeamRoulette.UI
{
    internal sealed class GameUISettingsControls
    {
        internal const string ReturnMessage = "메인 화면으로 돌아가시겠습니까?\n마지막 자동저장 이후의 진행 데이터는 저장되지 않습니다.";

        static readonly Color Ink = new Color(.20f, .17f, .13f);
        static readonly Color Paper = new Color(.89f, .85f, .77f);
        static readonly Color Rule = new Color(.49f, .44f, .37f);

        readonly GameUI ui;
        // Centers between the rules on the 1042 px paper texture, measured from the top.
        const float PaperTextureHeight = 1042f;
        static readonly float[] PaperRows = { 163f, 310f, 383f, 454f, 529f };
        readonly bool hasPaper;
        readonly RawImage[] tabMarks;

        public Color GuideInk => hasPaper ? Ink : Color.white;
        public Color GuideLockedInk => hasPaper ? new Color(.39f, .34f, .27f) : new Color(.55f, .57f, .60f);
        readonly TMP_Dropdown ratioDropdown;
        readonly CanvasGroup settingsPanel;
        readonly GameObject confirmation;
        readonly Button cancel;
        readonly Button confirm;

        public bool IsConfirming => confirmation.activeSelf;

        public GameUISettingsControls(GameUI ui, Action<int> selectRatio, Action returnToTitle)
        {
            this.ui = ui;
            hasPaper = ui.settingsPaper;
            tabMarks = new RawImage[ui.tabButtons.Length];
            CenterButtonLabel(ui.backButton);
            foreach (var tab in ui.tabButtons) CenterButtonLabel(tab);
            TMP_Text fontSource = ui.brightnessValue;
            var displayPage = ui.settingsPages[0].transform;
            float rowY = ((RectTransform)ui.brightnessSlider.transform).anchoredPosition.y - 66f;
            Label("화면 비율", displayPage, 0, 1, 124, rowY, 180, 42, fontSource, 22)
                .alignment = TextAlignmentOptions.Left;

            ratioDropdown = CreateAspectDropdown(displayPage, rowY, fontSource, selectRatio);


            if (hasPaper) ApplyPaperLayout();

            var soundTab = (RectTransform)ui.tabButtons[1].transform;
            var controlsTab = (RectTransform)ui.tabButtons[2].transform;
            float tabStep = controlsTab.anchoredPosition.y - soundTab.anchoredPosition.y;
            CreateButton("Return To Title", "메인화면", controlsTab.parent, controlsTab.anchorMin.x, controlsTab.anchorMin.y,
                controlsTab.anchoredPosition.x, hasPaper ? PaperY((RectTransform)controlsTab.parent, 899.5f) : controlsTab.anchoredPosition.y + tabStep,
                controlsTab.sizeDelta.x, controlsTab.sizeDelta.y,
                () => SetConfirmation(true));

            settingsPanel = controlsTab.parent.gameObject.AddComponent<CanvasGroup>();
            var modal = GameViewport.MakeRect("Return To Title Confirmation", ui.settings.transform);
            GameViewport.Stretch(modal, new Rect(0, 0, 1, 1));
            modal.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .72f);
            confirmation = modal.gameObject;

            var panel = GameViewport.MakeRect("Panel", modal);
            Place(panel, .5f, .5f, 0, 0, 720, 230);
            panel.gameObject.AddComponent<Image>().color = hasPaper ? Paper : new Color(.11f, .12f, .14f);
            var message = Label(ReturnMessage, panel, .5f, .5f, 0, 36, 664, 120, fontSource, 23);
            message.textWrappingMode = TextWrappingModes.Normal;
            confirm = CreateButton("Yes", "예", panel, .5f, .5f, -90, -66, 150, 48, returnToTitle);
            cancel = CreateButton("No", "아니요", panel, .5f, .5f, 90, -66, 150, 48, () => SetConfirmation(false));
            confirmation.SetActive(false);

            if (hasPaper)
            {
                foreach (var label in ui.settings.GetComponentsInChildren<TMP_Text>(true))
                    label.color = Ink;
            }
        }

        void ApplyPaperLayout()
        {
            var panel = (RectTransform)ui.tabButtons[0].transform.parent;
            panel.GetComponent<Image>().color = Color.clear;
            var background = GameViewport.MakeRect("Settings Paper", panel);
            GameViewport.Stretch(background, new Rect(0, 0, 1, 1));
            background.SetAsFirstSibling();
            var paper = background.gameObject.AddComponent<RawImage>();
            paper.texture = ui.settingsPaper;
            paper.raycastTarget = false;

            var divider = panel.Find("Divider");
            if (divider) divider.gameObject.SetActive(false); // The paper has its own rules.
            var title = panel.Find("Title") as RectTransform;
            if (title) title.anchoredPosition = new Vector2(305f, PaperY(panel, PaperRows[0]));
            var back = (RectTransform)ui.backButton.transform;
            back.anchoredPosition = new Vector2(821f, PaperY(panel, PaperRows[0]));
            ui.backButton.image.color = Color.clear;

            foreach (var page in ui.settingsPages)
            {
                var rect = (RectTransform)page.transform;
                rect.anchoredPosition = new Vector2(656f, -385f);
                rect.localScale = new Vector3(.90f, .88f, 1f);
            }

            var display = (RectTransform)ui.settingsPages[0].transform;
            AlignPaperRow(panel, display, -59f, 1);
            AlignPaperRow(panel, display, -125f, 2);

            var sound = (RectTransform)ui.settingsPages[1].transform;
            AlignPaperRow(panel, sound, -59f, 1);
            AlignPaperRow(panel, sound, -139f, 2);

            var controls = (RectTransform)ui.settingsPages[2].transform;
            AlignPaperRow(panel, controls, -41f, 1);
            AlignPaperRow(panel, controls, -104f, 2);
            AlignPaperRow(panel, controls, -160f, 3);
            AlignPaperRow(panel, controls, -209f, 4);
            foreach (RectTransform item in controls)
            {
                if (item.name.StartsWith("Guide ", StringComparison.Ordinal) && item.GetComponent<Image>())
                    item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 52f);
            }

            var paw = panel.Find("Paw Progress") as RectTransform;
            if (paw)
            {
                paw.anchoredPosition = new Vector2(185f, 172f);
                paw.sizeDelta = new Vector2(94f, 94f);
                paw.GetComponent<Graphic>().color = Rule;
            }

            for (int i = 0; i < ui.tabButtons.Length; i++)
            {
                var tab = ui.tabButtons[i];
                var rect = (RectTransform)tab.transform;
                rect.anchoredPosition = new Vector2(185f, PaperY(panel, PaperRows[i + 1]));
                tab.image.color = Color.clear;
                if (!ui.selectedTabMark) continue;
                var mark = GameViewport.MakeRect("Selected Tab Mark", rect);
                Place(mark, .5f, .5f, 0, 0, 142, 48);
                mark.SetAsFirstSibling();
                tabMarks[i] = mark.gameObject.AddComponent<RawImage>();
                tabMarks[i].texture = ui.selectedTabMark;
                tabMarks[i].raycastTarget = false;
                mark.gameObject.SetActive(false);
            }

            foreach (var slider in new[] { ui.brightnessSlider, ui.musicSlider, ui.effectsSlider, ui.sensitivitySlider })
            {
                foreach (var image in slider.GetComponentsInChildren<Image>(true))
                    image.color = image.name == "Track" ? Rule : Ink;
            }
            ui.shakeToggle.targetGraphic.color = Rule;
            ui.shakeToggle.graphic.color = Ink;
            ratioDropdown.targetGraphic.color = new Color(.73f, .68f, .59f);
            ratioDropdown.template.GetComponent<Image>().color = Paper;
            var option = ratioDropdown.template.GetComponentInChildren<Toggle>(true);
            option.targetGraphic.color = Paper;
            option.graphic.color = Ink;
        }

        static float PaperY(RectTransform panel, float imageY) =>
            -imageY * panel.rect.height / PaperTextureHeight;

        static void AlignPaperRow(RectTransform panel, RectTransform page, float oldY, int index)
        {
            float pageTop = page.anchoredPosition.y + page.rect.height * (1f - page.pivot.y) * page.localScale.y;
            float rowY = (PaperY(panel, PaperRows[index]) - pageTop) / page.localScale.y;
            foreach (RectTransform item in page)
            {
                Vector2 pos = item.anchoredPosition;
                if (item.anchorMin.y == 1f && item.anchorMax.y == 1f && Mathf.Abs(pos.y - oldY) <= 1f)
                    item.anchoredPosition = new Vector2(pos.x, rowY);
            }
        }

        public void MarkTab(int index, bool selected)
        {
            var mark = tabMarks[index];
            if (mark) mark.gameObject.SetActive(selected);
            ui.tabButtons[index].image.color = hasPaper
                ? Color.clear
                : selected ? new Color(.30f, .33f, .36f) : new Color(.16f, .18f, .20f);
        }

        public void StyleGuides(Scrollbar scrollbar, TMP_Text[] actions, TMP_Text[] keys)
        {
            if (!hasPaper) return;
            scrollbar.GetComponent<Image>().color = new Color(.53f, .48f, .40f, .42f);
            scrollbar.targetGraphic.color = Ink;
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i].color = Ink;
                keys[i].transform.parent.GetComponent<Image>().color = new Color(.95f, .92f, .86f, .46f);
            }
        }

        public void SetConfirmation(bool open)
        {
            confirmation.SetActive(open);
            settingsPanel.interactable = !open;
            settingsPanel.blocksRaycasts = !open;
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(open && cancel.gameObject.activeInHierarchy ? cancel.gameObject : null);
        }

        public void SetLoading(bool loading)
        {
            confirm.interactable = cancel.interactable = !loading;
        }

        public void MarkRatio(int current) => ratioDropdown.SetValueWithoutNotify(current);

        static TMP_Dropdown CreateAspectDropdown(Transform parent, float y, TMP_Text source, Action<int> changed)
        {
            const float rowHeight = 40;
            var rect = GameViewport.MakeRect("Aspect Ratio Dropdown", parent);
            Place(rect, 0, 1, 375, y, 290, rowHeight);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(.16f, .18f, .20f);
            var caption = Label("", rect, .5f, .5f, 0, 0, 225, rowHeight, source, 22);
            Label("▼", rect, 1, .5f, -22, 0, 30, rowHeight, source, 18);

            var template = GameViewport.MakeRect("Options", rect);
            template.anchorMin = new Vector2(0, 0);
            template.anchorMax = new Vector2(1, 0);
            template.pivot = new Vector2(.5f, 1);
            template.anchoredPosition = Vector2.down * 4;
            template.sizeDelta = new Vector2(0, rowHeight * GameViewport.Names.Length);
            template.gameObject.AddComponent<Image>().color = new Color(.12f, .14f, .16f);

            var content = GameViewport.MakeRect("Content", template);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, rowHeight);
            var item = GameViewport.MakeRect("Option", content);
            GameViewport.Stretch(item, new Rect(0, 0, 1, 1));
            var optionBackground = item.gameObject.AddComponent<Image>();
            optionBackground.color = new Color(.18f, .20f, .22f);
            var selected = GameViewport.MakeRect("Selected", item);
            Place(selected, 0, .5f, 20, 0, 8, 8);
            var marker = selected.gameObject.AddComponent<Image>();
            marker.raycastTarget = false;
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = optionBackground;
            toggle.graphic = marker;
            var itemLabel = Label("", item, .5f, .5f, 0, 0, 225, rowHeight, source, 22);

            var dropdown = rect.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.AddOptions(new System.Collections.Generic.List<string>(GameViewport.Names));
            dropdown.onValueChanged.AddListener(value => changed(value));
            template.gameObject.SetActive(false);
            return dropdown;
        }


        Button CreateButton(string name, string text, Transform parent, float anchorX, float anchorY,
            float x, float y, float width, float height, Action clicked)
        {
            var button = Object.Instantiate(ui.backButton, parent, false);
            button.name = name;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => clicked());
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Place((RectTransform)button.transform, anchorX, anchorY, x, y, width, height);
            var label = CenterButtonLabel(button);
            if (hasPaper)
            {
                button.image.color = text == "메인화면" ? Color.clear : new Color(.73f, .68f, .59f);
                if (text == "메인화면") label.fontStyle |= FontStyles.Underline;
            }
            label.text = text;
            label.fontSize = Mathf.Min(label.fontSize, 22);
            return button;
        }

        static TMP_Text CenterButtonLabel(Button button)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            GameViewport.Stretch(label.rectTransform, new Rect(0, 0, 1, 1));
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        static TMP_Text Label(string text, Transform parent, float anchorX, float anchorY,
            float x, float y, float width, float height, TMP_Text source, float fontSize)
        {
            var rect = GameViewport.MakeRect("Text", parent);
            Place(rect, anchorX, anchorY, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = source.font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        static void Place(RectTransform rect, float anchorX, float anchorY, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, anchorY);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
