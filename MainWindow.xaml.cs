using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using EXPasteWPF.Helpers;
using EXPasteWPF.Services;
using EXPasteWPF.ViewModels;
using Microsoft.Win32;

namespace EXPasteWPF
{
    public partial class MainWindow : Window
    {
        private readonly ConfigService _configService = new();
        private HotkeyService? _hotkeyService;

        private IntPtr _windowHandle;
        private HwndSource? _source;
        private bool _hookAdded;

        // 粘贴状态
        private CancellationTokenSource? _pasteCts;
        private TaskCompletionSource<bool>? _pasteResumeSignal;
        private bool _isPasting;
        private bool _isPaused;
        private string? _pasteContent;
        private int _pastePosition;
        private int _pasteCurrentLine;
        private int _pasteTotalLines;
        private bool _loadingConfig;

        // 编译器模式粘贴专用（全控制缩进 / 符号对称）
        private bool _compilerFullControl;
        private bool _compilerSymbolDetect;
        private int[]? _lineIndents;
        private int _compilerPasteLine;
        private bool _pendingLineStartRitual;

        // 快捷键捕获模式
        private bool _isCapturing;
        private string? _capturingTarget; // "File", "TextEdit", "Compiler" or "PauseResume"

        // 当前默认粘贴模式（最近一次切换到的内容模式）
        private ViewType _activePasteMode = ViewType.File;

        internal MainViewModel ViewModel { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = ViewModel;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewMouseLeftButtonDown += MainWindow_PreviewMouseLeftButtonDown;
            StateChanged += MainWindow_StateChanged;
            Activated += MainWindow_Activated;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _windowHandle = new WindowInteropHelper(this).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            if (_source != null && !_hookAdded)
            {
                _source.AddHook(HwndHook);
                _hookAdded = true;
            }
            _hotkeyService = new HotkeyService(_windowHandle);

            LoadConfig();
        }


        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            StopPaste();
            if (SettingsPanel.Visibility == Visibility.Visible && ViewModel.SettingsDirty)
            {
                LoadConfig();
            }
            _hotkeyService?.UnregisterAll();
            if (_source != null && _hookAdded)
            {
                _source.RemoveHook(HwndHook);
                _hookAdded = false;
            }
        }

        #region 配置加载与保存

        private void LoadConfig()
        {
            _loadingConfig = true;
            try
            {
                var cfg = _configService.Load();
                ViewModel.EnableHotkey = cfg.EnableHotkey;
                ViewModel.FileHotkey = ConfigService.StringToGesture(cfg.FileHotkey);
                ViewModel.TextEditHotkey = ConfigService.StringToGesture(cfg.TextEditHotkey);
                ViewModel.CompilerHotkey = ConfigService.StringToGesture(cfg.CompilerHotkey);
                ViewModel.PauseResumeHotkey = ConfigService.StringToGesture(cfg.PauseResumeHotkey);
                ViewModel.FileHotkeyText = FormatGesture(ViewModel.FileHotkey);
                ViewModel.TextEditHotkeyText = FormatGesture(ViewModel.TextEditHotkey);
                ViewModel.CompilerHotkeyText = FormatGesture(ViewModel.CompilerHotkey);
                ViewModel.PauseResumeHotkeyText = FormatGesture(ViewModel.PauseResumeHotkey);
                ViewModel.CountdownSeconds = Math.Clamp(cfg.CountdownSeconds, 1, 60);
                ViewModel.PasteSpeedMs = Math.Clamp(cfg.PasteSpeedMs, 20, 500);
                ViewModel.CompilerQuickMode = cfg.CompilerQuickMode;
                ViewModel.TougeFullControl = cfg.TougeFullControl;
                ViewModel.TougeSymbolDetect = cfg.TougeSymbolDetect;
                ViewModel.VsFullControl = cfg.VsFullControl;
                ViewModel.VsSymbolDetect = cfg.VsSymbolDetect;
                ViewModel.CustomFullControl = cfg.CustomFullControl;
                ViewModel.CustomSymbolDetect = cfg.CustomSymbolDetect;

                EnableHotkeyCheckBox.IsChecked = cfg.EnableHotkey;
                CountdownSlider.Value = ViewModel.CountdownSeconds;
                CountdownValueText.Text = $"{ViewModel.CountdownSeconds} 秒";
                PasteSpeedSlider.Value = ViewModel.PasteSpeedMs;
                PasteSpeedValueText.Text = $"{ViewModel.PasteSpeedMs} ms";
                ApplyCompilerModeUI();
                UpdateHotkeyDisplays();
                ClearSettingsDirty();

                if (cfg.EnableHotkey)
                {
                    ApplyHotkeys();
                }
                else
                {
                    _hotkeyService?.UnregisterAll();
                }
            }
            finally
            {
                _loadingConfig = false;
            }
        }

        private void SaveConfig()
        {
            var cfg = new AppConfig
            {
                EnableHotkey = ViewModel.EnableHotkey,
                FileHotkey = FormatGesture(ViewModel.FileHotkey),
                TextEditHotkey = FormatGesture(ViewModel.TextEditHotkey),
                CompilerHotkey = FormatGesture(ViewModel.CompilerHotkey),
                PauseResumeHotkey = FormatGesture(ViewModel.PauseResumeHotkey),
                CountdownSeconds = Math.Clamp(ViewModel.CountdownSeconds, 1, 60),
                PasteSpeedMs = Math.Clamp(ViewModel.PasteSpeedMs, 20, 500),
                CompilerQuickMode = ViewModel.CompilerQuickMode,
                TougeFullControl = ViewModel.TougeFullControl,
                TougeSymbolDetect = ViewModel.TougeSymbolDetect,
                VsFullControl = ViewModel.VsFullControl,
                VsSymbolDetect = ViewModel.VsSymbolDetect,
                CustomFullControl = ViewModel.CustomFullControl,
                CustomSymbolDetect = ViewModel.CustomSymbolDetect
            };
            _configService.Save(cfg);
        }

        /// <summary>
        /// 更新设置界面中快捷键显示框的内容
        /// </summary>
        private void ClearSettingsDirty()
        {
            ViewModel.SettingsDirty = false;
            SaveSettingsBtn.IsEnabled = false;
            SaveSettingsBtn.Opacity = 0.4;
        }

        private void MarkSettingsDirty()
        {
            if (_loadingConfig) return;
            ViewModel.SettingsDirty = true;
            SaveSettingsBtn.IsEnabled = true;
            SaveSettingsBtn.Opacity = 1.0;
        }

