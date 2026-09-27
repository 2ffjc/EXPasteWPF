namespace EXPasteWPF.Models
{
    /// <summary>
    /// 文本项模型
    /// </summary>
    internal class TextItem
    {
        public string Content { get; set; } = "";
        public string? SourcePath { get; set; }
        public bool IsCompilerMode { get; set; }
    }
}
