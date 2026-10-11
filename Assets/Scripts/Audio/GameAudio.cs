using UnityEngine;

namespace SonTinhThuyTinh.Audio
{
    // The way the rest of the game asks for a sound: GameAudio.Play(Sfx.BellRing) (flat) or GameAudio.Play(Sfx.HitHeavy, position) (3D).
    // Does nothing when there is no AudioLibrary in Resources, so a missing clip can never break a scene. Volumes are saved in PlayerPrefs.
    public static class GameAudio
    {
        const string MasterKey = "audio.master", MusicKey = "audio.music", SfxKey = "audio.sfx";

        static float master = -1f, music = -1f, sfx = -1f;

        public static float MasterVolume { get => Get(ref master, MasterKey, 1f); set { Set(ref master, MasterKey, value); AudioListener.volume = master; } }
        public static float MusicVolume { get => Get(ref music, MusicKey, 0.8f); set => Set(ref music, MusicKey, value); }
        public static float SfxVolume { get => Get(ref sfx, SfxKey, 1f); set => Set(ref sfx, SfxKey, value); }

        public static void Play(Sfx id, float volume = 1f) => AudioManager.Instance?.PlaySfx(id, null, volume);

        public static void Play(Sfx id, Vector3 position, float volume = 1f) => AudioManager.Instance?.PlaySfx(id, position, volume);

        // Treat the next few seconds as a fight: the music turns to the battle track on the maps.
        public static void Combat(float seconds = 8f) => AudioManager.Instance?.NoteCombat(seconds);

        static float Get(ref float field, string key, float fallback)
        {
            if (field < 0f)
            {
                try { field = Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback)); }
                catch { field = fallback; }
            }
            return field;
        }

        static void Set(ref float field, string key, float value)
        {
            field = Mathf.Clamp01(value);
            try { PlayerPrefs.SetFloat(key, field); }
            catch { /* the volume still applies for this session */ }
        }

        // Keeps state clean when Enter Play Mode Options skip the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => master = music = sfx = -1f;
    }
}
