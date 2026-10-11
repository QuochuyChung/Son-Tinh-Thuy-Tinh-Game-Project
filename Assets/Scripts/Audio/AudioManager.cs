using System.Collections;
using System.Collections.Generic;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Audio
{
    // The game's sound: made once before the first scene and kept across scene loads (no scene needs setting up).
    //  - One music track and one ambience bed per scene, crossfaded; on the maps the music turns to the battle track while a fight is
    //    going on (anyone took damage in the last few seconds).
    //  - Listens to the game instead of being called from everywhere: it finds the players (adds PlayerAudio: footsteps, jumps, swings,
    //    spells), hooks every Health (hits, hurt, deaths), the dialogue box (a blip per line), the gift banner, the quests (bell, wood,
    //    horse, rooster), the elephant (roar, stomps) and menu clicks.
    //  - Plays one-shots through a pool of sources; the same sound is not repeated within 40 ms.
    // Clips come from Resources/AudioLibrary (AudioLibrary.cs). Volumes: GameAudio.MasterVolume / MusicVolume / SfxVolume.
    public class AudioManager : MonoBehaviour
    {
        const int PoolSize = 24;
        const float ScanEvery = 1f;

        public static AudioManager Instance { get; private set; }

        AudioLibrary library;
        AudioSource[] pool;
        int nextSource;
        readonly Dictionary<Sfx, float> lastPlayed = new();
        LoopChannel music, ambience;

        readonly HashSet<int> hooked = new();
        DialogueRunner hookedRunner;
        readonly Dictionary<VoiChinNgaBoss, bool> bossAwake = new();
        readonly Dictionary<VoiChinNgaBoss, Vector3> bossStep = new();
        GameObject lastSelected;
        float sceneLoadedAt, nextScan, combatUntil;
        LoopId sceneMusic, sceneAmbience;
        bool bossScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if (Instance != null) return;
            var lib = Resources.Load<AudioLibrary>(AudioLibrary.ResourceName);
            if (lib == null)
            {
                Debug.LogWarning("AudioManager: Resources/AudioLibrary.asset is missing (Tools > Son Tinh Thuy Tinh > Audio > Generate Placeholder Audio): the game stays silent.");
                return;
            }
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioManager>().library = lib;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Sfx_" + i);
                child.transform.SetParent(transform, false);
                AudioSource s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.ignoreListenerPause = true;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 6f;
                s.maxDistance = 80f;
                pool[i] = s;
            }
            music = new LoopChannel(gameObject);
            ambience = new LoopChannel(gameObject);

            AudioListener.volume = GameAudio.MasterVolume;
            SceneManager.sceneLoaded += OnSceneLoaded;
            GiftTracker.Collected += OnGift;
            GiftTracker.Notice += OnNotice;
            HorseQuest.Changed += OnHorseQuest;
            RoosterQuest.Changed += OnRoosterQuest;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GiftTracker.Collected -= OnGift;
            GiftTracker.Notice -= OnNotice;
            HorseQuest.Changed -= OnHorseQuest;
            RoosterQuest.Changed -= OnRoosterQuest;
            if (hookedRunner != null) hookedRunner.LineStarted -= OnDialogueLine;
        }

        // ---------------------------------------------------------------- one-shots

        public void PlaySfx(Sfx id, Vector3? position, float volume)
        {
            if (library == null || id == Sfx.None) return;
            AudioLibrary.SfxEntry entry = library.Get(id);
            if (entry == null || entry.clips == null || entry.clips.Length == 0) return;
            if (lastPlayed.TryGetValue(id, out float at) && Time.unscaledTime - at < 0.04f) return;
            lastPlayed[id] = Time.unscaledTime;

            AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
            if (clip == null) return;
            AudioSource s = NextSource();
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(entry.volume * volume * GameAudio.SfxVolume);
            s.pitch = Random.Range(entry.pitch.x, entry.pitch.y);
            if (position.HasValue)
            {
                s.transform.position = position.Value;
                s.spatialBlend = entry.spatial;
            }
            else
            {
                s.transform.localPosition = Vector3.zero;
                s.spatialBlend = 0f;
            }
            s.Play();
        }

        AudioSource NextSource()
        {
            for (int i = 0; i < pool.Length; i++)
            {
                AudioSource s = pool[(nextSource + i) % pool.Length];
                if (!s.isPlaying) { nextSource = (nextSource + i + 1) % pool.Length; return s; }
            }
            AudioSource oldest = pool[nextSource];   // all busy: take the next in turn
            nextSource = (nextSource + 1) % pool.Length;
            return oldest;
        }

        public void NoteCombat(float seconds) => combatUntil = Mathf.Max(combatUntil, Time.unscaledTime + seconds);

        // ---------------------------------------------------------------- scenes, music, ambience

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            sceneLoadedAt = Time.unscaledTime;
            hooked.Clear();
            hookedRunner = null;
            bossAwake.Clear();
            bossStep.Clear();
            combatUntil = 0f;
            lastSelected = null;

            string n = scene.name;
            bossScene = n == "Map_FinalBattle" || n.StartsWith("Battle_") || n == "Sandbox_Combat";
            switch (n)
            {
                case "MainMenu": case "CharacterSelect": sceneMusic = LoopId.MusicMenu; sceneAmbience = LoopId.None; break;
                case "Prologue": case "Cutscene_PhanXu": sceneMusic = LoopId.MusicCutscene; sceneAmbience = LoopId.None; break;
                case "Map_SonTinh": sceneMusic = LoopId.MusicSonTinh; sceneAmbience = LoopId.AmbForest; break;
                case "Map_ThuyTinh": sceneMusic = LoopId.MusicThuyTinh; sceneAmbience = LoopId.AmbWater; break;
                case "Map_HungVuong": sceneMusic = LoopId.MusicPalace; sceneAmbience = LoopId.AmbPalace; break;
                default: sceneMusic = LoopId.MusicBattle; sceneAmbience = LoopId.AmbWind; break;   // the battle scenes
            }
            nextScan = 0f;
        }

        void Update()
        {
            if (library == null) return;

            // music: the battle track while a fight goes on (on the maps), else the scene's own
            LoopId wantMusic = bossScene || Time.unscaledTime < combatUntil ? LoopId.MusicBattle : sceneMusic;
            if (wantMusic == LoopId.MusicBattle && sceneMusic == LoopId.MusicCutscene) wantMusic = sceneMusic;   // cutscenes keep their own
            music.Set(wantMusic, library, 1.6f);
            ambience.Set(sceneAmbience, library, 2.5f);
            music.Tick(Time.unscaledDeltaTime, GameAudio.MusicVolume);
            ambience.Tick(Time.unscaledDeltaTime, 1f);

            UpdateMenuClicks();
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + ScanEvery; Scan(); }
            UpdateBosses();
        }

        // the interface: a click when a button gets selected by the pointer (not for the first selection when a menu opens)
        void UpdateMenuClicks()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;
            GameObject selected = es.currentSelectedGameObject;
            if (selected == lastSelected) return;
            lastSelected = selected;
            if (selected != null && Time.unscaledTime - sceneLoadedAt > 0.6f && selected.GetComponent<Selectable>() != null) PlaySfx(Sfx.UiClick, null, 1f);
        }

        // ---------------------------------------------------------------- finding things to listen to

        void Scan()
        {
            foreach (PlayerController pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (pc.isActiveAndEnabled && pc.GetComponent<PlayerAudio>() == null) pc.gameObject.AddComponent<PlayerAudio>();

            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (!hooked.Add(h.GetInstanceID())) continue;
                Health captured = h;
                h.Damaged += info => OnDamaged(captured, info);
                h.Died += info => OnDied(captured, info);
            }

            DialogueRunner runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            if (runner != null && runner != hookedRunner)
            {
                if (hookedRunner != null) hookedRunner.LineStarted -= OnDialogueLine;
                runner.LineStarted += OnDialogueLine;
                hookedRunner = runner;
            }

            foreach (VoiChinNgaBoss b in FindObjectsByType<VoiChinNgaBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!bossAwake.ContainsKey(b)) { bossAwake[b] = b.enabled; bossStep[b] = b.transform.position; }
        }

        void OnDamaged(Health h, DamageInfo info)
        {
            if (h == null) return;
            bool player = IsPlayer(h);
            Vector3 at = info.HitPoint != default ? info.HitPoint : h.transform.position + Vector3.up;
            NoteCombat(8f);
            if (player) PlaySfx(Sfx.Hurt, null, info.IsHeavy ? 1f : 0.8f);
            else PlaySfx(info.IsHeavy ? Sfx.HitHeavy : Sfx.HitFlesh, at, 1f);
        }

        void OnDied(Health h, DamageInfo info)
        {
            if (h == null) return;
            if (IsPlayer(h)) PlaySfx(Sfx.Death, null, 1f);
            else PlaySfx(h.GetComponent<VoiChinNgaBoss>() != null ? Sfx.ElephantRoar : Sfx.EnemyDeath, h.transform.position + Vector3.up, 1f);
        }

        static bool IsPlayer(Health h) => h.TryGetComponent(out PlayerController pc) && pc.GetComponent<BossAI>() == null;

        // ---------------------------------------------------------------- dialogue voices

        Coroutine voiceRoutine;

        // A babble of syllables in the speaker's voice for as long as the line takes to type (a voiced line without recorded speech).
        void OnDialogueLine(DialogueLine line)
        {
            if (voiceRoutine != null) { StopCoroutine(voiceRoutine); voiceRoutine = null; }
            VoiceId voice = VoiceFor(line.speaker);
            AudioLibrary.VoiceEntry entry = voice == VoiceId.None ? null : library.Get(voice);
            if (entry == null || entry.syllables == null || entry.syllables.Length == 0 || string.IsNullOrEmpty(line.text))
            {
                PlaySfx(Sfx.DialogueBlip, null, 1f);
                return;
            }
            voiceRoutine = StartCoroutine(Babble(line.text, entry));
        }

        IEnumerator Babble(string text, AudioLibrary.VoiceEntry entry)
        {
            float interval = 1f / Mathf.Max(1f, entry.rate);
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(8f, text.Length / 40f) / interval), 2, 80);   // the dialogue box types 40 characters a second
            bool question = text.TrimEnd().EndsWith("?");
            for (int i = 0; i < count; i++)
            {
                if (hookedRunner == null || !hookedRunner.IsPlaying) break;
                char ch = text[Mathf.Min(text.Length - 1, Mathf.RoundToInt(i * text.Length / (float)count))];
                AudioClip clip = entry.syllables[Mathf.Abs(ch) % entry.syllables.Length];
                float tail = question && i >= count - 3 ? 1f + 0.06f * (i - (count - 3) + 1) : 1f;   // a question rises at the end
                PlayClip(clip, entry.volume, Random.Range(entry.pitch.x, entry.pitch.y) * tail);
                yield return new WaitForSecondsRealtime(interval * Random.Range(0.8f, 1.25f) * (",.;".IndexOf(ch) >= 0 ? 1.8f : 1f));
            }
            voiceRoutine = null;
        }

        void PlayClip(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            AudioSource s = NextSource();
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume * GameAudio.SfxVolume);
            s.pitch = pitch;
            s.transform.localPosition = Vector3.zero;
            s.spatialBlend = 0f;
            s.Play();
        }

        // which voice speaks a line, from the name above it (empty = narration)
        static VoiceId VoiceFor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return VoiceId.Narrator;
            string s = speaker.Normalize().ToLowerInvariant();
            if (s.Contains("sơn tinh")) return VoiceId.SonTinh;
            if (s.Contains("thủy tinh") || s.Contains("thuỷ tinh")) return VoiceId.ThuyTinh;
            if (s.Contains("vua") || s.Contains("hùng vương")) return VoiceId.KingHung;
            if (s.Contains("mị nương")) return VoiceId.MiNuong;
            if (s.Contains("trộm") || s.Contains("ninja") || s.Contains("thủ lĩnh") || s.Contains("cướp")) return VoiceId.Thief;
            if (s.Contains("voi") || s.Contains("sấu") || s.Contains("rùa") || s.Contains("cá ")) return VoiceId.Elephant;
            if (s.Contains("chăn ngựa") || s.Contains("ông") || s.Contains("già") || s.Contains("dân")) return VoiceId.Elder;
            return VoiceId.Narrator;
        }

        // the elephant: a roar when it wakes up, a heavy step every few metres it walks
        void UpdateBosses()
        {
            if (bossAwake.Count == 0) return;
            foreach (VoiChinNgaBoss b in new List<VoiChinNgaBoss>(bossAwake.Keys))
            {
                if (b == null) { bossAwake.Remove(b); bossStep.Remove(b); continue; }
                bool awake = b.enabled && b.gameObject.activeInHierarchy;
                if (awake && !bossAwake[b]) { PlaySfx(Sfx.ElephantRoar, b.transform.position + Vector3.up * 4f, 1f); NoteCombat(10f); bossStep[b] = b.transform.position; }
                bossAwake[b] = awake;
                if (!awake || b.Health.IsDead) continue;
                if (Vector3.Distance(b.transform.position, bossStep[b]) > 4.5f)
                {
                    bossStep[b] = b.transform.position;
                    PlaySfx(Sfx.ElephantStomp, b.transform.position, 1f);
                }
            }
        }

        // ---------------------------------------------------------------- story events

        void OnGift(GiftItem _) => PlaySfx(Sfx.GiftCollected, null, 1f);

        void OnNotice(string _) => PlaySfx(Sfx.Notice, null, 0.8f);

        void OnHorseQuest(HorseQuestState state)
        {
            switch (state)
            {
                case HorseQuestState.Accepted: PlaySfx(Sfx.QuestAccept, null, 1f); PlaySfx(Sfx.BellRing, null, 0.6f); break;
                case HorseQuestState.Cleared: PlaySfx(Sfx.WoodCrash, null, 1f); break;
                case HorseQuestState.Calmed: StartCoroutine(RingThenNeigh()); break;
            }
        }

        IEnumerator RingThenNeigh()
        {
            PlaySfx(Sfx.BellRing, null, 1f);
            yield return new WaitForSecondsRealtime(1.2f);
            PlaySfx(Sfx.HorseNeigh, null, 0.9f);
        }

        void OnRoosterQuest(RoosterQuestState state)
        {
            if (state == RoosterQuestState.RoosterFree || state == RoosterQuestState.Completed) PlaySfx(Sfx.RoosterCluck, null, 0.9f);
        }

        // ---------------------------------------------------------------- a looping channel with a crossfade

        sealed class LoopChannel
        {
            readonly AudioSource[] sources = new AudioSource[2];
            int active;
            float target, fade = 1.5f;
            public LoopId Current { get; private set; }

            public LoopChannel(GameObject host)
            {
                for (int i = 0; i < 2; i++)
                {
                    AudioSource s = host.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.loop = true;
                    s.spatialBlend = 0f;
                    s.ignoreListenerPause = true;
                    s.volume = 0f;
                    sources[i] = s;
                }
            }

            public void Set(LoopId id, AudioLibrary library, float fadeSeconds)
            {
                if (id == Current) return;
                Current = id;
                fade = fadeSeconds;
                AudioLibrary.LoopEntry entry = id == LoopId.None ? null : library.Get(id);
                active = 1 - active;
                AudioSource s = sources[active];
                if (entry == null || entry.clip == null) { s.Stop(); target = 0f; return; }
                s.clip = entry.clip;
                s.volume = 0f;
                s.Play();
                target = entry.volume;
            }

            public void Tick(float dt, float scale)
            {
                for (int i = 0; i < 2; i++)
                {
                    AudioSource s = sources[i];
                    float wanted = i == active ? target * scale : 0f;
                    s.volume = Mathf.MoveTowards(s.volume, wanted, dt / Mathf.Max(0.05f, fade));
                    if (i != active && s.isPlaying && s.volume <= 0.001f) s.Stop();
                }
            }
        }
    }
}
