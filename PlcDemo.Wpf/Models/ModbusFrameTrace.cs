namespace PlcDemo.Wpf.Models;

// 当前只支持的两类学习操作：读保持寄存器和写单个寄存器。
public enum ModbusOperationKind
{
    ReadHoldingRegisters,
    WriteSingleRegister
}

// 协议实现层把最近一次请求/响应保留下来，给界面的报文解析面板使用。
public sealed record ModbusFrameTrace(
    ProtocolKind Protocol,
    ModbusOperationKind Operation,
    byte UnitId,
    byte[] RequestFrame,
    byte[]? ResponseFrame,
    int StartAddress,
    int Quantity,
    ushort? WriteValue = null,
    ushort? TransactionId = null,
    string? Notes = null);
