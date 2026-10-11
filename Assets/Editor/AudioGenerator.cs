using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SonTinhThuyTinh.Audio;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Makes the game's PLACEHOLDER sound: every effect, three ambience beds and six music loops are synthesised here (noise, sweeps, bells,
    // plucks, drums), written as WAV files in Assets/Audio and collected in Assets/Resources/AudioLibrary.asset (docs/progress.md 9.25).
    // They are good enough to hear the game react, not a final mix: to use a real recording drop a file called like the Sfx / LoopId
    // (e.g. HitHeavy.wav, MusicBattle.ogg) into Assets/Audio/Override and run the menu again; those clips replace the generated ones.
    // Deterministic: the same files come out every run.
    public static class AudioGenerator
    {
        const string Root = "Assets/Audio";
        const string LibraryPath = "Assets/Resources/AudioLibrary.asset";
        const int SfxRate = 32000, LoopRate = 24000;

        static int sr = SfxRate;
        static bool wrap;                  // loops write round the end of the buffer, so notes and echoes are seamless
        static System.Random rng = new(1);

        [MenuItem("Tools/Son Tinh Thuy Tinh/Audio/Generate Placeholder Audio")]
        public static void Generate()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            Directory.CreateDirectory(Root + "/Sfx");
            Directory.CreateDirectory(Root + "/Music");
            Directory.CreateDirectory(Root + "/Ambience");
            Directory.CreateDirectory(Root + "/Override");
            Directory.CreateDirectory("Assets/Resources");
            string readme = Root + "/Override/README.txt";
            if (!File.Exists(readme))
                File.WriteAllText(readme, "Put real recordings here to replace the generated placeholder sounds.\r\n" +
                    "Name the file like the Sfx / LoopId it replaces (HitHeavy.wav, FootstepGrass_1.ogg, MusicBattle.mp3, AmbForest.wav ...), then run\r\n" +
                    "Tools > Son Tinh Thuy Tinh > Audio > Generate Placeholder Audio again: clips found here are used instead of the generated ones.\r\n");
            AssetDatabase.Refresh();   // the new folders must be known to the AssetDatabase (FindAssets below)

            Directory.CreateDirectory(Root + "/Voice");
            var sfx = new List<AudioLibrary.SfxEntry>();
            var loops = new List<AudioLibrary.LoopEntry>();
            var voices = new List<AudioLibrary.VoiceEntry>();
            BuildSfx(sfx);
            BuildShouts(sfx);
            BuildLoops(loops);
            BuildVoices(voices);

            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.sfx = sfx.ToArray();
            library.loops = loops.ToArray();
            library.voices = voices.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"AudioGenerator: {sfx.Count} effects, {loops.Count} loops and {voices.Count} voices written to {Root}, library at {LibraryPath}.");
        }

        // ---------------------------------------------------------------- the library entries

        static void AddSfx(List<AudioLibrary.SfxEntry> list, Sfx id, float volume, float spatial, float pitchLo, float pitchHi, int variants, Func<int, float[]> make)
        {
            sr = SfxRate; wrap = false;
            var clips = new List<AudioClip>();
            AudioClip[] over = Overrides(id.ToString());
            if (over.Length > 0) clips.AddRange(over);
            else
                for (int v = 0; v < variants; v++)
                {
                    rng = new System.Random(id.GetHashCode() * 31 + v);
                    float[] data = make(v);
                    Finish(data, 0.9f);
                    string path = $"{Root}/Sfx/{id}_{v}.wav";
                    clips.Add(Write(path, data, SfxRate, false));
                }
            list.Add(new AudioLibrary.SfxEntry { id = id, clips = clips.ToArray(), volume = volume, pitch = new Vector2(pitchLo, pitchHi), spatial = spatial });
        }

        static void AddLoop(List<AudioLibrary.LoopEntry> list, LoopId id, float volume, bool music, Func<float[]> make)
        {
            sr = LoopRate; wrap = true;
            AudioClip clip = null;
            AudioClip[] over = Overrides(id.ToString());
            if (over.Length > 0) clip = over[0];
            else
            {
                rng = new System.Random(id.GetHashCode() * 17 + 5);
                float[] data = make();
                if (!music) data = Seamless(data, 0.6f);
                Finish(data, 0.8f);
                clip = Write($"{Root}/{(music ? "Music" : "Ambience")}/{id}.wav", data, LoopRate, music);
            }
            list.Add(new AudioLibrary.LoopEntry { id = id, clip = clip, volume = volume });
            wrap = false;
        }

        static AudioClip[] Overrides(string name)
        {
            var found = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets(name + " t:AudioClip", new[] { Root + "/Override" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(p);
                if (file == name || file.StartsWith(name + "_")) found.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(p));
            }
            return found.ToArray();
        }

        // ---------------------------------------------------------------- effects

        static void BuildSfx(List<AudioLibrary.SfxEntry> l)
        {
            // interface and story
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.UiClick, 0.6f, 0f, 0.97f, 1.03f, 1, v =>
            {
                float[] b = Buf(0.12f);
                Tone(b, 1700f, 0f, 0.06f, 0.5f, 0.001f, 0.012f);
                Noise(b, 0f, 0.02f, 0.3f, 0.0005f, 0.006f, 2000f, 8000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.DialogueBlip, 0.35f, 0f, 0.95f, 1.05f, 3, v =>
            {
                float[] b = Buf(0.08f);
                Tone(b, 560f + v * 80f, 0f, 0.06f, 0.4f, 0.004f, 0.02f, 1);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.GiftCollected, 0.8f, 0f, 1f, 1f, 1, v =>
            {
                float[] b = Buf(2.6f);
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                for (int i = 0; i < notes.Length; i++) Bell(b, notes[i], 0.12f * i, 0.45f, 0.8f);
                Echo(b, 0.18f, 0.4f, 3);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Notice, 0.5f, 0f, 1f, 1f, 1, v =>
            {
                float[] b = Buf(1.4f);
                Bell(b, 880f, 0f, 0.6f, 0.6f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.QuestAccept, 0.7f, 0f, 1f, 1f, 1, v =>
            {
                float[] b = Buf(1.6f);
                Tone(b, 392f, 0f, 0.5f, 0.5f, 0.004f, 0.3f, 1);
                Tone(b, 587f, 0.18f, 0.7f, 0.5f, 0.004f, 0.35f, 1);
                Bell(b, 784f, 0.18f, 0.3f, 0.7f);
                Echo(b, 0.2f, 0.35f, 2);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.QuestDone, 0.8f, 0f, 1f, 1f, 1, v =>
            {
                float[] b = Buf(2.8f);
                float[] notes = { 392f, 494f, 587f, 784f };
                for (int i = 0; i < notes.Length; i++) Bell(b, notes[i], 0.08f * i, 0.4f, 1f);
                Tone(b, 392f, 0f, 1.6f, 0.2f, 0.1f, 0.8f);
                Tone(b, 587f, 0f, 1.6f, 0.15f, 0.1f, 0.8f);
                Echo(b, 0.25f, 0.4f, 3);
                return b;
            });

            // the player
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.FootstepGrass, 0.5f, 0f, 0.9f, 1.1f, 4, v =>
            {
                float[] b = Buf(0.18f);
                Noise(b, 0f, 0.1f, 0.7f, 0.004f, 0.03f, 220f + v * 40f, 1500f + v * 250f);
                Sweep(b, 110f, 60f, 0f, 0.08f, 0.5f, 0.002f, 0.03f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.FootstepStone, 0.5f, 0f, 0.9f, 1.1f, 4, v =>
            {
                float[] b = Buf(0.16f);
                Noise(b, 0f, 0.06f, 0.6f, 0.001f, 0.015f, 1200f, 6000f);
                Tone(b, 180f + v * 25f, 0f, 0.04f, 0.3f, 0.001f, 0.012f);
                Tone(b, 900f + v * 120f, 0f, 0.08f, 0.1f, 0.001f, 0.03f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.FootstepWet, 0.5f, 0f, 0.9f, 1.1f, 4, v =>
            {
                float[] b = Buf(0.22f);
                Noise(b, 0f, 0.14f, 0.6f, 0.006f, 0.045f, 300f, 2500f);
                Noise(b, 0.03f, 0.12f, 0.35f, 0.01f, 0.04f, 1500f, 6000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Jump, 0.45f, 0f, 0.95f, 1.05f, 2, v =>
            {
                float[] b = Buf(0.3f);
                Whoosh(b, 0f, 0.25f, 0.35f, 400f, 900f, 1500f, 3000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Land, 0.6f, 0f, 0.95f, 1.05f, 2, v =>
            {
                float[] b = Buf(0.3f);
                Sweep(b, 95f, 45f, 0f, 0.18f, 0.7f, 0.002f, 0.06f);
                Noise(b, 0f, 0.12f, 0.5f, 0.002f, 0.04f, 150f, 1200f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Dodge, 0.55f, 0f, 0.95f, 1.05f, 2, v =>
            {
                float[] b = Buf(0.4f);
                Whoosh(b, 0f, 0.32f, 0.55f, 500f, 1500f, 1500f, 4500f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Slide, 0.5f, 0f, 1f, 1f, 1, v =>
            {
                float[] b = Buf(0.7f);
                Whoosh(b, 0f, 0.55f, 0.35f, 300f, 1200f, 300f, 2500f);
                Noise(b, 0f, 0.5f, 0.25f, 0.05f, 0.4f, 1500f, 6000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Climb, 0.5f, 0f, 0.95f, 1.05f, 2, v =>
            {
                float[] b = Buf(0.35f);
                Noise(b, 0f, 0.12f, 0.5f, 0.004f, 0.04f, 200f, 1500f);
                Tone(b, 220f, 0f, 0.1f, 0.2f, 0.002f, 0.05f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.SwingLight, 0.55f, 0f, 0.92f, 1.1f, 3, v =>
            {
                float[] b = Buf(0.3f);
                Whoosh(b, 0f, 0.2f, 0.5f, 900f, 2200f, 2500f, 6000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.SwingHeavy, 0.7f, 0f, 0.92f, 1.05f, 2, v =>
            {
                float[] b = Buf(0.5f);
                Whoosh(b, 0f, 0.38f, 0.65f, 400f, 1200f, 1200f, 4000f);
                Sweep(b, 140f, 70f, 0.02f, 0.3f, 0.2f, 0.01f, 0.12f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Hurt, 0.7f, 0f, 0.9f, 1.1f, 3, v =>
            {
                float[] b = Buf(0.45f);
                Sweep(b, 360f - v * 30f, 190f, 0f, 0.28f, 0.5f, 0.01f, 0.12f, 2);
                LowPass(b, 2500f);
                Noise(b, 0f, 0.08f, 0.2f, 0.002f, 0.03f, 300f, 2000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Death, 0.9f, 0f, 1f, 1f, 1, v =>
            {
                float[] b = Buf(1.5f);
                Sweep(b, 300f, 70f, 0f, 1f, 0.5f, 0.02f, 0.5f, 2);
                LowPass(b, 1800f);
                Sweep(b, 70f, 35f, 0.9f, 0.4f, 0.6f, 0.005f, 0.15f);
                Noise(b, 0.9f, 0.2f, 0.4f, 0.002f, 0.08f, 100f, 900f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Block, 0.7f, 0.5f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(0.45f);
                Tone(b, 1750f, 0f, 0.3f, 0.4f, 0.001f, 0.09f);
                Tone(b, 2430f, 0f, 0.3f, 0.3f, 0.001f, 0.07f);
                Tone(b, 3150f, 0f, 0.3f, 0.2f, 0.001f, 0.05f);
                Noise(b, 0f, 0.02f, 0.5f, 0.0005f, 0.006f, 1500f, 9000f);
                return b;
            });

            // spells: Son Tinh's earth, Thuy Tinh's water
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.EarthSpell0, 0.9f, 0f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(1.6f);
                Noise(b, 0f, 1.2f, 0.7f, 0.25f, 0.5f, 40f, 260f);
                for (int k = 0; k < 5; k++) Noise(b, 0.1f + k * 0.17f + Rnd() * 0.05f, 0.06f, 0.6f, 0.001f, 0.02f, 600f, 3500f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.EarthSpell1, 0.95f, 0f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(1.8f);
                Sweep(b, 110f, 32f, 0f, 1.2f, 0.8f, 0.01f, 0.35f);
                Noise(b, 0f, 0.9f, 0.5f, 0.02f, 0.3f, 60f, 900f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.EarthSpell2, 1f, 0f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(2.4f);
                Sweep(b, 70f, 26f, 0f, 1.8f, 0.9f, 0.04f, 0.7f);
                Noise(b, 0f, 1.8f, 0.6f, 0.3f, 0.8f, 30f, 300f);
                for (int k = 0; k < 6; k++) Noise(b, 0.3f + k * 0.2f + Rnd() * 0.08f, 0.07f, 0.5f, 0.001f, 0.025f, 500f, 3000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.WaterSpell0, 0.8f, 0f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(1.1f);
                Whoosh(b, 0f, 0.8f, 0.55f, 300f, 1200f, 900f, 3500f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.WaterSpell1, 0.8f, 0f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(1.8f);
                Noise(b, 0f, 1.4f, 0.45f, 0.2f, 0.9f, 3500f, 9000f);
                for (int k = 0; k < 18; k++) Tone(b, 1000f + Rnd() * 1500f, Rnd() * 1.4f, 0.03f, 0.15f, 0.001f, 0.01f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.WaterSpell2, 0.95f, 0f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(2.2f);
                Whoosh(b, 0f, 1.6f, 0.7f, 150f, 700f, 700f, 2500f);
                Whoosh(b, 0.6f, 1.2f, 0.5f, 200f, 900f, 300f, 1200f);
                return b;
            });

            // enemies and the world
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.HitFlesh, 0.8f, 0.8f, 0.9f, 1.1f, 3, v =>
            {
                float[] b = Buf(0.3f);
                Noise(b, 0f, 0.08f, 0.7f, 0.001f, 0.025f, 200f, 2500f);
                Sweep(b, 150f, 70f, 0f, 0.12f, 0.6f, 0.002f, 0.05f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.HitHeavy, 0.95f, 0.8f, 0.92f, 1.05f, 2, v =>
            {
                float[] b = Buf(0.6f);
                Sweep(b, 110f, 38f, 0f, 0.35f, 0.9f, 0.002f, 0.12f);
                Noise(b, 0f, 0.18f, 0.6f, 0.001f, 0.07f, 100f, 3000f);
                Noise(b, 0f, 0.03f, 0.5f, 0.0005f, 0.01f, 2000f, 9000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.EnemyDeath, 0.9f, 0.9f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(1.3f);
                Sweep(b, 210f, 45f, 0f, 0.9f, 0.55f, 0.01f, 0.4f, 2);
                LowPass(b, 1500f);
                Sweep(b, 70f, 30f, 0.75f, 0.3f, 0.6f, 0.005f, 0.1f);
                Noise(b, 0.75f, 0.15f, 0.35f, 0.002f, 0.06f, 80f, 700f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.BellRing, 0.9f, 0f, 1f, 1f, 1, v =>
            {
                float[] b = Buf(3.8f);
                Bell(b, 330f, 0f, 0.7f, 1f);
                Echo(b, 0.25f, 0.3f, 2);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.WoodCrash, 0.9f, 0f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(1.5f);
                Sweep(b, 90f, 40f, 0f, 0.3f, 0.8f, 0.002f, 0.12f);
                for (int k = 0; k < 7; k++)
                {
                    float t = Rnd() * 0.9f;
                    Noise(b, t, 0.12f, 0.7f, 0.001f, 0.04f, 250f, 2500f);
                    Sweep(b, 170f, 90f, t, 0.08f, 0.5f, 0.001f, 0.03f);
                }
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.HorseNeigh, 0.9f, 0.3f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(1.6f);
                Sweep(b, 500f, 900f, 0f, 0.4f, 0.5f, 0.03f, 0.5f, 2);
                Sweep(b, 900f, 420f, 0.4f, 0.7f, 0.5f, 0.005f, 0.35f, 2);
                Sweep(b, 1000f, 1800f, 0f, 0.4f, 0.2f, 0.03f, 0.5f, 2);
                Sweep(b, 1800f, 840f, 0.4f, 0.7f, 0.2f, 0.005f, 0.35f, 2);
                LowPass(b, 3200f);
                Noise(b, 0f, 1.1f, 0.12f, 0.05f, 0.6f, 800f, 4000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.RoosterCluck, 0.8f, 0.5f, 0.9f, 1.1f, 2, v =>
            {
                float[] b = Buf(1f);
                float[] at = { 0f, 0.12f, 0.24f, 0.5f };
                for (int k = 0; k < at.Length; k++) Tone(b, 600f + k * 40f, at[k], 0.1f, 0.5f, 0.005f, 0.04f, 2, 30f, 0.04f);
                LowPass(b, 2800f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Splash, 0.8f, 0.7f, 0.9f, 1.1f, 2, v =>
            {
                float[] b = Buf(0.9f);
                Noise(b, 0f, 0.6f, 0.7f, 0.01f, 0.2f, 400f, 6000f);
                Noise(b, 0.05f, 0.3f, 0.3f, 0.01f, 0.1f, 2000f, 9000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.Thunder, 1f, 0f, 1f, 1f, 1, v =>
            {
                float[] b = Buf(3.8f);
                Noise(b, 0f, 3.2f, 0.8f, 0.15f, 1.3f, 30f, 220f);
                for (int k = 0; k < 5; k++) Noise(b, Rnd() * 2f, 0.4f, 0.6f, 0.05f, 0.2f, 40f, 300f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.ElephantRoar, 1f, 1f, 0.95f, 1.03f, 1, v =>
            {
                float[] b = Buf(2.6f);
                Sweep(b, 110f, 200f, 0f, 0.5f, 0.6f, 0.05f, 1.2f, 2);
                Sweep(b, 200f, 160f, 0.5f, 0.6f, 0.6f, 0.01f, 1.0f, 2);
                Sweep(b, 160f, 60f, 1.1f, 1.0f, 0.6f, 0.01f, 0.5f, 2);
                LowPass(b, 1400f);
                Noise(b, 0f, 2f, 0.2f, 0.1f, 1.2f, 300f, 1800f);
                Echo(b, 0.15f, 0.3f, 2);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.ElephantStomp, 1f, 1f, 0.9f, 1.05f, 2, v =>
            {
                float[] b = Buf(0.7f);
                Sweep(b, 75f, 32f, 0f, 0.35f, 0.9f, 0.002f, 0.12f);
                Noise(b, 0f, 0.2f, 0.45f, 0.002f, 0.08f, 60f, 500f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.CrocodileRoar, 1f, 1f, 0.95f, 1.05f, 1, v =>
            {
                float[] b = Buf(2f);
                Noise(b, 0f, 1.6f, 0.7f, 0.1f, 0.8f, 40f, 400f);
                Sweep(b, 95f, 55f, 0f, 1.4f, 0.6f, 0.1f, 0.8f, 2);
                Modulate(b, 28f, 0.4f);
                LowPass(b, 900f);
                return b;
            });
        }

        // ---------------------------------------------------------------- voices: battle cries and the babble of the dialogue

        // vowels as (F1, F2, F3) of a man's voice: a, o, e, u, i
        static readonly Vector3[] Vowels =
        {
            new(730f, 1090f, 2440f), new(570f, 840f, 2410f), new(530f, 1840f, 2480f), new(300f, 870f, 2240f), new(270f, 2290f, 3010f),
        };

        struct VoicePreset
        {
            public float f0, formantScale, breath, vibrato, rough, lowpass, syllable, rate, volume;
        }

        static void BuildShouts(List<AudioLibrary.SfxEntry> l)
        {
            // Son Tinh: a deep "Hựa!" for the heavy blow, a long "Háaa!" for a spell
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.SonShoutHeavy, 0.85f, 0f, 0.93f, 1.07f, 3, v =>
            {
                float[] b = Buf(0.5f);
                Noise(b, 0f, 0.05f, 0.4f, 0.004f, 0.02f, 1000f, 6000f);   // the breath of the "h"
                Formant(b, 0.03f, 0.32f, u => Mathf.Lerp(150f, 190f, Mathf.Sin(u * Mathf.PI)) * (1f - 0.35f * u * u) * (1f + 0.03f * v),
                    u => Vector3.Lerp(Vowels[0], Vowels[1], u) * 0.95f, 0.1f, 0.012f, 1f, 0f, 2.5f);
                LowPass(b, 5500f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.SonShoutSpell, 0.85f, 0f, 0.93f, 1.07f, 3, v =>
            {
                float[] b = Buf(0.7f);
                Noise(b, 0f, 0.05f, 0.4f, 0.004f, 0.02f, 1000f, 6000f);
                Formant(b, 0.03f, 0.55f, u => Mathf.Lerp(140f, 230f, Mathf.Sin(Mathf.Min(1f, u * 1.4f) * Mathf.PI * 0.5f)) * (1f - 0.25f * u),
                    u => Vector3.Lerp(Vowels[0], Vowels[2], Mathf.Clamp01(u * 1.2f - 0.2f)) * 0.95f, 0.1f, 0.02f, 1f, 0f, 2.2f);
                LowPass(b, 5500f);
                Echo(b, 0.07f, 0.2f, 2);
                return b;
            });

            // Thuy Tinh: a higher "Hyah!" and a rising "Haaa-ha!"
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.ThuyShoutHeavy, 0.85f, 0f, 0.93f, 1.07f, 3, v =>
            {
                float[] b = Buf(0.5f);
                Noise(b, 0f, 0.05f, 0.4f, 0.004f, 0.02f, 1500f, 7000f);
                Formant(b, 0.03f, 0.3f, u => Mathf.Lerp(170f, 250f, Mathf.Sin(u * Mathf.PI * 0.9f)) * (1f - 0.3f * u * u) * (1f + 0.03f * v),
                    u => Vector3.Lerp(Vowels[4], Vowels[0], Mathf.Clamp01(u * 2.2f)), 0.12f, 0.012f, 1f, 0f, 2.5f);
                LowPass(b, 6000f);
                return b;
            });
            AddSfx(l, SonTinhThuyTinh.Audio.Sfx.ThuyShoutSpell, 0.85f, 0f, 0.93f, 1.07f, 3, v =>
            {
                float[] b = Buf(0.75f);
                Noise(b, 0f, 0.05f, 0.4f, 0.004f, 0.02f, 1500f, 7000f);
                Formant(b, 0.03f, 0.58f, u => Mathf.Lerp(175f, 295f, Mathf.Sin(Mathf.Min(1f, u * 1.3f) * Mathf.PI * 0.5f)) * (1f - 0.3f * u),
                    u => Vector3.Lerp(Vowels[0], Vowels[2], Mathf.Clamp01(u * 1.3f - 0.3f)), 0.12f, 0.02f, 1f, 0f, 2.2f);
                LowPass(b, 6000f);
                Echo(b, 0.07f, 0.2f, 2);
                return b;
            });
        }

        static void BuildVoices(List<AudioLibrary.VoiceEntry> l)
        {
            //                                            f0    formants breath vib   rough  lowpass syll.  rate  volume
            AddVoice(l, VoiceId.SonTinh,  new VoicePreset { f0 = 118f, formantScale = 0.95f, breath = 0.05f, vibrato = 0.010f, rough = 0f,   lowpass = 0f,    syllable = 0.11f, rate = 8f, volume = 0.8f });
            AddVoice(l, VoiceId.ThuyTinh, new VoicePreset { f0 = 135f, formantScale = 1.00f, breath = 0.08f, vibrato = 0.012f, rough = 0f,   lowpass = 0f,    syllable = 0.11f, rate = 8f, volume = 0.8f });
            AddVoice(l, VoiceId.KingHung, new VoicePreset { f0 = 92f,  formantScale = 0.90f, breath = 0.10f, vibrato = 0.030f, rough = 0f,   lowpass = 3500f, syllable = 0.15f, rate = 6f, volume = 0.85f });
            AddVoice(l, VoiceId.MiNuong,  new VoicePreset { f0 = 235f, formantScale = 1.18f, breath = 0.05f, vibrato = 0.012f, rough = 0f,   lowpass = 0f,    syllable = 0.11f, rate = 8f, volume = 0.75f });
            AddVoice(l, VoiceId.Elder,    new VoicePreset { f0 = 108f, formantScale = 0.97f, breath = 0.12f, vibrato = 0.045f, rough = 0f,   lowpass = 4500f, syllable = 0.12f, rate = 7f, volume = 0.8f });
            AddVoice(l, VoiceId.Thief,    new VoicePreset { f0 = 100f, formantScale = 0.93f, breath = 0.25f, vibrato = 0.010f, rough = 0.5f, lowpass = 0f,    syllable = 0.10f, rate = 9f, volume = 0.8f });
            AddVoice(l, VoiceId.Elephant, new VoicePreset { f0 = 55f,  formantScale = 0.70f, breath = 0.20f, vibrato = 0.020f, rough = 0.3f, lowpass = 800f,  syllable = 0.22f, rate = 4f, volume = 1f });
            AddVoice(l, VoiceId.Narrator, new VoicePreset { f0 = 125f, formantScale = 1.00f, breath = 0.10f, vibrato = 0.008f, rough = 0f,   lowpass = 3200f, syllable = 0.12f, rate = 7f, volume = 0.45f });
        }

        static void AddVoice(List<AudioLibrary.VoiceEntry> list, VoiceId id, VoicePreset p)
        {
            sr = SfxRate; wrap = false;
            var clips = new List<AudioClip>();
            AudioClip[] over = Overrides("Voice" + id);
            if (over.Length > 0) clips.AddRange(over);
            else
                for (int v = 0; v < 5; v++)
                {
                    rng = new System.Random(id.GetHashCode() * 29 + v);
                    float[] b = Buf(p.syllable + 0.05f);
                    string consonant = "-tnkm"[v] == '-' ? "" : "tnkm"[v - 1].ToString();
                    if (consonant == "t") Noise(b, 0f, 0.02f, 0.5f, 0.001f, 0.008f, 3000f, 9000f);
                    else if (consonant == "k") Noise(b, 0f, 0.025f, 0.45f, 0.001f, 0.01f, 1200f, 4500f);
                    float f0 = p.f0;
                    Formant(b, consonant.Length > 0 ? 0.015f : 0f, p.syllable, u => f0 * (1f + 0.07f * Mathf.Sin(u * Mathf.PI)),
                        u => Vowels[v] * p.formantScale, p.breath, p.vibrato, 1f, p.rough, 0f);
                    if (p.lowpass > 0f) LowPass(b, p.lowpass);
                    Finish(b, 0.85f);
                    clips.Add(Write($"{Root}/Voice/{id}_{v}.wav", b, SfxRate, false));
                }
            list.Add(new AudioLibrary.VoiceEntry { id = id, syllables = clips.ToArray(), volume = p.volume, rate = p.rate, pitch = new Vector2(0.9f, 1.12f) });
        }

        // A voice: a pulse train at the pitch f0(u) (u = 0..1 over the sound) with a little breath, shaped by three resonances that
        // move with the vowel (formants(u) = F1, F2, F3). `rough` modulates it at 70 Hz (a growl), `drive` overdrives it (a shout).
        static void Formant(float[] b, float start, float dur, Func<float, float> f0, Func<float, Vector3> formants, float breath, float vibrato, float amp, float rough, float drive)
        {
            int i0 = Mathf.RoundToInt(start * sr), n = Mathf.RoundToInt(dur * sr);
            double phase = 0.0;
            float y11 = 0f, y12 = 0f, y21 = 0f, y22 = 0f, y31 = 0f, y32 = 0f;
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)n, t = k / (float)sr;
                float f = f0(u) * (1f + vibrato * Mathf.Sin(2f * Mathf.PI * 5.5f * t));
                phase += f / sr;
                float p = (float)(phase - Math.Floor(phase));
                float src = (1f - p) * (1f - p) * 2f - 0.5f + breath * NoiseSample();
                if (rough > 0f) src *= 1f - rough * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 70f * t));
                Vector3 fm = formants(u);
                float r1 = Mathf.Exp(-Mathf.PI * 70f / sr), r2 = Mathf.Exp(-Mathf.PI * 110f / sr), r3 = Mathf.Exp(-Mathf.PI * 170f / sr);
                float y1 = (1f - r1) * src + 2f * r1 * Mathf.Cos(2f * Mathf.PI * fm.x / sr) * y11 - r1 * r1 * y12;
                float y2 = (1f - r2) * src + 2f * r2 * Mathf.Cos(2f * Mathf.PI * fm.y / sr) * y21 - r2 * r2 * y22;
                float y3 = (1f - r3) * src + 2f * r3 * Mathf.Cos(2f * Mathf.PI * Mathf.Min(fm.z, sr * 0.45f) / sr) * y31 - r3 * r3 * y32;
                y12 = y11; y11 = y1; y22 = y21; y21 = y2; y32 = y31; y31 = y3;
                float env = Mathf.Clamp01(k / (0.012f * sr)) * Mathf.Clamp01((n - k) / (0.03f * sr));
                float o = (y1 * 1f + y2 * 0.7f + y3 * 0.45f) * 6f * amp * env;
                if (drive > 0f) o = (float)Math.Tanh(o * 0.12f * drive);   // the raw level is about 10: bring it to ~1 before it is overdriven
                Put(b, i0 + k, o);
            }
        }

        // ---------------------------------------------------------------- ambience and music

        static void BuildLoops(List<AudioLibrary.LoopEntry> l)
        {
            AddLoop(l, LoopId.AmbForest, 0.55f, false, AmbForest);
            AddLoop(l, LoopId.AmbWater, 0.55f, false, AmbWater);
            AddLoop(l, LoopId.AmbPalace, 0.5f, false, AmbPalace);
            AddLoop(l, LoopId.AmbWind, 0.5f, false, AmbWind);
            AddLoop(l, LoopId.MusicMenu, 0.5f, true, MusicMenu);
            AddLoop(l, LoopId.MusicSonTinh, 0.5f, true, MusicSonTinh);
            AddLoop(l, LoopId.MusicThuyTinh, 0.5f, true, MusicThuyTinh);
            AddLoop(l, LoopId.MusicPalace, 0.5f, true, MusicPalace);
            AddLoop(l, LoopId.MusicCutscene, 0.5f, true, MusicCutscene);
            AddLoop(l, LoopId.MusicBattle, 0.55f, true, MusicBattle);
        }

        // A slowly breathing wind: noise through a low-pass, its level moved by whole cycles over the loop so the ends meet.
        static void Wind(float[] b, float amp, float cutoff, int cycles, int cycles2)
        {
            float[] w = Buf(b.Length / (float)sr);
            for (int i = 0; i < w.Length; i++) w[i] = NoiseSample();
            LowPass(w, cutoff);
            for (int i = 0; i < w.Length; i++)
            {
                float t = i / (float)w.Length;
                float level = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * cycles * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * cycles2 * t + 1.3f);
                b[i] += w[i] * amp * level * 3f;
            }
        }

        static float[] AmbForest()
        {
            float[] b = Buf(20f);
            Wind(b, 0.5f, 500f, 2, 5);
            Noise(b, 0f, 20f, 0.05f, 0.5f, 100f, 2500f, 7000f);
            for (int k = 0; k < 9; k++)   // birds: two to four little glides
            {
                float t = Rnd() * 19f, f = 2600f + Rnd() * 1000f;
                int notes = 2 + rng.Next(3);
                for (int n = 0; n < notes; n++) Sweep(b, f, f * (1.1f + Rnd() * 0.4f), t + n * 0.11f, 0.08f, 0.1f, 0.01f, 0.05f);
            }
            for (int s = 0; s < 2; s++)   // crickets: a high tone pulsed fast
            {
                float start = s * 10f + 2f + Rnd() * 3f;
                float[] c = Buf(4f);
                Tone(c, 4300f, 0f, 4f, 1f, 0.2f, 100f);
                Modulate(c, 16f, 0.95f);
                Add(b, c, start, 0.025f);
            }
            return b;
        }

        static float[] AmbWater()
        {
            float[] b = Buf(20f);
            Wind(b, 0.3f, 700f, 3, 7);
            float[] stream = Buf(20f);
            for (int i = 0; i < stream.Length; i++) stream[i] = NoiseSample();
            BandPass(stream, 300f, 2500f);
            for (int i = 0; i < stream.Length; i++) b[i] += stream[i] * 0.45f * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 4f * i / stream.Length));
            for (int k = 0; k < 14; k++) Sweep(b, 1100f, 750f, Rnd() * 19.5f, 0.08f, 0.1f, 0.003f, 0.03f);   // drips
            for (int k = 0; k < 4; k++)   // frogs
            {
                float[] f = Buf(0.5f);
                Tone(f, 160f, 0f, 0.4f, 0.8f, 0.02f, 0.25f, 2);
                Modulate(f, 25f, 0.6f);
                LowPass(f, 700f);
                Add(b, f, Rnd() * 19f, 0.12f);
            }
            return b;
        }

        static float[] AmbPalace()
        {
            float[] b = Buf(20f);
            Wind(b, 0.3f, 350f, 2, 3);
            float[] chimes = { 784f, 987.8f, 1175f, 1568f };
            for (int k = 0; k < 4; k++) Bell(b, chimes[rng.Next(chimes.Length)], 2f + k * 4.5f + Rnd() * 1.5f, 0.12f, 1f);
            Echo(b, 0.4f, 0.4f, 3);
            return b;
        }

        static float[] AmbWind()
        {
            float[] b = Buf(20f);
            Wind(b, 0.5f, 450f, 2, 6);
            Noise(b, 0f, 20f, 0.04f, 0.5f, 100f, 2000f, 6000f);
            return b;
        }

        // ---- music ----

        static int Scale(int root, int[] scale, int degree)
        {
            int oct = Mathf.FloorToInt(degree / (float)scale.Length), idx = ((degree % scale.Length) + scale.Length) % scale.Length;
            return root + 12 * oct + scale[idx];
        }

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        static float[] Track(float bpm, int bars, out float beat)
        {
            beat = 60f / bpm;
            return Buf(bars * 4 * beat);
        }

        static void Pluck(float[] b, int midi, float start, float len, float amp)
        {
            Tone(b, Hz(midi), start, len, amp, 0.004f, 0.25f, 1);
            Tone(b, Hz(midi) * 2f, start, len, amp * 0.3f, 0.004f, 0.12f);
        }

        static void Pad(float[] b, int midi, float start, float dur, float amp)
        {
            Tone(b, Hz(midi), start, dur, amp, dur * 0.35f, dur * 2f, 0, 0f, 0f, dur * 0.4f);
            Tone(b, Hz(midi) * 2.003f, start, dur, amp * 0.3f, dur * 0.4f, dur * 2f, 0, 0f, 0f, dur * 0.4f);
        }

        static void Flute(float[] b, int midi, float start, float dur, float amp)
        {
            Tone(b, Hz(midi), start, dur, amp, 0.07f, dur * 1.2f, 0, 5f, 0.006f, 0.12f);
            Tone(b, Hz(midi) * 2f, start, dur, amp * 0.18f, 0.07f, dur, 0, 5f, 0.006f, 0.12f);
            Noise(b, start, dur, amp * 0.06f, 0.07f, dur, 1500f, 5000f);
        }

        static void KickDrum(float[] b, float start, float amp)
        {
            Sweep(b, 120f, 45f, start, 0.25f, amp, 0.002f, 0.09f);
            Noise(b, start, 0.02f, amp * 0.3f, 0.0005f, 0.006f, 1000f, 5000f);
        }

        static void FrameDrum(float[] b, float start, float amp)
        {
            Sweep(b, 160f, 80f, start, 0.22f, amp, 0.002f, 0.08f);
            Noise(b, start, 0.1f, amp * 0.4f, 0.002f, 0.03f, 200f, 800f);
        }

        static void Hat(float[] b, float start, float amp) => Noise(b, start, 0.05f, amp, 0.0008f, 0.012f, 6000f, 11000f);

        static void Bass(float[] b, int midi, float start, float len, float amp)
        {
            Tone(b, Hz(midi), start, len, amp, 0.01f, 0.2f, 0);
            Tone(b, Hz(midi) * 2f, start, len, amp * 0.25f, 0.01f, 0.12f, 2);
        }

        // a drone that holds a whole number of cycles over the loop, so it never clicks at the seam
        static void Drone(float[] b, float freq, float amp)
        {
            float len = b.Length / (float)sr, f = Mathf.Max(1f, Mathf.Round(freq * len)) / len;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)sr;
                b[i] += Mathf.Sin(2f * Mathf.PI * f * t) * amp * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * t / len));
            }
        }

        static float[] MusicMenu()
        {
            float[] b = Track(72f, 12, out float beat);
            int[] minor = { 0, 3, 5, 7, 10 };
            int[] roots = { 50, 46, 53, 48 };
            for (int c = 0; c < 6; c++)
            {
                int root = roots[c % 4];
                float t0 = c * 8 * beat;
                Pad(b, root, t0, 8.6f * beat, 0.16f);
                Pad(b, root + 7, t0, 8.6f * beat, 0.11f);
                Pad(b, root + 15, t0, 8.6f * beat, 0.08f);
                for (int i = 0; i < 16; i++)   // an arpeggio of eighth notes over the chord
                {
                    int[] shape = { 0, 2, 4, 2 };
                    Pluck(b, Scale(root + 24, minor, shape[i % 4] + (i / 8) * 1), t0 + i * 0.5f * beat, 1.2f * beat, 0.16f);
                }
            }
            for (int phrase = 0; phrase < 6; phrase++)   // a slow flute line
            {
                float t0 = phrase * 8 * beat + 2f * beat;
                int deg = 2 + rng.Next(4);
                float[] lens = { 1.5f, 1f, 2f, 1.5f };
                float t = t0;
                for (int n = 0; n < 4; n++)
                {
                    deg += rng.Next(3) - 1;
                    Flute(b, Scale(74, minor, deg), t, lens[n] * beat * 0.95f, 0.18f);
                    t += lens[n] * beat;
                }
            }
            Echo(b, beat * 0.75f, 0.45f, 5, true);
            return b;
        }

        static float[] MusicSonTinh()
        {
            float[] b = Track(84f, 12, out float beat);
            int[] minor = { 0, 3, 5, 7, 10 };
            Drone(b, Hz(38), 0.2f);
            Drone(b, Hz(45), 0.12f);
            for (int bar = 0; bar < 12; bar++)
            {
                float t0 = bar * 4 * beat;
                FrameDrum(b, t0, 0.5f);
                FrameDrum(b, t0 + 1.5f * beat, 0.28f);
                FrameDrum(b, t0 + 2f * beat, 0.4f);
                FrameDrum(b, t0 + 3.25f * beat, 0.26f);
                if (bar % 4 == 0) Bell(b, Hz(50), t0, 0.28f, 1.2f);   // a bronze drum stroke
            }
            for (int phrase = 0; phrase < 6; phrase++)
            {
                float t = phrase * 8 * beat + 1f * beat;
                int deg = 3 + rng.Next(3);
                float[] lens = { 1f, 1f, 0.5f, 1.5f, 1f, 1f, 2f };
                for (int n = 0; n < lens.Length; n++)
                {
                    deg += rng.Next(3) - 1;
                    if (rng.Next(5) != 0) Flute(b, Scale(62, minor, deg), t, lens[n] * beat * 0.95f, 0.2f);
                    t += lens[n] * beat;
                }
            }
            Echo(b, beat * 0.75f, 0.4f, 4, true);
            return b;
        }

        static float[] MusicThuyTinh()
        {
            float[] b = Track(76f, 12, out float beat);
            int[] minor = { 0, 3, 5, 7, 10 };
            int[] roots = { 52, 48, 55, 50 };
            for (int c = 0; c < 6; c++)
            {
                int root = roots[c % 4];
                float t0 = c * 8 * beat;
                Pad(b, root, t0, 8.6f * beat, 0.14f);
                Pad(b, root + 7, t0, 8.6f * beat, 0.1f);
                int[] shape = { 0, 1, 2, 3, 4, 3, 2, 1 };   // a harp running up and down
                for (int i = 0; i < 16; i++)
                    Pluck(b, Scale(root + 24, minor, shape[i % 8] + 2), t0 + i * 0.5f * beat, 1.6f * beat, 0.15f);
            }
            for (int k = 0; k < 6; k++) Bell(b, Hz(Scale(88, minor, rng.Next(5))), k * 8 * beat + (2 + rng.Next(4)) * beat, 0.1f, 0.8f);
            Echo(b, beat * 0.75f, 0.55f, 6, true);
            return b;
        }

        static float[] MusicPalace()
        {
            float[] b = Track(66f, 12, out float beat);
            int[] major = { 0, 2, 4, 7, 9 };
            int[] roots = { 50, 45, 47, 43 };
            for (int c = 0; c < 6; c++)
            {
                int root = roots[c % 4];
                float t0 = c * 8 * beat;
                Pad(b, root, t0, 8.6f * beat, 0.16f);
                Pad(b, root + 7, t0, 8.6f * beat, 0.12f);
                Pad(b, root + 12, t0, 8.6f * beat, 0.1f);
                Pad(b, root + (c % 4 == 2 ? 15 : 16), t0, 8.6f * beat, 0.07f);
            }
            for (int bar = 0; bar < 12; bar++) KickDrum(b, bar * 4 * beat, 0.3f);
            for (int phrase = 0; phrase < 6; phrase++)
            {
                float t = phrase * 8 * beat + 2f * beat;
                int deg = 5 + rng.Next(3);
                for (int n = 0; n < 5; n++)
                {
                    deg += rng.Next(3) - 1;
                    Bell(b, Hz(Scale(74, major, deg)), t, 0.2f, 0.9f);
                    t += (n % 2 == 0 ? 1f : 1.5f) * beat;
                }
            }
            Echo(b, beat * 0.5f, 0.45f, 5, true);
            return b;
        }

        static float[] MusicCutscene()
        {
            float[] b = Track(60f, 8, out float beat);
            Drone(b, Hz(38), 0.22f);
            Drone(b, Hz(39), 0.1f);
            Drone(b, Hz(45), 0.12f);
            for (int bar = 0; bar < 8; bar++)
            {
                float t0 = bar * 4 * beat;
                KickDrum(b, t0, 0.35f);
                KickDrum(b, t0 + 0.6f * beat, 0.2f);   // a slow heartbeat
                KickDrum(b, t0 + 2f * beat, 0.3f);
                KickDrum(b, t0 + 2.6f * beat, 0.18f);
            }
            Bell(b, Hz(62), 2 * 4 * beat, 0.25f, 1.5f);
            Bell(b, Hz(63), 6 * 4 * beat, 0.25f, 1.5f);
            Flute(b, 74, 4 * 4 * beat + beat, 6 * beat, 0.16f);
            Echo(b, beat * 0.75f, 0.5f, 5, true);
            return b;
        }

        static float[] MusicBattle()
        {
            float[] b = Track(132f, 16, out float beat);
            int[] minor = { 0, 3, 5, 7, 10 };
            int[] roots = { 38, 38, 41, 36 };
            for (int bar = 0; bar < 16; bar++)
            {
                float t0 = bar * 4 * beat;
                int root = roots[(bar / 2) % 4];
                for (int q = 0; q < 4; q++) KickDrum(b, t0 + q * beat, q % 2 == 0 ? 0.7f : 0.5f);
                for (int e = 0; e < 8; e++) { FrameDrum(b, t0 + (e + 0.5f) * 0.5f * beat, e % 2 == 0 ? 0.18f : 0.3f); }
                for (int s = 0; s < 16; s++) Hat(b, t0 + s * 0.25f * beat, s % 4 == 2 ? 0.12f : 0.06f);
                for (int e = 0; e < 8; e++) Bass(b, root + (e % 4 == 3 ? 7 : 0), t0 + e * 0.5f * beat, 0.45f * beat, 0.28f);
                Pluck(b, root + 24, t0, 0.8f * beat, 0.2f);
                Pluck(b, root + 31, t0, 0.8f * beat, 0.16f);
                Pluck(b, root + 27, t0 + 2.5f * beat, 0.8f * beat, 0.2f);
            }
            for (int phrase = 0; phrase < 8; phrase++)
            {
                float t = phrase * 8 * beat + beat;
                int deg = 6 + rng.Next(3);
                for (int n = 0; n < 6; n++)
                {
                    deg += rng.Next(3) - 1;
                    Pluck(b, Scale(62, minor, deg), t, 0.7f * beat, 0.2f);
                    t += (n % 3 == 2 ? 1f : 0.5f) * beat;
                }
            }
            Echo(b, beat * 0.75f, 0.3f, 3, true);
            return b;
        }

        // ---------------------------------------------------------------- DSP

        static float Rnd() => (float)rng.NextDouble();

        static float NoiseSample() => (float)(rng.NextDouble() * 2.0 - 1.0);

        static float[] Buf(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * sr))];

        static void Put(float[] b, int index, float value)
        {
            if (index < 0) return;
            if (index >= b.Length)
            {
                if (!wrap) return;
                index %= b.Length;
            }
            b[index] += value;
        }

        static float Envelope(float t, float attack, float tau) => (attack > 0f && t < attack ? t / attack : 1f) * Mathf.Exp(-Mathf.Max(0f, t - attack) / Mathf.Max(0.0001f, tau));

        static float Osc(double phase, int wave)
        {
            double p = phase - Math.Floor(phase);
            switch (wave)
            {
                case 0: return (float)Math.Sin(p * 2.0 * Math.PI);
                case 1: return (float)(4.0 * Math.Abs(p - 0.5) - 1.0);
                case 2: return (float)(2.0 * p - 1.0);
                default: return p < 0.5 ? 1f : -1f;
            }
        }

        // a note: wave 0 sine, 1 triangle, 2 saw, 3 square; an exponential decay (tau) after the attack, a linear fade over the last `release` seconds
        static void Tone(float[] b, float freq, float start, float dur, float amp, float attack, float tau, int wave = 0, float vibHz = 0f, float vibDepth = 0f, float release = 0.01f)
        {
            int i0 = Mathf.RoundToInt(start * sr), n = Mathf.RoundToInt(dur * sr);
            double phase = 0.0;
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)sr;
                float f = freq * (1f + (vibHz > 0f ? vibDepth * Mathf.Sin(2f * Mathf.PI * vibHz * t) : 0f));
                phase += f / sr;
                float fade = release > 0f ? Mathf.Clamp01((dur - t) / release) : 1f;
                Put(b, i0 + k, Osc(phase, wave) * amp * Envelope(t, attack, tau) * fade);
            }
        }

        // a note gliding exponentially from f0 to f1
        static void Sweep(float[] b, float f0, float f1, float start, float dur, float amp, float attack, float tau, int wave = 0)
        {
            int i0 = Mathf.RoundToInt(start * sr), n = Mathf.RoundToInt(dur * sr);
            double phase = 0.0;
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)sr;
                float f = f0 * Mathf.Pow(f1 / f0, t / dur);
                phase += f / sr;
                float fade = Mathf.Clamp01((dur - t) / 0.01f);
                Put(b, i0 + k, Osc(phase, wave) * amp * Envelope(t, attack, tau) * fade);
            }
        }

        // a burst of noise kept between two cut-off frequencies
        static void Noise(float[] b, float start, float dur, float amp, float attack, float tau, float lo, float hi)
        {
            int i0 = Mathf.RoundToInt(start * sr), n = Mathf.RoundToInt(dur * sr);
            float aHi = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Min(hi, sr * 0.45f) / sr), aLo = 1f - Mathf.Exp(-2f * Mathf.PI * lo / sr);
            float y1 = 0f, y2 = 0f;
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)sr;
                y1 += aHi * (NoiseSample() - y1);
                y2 += aLo * (y1 - y2);
                float fade = Mathf.Clamp01((dur - t) / 0.01f);
                Put(b, i0 + k, (y1 - y2) * 3f * amp * Envelope(t, attack, tau) * fade);
            }
        }

        // a rush of air: noise through a band that slides from (lo0, hi0) to (lo1, hi1), swelling and fading
        static void Whoosh(float[] b, float start, float dur, float amp, float lo0, float hi0, float lo1, float hi1)
        {
            int i0 = Mathf.RoundToInt(start * sr), n = Mathf.RoundToInt(dur * sr);
            float y1 = 0f, y2 = 0f;
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)n;
                float hi = Mathf.Min(Mathf.Lerp(hi0, hi1, u), sr * 0.45f), lo = Mathf.Lerp(lo0, lo1, u);
                float aHi = 1f - Mathf.Exp(-2f * Mathf.PI * hi / sr), aLo = 1f - Mathf.Exp(-2f * Mathf.PI * lo / sr);
                y1 += aHi * (NoiseSample() - y1);
                y2 += aLo * (y1 - y2);
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)), 1.4f);
                Put(b, i0 + k, (y1 - y2) * 3f * amp * env);
            }
        }

        // a bell: five inharmonic partials with their own decays
        static void Bell(float[] b, float freq, float start, float amp, float tauScale)
        {
            float[] ratio = { 1f, 2f, 2.76f, 5.4f, 8.93f }, level = { 1f, 0.55f, 0.45f, 0.2f, 0.1f }, tau = { 2.4f, 1.6f, 1.1f, 0.6f, 0.35f };
            for (int p = 0; p < ratio.Length; p++)
            {
                float f = freq * ratio[p];
                if (f > sr * 0.45f) continue;
                Tone(b, f, start, tau[p] * tauScale * 4f, amp * level[p] * 0.4f, 0.002f, tau[p] * tauScale, 0, 0f, 0f, 0.05f);
            }
        }

        static void Add(float[] dst, float[] src, float start, float gain)
        {
            int i0 = Mathf.RoundToInt(start * sr);
            for (int i = 0; i < src.Length; i++) Put(dst, i0 + i, src[i] * gain);
        }

        static void Modulate(float[] b, float hz, float depth)
        {
            for (int i = 0; i < b.Length; i++) b[i] *= 1f - depth + depth * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * hz * i / sr));
        }

        static void LowPass(float[] b, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sr), y = 0f;
            for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] = y; }
        }

        static void HighPass(float[] b, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sr), y = 0f;
            for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] -= y; }
        }

        static void BandPass(float[] b, float lo, float hi)
        {
            LowPass(b, hi);
            HighPass(b, lo);
        }

        // repeats of the sound, each softer; circular on a loop so the tail of the last bar rings into the first
        static void Echo(float[] b, float delay, float feedback, int taps, bool circular = false)
        {
            float[] src = (float[])b.Clone();
            int d = Mathf.RoundToInt(delay * sr), n = b.Length;
            for (int tap = 1; tap <= taps; tap++)
            {
                float g = Mathf.Pow(feedback, tap);
                for (int i = 0; i < n; i++)
                {
                    int j = i - tap * d;
                    if (circular) j = ((j % n) + n) % n;
                    else if (j < 0) continue;
                    b[i] += src[j] * g;
                }
            }
        }

        // Makes noisy material loop without a click: the last `seconds` fade into the first, and the loop is that much shorter.
        static float[] Seamless(float[] b, float seconds)
        {
            int x = Mathf.Min(b.Length / 3, Mathf.RoundToInt(seconds * sr)), n = b.Length - x;
            var r = new float[n];
            Array.Copy(b, r, n);
            for (int i = 0; i < x; i++)
            {
                float w = i / (float)x;
                r[i] = b[i] * Mathf.Sqrt(w) + b[n + i] * Mathf.Sqrt(1f - w);
            }
            return r;
        }

        // two milliseconds of fade at each end (no clicks), then the loudest sample brought to `peak`
        static void Finish(float[] b, float peak)
        {
            if (!wrap)
            {
                int fade = Mathf.Min(b.Length / 2, Mathf.RoundToInt(0.002f * sr));
                for (int i = 0; i < fade; i++)
                {
                    float g = i / (float)fade;
                    b[i] *= g;
                    b[b.Length - 1 - i] *= g;
                }
            }
            float max = 0.0001f;
            for (int i = 0; i < b.Length; i++) max = Mathf.Max(max, Mathf.Abs(b[i]));
            float gain = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] = Mathf.Clamp(b[i] * gain, -1f, 1f);
        }

        // ---------------------------------------------------------------- files

        static AudioClip Write(string path, float[] data, int rate, bool streaming)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = data.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + bytes);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(bytes);
                for (int i = 0; i < data.Length; i++) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(data[i], -1f, 1f) * 32767f));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                AudioImporterSampleSettings s = importer.defaultSampleSettings;
                s.loadType = streaming ? AudioClipLoadType.Streaming : (data.Length > rate * 3 ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad);
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                importer.defaultSampleSettings = s;
                importer.forceToMono = true;
                importer.loadInBackground = streaming;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
