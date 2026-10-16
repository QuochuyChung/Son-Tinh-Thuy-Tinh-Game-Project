using System;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue
{
    // Per-speaker voice weights for DialogueVoicePlayer. Create via Tools > Create Voice Library;
    // without the asset the built-in defaults from CreateDefaults() are used.
    [CreateAssetMenu(fileName = "VoiceLibrary", menuName = "Son Tinh Thuy Tinh/Voice Library")]
    public class VoiceLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Must match DialogueLine.speaker exactly.")]
            public string speaker;
            [Tooltip("Folder under Resources/Audio/Voice.")]
            public string folder;
            [Range(0f, 2f), Tooltip("Volume weight for this speaker.")]
            public float volume = 1f;
            [Range(0.5f, 2f), Tooltip("Pitch weight for this speaker (1 = as generated).")]
            public float pitch = 1f;
            [Tooltip("edge-tts voice name baked into the mp3s, e.g. vi-VN-NamMinhNeural. Used by the Inspector's Regenerate button.")]
            public string voice = "vi-VN-NamMinhNeural";
            [Tooltip("Base speaking rate baked into the mp3s, e.g. +10%, -8%. Used by the Inspector's Regenerate button.")]
            public string rate = "+0%";
        }

        [Range(0f, 2f)] public float masterVolume = 1f;
        public Entry[] entries = Array.Empty<Entry>();

        static Entry[] defaults;

        public static Entry[] CreateDefaults()
        {
            if (defaults == null)
            {
                defaults = new[]
                {
                    new Entry { speaker = "Sơn Tinh", folder = "SonTinh", volume = 1f, pitch = 0.9f, voice = "vi-VN-NamMinhNeural", rate = "+10%" },
                    new Entry { speaker = "Thủy Tinh", folder = "ThuyTinh", volume = 1f, pitch = 0.92f, voice = "vi-VN-NamMinhNeural", rate = "+16%" },
                    new Entry { speaker = "Hùng Vương", folder = "HungVuong", volume = 1f, pitch = 0.8f, voice = "vi-VN-NamMinhNeural", rate = "+8%" },
                    new Entry { speaker = "Mị Nương", folder = "MiNuong", volume = 0.8f, pitch = 1.1f, voice = "fr-FR-VivienneMultilingualNeural", rate = "+4%" },
                    new Entry { speaker = "Người chăn ngựa", folder = "NguoiChanNgua", volume = 0.9f, pitch = 0.95f, voice = "vi-VN-NamMinhNeural", rate = "+4%" },
                    new Entry { speaker = "Thủ lĩnh Ninja", folder = "ThuLinhNinja", volume = 0.9f, pitch = 0.9f, voice = "vi-VN-NamMinhNeural", rate = "+6%" },
                    new Entry { speaker = "Ninja", folder = "Ninja", volume = 0.8f, pitch = 1.1f, voice = "vi-VN-NamMinhNeural", rate = "+20%" },
                    new Entry { speaker = "Voi Chín Ngà", folder = "VoiChinNga", volume = 1f, pitch = 0.75f, voice = "vi-VN-NamMinhNeural", rate = "-8%" },
                    new Entry { speaker = "Dẫn chuyện", folder = "Narrator", volume = 1f, pitch = 0.9f, voice = "vi-VN-NamMinhNeural", rate = "+4%" },
                };
            }
            return defaults;
        }
    }
}
