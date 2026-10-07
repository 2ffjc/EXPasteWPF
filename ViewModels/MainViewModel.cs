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
        private int _pasteSpeedMs = 50;
        private string _compilerQuickMode = "头歌";
        private bool _tougeFullControl = true;
        private bool _tougeSymbolDetect = true;
        private bool _vsFullControl;
        private bool _vsSymbolDetect;
        private bool _customFullControl;
        private bool _customSymbolDetect;
        private string _fullControlLabel = "全控制缩进检测（头歌）";
        private string _symbolDetectLabel = "左右符号对称检测（头歌）";
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

        public int PasteSpeedMs
        {
            get => _pasteSpeedMs;
            set { _pasteSpeedMs = value; OnPropertyChanged(); }
        }

        public string CompilerQuickMode
        {
            get => _compilerQuickMode;
            set { _compilerQuickMode = value; OnPropertyChanged(); }
        }

        public bool TougeFullControl
        {
            get => _tougeFullControl;
            set { _tougeFullControl = value; OnPropertyChanged(); }
        }

        public bool TougeSymbolDetect
        {
            get => _tougeSymbolDetect;
            set { _tougeSymbolDetect = value; OnPropertyChanged(); }
        }

        public bool VsFullControl
        {
            get => _vsFullControl;
            set { _vsFullControl = value; OnPropertyChanged(); }
        }

        public bool VsSymbolDetect
        {
            get => _vsSymbolDetect;
            set { _vsSymbolDetect = value; OnPropertyChanged(); }
        }

        public bool CustomFullControl
        {
            get => _customFullControl;
            set { _customFullControl = value; OnPropertyChanged(); }
        }

        public bool CustomSymbolDetect
        {
            get => _customSymbolDetect;
            set { _customSymbolDetect = value; OnPropertyChanged(); }
        }

        public string FullControlLabel
        {
            get => _fullControlLabel;
            set { _fullControlLabel = value; OnPropertyChanged(); }
        }

        public string SymbolDetectLabel
        {
            get => _symbolDetectLabel;
            set { _symbolDetectLabel = value; OnPropertyChanged(); }
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
