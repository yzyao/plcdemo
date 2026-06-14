namespace PlcDemo.Wpf.Models;

// 场景状态级别：用于让监控卡片、工序和联锁项显示不同语义。
public enum CaseStatusLevel
{
    Idle,
    Running,
    Normal,
    Warning,
    Critical
}

public static class CaseStatusLevelExtensions
{
    public static string ToChineseLabel(this CaseStatusLevel level)
        => level switch
        {
            CaseStatusLevel.Idle => "待机",
            CaseStatusLevel.Running => "运行",
            CaseStatusLevel.Normal => "正常",
            CaseStatusLevel.Warning => "注意",
            CaseStatusLevel.Critical => "异常",
            _ => "未知"
        };
}

// 一个 PLC 学习案例。它本质上是预设寄存器范围 + 学习目标。
public sealed record PlcLearningCase(
    string Title,
    string Scenario,
    int StartAddress,
    int ReadCount,
    ushort SuggestedWriteValue,
    string RegisterMap,
    string Focus,
    string Tip)
{
    public string ReadRangeText => $"HR[{StartAddress}] - HR[{StartAddress + ReadCount - 1}]";

    public string SuggestedWriteText => $"{SuggestedWriteValue} (0x{SuggestedWriteValue:X4})";
}

// 监控总览中的指标卡片，例如运行状态、产量、温度、压力。
public sealed record StatusMetric(
    string Label,
    string Value,
    string Unit,
    string Description,
    CaseStatusLevel Level)
{
    public string LevelLabel => Level.ToChineseLabel();
}

// 产线工序状态，例如上料、加工、检测、下料。
public sealed record ProcessStep(
    string Name,
    string AddressText,
    string ValueText,
    string Description,
    CaseStatusLevel Level)
{
    public string LevelLabel => Level.ToChineseLabel();
}

// 安全联锁状态，例如安全门、急停、光栅、气压。
public sealed record InterlockItem(
    string Name,
    string AddressText,
    string ActualText,
    string ExpectedText,
    bool IsOk,
    string Description)
{
    public string ResultText => IsOk ? "允许" : "禁止";

    public CaseStatusLevel Level => IsOk ? CaseStatusLevel.Normal : CaseStatusLevel.Critical;

    public string LevelLabel => Level.ToChineseLabel();
}
