using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlcDemo.Wpf.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // WPF 绑定依赖属性变更通知：属性改了以后，界面才知道需要刷新。
    // 这和 Web API 返回一次 DTO 不同，桌面端 ViewModel 会长期维护可变化的状态。
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    // 通知绑定系统指定属性已经变更。
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
