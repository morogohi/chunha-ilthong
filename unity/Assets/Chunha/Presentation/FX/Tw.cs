// 외부 트윈 라이브러리 없이 쓰는 작은 코루틴 트윈 모음.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Chunha.FX
{
    public static class Ease
    {
        public static float OutCubic(float t) { t = 1 - t; return 1 - t * t * t; }
        public static float InOutCubic(float t) => t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        public static float OutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }
        public static float InQuad(float t) => t * t;
    }

    public static class Tw
    {
        /// <summary>0→1 진행값을 넘겨주는 기본 트윈</summary>
        public static IEnumerator Run(float dur, Action<float> step, Func<float, float> ease = null)
        {
            ease ??= Ease.OutCubic;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                step(ease(Mathf.Clamp01(t / dur)));
                yield return null;
            }
            step(1);
        }

        public static IEnumerator Move(Transform tr, Vector3 to, float dur, Func<float, float> ease = null)
        {
            if (tr == null) yield break;
            var from = tr.position;
            yield return Run(dur, k => { if (tr) tr.position = Vector3.LerpUnclamped(from, to, k); }, ease);
        }

        public static IEnumerator Scale(Transform tr, Vector3 to, float dur, Func<float, float> ease = null)
        {
            if (tr == null) yield break;
            var from = tr.localScale;
            yield return Run(dur, k => { if (tr) tr.localScale = Vector3.LerpUnclamped(from, to, k); }, ease);
        }

        public static IEnumerator Fade(SpriteRenderer sr, float to, float dur)
        {
            if (sr == null) yield break;
            var c = sr.color; float from = c.a;
            yield return Run(dur, k => { if (sr) { c.a = Mathf.Lerp(from, to, k); sr.color = c; } });
        }

        public static IEnumerator Fade(CanvasGroup g, float to, float dur)
        {
            if (g == null) yield break;
            float from = g.alpha;
            yield return Run(dur, k => { if (g) g.alpha = Mathf.Lerp(from, to, k); });
        }

        public static IEnumerator Anchored(RectTransform rt, Vector2 to, float dur, Func<float, float> ease = null)
        {
            if (rt == null) yield break;
            var from = rt.anchoredPosition;
            yield return Run(dur, k => { if (rt) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k); }, ease);
        }

        public static IEnumerator Fill(Image img, float to, float dur, Func<float, float> ease = null)
        {
            if (img == null) yield break;
            float from = img.fillAmount;
            yield return Run(dur, k => { if (img) img.fillAmount = Mathf.Lerp(from, to, k); }, ease);
        }

        public static IEnumerator Wait(float s) { yield return new WaitForSeconds(s); }
    }

    /// <summary>카메라 흔들기</summary>
    public class CameraShake : MonoBehaviour
    {
        float amp, until;
        Vector3 offset;
        public void Shake(float amplitude, float duration) { amp = Mathf.Max(amp, amplitude); until = Time.time + duration; }

        void LateUpdate()
        {
            transform.localPosition -= offset;
            offset = Vector3.zero;
            if (Time.time < until)
            {
                float k = (until - Time.time);
                offset = (Vector3)(UnityEngine.Random.insideUnitCircle * amp * Mathf.Clamp01(k * 3));
                transform.localPosition += offset;
            }
            else amp = 0;
        }
    }
}
