using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualAudio : MonoBehaviour
    {
        struct Queued
        {
            public float at;
            public Vector3 position;
            public int units;
            public float volume;
            public bool windChime;
        }

        [Header("Chime")]
        [SerializeField] float chimeHz = 528f;
        [SerializeField] float semitonesPerUnit = 0.35f;
        [SerializeField, Range(0f, 1f)] float chimeVolume = 0.38f;
        [SerializeField, Range(0f, 1f)] float markVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] float downVolume = 0.14f;

        [Header("Ambient BGM")]
        [SerializeField] AudioClip bgmClip;
        [SerializeField, Range(0f, 1f)] float bgmVolume = 0.32f;

        [Header("Sand")]
        [SerializeField, Range(0f, 1f)] float sandVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] float glassVolume = 0.2f;
        [SerializeField] float glassHz = 196f;
        [SerializeField] float glassInterval = 0.085f;

        [Header("Seal")]
        [SerializeField, Range(0f, 1f)] float sealVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] float bowlVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] float clockVolume = 0.45f;
        [SerializeField] float sealHz = 92f;
        [SerializeField, Range(0f, 1f)] float pressVolume = 0.14f;

        [Header("Voices")]
        [SerializeField] int voices = 8;
        [SerializeField, Range(0f, 1f)] float spatialBlend = 0.55f;

        readonly List<Queued> _queue = new List<Queued>(64);
        static readonly int[] Pentatonic = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };
        AudioSource[] _pool;
        AudioSource _sand;
        AudioSource _press;
        AudioSource _bowlVoice;
        AudioClip _bowl;
        AudioClip _clockTick;
        AudioClip _clockTock;
        float _hush;
        bool _bgmHeld;
        AudioSource _bgm;
        AudioClip _tick;
        AudioClip _mark30;
        AudioClip _mark60;
        AudioClip _glass;
        AudioClip _seal;
        AudioClip _soft;
        AudioClip _kneadJelly;
        AudioClip _kneadWater;
        AudioClip _kneadFur;
        AudioClip _chimeTick;
        AudioClip _chimeMark30;
        AudioClip _chimeMark60;
        AudioClip _chimeSoft;
        AudioClip _kneadChime;
        int _next;
        float _clock;
        float _sandTarget;
        float _pressTarget;
        float _lastGlass = -1f;
        float _master = 1f;
        bool _ready;

        public void Init(float master)
        {
            _master = master;
            if (_ready || !Application.isPlaying) return;
            _ready = true;
            int rate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            _tick = BakeWaterDrop("ritual-tick", rate, 0.24f, new[]
            {
                (0f, 560f, 980f, 38f, 28f, 0.68f),
                (0.034f, 840f, 1280f, 46f, 42f, 0.24f),
            });
            _mark30 = BakeWaterDrop("ritual-mark30", rate, 0.34f, new[]
            {
                (0f, 500f, 880f, 36f, 24f, 0.60f),
                (0.042f, 720f, 1160f, 42f, 28f, 0.45f),
                (0.082f, 940f, 1420f, 46f, 32f, 0.35f),
            });
            _mark60 = BakeWaterDrop("ritual-mark60", rate, 0.42f, new[]
            {
                (0f, 420f, 740f, 34f, 20f, 0.55f),
                (0.038f, 620f, 1040f, 38f, 24f, 0.46f),
                (0.076f, 820f, 1320f, 42f, 26f, 0.38f),
                (0.112f, 1020f, 1560f, 46f, 30f, 0.30f),
            });
            _glass = Bake("ritual-glass", rate, 0.22f, (t, n) =>
            {
                float w = 2f * Mathf.PI * (glassHz * 2.2f) * t;
                float attack = Mathf.Min(1f, t / 0.004f);
                return attack * (Mathf.Sin(w) * Mathf.Exp(-t * 22f) + 0.35f * Mathf.Sin(w * 2.38f) * Mathf.Exp(-t * 36f)) * 0.5f;
            });
            _seal = Bake("ritual-seal", rate, 1.65f, (t, n) =>
            {
                float phase = 2f * Mathf.PI * (sealHz * 0.62f * t + sealHz * 0.38f * (1f - Mathf.Exp(-t * 8f)) / 8f);
                float thudAtk = Mathf.Min(1f, t / 0.012f);
                float thud = thudAtk * (Mathf.Sin(phase) * Mathf.Exp(-t * 4.8f) + 0.3f * Mathf.Sin(phase * 2f) * Mathf.Exp(-t * 8f)) * 0.55f;
                float bellAtk = Mathf.Min(1f, t / 0.045f);
                bellAtk = bellAtk * bellAtk * (3f - 2f * bellAtk);
                float chord = (Mathf.Sin(2f * Mathf.PI * 264f * t) * 0.4f
                    + Mathf.Sin(2f * Mathf.PI * 396f * t) * 0.32f
                    + Mathf.Sin(2f * Mathf.PI * 528f * t) * 0.28f
                    + Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.2f) * Mathf.Exp(-t * 1.9f) * bellAtk * 0.65f;
                return thud + chord + n * 0.03f * Mathf.Exp(-t * 110f);
            });
            _soft = BakeWaterDrop("ritual-soft", rate, 0.20f, new[]
            {
                (0f, 680f, 480f, 28f, 32f, 0.45f),
            });
            _kneadJelly = Bake("ritual-knead-jelly", rate, 0.22f, (t, n) =>
            {
                float atk = Mathf.Sin(Mathf.Min(1f, t / 0.016f) * Mathf.PI * 0.5f);
                return atk * (Mathf.Sin(2f * Mathf.PI * 246.94f * t) * 0.55f + Mathf.Sin(2f * Mathf.PI * 370.0f * t) * 0.30f) * Mathf.Exp(-t * 14f) * 0.42f;
            });
            _kneadWater = Bake("ritual-knead-water", rate, 0.14f, (t, n) =>
            {
                float atk = Mathf.Min(1f, t / 0.005f);
                float u = Mathf.Clamp01(t / 0.14f);
                float phase = 2f * Mathf.PI * (380f * t + 150f * t * u);
                return atk * Mathf.Sin(phase) * Mathf.Exp(-t * 22f) * 0.5f;
            });
            _kneadFur = BakeVelvet(rate);
            _chimeTick = BakeMusicBoxStardust("ritual-chime-tick", rate, 0.95f,
                new[]
                {
                    (0f, 1174.66f, 2.8f, 0.56f),
                },
                new[]
                {
                    (0.022f, 2637.02f, 0.14f),
                    (0.048f, 3135.96f, 0.12f),
                    (0.078f, 3520.00f, 0.10f),
                    (0.112f, 4186.01f, 0.08f),
                });
            _chimeMark30 = BakeMusicBoxStardust("ritual-chime-mark30", rate, 1.15f,
                new[]
                {
                    (0f, 1174.66f, 2.5f, 0.52f),
                    (0.052f, 1760.00f, 2.6f, 0.44f),
                },
                new[]
                {
                    (0.026f, 2349.32f, 0.15f),
                    (0.058f, 2637.02f, 0.14f),
                    (0.092f, 3135.96f, 0.13f),
                    (0.128f, 3520.00f, 0.11f),
                    (0.168f, 3951.07f, 0.10f),
                    (0.212f, 4698.63f, 0.08f),
                });
            _chimeMark60 = BakeMusicBoxStardust("ritual-chime-mark60", rate, 1.35f,
                new[]
                {
                    (0f, 1046.50f, 2.3f, 0.50f),
                    (0.048f, 1567.98f, 2.4f, 0.44f),
                    (0.096f, 2093.00f, 2.6f, 0.38f),
                },
                new[]
                {
                    (0.030f, 2093.00f, 0.15f),
                    (0.062f, 2637.02f, 0.15f),
                    (0.096f, 3135.96f, 0.14f),
                    (0.132f, 3520.00f, 0.13f),
                    (0.170f, 4186.01f, 0.12f),
                    (0.210f, 3135.96f, 0.10f),
                    (0.252f, 3951.07f, 0.09f),
                    (0.298f, 4698.63f, 0.08f),
                });
            _chimeSoft = BakeMusicBoxStardust("ritual-chime-soft", rate, 0.70f,
                new[]
                {
                    (0f, 1318.51f, 3.4f, 0.38f),
                },
                new[]
                {
                    (0.024f, 2637.02f, 0.12f),
                    (0.056f, 3135.96f, 0.09f),
                });
            _kneadChime = BakeMusicBoxStardust("ritual-knead-chime", rate, 0.90f,
                new[]
                {
                    (0f, 1318.51f, 2.8f, 0.36f),
                    (0.065f, 1975.53f, 3.0f, 0.30f),
                },
                new[]
                {
                    (0.015f, 2349.32f, 0.16f),
                    (0.042f, 2637.02f, 0.15f),
                    (0.072f, 3135.96f, 0.15f),
                    (0.105f, 3520.00f, 0.14f),
                    (0.140f, 3951.07f, 0.13f),
                    (0.178f, 4186.01f, 0.11f),
                    (0.220f, 4698.63f, 0.09f),
                });

            _pool = new AudioSource[Mathf.Max(2, voices)];
            for (int i = 0; i < _pool.Length; i++) _pool[i] = Voice("Voice" + i, false);
            _sand = Voice("Sand", true);
            _sand.clip = BakeSand(rate);
            _press = Voice("Press", true);
            _press.clip = Bake("ritual-press", rate, 1f, (t, n) => (Mathf.Sin(2f * Mathf.PI * 132f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 198f * t)) * 0.32f);
            _bowl = BakeBowl(rate);
            _clockTick = BakeClockTick(rate, false);
            _clockTock = BakeClockTick(rate, true);
            _bowlVoice = Voice("Bowl", false);
            _bowlVoice.spatialBlend = spatialBlend * 0.5f;
            _bowlVoice.maxDistance = 6f;
            _bgm = Voice("Bgm", true);
            _bgm.spatialBlend = 0f;
            _bgm.clip = bgmClip != null ? bgmClip : BakeBgm(rate);
            _bgm.volume = Mathf.Clamp01(bgmVolume * _master);
            _bgm.Play();
        }

        public static AudioClip BakeBowl(int rate)
        {
            const float Tau = 6.2831853f;
            return Bake("ritual-bowl", rate, 9f, (t, n) =>
            {
                float swell = 1f - Mathf.Exp(-t / 0.18f);
                const float f0 = 174.6f;
                float wob = 1f + 0.0025f * Mathf.Sin(Tau * 0.31f * t);
                float a = Mathf.Exp(-t / 4.2f) * Mathf.Sin(Tau * f0 * wob * t) * (0.8f + 0.2f * Mathf.Sin(Tau * 0.9f * t));
                a += 0.42f * Mathf.Exp(-t / 3.0f) * Mathf.Sin(Tau * f0 * 2.76f * t + 0.3f) * (0.85f + 0.15f * Mathf.Sin(Tau * 1.3f * t));
                a += 0.18f * Mathf.Exp(-t / 2.0f) * Mathf.Sin(Tau * f0 * 5.40f * t + 1.1f);
                float shimmerEnv = Mathf.Clamp01((t - 0.3f) / 1.2f) * Mathf.Exp(-t / 3.2f);
                float shimmer = Mathf.Sin(Tau * 1318.5f * t) * (0.5f + 0.5f * Mathf.Sin(Tau * 0.37f * t))
                    + Mathf.Sin(Tau * 1975.5f * t + 0.7f) * (0.5f + 0.5f * Mathf.Sin(Tau * 0.53f * t + 1f))
                    + 0.6f * Mathf.Sin(Tau * 2637f * t + 2f) * (0.5f + 0.5f * Mathf.Sin(Tau * 0.71f * t + 2f));
                a += 0.05f * shimmerEnv * shimmer;
                a += 0.012f * Mathf.Exp(-t / 1.5f) * n * swell;
                return a * 0.2f * swell;
            });
        }

        public static AudioClip BakeClockTick(int rate, bool tock)
        {
            const float Tau = 6.2831853f;
            float f = tock ? 2350f : 3050f;
            return Bake(tock ? "ritual-clock-tock" : "ritual-clock-tick", rate, 0.09f, (t, n) =>
            {
                float click = Mathf.Exp(-t / 0.0045f);
                float body = Mathf.Exp(-t / 0.018f) * Mathf.Sin(Tau * f * 0.42f * t);
                return (0.55f * Mathf.Sin(Tau * f * t) * click + 0.35f * n * click + 0.3f * body) * 0.5f;
            });
        }

        public void ClockTick(Vector3 position, bool tock)
        {
            if (!_ready) return;
            Play(tock ? _clockTock : _clockTick, position, clockVolume, 1f);
        }

        public bool BgmHeld => _bgmHeld;

        public void HoldBgm(bool held)
        {
            _bgmHeld = held;
            if (held && _bgm != null) _bgm.volume = 0f;
        }

        public float BowlLength => _bowl != null ? _bowl.length : 0f;
        public bool Hushed => _hush > 0f;

        public void Bowl(Vector3 position)
        {
            if (!_ready || _bowlVoice == null) return;
            _bowlVoice.transform.position = position;
            _bowlVoice.clip = _bowl;
            _bowlVoice.volume = Mathf.Clamp01(bowlVolume * _master);
            _bowlVoice.Play();
        }

        public void Hush(float seconds)
        {
            _hush = Mathf.Max(_hush, seconds);
            _pressTarget = 0f;
            _sandTarget = 0f;
            if (_bgm != null) _bgm.volume = 0f;
        }

        AudioSource Voice(string label, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = loop ? spatialBlend * 0.6f : spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.25f;
            source.maxDistance = 3f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            return source;
        }

        static AudioClip Bake(string label, int rate, float seconds, Func<float, float, float> wave)
        {
            int samples = Mathf.Max(8, Mathf.RoundToInt(rate * seconds));
            var data = new float[samples];
            uint state = 2463534242u;
            int fade = Mathf.Min(samples / 4, rate / 100);
            for (int i = 0; i < samples; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float noise = (state & 0xFFFF) / 32768f - 1f;
                float v = wave(i / (float)rate, noise);
                if (i > samples - fade) v *= (samples - i) / (float)fade;
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create(label, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip BakeWaterDrop(string label, int rate, float seconds, (float delay, float f0, float f1, float chirpRate, float decay, float amp)[] drops)
        {
            int samples = Mathf.Max(8, Mathf.RoundToInt(rate * seconds));
            var data = new float[samples];
            var phases = new float[drops.Length];
            var echoPhases = new float[drops.Length];
            int fade = Mathf.Min(samples / 4, rate / 100);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float sum = 0f;
                for (int k = 0; k < drops.Length; k++)
                {
                    var d = drops[k];
                    if (t >= d.delay)
                    {
                        float dt = t - d.delay;
                        float freq = Mathf.Lerp(d.f0, d.f1, 1f - Mathf.Exp(-dt * d.chirpRate));
                        phases[k] += 2f * Mathf.PI * freq / rate;
                        float atk = Mathf.Sin(Mathf.Min(1f, dt / 0.003f) * Mathf.PI * 0.5f);
                        sum += Mathf.Sin(phases[k]) * atk * Mathf.Exp(-dt * d.decay) * d.amp;
                    }
                    float echoDelay = d.delay + 0.018f;
                    if (t >= echoDelay)
                    {
                        float et = t - echoDelay;
                        float eFreq = Mathf.Lerp(d.f0, d.f1, 1f - Mathf.Exp(-et * d.chirpRate)) * 1.005f;
                        echoPhases[k] += 2f * Mathf.PI * eFreq / rate;
                        float eatk = Mathf.Sin(Mathf.Min(1f, et / 0.003f) * Mathf.PI * 0.5f);
                        sum += Mathf.Sin(echoPhases[k]) * eatk * Mathf.Exp(-et * d.decay * 0.85f) * d.amp * 0.16f;
                    }
                }
                if (i > samples - fade) sum *= (samples - i) / (float)fade;
                data[i] = Mathf.Clamp(sum, -1f, 1f);
            }
            var clip = AudioClip.Create(label, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip BakeMusicBoxStardust(
            string label,
            int rate,
            float seconds,
            (float delay, float freq, float decay, float amp)[] tines,
            (float delay, float freq, float amp)[] dust)
        {
            int samples = Mathf.Max(8, Mathf.RoundToInt(rate * seconds));
            var data = new float[samples];
            int fade = Mathf.Min(samples / 4, rate / 10);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float sum = 0f;
                for (int k = 0; k < tines.Length; k++)
                {
                    var n = tines[k];
                    if (t >= n.delay) sum += MusicBoxTine(t - n.delay, n.freq, n.decay) * n.amp;
                    float echo1 = n.delay + 0.055f;
                    if (t >= echo1) sum += MusicBoxTine(t - echo1, n.freq, n.decay * 1.1f) * (n.amp * 0.18f);
                }
                for (int k = 0; k < dust.Length; k++)
                {
                    var d = dust[k];
                    if (t >= d.delay) sum += StardustGrain(t - d.delay, d.freq) * d.amp;
                }
                if (i > samples - fade) sum *= (samples - i) / (float)fade;
                data[i] = Mathf.Clamp(sum, -1f, 1f);
            }
            var clip = AudioClip.Create(label, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float MusicBoxTine(float dt, float f, float decay)
        {
            if (dt > 1.2f) return 0f;
            float atk = 1f - Mathf.Exp(-dt * 1800f);
            float pin = (Mathf.Sin(2f * Mathf.PI * (f * 5.41f) * dt) * 0.20f
                + Mathf.Sin(2f * Mathf.PI * (f * 3.00f) * dt) * 0.12f) * Mathf.Exp(-dt * 95f);
            float env = 0.68f * Mathf.Exp(-dt * (decay * 2.5f)) + 0.32f * Mathf.Exp(-dt * decay);
            float tone = Mathf.Sin(2f * Mathf.PI * f * dt) * env
                + Mathf.Sin(2f * Mathf.PI * (f * 2.0f) * dt) * (0.14f * Mathf.Exp(-dt * (decay * 2.8f)));
            return atk * (tone + pin);
        }

        static float StardustGrain(float dt, float f)
        {
            if (dt > 0.16f) return 0f;
            float atk = 1f - Mathf.Exp(-dt * 2400f);
            float env = Mathf.Exp(-dt * 34f);
            float s1 = Mathf.Sin(2f * Mathf.PI * f * dt);
            float s2 = Mathf.Sin(2f * Mathf.PI * (f * 1.4983f) * dt) * 0.28f;
            return atk * env * (s1 + s2);
        }

        static AudioClip BakeVelvet(int rate)
        {
            int samples = Mathf.Max(16, Mathf.RoundToInt(rate * 0.18f));
            var data = new float[samples];
            uint state = 192837465u;
            float lp = 0f, hp = 0f;
            for (int i = 0; i < samples; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float white = (state & 0xFFFF) / 32768f - 1f;
                lp += (white - lp) * 0.18f;
                hp += (lp - hp) * 0.04f;
                float t = i / (float)samples;
                float env = Mathf.Sin(t * Mathf.PI);
                data[i] = Mathf.Clamp((lp - hp) * env * 1.15f, -1f, 1f);
            }
            var clip = AudioClip.Create("ritual-knead-fur", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip BakeBgm(int rate)
        {
            const float loopSeconds = 8.0f;
            int cross = rate / 2;
            int loopSamples = Mathf.RoundToInt(rate * loopSeconds);
            int total = loopSamples + cross;
            var raw = new float[total];
            float[][] chords =
            {
                new[] { 174.61f, 220.00f, 261.63f, 329.63f },
                new[] { 164.81f, 196.00f, 246.94f, 293.66f },
                new[] { 146.83f, 174.61f, 220.00f, 329.63f },
                new[] { 130.81f, 196.00f, 246.94f, 293.66f },
            };
            var phases = new float[4][];
            for (int c = 0; c < 4; c++) phases[c] = new float[4];

            for (int i = 0; i < total; i++)
            {
                float t = (i / (float)rate) % loopSeconds;
                float bar = t / 2.0f;
                int cur = Mathf.FloorToInt(bar) & 3;
                float barFrac = bar - Mathf.Floor(bar);
                int prev = (cur + 3) & 3;
                float blend = Mathf.Clamp01(barFrac / 0.18f);
                blend = blend * blend * (3f - 2f * blend);

                float sCur = 0f, sPrev = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float detune = 1f + (k - 1.5f) * 0.0018f;
                    phases[cur][k] += 2f * Mathf.PI * chords[cur][k] * detune / rate;
                    phases[prev][k] += 2f * Mathf.PI * chords[prev][k] * detune / rate;
                    float wCur = k == 0 ? 0.36f : 0.20f;
                    sCur += Mathf.Sin(phases[cur][k]) * wCur;
                    sPrev += Mathf.Sin(phases[prev][k]) * wCur;
                }
                float trem = 0.88f + 0.12f * Mathf.Sin(2f * Mathf.PI * 0.25f * (i / (float)rate));
                raw[i] = Mathf.Lerp(sPrev, sCur, blend) * trem * 0.52f;
            }
            var data = new float[loopSamples];
            Array.Copy(raw, data, loopSamples);
            for (int i = 0; i < cross; i++)
            {
                float k = i / (float)cross;
                data[i] = data[i] * k + raw[loopSamples + i] * (1f - k);
            }
            var clip = AudioClip.Create("ritual-bgm", loopSamples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip BakeSand(int rate)
        {
            int samples = rate * 2;
            int cross = rate / 6;
            var data = new float[samples];
            uint state = 88172645u;
            float low = 0f, high = 0f;
            for (int i = 0; i < samples; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float white = (state & 0xFFFF) / 32768f - 1f;
                low += (white - low) * 0.22f;
                high += (low - high) * 0.06f;
                data[i] = (low - high) * 0.14f;
            }
            for (int g = 0; g < 420; g++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                int start = (int)(state % (uint)(samples - rate / 20));
                float freq = 950f + ((state >> 8) & 0xFF) / 255f * 1650f;
                float amp = 0.10f + ((state >> 16) & 0x7F) / 127f * 0.16f;
                int len = Mathf.Min(samples - start, Mathf.RoundToInt(rate * 0.022f));
                for (int j = 0; j < len; j++)
                {
                    float tj = j / (float)rate;
                    float ping = Mathf.Sin(2f * Mathf.PI * freq * tj) * Mathf.Exp(-tj * 165f);
                    data[start + j] += ping * amp;
                }
            }
            for (int i = 0; i < cross; i++)
            {
                float k = i / (float)cross;
                data[i] = data[i] * k + data[samples - cross + i] * (1f - k);
            }
            int trimmedLen = samples - cross;
            var trimmed = new float[trimmedLen];
            for (int i = 0; i < trimmedLen; i++) trimmed[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("ritual-sand", trimmedLen, 1, rate, false);
            clip.SetData(trimmed, 0);
            return clip;
        }

        void Play(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            if (!_ready || clip == null || volume * _master <= 0.001f) return;
            AudioSource source = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            source.transform.position = position;
            source.clip = clip;
            source.pitch = Mathf.Clamp(pitch, 0.3f, 3f);
            source.volume = Mathf.Clamp01(volume * _master);
            source.Play();
        }

        float PitchFor(int units)
        {
            float h = Mathf.Sin(units * 2.399963f) * 0.5f + Mathf.Cos(units * 1.618034f) * 0.5f;
            return 0.96f + 0.08f * h;
        }

        static readonly float[] ChimeTunings = { 0.89090f, 1.00000f, 1.12246f, 1.33484f, 1.49831f, 1.68179f };

        float ChimePitchFor(int units)
        {
            int idx = Mathf.Abs(units * 5 + units / 2) % ChimeTunings.Length;
            return ChimeTunings[idx];
        }

        public void Knead(Vector3 position, PieceFeel feel, bool windChime = false)
        {
            if (windChime)
            {
                int idx = Mathf.Abs(Mathf.RoundToInt((position.x + position.y + position.z) * 43f)) % ChimeTunings.Length;
                Play(_kneadChime, position, chimeVolume * 0.90f, ChimeTunings[idx]);
                return;
            }
            AudioClip clip = feel == PieceFeel.Water ? _kneadWater : feel == PieceFeel.Fur ? _kneadFur : _kneadJelly;
            float pitch = 0.94f + (Mathf.Abs(position.x + position.y + position.z) % 0.12f);
            Play(clip, position, chimeVolume * 0.9f, pitch);
        }

        public void Unit(Vector3 position, int units, bool up, bool windChime = false)
        {
            if (windChime)
            {
                float pitch = ChimePitchFor(units);
                if (!up)
                {
                    Play(_chimeSoft, position, downVolume * 1.25f, pitch);
                    return;
                }
                int s = TimeUnits.NodeStrength(units);
                if (s == 2) Play(_chimeMark60, position, markVolume, pitch);
                else if (s == 1) Play(_chimeMark30, position, markVolume * 0.92f, pitch);
                else Play(_chimeTick, position, chimeVolume, pitch);
                return;
            }
            if (!up)
            {
                Play(_soft, position, downVolume, PitchFor(units));
                return;
            }
            int strength = TimeUnits.NodeStrength(units);
            if (strength == 2) Play(_mark60, position, markVolume, PitchFor(units));
            else if (strength == 1) Play(_mark30, position, markVolume * 0.9f, PitchFor(units));
            else Play(_tick, position, chimeVolume, PitchFor(units));
        }

        public void Chimes(Vector3 position, int firstUnit, int count, float interval, bool windChime = false)
        {
            int maxCount = windChime ? Mathf.Min(count, 3) : count;
            float step = windChime ? Mathf.Max(interval, 0.14f) : interval;
            for (int i = 0; i < maxCount; i++)
                _queue.Add(new Queued { at = _clock + i * step, position = position, units = firstUnit + i + 1, volume = 0.85f, windChime = windChime });
        }

        public void Cancel(Vector3 position, bool windChime = false)
        {
            if (windChime) Play(_chimeSoft, position, downVolume * 1.2f, 0.94f);
            else Play(_soft, position, downVolume, 0.8f);
        }

        public void PourStart(Vector3 position)
        {
            if (!_ready) return;
            _sand.transform.position = position;
            _sandTarget = sandVolume;
            if (!_sand.isPlaying) _sand.Play();
        }

        public void PourStop()
        {
            _sandTarget = 0f;
        }

        public void Landed(Vector3 position, int grains)
        {
            if (_clock - _lastGlass < glassInterval) return;
            _lastGlass = _clock;
            float strength = Mathf.Clamp01(grains / 60f);
            Play(_glass, position, glassVolume * (0.4f + 0.6f * strength), 0.9f + 0.25f * ((grains * 7) % 10) / 10f);
        }

        public void Press(Vector3 position, float progress)
        {
            if (!_ready) return;
            _press.transform.position = position;
            _pressTarget = progress > 0.01f ? pressVolume * progress : 0f;
            _press.pitch = 0.8f + 0.35f * progress;
            if (_pressTarget > 0f && !_press.isPlaying) _press.Play();
        }

        public void Seal(Vector3 position)
        {
            _pressTarget = 0f;
            Play(_seal, position, sealVolume, 1f);
        }

        public void Tick(float dt, float master)
        {
            _master = master;
            _clock += dt;
            if (_hush > 0f) _hush = Mathf.Max(0f, _hush - dt);
            if (!_ready) return;
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i].at > _clock) continue;
                Queued q = _queue[i];
                _queue.RemoveAt(i);
                int strength = TimeUnits.NodeStrength(q.units);
                if (q.windChime)
                {
                    AudioClip clip = strength == 2 ? _chimeMark60 : strength == 1 ? _chimeMark30 : _chimeTick;
                    Play(clip, q.position, (strength > 0 ? markVolume : chimeVolume) * q.volume, ChimePitchFor(q.units));
                }
                else
                {
                    Play(strength == 2 ? _mark60 : strength == 1 ? _mark30 : _tick, q.position, (strength > 0 ? markVolume : chimeVolume) * q.volume, PitchFor(q.units));
                }
            }
            float sand = Mathf.MoveTowards(_sand.volume, _sandTarget * _master, dt * 1.6f);
            _sand.volume = sand;
            if (sand <= 0.0005f && _sandTarget <= 0f && _sand.isPlaying) _sand.Stop();
            float press = Mathf.MoveTowards(_press.volume, _pressTarget * _master, dt * 1.2f);
            _press.volume = press;
            if (press <= 0.0005f && _pressTarget <= 0f && _press.isPlaying) _press.Stop();
            if (_hush > 0f || _bgmHeld)
            {
                if (_bgm != null) _bgm.volume = 0f;
            }
            else if (_bgm != null)
            {
                float target = bgmVolume * _master;
                _bgm.volume = Mathf.MoveTowards(_bgm.volume, target, dt * (_bgm.volume < target ? 0.1f : 0.25f));
            }
        }
    }
}
