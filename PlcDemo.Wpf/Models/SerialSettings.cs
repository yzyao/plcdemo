namespace PlcDemo.Wpf.Models;

public sealed record SerialSettings(
    string PortName,
    int BaudRate = 9600,
    int DataBits = 8,
    string Parity = "None",
    string StopBits = "One");
