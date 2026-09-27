using System.IO;
using System.Windows.Input;
using EXPasteWPF.Services;
using Xunit;

namespace EXPasteWPF.Tests
{
    public class ConfigServiceTests
    {
        private static string GetTempConfigPath()
        {
            return Path.Combine(Path.GetTempPath(), $"expaste_test_{Guid.NewGuid():N}.ini");
        }

        [Fact]
        public void GestureToString_Null_ReturnsEmpty()
        {
            Assert.Equal("", ConfigService.GestureToString(null));
        }

        [Fact]
        public void GestureToString_ValidGesture_ReturnsDisplayString()
        {
            var gesture = new KeyGesture(Key.V, ModifierKeys.Control);
            string result = ConfigService.GestureToString(gesture);
            Assert.False(string.IsNullOrEmpty(result));
            Assert.Contains("V", result);
        }

        [Fact]
        public void StringToGesture_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(ConfigService.StringToGesture(null));
            Assert.Null(ConfigService.StringToGesture(""));
            Assert.Null(ConfigService.StringToGesture("   "));
        }

        [Fact]
        public void StringToGesture_InvalidText_ReturnsNull()
        {
            Assert.Null(ConfigService.StringToGesture("NotAValidGesture"));
        }

        [Fact]
        public void StringToGesture_ValidText_RoundTrips()
        {
            var original = new KeyGesture(Key.F5, ModifierKeys.Control | ModifierKeys.Shift);
            string text = ConfigService.GestureToString(original);
            var parsed = ConfigService.StringToGesture(text);

            Assert.NotNull(parsed);
            Assert.Equal(original.Key, parsed!.Key);
            Assert.Equal(original.Modifiers, parsed.Modifiers);
        }

        [Fact]
        public void AppConfig_Defaults_AreCorrect()
        {
            var cfg = new AppConfig();
            Assert.False(cfg.EnableHotkey);
            Assert.Equal("", cfg.FileHotkey);
            Assert.Equal("", cfg.TextEditHotkey);
            Assert.Equal("", cfg.CompilerHotkey);
            Assert.Equal("", cfg.PauseResumeHotkey);
            Assert.Equal(3, cfg.CountdownSeconds);
        }

        [Fact]
        public void Save_Load_RoundTrip_PreservesAllFields()
        {
            string path = GetTempConfigPath();
            try
            {
                var service = new ConfigService(path);
                var cfg = new AppConfig
                {
                    EnableHotkey = true,
                    FileHotkey = "Ctrl+F1",
                    TextEditHotkey = "Ctrl+F2",
                    CompilerHotkey = "Ctrl+F3",
                    PauseResumeHotkey = "Ctrl+F4",
                    CountdownSeconds = 10
                };

                service.Save(cfg);
                var loaded = service.Load();

                Assert.True(loaded.EnableHotkey);
                Assert.Equal("Ctrl+F1", loaded.FileHotkey);
                Assert.Equal("Ctrl+F2", loaded.TextEditHotkey);
                Assert.Equal("Ctrl+F3", loaded.CompilerHotkey);
                Assert.Equal("Ctrl+F4", loaded.PauseResumeHotkey);
                Assert.Equal(10, loaded.CountdownSeconds);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Load_MissingFile_ReturnsDefaults()
        {
            string path = GetTempConfigPath();
            try
            {
                var service = new ConfigService(path);
                var loaded = service.Load();

                Assert.False(loaded.EnableHotkey);
                Assert.Equal(3, loaded.CountdownSeconds);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Save_Load_CountdownSeconds_ClampedToValidRange()
        {
            string path = GetTempConfigPath();
            try
            {
                var service = new ConfigService(path);
                File.WriteAllText(path, "CountdownSeconds=999\n");

                var loaded = service.Load();
                Assert.Equal(60, loaded.CountdownSeconds);

                File.WriteAllText(path, "CountdownSeconds=0\n");
                loaded = service.Load();
                Assert.Equal(1, loaded.CountdownSeconds);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Load_EnableHotkey_AcceptsTrueAndOne()
        {
            string path = GetTempConfigPath();
            try
            {
                var service = new ConfigService(path);

                File.WriteAllText(path, "EnableHotkey=true\n");
                Assert.True(service.Load().EnableHotkey);

                File.WriteAllText(path, "EnableHotkey=1\n");
                Assert.True(service.Load().EnableHotkey);

                File.WriteAllText(path, "EnableHotkey=false\n");
                Assert.False(service.Load().EnableHotkey);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Load_IgnoresCommentsAndBlankLines()
        {
            string path = GetTempConfigPath();
            try
            {
                var service = new ConfigService(path);
                File.WriteAllText(path,
                    "; this is a comment\n" +
                    "# another comment\n" +
                    "\n" +
                    "   \n" +
                    "EnableHotkey=true\n");

                var loaded = service.Load();
                Assert.True(loaded.EnableHotkey);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
