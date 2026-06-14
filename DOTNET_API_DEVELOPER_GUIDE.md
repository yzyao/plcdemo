# 给 .NET 后端 API 开发者的 WPF / PLC 入门对照

这份文档假设你已经熟悉 ASP.NET Core、Controller / Minimal API、Service、DTO、后台任务、日志等概念，但刚开始接触 WPF、MVVM、PLC 通讯和 Modbus。

目标不是解释所有 WPF 细节，而是帮你把已有后端经验迁移到这个项目。

## 1. 先建立一条主线

在 Web API 里，你通常这样理解一次调用：

```text
HTTP Request
  -> Controller / Endpoint
  -> Service
  -> Repository / 外部 API
  -> Response DTO
```

在这个 WPF Demo 里，对应主线是：

```text
按钮点击 / 定时器 Tick
  -> Command
  -> MainViewModel
  -> IPlcClient
  -> Simulation / Modbus TCP / Modbus RTU
  -> ObservableCollection / 属性变化
  -> 界面自动刷新
```

最重要的差别是：WPF 不是返回一次 HTTP 响应，而是持续维护一组界面状态。

## 2. 概念对照表

| 后端 API 概念 | WPF / PLC Demo 概念 | 在项目里看哪里 |
| --- | --- | --- |
| Controller Action | `ConnectCommand`、`ReadHoldingRegistersCommand` | `MainViewModel` |
| Minimal API Handler | `RelayCommand` / `AsyncRelayCommand` 执行的方法 | `ViewModels` |
| Request DTO | `ConnectionProfile`、界面输入属性 | `Models` / `MainViewModel` |
| Application Service | `MainViewModel` | `ViewModels/MainViewModel.cs` |
| 外部系统接口 | `IPlcClient` | `Services/IPlcClient.cs` |
| 第三方 API Client | `ModbusTcpPlcClient`、`ModbusRtuPlcClient` | `Services/Protocols/Modbus` |
| Mock / Fake Service | `SimulationPlcClient` | `Services/Protocols/Simulation` |
| DI 注册不同实现 | `ProtocolFactory` | `Services/ProtocolFactory.cs` |
| BackgroundService | `DispatcherTimer` 自动刷新 | `MainViewModel` |
| ILogger | `LogEntries` | `MainViewModel` |
| Error / ProblemDetails | `AlarmItems` | `MainViewModel` |
| Response DTO / ViewModel | 绑定属性、`ObservableCollection` | `MainViewModel` / `Models` |
| 页面 DTO 组装 | `StatusMetric`、`ProcessStep`、`InterlockItem` | `Models/PlcCaseModels.cs` |

## 3. WPF 里最容易卡住的几个点

### DataContext

`MainWindow.xaml` 里设置了：

```xml
<Window.DataContext>
    <vm:MainViewModel />
</Window.DataContext>
```

这相当于告诉整个窗口：后面的 `{Binding ...}` 都默认从 `MainViewModel` 上找属性或命令。

你可以把它粗略理解成“这个页面使用哪个 ViewModel 作为数据源”。

### Binding

例如：

```xml
<TextBox Text="{Binding Host, UpdateSourceTrigger=PropertyChanged}" />
<Button Command="{Binding ConnectCommand}" />
```

含义是：

- `TextBox.Text` 和 `MainViewModel.Host` 双向同步
- 按钮点击时执行 `MainViewModel.ConnectCommand`

后端开发者可以把它理解成“框架自动帮你做 UI 输入和对象属性之间的绑定”，只是这里不是 HTTP Model Binding。

### INotifyPropertyChanged

后端里对象属性变了，调用方通常不会自动知道。

WPF 里，如果 ViewModel 实现了 `INotifyPropertyChanged`，属性变化时 UI 会收到通知并刷新。

本项目的基础实现是：

- `ViewModelBase`
- `SetProperty`
- `OnPropertyChanged`

### ObservableCollection

`ObservableCollection<T>` 适合绑定列表控件。

集合新增、删除、替换元素时，界面会自动刷新。

本项目里用在：

- `RegisterRows`
- `LogEntries`
- `AlarmItems`
- `TrendSamples`
- `StatusMetrics`
- `ProcessSteps`
- `InterlockItems`

## 4. MainViewModel 应该怎么读

不要从第一行机械读到最后一行。建议按职责读：

1. 看构造函数：它初始化协议选项、表格、日志、命令
2. 看属性：这些大多直接绑定到界面
3. 看命令：`ConnectAsync`、`DisconnectAsync`、`ReadHoldingRegistersAsync`、`WriteSingleRegisterAsync`
4. 看自动刷新：`DispatcherTimer`、`RefreshTimerTick`
5. 看结果处理：`AddLog`、`AddAlarm`、`UpdateRegisterRows`、`RecordTrendSample`
6. 看案例转换：`RefreshMonitorOverview`、`RefreshProductionLine`、`RefreshSafetyInterlocks`、`RefreshParameterValidation`

一句话理解：

```text
MainViewModel = 页面状态 + 用户操作入口 + 协议调用编排
```

它有点像后端里的 Controller 加 Application Service，但因为这是学习 Demo，没有再拆更细。

## 5. IPlcClient 为什么重要

`IPlcClient` 是协议层的统一接口：

```csharp
Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);
Task DisconnectAsync(CancellationToken cancellationToken = default);
Task<IReadOnlyList<ushort>> ReadHoldingRegistersAsync(int startAddress, int count, CancellationToken cancellationToken = default);
Task WriteSingleRegisterAsync(int address, ushort value, CancellationToken cancellationToken = default);
```

