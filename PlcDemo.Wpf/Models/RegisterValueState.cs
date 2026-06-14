namespace PlcDemo.Wpf.Models;

// 寄存器行的视觉状态，用于在表格里突出显示刚发生变化的值。
public enum RegisterValueState
{
    Initial,
    Read,
    Changed,
    Written
}
