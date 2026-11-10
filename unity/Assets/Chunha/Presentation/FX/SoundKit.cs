// 오디오 파일 없이 코드로 합성하는 효과음과 배경음악.
// 북(돌격), 징(일기토·승리), 붓 소리(화면 전환), 칼 부딪힘, 불길, 낙관, 버튼, 업적 팡파르,
// 그리고 고쟁 느낌의 현 뜯는 소리(카플러스-스트롱)로 만든 5음 음계 배경음악.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chunha.FX
{
    public enum Sfx { Drum, Gong, Swish, Clash, Fire, Stamp, Tick, Fanfare, March }

    public class SoundKit : MonoBehaviour
    {
        public static SoundKit I { get; private set; }
        const int Rate = 22050;
        AudioSource sfx, bgm;
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        float[] bgmData;
        volatile bool bgmReady;   // 백그라운드 스레드가 쓰고 메인 스레드가 읽는다

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt("chunha-sound", 1) == 1;
            set { PlayerPrefs.SetInt("chunha-sound", value ? 1 : 0); if (I) I.ApplyVolume(); }
        }

        void Awake()
        {
            I = this;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            bgm = gameObject.AddComponent<AudioSource>();
            bgm.playOnAwake = false; bgm.loop = true; bgm.volume = 0.32f;
            foreach (Sfx k in Enum.GetValues(typeof(Sfx))) clips[k] = Make(k.ToString(), Synth(k));
            ApplyVolume();
            // 배경음악은 32초 분량이라 백그라운드 스레드에서 합성한다 (첫 화면이 멈추지 않게)
            Task.Run(() => { bgmData = Bgm(); bgmReady = true; });
        }

        void Update()
        {
            if (!bgmReady) return;
            bgmReady = false;
            bgm.clip = Make("bgm", bgmData);
            bgmData = null;
            if (SoundOn) bgm.Play();
        }

        void ApplyVolume()
        {
            sfx.mute = !SoundOn;
            bgm.mute = !SoundOn;
            if (SoundOn && bgm.clip != null && !bgm.isPlaying) bgm.Play();
        }

        public static void Play(Sfx s, float volume = 1f)
        {
            if (I == null || !SoundOn) return;
            I.sfx.pitch = 1f + UnityEngine.Random.Range(-0.04f, 0.04f);   // 반복해도 덜 기계적으로
            I.sfx.PlayOneShot(I.clips[s], volume);
        }

        static AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        // ───────────── 합성
        static float[] Buf(float seconds) => new float[(int)(Rate * seconds)];
        static readonly System.Random rnd = new System.Random(7);
        static float Noise() => (float)(rnd.NextDouble() * 2 - 1);

        /// <summary>합성 후 최대 0.9로 맞춘다 (클리핑 방지, 효과음 간 음량 균형)</summary>
        static float[] Synth(Sfx k)
        {
            var d = SynthRaw(k);
            float peak = 0.001f;
            foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            float target = k == Sfx.Tick ? 0.4f : k == Sfx.March ? 0.6f : 0.9f;
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * target;
            return d;
        }

        static float[] SynthRaw(Sfx k)
        {
            switch (k)
            {
                case Sfx.Drum: return Drum(0.9f, 110, 48, 1f);
                case Sfx.March: return Drum(0.5f, 150, 70, 0.5f);
                case Sfx.Gong: return Gong();
                case Sfx.Swish: return Swish();
                case Sfx.Clash: return Clash();
                case Sfx.Fire: return Fire();
                case Sfx.Stamp: return Stamp();
                case Sfx.Tick: return Tick();
                default: return Fanfare();
            }
        }

        /// <summary>북: 높은 음에서 낮은 음으로 떨어지는 사인 + 가죽 치는 잡음</summary>
        static float[] Drum(float len, float f0, float f1, float gain)
        {
            var b = Buf(len); double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = f1 + (f0 - f1) * Mathf.Exp(-t * 18);
                ph += 2 * Math.PI * f / Rate;
                float body = (float)Math.Sin(ph) * Mathf.Exp(-t * 6);
                float skin = Noise() * Mathf.Exp(-t * 60) * 0.35f;
                b[i] = (body + skin) * 0.9f * gain;
            }
            return b;
        }

        /// <summary>징: 비정수배 배음들이 길게 울리며 맥놀이한다</summary>
        static float[] Gong()
        {
            var b = Buf(3.2f);
            float[] ratio = { 1f, 1.483f, 1.932f, 2.546f, 3.11f }, amp = { 1f, 0.6f, 0.45f, 0.3f, 0.2f };
            const float f = 98;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate, v = 0;
                for (int k = 0; k < ratio.Length; k++)
                    v += amp[k] * Mathf.Sin(2 * Mathf.PI * f * ratio[k] * t * (1 + 0.0015f * Mathf.Sin(t * 3))) * Mathf.Exp(-t * (0.9f + k * 0.5f));
                float attack = Mathf.Clamp01(t * 60);
                b[i] = v * 0.32f * attack + Noise() * Mathf.Exp(-t * 40) * 0.1f;
            }
            return b;
        }

        /// <summary>붓 획: 잡음을 걸러 부드럽게 올렸다 내린다</summary>
        static float[] Swish()
        {
            var b = Buf(0.45f); float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)b.Length;
                float env = Mathf.Sin(t * Mathf.PI) * Mathf.Sin(t * Mathf.PI);
                float cutoff = 0.08f + 0.25f * t;
                lp += (Noise() - lp) * cutoff;
                b[i] = lp * env * 1.6f;
            }
            return b;
        }

        static float[] Clash()
        {
            var b = Buf(0.7f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float ring = Mathf.Sin(2 * Mathf.PI * 2350 * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 3720 * t) * 0.35f + Mathf.Sin(2 * Mathf.PI * 5130 * t) * 0.2f;
                b[i] = (ring * Mathf.Exp(-t * 9) + Noise() * Mathf.Exp(-t * 80)) * 0.45f;
            }
            return b;
        }

        /// <summary>불길: 낮게 깔리는 바람 소리 + 탁탁 튀는 소리</summary>
        static float[] Fire()
        {
            var b = Buf(1.6f); float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)b.Length;
                lp += (Noise() - lp) * 0.04f;
                float crackle = rnd.NextDouble() < 0.0018 ? Noise() * 2 : 0;
                b[i] = (lp * 2.2f + crackle) * Mathf.Sin(Mathf.Min(1, t * 4) * Mathf.PI * 0.5f) * (1 - t * t) * 0.6f;
            }
            for (int i = 1; i < b.Length; i++) b[i] = b[i] * 0.7f + b[i - 1] * 0.3f;
            return b;
        }

        static float[] Stamp()
        {
            var b = Buf(0.35f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = (Mathf.Sin(2 * Mathf.PI * 70 * t) * Mathf.Exp(-t * 25) + Noise() * Mathf.Exp(-t * 90) * 0.5f) * 0.8f;
            }
            return b;
        }

        static float[] Tick()
        {
            var b = Buf(0.04f);
            for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; b[i] = Mathf.Sin(2 * Mathf.PI * 1800 * t) * Mathf.Exp(-t * 160) * 0.35f; }
            return b;
        }

        /// <summary>업적: 5음 음계로 올라가는 현 뜯는 소리</summary>
        static float[] Fanfare()
        {
            var b = Buf(1.6f);
            float[] notes = { 293.66f, 349.23f, 392f, 440f, 587.33f };
            for (int n = 0; n < notes.Length; n++) Pluck(b, (int)(n * 0.12f * Rate), notes[n], 0.5f, 0.994f);
            return b;
        }

        /// <summary>카플러스-스트롱 현 뜯기 (고쟁·가야금 느낌)</summary>
        static void Pluck(float[] dst, int start, float freq, float amp, float decay)
        {
            int n = Mathf.Max(2, (int)(Rate / freq));
            var ring = new float[n];
            for (int i = 0; i < n; i++) ring[i] = Noise() * amp;
            int idx = 0;
            int len = Mathf.Min(dst.Length - start, Rate * 3);
            for (int i = 0; i < len; i++)
            {
                int j = (idx + 1) % n;
                float v = ring[idx];
                ring[idx] = decay * 0.5f * (ring[idx] + ring[j]);
                idx = j;
                dst[start + i] += v;
            }
        }

        /// <summary>배경음악 32초 루프: 낮은 지속음 + 느린 5음 음계 선율 + 가끔 북</summary>
        static float[] Bgm()
        {
            const float seconds = 32f;
            var b = Buf(seconds);
            var r = new System.Random(2026);
            float[] scale = { 146.83f, 174.61f, 196f, 220f, 261.63f, 293.66f, 349.23f, 392f, 440f };   // D 단조 5음 음계 (D F G A C)
            // 지속음 (D2 + A2, 느린 떨림)
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float sw = 0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * t / seconds);   // 루프 경계에서 이어지도록 주기를 맞춘다
                b[i] += (Mathf.Sin(2 * Mathf.PI * 73.42f * t) * 0.10f + Mathf.Sin(2 * Mathf.PI * 110f * t) * 0.05f) * (0.6f + 0.4f * sw);
            }
            // 선율: 한 음계 안에서 가까운 음으로 걷는다
            int pos = 4; float time = 0.5f;
            while (time < seconds - 2.5f)
            {
                pos = Mathf.Clamp(pos + r.Next(-2, 3), 0, scale.Length - 1);
                Pluck(b, (int)(time * Rate), scale[pos], 0.28f, 0.996f);
                if (r.NextDouble() < 0.25) Pluck(b, (int)((time + 0.09f) * Rate), scale[Mathf.Max(0, pos - 2)], 0.16f, 0.995f);
                time += r.NextDouble() < 0.3 ? 1.0f : r.NextDouble() < 0.5 ? 0.5f : 0.75f;
            }
            // 8초마다 낮은 북
            var drum = Drum(0.9f, 90, 45, 0.35f);
            for (float t = 0; t < seconds; t += 8f)
            {
                int s0 = (int)(t * Rate);
                for (int i = 0; i < drum.Length && s0 + i < b.Length; i++) b[s0 + i] += drum[i];
            }
            // 끝 0.5초를 처음과 겹쳐 이음매를 없앤다
            int fade = Rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                b[i] = b[i] * k + b[b.Length - fade + i] * (1 - k);
            }
            var outBuf = new float[b.Length - fade];
            Array.Copy(b, outBuf, outBuf.Length);
            float peak = 0.001f;
            foreach (var v in outBuf) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < outBuf.Length; i++) outBuf[i] = outBuf[i] / peak * 0.8f;
            return outBuf;
        }

        /// <summary>테스트용: 합성 결과를 그대로 돌려준다 (Unity 밖에서 파형을 확인할 때)</summary>
        public static float[] DebugSynth(Sfx k) => Synth(k);
        public static float[] DebugBgm() => Bgm();
    }

    /// <summary>진동 (큰 사건에만 짧게)</summary>
    public static class Haptics
    {
        public static bool On
        {
            get => PlayerPrefs.GetInt("chunha-haptics", 1) == 1;
            set => PlayerPrefs.SetInt("chunha-haptics", value ? 1 : 0);
        }

        public static void Heavy()
        {
            if (!On) return;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
