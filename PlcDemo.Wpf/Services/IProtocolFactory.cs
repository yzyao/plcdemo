using PlcDemo.Wpf.Models;

namespace PlcDemo.Wpf.Services;

public interface IProtocolFactory
{
    // 根据界面选择的协议类型创建具体客户端。
    // 学习版先用手写工厂，后续可以替换为 .NET DI 容器。
    IPlcClient Create(ProtocolKind kind);
}