后端项目里你可能会写：

```text
IUserRepository
IPaymentClient
IErpClient
```

这里的 `IPlcClient` 也是同一类东西：把外部系统访问隔离在接口后面。

这样 `MainViewModel` 不需要知道：

- TCP 帧怎么拼
- RTU CRC 怎么算
- 模拟设备怎么存寄存器
- 以后 S7、FINS、MC Protocol 怎么实现

## 6. ProtocolFactory 怎么看

`ProtocolFactory` 现在是一个很直接的工厂：

```text
Simulation -> SimulationPlcClient
ModbusTcp  -> ModbusTcpPlcClient
ModbusRtu  -> ModbusRtuPlcClient
```

它类似后端项目里“根据配置选择某个接口实现”的位置。

这个 Demo 没有引入完整 DI 容器，是为了降低学习门槛。后面如果项目变大，可以再把它换成 `Microsoft.Extensions.DependencyInjection`。

## 7. DispatcherTimer 和 BackgroundService 的差别

你熟悉的后端定时任务可能是：

```text
BackgroundService.ExecuteAsync
PeriodicTimer
Quartz / Hangfire
```

WPF 这里用的是：

```text
DispatcherTimer
```

它的特点：

- 在 UI 调度上下文里触发
- 适合直接更新绑定属性
- 不适合做很重的阻塞任务

本项目还用了 `SemaphoreSlim`，避免手动读写和自动刷新同时打到设备层。

这点和后端里“避免同一个外部接口被并发重复调用”是同一类问题。

## 8. PLC / Modbus 入门只先抓三件事

### 第一，寄存器

先把保持寄存器 `Holding Register` 理解成设备里的地址和值：

```text
HR[0] = 123
HR[1] = 456
```

读就是按地址取值，写就是按地址改值。

### 第二，功能码

本项目先只看两个功能码：

- `0x03`：读保持寄存器
- `0x06`：写单个保持寄存器

### 第三，TCP 和 RTU 的外壳不同

Modbus TCP：

```text
MBAP Header + PDU
```

Modbus RTU：

```text
UnitId + FunctionCode + Data + CRC16
```

业务含义差不多，外层封装不同。

## 9. 怎么理解这些 PLC 案例页面

新增的几个页面可以当成“上位机页面 DTO”来理解：

| 页面 | 原始数据 | ViewModel 输出 |
| --- | --- | --- |
| 监控总览 | HR0-HR7 | `StatusMetric` 列表 |
| 产线工序 | HR10-HR15 | `ProcessStep` 列表 |
| 安全联锁 | HR20-HR25 | `InterlockItem` 列表 |
| 参数验证 | HR30-HR32 | `ParameterSummaryText` |
| 趋势 | 每次读取的首个寄存器值 | `TrendSamples`、`TrendPolylinePoints`、趋势辅助标签 |

这和后端项目里的流程类似：

```text
External API / Database 原始数据
  -> Application Service 转换
  -> Response DTO
  -> 前端展示
```

在这个 WPF Demo 里就是：

```text
PLC 寄存器
  -> MainViewModel 转换
  -> ObservableCollection / 绑定属性
  -> XAML 展示
```

趋势页有一个额外点：折线坐标仍然在 `MainViewModel` 里按固定逻辑计算，XAML 用 `Viewbox` 做自适应缩放。
这可以先让新手理解数据转换，不必一开始处理复杂的画布尺寸计算。

学习时不要只看 UI。你应该把每个页面和它背后的寄存器地址对上：

- [PLC_CASES.md](./PLC_CASES.md)
- [PlcCaseModels.cs](./PlcDemo.Wpf/Models/PlcCaseModels.cs)
- [MainViewModel.cs](./PlcDemo.Wpf/ViewModels/MainViewModel.cs)

## 10. 推荐学习路线

1. 运行程序，协议选择 `Simulation`
2. 在左侧选择“设备状态监控”，点击“套用案例到读写参数”
3. 点“连接”，再点“读取保持寄存器”
4. 看 `LogEntries` 如何变化
5. 看 `RegisterRows` 如何更新
6. 看“监控总览”如何从 HR0-HR7 推导状态
7. 再分别套用“产线节拍分析”“安全联锁排查”“参数写入验证”
8. 看 `MainWindow.xaml` 中多个 Tab 如何绑定集合
9. 看“趋势”页如何绑定 `TrendPolylinePoints` 和趋势辅助标签
10. 看 `MainViewModel.ConnectAsync`
11. 看 `RefreshCasePresentations`
12. 看 `IPlcClient`
13. 看 `SimulationPlcClient`
14. 最后再看 `ModbusTcpPlcClient` 和 `ModbusRtuPlcClient`

不要一开始就直接啃 CRC 和报文字节。先把“界面入口 -> ViewModel -> 协议接口 -> 结果回到界面”的调用链跑通。

## 11. 后续扩展建议

适合按这个顺序扩展：

1. 给 `IPlcClient` 增加取消、超时、重试策略
2. 把连接配置持久化到 JSON 文件
3. 给报警增加确认、恢复、历史查询
4. 把趋势样本保存到 SQLite
5. 使用 DI 容器替代手写 `ProtocolFactory`
6. 接真实 PLC 或串口网关验证 Modbus TCP / RTU

这些扩展会更接近真实上位机项目，同时也能复用你已有的 .NET 后端工程经验。
