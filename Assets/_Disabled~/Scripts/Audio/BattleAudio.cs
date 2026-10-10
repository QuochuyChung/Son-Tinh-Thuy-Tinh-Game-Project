using System.Collections.Generic;
using SonTinhThuyTinh.Combat.Boss;
using SonTinhThuyTinh.Combat.Environment;
using UnityEngine;

namespace SonTinhThuyTinh.Audio
{
    [DisallowMultipleComponent]
    public sealed class BattleAudio : MonoBehaviour
    {
        public static BattleAudio Instance { get; private set; }

        [Header("Clip override (bỏ trống = sinh procedural lúc Awake)")]
        [Tooltip("Tiếng sấm đánh — cue bão P3 + khe sấm.")]
        [SerializeField] private AudioClip thunderClip;
        [Tooltip("Tiếng sóng đập — env lật có yếu tố Nước.")]
        [SerializeField] private AudioClip waveClip;
        [Tooltip("Tiếng đá vỡ vụn — env lật có yếu tố Đất.")]
        [SerializeField] private AudioClip crumblingClip;
        [Tooltip("Cue 'rung chuyển' mỗi lần env lật.")]
        [SerializeField] private AudioClip rumbleClip;
        [Tooltip("Tiếng underwater bị chặn — đứng lũ (flood ≥ threshold).")]
        [SerializeField] private AudioClip muffledClip;
        [Tooltip("Ult stinger phe Sơn Tinh.")]
        [SerializeField] private AudioClip stingerSonTinhClip;
        [Tooltip("Ult stinger phe Thủy Tinh.")]
        [SerializeField] private AudioClip stingerThuyTinhClip;

        [Header("Tuning [tune]")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float stingerVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float muffledVolume = 0.5f;
        [SerializeField, Min(0.01f)] private float muffledFadeSeconds = 0.5f;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("flood01 = max(0,-level)/3 ≥ 0.5 (level ≤ -2) = đứng lũ → muffled.")]
        private float muffledThreshold = 0.5f;

