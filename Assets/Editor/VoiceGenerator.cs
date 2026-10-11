using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue.Editor
{
    public class VoiceJob
    {
        public string Speaker;
        public string Folder;
        public string Voice;
        public string BaseRate;
        public string LineRate;
        public string LineVolume;
        public string Text;
        public string OutputPath;
    }

    public class VoiceGenProgress
    {
        public int Total;
        public int Voiced;
        public string OnlyFolder;
        public bool Force;
        public string Unknown;
        public CancellationTokenSource Cts;

        public int Done;
        public int Made;
        public int Skipped;
        public int Failed;
        public int Cancelled;

        public volatile string CurrentLabel = "";
        public volatile bool CancelRequested;
        public volatile bool Finished;

        public readonly List<string> Failures = new List<string>();

        public float Fraction
        {
            get
            {
                int done = Volatile.Read(ref Done);
                return Total <= 0 ? 0f : Mathf.Clamp01((float)done / Total);
            }
        }

        public void AddDone()
        {
            Interlocked.Increment(ref Done);
        }

        public string BuildSummary()
        {
            return "[Voice] done: "
                + Volatile.Read(ref Made) + " new, "
                + Volatile.Read(ref Skipped) + " already existed, "
                + Volatile.Read(ref Failed) + " failed, "
                + Volatile.Read(ref Cancelled) + " cancelled - "
                + Total + " unique clips (" + Voiced + " voiced lines, filter=" + (OnlyFolder ?? "all")
                + ", force=" + Force + ")";
        }
    }

    public static class VoiceGenerator
    {
        public const string NarratorSpeaker = "Dẫn chuyện";

        static readonly Regex RatePattern = new Regex(@"^[+-]\d+%$", RegexOptions.Compiled);

        static readonly (string Speaker, string Text)[] ExtraLines =
        {
            (NarratorSpeaker, "Giữa thung lũng đá, Voi Chín Ngà đứng chắn lối. Mỗi nhịp thở của linh tượng làm mặt đất khẽ rung chuyển."),
            (NarratorSpeaker, "Chín chiếc ngà rực sáng. Linh tượng hạ thấp thân mình, sẵn sàng tung ra những đòn rung núi chuyển rừng."),
            ("Voi Chín Ngà", "Kẻ phàm nào dám bước vào lãnh địa của ta?"),
            ("Sơn Tinh", "Ta là Sơn Tinh. Ta đến tìm Voi Chín Ngà làm sính lễ dâng Vua Hùng."),
            ("Voi Chín Ngà", "Sính lễ không dành cho kẻ chỉ biết khoe sức. Hãy chứng minh ngươi đủ bản lĩnh bảo vệ Mị Nương."),
            ("Sơn Tinh", "Ta chấp nhận thử thách. Xin hãy xuất chiêu!"),
        };

        public static string Validate(VoiceLibrary lib, string onlyFolder)
        {
            if (lib == null)
                return "No VoiceLibrary asset (Tools > Create Voice Library).";
            if (lib.entries == null || lib.entries.Length == 0)
                return "VoiceLibrary has no entries.";

            var problems = new List<string>();
            foreach (VoiceLibrary.Entry entry in lib.entries)
            {
                if (entry == null)
                    continue;
                if (onlyFolder != null && entry.folder != onlyFolder)
                    continue;
                string who = string.IsNullOrEmpty(entry.speaker) ? "(unnamed entry)" : "\"" + entry.speaker + "\"";
                if (string.IsNullOrWhiteSpace(entry.speaker))
                    problems.Add("An entry has an empty speaker name.");
                else if (string.IsNullOrWhiteSpace(entry.folder))
                    problems.Add(who + ": folder is empty.");
                else
                {
                    if (!EdgeTtsClient.TryToLongVoice(entry.voice, out _, out string voiceError))
                        problems.Add(who + ": " + voiceError);
                    if (!EdgeTtsClient.IsValidRate(entry.rate))
                        problems.Add(who + ": rate \"" + entry.rate + "\" must look like +10% or -8%.");
                }
            }
            return problems.Count == 0 ? null : string.Join("\n", problems);
        }

        public static bool TryBuildJobs(VoiceLibrary lib, string onlyFolder, out List<VoiceJob> jobs,
            out int voiced, out string unknownText, out string error)
        {
            jobs = new List<VoiceJob>();
            voiced = 0;
            unknownText = null;
            error = null;

            try
            {
                var bySpeaker = new Dictionary<string, VoiceLibrary.Entry>();
                if (lib?.entries != null)
                {
                    foreach (VoiceLibrary.Entry entry in lib.entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.speaker) || bySpeaker.ContainsKey(entry.speaker))
                            continue;
                        bySpeaker[entry.speaker] = entry;
                    }
                }

                var unknown = new Dictionary<string, int>();
                var pairs = new List<(string Speaker, string Text)>();

                string dialogueDir = Path.Combine(Application.dataPath, "Data", "Dialogue");
                if (!Directory.Exists(dialogueDir))
                {
                    error = "Dialogue folder not found: " + dialogueDir;
                    return false;
                }
                string[] files = Directory.GetFiles(dialogueDir, "*.asset", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.Ordinal);
                foreach (string file in files)
                {
                    DialogueSequence sequence = AssetDatabase.LoadAssetAtPath<DialogueSequence>(
                        "Assets/Data/Dialogue/" + Path.GetFileName(file));
                    if (sequence?.Lines == null)
                        continue;
                    foreach (DialogueLine line in sequence.Lines)
                    {
                        if (line == null)
                            continue;
                        string speaker = (line.speaker ?? "").Trim();
                        string text = (line.text ?? "").Trim();
                        if (text.Length == 0)
                            continue;
                        if (speaker.Length == 0)
                            speaker = NarratorSpeaker;
                        if (!bySpeaker.ContainsKey(speaker))
                        {
                            AddUnknown(unknown, speaker);
                            continue;
                        }
                        voiced++;
                        pairs.Add((speaker, text));
                    }
                }

                foreach ((string speaker, string text) in ExtraLines)
                {
                    if (!bySpeaker.ContainsKey(speaker))
                    {
                        AddUnknown(unknown, speaker);
                        continue;
                    }
                    voiced++;
                    pairs.Add((speaker, text));
                }

                var seen = new HashSet<(string, string)>();
                var unique = new List<(string Speaker, string Text)>();
                foreach ((string speaker, string text) in pairs)
                {
                    if (seen.Add((speaker, text)))
                        unique.Add((speaker, text));
                }
                if (onlyFolder != null)
                    unique = unique.Where(p => bySpeaker[p.Speaker].folder == onlyFolder).ToList();

                string voiceRoot = Path.Combine(Application.dataPath, "Resources", "Audio", "Voice");
                foreach ((string speaker, string text) in unique)
                {
                    VoiceLibrary.Entry entry = bySpeaker[speaker];
                    (string lineRate, string lineVolume) = LineProsody(entry.rate, text);
                    jobs.Add(new VoiceJob
                    {
                        Speaker = speaker,
                        Folder = entry.folder,
                        Voice = entry.voice,
                        BaseRate = entry.rate,
                        LineRate = lineRate,
                        LineVolume = lineVolume,
                        Text = text,
                        OutputPath = Path.Combine(voiceRoot, entry.folder, ClipHash(text) + ".mp3"),
                    });
                }

                if (unknown.Count > 0)
                    unknownText = string.Join(", ", unknown.Select(kv => kv.Key + " (" + kv.Value + ")"));
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        public static async Task RunAsync(VoiceGenProgress progress, List<VoiceJob> jobs)
        {
            CancellationToken ct = progress.Cts.Token;
            try
            {
                await EdgeTtsClient.PrefetchSkewAsync();
                using (var semaphore = new SemaphoreSlim(3))
                {
                    var tasks = new List<Task>(jobs.Count);
                    foreach (VoiceJob job in jobs)
                        tasks.Add(Worker(semaphore, job, progress, ct));
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                progress.Finished = true;
            }
        }

        static async Task Worker(SemaphoreSlim semaphore, VoiceJob job, VoiceGenProgress progress, CancellationToken ct)
        {
            bool acquired = false;
            try
            {
                if (progress.CancelRequested || ct.IsCancellationRequested)
                {
                    CountCancelled(progress);
                    return;
                }
                if (!progress.Force && File.Exists(job.OutputPath) && new FileInfo(job.OutputPath).Length > 2000)
                {
                    Interlocked.Increment(ref progress.Skipped);
                    progress.AddDone();
                    return;
                }

                await semaphore.WaitAsync(ct);
                acquired = true;
                progress.CurrentLabel = job.Speaker + ": " + Truncate(job.Text, 48);

                for (int attempt = 0; attempt < 4; attempt++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        CountCancelled(progress);
                        return;
                    }
                    try
                    {
                        byte[] mp3 = await EdgeTtsClient.SynthesizeAsync(
                            job.Text, job.Voice, job.LineRate, job.LineVolume, "+0Hz", ct);
                        Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath));
                        File.WriteAllBytes(job.OutputPath, mp3);
                        Interlocked.Increment(ref progress.Made);
                        progress.AddDone();
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        CountCancelled(progress);
                        return;
                    }
                    catch (ArgumentException e)
                    {
                        RecordFailure(progress, job, e.Message);
                        return;
                    }
                    catch (Exception e)
                    {
                        if (attempt == 3)
                        {
                            RecordFailure(progress, job, e.Message);
                            return;
                        }
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1.5 * (attempt + 1)), ct);
                        }
                        catch (OperationCanceledException)
                        {
                            CountCancelled(progress);
                            return;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                CountCancelled(progress);
            }
            finally
            {
                if (acquired)
                    semaphore.Release();
            }
        }

        static void CountCancelled(VoiceGenProgress progress)
        {
            Interlocked.Increment(ref progress.Cancelled);
            progress.AddDone();
        }

        static void RecordFailure(VoiceGenProgress progress, VoiceJob job, string message)
        {
            string line = "[" + job.Folder + "] " + Truncate(job.Text, 48) + ": " + message;
            lock (progress.Failures)
                progress.Failures.Add(line);
            Interlocked.Increment(ref progress.Failed);
            progress.AddDone();
            Debug.LogWarning("[Voice] FAIL " + line);
        }

        static void AddUnknown(Dictionary<string, int> unknown, string speaker)
        {
            unknown.TryGetValue(speaker, out int count);
            unknown[speaker] = count + 1;
        }

        public static (string Rate, string Volume) LineProsody(string baseRate, string text)
        {
            string hex = Sha1Hex(text).Substring(0, 4);
            int jitter = (int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture) % 7) - 3;
            int rate = int.Parse(baseRate.TrimEnd('%'), CultureInfo.InvariantCulture) + jitter;
            string trimmed = text.TrimEnd();
            if (trimmed.EndsWith("?"))
                rate += 4;
            else if (trimmed.EndsWith("!"))
                rate += 2;
            rate = Mathf.Clamp(rate, -40, 60);
            string volume = trimmed.EndsWith("!") ? "+10%" : "+0%";
            string sign = rate >= 0 ? "+" : "";
            return (sign + rate + "%", volume);
        }

        public static string ClipHash(string text)
        {
            return Sha1Hex(text).Substring(0, 16);
        }

        static string Sha1Hex(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text;
            return text.Substring(0, max) + "\u2026";
        }
    }
}
