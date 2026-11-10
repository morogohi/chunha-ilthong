// uGUI를 프리팹 없이 코드로 구성하는 도우미. 기준 해상도 1080×1920 (세로).
using System;
using System.Collections.Generic;
using Chunha.Art;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chunha.UI
{
    public static class UIKit
    {
        public const float W = 1080, H = 1920;

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(W, H);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0f;   // 세로 화면: 폭 기준. 고정 셀 폭이 어떤 화면비에서도 넘치지 않는다
            go.AddComponent<GraphicRaycaster>();
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                UnityEngine.Object.DontDestroyOnLoad(es);
            }
            return c;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(this RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>상단/하단에 붙는 띠 (높이 h)</summary>
        public static RectTransform Dock(this RectTransform rt, bool top, float h, float y = 0)
        {
            rt.anchorMin = new Vector2(0, top ? 1 : 0); rt.anchorMax = new Vector2(1, top ? 1 : 0);
            rt.pivot = new Vector2(0.5f, top ? 1 : 0);
            rt.sizeDelta = new Vector2(0, h);
            rt.anchoredPosition = new Vector2(0, top ? -y : y);
            return rt;
        }

        public static Image Panel(Transform parent, Color color, string name = "Panel", bool rounded = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded) { img.sprite = ProcArt.RoundRect; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.2f; }
            return img;
        }

        public static Text Label(Transform parent, string text, int size = 40, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = ProcArt.Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color ?? Palette.Ink;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.lineSpacing = 1.15f;
            return t;
        }

        public static Button Button(Transform parent, string text, Action onClick, Color? bg = null, Color? fg = null, int size = 42)
        {
            var img = Panel(parent, bg ?? Palette.Paper2, "Button");
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(1, 1, 1, 0.4f);
            b.colors = colors;
            b.targetGraphic = img;
            var t = Label(img.transform, text, size, fg ?? Palette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch(16, 4, 16, 4);
            if (onClick != null) b.onClick.AddListener(() => onClick());
            img.gameObject.AddComponent<PressFx>();   // 눌림 피드백 + 딸깍 소리
            Size(img.rectTransform, -1, 120);
            return b;
        }

        public static void SetText(this Button b, string text) { var t = b.GetComponentInChildren<Text>(); if (t) t.text = text; }

        public static LayoutElement Size(RectTransform rt, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>(); // Unity 객체는 ?? 대신 == null로 검사한다
            if (w >= 0) le.preferredWidth = w;
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static VerticalLayoutGroup VList(RectTransform rt, float spacing = 16, int pad = 0)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            return v;
        }

        public static HorizontalLayoutGroup HList(RectTransform rt, float spacing = 16, int pad = 0)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.childControlHeight = true; h.childControlWidth = true;
            h.childForceExpandHeight = true; h.childForceExpandWidth = true;
            return h;
        }

        public static GridLayoutGroup Grid(RectTransform rt, int cols, float cellH, float spacing = 16)
        {
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = cols;
            g.spacing = new Vector2(spacing, spacing);
            g.cellSize = new Vector2((W - 96 - spacing * (cols - 1)) / cols, cellH);
            return g;
        }

        public static void FitHeight(RectTransform rt)
        {
            var f = rt.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>세로 스크롤 영역. 반환값은 내용을 넣을 Content.</summary>
        public static RectTransform Scroll(RectTransform area, float spacing = 16, int pad = 24)
        {
            var sr = area.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30;
            var vp = Rect("Viewport", area).Stretch();
            vp.gameObject.AddComponent<RectMask2D>();
            var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(0, 0, 0, 0.001f);
            var content = Rect("Content", vp);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            VList(content, spacing, pad);
            FitHeight(content);
            sr.viewport = vp;
            sr.content = content;
            return content;
        }

        /// <summary>막대 (능력치, 병력, 사기)</summary>
        public static Image Bar(Transform parent, Color fill, float h = 14)
        {
            var bg = Panel(parent, new Color(0.87f, 0.84f, 0.77f), "Bar");
            Size(bg.rectTransform, -1, h);
            var f = Panel(bg.transform, fill, "Fill");
            f.rectTransform.Stretch();
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.sprite = ProcArt.White;   // null이면 fill이 무시된다
            f.fillAmount = 1;
            return f;
        }

        public static void Clear(Transform t)
        {
            // Destroy는 프레임 끝에 일어나므로, 먼저 꺼서 떼어 내야 같은 프레임 레이아웃에 옛 내용이 섞이지 않는다
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                c.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(c);
            }
        }

        public static Color FactionColor(string hex) => string.IsNullOrEmpty(hex) ? Palette.Neutral : Palette.Hex(hex);
    }

    /// <summary>아래에서 올라오는 시트. 배경을 누르면 닫힌다.</summary>
    public class Sheet
    {
        public RectTransform root, body, content;
        public Text title;
        readonly MonoBehaviour host;
        public Action onClosed;

        public Sheet(Transform canvas, MonoBehaviour host, string titleText, float heightRatio = 0.72f, bool closable = true)
        {
            this.host = host;
            root = UIKit.Rect("Sheet", canvas).Stretch();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.12f, 0.11f, 0.1f, 0.45f);
            if (closable) { var b = root.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.onClick.AddListener(Close); }
            var panel = UIKit.Panel(root, Palette.Paper, "Body");
            body = panel.rectTransform;
            body.anchorMin = new Vector2(0, 0); body.anchorMax = new Vector2(1, heightRatio); body.offsetMin = Vector2.zero; body.offsetMax = new Vector2(0, 24);
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // 안쪽 클릭이 배경으로 새지 않게
            var head = UIKit.Rect("Head", body).Dock(true, 130);
            title = UIKit.Label(head, titleText, 54, Palette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.Stretch(48, 0, 220, 0);
            if (closable)
            {
                var close = UIKit.Button(head, "닫기", Close, new Color(0, 0, 0, 0), Palette.Ink2, 38);
                var crt = (RectTransform)close.transform;
                crt.anchorMin = new Vector2(1, 0); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 0.5f);
                crt.sizeDelta = new Vector2(200, 0); crt.anchoredPosition = new Vector2(-24, 0);
            }
            // 바닥은 화면 끝까지 칠하고, 내용은 홈 인디케이터 위에서 끝나게 한다
            var area = UIKit.Rect("Area", body).Stretch(0, SafeArea.BottomInset(), 0, 130);
            content = UIKit.Scroll(area, 18, 48);
            host.StartCoroutine(SlideIn());
        }

        System.Collections.IEnumerator SlideIn()
        {
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            var to = body.anchoredPosition;
            body.anchoredPosition = to + new Vector2(0, -900);
            host.StartCoroutine(FX.Tw.Fade(cg, 1, 0.18f));
            yield return FX.Tw.Anchored(body, to, 0.32f, FX.Ease.OutCubic);
        }

        public void Close()
        {
            if (root == null) return;
            UnityEngine.Object.Destroy(root.gameObject);
            root = null;
            onClosed?.Invoke();
        }

        public bool Open => root != null;
    }

    /// <summary>화면 아래 잠깐 뜨는 알림</summary>
    public class Toast : MonoBehaviour
    {
        Text text; CanvasGroup cg; float until;

        public static Toast Create(Transform canvas)
        {
            var img = UIKit.Panel(canvas, Palette.Ink, "Toast");
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.06f, 0); rt.anchorMax = new Vector2(0.94f, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, 120); rt.anchoredPosition = new Vector2(0, 260 + SafeArea.BottomInset());
            var t = img.gameObject.AddComponent<Toast>();
            t.text = UIKit.Label(rt, "", 38, Palette.Paper, TextAnchor.MiddleCenter);
            t.text.rectTransform.Stretch(24, 0, 24, 0);
            t.cg = img.gameObject.AddComponent<CanvasGroup>();
            t.cg.alpha = 0; t.cg.blocksRaycasts = false;
            return t;
        }

        public void Show(string msg, float seconds = 2.2f) { text.text = msg; until = Time.unscaledTime + seconds; transform.SetAsLastSibling(); }

        void Update() => cg.alpha = Mathf.MoveTowards(cg.alpha, Time.unscaledTime < until ? 1 : 0, Time.unscaledDeltaTime * 5);
    }
}
