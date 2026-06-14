using PlcDemo.Wpf.Models;

namespace PlcDemo.Wpf.Services;

// 可选的报文追踪接口。
// 不是所有模拟实现都必须提供，但 Modbus TCP / RTU 会把最近一次报文挂在这里。
public interface IProtocolTraceSource
{
    ModbusFrameTrace? LastTrace { get; }
}
