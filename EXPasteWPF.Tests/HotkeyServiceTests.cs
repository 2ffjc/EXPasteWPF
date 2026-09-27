using System;
using EXPasteWPF.Services;
using Xunit;

namespace EXPasteWPF.Tests
{
    public class HotkeyServiceTests
    {
        [Fact]
        public void GetHotkeyType_FileId_ReturnsFile()
        {
            Assert.Equal(HotkeyType.File, HotkeyService.GetHotkeyType(new IntPtr(9000)));
        }

        [Fact]
        public void GetHotkeyType_TextEditId_ReturnsTextEdit()
        {
            Assert.Equal(HotkeyType.TextEdit, HotkeyService.GetHotkeyType(new IntPtr(9001)));
        }

        [Fact]
        public void GetHotkeyType_CompilerId_ReturnsCompiler()
        {
            Assert.Equal(HotkeyType.Compiler, HotkeyService.GetHotkeyType(new IntPtr(9002)));
        }

        [Fact]
        public void GetHotkeyType_PauseResumeId_ReturnsPauseResume()
        {
            Assert.Equal(HotkeyType.PauseResume, HotkeyService.GetHotkeyType(new IntPtr(9003)));
        }

        [Fact]
        public void GetHotkeyType_UnknownId_ReturnsNone()
        {
            Assert.Equal(HotkeyType.None, HotkeyService.GetHotkeyType(new IntPtr(0)));
            Assert.Equal(HotkeyType.None, HotkeyService.GetHotkeyType(new IntPtr(12345)));
            Assert.Equal(HotkeyType.None, HotkeyService.GetHotkeyType(new IntPtr(-1)));
        }

        [Fact]
        public void HotkeyType_Enum_HasAllExpectedValues()
        {
            Assert.True(Enum.IsDefined(typeof(HotkeyType), HotkeyType.None));
            Assert.True(Enum.IsDefined(typeof(HotkeyType), HotkeyType.File));
            Assert.True(Enum.IsDefined(typeof(HotkeyType), HotkeyType.TextEdit));
            Assert.True(Enum.IsDefined(typeof(HotkeyType), HotkeyType.Compiler));
            Assert.True(Enum.IsDefined(typeof(HotkeyType), HotkeyType.PauseResume));
        }

        [Fact]
        public void Register_WithZeroHwnd_ReturnsFalse()
        {
            // 无窗口句柄时注册必须返回 false，避免在无宿主环境下误报成功
            var service = new HotkeyService(IntPtr.Zero);
            var gesture = new System.Windows.Input.KeyGesture(System.Windows.Input.Key.F1);
            Assert.False(service.RegisterFileHotkey(gesture));
            Assert.False(service.RegisterTextEditHotkey(gesture));
            Assert.False(service.RegisterCompilerHotkey(gesture));
            Assert.False(service.RegisterPauseResumeHotkey(gesture));
        }

        [Fact]
        public void UnregisterAll_WithZeroHwnd_DoesNotThrow()
        {
            var service = new HotkeyService(IntPtr.Zero);
            service.UnregisterAll();
        }
    }
}
