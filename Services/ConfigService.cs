using System;
using System.IO;
using System.Text;
using System.Windows.Input;

namespace EXPasteWPF.Services
{
    /// <summary>
    /// 配置服务：读写 INI 配置文件，保存快捷键等设置
    /// </summary>
    internal class ConfigService
    {
        private readonly string _configPath;

        public ConfigService()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EXPaste");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            _configPath = Path.Combine(dir, "config.ini");
        }

        /// <summary>
        /// 仅供测试使用：使用指定路径读写配置，避免污染真实配置文件
        /// </summary>
        internal ConfigService(string configPath)
        {
            _configPath = configPath;
        }

        public string ConfigPath => _configPath;

        /// <summary>
        /// 读取配置
        /// </summary>
        public AppConfig Load()
        {
            var cfg = new AppConfig();
            if (!File.Exists(_configPath)) return cfg;

            foreach (var rawLine in File.ReadAllLines(_configPath, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "EnableHotkey":
                        cfg.EnableHotkey = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        break;
                    case "FileHotkey":
                        cfg.FileHotkey = value;
                        break;
                    case "TextEditHotkey":
                        cfg.TextEditHotkey = value;
                        break;
                    case "CompilerHotkey":
                        cfg.CompilerHotkey = value;
                        break;
                    case "PauseResumeHotkey":
                        cfg.PauseResumeHotkey = value;
                        break;
                    case "CountdownSeconds":
                        if (int.TryParse(value, out int cd)) cfg.CountdownSeconds = Math.Clamp(cd, 1, 60);
                        break;
                    case "PasteSpeedMs":
                        if (int.TryParse(value, out int ps)) cfg.PasteSpeedMs = Math.Clamp(ps, 20, 500);
                        break;
                    case "CompilerQuickMode":
                        cfg.CompilerQuickMode = (value == "VSCode" || value == "自定义") ? value : "头歌";
                        break;
                    case "TougeFullControl":
                        cfg.TougeFullControl = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        break;
                    case "TougeSymbolDetect":
                        cfg.TougeSymbolDetect = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        break;
                    case "VsFullControl":
                        cfg.VsFullControl = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        break;
                    case "VsSymbolDetect":
                        cfg.VsSymbolDetect = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        break;
                    case "CustomFullControl":
                        cfg.CustomFullControl = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        break;
                    case "CustomSymbolDetect":
                        cfg.CustomSymbolDetect = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        break;
                }
            }
            return cfg;
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void Save(AppConfig cfg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; EXPaste 配置文件");
            sb.AppendLine("[Hotkey]");
            sb.AppendLine($"EnableHotkey={(cfg.EnableHotkey ? "true" : "false")}");
            sb.AppendLine($"FileHotkey={cfg.FileHotkey ?? ""}");
            sb.AppendLine($"TextEditHotkey={cfg.TextEditHotkey ?? ""}");
            sb.AppendLine($"CompilerHotkey={cfg.CompilerHotkey ?? ""}");
            sb.AppendLine($"PauseResumeHotkey={cfg.PauseResumeHotkey ?? ""}");
            sb.AppendLine($"CountdownSeconds={cfg.CountdownSeconds}");
            sb.AppendLine($"PasteSpeedMs={cfg.PasteSpeedMs}");
            sb.AppendLine($"CompilerQuickMode={cfg.CompilerQuickMode}");
            sb.AppendLine($"TougeFullControl={(cfg.TougeFullControl ? "true" : "false")}");
            sb.AppendLine($"TougeSymbolDetect={(cfg.TougeSymbolDetect ? "true" : "false")}");
            sb.AppendLine($"VsFullControl={(cfg.VsFullControl ? "true" : "false")}");
            sb.AppendLine($"VsSymbolDetect={(cfg.VsSymbolDetect ? "true" : "false")}");
            sb.AppendLine($"CustomFullControl={(cfg.CustomFullControl ? "true" : "false")}");
            sb.AppendLine($"CustomSymbolDetect={(cfg.CustomSymbolDetect ? "true" : "false")}");

            File.WriteAllText(_configPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 将 KeyGesture 转为可持久化字符串
        /// </summary>
        public static string GestureToString(KeyGesture? gesture)
        {
            if (gesture == null) return "";
            if (!string.IsNullOrWhiteSpace(gesture.DisplayString)) return gesture.DisplayString;

            var converter = new KeyGestureConverter();
            return converter.ConvertToString(gesture) ?? "";
        }

        /// <summary>
        /// 将字符串解析为 KeyGesture（失败返回 null）
        /// </summary>
        public static KeyGesture? StringToGesture(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var converter = new KeyGestureConverter();
                return converter.ConvertFromString(text) as KeyGesture;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 应用配置模型
    /// </summary>
    internal class AppConfig
    {
        public bool EnableHotkey { get; set; } = false;
        public string? FileHotkey { get; set; } = "";
        public string? TextEditHotkey { get; set; } = "";
        public string? CompilerHotkey { get; set; } = "";
        public string? PauseResumeHotkey { get; set; } = "";
        public int CountdownSeconds { get; set; } = 3;
        public int PasteSpeedMs { get; set; } = 50;
        public string CompilerQuickMode { get; set; } = "头歌";
        public bool TougeFullControl { get; set; } = true;
        public bool TougeSymbolDetect { get; set; } = true;
        public bool VsFullControl { get; set; } = false;
        public bool VsSymbolDetect { get; set; } = false;
        public bool CustomFullControl { get; set; } = false;
        public bool CustomSymbolDetect { get; set; } = false;
    }
}
