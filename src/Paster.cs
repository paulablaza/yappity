using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Saywrite
{
    internal enum PasteResult { Pasted, CopiedOnly, Failed }

    // Puts text into the active window: copy to clipboard, press Ctrl+V, then put
    // back whatever was on the clipboard before.
    internal static class Paster
    {
        static readonly bool SelfElevated = Native.IsProcessElevated(Native.GetCurrentProcess());

        public static async Task<PasteResult> Paste(string text, bool autoPaste, bool restoreClipboard)
        {
            IntPtr target = Native.GetForegroundWindow();
            bool blocked = !SelfElevated && Native.IsWindowElevated(target);
            bool paste = autoPaste && !blocked;

            DataObject saved = paste && restoreClipboard ? Snapshot() : null;
            if (!SetText(text)) return PasteResult.Failed;
            if (!paste) return PasteResult.CopiedOnly;

            await WaitForModifiersReleased();
            SendCtrlV();

            if (saved != null)
            {
                // Give the target app time to read the clipboard before we swap it back.
                await Task.Delay(700);
                try { Clipboard.SetDataObject(saved, true, 5, 100); }
                catch (Exception e) { Store.Log("Could not restore clipboard: " + e.Message); }
            }
            return PasteResult.Pasted;
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
                    catch (Exception) { } // some formats can't be copied; skip them
                }
                return kept > 0 ? copy : null;
            }
            catch (Exception) { return null; }
        }

        // If a modifier is still physically down (for example Shift), Ctrl+V would
        // become Ctrl+Shift+V, so wait briefly for the user to let go.
        static async Task WaitForModifiersReleased()
        {
            int[] mods = { 0x10, 0x11, 0x12, 0x5B, 0x5C }; // Shift, Ctrl, Alt, Win
            for (int tries = 0; tries < 20; tries++)
            {
                bool any = false;
                foreach (int m in mods) if ((Native.GetAsyncKeyState(m) & 0x8000) != 0) any = true;
                if (!any) return;
                await Task.Delay(25);
            }
        }

        static void SendCtrlV()
        {
            var inputs = new[]
            {
                Key(0x11, false), Key(0x56, false), Key(0x56, true), Key(0x11, true)
            };
            Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
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
