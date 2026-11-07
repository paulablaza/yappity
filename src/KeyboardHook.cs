using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Yappity
{
    // Watches the keyboard system-wide so the dictation key works in any app.
    // Fires Pressed when the key goes down, Released when it comes back up, and
    // Interrupted when another key is pressed while it is held (a shortcut like
    // Right Ctrl+C, not dictation).
    internal sealed class KeyboardHook : IDisposable
    {
        public sealed class KeyOption
        {
            public readonly string Name;
            public readonly int Vk;
            public readonly bool Swallow; // hide the key from other apps
            public KeyOption(string name, int vk, bool swallow) { Name = name; Vk = vk; Swallow = swallow; }
            public override string ToString() { return Name; }
        }

        public static readonly KeyOption[] Options =
        {
            new KeyOption("Right Ctrl", 0xA3, false),
            new KeyOption("Right Alt", 0xA5, true),
            new KeyOption("Right Shift", 0xA1, false),
            new KeyOption("F8", 0x77, true),
            new KeyOption("F9", 0x78, true),
            new KeyOption("F10", 0x79, true),
            new KeyOption("Scroll Lock", 0x91, true),
            new KeyOption("Pause", 0x13, true),
        };

        public static KeyOption Find(int vk)
        {
            foreach (var o in Options) if (o.Vk == vk) return o;
            return Options[0];
        }

        public event Action Pressed;
        public event Action Released;
        public event Action Interrupted;

        KeyOption key = Options[0];
        bool held;
        IntPtr hook = IntPtr.Zero;
        Native.LowLevelKeyboardProc proc; // kept in a field so it is not garbage collected
        SynchronizationContext ui;

        public KeyOption Key
        {
            get { return key; }
            set { key = value ?? Options[0]; held = false; }
        }

        public void Install()
        {
            ui = SynchronizationContext.Current;
            proc = Callback;
            hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, proc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
                throw new InvalidOperationException("Could not watch the keyboard (error " + Marshal.GetLastWin32Error() + ").");
        }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                bool injected = (k.flags & Native.LLKHF_INJECTED) != 0;
                int msg = wParam.ToInt32();
                bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;

                if (!injected)
                {
                    if (k.vkCode == key.Vk)
                    {
                        if (down && !held) { held = true; Raise(Pressed); }
                        else if (up && held) { held = false; Raise(Released); }
                        if (key.Swallow) return (IntPtr)1;
                    }
                    else if (held && down && !IsAltGrCtrl(k))
                    {
                        Raise(Interrupted);
                    }
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        // Right Alt on many layouts sends a fake Left Ctrl along with it (AltGr).
        static bool IsAltGrCtrl(Native.KBDLLHOOKSTRUCT k)
        {
            return k.vkCode == 0xA2 && (k.scanCode & 0x200) != 0;
        }

        // The hook must return fast, so the real work runs on the UI thread afterwards.
        void Raise(Action a)
        {
            if (a == null) return;
            if (ui != null) ui.Post(delegate { a(); }, null);
            else a();
        }

        public void Dispose()
        {
            if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        }
    }
}
