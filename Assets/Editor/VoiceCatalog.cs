using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue.Editor
{
    [Serializable]
    public class VoiceInfo
    {
        public string Name;
        public string ShortName;
        public string Gender;
        public string Locale;
        public string LocaleName;
        public string FriendlyName;
    }

    public static class VoiceCatalog
    {
        const string VoicesUrl = "https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/voices/list?trustedclienttoken=6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        const string CachePath = "Library/EdgeTtsVoices.json";
        static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

        static List<VoiceInfo> voices;
        static HashSet<string> shortNames;
        static bool attempted;

        public static bool IsLoaded => voices != null;
        public static int Count => voices?.Count ?? 0;
        public static string LastError { get; private set; }
        public static IReadOnlyList<VoiceInfo> Voices
        {
            get
            {
                EnsureLoaded();
                return voices;
            }
        }

        public static bool Contains(string shortName)
        {
            EnsureLoaded();
            return shortName != null && shortNames != null && shortNames.Contains(shortName);
        }

        public static VoiceInfo Find(string shortName)
        {
            EnsureLoaded();
            if (voices == null || string.IsNullOrEmpty(shortName))
                return null;
            for (int i = 0; i < voices.Count; i++)
                if (voices[i].ShortName == shortName)
                    return voices[i];
            return null;
        }

        public static void EnsureLoaded()
        {
            if (IsLoaded || attempted)
                return;
            attempted = true;
            if (File.Exists(CachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(CachePath) <= MaxAge)
            {
                if (TryLoad())
                    return;
            }
            Refresh();
        }

        public static bool Refresh()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(VoicesUrl);
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0";
                request.Timeout = 15000;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var file = File.Create(CachePath))
                    stream.CopyTo(file);
                LastError = null;
                return TryLoad();
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[Voice] voice list fetch failed: " + e.Message);
                if (!IsLoaded && File.Exists(CachePath))
                    return TryLoad();
                if (!IsLoaded)
                    voices = new List<VoiceInfo>();
                return false;
            }
        }

        static bool TryLoad()
        {
            try
            {
                string raw = File.ReadAllText(CachePath, Encoding.UTF8).Trim();
                var wrapper = JsonUtility.FromJson<VoiceListWrapper>("{\"voices\":" + raw + "}");
                voices = new List<VoiceInfo>(wrapper.voices);
                voices.Sort((a, b) =>
                {
                    int c = string.Compare(a.Locale, b.Locale, StringComparison.OrdinalIgnoreCase);
                    return c != 0 ? c : string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase);
                });
                shortNames = new HashSet<string>();
                foreach (VoiceInfo v in voices)
                    if (!string.IsNullOrEmpty(v.ShortName))
                        shortNames.Add(v.ShortName);
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[Voice] voice list parse failed: " + e.Message);
                voices = null;
                shortNames = null;
                return false;
            }
        }

        [Serializable]
        class VoiceListWrapper
        {
            public VoiceInfo[] voices;
        }
    }
}
