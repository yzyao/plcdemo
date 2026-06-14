using PlcDemo.Wpf.Models;

namespace PlcDemo.Wpf.ViewModels;

// 把协议层的原始报文字节，整理成适合界面展示的“学习卡片”。
public static class ProtocolFramePresenter
{
    public static ProtocolFrameSnapshot CreateSnapshot(ProtocolKind selectedProtocol, ModbusFrameTrace? trace)
    {
        if (trace is null)
        {
            return selectedProtocol switch
            {
                ProtocolKind.ModbusTcp => ProtocolFrameSnapshot.CreateIdle(
                    "Modbus TCP 报文解析",
                    "已连接 Modbus TCP，但还没有抓到一笔读写记录。",
                    "先点击“读取保持寄存器”或“写入单个寄存器”，这里会显示 MBAP 头和 PDU 的拆解。"),
                ProtocolKind.ModbusRtu => ProtocolFrameSnapshot.CreateIdle(
                    "Modbus RTU 报文解析",
                    "已连接 Modbus RTU，但还没有抓到一笔读写记录。",
                    "先点击“读取保持寄存器”或“写入单个寄存器”，这里会显示串口帧、功能码和 CRC16。"),
                _ => ProtocolFrameSnapshot.CreateIdle(
                    "Simulation 模式没有真实帧",
                    "当前连接的是模拟设备，不会产生真实 TCP / 串口报文。",
                    "切换到 Modbus TCP 或 Modbus RTU 后，再执行一次读写操作即可看到真实帧结构。")
            };
        }

        return trace.Protocol switch
        {
            ProtocolKind.ModbusTcp => BuildTcpSnapshot(trace),
            ProtocolKind.ModbusRtu => BuildRtuSnapshot(trace),
            _ => ProtocolFrameSnapshot.CreateIdle(
                "Simulation 模式没有真实帧",
                "当前记录来自模拟设备，不会输出真实 Modbus 字节。",
                "切换到 Modbus TCP 或 Modbus RTU 后，再执行一次读写操作即可查看报文。")
        };
    }

    private static ProtocolFrameSnapshot BuildTcpSnapshot(ModbusFrameTrace trace)
    {
        var requestHex = FormatFrame(trace.RequestFrame);
        var responseHex = trace.ResponseFrame is null ? "暂无响应帧" : FormatFrame(trace.ResponseFrame);
        var requestFields = BuildTcpRequestFields(trace);
        var responseFields = trace.ResponseFrame is null ? Array.Empty<FrameFieldRow>() : BuildTcpResponseFields(trace);
        var summary = trace.Operation switch
        {
            ModbusOperationKind.ReadHoldingRegisters => $"读取保持寄存器 | 起始地址={trace.StartAddress} | 数量={trace.Quantity}",
            ModbusOperationKind.WriteSingleRegister => $"写入单个寄存器 | 地址={trace.StartAddress} | 值={trace.WriteValue ?? 0}",
            _ => "Modbus TCP 操作"
        };

        return new ProtocolFrameSnapshot(
            "Modbus TCP 报文解析",
            summary,
            requestHex,
            responseHex,
            trace.Notes ?? "TCP 重点看 MBAP Header 的 TransactionId、Length 和 UnitId，再看 PDU 的 FunctionCode 与数据字段。",
            requestFields,
            responseFields);
    }

    private static ProtocolFrameSnapshot BuildRtuSnapshot(ModbusFrameTrace trace)
    {
        var requestHex = FormatFrame(trace.RequestFrame);
        var responseHex = trace.ResponseFrame is null ? "暂无响应帧" : FormatFrame(trace.ResponseFrame);
        var requestFields = BuildRtuRequestFields(trace);
        var responseFields = trace.ResponseFrame is null ? Array.Empty<FrameFieldRow>() : BuildRtuResponseFields(trace);
        var summary = trace.Operation switch
        {
            ModbusOperationKind.ReadHoldingRegisters => $"读取保持寄存器 | 起始地址={trace.StartAddress} | 数量={trace.Quantity}",
            ModbusOperationKind.WriteSingleRegister => $"写入单个寄存器 | 地址={trace.StartAddress} | 值={trace.WriteValue ?? 0}",
            _ => "Modbus RTU 操作"
        };

        return new ProtocolFrameSnapshot(
            "Modbus RTU 报文解析",
            summary,
            requestHex,
            responseHex,
            trace.Notes ?? "RTU 重点看 UnitId、FunctionCode、数据字段以及 CRC16，CRC 低字节在前、高字节在后。",
            requestFields,
            responseFields);
    }

    private static IReadOnlyList<FrameFieldRow> BuildTcpRequestFields(ModbusFrameTrace trace)
    {
        var frame = trace.RequestFrame;
        var fields = new List<FrameFieldRow>
        {
            Field("TransactionId", FormatWord(ReadUInt16(frame, 0)), "事务号，用来把请求和响应配对。"),
            Field("ProtocolId", FormatWord(ReadUInt16(frame, 2)), "Modbus TCP 固定为 0x0000。"),
            Field("Length", FormatWord(ReadUInt16(frame, 4)), "后续 UnitId + PDU 的总字节数。"),
            Field("UnitId", FormatByte(frame[6]), "站号，也可以理解为从站地址。"),
            Field("FunctionCode", FormatByte(frame[7]), trace.Operation == ModbusOperationKind.ReadHoldingRegisters ? "0x03 读取保持寄存器" : "0x06 写单个寄存器"),
            Field("StartAddress", FormatWord(ReadUInt16(frame, 8)), "寄存器起始地址。")
        };

        if (trace.Operation == ModbusOperationKind.ReadHoldingRegisters)
        {
            fields.Add(Field("Quantity", FormatWord(ReadUInt16(frame, 10)), "读取数量。"));
        }
        else
        {
            fields.Add(Field("Value", FormatWord(ReadUInt16(frame, 10)), "要写入的寄存器值。"));
        }

        return fields;
    }

