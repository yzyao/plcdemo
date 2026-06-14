using System.Globalization;

namespace PlcDemo.Wpf.Models;

// 趋势采样点：记录某个寄存器在某个时间点的值。
public sealed record TrendSample(
    DateTime Timestamp,
    int Address,
    ushort Value,
    string Source)
{
    public string TimestampText => Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public string AddressText => $"HR[{Address}]";

    public string ValueText => $"{Value} (0x{Value:X4})";
}
