using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Yappity
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
            if (args.Length >= 3 && args[0] == "--bubble-test")
                return TestBubble(args[1], args[2]);
            if (args.Length >= 3 && args[0] == "--test")
                return TestFile(args[1], args[2]);
            if (args.Length >= 3 && args[0] == "--record-test")
                return TestRecord(double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), args[2]);

            bool first;
            using (var mutex = new Mutex(true, @"Local\Yappity-single-instance", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Yappity is already running. Check your tray, it's the mic icon.",
                        "Yappity", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show("Yappity couldn't start: " + e.Message, "Yappity", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
            return 0;
        }

        // Yappity.exe --test input.wav output.txt
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
                string speechModel = Environment.GetEnvironmentVariable("YAPPITY_SPEECH") ?? s.SpeechModel;
                string cleanModel = Environment.GetEnvironmentVariable("YAPPITY_CLEANUP") ?? s.CleanupModel;
                string lang = Environment.GetEnvironmentVariable("YAPPITY_LANG") ?? s.Language;
                int chunk;
                if (!int.TryParse(Environment.GetEnvironmentVariable("YAPPITY_CHUNK_SECONDS"), out chunk)) chunk = 600;
                int parts = 0;
                string raw = App.TranscribeLong(key, speechModel, wav, lang, s.Vocabulary, chunk,
                    delegate(int part, int total) { parts = total; }).GetAwaiter().GetResult();
                long whisperMs = sw.ElapsedMilliseconds;
                sw.Restart();
                string clean = cleanModel.Length > 0 ? Groq.Cleanup(key, cleanModel, raw).GetAwaiter().GetResult() : raw;
                long cleanupMs = sw.ElapsedMilliseconds;
                File.WriteAllText(outPath,
                    speechModel + " + " + cleanModel + "  (parts: " + Math.Max(1, parts) + ")" + Environment.NewLine + "RAW (" + whisperMs + " ms): " + raw + Environment.NewLine +
                    "CLEAN (" + cleanupMs + " ms): " + clean + Environment.NewLine + "LEFT TODAY: " + Groq.Remaining(speechModel) + " recordings, " + Groq.Remaining(cleanModel) + " cleanups" + Environment.NewLine, Encoding.UTF8);
                return 0;
            }
            catch (Exception e)
            {
                File.WriteAllText(outPath, "ERROR: " + e.Message + Environment.NewLine, Encoding.UTF8);
                return 1;
            }
        }

        // Yappity.exe --bubble-test top|top-right|bottom... output.png
        // Shows the listening bubble for a moment and saves a screenshot of that screen area.
        static int TestBubble(string position, string outPath)
        {
            Application.EnableVisualStyles();
            var overlay = new Overlay();
            overlay.Position = position;
            overlay.ShowListening();
            var rnd = new Random(1);
            var until = DateTime.UtcNow.AddMilliseconds(1600);
            while (DateTime.UtcNow < until)
            {
                overlay.PushLevel((float)rnd.NextDouble() * 0.9f);
                Application.DoEvents();
                Thread.Sleep(30);
            }
            var b = overlay.Bounds;
            b.Inflate(60, 30);
            using (var bmp = new System.Drawing.Bitmap(b.Width, b.Height))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(b.Location, System.Drawing.Point.Empty, b.Size);
                bmp.Save(outPath);
            }
            File.WriteAllText(outPath + ".txt", "bounds=" + overlay.Bounds + " screen=" + Screen.FromPoint(Cursor.Position).WorkingArea);
            overlay.Close();
            return 0;
        }

        // Yappity.exe --record-test seconds output.wav
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
