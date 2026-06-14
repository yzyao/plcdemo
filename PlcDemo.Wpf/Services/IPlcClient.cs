using PlcDemo.Wpf.Models;

namespace PlcDemo.Wpf.Services;

// PLC 通讯统一接口。
// 对有 .NET 后端经验的人来说，它类似 IPaymentClient、IErpClient、IRepository 这类外部系统抽象：
// ViewModel 只依赖接口，不直接依赖 TCP、串口或具体 PLC 协议细节。
public interface IPlcClient
{
    string Name { get; }

    // 建立连接。真实设备里通常会在这里打开 TCP 连接或串口。
    Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);

    // 断开连接。调用方不关心底层是 Socket、SerialPort 还是模拟对象。
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    // 读取保持寄存器，对应 Modbus 常用功能码 0x03。
    Task<IReadOnlyList<ushort>> ReadHoldingRegistersAsync(int startAddress, int count, CancellationToken cancellationToken = default);

    // 写单个保持寄存器，对应 Modbus 常用功能码 0x06。
    Task WriteSingleRegisterAsync(int address, ushort value, CancellationToken cancellationToken = default);
}
