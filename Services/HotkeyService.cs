using System;
using System.Windows.Input;
using System.Windows.Interop;
using EXPasteWPF.Helpers;

namespace EXPasteWPF.Services
{
    /// <summary>
    /// 全局快捷键服务：注册/注销全局热键
    /// </summary>
    internal class HotkeyService
    {
        private const int HOTKEY_FILE_ID = 9000;
        private const int HOTKEY_TEXTEDIT_ID = 9001;
        private const int HOTKEY_COMPILER_ID = 9002;
        private const int HOTKEY_PAUSE_RESUME_ID = 9003;
        private const int HOTKEY_TEST_ID = 9010;

        private readonly IntPtr _hwnd;

        public HotkeyService(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        /// <summary>
        /// 注册选择文件模式热键
        /// </summary>
        public bool RegisterFileHotkey(KeyGesture gesture) => Register(gesture, HOTKEY_FILE_ID);

        /// <summary>
        /// 注册文本编辑直接粘贴热键
        /// </summary>
        public bool RegisterTextEditHotkey(KeyGesture gesture) => Register(gesture, HOTKEY_TEXTEDIT_ID);

        /// <summary>
        /// 注册编译器模式热键
        /// </summary>
        public bool RegisterCompilerHotkey(KeyGesture gesture) => Register(gesture, HOTKEY_COMPILER_ID);

        public bool RegisterPauseResumeHotkey(KeyGesture gesture) => Register(gesture, HOTKEY_PAUSE_RESUME_ID);

        public bool CanRegisterHotkey(KeyGesture gesture)
        {
            if (!Register(gesture, HOTKEY_TEST_ID)) return false;
            Win32Helper.UnregisterHotKey(_hwnd, HOTKEY_TEST_ID);
            return true;
        }

        /// <summary>
        /// 注销所有热键
        /// </summary>
        public void UnregisterAll()
        {
            if (_hwnd == IntPtr.Zero) return;
            Win32Helper.UnregisterHotKey(_hwnd, HOTKEY_FILE_ID);
            Win32Helper.UnregisterHotKey(_hwnd, HOTKEY_TEXTEDIT_ID);
            Win32Helper.UnregisterHotKey(_hwnd, HOTKEY_COMPILER_ID);
            Win32Helper.UnregisterHotKey(_hwnd, HOTKEY_PAUSE_RESUME_ID);
        }

        private bool Register(KeyGesture gesture, int id)
        {
            if (_hwnd == IntPtr.Zero) return false;

            uint modifiers = 0;
            if (gesture.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= Win32Helper.MOD_ALT;
            if (gesture.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= Win32Helper.MOD_CONTROL;
            if (gesture.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= Win32Helper.MOD_SHIFT;
            if (gesture.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= Win32Helper.MOD_WIN;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key);
            return Win32Helper.RegisterHotKey(_hwnd, id, modifiers, vk);
        }

        /// <summary>
        /// 判断 WM_HOTKEY 消息对应的热键类型
        /// </summary>
        public static HotkeyType GetHotkeyType(IntPtr wParam)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_FILE_ID) return HotkeyType.File;
            if (id == HOTKEY_TEXTEDIT_ID) return HotkeyType.TextEdit;
            if (id == HOTKEY_COMPILER_ID) return HotkeyType.Compiler;
            if (id == HOTKEY_PAUSE_RESUME_ID) return HotkeyType.PauseResume;
            return HotkeyType.None;
        }
    }

    internal enum HotkeyType
    {
        None,
        File,
        TextEdit,
        Compiler,
        PauseResume
    }
}
