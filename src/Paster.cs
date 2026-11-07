using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yappity
{
    internal enum OutputResult { Typed, Pasted, SavedOnly, AdminWindow, Failed }

    // Gets the text into the app you're using. By default it types it like a
    // keyboard would, so your clipboard is never touched.
    internal static class Paster
    {
        static readonly bool SelfElevated = Native.IsProcessElevated(Native.GetCurrentProcess());
        const uint KEYEVENTF_UNICODE = 0x0004;

        public static async Task<OutputResult> Deliver(string text, string mode)
        {
            if (mode == "history") return OutputResult.SavedOnly;

            IntPtr target = Native.GetForegroundWindow();
            if (!SelfElevated && Native.IsWindowElevated(target)) return OutputResult.AdminWindow;

            await WaitForModifiersReleased();

            // Long text gets pasted even in "type" mode. Typing thousands of characters can
            // overrun a busy app (terminals especially) and it silently drops a chunk from the
            // middle. A paste always lands in one piece, and the clipboard is put back after.
            if (mode == "paste" || text.Length > TypeLimit)
                return await PasteWithClipboard(text) ? OutputResult.Pasted : OutputResult.Failed;
            return await Type(text) ? OutputResult.Typed : OutputResult.Failed;
        }

        public const int TypeLimit = 400;

        // Sends the text as Unicode key presses, a small chunk at a time so slow apps keep up.
        static async Task<bool> Type(string text)
        {
            string clean = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
            int size = Marshal.SizeOf(typeof(Native.INPUT));
            const int chunk = 24;
            for (int start = 0; start < clean.Length; start += chunk)
            {
                int end = Math.Min(clean.Length, start + chunk);
                var inputs = new List<Native.INPUT>((end - start) * 2);
                for (int i = start; i < end; i++)
                {
                    inputs.Add(Unicode(clean[i], false));
                    inputs.Add(Unicode(clean[i], true));
                }
                uint sent = Native.SendInput((uint)inputs.Count, inputs.ToArray(), size);
                if (sent != inputs.Count) return false;
                if (end < clean.Length) await Task.Delay(15);
            }
            return true;
        }

        static Native.INPUT Unicode(char c, bool up)
        {
            var i = new Native.INPUT();
            i.type = Native.INPUT_KEYBOARD;
            i.U.ki.wVk = 0;
            i.U.ki.wScan = c;
            i.U.ki.dwFlags = KEYEVENTF_UNICODE | (up ? Native.KEYEVENTF_KEYUP : 0);
            return i;
        }

        // Copy, Ctrl+V, then put the old clipboard back.
        static async Task<bool> PasteWithClipboard(string text)
        {
            DataObject saved = Snapshot();
            if (!SetText(text.Replace("\r\n", "\n").Replace("\n", "\r\n"))) return false;
            var inputs = new[] { Key(0x11, false), Key(0x56, false), Key(0x56, true), Key(0x11, true) };
            Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
            if (saved != null)
            {
                // Give the app time to read the clipboard before swapping it back.
                await Task.Delay(700);
                try { Clipboard.SetDataObject(saved, true, 5, 100); }
                catch (Exception e) { Store.Log("Couldn't restore the clipboard: " + e.Message); }
            }
            return true;
        }

        public static bool SetText(string text)
        {
            try
            {
                Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, text), true, 10, 100);
                return true;
            }
            catch (Exception e)
            {
                Store.Log("Clipboard busy: " + e.Message);
                return false;
            }
        }

        static DataObject Snapshot()
        {
            try
            {
                IDataObject current = Clipboard.GetDataObject();
                if (current == null) return null;
                var copy = new DataObject();
                int kept = 0;
                foreach (string format in current.GetFormats(false))
                {
                    try
                    {
                        object data = current.GetData(format, false);
                        if (data != null) { copy.SetData(format, false, data); kept++; }
                    }
                    catch (Exception) { } // some formats can't be copied, so skip them
                }
                return kept > 0 ? copy : null;
            }
            catch (Exception) { return null; }
        }

        // If you're still holding Ctrl or Shift from the shortcut, typed letters would
        // turn into shortcuts, so wait until you let go (up to 2 seconds).
        static async Task WaitForModifiersReleased()
        {
            int[] mods = { 0x10, 0x11, 0x12, 0x5B, 0x5C };
            for (int tries = 0; tries < 80; tries++)
            {
                bool any = false;
                foreach (int m in mods) if ((Native.GetAsyncKeyState(m) & 0x8000) != 0) any = true;
                if (!any) return;
                await Task.Delay(25);
            }
        }

        static Native.INPUT Key(ushort vk, bool up)
        {
            var i = new Native.INPUT();
            i.type = Native.INPUT_KEYBOARD;
            i.U.ki.wVk = vk;
            i.U.ki.wScan = (ushort)Native.MapVirtualKey(vk, 0);
            i.U.ki.dwFlags = up ? Native.KEYEVENTF_KEYUP : 0;
            return i;
        }
    }
}
