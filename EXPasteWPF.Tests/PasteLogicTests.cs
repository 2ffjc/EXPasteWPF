using System.Windows.Input;
using EXPasteWPF.Services;
using EXPasteWPF.ViewModels;
using Xunit;

namespace EXPasteWPF.Tests
{
    public class PasteLogicTests
    {
        #region StripLeadingTabs

        [Theory]
        [InlineData("", "")]
        [InlineData(null, null)]
        [InlineData("hello", "hello")]
        [InlineData("\thello", "hello")]
        [InlineData("\t\thello", "hello")]
        [InlineData("\thello\tworld", "hello\tworld")]
        public void StripLeadingTabs_SingleLine(string? input, string? expected)
        {
            Assert.Equal(expected, MainWindow.StripLeadingTabs(input!));
        }

        [Fact]
        public void StripLeadingTabs_MultipleLines_StripsEachLineLeadingTabs()
        {
            string input = "\tline1\n\t\tline2\nline3";
            string result = MainWindow.StripLeadingTabs(input);
            Assert.Equal($"line1{Environment.NewLine}line2{Environment.NewLine}line3", result);
        }

        [Fact]
        public void StripLeadingTabs_CrlfLineEnding_Preserved()
        {
            string input = "\ta\r\n\tb";
            string result = MainWindow.StripLeadingTabs(input);
            // StringBuilder.AppendLine uses Environment.NewLine, so check normalized
            Assert.Contains("a", result);
            Assert.Contains("b", result);
            Assert.DoesNotContain("\t", result);
        }

        [Fact]
        public void StripLeadingTabs_LineWithOnlyTabs_BecomesEmpty()
        {
            string input = "\t\t";
            string result = MainWindow.StripLeadingTabs(input);
            Assert.Equal("", result);
        }

        #endregion

        #region CountContentLines

        [Fact]
        public void CountContentLines_NullOrEmpty_ReturnsZero()
        {
            Assert.Equal(0, MainWindow.CountContentLines(null!));
            Assert.Equal(0, MainWindow.CountContentLines(""));
        }

        [Fact]
        public void CountContentLines_SingleLine_ReturnsOne()
        {
            Assert.Equal(1, MainWindow.CountContentLines("hello"));
        }

        [Fact]
        public void CountContentLines_LfEnding_ReturnsTwo()
        {
            Assert.Equal(2, MainWindow.CountContentLines("a\nb"));
        }

        [Fact]
        public void CountContentLines_CrLfEnding_ReturnsTwo()
        {
            Assert.Equal(2, MainWindow.CountContentLines("a\r\nb"));
        }

        [Fact]
        public void CountContentLines_CrOnlyEnding_ReturnsTwo()
        {
            Assert.Equal(2, MainWindow.CountContentLines("a\rb"));
        }

        [Fact]
        public void CountContentLines_MixedEndings_CountsCorrectly()
        {
            Assert.Equal(4, MainWindow.CountContentLines("a\nb\r\nc\rd"));
        }

        [Fact]
        public void CountContentLines_TrailingNewline_NotCountedAsExtraLine()
        {
            // "a\n" has two lines: "a" and "" after newline
            Assert.Equal(2, MainWindow.CountContentLines("a\n"));
        }

        #endregion

        #region AreGesturesEqual

        [Fact]
        public void AreGesturesEqual_BothNull_ReturnsFalse()
        {
            Assert.False(MainWindow.AreGesturesEqual(null, null));
        }

        [Fact]
        public void AreGesturesEqual_OneNull_ReturnsFalse()
        {
            var g = ConfigService.StringToGesture("Ctrl+A")!;
            Assert.False(MainWindow.AreGesturesEqual(g, null));
            Assert.False(MainWindow.AreGesturesEqual(null, g));
        }

        [Fact]
        public void AreGesturesEqual_SameKeyAndModifiers_ReturnsTrue()
        {
            var g1 = new KeyGesture(Key.V, ModifierKeys.Control);
            var g2 = new KeyGesture(Key.V, ModifierKeys.Control);
            Assert.True(MainWindow.AreGesturesEqual(g1, g2));
        }

        [Fact]
        public void AreGesturesEqual_DifferentKey_ReturnsFalse()
        {
            var g1 = new KeyGesture(Key.V, ModifierKeys.Control);
            var g2 = new KeyGesture(Key.C, ModifierKeys.Control);
            Assert.False(MainWindow.AreGesturesEqual(g1, g2));
        }

        [Fact]
        public void AreGesturesEqual_DifferentModifiers_ReturnsFalse()
        {
            var g1 = ConfigService.StringToGesture("Ctrl+V")!;
            var g2 = ConfigService.StringToGesture("Alt+V")!;
            Assert.False(MainWindow.AreGesturesEqual(g1, g2));
        }

        #endregion

        #region FormatGesture

        [Fact]
        public void FormatGesture_Null_ReturnsEmpty()
        {
            Assert.Equal("", MainWindow.FormatGesture(null));
        }

        [Fact]
        public void FormatGesture_ValidGesture_ReturnsNonEmpty()
        {
            var g = new KeyGesture(Key.V, ModifierKeys.Control);
            string result = MainWindow.FormatGesture(g);
            Assert.False(string.IsNullOrEmpty(result));
        }

        #endregion

        #region FindDuplicateHotkeyTarget

        [Fact]
        public void FindDuplicateHotkeyTarget_NoDuplicates_ReturnsNull()
        {
            var vm = new MainViewModel
            {
                FileHotkey = new KeyGesture(Key.F1),
                TextEditHotkey = new KeyGesture(Key.F2),
                CompilerHotkey = new KeyGesture(Key.F3),
                PauseResumeHotkey = new KeyGesture(Key.F4)
            };

            var candidate = new KeyGesture(Key.F5);
            Assert.Null(MainWindow.FindDuplicateHotkeyTarget(vm, candidate, "TextEdit"));
        }

        [Fact]
        public void FindDuplicateHotkeyTarget_DuplicateWithFile_ReturnsFileName()
        {
            var vm = new MainViewModel
            {
                FileHotkey = new KeyGesture(Key.F1)
            };

            var candidate = new KeyGesture(Key.F1);
            string? result = MainWindow.FindDuplicateHotkeyTarget(vm, candidate, "TextEdit");
            Assert.Equal("选择文件模式", result);
        }

        [Fact]
        public void FindDuplicateHotkeyTarget_DuplicateWithPauseResume_ReturnsName()
        {
            var vm = new MainViewModel
            {
                PauseResumeHotkey = ConfigService.StringToGesture("Ctrl+F1")
            };

            var candidate = ConfigService.StringToGesture("Ctrl+F1")!;
            string? result = MainWindow.FindDuplicateHotkeyTarget(vm, candidate, "File");
            Assert.Equal("开始/暂停/继续", result);
        }

        [Fact]
        public void FindDuplicateHotkeyTarget_SameTarget_IsNotDuplicate()
        {
            var vm = new MainViewModel
            {
                TextEditHotkey = new KeyGesture(Key.F1)
            };

            var candidate = new KeyGesture(Key.F1);
            Assert.Null(MainWindow.FindDuplicateHotkeyTarget(vm, candidate, "TextEdit"));
        }

        #endregion
    }
}