        private void SaveSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            ViewModel.CountdownSeconds = (int)Math.Round(CountdownSlider.Value);
            ViewModel.PasteSpeedMs = Math.Clamp((int)Math.Round(PasteSpeedSlider.Value), 20, 500);
            ClearSettingsDirty();
            if (ViewModel.EnableHotkey)
            {
                ApplyHotkeys();
            }
            else
            {
                _hotkeyService?.UnregisterAll();
            }
        }

        private void CountdownSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is not Slider slider || CountdownValueText == null) return;

            int seconds = (int)Math.Round(slider.Value);
            seconds = Math.Clamp(seconds, 1, 60);
            ViewModel.CountdownSeconds = seconds;
            CountdownValueText.Text = $"{seconds} 秒";
            MarkSettingsDirty();
        }

        private void PasteSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is not Slider slider || PasteSpeedValueText == null) return;

            int ms = (int)Math.Round(slider.Value);
            ms = Math.Clamp(ms, 20, 500);
            ViewModel.PasteSpeedMs = ms;
            PasteSpeedValueText.Text = $"{ms} ms";
            MarkSettingsDirty();
        }

        /// <summary>
        /// 按当前 CompilerQuickMode 更新 UI：下拉文字、两个开关标签文字、开关状态
        /// </summary>
        private void ApplyCompilerModeUI()
        {
            string mode = ViewModel.CompilerQuickMode;
            string displayText, fcLabel, sdLabel;
            bool fcOn, sdOn;

            switch (mode)
            {
                case "VSCode":
                    displayText = "VSCode";
                    fcLabel = "全控制缩进检测（vs）";
                    sdLabel = "左右符号对称检测（vs）";
                    fcOn = ViewModel.VsFullControl;
                    sdOn = ViewModel.VsSymbolDetect;
                    break;
                case "自定义":
                    displayText = "自定义编辑器模式";
                    fcLabel = "全控制缩进检测（自定义）";
                    sdLabel = "左右符号对称检测（自定义）";
                    fcOn = ViewModel.CustomFullControl;
                    sdOn = ViewModel.CustomSymbolDetect;
                    break;
                default: // 头歌
                    displayText = "头歌模式";
                    fcLabel = "全控制缩进检测（头歌）";
                    sdLabel = "左右符号对称检测（头歌）";
                    fcOn = ViewModel.TougeFullControl;
                    sdOn = ViewModel.TougeSymbolDetect;
                    break;
            }

            QuickModeText.Text = displayText;
            ViewModel.FullControlLabel = fcLabel;
            ViewModel.SymbolDetectLabel = sdLabel;

            _loadingConfig = true; // 防止 Toggle 切换时误触发 MarkSettingsDirty
            FullControlIndentToggle.IsChecked = fcOn;
            SymbolSymmetryToggle.IsChecked = sdOn;
            _loadingConfig = false;
        }

        /// <summary>
        /// 把当前 UI 上两个开关的状态写回 ViewModel 对应模式字段
        /// </summary>
        private void WriteToggleToViewModel()
        {
            bool fc = FullControlIndentToggle.IsChecked == true;
            bool sd = SymbolSymmetryToggle.IsChecked == true;
            switch (ViewModel.CompilerQuickMode)
            {
                case "VSCode":
                    ViewModel.VsFullControl = fc;
                    ViewModel.VsSymbolDetect = sd;
                    break;
                case "自定义":
                    ViewModel.CustomFullControl = fc;
                    ViewModel.CustomSymbolDetect = sd;
                    break;
                default:
                    ViewModel.TougeFullControl = fc;
                    ViewModel.TougeSymbolDetect = sd;
                    break;
            }
        }

        private void FullControlIndentToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingConfig || FullControlIndentToggle == null) return;
            WriteToggleToViewModel();
            MarkSettingsDirty();
        }

        private void SymbolSymmetryToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingConfig || SymbolSymmetryToggle == null) return;
            WriteToggleToViewModel();
            MarkSettingsDirty();
        }

        #region 快速选择模式抽屉

        private bool _drawerOpen;

        private void QuickModeDropdownBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_drawerOpen) CloseQuickModeDrawer();
            else OpenQuickModeDrawer();
        }

        private void OpenQuickModeDrawer()
        {
            QuickModeDrawerPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double targetHeight = QuickModeDrawerPanel.DesiredSize.Height + 8;
            _drawerOpen = true;

            var anim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(180)
            };
            anim.Completed += (s, _) => { if (_drawerOpen) QuickModeDrawer.Height = targetHeight; };
            QuickModeDrawer.BeginAnimation(HeightProperty, anim);
        }

        private void CloseQuickModeDrawer()
        {
            _drawerOpen = false;
            var anim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = QuickModeDrawer.ActualHeight,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            anim.Completed += (s, _) => { if (!_drawerOpen) QuickModeDrawer.Height = 0; };
            QuickModeDrawer.BeginAnimation(HeightProperty, anim);
        }

        private void QuickModeItem_Touge_Click(object sender, RoutedEventArgs e)
        {
            WriteToggleToViewModel(); // 先保存当前模式开关
            ViewModel.CompilerQuickMode = "头歌";
            ApplyCompilerModeUI();
            MarkSettingsDirty();
            CloseQuickModeDrawer();
        }

        private void QuickModeItem_VSCode_Click(object sender, RoutedEventArgs e)
        {
            WriteToggleToViewModel();
            ViewModel.CompilerQuickMode = "VSCode";
            ApplyCompilerModeUI();
            MarkSettingsDirty();
            CloseQuickModeDrawer();
        }

        private void QuickModeItem_Custom_Click(object sender, RoutedEventArgs e)
        {
            WriteToggleToViewModel();
            ViewModel.CompilerQuickMode = "自定义";
            ApplyCompilerModeUI();
            MarkSettingsDirty();
            CloseQuickModeDrawer();
        }

        #endregion

        internal static string FormatGesture(KeyGesture? gesture)
        {
            if (gesture == null) return "";
            if (!string.IsNullOrWhiteSpace(gesture.DisplayString)) return gesture.DisplayString;

            var converter = new KeyGestureConverter();
            return converter.ConvertToString(gesture) ?? "";
        }

        internal static bool AreGesturesEqual(KeyGesture? first, KeyGesture? second)
        {
            if (first == null || second == null) return false;
            return first.Key == second.Key && first.Modifiers == second.Modifiers;
        }

        internal string? FindDuplicateHotkeyTarget(KeyGesture gesture, string currentTarget)
        {
            return FindDuplicateHotkeyTarget(ViewModel, gesture, currentTarget);
        }

        internal static string? FindDuplicateHotkeyTarget(MainViewModel viewModel, KeyGesture gesture, string currentTarget)
        {
            var items = new (string Target, string Name, KeyGesture? Gesture)[]
            {
                ("File", "选择文件模式", viewModel.FileHotkey),
                ("TextEdit", "文本编辑模式", viewModel.TextEditHotkey),
                ("Compiler", "编译器模式", viewModel.CompilerHotkey),
                ("PauseResume", "开始/暂停/继续", viewModel.PauseResumeHotkey)
            };

            foreach (var item in items)
            {
                if (item.Target != currentTarget && AreGesturesEqual(gesture, item.Gesture))
                {
                    return item.Name;
                }
            }

            return null;
        }

        private bool IsHotkeyAvailable(KeyGesture gesture)
        {
            if (_hotkeyService == null) return true;

            _hotkeyService.UnregisterAll();
            bool available = _hotkeyService.CanRegisterHotkey(gesture);
            if (ViewModel.EnableHotkey)
            {
                ApplyHotkeys();
            }
            return available;
        }

        private void UpdateHotkeyDisplays()
        {
            string fileText = FormatGesture(ViewModel.FileHotkey);
            if (string.IsNullOrEmpty(fileText))
            {
                FileHotkeyText.Text = ViewModel.EnableHotkey ? "点击设置快捷键" : "未设置";
                FileHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    ViewModel.EnableHotkey
                        ? System.Windows.Media.Color.FromRgb(0x8A, 0x8A, 0x8A)
                        : System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60));
            }
            else
            {
                FileHotkeyText.Text = fileText;
                FileHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
            }

            string teText = FormatGesture(ViewModel.TextEditHotkey);
            if (string.IsNullOrEmpty(teText))
            {
                TextEditHotkeyText.Text = ViewModel.EnableHotkey ? "点击设置快捷键" : "未设置";
                TextEditHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    ViewModel.EnableHotkey
                        ? System.Windows.Media.Color.FromRgb(0x8A, 0x8A, 0x8A)
                        : System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60));
            }
            else
            {
                TextEditHotkeyText.Text = teText;
                TextEditHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
            }

            string cmpText = FormatGesture(ViewModel.CompilerHotkey);
            if (string.IsNullOrEmpty(cmpText))
            {
                CompilerHotkeyText.Text = ViewModel.EnableHotkey ? "点击设置快捷键" : "未设置";
                CompilerHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    ViewModel.EnableHotkey
                        ? System.Windows.Media.Color.FromRgb(0x8A, 0x8A, 0x8A)
                        : System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60));
            }
            else
            {
                CompilerHotkeyText.Text = cmpText;
                CompilerHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
            }

            string prText = FormatGesture(ViewModel.PauseResumeHotkey);
            if (string.IsNullOrEmpty(prText))
            {
                PauseResumeHotkeyText.Text = ViewModel.EnableHotkey ? "点击设置快捷键" : "未设置";
                PauseResumeHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    ViewModel.EnableHotkey
                        ? System.Windows.Media.Color.FromRgb(0x8A, 0x8A, 0x8A)
                        : System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60));
            }
            else
            {
                PauseResumeHotkeyText.Text = prText;
                PauseResumeHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
            }

        }

        #endregion

        #region 视图切换

        private enum ViewType { File, TextEdit, Compiler, Settings, About }

        private void SwitchView(ViewType view)
        {
            // 离开设置页时，若有未保存的更改则放弃（重新读取配置，恢复到上次保存状态）
            if (SettingsPanel.Visibility == Visibility.Visible && view != ViewType.Settings && ViewModel.SettingsDirty)
            {
                LoadConfig();
            }

            FileModePanel.Visibility = Visibility.Hidden;
            TextEditModePanel.Visibility = Visibility.Hidden;
            CompilerModePanel.Visibility = Visibility.Hidden;
            SettingsPanel.Visibility = Visibility.Hidden;
            AboutPanel.Visibility = Visibility.Hidden;

            BtnFileMode.Style = (Style)FindResource("menuButton");
            BtnTextEditMode.Style = (Style)FindResource("menuButton");
            BtnCompilerMode.Style = (Style)FindResource("menuButton");
            BtnSettings.Style = (Style)FindResource("menuButton");
            BtnAbout.Style = (Style)FindResource("menuButton");

            switch (view)
            {
                case ViewType.File:
                    FileModePanel.Visibility = Visibility.Visible;
                    BtnFileMode.Style = (Style)FindResource("activeMenuButton");
                    _activePasteMode = ViewType.File;
                    break;
                case ViewType.TextEdit:
                    TextEditModePanel.Visibility = Visibility.Visible;
                    BtnTextEditMode.Style = (Style)FindResource("activeMenuButton");
                    _activePasteMode = ViewType.TextEdit;
                    break;
                case ViewType.Compiler:
                    CompilerModePanel.Visibility = Visibility.Visible;
                    BtnCompilerMode.Style = (Style)FindResource("activeMenuButton");
                    _activePasteMode = ViewType.Compiler;
                    break;
                case ViewType.Settings:
                    // 进入设置页时重新读取配置（确保显示已保存的状态）
                    LoadConfig();
                    SettingsPanel.Visibility = Visibility.Visible;
                    BtnSettings.Style = (Style)FindResource("activeMenuButton");
                    break;
                case ViewType.About:
                    AboutPanel.Visibility = Visibility.Visible;
                    BtnAbout.Style = (Style)FindResource("activeMenuButton");
                    break;
            }
        }

        private void BtnFileMode_Click(object sender, RoutedEventArgs e) => SwitchView(ViewType.File);
        private void BtnTextEditMode_Click(object sender, RoutedEventArgs e) => SwitchView(ViewType.TextEdit);
        private void BtnCompilerMode_Click(object sender, RoutedEventArgs e) => SwitchView(ViewType.Compiler);
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => SwitchView(ViewType.Settings);
        private void BtnAbout_Click(object sender, RoutedEventArgs e) => SwitchView(ViewType.About);

        #endregion

        #region 文件模式：拖放 + 路径 + 浏览 + 预览

        private void DropBorder_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DropBorder.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5a4f78"));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void DropBorder_Drop(object sender, DragEventArgs e)
        {
            DropBorder.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#443c5a"));

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                ShowDroppedFiles(files);
            }
        }

        private void ShowDroppedFiles(string[] files)
        {
            if (files == null || files.Length == 0)
            {
                FilePreviewText.Text = "拖放文件到此处，或点击下方“浏览文件...”选择文件";
                return;
            }

            // 路径显示在下方路径框
            PathTextBox.Text = string.Join(";", files);

            // 上方框预览第一个文件的全部内容
            string firstFile = files.FirstOrDefault(f => File.Exists(f)) ?? files[0];
            LoadFileContentToPreview(firstFile);
        }

        private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var paths = PathTextBox.Text.Split(new[] { ';', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            string? firstFile = paths.Select(p => p.Trim()).FirstOrDefault(p => File.Exists(p));
            if (firstFile != null)
            {
                LoadFileContentToPreview(firstFile);
            }
        }

        /// <summary>
        /// 加载文件全部内容到预览框（不再限制行数）
        /// </summary>
        private void LoadFileContentToPreview(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    FilePreviewText.Text = $"文件不存在：{filePath}";
                    return;
                }

                string content = File.ReadAllText(filePath, Encoding.UTF8);
                FilePreviewText.Text = content;
                // ScrollToHome equivalent: scroll viewer to top-left
                var sv = FindScrollViewer(DropBorder);
                if (sv != null)
                {
                    sv.ScrollToHome();
                }
            }
            catch (Exception ex)
            {
                FilePreviewText.Text = $"无法读取文件内容：{ex.Message}";
            }
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject root)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is ScrollViewer sv) return sv;
                var found = FindScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            string? initialDir = null;
            var firstPath = PathTextBox.Text.Split(new[] { ';', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstPath) && File.Exists(firstPath))
            {
                initialDir = Path.GetDirectoryName(firstPath);
            }
            else if (!string.IsNullOrWhiteSpace(firstPath) && Directory.Exists(firstPath))
            {
                initialDir = firstPath;
            }

            var dlg = new OpenFileDialog
            {
                Title = "选择文件",
                Multiselect = true,
                InitialDirectory = initialDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dlg.ShowDialog() == true)
            {
                ShowDroppedFiles(dlg.FileNames);
            }
        }

        #endregion

        #region 编译器模式：粘贴时去除行首 Tab

        private void CompilerTextBox_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = (string)e.DataObject.GetData(DataFormats.Text);
                string stripped = StripLeadingTabs(text);
                e.DataObject = new DataObject(DataFormats.Text, stripped);
            }
        }

        internal static string StripLeadingTabs(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int j = 0;
                while (j < line.Length && line[j] == '\t') j++;
                sb.Append(line.Substring(j));
                if (i < lines.Length - 1) sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// 为「全控制缩进」准备编译器内容：保留每行缩进级数（Tab=4 空格），
        /// 同时剥除每行前导空白，返回 (剥除后内容, 每行缩进空格数)。
        /// fullControl=false 时退化为旧 StripLeadingTabs 行为，indents 为 null。
        /// </summary>
        internal static (string Content, int[]? Indents) PrepareCompilerContent(string raw, bool fullControl)
        {
            if (!fullControl) return (StripLeadingTabs(raw), null);

            var lines = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var indents = new int[lines.Length];
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int j = 0;
                int indent = 0;
                while (j < line.Length)
                {
                    char ch = line[j];
                    if (ch == '\t') { indent += 4; j++; }
                    else if (ch == ' ') { indent += 1; j++; }
                    else break;
                }
                indents[i] = indent;
                sb.Append(line.Substring(j));
                if (i < lines.Length - 1) sb.Append('\n');
            }
            return (sb.ToString(), indents);
        }

        /// <summary>
        /// 判定一个字符是否为「左半」配对符号（触发方法 B：输入后按 DEL 删掉自动配对的右半）。
        /// 覆盖半角括号/方括号/花括号/尖括号/引号，以及全角对应符号。
        /// </summary>
        internal static bool IsLeftPairSymbol(char c)
        {
            switch (c)
            {
                case '(':
                case '[':
                case '{':
                case '<':
                case '"':
                case '\'':
                case '（':   // 全角左圆括号
                case '［':   // 全角左方括号
                case '｛':   // 全角左花括号
                case '＜':   // 全角左尖括号
                case '“':   // 中文左双引号
                case '‘':   // 中文左单引号
                    return true;
                default:
                    return false;
            }
        }

        #endregion


        #region 关于：GitHub 链接

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch
            {
            }
        }

        #endregion

        #region 快捷键捕获与注册

        /// <summary>
        /// 点击显示框进入捕获模式
        /// </summary>
        private void HotkeyDisplay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!ViewModel.EnableHotkey)
            {
                HotkeyErrorText.Text = "请先启用快捷键再设置快捷键，别忘保存喵";
                e.Handled = true;
                return;
            }

            var border = (Border)sender;
            string tag = border.Tag?.ToString() ?? "";
            StartCapture(tag);
            e.Handled = true;
        }

        private void StartCapture(string target)
        {
            // 互斥：若已经在捕获，先重置上一个目标的视觉状态，避免两个快捷键同时进入捕获模式
            if (_isCapturing)
            {
                ResetCaptureVisuals();
            }

            _isCapturing = true;
            _capturingTarget = target;

            if (target == "File")
            {
                FileHotkeyDisplay.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xd4, 0x89, 0xff));
                FileHotkeyText.Text = "按下快捷键组合...";
                FileHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xd4, 0x89, 0xff));
            }
            else if (target == "TextEdit")
            {
                TextEditHotkeyDisplay.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xd4, 0x89, 0xff));
                TextEditHotkeyText.Text = "按下快捷键组合...";
                TextEditHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xd4, 0x89, 0xff));
            }
            else if (target == "Compiler")
            {
                CompilerHotkeyDisplay.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xd4, 0x89, 0xff));
                CompilerHotkeyText.Text = "按下快捷键组合...";
                CompilerHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xd4, 0x89, 0xff));
            }
            else if (target == "PauseResume")
            {
                PauseResumeHotkeyDisplay.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xd4, 0x89, 0xff));
                PauseResumeHotkeyText.Text = "按下快捷键组合...";
                PauseResumeHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xd4, 0x89, 0xff));
            }

            HotkeyErrorText.Text = "BackSpace清除当前快捷键，ESC可取消更改，别忘保存喵";
        }

        /// <summary>
        /// 重置快捷键捕获相关的视觉元素，用于切换捕获目标时清理上一个目标的残留
        /// </summary>
        private void ResetCaptureVisuals()
        {
            FileHotkeyDisplay.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x5a, 0x52, 0x78));

            TextEditHotkeyDisplay.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x5a, 0x52, 0x78));

            CompilerHotkeyDisplay.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x5a, 0x52, 0x78));

            PauseResumeHotkeyDisplay.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x5a, 0x52, 0x78));

            UpdateHotkeyDisplays();
        }

        private void EndCapture()
        {
            _isCapturing = false;
            _capturingTarget = null;
            ResetCaptureVisuals();
            HotkeyErrorText.Text = "";
        }

        /// <summary>
        /// 鼠标在别处点击时自动取消快捷键捕获模式（点击设置按钮/显示框本身除外）
        /// </summary>
        private void MainWindow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isCapturing) return;

            // 点击在“设置快捷键”按钮或显示框上时，不取消（让对应按钮的处理逻辑接管）
            if (e.OriginalSource is DependencyObject src)
            {
                if (IsDescendantOf(FileHotkeyDisplay, src) ||
                    IsDescendantOf(TextEditHotkeyDisplay, src) ||
                    IsDescendantOf(CompilerHotkeyDisplay, src) ||
                    IsDescendantOf(PauseResumeHotkeyDisplay, src))
                {
                    return;
                }
            }
            EndCapture();
        }

        private static bool IsDescendantOf(DependencyObject? parent, DependencyObject? node)
        {
            if (parent == null || node == null) return false;
            while (node != null && node != parent)
            {
                node = System.Windows.Media.VisualTreeHelper.GetParent(node);
                if (node == null) break;
            }
            return node == parent;
        }

        /// <summary>
        /// 窗口级 PreviewKeyDown：在捕获模式下拦截按键
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isCapturing || _capturingTarget == null) return;

            // ESC：取消捕获
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                EndCapture();
                return;
            }

            // Backspace：删除当前快捷键
            if (e.Key == Key.Back)
            {
                e.Handled = true;
                if (_capturingTarget == "File")
                    ViewModel.FileHotkey = null;
                else if (_capturingTarget == "TextEdit")
                    ViewModel.TextEditHotkey = null;
                else if (_capturingTarget == "Compiler")
                    ViewModel.CompilerHotkey = null;
                else if (_capturingTarget == "PauseResume")
                    ViewModel.PauseResumeHotkey = null;

                EndCapture();
                UpdateHotkeyDisplays();
                MarkSettingsDirty();
                return;
            }

            e.Handled = true;

            Key key = e.Key;
            if (key == Key.System) key = e.SystemKey;

            // 忽略仅修饰键的按下
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            var modifiers = Keyboard.Modifiers;
            bool isFunctionKey = key >= Key.F1 && key <= Key.F12;
            if (modifiers == ModifierKeys.None && !isFunctionKey)
            {
                HotkeyErrorText.Text = "该按键需要与 Ctrl、Shift、Alt 或 Win 组合，F1-F12 可单独设置喵";
                return;
            }

            HotkeyErrorText.Text = "";

            KeyGesture gesture;
            try
            {
                gesture = new KeyGesture(key, modifiers);
            }
            catch (NotSupportedException)
            {
                HotkeyErrorText.Text = "该按键不支持作为快捷键，请尝试其他按键";
                return;
            }

            string? duplicateTarget = FindDuplicateHotkeyTarget(gesture, _capturingTarget);
            if (duplicateTarget != null)
            {
                HotkeyErrorText.Text = $"该快捷键已用于{duplicateTarget}，请换一个快捷键";
                return;
            }

            if (!IsHotkeyAvailable(gesture))
            {
                HotkeyErrorText.Text = "该快捷键可能已被系统或其他程序占用，请换一个快捷键";
                return;
            }

            string gestureText = FormatGesture(gesture);
            if (_capturingTarget == "File")
            {
                ViewModel.FileHotkey = gesture;
                ViewModel.FileHotkeyText = gestureText;
            }
            else if (_capturingTarget == "TextEdit")
            {
                ViewModel.TextEditHotkey = gesture;
                ViewModel.TextEditHotkeyText = gestureText;
            }
            else if (_capturingTarget == "Compiler")
            {
                ViewModel.CompilerHotkey = gesture;
                ViewModel.CompilerHotkeyText = gestureText;
            }
            else if (_capturingTarget == "PauseResume")
            {
                ViewModel.PauseResumeHotkey = gesture;
                ViewModel.PauseResumeHotkeyText = gestureText;
            }

            EndCapture();
            UpdateHotkeyDisplays();
            MarkSettingsDirty();
        }

        private void EnableHotkeyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ViewModel.EnableHotkey = EnableHotkeyCheckBox.IsChecked == true;
            UpdateHotkeyDisplays();
            MarkSettingsDirty();
        }

        private void ApplyHotkeys()
        {
            _hotkeyService?.UnregisterAll();
            HotkeyErrorText.Text = "";

            if (ViewModel.FileHotkey != null && !_hotkeyService!.RegisterFileHotkey(ViewModel.FileHotkey))
            {
                HotkeyErrorText.Text = $"选择文件模式快捷键 {FormatGesture(ViewModel.FileHotkey)} 注册失败，可能被占用";
            }

            if (ViewModel.TextEditHotkey != null && !_hotkeyService!.RegisterTextEditHotkey(ViewModel.TextEditHotkey))
            {
                HotkeyErrorText.Text = $"文本编辑模式快捷键 {FormatGesture(ViewModel.TextEditHotkey)} 注册失败，可能被占用";
            }

            if (ViewModel.CompilerHotkey != null && !_hotkeyService!.RegisterCompilerHotkey(ViewModel.CompilerHotkey))
            {
                HotkeyErrorText.Text = $"编译器模式快捷键 {FormatGesture(ViewModel.CompilerHotkey)} 注册失败，可能被占用";
            }

            if (ViewModel.PauseResumeHotkey != null && !_hotkeyService!.RegisterPauseResumeHotkey(ViewModel.PauseResumeHotkey))
            {
                HotkeyErrorText.Text = $"开始/暂停/继续快捷键 {FormatGesture(ViewModel.PauseResumeHotkey)} 注册失败，可能被占用";
            }
        }

        #region 窗口控制：最小化/最大化/关闭 + 拖动 + 边缘缩放

        // WM_NCHITTEST 相关常量
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int ResizeBorderSize = 6;
        // 右上角窗口按钮区域宽度（三个按钮 46*3 ≈ 138，留余量）
        private const double TopRightButtonsWidth = 140;
        private const double TopRightButtonsHeight = 32;

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            // 注意：原生 MINMAXINFO 字段顺序为 ptReserved / ptMaxSize / ptMaxPosition / ptMinTrack / ptMaxTrack
            public int ReservedX;
            public int ReservedY;
            public int MaxSizeWidth;      // ptMaxSize
            public int MaxSizeHeight;
            public int MaxPositionX;     // ptMaxPosition
            public int MaxPositionY;
            public int MinTrackWidth;    // ptMinTrack
            public int MinTrackHeight;
            public int MaxTrackWidth;    // ptMaxTrack
            public int MaxTrackHeight;
        }

        /// <summary>
        /// 点击背景区域时拖动整个窗口
        /// </summary>
        private void BackgroundBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { /* 拖动失败忽略 */ }
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>
        /// 窗口状态变化时更新最大化/还原图标
        /// </summary>
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateMaximizeIcon();
        }

        private void UpdateMaximizeIcon()
        {
            if (MaximizeIcon == null) return;
            // 最大化时显示“还原”图标（两个错位的方框），正常时显示单个方框
            if (WindowState == WindowState.Maximized)
            {
                MaximizeIcon.Data = System.Windows.Media.Geometry.Parse("M2,0 L10,0 L10,8 L2,8 Z M0,2 L8,2 L8,10 L0,10 Z");
            }
            else
            {
                MaximizeIcon.Data = System.Windows.Media.Geometry.Parse("M0,0 L10,0 L10,10 L0,10 Z");
            }
        }

        /// <summary>
        /// 处理 WM_NCHITTEST：实现 4 条边 + 4 个角的窗口缩放
        /// </summary>
        private IntPtr HandleNcHitTest(IntPtr lParam, ref bool handled)
        {
            int l = lParam.ToInt32();
            int sx = (short)(l & 0xFFFF);
            int sy = (short)((l >> 16) & 0xFFFF);

            // 屏幕坐标 → WPF 坐标
            Point p = PointFromScreen(new Point(sx, sy));
            double w = ActualWidth;
            double h = ActualHeight;

            // 右上角窗口按钮区域不拦截（让按钮正常响应点击）
            if (p.X >= w - TopRightButtonsWidth && p.Y <= TopRightButtonsHeight)
            {
                return IntPtr.Zero; // 默认 HTCLIENT，交给 WPF
            }

            bool onLeft = p.X <= ResizeBorderSize;
            bool onRight = p.X >= w - ResizeBorderSize;
            bool onTop = p.Y <= ResizeBorderSize;
            bool onBottom = p.Y >= h - ResizeBorderSize;

            if (onTop && onLeft) { handled = true; return (IntPtr)HTTOPLEFT; }
            if (onTop && onRight) { handled = true; return (IntPtr)HTTOPRIGHT; }
            if (onBottom && onLeft) { handled = true; return (IntPtr)HTBOTTOMLEFT; }
            if (onBottom && onRight) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
            if (onLeft) { handled = true; return (IntPtr)HTLEFT; }
            if (onRight) { handled = true; return (IntPtr)HTRIGHT; }
            if (onTop) { handled = true; return (IntPtr)HTTOP; }
            if (onBottom) { handled = true; return (IntPtr)HTBOTTOM; }

            return IntPtr.Zero;
        }

        #endregion

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32Helper.WM_HOTKEY)
            {
                var type = HotkeyService.GetHotkeyType(wParam);
                if (type == HotkeyType.File)
                {
                    Dispatcher.Invoke(() => SwitchView(ViewType.File));
                    handled = true;
                }
                else if (type == HotkeyType.TextEdit)
                {
                    Dispatcher.Invoke(() => SwitchView(ViewType.TextEdit));
                    handled = true;
                }
                else if (type == HotkeyType.Compiler)
                {
                    Dispatcher.Invoke(() => SwitchView(ViewType.Compiler));
                    handled = true;
                }
                else if (type == HotkeyType.PauseResume)
                {
                    Dispatcher.Invoke(HandleStartPauseResumeHotkey);
                    handled = true;
                }
            }
            else if (msg == WM_NCHITTEST)
            {
                return HandleNcHitTest(lParam, ref handled);
            }
            else if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                // 取窗口当前所在显示器的工作区（物理像素），正确支持多显示器 / 高 DPI 最大化
                IntPtr hMon = Win32Helper.MonitorFromWindow(_windowHandle, Win32Helper.MONITOR_DEFAULTTONEAREST);
                var info = new Win32Helper.MONITORINFO { cbSize = Marshal.SizeOf<Win32Helper.MONITORINFO>() };
                if (hMon != IntPtr.Zero && Win32Helper.GetMonitorInfo(hMon, ref info))
                {
                    var work = info.rcWork;
                    mmi.MaxPositionX = work.left;
                    mmi.MaxPositionY = work.top;
                    mmi.MaxSizeWidth = work.right - work.left;
                    mmi.MaxSizeHeight = work.bottom - work.top;
                    mmi.MaxTrackWidth = info.rcMonitor.right - info.rcMonitor.left;
                    mmi.MaxTrackHeight = info.rcMonitor.bottom - info.rcMonitor.top;
                }

                // 最小缩放尺寸：MinWidth/MinHeight 是 DIP，MINMAXINFO 要物理像素，按当前 DPI 换算
                double dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
                mmi.MinTrackWidth = (int)Math.Ceiling(MinWidth * dpiScale);
                mmi.MinTrackHeight = (int)Math.Ceiling(MinHeight * dpiScale);

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 开始/暂停/继续快捷键：未粘贴则从当前激活模式开始；已粘贴则切换暂停/继续
        /// </summary>
        private void HandleStartPauseResumeHotkey()
        {
            if (_isPasting)
            {
                TogglePauseResumePaste();
            }
            else
            {
                StartPasteFromActiveMode();
            }
        }

        /// <summary>
        /// 从当前激活的内容模式读取文本并开始粘贴
        /// </summary>
        private void StartPasteFromActiveMode()
        {
            switch (_activePasteMode)
            {
                case ViewType.File:
                    var paths = PathTextBox.Text.Split(new[] { ';', '\r', '\n' },
                            StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => File.Exists(p))
                        .ToList();

                    if (paths.Count == 0)
                    {
                        MessageBox.Show("请先在选择文件模式中拖放或选择有效的文件", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var sb = new StringBuilder();
                    foreach (var path in paths)
                    {
                        try
                        {
                            sb.Append(File.ReadAllText(path, Encoding.UTF8));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"读取文件失败：{path}\n{ex.Message}", "错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    ResetCompilerPasteState();
                    StartPaste(sb.ToString());
                    break;

                case ViewType.TextEdit:
                    string teContent = TextEditBox.Text;
                    if (string.IsNullOrEmpty(teContent))
                    {
                        MessageBox.Show("请先在文本编辑模式中输入要粘贴的文本内容", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    ResetCompilerPasteState();
                    StartPaste(teContent);
                    break;

                case ViewType.Compiler:
                    string cmpContent = CompilerTextBox.Text;
                    if (string.IsNullOrEmpty(cmpContent))
                    {
                        MessageBox.Show("请先在编译器模式中输入要粘贴的代码内容", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    // 按当前选中的快速模式分发到对应逻辑
                    switch (ViewModel.CompilerQuickMode)
                    {
                        case "VSCode":
                            vs_PreparePaste(cmpContent);
                            break;
                        case "自定义":
                            custom_PreparePaste(cmpContent);
                            break;
                        default: // 头歌
                            touge_PreparePaste(cmpContent);
                            break;
                    }
                    StartPaste(_compilerPreparedContent!);
                    break;
            }
        }

        /// <summary>
        /// 非编译器模式粘贴前清零编译器专用状态，避免误触发缩进/符号逻辑
        /// </summary>
        private void ResetCompilerPasteState()
        {
            _compilerFullControl = false;
            _compilerSymbolDetect = false;
            _lineIndents = null;
            _compilerPasteLine = 0;
            _pendingLineStartRitual = false;
            _compilerActiveMode = "";
            _compilerPreparedContent = null;
        }

        #endregion

        #region 头歌模式（touge_）粘贴逻辑

        // 编译器模式当前活动子模式（"头歌"/"VSCode"/"自定义"），供 TypeContentAsync 分发
        private string _compilerActiveMode = "";
        // 准备好的最终粘贴内容（各模式 PreparePaste 写入）
        private string? _compilerPreparedContent;

        private void touge_PreparePaste(string rawContent)
        {
            bool full = ViewModel.TougeFullControl;
            bool symbol = ViewModel.TougeSymbolDetect;
            var (content, indents) = PrepareCompilerContent(rawContent, full);
            _compilerActiveMode = "头歌";
            _compilerFullControl = full;
            _compilerSymbolDetect = symbol;
            _lineIndents = indents;
            _compilerPasteLine = 0;
            _pendingLineStartRitual = full;
            _compilerPreparedContent = content;
        }

        /// <summary>
        /// 头歌模式行首仪式：Home 跳行首 + 补空格缩进
        /// </summary>
        private async Task touge_LineStartRitual(int speedMs, CancellationToken token)
        {
            Win32Helper.SendKey(Win32Helper.VK_HOME);
            await Task.Delay(speedMs, token);
            int indent = _lineIndents![_compilerPasteLine];
            for (int s = 0; s < indent; s++)
            {
                Win32Helper.SendUnicodeChar(' ');
                await Task.Delay(speedMs, token);
            }
        }

        /// <summary>
        /// 头歌模式符号对称检测：左半符号后按 DEL 删掉自动配对的右半
        /// </summary>
        private async Task touge_SymbolDetect(char c, int speedMs, CancellationToken token)
        {
            await Task.Delay(speedMs, token);
            Win32Helper.SendKey(Win32Helper.VK_DELETE);
        }

        #endregion

        #region VSCode 模式（vs_）粘贴逻辑

        private void vs_PreparePaste(string rawContent)
        {
            // VSCode 模式：暂用最基础的逐字粘贴（StripLeadingTabs），用户后续再改
            _compilerActiveMode = "VSCode";
            _compilerFullControl = ViewModel.VsFullControl;
            _compilerSymbolDetect = ViewModel.VsSymbolDetect;
            _lineIndents = null;
            _compilerPasteLine = 0;
            _pendingLineStartRitual = false;
            _compilerPreparedContent = StripLeadingTabs(rawContent);
        }

        private async Task vs_LineStartRitual(int speedMs, CancellationToken token)
        {
            // TODO: 用户后续定义 VSCode 模式缩进逻辑
            await Task.CompletedTask;
        }

        private async Task vs_SymbolDetect(char c, int speedMs, CancellationToken token)
        {
            // TODO: 用户后续定义 VSCode 模式符号逻辑
            await Task.CompletedTask;
        }

        #endregion

        #region 自定义模式（custom_）粘贴逻辑

        private void custom_PreparePaste(string rawContent)
        {
            // 自定义模式：暂用最基础的逐字粘贴，用户后续再改
            _compilerActiveMode = "自定义";
            _compilerFullControl = ViewModel.CustomFullControl;
            _compilerSymbolDetect = ViewModel.CustomSymbolDetect;
            _lineIndents = null;
            _compilerPasteLine = 0;
            _pendingLineStartRitual = false;
            _compilerPreparedContent = StripLeadingTabs(rawContent);
        }

        private async Task custom_LineStartRitual(int speedMs, CancellationToken token)
        {
            // TODO: 用户后续定义自定义模式缩进逻辑
            await Task.CompletedTask;
        }

        private async Task custom_SymbolDetect(char c, int speedMs, CancellationToken token)
        {
            // TODO: 用户后续定义自定义模式符号逻辑
            await Task.CompletedTask;
        }


        #endregion

        #region 清空按钮

        private void TextEditClearBtn_Click(object sender, RoutedEventArgs e) => TextEditBox.Clear();
        private void CompilerClearBtn_Click(object sender, RoutedEventArgs e) => CompilerTextBox.Clear();

        #endregion

        #region 核心粘贴逻辑：模拟键盘自动输入

        private void FileStartPasteBtn_Click(object sender, RoutedEventArgs e)
            => HandleStartPauseResumeHotkey();

        private void TextEditStartPasteBtn_Click(object sender, RoutedEventArgs e)
            => HandleStartPauseResumeHotkey();

        private void CompilerStartPasteBtn_Click(object sender, RoutedEventArgs e)
            => HandleStartPauseResumeHotkey();

        private void StopPasteBtn_Click(object sender, RoutedEventArgs e) => StopPaste();

        private void StopPaste()
        {
            _pasteCts?.Cancel();
            _pasteResumeSignal?.TrySetResult(true);
        }

        private void SetCountdownText(string text)
        {
            FileCountdownText.Text = text;
            TextEditCountdownText.Text = text;
            CompilerCountdownText.Text = text;
        }

        private async void StartPaste(string content)
        {
            if (_isPasting) return;
            _isPasting = true;
            _isPaused = false;
            _pasteContent = content;
            _pastePosition = 0;
            _pasteTotalLines = CountContentLines(content);
            _pasteCurrentLine = _pasteTotalLines > 0 ? 1 : 0;
            SetPasteButtonsForRunning();

            _pasteCts = new CancellationTokenSource();
            var token = _pasteCts.Token;

            try
            {
                await RunPasteCountdownAsync(token);
                await TypeContentAsync(token);

                Title = "EXPaste - 粘贴完成";
                await Task.Delay(800, token);
            }
            catch (OperationCanceledException)
            {
                Title = "EXPaste - 已停止";
            }
            catch (Exception ex)
            {
                Title = "EXPaste - 粘贴出错";
                MessageBox.Show($"粘贴过程出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Title = "EXPaste";
                _isPasting = false;
                _isPaused = false;
                _pasteContent = null;
                _pastePosition = 0;
                _pasteCurrentLine = 0;
                _pasteTotalLines = 0;
                _pasteResumeSignal?.TrySetResult(true);
                _pasteResumeSignal = null;
                _pasteCts?.Dispose();
                _pasteCts = null;
                ResetCompilerPasteState();
                SetCountdownText("");
                SetPasteButtonsForIdle();
            }
        }

        private async Task RunPasteCountdownAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (_isPaused)
                {
                    await WaitUntilResumedAsync(token);
                }

                // 每次开始倒计时都重新读取当前滑块值，确保暂停期间保存的设置立即生效（Bug1 修复）
                int countdownSeconds = Math.Clamp((int)Math.Round(CountdownSlider.Value), 1, 60);
                ViewModel.CountdownSeconds = countdownSeconds;

                bool interrupted = false;
                for (int i = countdownSeconds; i > 0; i--)
                {
                    token.ThrowIfCancellationRequested();
                    if (_isPaused) { interrupted = true; break; }

                    string countdownText = $"倒计时：{i}秒";
                    SetCountdownText(countdownText);
                    Title = $"EXPaste - {i} 秒后开始粘贴，请将光标移到目标输入框";

                    // 可中断的倒计时延迟：每 50ms 检查一次暂停，实现实时响应（Bug2 修复）
                    interrupted = await PauseAwareDelayAsync(1000, token);
                    if (interrupted) break;
                }

                if (interrupted)
                {
                    await WaitUntilResumedAsync(token);
                    continue; // 重新开始倒计时
                }
                break; // 倒计时正常结束
            }
            SetPastingProgressText();
        }

        /// <summary>
        /// 可被暂停打断的延迟。返回 true 表示因暂停而提前返回，调用方应重新开始倒计时。
        /// </summary>
        private async Task<bool> PauseAwareDelayAsync(int milliseconds, CancellationToken token)
        {
            int elapsed = 0;
            while (elapsed < milliseconds)
            {
                token.ThrowIfCancellationRequested();
                if (_isPaused) return true;
                await Task.Delay(50, token);
                elapsed += 50;
            }
            return false;
        }

        internal static int CountContentLines(string content)
        {
            if (string.IsNullOrEmpty(content)) return 0;

            int lines = 1;
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '\r')
                {
                    lines++;
                    if (i + 1 < content.Length && content[i + 1] == '\n') i++;
                }
                else if (content[i] == '\n')
                {
                    lines++;
                }
            }
            return lines;
        }

        private void SetPastingProgressText()
        {
            int totalLines = Math.Max(_pasteTotalLines, 1);
            int currentLine = Math.Clamp(_pasteCurrentLine, 1, totalLines);
            string text = $"正在粘贴第{currentLine}/{totalLines}行...";
            SetCountdownText(text);
            Title = $"EXPaste - {text}";
        }

        private void AdvancePasteLine()
        {
            if (_pastePosition < (_pasteContent?.Length ?? 0))
            {
                _pasteCurrentLine = Math.Min(_pasteCurrentLine + 1, Math.Max(_pasteTotalLines, 1));
            }
        }

        private async Task WaitUntilResumedAsync(CancellationToken token)
        {
            while (_isPaused)
            {
                token.ThrowIfCancellationRequested();
                _pasteResumeSignal ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await _pasteResumeSignal.Task.WaitAsync(token);
            }
        }

        private async Task WaitIfPausedAsync(CancellationToken token)
        {
            if (!_isPaused) return;

            await WaitUntilResumedAsync(token);
            token.ThrowIfCancellationRequested();
            await RunPasteCountdownAsync(token);
        }

        private async Task TypeContentAsync(CancellationToken token)
        {
            string content = _pasteContent ?? string.Empty;
            int speedMs = Math.Clamp(ViewModel.PasteSpeedMs, 20, 500);

            // 非编译器模式或编译器模式但未激活任何子模式时，走基础逐字粘贴
            bool isCompilerMode = !string.IsNullOrEmpty(_compilerActiveMode);

            while (_pastePosition < content.Length)
            {
                await WaitIfPausedAsync(token);
                token.ThrowIfCancellationRequested();
                SetPastingProgressText();

                char c = content[_pastePosition];

                if (c == '\r' || c == '\n')
                {
                    Win32Helper.SendKey(Win32Helper.VK_RETURN);
                    if (c == '\r' && _pastePosition + 1 < content.Length && content[_pastePosition + 1] == '\n')
                        _pastePosition += 2;
                    else
                        _pastePosition++;
                    AdvancePasteLine();

                    // 编译器模式换行后：递进行号 + 标记待执行行首仪式
                    if (isCompilerMode && _compilerFullControl)
                    {
                        _compilerPasteLine = Math.Min(_compilerPasteLine + 1, (_lineIndents?.Length ?? 1) - 1);
                        _pendingLineStartRitual = true;
                    }
                }
                else if (c == '\t')
                {
                    Win32Helper.SendKey(Win32Helper.VK_TAB);
                    _pastePosition++;
                }
                else
                {
                    // 行首仪式：按当前子模式分发
                    if (_pendingLineStartRitual && _compilerFullControl && _lineIndents != null)
                    {
                        _pendingLineStartRitual = false;
                        switch (_compilerActiveMode)
                        {
                            case "VSCode":
                                await vs_LineStartRitual(speedMs, token);
                                break;
                            case "自定义":
                                await custom_LineStartRitual(speedMs, token);
                                break;
                            default:
                                await touge_LineStartRitual(speedMs, token);
                                break;
                        }
                    }

                    Win32Helper.SendUnicodeChar(c);
                    _pastePosition++;

                    // 符号对称检测：按当前子模式分发
                    if (_compilerSymbolDetect && IsLeftPairSymbol(c))
                    {
                        switch (_compilerActiveMode)
                        {
                            case "VSCode":
                                await vs_SymbolDetect(c, speedMs, token);
                                break;
                            case "自定义":
                                await custom_SymbolDetect(c, speedMs, token);
                                break;
                            default:
                                await touge_SymbolDetect(c, speedMs, token);
                                break;
                        }
                    }
                }

                await Task.Delay(speedMs, token);
            }
        }

        private void TogglePauseResumePaste()
        {
            if (!_isPasting) return;

            if (_isPaused)
            {
                _isPaused = false;
                _pasteResumeSignal?.TrySetResult(true);
                _pasteResumeSignal = null;
                SetPasteButtonsForRunning();
            }
            else
            {
                PausePaste();
            }
        }

        private void PausePaste()
        {
            if (!_isPasting || _isPaused) return;

            _isPaused = true;
            _pasteResumeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetCountdownText("已暂停");
            Title = "EXPaste - 已暂停";
            SetPasteButtonsForPaused();
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            if (_isPasting && !_isPaused)
            {
                PausePaste();
            }
        }

        private void SetPasteButtonsForRunning()
        {
            FileStartPasteBtn.IsEnabled = true;
            TextEditStartPasteBtn.IsEnabled = true;
            CompilerStartPasteBtn.IsEnabled = true;
            FileStartPasteBtn.Content = "暂停";
            TextEditStartPasteBtn.Content = "暂停";
            CompilerStartPasteBtn.Content = "暂停";
            TextEditClearBtn.IsEnabled = false;
            CompilerClearBtn.IsEnabled = false;

            FileStopPasteBtn.IsEnabled = true;
            TextEditStopPasteBtn.IsEnabled = true;
            CompilerStopPasteBtn.IsEnabled = true;
        }

        private void SetPasteButtonsForPaused()
        {
            FileStartPasteBtn.IsEnabled = true;
            TextEditStartPasteBtn.IsEnabled = true;
            CompilerStartPasteBtn.IsEnabled = true;
            FileStartPasteBtn.Content = "继续";
            TextEditStartPasteBtn.Content = "继续";
            CompilerStartPasteBtn.Content = "继续";
            TextEditClearBtn.IsEnabled = false;
            CompilerClearBtn.IsEnabled = false;

            FileStopPasteBtn.IsEnabled = true;
            TextEditStopPasteBtn.IsEnabled = true;
            CompilerStopPasteBtn.IsEnabled = true;
        }

        private void SetPasteButtonsForIdle()
        {
            FileStartPasteBtn.IsEnabled = true;
            TextEditStartPasteBtn.IsEnabled = true;
            CompilerStartPasteBtn.IsEnabled = true;
            FileStartPasteBtn.Content = "开始粘贴";
            TextEditStartPasteBtn.Content = "开始粘贴";
            CompilerStartPasteBtn.Content = "开始粘贴";
            TextEditClearBtn.IsEnabled = true;
            CompilerClearBtn.IsEnabled = true;

            FileStopPasteBtn.IsEnabled = false;
            TextEditStopPasteBtn.IsEnabled = false;
            CompilerStopPasteBtn.IsEnabled = false;
        }

        #endregion
    }
}
