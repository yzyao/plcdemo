using System.Globalization;

namespace PlcDemo.Wpf.Models;

// 报警级别：用于区分提示、警告和严重错误。
public enum AlarmSeverity
{
    Info,
    Warning,
    Critical
}

public static class AlarmSeverityExtensions
{
    public static string ToChineseLabel(this AlarmSeverity severity)
        => severity switch
        {
            AlarmSeverity.Info => "信息",
            AlarmSeverity.Warning => "警告",
            AlarmSeverity.Critical => "严重",
            _ => "未知"
        };
}

// 报警记录：界面只负责展示，不负责拼报警文案。
public sealed record AlarmItem(
    DateTime Timestamp,
    AlarmSeverity Severity,
    string Source,
    string Message)
{
    public string TimestampText => Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public string SeverityLabel => Severity.ToChineseLabel();
}
