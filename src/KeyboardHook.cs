using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Yappity
{
    // Watches the keyboard in every app so the shortcut works anywhere.
    //
    // Toggle mode: press the shortcut once to start and again to stop.
    // Hold mode: hold the shortcut while talking and let go to stop.
    // Esc throws away the recording in both modes.
    internal sealed class KeyboardHook : IDisposable
    {
        public const int ModCtrl = 1, ModShift = 2, ModAlt = 4, ModWin = 8;
        const int VK_ESCAPE = 0x1B;

        public int Vk = 0x4B;                    // K
        public int Mods = ModCtrl | ModShift;
        public bool HoldMode;
        public volatile bool Recording;          // lets Esc cancel only while recording

        public event Action Pressed;             // hold mode: shortcut went down
        public event Action Released;            // hold mode: shortcut came up
        public event Action Toggled;             // toggle mode: shortcut pressed
        public event Action Interrupted;         // hold mode: another key was pressed with a lone modifier
        public event Action Cancelled;           // Esc while recording

        bool held;
        bool tapPending;                         // toggle mode with a lone modifier key
        int swallowUpVk = -1;
        IntPtr hook = IntPtr.Zero;
        Native.LowLevelKeyboardProc proc;        // kept in a field so it is not garbage collected
        SynchronizationContext ui;

        Action<int, int> capture;                // set while the user records a new shortcut
        int captureModifier;

        public void Install()
        {
            ui = SynchronizationContext.Current;
            proc = Callback;
            hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, proc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
                throw new InvalidOperationException("Couldn't watch the keyboard (error " + Marshal.GetLastWin32Error() + ").");
        }

        public void Set(int vk, int mods, bool holdMode)
        {
            Vk = vk;
            Mods = mods;
            HoldMode = holdMode;
            held = false;
            tapPending = false;
        }

        // The next shortcut the user presses is handed to `done` as (vk, mods),
        // or (-1, 0) if they press Esc.
        public void Capture(Action<int, int> done)
        {
            captureModifier = 0;
            capture = done;
        }

        public void CancelCapture() { capture = null; }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                if ((k.flags & Native.LLKHF_INJECTED) == 0 && Handle((int)k.vkCode, k.scanCode, wParam.ToInt32()))
                    return (IntPtr)1;
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        // Returns true to hide the key from other apps.
        bool Handle(int vk, uint scanCode, int msg)
        {
            bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
            bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;

            if (up && vk == swallowUpVk) { swallowUpVk = -1; return true; }

            if (capture != null) return HandleCapture(vk, down, up);

            if (down && vk == VK_ESCAPE && Recording)
            {
                Raise(Cancelled);
                swallowUpVk = VK_ESCAPE;
                return true;
            }

            bool lone = IsModifier(vk) && vk == Vk;

            if (vk == Vk)
            {
                if (down && !held)
                {
                    if (CurrentMods(Vk) != Mods) return false;
                    held = true;
                    if (HoldMode) Raise(Pressed);
                    else if (lone) tapPending = true;
                    else Raise(Toggled);
                    if (!IsModifier(vk)) { swallowUpVk = vk; return true; }
                    return false;
                }
                if (down && held) return !IsModifier(vk); // key repeat
                if (up && held)
                {
                    held = false;
                    if (HoldMode) Raise(Released);
                    else if (tapPending) { tapPending = false; Raise(Toggled); }
                }
                return false;
            }

            if (down && held && IsModifier(Vk) && !IsAltGrCtrl(vk, scanCode))
            {
                // A lone modifier used in a normal shortcut, like Right Ctrl + C.
                tapPending = false;
                if (HoldMode) Raise(Interrupted);
            }
            return false;
        }

        bool HandleCapture(int vk, bool down, bool up)
        {
            if (down && IsModifier(vk))
            {
                captureModifier = vk;
                return false;
            }
            if (down)
            {
                var done = capture;
                capture = null;
                swallowUpVk = vk;
                int mods = CurrentMods(vk);
                if (vk == VK_ESCAPE && mods == 0) RaiseCapture(done, -1, 0);
                else RaiseCapture(done, vk, mods);
                return true;
            }
            if (up && IsModifier(vk) && vk == captureModifier)
            {
                // Only a modifier was pressed and released: use it on its own (like Right Ctrl).
                var done = capture;
                capture = null;
                RaiseCapture(done, vk, 0);
            }
            return false;
        }

        public static bool IsModifier(int vk)
        {
            return (vk >= 0xA0 && vk <= 0xA5) || vk == 0x10 || vk == 0x11 || vk == 0x12 || vk == 0x5B || vk == 0x5C;
        }

        static bool Down(int vk) { return (Native.GetAsyncKeyState(vk) & 0x8000) != 0; }

        // Modifiers held right now, not counting the key itself when it is one.
        static int CurrentMods(int self)
        {
            int m = 0;
            if (Down(0x11) && Family(self) != ModCtrl) m |= ModCtrl;
            if (Down(0x10) && Family(self) != ModShift) m |= ModShift;
            if (Down(0x12) && Family(self) != ModAlt) m |= ModAlt;
            if ((Down(0x5B) || Down(0x5C)) && Family(self) != ModWin) m |= ModWin;
            return m;
        }

        static int Family(int vk)
        {
            if (vk == 0x11 || vk == 0xA2 || vk == 0xA3) return ModCtrl;
            if (vk == 0x10 || vk == 0xA0 || vk == 0xA1) return ModShift;
            if (vk == 0x12 || vk == 0xA4 || vk == 0xA5) return ModAlt;
            if (vk == 0x5B || vk == 0x5C) return ModWin;
            return 0;
        }

        // Right Alt on many layouts sends a fake Left Ctrl along with it (AltGr).
        static bool IsAltGrCtrl(int vk, uint scanCode)
        {
            return vk == 0xA2 && (scanCode & 0x200) != 0;
        }

        // Shortcuts that would get in the way of normal typing.
        public static string Problem(int vk, int mods)
        {
            if (vk < 0) return null;
            bool plainKey = (vk >= 0x30 && vk <= 0x5A) || (vk >= 0xBA && vk <= 0xE2) || vk == 0x20 || vk == 0x0D || vk == 0x08 || vk == 0x09;
            if (plainKey && (mods & ~ModShift) == 0)
                return "That key types text, so add Ctrl, Alt or Win to it.";
            if (vk == VK_ESCAPE) return "Esc is used to cancel a recording, so pick another key.";
            return null;
        }

        public static string Describe(int vk, int mods)
        {
            var sb = new StringBuilder();
            if ((mods & ModCtrl) != 0) sb.Append("Ctrl + ");
            if ((mods & ModAlt) != 0) sb.Append("Alt + ");
            if ((mods & ModShift) != 0) sb.Append("Shift + ");
            if ((mods & ModWin) != 0) sb.Append("Win + ");
            sb.Append(KeyName(vk));
            return sb.ToString();
        }

        static string KeyName(int vk)
        {
            switch (vk)
            {
                case 0xA2: return "Left Ctrl";
                case 0xA3: return "Right Ctrl";
                case 0xA0: return "Left Shift";
                case 0xA1: return "Right Shift";
                case 0xA4: return "Left Alt";
                case 0xA5: return "Right Alt";
                case 0x5B: case 0x5C: return "Win";
                case 0x20: return "Space";
                case 0x13: return "Pause";
                case 0x91: return "Scroll Lock";
                case 0x14: return "Caps Lock";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "Page Up";
                case 0x22: return "Page Down";
                case 0xC0: return "`";
                case 0xBD: return "-";
                case 0xBB: return "=";
                case 0xDB: return "[";
                case 0xDD: return "]";
                case 0xDC: return "\\";
                case 0xBA: return ";";
                case 0xDE: return "'";
                case 0xBC: return ",";
                case 0xBE: return ".";
                case 0xBF: return "/";
            }
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return "Num " + (vk - 0x60);
            return ((Keys)vk).ToString();
        }

        // The hook must return fast, so the real work runs on the UI thread afterwards.
        void Raise(Action a)
        {
            if (a == null) return;
            if (ui != null) ui.Post(delegate { a(); }, null);
            else a();
        }

        void RaiseCapture(Action<int, int> done, int vk, int mods)
        {
            if (done == null) return;
            if (ui != null) ui.Post(delegate { done(vk, mods); }, null);
            else done(vk, mods);
        }

        public void Dispose()
        {
            if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        }
    }
}
