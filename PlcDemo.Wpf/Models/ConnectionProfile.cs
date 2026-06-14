namespace PlcDemo.Wpf.Models;

public sealed record ConnectionProfile(
    ProtocolKind Protocol,
    string Host,
    int Port,
    byte UnitId,
    string? SerialPortName = null,
    int BaudRate = 9600,
    int DataBits = 8,
    string Parity = "None",
    string StopBits = "One");
