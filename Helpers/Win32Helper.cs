using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EXPasteWPF.Helpers
{
    /// <summary>
    /// Win32 API 封装：全局热键注册、键盘模拟输入
    /// </summary>
    internal static class Win32Helper
    {
        #region 全局热键

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        #endregion

        #region 键盘模拟输入 SendInput

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_UNICODE = 0x0004;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const ushort VK_RETURN = 0x0D;
        public const ushort VK_TAB = 0x09;
        public const ushort VK_HOME = 0x24;
        public const ushort VK_END = 0x23;
        public const ushort VK_LEFT = 0x25;
        public const ushort VK_RIGHT = 0x27;
        public const ushort VK_DELETE = 0x2E;

        /// <summary>
        /// 发送单个 Unicode 字符（按下+抬起）
        /// </summary>
        public static void SendUnicodeChar(char c)
        {
            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wScan = c;
            inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wScan = c;
            inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

            SendKeyboardInput(inputs);
        }

        /// <summary>
        /// 发送一个虚拟键（按下+抬起），如回车、Tab
        /// </summary>
        public static void SendKey(ushort vk)
        {
            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = vk;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = vk;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;

            SendKeyboardInput(inputs);
        }

        private static void SendKeyboardInput(INPUT[] inputs)
        {
            int cbSize = Marshal.SizeOf<INPUT>();
            uint sent = SendInput((uint)inputs.Length, inputs, cbSize);
            int lastError = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[EXPaste SendInput] expected={inputs.Length}, sent={sent}, cbSize={cbSize}, lastError={lastError}");
            if (sent != inputs.Length)
            {
                var ex = new Win32Exception(lastError);
                throw new InvalidOperationException($"SendInput failed: expected={inputs.Length}, sent={sent}, cbSize={cbSize}, lastError={lastError}, message={ex.Message}", ex);
            }
        }

        #endregion

        #region 显示器信息（用于多显示器 / 高 DPI 下的最大化区域计算）

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        public const uint MONITOR_DEFAULTTONULL = 0x00000000;
        public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        #endregion
    }
}
