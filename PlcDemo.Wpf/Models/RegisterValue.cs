namespace PlcDemo.Wpf.Models;

// 用于表格展示单个寄存器的地址、值和最近一次更新来源。
public sealed partial record RegisterValue(
    int Address,
    ushort Value,
    string Source = "初始值",
    RegisterValueState State = RegisterValueState.Initial);

public static class RegisterValueStateExtensions
{
    public static string ToChineseLabel(this RegisterValueState state)
        => state switch
        {
            RegisterValueState.Initial => "初始",
            RegisterValueState.Read => "读取",
            RegisterValueState.Changed => "变化",
            RegisterValueState.Written => "写入",
            _ => "未知"
        };
}

public sealed partial record RegisterValue
{
    public string StateLabel => State.ToChineseLabel();
}
