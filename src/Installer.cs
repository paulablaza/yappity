using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Yappity
{
    // Two ways to run Yappity:
    //   Portable: run Yappity.exe from anywhere. Put an empty "Yappity.portable" file next to
    //             it and settings, history and recordings stay in a YappityData folder beside it.
    //   Installed: Settings > Install copies it to %LOCALAPPDATA%\Programs\Yappity, adds it to
    //             the Start menu and to Windows' installed apps list. No admin needed.
    internal static class Installer
    {
        public static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Yappity");
        public static readonly string InstalledExe = Path.Combine(InstallDir, "Yappity.exe");
        static readonly string StartMenuLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Yappity.lnk");
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Yappity";

        static string Exe { get { return Application.ExecutablePath; } }

        public static bool IsInstalledCopy
        {
            get { return string.Equals(Path.GetFullPath(Exe), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase); }
        }

        public static bool IsInstalled { get { return File.Exists(InstalledExe); } }

        public static string PortableMarker { get { return Path.Combine(Path.GetDirectoryName(Exe), "Yappity.portable"); } }

        // Copies this .exe into place, registers it, and starts the installed copy.
        public static void Install()
        {
            Directory.CreateDirectory(InstallDir);
            KillOthersAt(InstalledExe);
            File.Copy(Exe, InstalledExe, true);
            Shortcut(StartMenuLink, InstalledExe);
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "Yappity");
                k.SetValue("DisplayVersion", App.Version);
                k.SetValue("Publisher", "Yappity");
                k.SetValue("DisplayIcon", InstalledExe);
                k.SetValue("InstallLocation", InstallDir);
                k.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
                k.SetValue("URLInfoAbout", "https://github.com/paulablaza/yappity");
                k.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            if (Store.StartsWithWindows) Store.StartsWithWindows = true; // points the Run entry at the new place
        }

        // Starts the installed copy once this one has quit, so the two never run together.
        public static void RelaunchInstalled()
        {
            Process.Start(new ProcessStartInfo(InstalledExe, "--wait-for " + Process.GetCurrentProcess().Id + " --history") { UseShellExecute = false });
        }

        // Yappity.exe --uninstall (what Windows Settings > Apps runs).
        public static int Uninstall()
        {
            if (MessageBox.Show("Remove Yappity from this PC?", "Yappity", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return 0;
            bool wipe = MessageBox.Show("Also delete your settings, history and saved recordings?\n\nChoose No to keep them in case you install Yappity again.",
                "Yappity", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            KillOthersAt(InstalledExe);
            try { Store.StartsWithWindows = false; } catch (Exception) { }
            try { if (File.Exists(StartMenuLink)) File.Delete(StartMenuLink); } catch (Exception) { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch (Exception) { }
            // A running .exe can't delete itself, so a short command does it a moment after we exit.
            string cmd = "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + InstallDir + "\"";
            if (wipe) cmd += " & rmdir /s /q \"" + Store.Dir + "\"";
            Process.Start(new ProcessStartInfo("cmd.exe", cmd) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            return 0;
        }

        // Start menu entry so Windows search finds Yappity. The installed copy always owns it;
        // a portable copy only adds one when there isn't one yet.
        public static void EnsureStartMenu()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("YAPPITY_HOME"))) return;
            if (StoreApp.IsPackaged) return; // the Store adds its own Start menu entry
            try
            {
                if (IsInstalledCopy || !File.Exists(StartMenuLink)) Shortcut(StartMenuLink, Exe);
            }
            catch (Exception e) { Store.Log("Couldn't add the Start menu shortcut: " + e.Message); }
        }

        static void Shortcut(string lnk, string target)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object link = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type lt = link.GetType();
            lt.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, link, new object[] { target });
            lt.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(target) });
            lt.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, link, new object[] { "Free voice typing" });
            lt.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, link, null);
        }

        static void KillOthersAt(string path)
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("Yappity"))
            {
                try
                {
                    if (p.Id != me && string.Equals(p.MainModule.FileName, path, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                }
                catch (Exception) { }
            }
        }
    }
}
