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
            if (args.Length >= 3 && args[0] == "--clean-test")
            {
                // Each line of the input file goes through cleanup on its own.
                string key = Environment.GetEnvironmentVariable("GROQ_API_KEY");
                string model = Environment.GetEnvironmentVariable("YAPPITY_CLEANUP") ?? Groq.CleanupModels[0].Id;
                var sb = new StringBuilder();
                foreach (string line in File.ReadAllLines(args[1], Encoding.UTF8))
                {
                    if (line.Trim().Length == 0) continue;
                    string outText;
                    try { outText = Groq.Cleanup(key, model, line).GetAwaiter().GetResult(); }
                    catch (Exception e) { outText = "ERROR: " + e.Message; }
                    sb.AppendLine("IN:  " + line).AppendLine("OUT: " + outText).AppendLine();
                }
                File.WriteAllText(args[2], sb.ToString(), Encoding.UTF8);
                return 0;
            }
            if (args.Length >= 2 && args[0] == "--tray-icons")
            {
                // Saves the idle, recording and working tray icons side by side.
                var colors = new[] { System.Drawing.Color.Empty, System.Drawing.Color.FromArgb(255, 69, 58), System.Drawing.Color.FromArgb(251, 191, 36) };
                using (var sheet = new System.Drawing.Bitmap(3 * 72, 72))
                {
                    using (var g = System.Drawing.Graphics.FromImage(sheet))
                    {
                        g.Clear(System.Drawing.Color.FromArgb(32, 32, 36));
                        for (int i = 0; i < 3; i++)
                            using (var ico = Icons.Tray(64, colors[i])) g.DrawIcon(ico, new System.Drawing.Rectangle(i * 72 + 4, 4, 64, 64));
                    }
                    sheet.Save(args[1]);
                }
                return 0;
            }
            if (args.Length >= 3 && args[0] == "--bubble-test")
                return TestBubble(args[1], args[2]);
            if (args.Length >= 3 && args[0] == "--test")
                return TestFile(args[1], args[2]);
            if (args.Length >= 3 && args[0] == "--record-test")
                return TestRecord(double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), args[2]);

            bool first;
            // One copy per data folder, so a test copy with YAPPITY_HOME can run next to the real one.
            string instance = @"Local\Yappity-" + Store.Dir.ToLowerInvariant().GetHashCode().ToString("x");
            using (var mutex = new Mutex(true, instance, out first))
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
                    // WPF runs the message loop; the tray icon and keyboard hook live on the same thread.
                    var wpf = new System.Windows.Application();
                    wpf.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                    wpf.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                    {
                        Store.Log("UI error: " + e.Exception);
                        e.Handled = true;
                    };
                    wpf.Startup += delegate
                    {
                        var app = new App();
                        if (Array.IndexOf(args, "--history") >= 0) app.ShowWindow(false);
                        if (Array.IndexOf(args, "--settings") >= 0) app.ShowWindow(true);
                    };
                    wpf.Run();
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
        // Captures the bubble in each state: listening, working and done.
        static int TestBubble(string position, string outPrefix)
        {
            var wpf = new System.Windows.Application();
            wpf.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            wpf.Startup += async delegate
            {
                K.Load();
                var bubble = new Bubble();
                bubble.Position = position;
                var rnd = new Random(1);
                var levels = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                levels.Tick += delegate { bubble.PushLevel((float)(0.2 + rnd.NextDouble() * 0.7)); };
                levels.Start();
                bubble.ShowListening();
                await System.Threading.Tasks.Task.Delay(1500);
                Snap(bubble, outPrefix + "-listening.png");
                levels.Stop();
                bubble.ShowWorking("Tidying up");
                await System.Threading.Tasks.Task.Delay(900);
                Snap(bubble, outPrefix + "-working.png");
                bubble.ShowMessage("Done", Bubble.Green, 3000);
                await System.Threading.Tasks.Task.Delay(700);
                Snap(bubble, outPrefix + "-done.png");
                bubble.Close();
                wpf.Shutdown();
            };
            wpf.Run();
            return 0;
        }

        static void Snap(System.Windows.Window w, string path)
        {
            var src = System.Windows.PresentationSource.FromVisual(w);
            double sx = src.CompositionTarget.TransformToDevice.M11, sy = src.CompositionTarget.TransformToDevice.M22;
            int x = (int)(w.Left * sx), y = (int)(w.Top * sy), width = (int)(w.ActualWidth * sx), height = (int)(w.ActualHeight * sy);
            using (var bmp = new System.Drawing.Bitmap(width, height))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(x, y, 0, 0, bmp.Size);
                bmp.Save(path);
            }
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
