using PlcDemo.Wpf.Models;
using PlcDemo.Wpf.Services.Protocols.Modbus;
using PlcDemo.Wpf.Services.Protocols.Simulation;

namespace PlcDemo.Wpf.Services;

public sealed class ProtocolFactory : IProtocolFactory
{
    // 根据协议类型返回不同实现，类似后端项目里“根据配置选择某个外部服务客户端”。
    // 后面扩展 S7、MC Protocol、FINS 时也走这里，避免 ViewModel 里堆 if/else。
    public IPlcClient Create(ProtocolKind kind) =>
        kind switch
        {
            ProtocolKind.ModbusTcp => new ModbusTcpPlcClient(),
            ProtocolKind.ModbusRtu => new ModbusRtuPlcClient(),
            _ => new SimulationPlcClient()
        };
}
