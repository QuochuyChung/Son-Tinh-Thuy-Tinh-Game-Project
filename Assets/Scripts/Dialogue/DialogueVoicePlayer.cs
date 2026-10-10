using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue
{
    // Plays a pre-generated TTS clip for the line that DialogueRunner just started:
    // Resources/Audio/Voice/<folder>/<sha1(line.text) UTF-8, first 16 hex chars>.mp3
    // Missing clip (narration, runtime-only lines) = silence. Attached automatically by DialogueRunner.Awake.
    public class DialogueVoicePlayer : MonoBehaviour
    {
        const string ResourcesRoot = "Audio/Voice/";
        const string NarratorSpeaker = "Dẫn chuyện";

        [Tooltip("Optional. Voice weights asset at Assets/Resources/VoiceLibrary. Falls back to built-in defaults.")]
        [SerializeField] VoiceLibrary library;

        DialogueRunner runner;
        AudioSource source;
        readonly Dictionary<string, Dictionary<string, AudioClip>> clipsByFolder = new Dictionary<string, Dictionary<string, AudioClip>>();

        // Question-mark lines: pitch glides upward across the clip so the delivery rises like a real question.
        bool pitchRamp;
        float rampFrom, rampTo;

        void Awake()
        {
            runner = GetComponent<DialogueRunner>();

            Transform child = transform.Find("DialogueVoice");
            GameObject audioObject = child != null ? child.gameObject : new GameObject("DialogueVoice");
            if (child == null) audioObject.transform.SetParent(transform, false);
            source = audioObject.GetComponent<AudioSource>();
            if (source == null) source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            if (library == null) library = Resources.Load<VoiceLibrary>("VoiceLibrary");
        }

        void OnEnable()
        {
            if (runner != null) runner.LineStarted += OnLineStarted;
        }

        void OnDisable()
        {
            if (runner != null) runner.LineStarted -= OnLineStarted;
            if (source != null) source.Stop();
        }

        void Update()
        {
            if (source != null && source.isPlaying && runner != null && !runner.IsPlaying)
                source.Stop();
            if (pitchRamp && source != null && source.isPlaying && source.clip != null && source.clip.length > 0.1f)
                source.pitch = Mathf.Lerp(rampFrom, rampTo, Mathf.Clamp01(source.time / source.clip.length));
        }

        void OnLineStarted(DialogueLine line)
        {
            if (!TryGetVoice(line.speaker, out string folder, out float volume, out float pitch)
                || string.IsNullOrEmpty(line.text))
            {
                source.Stop();
                return;
            }

            AudioClip clip = LoadClip(folder, Hash(line.text));
            if (clip == null)
            {
                source.Stop();
                return;
            }

            // Delivery variation: small per-line pitch jitter, rising glide for questions, punch for exclamations.
            float basePitch = pitch * Random.Range(0.97f, 1.03f);
            float lineVolume = volume * (library != null ? library.masterVolume : 1f);
            string trimmed = line.text.TrimEnd();
            if (trimmed.EndsWith("?"))
            {
                pitchRamp = true;
                rampFrom = basePitch * 0.96f;
                rampTo = basePitch * 1.07f;
                source.pitch = rampFrom;
            }
            else
            {
                pitchRamp = false;
                source.pitch = basePitch;
            }
            if (trimmed.EndsWith("!"))
                lineVolume = Mathf.Min(1f, lineVolume * 1.15f);
            source.volume = lineVolume;
            source.clip = clip;
            source.Stop();
            source.Play();
        }

        bool TryGetVoice(string speaker, out string folder, out float volume, out float pitch)
        {
            folder = null;
            volume = 1f;
            pitch = 1f;
            // An empty speaker means narration in DialogueSequence assets -> narrator voice.
            if (string.IsNullOrEmpty(speaker)) speaker = NarratorSpeaker;

            if (library != null && library.entries != null)
            {
                foreach (VoiceLibrary.Entry entry in library.entries)
                {
                    if (entry == null || entry.speaker != speaker) continue;
                    folder = entry.folder;
                    volume = entry.volume;
                    pitch = entry.pitch;
                    return !string.IsNullOrEmpty(folder);
                }
            }

            foreach (VoiceLibrary.Entry entry in VoiceLibrary.CreateDefaults())
            {
                if (entry.speaker != speaker) continue;
                folder = entry.folder;
                volume = entry.volume;
                pitch = entry.pitch;
                return true;
            }
            return false;
        }

        AudioClip LoadClip(string folder, string hash)
        {
            if (!clipsByFolder.TryGetValue(folder, out Dictionary<string, AudioClip> map))
            {
                map = new Dictionary<string, AudioClip>();
                foreach (AudioClip clip in Resources.LoadAll<AudioClip>(ResourcesRoot + folder))
                    map[clip.name] = clip;
                clipsByFolder[folder] = map;
            }
            return map.TryGetValue(hash, out AudioClip found) ? found : null;
        }

        static string Hash(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                StringBuilder builder = new StringBuilder(16);
                for (int i = 0; i < 8; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
