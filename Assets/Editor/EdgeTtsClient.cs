using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue.Editor
{
    public static class EdgeTtsClient
    {
        const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        const string WssUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
        const string VoiceListUrl = "https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/voices/list?trustedclienttoken=" + TrustedClientToken;
        const string ChromiumFullVersion = "143.0.3650.75";
        const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0";
        const string EdgeExtensionOrigin = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold";

        static readonly Regex ShortVoicePattern = new Regex(@"^([a-z]{2,})-([A-Z]{2,})-(.+Neural)$", RegexOptions.Compiled);
        static readonly Regex LongVoicePattern = new Regex(@"^Microsoft Server Speech Text to Speech Voice \(.+,.+\)$", RegexOptions.Compiled);
        static readonly Regex RatePattern = new Regex(@"^[+-]\d+%$", RegexOptions.Compiled);
        static readonly Regex PitchPattern = new Regex(@"^[+-]\d+Hz$", RegexOptions.Compiled);

        static double clockSkewSeconds;

        public static double ClockSkewSeconds => clockSkewSeconds;

        public static bool IsValidRate(string rate)
        {
            return !string.IsNullOrEmpty(rate) && RatePattern.IsMatch(rate.Trim());
        }

        public static bool IsValidPitch(string pitch)
        {
            return !string.IsNullOrEmpty(pitch) && PitchPattern.IsMatch(pitch.Trim());
        }

        public static bool TryToLongVoice(string voice, out string longVoice, out string error)
        {
            longVoice = null;
            error = null;
            if (string.IsNullOrWhiteSpace(voice))
            {
                error = "voice name is empty";
                return false;
            }
            string v = voice.Trim();
            if (LongVoicePattern.IsMatch(v))
            {
                longVoice = v;
                return true;
            }
            Match m = ShortVoicePattern.Match(v);
            if (!m.Success)
            {
                error = "'" + v + "' is not a valid edge-tts voice (expected e.g. vi-VN-NamMinhNeural)";
                return false;
            }
            string lang = m.Groups[1].Value;
            string region = m.Groups[2].Value;
            string name = m.Groups[3].Value;
            int dash = name.IndexOf('-');
            if (dash >= 0)
            {
                region += "-" + name.Substring(0, dash);
                name = name.Substring(dash + 1);
            }
            string candidate = "Microsoft Server Speech Text to Speech Voice (" + lang + "-" + region + ", " + name + ")";
            if (!LongVoicePattern.IsMatch(candidate))
            {
                error = "voice '" + v + "' converts to invalid long name '" + candidate + "'";
                return false;
            }
            longVoice = candidate;
            return true;
        }

        public static async Task PrefetchSkewAsync()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(VoiceListUrl);
                request.Method = "GET";
                request.UserAgent = UserAgent;
                request.Timeout = 8000;
                request.ReadWriteTimeout = 8000;
                using (WebResponse response = await request.GetResponseAsync())
                {
                    string dateHeader = ((HttpWebResponse)response).Headers[HttpResponseHeader.Date];
                    if (!string.IsNullOrEmpty(dateHeader) &&
                        DateTime.TryParse(dateHeader, CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                            out DateTime serverDate))
                    {
                        clockSkewSeconds = (serverDate - DateTime.UtcNow).TotalSeconds;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Voice] clock skew sync failed: " + e.Message);
            }
        }

        public static async Task<byte[]> SynthesizeAsync(string text, string voice, string rate, string volume, string pitch, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Line text is empty");
            if (!TryToLongVoice(voice, out string longVoice, out string voiceError))
                throw new ArgumentException(voiceError);

            string sanitized = SanitizeControl(text);
            if (Encoding.UTF8.GetByteCount(sanitized) > 4096)
                throw new ArgumentException("Line text exceeds 4096 UTF-8 bytes: " + sanitized.Substring(0, Math.Min(60, sanitized.Length)));

            string ssml = BuildSsml(longVoice, XmlEscape(sanitized), rate, volume, pitch);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    return await SynthesizeOnceAsync(ssml, ct);
                }
                catch (WebSocketException) when (attempt == 0 && !ct.IsCancellationRequested)
                {
                    await PrefetchSkewAsync();
                }
            }
            throw new InvalidOperationException("unreachable");
        }

        static async Task<byte[]> SynthesizeOnceAsync(string ssml, CancellationToken ct)
        {
            string connectionId = Guid.NewGuid().ToString("N");
            string url = WssUrl
                + "?TrustedClientToken=" + TrustedClientToken
                + "&ConnectionId=" + connectionId
                + "&Sec-MS-GEC=" + GenerateSecMsGec()
                + "&Sec-MS-GEC-Version=1-" + ChromiumFullVersion;

            using (var socket = new ClientWebSocket())
            {
                TrySetHeader(socket, "Pragma", "no-cache");
                TrySetHeader(socket, "Cache-Control", "no-cache");
                TrySetHeader(socket, "Origin", EdgeExtensionOrigin);
                TrySetHeader(socket, "User-Agent", UserAgent);
                TrySetHeader(socket, "Accept-Encoding", "gzip, deflate, br, zstd");
                TrySetHeader(socket, "Accept-Language", "en-US,en;q=0.9");
                TrySetHeader(socket, "Cookie", "muid=" + Guid.NewGuid().ToString("N").ToUpperInvariant());

                using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    connectCts.CancelAfter(TimeSpan.FromSeconds(10));
                    try
                    {
                        await socket.ConnectAsync(new Uri(url), connectCts.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        throw new TimeoutException("edge-tts connect timed out (10s)");
                    }
                }

                try
                {
                    await SendTextAsync(socket, BuildConfigMessage(), ct);
                    await SendTextAsync(socket, BuildSsmlMessage(ssml), ct);

                    using (var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        receiveCts.CancelAfter(TimeSpan.FromSeconds(60));
                        CancellationToken receiveToken = receiveCts.Token;
                        byte[] buffer = new byte[16 * 1024];

                        using (var audio = new MemoryStream())
                        {
                            while (true)
                            {
                                WebSocketReceiveResult result;
                                using (var message = new MemoryStream())
                                {
                                    do
                                    {
                                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), receiveToken);
                                        if (result.MessageType == WebSocketMessageType.Close)
                                            throw new InvalidOperationException("edge-tts closed the connection: " + socket.CloseStatusDescription);
                                        message.Write(buffer, 0, result.Count);
                                    } while (!result.EndOfMessage);

                                    byte[] data = message.ToArray();

                                    if (result.MessageType == WebSocketMessageType.Text)
                                    {
                                        string headers = DecodeTextHeaders(data);
                                        string path = GetHeaderValue(headers, "Path");
                                        if (path == "turn.end")
                                        {
                                            if (audio.Length == 0)
                                                throw new InvalidOperationException("No audio received (turn.end with empty body)");
                                            return audio.ToArray();
                                        }
                                        if (path == null)
                                            Debug.LogWarning("[Voice] edge-tts text message without Path header");
                                    }
                                    else if (result.MessageType == WebSocketMessageType.Binary)
                                    {
                                        if (data.Length < 2)
                                            continue;
                                        int headerLength = (data[0] << 8) | data[1];
                                        int bodyStart = 2 + headerLength;
                                        string headers = Encoding.ASCII.GetString(data, 0, Math.Min(bodyStart, data.Length));
                                        string path = GetHeaderValue(headers, "Path");
                                        if (path == "audio")
                                        {
                                            if (bodyStart < data.Length)
                                                audio.Write(data, bodyStart, data.Length - bodyStart);
                                        }
                                        else if (path != null && path != "turn.start" && path != "turn.end"
                                                 && path != "audio.metadata" && path != "response")
                                        {
                                            Debug.LogWarning("[Voice] edge-tts binary message with Path=" + path);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new TimeoutException("edge-tts receive timed out (60s)");
                }
                finally
                {
                    try { socket.Abort(); } catch { }
                }
            }
        }

        static string GenerateSecMsGec()
        {
            double unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + clockSkewSeconds;
            double ticks = unix + 11644473600.0;
            ticks -= ticks % 300.0;
            ticks *= 10000000.0;
            string ticksString = Math.Round(ticks, MidpointRounding.ToEven).ToString("F0", CultureInfo.InvariantCulture);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(ticksString + TrustedClientToken));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) builder.Append(b.ToString("X2"));
                return builder.ToString();
            }
        }

        static void TrySetHeader(ClientWebSocket socket, string name, string value)
        {
            try
            {
                socket.Options.SetRequestHeader(name, value);
            }
            catch
            {
            }
        }

        static Task SendTextAsync(ClientWebSocket socket, string message, CancellationToken ct)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        static string Timestamp()
        {
            return DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", CultureInfo.InvariantCulture);
        }

        static string BuildConfigMessage()
        {
            return "X-Timestamp:" + Timestamp() + "\r\n"
                + "Content-Type:application/json; charset=utf-8\r\n"
                + "Path:speech.config\r\n\r\n"
                + "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"true\",\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}\r\n";
        }

        static string BuildSsmlMessage(string ssml)
        {
            return "X-RequestId:" + Guid.NewGuid().ToString("N") + "\r\n"
                + "Content-Type:application/ssml+xml\r\n"
                + "X-Timestamp:" + Timestamp() + "Z\r\n"
                + "Path:ssml\r\n\r\n"
                + ssml;
        }

        static string BuildSsml(string longVoice, string escapedText, string rate, string volume, string pitch)
        {
            return "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>"
                + "<voice name='" + longVoice + "'>"
                + "<prosody pitch='" + pitch + "' rate='" + rate + "' volume='" + volume + "'>"
                + escapedText
                + "</prosody></voice></speak>";
        }

        static string SanitizeControl(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
                builder.Append(c < 32 && c != '\t' && c != '\n' && c != '\r' ? ' ' : c);
            return builder.ToString();
        }

        static string XmlEscape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        static string DecodeTextHeaders(byte[] data)
        {
            for (int i = 0; i + 3 < data.Length; i++)
            {
                if (data[i] == (byte)'\r' && data[i + 1] == (byte)'\n' && data[i + 2] == (byte)'\r' && data[i + 3] == (byte)'\n')
                    return Encoding.UTF8.GetString(data, 0, i);
            }
            return Encoding.UTF8.GetString(data, 0, data.Length);
        }

        static string GetHeaderValue(string headers, string name)
        {
            if (string.IsNullOrEmpty(headers))
                return null;
            string[] lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                int colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;
                if (line.Substring(0, colon).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(colon + 1).Trim();
            }
            return null;
        }
    }
}
