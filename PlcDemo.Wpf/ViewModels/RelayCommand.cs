using System.Windows.Input;

namespace PlcDemo.Wpf.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action execute)
        : this(_ => execute(), null)
    {
    }

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    // WPF 按钮会先问 CanExecute，决定按钮是否可点击。
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    // 用户点击按钮时，WPF 会调用 Execute。
    public void Execute(object? parameter) => _execute(parameter);

    // 连接状态变化后主动通知按钮重新判断 CanExecute。
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
