using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace EXPasteWPF.ViewModels
{
    /// <summary>
    /// 主视图模型：承载快捷键配置与界面状态
    /// </summary>
    internal class MainViewModel : INotifyPropertyChanged
    {
        private bool _enableHotkey;
        private string _fileHotkeyText = "";
        private string _textEditHotkeyText = "";
        private string _compilerHotkeyText = "";
        private string _pauseResumeHotkeyText = "";
        private int _countdownSeconds = 3;
        private bool _settingsDirty;

        public bool EnableHotkey
        {
            get => _enableHotkey;
            set { _enableHotkey = value; OnPropertyChanged(); }
        }

        public string FileHotkeyText
        {
            get => _fileHotkeyText;
            set { _fileHotkeyText = value; OnPropertyChanged(); }
        }

        public string TextEditHotkeyText
        {
            get => _textEditHotkeyText;
            set { _textEditHotkeyText = value; OnPropertyChanged(); }
        }

        public string CompilerHotkeyText
        {
            get => _compilerHotkeyText;
            set { _compilerHotkeyText = value; OnPropertyChanged(); }
        }

        public string PauseResumeHotkeyText
        {
            get => _pauseResumeHotkeyText;
            set { _pauseResumeHotkeyText = value; OnPropertyChanged(); }
        }

        public int CountdownSeconds
        {
            get => _countdownSeconds;
            set { _countdownSeconds = value; OnPropertyChanged(); }
        }

        public bool SettingsDirty
        {
            get => _settingsDirty;
            set { _settingsDirty = value; OnPropertyChanged(); }
        }

        // 当前捕获的热键对象
        public KeyGesture? FileHotkey { get; set; }
        public KeyGesture? TextEditHotkey { get; set; }
        public KeyGesture? CompilerHotkey { get; set; }
        public KeyGesture? PauseResumeHotkey { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