    private static IReadOnlyList<FrameFieldRow> BuildTcpResponseFields(ModbusFrameTrace trace)
    {
        var frame = trace.ResponseFrame!;
        var fields = new List<FrameFieldRow>
        {
            Field("TransactionId", FormatWord(ReadUInt16(frame, 0)), "响应事务号，应该和请求一致。"),
            Field("ProtocolId", FormatWord(ReadUInt16(frame, 2)), "Modbus TCP 固定为 0x0000。"),
            Field("Length", FormatWord(ReadUInt16(frame, 4)), "后续 UnitId + PDU 的总字节数。"),
            Field("UnitId", FormatByte(frame[6]), "从站地址。"),
            Field("FunctionCode", FormatByte(frame[7]), "正常响应会回显功能码。")
        };

        if (trace.Operation == ModbusOperationKind.ReadHoldingRegisters)
        {
            var byteCount = frame[8];
            fields.Add(Field("ByteCount", FormatByte(byteCount), "后续寄存器数据的字节数。"));

            for (var i = 0; i < byteCount / 2; i++)
            {
                var value = ReadUInt16(frame, 9 + i * 2);
                fields.Add(Field($"Register[{i}]", FormatWord(value), $"地址 {trace.StartAddress + i} 的返回值。"));
            }
        }
        else
        {
            fields.Add(Field("Address", FormatWord(ReadUInt16(frame, 8)), "写响应会回显寄存器地址。"));
            fields.Add(Field("Value", FormatWord(ReadUInt16(frame, 10)), "写响应会回显寄存器值。"));
        }

        return fields;
    }

    private static IReadOnlyList<FrameFieldRow> BuildRtuRequestFields(ModbusFrameTrace trace)
    {
        var frame = trace.RequestFrame;
        var fields = new List<FrameFieldRow>
        {
            Field("UnitId", FormatByte(frame[0]), "从站地址。"),
            Field("FunctionCode", FormatByte(frame[1]), trace.Operation == ModbusOperationKind.ReadHoldingRegisters ? "0x03 读取保持寄存器" : "0x06 写单个寄存器"),
            Field("StartAddress", FormatWord(ReadUInt16(frame, 2)), "寄存器起始地址。")
        };

        if (trace.Operation == ModbusOperationKind.ReadHoldingRegisters)
        {
            fields.Add(Field("Quantity", FormatWord(ReadUInt16(frame, 4)), "读取数量。"));
        }
        else
        {
            fields.Add(Field("Value", FormatWord(ReadUInt16(frame, 4)), "要写入的寄存器值。"));
        }

        fields.Add(Field("CRC16", $"{frame[6]:X2} {frame[7]:X2}", "CRC 低字节在前，高字节在后。"));
        return fields;
    }

    private static IReadOnlyList<FrameFieldRow> BuildRtuResponseFields(ModbusFrameTrace trace)
    {
        var frame = trace.ResponseFrame!;
        var fields = new List<FrameFieldRow>
        {
            Field("UnitId", FormatByte(frame[0]), "从站地址。"),
            Field("FunctionCode", FormatByte(frame[1]), "正常响应会回显功能码。")
        };

        if (trace.Operation == ModbusOperationKind.ReadHoldingRegisters)
        {
            var byteCount = frame[2];
            fields.Add(Field("ByteCount", FormatByte(byteCount), "后续寄存器数据的字节数。"));

            for (var i = 0; i < byteCount / 2; i++)
            {
                var value = ReadUInt16(frame, 3 + i * 2);
                fields.Add(Field($"Register[{i}]", FormatWord(value), $"地址 {trace.StartAddress + i} 的返回值。"));
            }

            var crcOffset = 3 + byteCount;
            fields.Add(Field("CRC16", $"{frame[crcOffset]:X2} {frame[crcOffset + 1]:X2}", "CRC 低字节在前，高字节在后。"));
        }
        else
        {
            fields.Add(Field("Address", FormatWord(ReadUInt16(frame, 2)), "写响应会回显寄存器地址。"));
            fields.Add(Field("Value", FormatWord(ReadUInt16(frame, 4)), "写响应会回显寄存器值。"));
            fields.Add(Field("CRC16", $"{frame[6]:X2} {frame[7]:X2}", "CRC 低字节在前，高字节在后。"));
        }

        return fields;
    }

    private static FrameFieldRow Field(string name, string value, string description)
        => new(name, value, description);

    private static string FormatFrame(byte[] frame)
        => string.Join(" ", frame.Select(b => b.ToString("X2")));

    private static string FormatWord(ushort value)
        => $"0x{value:X4} ({value})";

    private static string FormatByte(byte value)
        => $"0x{value:X2} ({value})";

    private static ushort ReadUInt16(byte[] frame, int offset)
        => (ushort)((frame[offset] << 8) | frame[offset + 1]);
}
