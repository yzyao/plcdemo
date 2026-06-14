namespace PlcDemo.Wpf.Models;

// 报文字段的展示模型，用于把字节内容拆成“字段名 + 值 + 说明”。
public sealed record FrameFieldRow(string Name, string Value, string Description);

// 统一的报文解析面板数据，界面只需要绑定这个对象就能展示请求和响应。
public sealed record ProtocolFrameSnapshot(
    string Title,
    string Summary,
    string RequestHex,
    string ResponseHex,
    string Notes,
    IReadOnlyList<FrameFieldRow> RequestFields,
    IReadOnlyList<FrameFieldRow> ResponseFields)
{
    public static ProtocolFrameSnapshot Empty { get; } = CreateIdle(
        "报文解析",
        "先执行一次读写操作。",
        "Simulation 模式不会输出真实 Modbus 帧；切到 Modbus TCP 或 Modbus RTU 后，这里会显示请求帧和响应帧。");

    public static ProtocolFrameSnapshot CreateIdle(string title, string summary, string notes)
        => new(
            title,
            summary,
            "暂无请求帧",
            "暂无响应帧",
            notes,
            Array.Empty<FrameFieldRow>(),
            Array.Empty<FrameFieldRow>());
}
