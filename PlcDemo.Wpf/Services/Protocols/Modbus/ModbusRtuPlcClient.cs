using PlcDemo.Wpf.Models;

namespace PlcDemo.Wpf.Services.Protocols.Modbus;

public sealed class ModbusRtuPlcClient : IPlcClient, IProtocolTraceSource
{
    private readonly Dictionary<int, ushort> _registers = new();
    private bool _connected;
    private ConnectionProfile? _profile;
    private ModbusFrameTrace? _lastTrace;

    public string Name => "Modbus RTU";

    public ModbusFrameTrace? LastTrace => _lastTrace;

    public Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        // 当前实现用于学习协议流程，先把连接状态和参数保存下来。
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

        // RTU 的请求帧是：UnitId + FunctionCode + Data + CRC16。
        var request = BuildReadHoldingRegistersFrame(_profile?.UnitId ?? 1, (ushort)startAddress, (ushort)count);
        ValidateCrc(request);
        var response = SimulateDeviceResponse(request);
        ValidateCrc(response);
        _lastTrace = new ModbusFrameTrace(
            ProtocolKind.ModbusRtu,
            ModbusOperationKind.ReadHoldingRegisters,
            _profile?.UnitId ?? 1,
            request,
            response,
            startAddress,
            count,
            null,
            null,
            "RTU 重点看 UnitId、FunctionCode、数据字段以及 CRC16，CRC 低字节在前、高字节在后。");
        return Task.FromResult((IReadOnlyList<ushort>)ParseReadResponse(response));
    }

    public Task WriteSingleRegisterAsync(int address, ushort value, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var request = BuildWriteSingleRegisterFrame(_profile?.UnitId ?? 1, (ushort)address, value);
        ValidateCrc(request);

        // 这里没有真正打开串口，而是先做一个“模拟设备响应”。
        var response = SimulateDeviceResponse(request);
        _lastTrace = new ModbusFrameTrace(
            ProtocolKind.ModbusRtu,
            ModbusOperationKind.WriteSingleRegister,
            _profile?.UnitId ?? 1,
            request,
            response,
            address,
            1,
            value,
            null,
            "RTU 写单寄存器时，响应通常回显地址和值，CRC 也要重新校验。");
        _registers[address] = value;
        return Task.CompletedTask;
    }

    public static byte[] BuildReadHoldingRegistersFrame(byte unitId, ushort startAddress, ushort count)
        => BuildRequestFrame(unitId, 0x03, startAddress, count);

    public static byte[] BuildWriteSingleRegisterFrame(byte unitId, ushort address, ushort value)
        => BuildRequestFrame(unitId, 0x06, address, value);

    private static byte[] BuildRequestFrame(byte unitId, byte functionCode, ushort first, ushort second)
    {
        var frame = new byte[8];
        frame[0] = unitId;
        frame[1] = functionCode;
        frame[2] = (byte)(first >> 8);
        frame[3] = (byte)(first & 0xFF);
        frame[4] = (byte)(second >> 8);
        frame[5] = (byte)(second & 0xFF);
        var crc = ComputeCrc16(frame.AsSpan(0, 6));
        // Modbus RTU 的 CRC 低字节在前，高字节在后。
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)(crc >> 8);
        return frame;
    }

    public static ushort ComputeCrc16(ReadOnlySpan<byte> data)
    {
        // 标准 Modbus CRC16，初始值 0xFFFF，右移计算，多项式 0xA001。
        ushort crc = 0xFFFF;

        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                var lsb = (crc & 0x0001) != 0;
                crc >>= 1;
                if (lsb)
                {
                    crc ^= 0xA001;
                }
            }
        }

        return crc;
    }

    public static string FormatFrame(byte[] frame)
        => string.Join(" ", frame.Select(b => b.ToString("X2")));

    private static void ValidateCrc(byte[] frame)
    {
        if (frame.Length < 3)
        {
            throw new InvalidOperationException("Modbus RTU frame is too short.");
        }

        var expected = ComputeCrc16(frame.AsSpan(0, frame.Length - 2));
        var actual = (ushort)(frame[^2] | (frame[^1] << 8));
        if (expected != actual)
        {
            throw new InvalidOperationException($"Modbus RTU CRC mismatch. expected={expected:X4}, actual={actual:X4}");
        }
    }

    private byte[] SimulateDeviceResponse(byte[] request)
    {
        // 这是学习版模拟设备，不通过串口 IO，而是直接根据请求生成响应。
        var unitId = request[0];
        var functionCode = request[1];

        if (functionCode == 0x03)
        {
            var startAddress = ReadUInt16(request, 2);
            var count = ReadUInt16(request, 4);
            var payload = new byte[3 + count * 2];
            payload[0] = unitId;
            payload[1] = functionCode;
            payload[2] = (byte)(count * 2);

            for (var i = 0; i < count; i++)
            {
                var address = startAddress + i;
                var value = _registers.TryGetValue(address, out var existing)
                    ? existing
                    : (ushort)(2000 + address);
                payload[3 + i * 2] = (byte)(value >> 8);
                payload[4 + i * 2] = (byte)(value & 0xFF);
            }

            return AppendCrc(payload);
        }

        if (functionCode == 0x06)
        {
            var address = ReadUInt16(request, 2);
            var value = ReadUInt16(request, 4);
            _registers[address] = value;
            var response = new byte[8];
            Buffer.BlockCopy(request, 0, response, 0, 6);
            var crc = ComputeCrc16(response.AsSpan(0, 6));
            response[6] = (byte)(crc & 0xFF);
            response[7] = (byte)(crc >> 8);
            return response;
        }

        throw new InvalidOperationException($"Unsupported Modbus RTU function: {functionCode:X2}");
    }

    private static byte[] AppendCrc(byte[] payload)
    {
        var response = new byte[payload.Length + 2];
        Buffer.BlockCopy(payload, 0, response, 0, payload.Length);
        var crc = ComputeCrc16(payload);
        response[^2] = (byte)(crc & 0xFF);
        response[^1] = (byte)(crc >> 8);
        return response;
    }

    private static ushort ReadUInt16(byte[] buffer, int offset)
        => (ushort)((buffer[offset] << 8) | buffer[offset + 1]);

    private static IReadOnlyList<ushort> ParseReadResponse(byte[] response)
    {
        if (response.Length < 5)
        {
            throw new InvalidOperationException("Invalid Modbus RTU response length.");
        }

        // 第 3 个字节是数据字节数，后面每两个字节对应一个寄存器。
        var byteCount = response[2];
        if (response.Length != byteCount + 5)
        {
            throw new InvalidOperationException("Unexpected Modbus RTU response payload length.");
        }

        var values = new ushort[byteCount / 2];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadUInt16(response, 3 + i * 2);
        }

        return values;
    }

    private void EnsureConnected()
    {
        if (!_connected)
        {
            throw new InvalidOperationException("Modbus RTU client is not connected.");
        }
    }
}
