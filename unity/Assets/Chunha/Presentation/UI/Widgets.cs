// UI 위젯: 버튼 눌림 피드백, 능력치 레이더 차트, 숫자 굴러가기.
using Chunha.FX;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chunha.UI
{
    /// <summary>누르면 살짝 줄었다가 튕겨 돌아오고, 딸깍 소리를 낸다</summary>
    public class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float target = 1f, scale = 1f;
        public void OnPointerDown(PointerEventData e)
        {
            var sel = GetComponent<Selectable>();
            if (sel != null && !sel.interactable) return;
            target = 0.95f;
            SoundKit.Play(Sfx.Tick, 0.6f);
        }
        public void OnPointerUp(PointerEventData e) => target = 1f;
        public void OnPointerExit(PointerEventData e) => target = 1f;

        void Update()
        {
            // 돌아올 때는 살짝 넘쳤다 돌아오는 스프링
            float k = target < 1 ? 30f : 14f;
            scale = Mathf.Lerp(scale, target, Time.unscaledDeltaTime * k);
            if (target >= 1 && Mathf.Abs(scale - 1) < 0.002f) scale = 1;
            transform.localScale = new Vector3(scale, scale, 1);
        }
    }

    /// <summary>육각 레이더 차트 (무력·지력·정치·매력·육전·수전). 값은 0~1.</summary>
    public class RadarGraphic : MaskableGraphic
    {
        public float[] values = { 1, 1, 1, 1, 1, 1 };
        public float reveal = 1f;   // 0→1 로 펼쳐지는 연출

        public void SetValues(float[] v) { values = v; SetVerticesDirty(); }
        public void SetReveal(float r) { reveal = r; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            var c = r.center;
            Color32 col = color;
            vh.AddVert(new Vector3(c.x, c.y, 0), col, Vector4.zero);
            int n = values.Length;
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.PI / 2 - i * Mathf.PI * 2 / n;   // 12시 방향부터 시계 방향
                float v = Mathf.Clamp01(values[i]) * reveal;
                vh.AddVert(new Vector3(c.x + Mathf.Cos(ang) * radius * v, c.y + Mathf.Sin(ang) * radius * v, 0), col, Vector4.zero);
            }
            for (int i = 0; i < n; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % n);
        }
    }

    /// <summary>숫자가 목표값까지 굴러간다 (자원 표시)</summary>
    public class CountUp : MonoBehaviour
    {
        public Text text; public string prefix = "";
        double shown, target;
        public void Set(double v, bool instant = false) { target = v; if (instant) shown = v; }
        void Update()
        {
            if (text == null) return;
            if (System.Math.Abs(shown - target) < 0.5) shown = target;
            else shown += (target - shown) * Mathf.Min(1, Time.unscaledDeltaTime * 8);
            text.text = prefix + Chunha.Core.G.Fmt(shown);
        }
    }
}
