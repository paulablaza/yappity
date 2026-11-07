using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Yappity
{
    public sealed class Settings
    {
        public string ApiKeyProtected { get; set; }
        public int ShortcutVk { get; set; }
        public int ShortcutMods { get; set; }
        public string Mode { get; set; }          // "toggle" or "hold"
        public string Output { get; set; }        // "paste" (default), "type" or "history"
        public string SpeechModel { get; set; }
        public string CleanupModel { get; set; }  // empty means no cleanup
        public string Language { get; set; }      // empty = auto, "mixed-tl" = English + Tagalog, or a code
        public string Vocabulary { get; set; }
        public string Bubble { get; set; }        // where the "Yap away" bubble shows up
        public string Style { get; set; }         // "clean" or "format" (lists and paragraphs)

        public string Theme { get; set; }         // "system", "light" or "dark"
        public int KeepAudioDays { get; set; }    // how long recordings stay in History, 0 = delete after it works

        public Settings()
        {
            Theme = "system";
            KeepAudioDays = 7;
            Bubble = "top";
            Style = "clean";
            ShortcutVk = 0xA3; // Right Ctrl: tap to start and stop, or hold in Hold mode
            ShortcutMods = 0;
            Mode = "toggle";
            Output = "paste";
            SpeechModel = Groq.SpeechModels[0].Id;
            CleanupModel = Groq.CleanupModels[0].Id;
            Language = "";
            Vocabulary = "";
        }

        [ScriptIgnore]
        public bool HoldMode { get { return Mode == "hold"; } }

        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Yappity api key v1");

        // The key is encrypted with Windows DPAPI, so only this Windows user on this
        // PC can read it back.
        [ScriptIgnore]
        public string ApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(ApiKeyProtected)) return "";
                try
                {
                    byte[] plain = ProtectedData.Unprotect(Convert.FromBase64String(ApiKeyProtected), Entropy, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(plain);
                }
                catch (Exception) { return ""; }
            }
            set
            {
                string v = (value ?? "").Trim();
                if (v.Length == 0) { ApiKeyProtected = ""; return; }
                byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(v), Entropy, DataProtectionScope.CurrentUser);
                ApiKeyProtected = Convert.ToBase64String(enc);
            }
        }
    }

    public sealed class HistoryEntry
    {
        public string Id { get; set; }
        public DateTime TimeUtc { get; set; }
        public string Raw { get; set; }
        public string Text { get; set; }
        public bool Failed { get; set; }
        public string Error { get; set; }
        public string AudioFile { get; set; }
        public double Seconds { get; set; }
    }

    internal static class Store
    {
        public const int MaxHistory = 200;

        // YAPPITY_HOME lets you keep data somewhere else (portable use, testing).
        // Where settings, history and recordings live:
        //   YAPPITY_HOME if it's set (testing),
        //   a YappityData folder next to the .exe if a "Yappity.portable" file sits beside it,
        //   otherwise %APPDATA%\Yappity.
        public static readonly string Dir = PickDir();

        static string PickDir()
        {
            string env = Environment.GetEnvironmentVariable("YAPPITY_HOME");
            if (!string.IsNullOrEmpty(env)) return env;
            try
            {
                string exeDir = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                if (File.Exists(Path.Combine(exeDir, "Yappity.portable"))) return Path.Combine(exeDir, "YappityData");
            }
            catch (Exception) { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Yappity");
        }

        public static bool IsPortable { get { return !Dir.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("YAPPITY_HOME")); } }
        public static readonly string AudioDir = Path.Combine(Dir, "recordings");
        static readonly string SettingsFile = Path.Combine(Dir, "settings.json");
        static readonly string HistoryFile = Path.Combine(Dir, "history.json");
        static readonly string LogFile = Path.Combine(Dir, "log.txt");

        static Store()
        {
            Directory.CreateDirectory(Dir);
        }

        public static Settings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                    return Json.Deserialize<Settings>(File.ReadAllText(SettingsFile)) ?? new Settings();
            }
            catch (Exception e) { Log("Settings unreadable, using defaults: " + e.Message); }
            return new Settings();
        }

        public static void SaveSettings(Settings s) { WriteAtomic(SettingsFile, Json.Serialize(s)); }

        public static List<HistoryEntry> LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryFile))
                    return Json.Deserialize<List<HistoryEntry>>(File.ReadAllText(HistoryFile)) ?? new List<HistoryEntry>();
            }
            catch (Exception e) { Log("History unreadable, starting fresh: " + e.Message); }
            return new List<HistoryEntry>();
        }

        public static void SaveHistory(List<HistoryEntry> items)
        {
            while (items.Count > MaxHistory)
            {
                DeleteAudio(items[items.Count - 1]);
                items.RemoveAt(items.Count - 1);
            }
            WriteAtomic(HistoryFile, Json.Serialize(items));
        }

        public static string SaveAudio(string id, byte[] wav)
        {
            Directory.CreateDirectory(AudioDir);
            string path = Path.Combine(AudioDir, id + ".wav");
            File.WriteAllBytes(path, wav);
            return path;
        }

        public static void DeleteAudio(HistoryEntry e)
        {
            if (string.IsNullOrEmpty(e.AudioFile)) return;
            try { if (File.Exists(e.AudioFile)) File.Delete(e.AudioFile); } catch (Exception) { }
            e.AudioFile = null;
        }

        public static void Log(string message)
        {
            try
            {
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 512 * 1024) File.Delete(LogFile);
                File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch (Exception) { }
        }

        static void WriteAtomic(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        // ---- Start with Windows ----
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool StartsWithWindows
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("Yappity") != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue("Yappity", "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                    else if (k.GetValue("Yappity") != null) k.DeleteValue("Yappity");
                }
            }
        }
    }
}
