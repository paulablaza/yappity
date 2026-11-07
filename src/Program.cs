using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Saywrite
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // Developer commands, used by build.ps1 and the test script.
            if (args.Length >= 2 && args[0] == "--write-icon")
            {
                Icons.WriteIco(args[1]);
                return 0;
            }
            if (args.Length >= 3 && args[0] == "--test")
                return TestFile(args[1], args[2]);
            if (args.Length >= 3 && args[0] == "--record-test")
                return TestRecord(double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), args[2]);

            bool first;
            using (var mutex = new Mutex(true, @"Local\Saywrite-single-instance", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Saywrite is already running. Look for the microphone icon in the system tray.",
                        "Saywrite", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Store.Log("UI error: " + e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Store.Log("Crash: " + e.ExceptionObject); };

                try
                {
                    var app = new App();
                    if (Array.IndexOf(args, "--history") >= 0) app.ShowWindow(false);
                    if (Array.IndexOf(args, "--settings") >= 0) app.ShowWindow(true);
                    Application.Run(app);
                }
                catch (Exception e)
                {
                    Store.Log("Startup failed: " + e);
                    MessageBox.Show("Saywrite could not start: " + e.Message, "Saywrite", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
            return 0;
        }

        // Saywrite.exe --test input.wav output.txt
        // Sends a WAV file through the same Whisper + cleanup path the app uses.
        static int TestFile(string wavPath, string outPath)
        {
            try
            {
                string key = Environment.GetEnvironmentVariable("GROQ_API_KEY");
                if (string.IsNullOrEmpty(key)) key = Store.LoadSettings().ApiKey;
                var s = Store.LoadSettings();
                byte[] wav = File.ReadAllBytes(wavPath);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                string raw = Groq.Transcribe(key, wav, s.Language, s.Vocabulary).GetAwaiter().GetResult();
                long whisperMs = sw.ElapsedMilliseconds;
                sw.Restart();
                string clean = Groq.Cleanup(key, raw).GetAwaiter().GetResult();
                long cleanupMs = sw.ElapsedMilliseconds;
                File.WriteAllText(outPath,
                    "RAW (" + whisperMs + " ms): " + raw + Environment.NewLine +
                    "CLEAN (" + cleanupMs + " ms): " + clean + Environment.NewLine, Encoding.UTF8);
                return 0;
            }
            catch (Exception e)
            {
                File.WriteAllText(outPath, "ERROR: " + e.Message + Environment.NewLine, Encoding.UTF8);
                return 1;
            }
        }

        // Saywrite.exe --record-test seconds output.wav
        static int TestRecord(double seconds, string outPath)
        {
            try
            {
                var r = new Recorder();
                int updates = 0;
                r.LevelChanged = delegate { Interlocked.Increment(ref updates); };
                r.Start();
                Thread.Sleep((int)(seconds * 1000));
                byte[] pcm = r.Stop();
                File.WriteAllBytes(outPath, Recorder.ToWav(pcm));
                File.WriteAllText(outPath + ".txt", "bytes=" + pcm.Length + " seconds=" +
                    (pcm.Length / (double)Recorder.BytesPerSecond).ToString("0.00") + " peak=" + Recorder.Peak(pcm) +
                    " levelUpdates=" + updates + Environment.NewLine);
                return 0;
            }
            catch (Exception e)
            {
                File.WriteAllText(outPath + ".txt", "ERROR: " + e.Message + Environment.NewLine);
                return 1;
            }
        }
    }
}
