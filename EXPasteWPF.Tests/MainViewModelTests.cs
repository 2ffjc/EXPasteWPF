using System.ComponentModel;
using EXPasteWPF.ViewModels;
using Xunit;

namespace EXPasteWPF.Tests
{
    public class MainViewModelTests
    {
        [Fact]
        public void EnableHotkey_Set_RaisesPropertyChanged()
        {
            var vm = new MainViewModel();
            bool raised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.EnableHotkey)) raised = true;
            };

            vm.EnableHotkey = true;
            Assert.True(raised);
            Assert.True(vm.EnableHotkey);
        }

        [Theory]
        [InlineData(nameof(MainViewModel.FileHotkeyText))]
        [InlineData(nameof(MainViewModel.TextEditHotkeyText))]
        [InlineData(nameof(MainViewModel.CompilerHotkeyText))]
        [InlineData(nameof(MainViewModel.PauseResumeHotkeyText))]
        [InlineData(nameof(MainViewModel.CountdownSeconds))]
        [InlineData(nameof(MainViewModel.SettingsDirty))]
        public void Properties_Set_RaisePropertyChanged(string propertyName)
        {
            var vm = new MainViewModel();
            bool raised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyName) raised = true;
            };

            switch (propertyName)
            {
                case nameof(MainViewModel.FileHotkeyText):
                    vm.FileHotkeyText = "Ctrl+F1";
                    break;
                case nameof(MainViewModel.TextEditHotkeyText):
                    vm.TextEditHotkeyText = "Ctrl+F2";
                    break;
                case nameof(MainViewModel.CompilerHotkeyText):
                    vm.CompilerHotkeyText = "Ctrl+F3";
                    break;
                case nameof(MainViewModel.PauseResumeHotkeyText):
                    vm.PauseResumeHotkeyText = "Ctrl+F4";
                    break;
                case nameof(MainViewModel.CountdownSeconds):
                    vm.CountdownSeconds = 15;
                    break;
                case nameof(MainViewModel.SettingsDirty):
                    vm.SettingsDirty = true;
                    break;
            }

            Assert.True(raised, $"PropertyChanged for {propertyName} was not raised");
        }

        [Fact]
        public void HotkeyObjects_DefaultToNull()
        {
            var vm = new MainViewModel();
            Assert.Null(vm.FileHotkey);
            Assert.Null(vm.TextEditHotkey);
            Assert.Null(vm.CompilerHotkey);
            Assert.Null(vm.PauseResumeHotkey);
        }

        [Fact]
        public void CountdownSeconds_DefaultIsThree()
        {
            var vm = new MainViewModel();
            Assert.Equal(3, vm.CountdownSeconds);
        }

        [Fact]
        public void HotkeyTextProperties_DefaultToEmptyString()
        {
            var vm = new MainViewModel();
            Assert.Equal("", vm.FileHotkeyText);
            Assert.Equal("", vm.TextEditHotkeyText);
            Assert.Equal("", vm.CompilerHotkeyText);
            Assert.Equal("", vm.PauseResumeHotkeyText);
        }
    }
}
