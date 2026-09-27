using System.Windows;

namespace EXPasteWPF.Services
{
    /// <summary>
    /// 剪贴板服务
    /// </summary>
    internal static class ClipboardService
    {
        /// <summary>
        /// 获取剪贴板文本
        /// </summary>
        public static string? GetText()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 设置剪贴板文本
        /// </summary>
        public static void SetText(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                // 忽略剪贴板被占用的情况
            }
        }
    }
}