        private AudioSource sfxSource;
        private AudioSource stingerSource;
        private AudioSource ambientSource;
        private EnvironmentDirector env;
        private bool muffledOn;
        private readonly List<AudioClip> generated = new List<AudioClip>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            sfxSource = gameObject.AddComponent<AudioSource>();
            stingerSource = gameObject.AddComponent<AudioSource>();
            ambientSource = gameObject.AddComponent<AudioSource>();
            AudioSource[] sources = { sfxSource, stingerSource, ambientSource };
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].loop = false;
            }
            ambientSource.loop = true;
            ambientSource.volume = 0f;

            BuildClips();
            Debug.Log("[Audio] BattleAudio khởi tạo — procedural: thunder/wave/crumbling/rumble/muffled/stinger×2");
        }

        private void Start()
        {
            env = EnvironmentDirector.Instance;
            if (env != null) env.LevelChanged += OnLevelChanged;
            if (BossHealth.Instance != null) BossHealth.Instance.PhaseStarted += OnPhaseStarted;
        }

        private void OnDestroy()
        {
            if (env != null) env.LevelChanged -= OnLevelChanged;
            if (BossHealth.Instance != null) BossHealth.Instance.PhaseStarted -= OnPhaseStarted;
            if (Instance == this) Instance = null;
            for (int i = 0; i < generated.Count; i++)
                if (generated[i] != null) Destroy(generated[i]);
        }

        public void PlayThunderStrike() => PlayCue(thunderClip, sfxSource, sfxVolume, "thunder-strike");

        public void PlayUltStinger(EnvFaction faction)
        {
            AudioClip clip = faction == EnvFaction.SonTinh ? stingerSonTinhClip : stingerThuyTinhClip;
            PlayCue(clip, stingerSource, stingerVolume, $"ult-stinger phe={faction}");
        }

        private void OnLevelChanged(int level, int delta)
        {
            int oldLevel = level - delta;
            PlayCue(rumbleClip, sfxSource, sfxVolume, $"rung-chuyen lvl={level} d={delta}");
            if (oldLevel > 0 || level > 0)
                PlayCue(crumblingClip, sfxSource, sfxVolume, $"crumbling lvl={level} d={delta}");
            if (oldLevel < 0 || level < 0)
                PlayCue(waveClip, sfxSource, sfxVolume, $"wave lvl={level} d={delta}");
        }

        private void OnPhaseStarted(int phase)
        {
            if (phase >= 3) PlayCue(thunderClip, sfxSource, sfxVolume, "thunder-p3");
        }

        private void Update()
        {
            if (env == null) env = EnvironmentDirector.Instance;
            float maxDepth = Mathf.Abs(EnvironmentDirector.MinLevel);
            float flood01 = env != null && maxDepth > 0f ? Mathf.Max(0f, -env.Level) / maxDepth : 0f;
            bool want = flood01 >= muffledThreshold;
            if (want != muffledOn)
            {
                muffledOn = want;
                Debug.Log($"[Audio] cue=muffled-{(want ? "on" : "off")} flood={flood01:F2}");
            }

            float target = muffledOn ? muffledVolume : 0f;
            ambientSource.volume = Mathf.MoveTowards(
                ambientSource.volume, target,
                muffledVolume * Time.deltaTime / muffledFadeSeconds);
            if (muffledOn && !ambientSource.isPlaying) ambientSource.Play();
            else if (!muffledOn && ambientSource.volume <= 0.0005f && ambientSource.isPlaying)
                ambientSource.Stop();
        }

        private void PlayCue(AudioClip clip, AudioSource source, float volume, string cue)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip, volume);
            Debug.Log($"[Audio] cue={cue}");
        }

        private void BuildClips()
        {
            if (thunderClip == null) thunderClip = Track(SynthThunder());
            if (waveClip == null) waveClip = Track(SynthWave());
            if (crumblingClip == null) crumblingClip = Track(SynthCrumbling());
            if (rumbleClip == null) rumbleClip = Track(SynthRumble());
            if (muffledClip == null) muffledClip = Track(SynthMuffled());
            if (stingerSonTinhClip == null) stingerSonTinhClip = Track(SynthStingerSonTinh());
            if (stingerThuyTinhClip == null) stingerThuyTinhClip = Track(SynthStingerThuyTinh());
        }

        private AudioClip Track(AudioClip clip)
        {
            generated.Add(clip);
            return clip;
        }

        private static AudioClip SynthClip(string name, float seconds, System.Func<float, float> sampler)
        {
            const int rate = 44100;
            int count = Mathf.Max(1, Mathf.CeilToInt(seconds * rate));
            var data = new float[count];
            float step = 1f / rate;
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(sampler(i * step), -1f, 1f);
            AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthThunder()
        {
            var rnd = new System.Random(101);
            float lp = 0f;
            return SynthClip("SFX_Thunder", 2f, t =>
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float alpha = Mathf.Lerp(0.7f, 0.05f, Mathf.Clamp01(t / 1.2f));
                lp += alpha * (white - lp);
                float crack = t < 0.3f ? Mathf.Exp(-t * 20f) * 1.7f : 0f;
                float tail = Mathf.Exp(-t * 1.5f) * 0.75f;
                float sub = Mathf.Sin(2f * Mathf.PI * 40f * t) * Mathf.Exp(-t * 1.3f) * 0.4f;
                return lp * (crack + tail) + sub;
            });
        }

        private static AudioClip SynthWave()
        {
            var rnd = new System.Random(202);
            float lp = 0f;
            return SynthClip("SFX_WaveCrash", 1.4f, t =>
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += 0.3f * (white - lp);
                float u = Mathf.Clamp01(t / 1.4f);
                float swell = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.5f);
                float hiss = lp * Mathf.Lerp(1.2f, 0.5f, u);
                float low = Mathf.Sin(2f * Mathf.PI * 65f * t) * swell * 0.3f;
                return hiss * swell * 1.4f + low;
            });
        }

        private static AudioClip SynthCrumbling()
        {
            var rnd = new System.Random(303);
            float lp = 0f;
            int lastBlock = -1;
            float gate = 0f;
            return SynthClip("SFX_Crumbling", 1.1f, t =>
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += 0.5f * (white - lp);
                int block = (int)(t * 24f);
                if (block != lastBlock)
                {
                    lastBlock = block;
                    gate = rnd.NextDouble() < 0.6 ? (float)(0.55 + rnd.NextDouble() * 0.45) : 0.1f;
                }
                float decay = Mathf.Exp(-t * 1.6f);
                float grind = Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t * 2f) * 0.3f;
                return lp * gate * decay * 1.7f + grind;
            });
        }

        private static AudioClip SynthRumble()
        {
            var rnd = new System.Random(404);
            float lp = 0f;
            return SynthClip("SFX_Rumble", 1.6f, t =>
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += 0.08f * (white - lp);
                float u = Mathf.Clamp01(t / 1.6f);
                float env = Mathf.Sin(Mathf.PI * u);
                float tone = Mathf.Sin(2f * Mathf.PI * 36f * t) * 0.7f
                           + Mathf.Sin(2f * Mathf.PI * 54f * t) * 0.3f;
                return (lp * 1.1f + tone * 0.6f) * env;
            });
        }

        private static AudioClip SynthMuffled()
        {
            var rnd = new System.Random(505);
            float lp = 0f;
            return SynthClip("SFX_MuffledLoop", 2.5f, t =>
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += 0.03f * (white - lp);
                float lfo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.37f * t);
                float sub = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.2f;
                return (lp * 1.3f + sub) * lfo * 0.85f;
            });
        }

        private static AudioClip SynthStingerSonTinh()
        {
            var rnd = new System.Random(606);
            return SynthClip("SFX_Stinger_SonTinh", 1.7f, t =>
            {
                float attack = t < 0.04f ? t / 0.04f : 1f;
                float decay = Mathf.Exp(-Mathf.Max(0f, t - 0.04f) * 2.4f);
                float env = attack * decay;
                float v = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.5f
                        + Mathf.Sin(2f * Mathf.PI * 659.26f * t) * 0.34f
                        + Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.3f
                        + Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * 0.16f;
                float shimmer = 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                float hit = t < 0.07f
                    ? ((float)(rnd.NextDouble() * 2.0 - 1.0)) * (1f - t / 0.07f) * 0.5f
                    : 0f;
                return v * env * shimmer * 0.55f + hit;
            });
        }

        private static AudioClip SynthStingerThuyTinh()
        {
            var rnd = new System.Random(707);
            float lp = 0f;
            return SynthClip("SFX_Stinger_ThuyTinh", 2.1f, t =>
            {
                float attack = t < 0.12f ? t / 0.12f : 1f;
                float decay = Mathf.Exp(-Mathf.Max(0f, t - 0.12f) * 1.5f);
                float env = attack * decay;
                float v = Mathf.Sin(2f * Mathf.PI * 98f * t) * 0.5f
                        + Mathf.Sin(2f * Mathf.PI * 116.54f * t) * 0.32f
                        + Mathf.Sin(2f * Mathf.PI * 146.83f * t) * 0.3f
                        + Mathf.Sin(2f * Mathf.PI * 49f * t) * 0.4f;
                float tremolo = 1f + 0.14f * Mathf.Sin(2f * Mathf.PI * 6.5f * t);
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += 0.05f * (white - lp);
                float swell = Mathf.Clamp01(t / 0.5f);
                return (v * tremolo * 0.55f + lp * 0.5f * swell) * env;
            });
        }
    }
}
