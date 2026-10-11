using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Audio
{
    // Which clips play for each Sfx / LoopId. Loaded from Resources by AudioManager. Filled by the placeholder generator
    // (Assets/Editor/AudioGenerator.cs); to use a real recording, drop the clip into the entry's list in the Inspector.
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class SfxEntry
        {
            public Sfx id;
            [Tooltip("One of these is picked at random each time.")]
            public AudioClip[] clips;
            [Range(0f, 2f)] public float volume = 1f;
            [Tooltip("Random pitch between these two values.")]
            public Vector2 pitch = new(0.95f, 1.05f);
            [Tooltip("0 = plays flat in the head (the player's own sounds, the interface), 1 = fully 3D at its position.")]
            [Range(0f, 1f)] public float spatial;
        }

        [Serializable]
        public class LoopEntry
        {
            public LoopId id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.5f;
        }

        [Serializable]
        public class VoiceEntry
        {
            public VoiceId id;
            [Tooltip("The syllables the babble is made of (a, o, e, u, i sounds).")]
            public AudioClip[] syllables;
            [Range(0f, 2f)] public float volume = 0.8f;
            [Tooltip("Syllables per second while a line is typed.")]
            public float rate = 8f;
            [Tooltip("Random pitch spread of each syllable, so the babble is not a drone.")]
            public Vector2 pitch = new(0.9f, 1.12f);
        }

        public const string ResourceName = "AudioLibrary";

        public SfxEntry[] sfx = new SfxEntry[0];
        public LoopEntry[] loops = new LoopEntry[0];
        public VoiceEntry[] voices = new VoiceEntry[0];

        Dictionary<Sfx, SfxEntry> sfxLookup;
        Dictionary<LoopId, LoopEntry> loopLookup;
        Dictionary<VoiceId, VoiceEntry> voiceLookup;

        public VoiceEntry Get(VoiceId id)
        {
            if (voiceLookup == null)
            {
                voiceLookup = new Dictionary<VoiceId, VoiceEntry>();
                foreach (VoiceEntry e in voices) if (e != null) voiceLookup[e.id] = e;
            }
            return voiceLookup.TryGetValue(id, out VoiceEntry entry) ? entry : null;
        }

        public SfxEntry Get(Sfx id)
        {
            if (sfxLookup == null)
            {
                sfxLookup = new Dictionary<Sfx, SfxEntry>();
                foreach (SfxEntry e in sfx) if (e != null) sfxLookup[e.id] = e;
            }
            return sfxLookup.TryGetValue(id, out SfxEntry entry) ? entry : null;
        }

        public LoopEntry Get(LoopId id)
        {
            if (loopLookup == null)
            {
                loopLookup = new Dictionary<LoopId, LoopEntry>();
                foreach (LoopEntry e in loops) if (e != null) loopLookup[e.id] = e;
            }
            return loopLookup.TryGetValue(id, out LoopEntry entry) ? entry : null;
        }

        void OnValidate()
        {
            sfxLookup = null;
            loopLookup = null;
            voiceLookup = null;
        }
    }
}
