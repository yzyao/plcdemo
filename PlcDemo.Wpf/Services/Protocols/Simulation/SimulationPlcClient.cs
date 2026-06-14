using System.Collections.Concurrent;
using PlcDemo.Wpf.Models;

namespace PlcDemo.Wpf.Services.Protocols.Simulation;

public sealed class SimulationPlcClient : IPlcClient
{
    private readonly ConcurrentDictionary<int, ushort> _registers = new();
    private int _readTick;
    private bool _connected;
    private ConnectionProfile? _profile;

    public string Name => "Simulation";

    public Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        // 模拟模式只需要记住连接参数，不做真实 IO。
        _profile = profile;
        _connected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _connected = false;
        _profile = null;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ushort>> ReadHoldingRegistersAsync(int startAddress, int count, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        // 如果某个地址没写过，就返回一个可预测的案例值，方便学习观察多个 PLC 场景。
        var snapshotTick = Interlocked.Increment(ref _readTick);
        var values = new List<ushort>(count);
        for (var offset = 0; offset < count; offset++)
        {
            var address = startAddress + offset;
            values.Add(_registers.TryGetValue(address, out var value) ? value : BuildDefaultRegisterValue(address, snapshotTick));
        }

        return Task.FromResult((IReadOnlyList<ushort>)values);
    }

    public Task WriteSingleRegisterAsync(int address, ushort value, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        _registers[address] = value;
        return Task.CompletedTask;
    }

    private void EnsureConnected()
    {
        if (!_connected)
        {
            throw new InvalidOperationException("Simulation client is not connected.");
        }
    }

    private static ushort BuildDefaultRegisterValue(int address, int tick)
        => address switch
        {
            // HR0-HR7：设备状态监控案例。
            0 => (ushort)(tick % 9 == 0 ? 2 : 1), // 0=停机，1=运行，2=待机。
            1 => (ushort)(320 + tick * 2), // 当班产量。
            2 => (ushort)(660 + tick % 8 * 3), // 温度，缩放 0.1。
            3 => (ushort)(58 + tick % 5), // 压力，缩放 0.1。
            4 => (ushort)(1380 + tick % 7 * 25), // 电机转速 rpm。
            5 => (ushort)(986 - tick % 4), // 良率，缩放 0.1。
            6 => (ushort)(4100 + tick % 6 * 120), // 节拍 ms。
            7 => 0, // 设备报警位，0 表示无报警。

            // HR10-HR15：产线节拍案例，0=待机，1=运行，2=完成，3=故障。
            10 => 2,
            11 => (ushort)(tick % 3 == 0 ? 1 : 2),
            12 => (ushort)(tick % 4 == 0 ? 1 : 2),
            13 => 1,
            14 => (ushort)(3900 + tick % 5 * 140), // 当前节拍 ms。
            15 => (ushort)(tick % 10 == 0 ? 1 : 0), // 瓶颈提示位。

            // HR20-HR25：安全联锁案例。
            20 => 1, // 安全门闭合。
            21 => 0, // 急停未按下。
            22 => 1, // 光栅无遮挡。
            23 => (ushort)(tick % 12 == 0 ? 0 : 1), // 气压偶发不足。
            24 => 1, // 伺服就绪。
            25 => 0, // 无复位请求。

            // HR30-HR32：参数写入验证案例。
            30 => 680, // 温度设定值，缩放 0.1。
            31 => (ushort)(675 + tick % 7), // 温度实际值，缩放 0.1。
            32 => 60, // 允许偏差，缩放 0.1。

            _ => (ushort)(1000 + address)
        };
}
