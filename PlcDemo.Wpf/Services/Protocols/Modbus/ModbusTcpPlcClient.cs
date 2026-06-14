using System.IO;
using System.Net.Sockets;
using System.Text;
using PlcDemo.Wpf.Models;

namespace PlcDemo.Wpf.Services.Protocols.Modbus;

public sealed class ModbusTcpPlcClient : IPlcClient, IProtocolTraceSource
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private ushort _transactionId;
    private ConnectionProfile? _profile;
    private ModbusFrameTrace? _lastTrace;

    public string Name => "Modbus TCP";

    public ModbusFrameTrace? LastTrace => _lastTrace;

    public async Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        // 先断开旧连接，再建立新连接，避免重复占用同一个 socket。
        await DisconnectAsync(cancellationToken).ConfigureAwait(false);
        _profile = profile;
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(profile.Host, profile.Port, cancellationToken).ConfigureAwait(false);
        _stream = _tcpClient.GetStream();
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _stream?.Dispose();
        _tcpClient?.Close();
        _stream = null;
        _tcpClient = null;
        _profile = null;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ushort>> ReadHoldingRegistersAsync(int startAddress, int count, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        // Modbus TCP 的请求体核心是 PDU，外面再包一层 MBAP Header。
        var pdu = BuildReadHoldingRegistersPdu(startAddress, count);
        var exchange = await SendAndReceiveAsync(pdu, cancellationToken).ConfigureAwait(false);
        _lastTrace = BuildTrace(ModbusOperationKind.ReadHoldingRegisters, startAddress, count, null, exchange);
        return ParseRegisterResponse(exchange.ResponseBody);
    }

    public async Task WriteSingleRegisterAsync(int address, ushort value, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var pdu = BuildWriteSingleRegisterPdu(address, value);
        var exchange = await SendAndReceiveAsync(pdu, cancellationToken).ConfigureAwait(false);
        _lastTrace = BuildTrace(ModbusOperationKind.WriteSingleRegister, address, 1, value, exchange);
    }

    private byte[] BuildReadHoldingRegistersPdu(int startAddress, int count)
    {
        var pdu = new byte[5];
        pdu[0] = 0x03;
        WriteUInt16(pdu, 1, (ushort)startAddress);
        WriteUInt16(pdu, 3, (ushort)count);
        return pdu;
    }

    private byte[] BuildWriteSingleRegisterPdu(int address, ushort value)
    {
        var pdu = new byte[5];
        pdu[0] = 0x06;
        WriteUInt16(pdu, 1, (ushort)address);
        WriteUInt16(pdu, 3, value);
        return pdu;
    }

    private async Task<ModbusTcpExchange> SendAndReceiveAsync(byte[] pdu, CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new InvalidOperationException("Modbus TCP client is not connected.");
        var unitId = _profile?.UnitId ?? 1;
        var transactionId = unchecked(++_transactionId);
        var requestFrame = BuildMbapFrame(transactionId, unitId, pdu);

        // 写请求帧到网络流。
        await stream.WriteAsync(requestFrame, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // 先读 MBAP Header，再按 Length 读取后续 PDU。
        var header = await ReadExactAsync(stream, 7, cancellationToken).ConfigureAwait(false);
        var length = ReadUInt16(header, 4);
        var body = await ReadExactAsync(stream, length - 1, cancellationToken).ConfigureAwait(false);
        var responseFrame = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, responseFrame, 0, header.Length);
        Buffer.BlockCopy(body, 0, responseFrame, header.Length, body.Length);

        if (ReadUInt16(header, 0) != transactionId)
        {
            throw new InvalidOperationException("Modbus transaction mismatch.");
        }

        if (body.Length == 0)
        {
            throw new InvalidOperationException("Empty Modbus response.");
        }

        if ((body[0] & 0x80) != 0)
        {
            var errorCode = body.Length > 1 ? body[1] : (byte)0;
            throw new InvalidOperationException($"Modbus exception response: function={body[0]:X2}, code={errorCode:X2}");
        }

        return new ModbusTcpExchange(requestFrame, responseFrame, body, transactionId, unitId);
    }

    private static byte[] BuildMbapFrame(ushort transactionId, byte unitId, byte[] pdu)
    {
        var frame = new byte[7 + pdu.Length];
        // MBAP Header:
        // TransactionId(2) + ProtocolId(2) + Length(2) + UnitId(1)
        WriteUInt16(frame, 0, transactionId);
        WriteUInt16(frame, 2, 0);
        WriteUInt16(frame, 4, (ushort)(pdu.Length + 1));
        frame[6] = unitId;
        Buffer.BlockCopy(pdu, 0, frame, 7, pdu.Length);
        return frame;
    }

    private ModbusFrameTrace BuildTrace(
        ModbusOperationKind operation,
        int startAddress,
        int quantity,
        ushort? writeValue,
        ModbusTcpExchange exchange)
    {
        var notes = operation == ModbusOperationKind.ReadHoldingRegisters
            ? "TCP 重点看 MBAP Header 的 TransactionId、Length 和 UnitId，再看 PDU 的 FunctionCode 与数据字段。"
            : "TCP 写单寄存器时，PDU 里会回显地址和值，便于核对写入是否成功。";

        return new ModbusFrameTrace(
            ProtocolKind.ModbusTcp,
            operation,
            exchange.UnitId,
            exchange.RequestFrame,
            exchange.ResponseFrame,
            startAddress,
            quantity,
            writeValue,
            exchange.TransactionId,
            notes);
    }

    private static IReadOnlyList<ushort> ParseRegisterResponse(byte[] response)
    {
        if (response[0] != 0x03)
        {
            throw new InvalidOperationException($"Unexpected Modbus function code: {response[0]:X2}");
        }

        // response[1] 是字节数，后面每两个字节表示一个寄存器。
        var byteCount = response[1];
        if (byteCount + 2 != response.Length)
        {
            throw new InvalidOperationException("Invalid Modbus register response length.");
        }

        var values = new ushort[byteCount / 2];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadUInt16(response, 2 + i * 2);
        }

        return values;
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }

    private static ushort ReadUInt16(byte[] buffer, int offset)
        => (ushort)((buffer[offset] << 8) | buffer[offset + 1]);

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        // 网络流读取不保证一次读满，所以这里要循环读到指定字节数。
        var buffer = new byte[count];
        var read = 0;

        while (read < count)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(read, count - read), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("The Modbus TCP connection was closed unexpectedly.");
            }

            read += bytesRead;
        }

        return buffer;
    }

    private void EnsureConnected()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Modbus TCP client is not connected.");
        }
    }

    private sealed record ModbusTcpExchange(
        byte[] RequestFrame,
        byte[] ResponseFrame,
        byte[] ResponseBody,
        ushort TransactionId,
        byte UnitId);
}
