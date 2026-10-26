namespace EdgeShelf.Services;

public static class AppLanguage
{
    public static string Current { get; set; } = "zh-CN";
    public static bool IsEnglish => Current.Equals("en-US", StringComparison.OrdinalIgnoreCase);

    public static string Text(string key) => (IsEnglish ? English : Chinese).GetValueOrDefault(key, key);

    public static event EventHandler? Changed;

    public static void Set(string language)
    {
        var normalized = language.Equals("en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN";
        if (Current.Equals(normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Current = normalized;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static string SelectionCount(int count) => IsEnglish
        ? count == 0 ? "Win + Shift + S to capture" : $"{count} selected"
        : count == 0 ? "Win + Shift + S 截图" : $"已选 {count} 项";

    private static readonly Dictionary<string, string> Chinese = new()
    {
        ["subtitle"] = "截图与文件中转站",
        ["close"] = "关闭侧栏",
        ["dropTitle"] = "把文件拖到这里",
        ["dropDescription"] = "支持多个文件和文件夹，应用只保存原路径",
        ["pin"] = "固定 / 取消固定",
        ["remove"] = "从侧栏移除",
        ["invalid"] = "文件已失效",
        ["deleteSelected"] = "删除所选",
        ["handle"] = "点击展开侧栏",
        ["showShelf"] = "显示侧栏",
        ["retention"] = "截图保留时间",
        ["hour1"] = "1 小时",
        ["hour24"] = "24 小时",
        ["days7"] = "7 天",
        ["forever"] = "永久",
        ["startup"] = "开机启动",
        ["exit"] = "退出",
        ["language"] = "语言 / Language",
        ["chinese"] = "中文",
        ["english"] = "English",
        ["screenshot"] = "截图",
        ["file"] = "文件",
        ["previewTitle"] = "截图预览",
        ["captureHint"] = "拖动选择截图区域，Esc 取消"
    };

    private static readonly Dictionary<string, string> English = new()
    {
        ["subtitle"] = "Screenshot & file shelf",
        ["close"] = "Close shelf",
        ["dropTitle"] = "Drop files here",
        ["dropDescription"] = "Supports multiple files and folders; only paths are stored",
        ["pin"] = "Pin / Unpin",
        ["remove"] = "Remove from shelf",
        ["invalid"] = "File unavailable",
        ["deleteSelected"] = "Delete selected",
        ["handle"] = "Click to expand shelf",
        ["showShelf"] = "Show shelf",
        ["retention"] = "Screenshot retention",
        ["hour1"] = "1 hour",
        ["hour24"] = "24 hours",
        ["days7"] = "7 days",
        ["forever"] = "Forever",
        ["startup"] = "Start with Windows",
        ["exit"] = "Exit",
        ["language"] = "语言 / Language",
        ["chinese"] = "中文",
        ["english"] = "English",
        ["screenshot"] = "Screenshot",
        ["file"] = "File",
        ["previewTitle"] = "Screenshot Preview",
        ["captureHint"] = "Drag to select a screenshot area, Esc to cancel"
    };
}
