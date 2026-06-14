# MainViewModel 逐方法导读

这份文档只讲一个类：[MainViewModel.cs](./PlcDemo.Wpf/ViewModels/MainViewModel.cs)。

目标是让你知道：

- 每个字段和属性是干什么的
- 每个按钮按下去之后调用了什么
- 为什么这里要用字符串保存输入
- 日志、报警和趋势是怎么更新的

## 1. 这个类负责什么

`MainViewModel` 是界面和协议层之间的中间层。

它负责：

- 保存界面状态
- 响应按钮命令
- 解析用户输入
- 创建连接参数
- 调用协议实现
- 输出日志
- 维护报警和趋势
- 维护 PLC 学习案例
- 把寄存器转换成监控总览、产线工序、安全联锁、参数验证页面数据

你可以把它理解成“WPF 界面的控制中心”。

---

## 2. 字段部分

```csharp
private readonly IProtocolFactory _protocolFactory;
private IPlcClient? _client;
```

这两个字段是最核心的。

- `_protocolFactory`：负责创建具体协议实现
- `_client`：当前真正工作的协议对象

后面的 Simulation / Modbus TCP / Modbus RTU，最后都会通过 `_client` 去执行。

### 输入相关字段

```csharp
private ProtocolKind _selectedProtocol = ProtocolKind.Simulation;
private string _host = "127.0.0.1";
private string _port = "502";
private string _unitId = "1";
private string _serialPortName = "COM1";
private string _baudRate = "9600";
private string _startAddress = "0";
private string _readCount = "4";
private string _writeValue = "1234";
```

这些字段都对应界面上的输入框。

为什么都用 `string`？

- 因为 `TextBox` 直接绑定字符串最简单
- 用户输入时可能先是半成品，比如只输入了一半数字
- 先用字符串存住，再在执行命令时解析成真正类型

这是 WPF 里很常见的做法。

### 案例相关字段

```csharp
private PlcLearningCase? _selectedLearningCase;
private string _monitorSummaryText = "...";
private string _productionSummaryText = "...";
private string _interlockSummaryText = "...";
private string _parameterSummaryText = "...";
```

这些字段服务于右侧多页面案例：

- 设备状态监控
- 产线节拍分析
- 安全联锁排查
- 参数写入验证

它们的职责不是通讯，而是把寄存器值翻译成更接近真实上位机的业务状态。

---

## 3. 构造函数

```csharp
public MainViewModel()
    : this(new ProtocolFactory())
{
}
```

这是无参构造，方便 WPF 直接创建。

```csharp
public MainViewModel(IProtocolFactory protocolFactory)
```

这是可注入构造，方便测试和后续替换工厂。

### 构造函数里做了什么

1. 初始化协议选项
2. 初始化模式选项
3. 初始化 PLC 学习案例
4. 初始化日志、报警、趋势和案例展示集合
5. 写入几条启动日志
6. 创建命令对象

这意味着窗口一打开，就已经有可绑定数据了。

---

## 4. SelectedProtocol 属性

```csharp
public ProtocolKind SelectedProtocol
```

它绑定到界面的协议下拉框。

当协议切换时，会更新 `SelectedProtocolDescription`，给学习者提示当前协议特点。

### 这里的设计点

- `Simulation`：适合入门
- `ModbusTcp`：适合看网络帧
- `ModbusRtu`：适合看串口帧和 CRC

这能让界面不只是“切换值”，还带一点说明性。

---

## 5. 连接相关属性

这些属性直接映射到界面输入框：

- `Host`
- `Port`
- `UnitId`
- `SerialPortName`
- `BaudRate`
- `StartAddress`
- `ReadCount`
- `WriteValue`

### `ConnectionState`

这个属性显示当前状态：

- `未连接`
- `连接中...`
- `已连接：...`
- `连接失败`

它会让你一眼看出当前操作是否成功。

### `SelectedMode`

它显示当前学习模式或当前套用的 PLC 案例名称。

当前会在 `ApplySelectedLearningCase` 里更新为案例标题，例如：

- 设备状态监控
- 产线节拍分析
- 安全联锁排查
- 参数写入验证

---

## 6. 报警、趋势和案例展示属性

### `AlarmItems`

报警列表，保存最近的报警记录。

### `AlarmSummaryText`

报警摘要，显示当前报警总数和最新报警。

### `TrendSamples`

趋势采样点集合。

### `TrendPolylinePoints`

折线图真正绑定的点集合。

### `TrendSummaryText`

趋势摘要，显示最近采样数量、最新值和范围。

### `TrendMaxLabel` / `TrendMinLabel` / `TrendLatestTimeLabel`

趋势图的辅助标注。

它们用于补充趋势图的上下文：

- `TrendMaxLabel`：当前采样最大值
- `TrendMinLabel`：当前采样最小值
- `TrendLatestTimeLabel`：最新采样时间

### `LearningCases`

PLC 学习案例集合，左侧“PLC 学习案例”下拉框绑定它。

每个案例会提供：

- 案例标题
- 推荐读取范围
- 建议写入值
- 寄存器映射说明
- 学习重点

### `StatusMetrics`

监控总览页面的数据源。

它把 HR0-HR7 翻译成：

- 运行状态
- 当班产量
- 温度
- 压力
- 电机转速
- 良率
- 当前节拍
- 报警字

### `ProcessSteps`

产线工序页面的数据源。

它把 HR10-HR15 翻译成上料、加工、检测、下料等工序状态。

### `InterlockItems`

安全联锁页面的数据源。

它把 HR20-HR25 的实际值和期望值做对比，判断是否允许启动。

---

## 7. 命令对象

```csharp
ConnectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, CanDisconnect);
ReadHoldingRegistersCommand = new AsyncRelayCommand(ReadHoldingRegistersAsync, CanOperate);
WriteSingleRegisterCommand = new AsyncRelayCommand(WriteSingleRegisterAsync, CanOperate);
ApplySelectedCaseCommand = new RelayCommand(...);
```

这是典型 MVVM 写法。

按钮不直接连方法，而是连命令对象。

### 命令可用性

- `CanConnect()`：没连接时才可以连接
- `CanDisconnect()`：已经连接时才可以断开
- `CanOperate()`：已经连接时才可以读写
- `ApplySelectedCaseCommand`：有选中案例时可以套用

这样界面行为会更自然。

---

## 8. ConnectAsync

这是连接按钮的核心逻辑。

### 它做了什么

1. 把状态设成 `连接中...`
2. 通过工厂创建协议对象
3. 解析界面输入
4. 组装 `ConnectionProfile`
5. 调用协议对象的 `ConnectAsync`
6. 成功后写日志
7. 失败后写报警

### 为什么要先组装 `ConnectionProfile`

因为连接参数本来就应该是一个整体。

把协议类型、主机、端口、站号、串口信息放在一个对象里，后续扩展会方便很多。

### 这里的关键点

```csharp
_client = _protocolFactory.Create(SelectedProtocol);
await _client.ConnectAsync(profile)
```

这表示：

- `MainViewModel` 只关心“我要连接”
- 具体怎么连，由协议实现负责

---

## 9. DisconnectAsync

断开逻辑比较简单：

1. 调用 `_client.DisconnectAsync()`
2. 写入日志
3. 清空 `_client`
4. 把状态改回 `未连接`
5. 刷新命令可用性

这个方法的重点不是断开本身，而是“把 UI 状态恢复干净”。

---

## 10. ReadHoldingRegistersAsync

这是“读保持寄存器”按钮对应的方法。

### 流程

1. 检查是否已连接
2. 解析起始地址
3. 解析读取数量
4. 调用 `_client.ReadHoldingRegistersAsync(...)`
5. 把返回值拼成易读文本
6. 写入日志
7. 记录趋势采样

### 为什么默认读保持寄存器

因为这是 Modbus 里最常见、最适合入门的操作之一。

学习时建议你先把这个读通，再看写。

---

## 11. WriteSingleRegisterAsync

这是“写单个寄存器”按钮对应的方法。

### 流程

1. 检查是否已连接
2. 解析寄存器地址
3. 解析写入值
4. 调用 `_client.WriteSingleRegisterAsync(...)`
5. 写日志

### 它对应什么协议功能码

Modbus 里对应的是 `0x06`。

这也是你学习 Modbus 时必须记住的一个基础功能码。

---

## 12. 自动刷新相关方法

这一部分对应界面上的“刷新间隔 / 开始周期刷新 / 停止周期刷新”。

### 相关成员

- `RefreshIntervalMs`：界面输入的刷新间隔
- `_refreshTimer`：周期触发的 `DispatcherTimer`
- `_clientOperationGate`：保证读写和刷新不会同时进入协议层
- `StartAutoRefreshCommand`：开始周期刷新按钮
- `StopAutoRefreshCommand`：停止周期刷新按钮
- `AutoRefreshState`：界面上显示的刷新状态

### `StartAutoRefresh`

这个方法负责启动周期刷新。

它会做三件事：

1. 先确认已经连接
2. 解析刷新间隔
3. 启动定时器并更新状态

这里设置了最小间隔 `200ms`，是为了避免你把界面刷得过快。

### `StopAutoRefresh`

这个方法负责停止定时器，并把状态恢复成“自动刷新已停止”。

### `RefreshTimerTick`

这是定时器每次触发时执行的方法。

它不会强行排队，而是调用 `ExecuteClientOperationAsync(..., skipIfBusy: true)`：

- 如果上一轮读还没结束，就跳过这一轮
- 如果设备很慢，也不会让请求在后台越积越多

这在学习自动轮询时很重要，因为它更接近真实 HMI 的工作方式。

### `ExecuteClientOperationAsync`

这是一个通用保护方法，负责串行化对 `_client` 的调用。

它的作用是：

- 手动读写时正常等待
- 自动刷新时尽量不堆积请求
- 不管成功还是失败，最终都会释放门闩

---

## 13. 日志、报警和趋势

### `AddLog`

把日志插到最前面。

### `AddAlarm`

记录报警，刷新报警摘要，同时也写一条日志。

### `RecordTrendSample`

记录趋势样本，更新折线点，并在值超过阈值时补一条警告报警。

### `RefreshTrendPresentation`

这一步会把趋势采样转换成折线点，并刷新最大值、最小值和最新时间标签。

如果你看到趋势图显示拥挤，通常先检查这里，而不是先怀疑 PLC 数据。

### 为什么要分三类

- 日志：看过程
- 报警：看问题
- 趋势：看变化

这是学习上位机时很重要的思路。

---

## 14. PLC 案例转换方法

### `ApplySelectedLearningCase`

这个方法不会直接读写 PLC。

它只负责把当前选中的案例参数写到界面输入项：

- `StartAddress`
- `ReadCount`
- `WriteValue`
- `SelectedMode`

这样学习者能清楚区分：

- 套用配置
- 执行通讯
- 观察结果

### `RefreshCasePresentations`

每次读写寄存器后都会调用它。

它统一刷新四类案例页面：

- `RefreshMonitorOverview`
- `RefreshProductionLine`
- `RefreshSafetyInterlocks`
- `RefreshParameterValidation`

### `RefreshMonitorOverview`

把 HR0-HR7 转成监控总览卡片。

这一步会处理状态码翻译、比例缩放和状态级别判断。

### `RefreshProductionLine`

把 HR10-HR15 转成产线工序状态。

重点是把 `0/1/2/3` 这类状态码翻译成“待机 / 运行中 / 完成 / 故障”。

### `RefreshSafetyInterlocks`

把 HR20-HR25 和期望值对比，生成安全联锁表。

如果当前选中“安全联锁排查”案例，且存在不满足条件，会写一条报警。

### `RefreshParameterValidation`

把 HR30-HR32 转成参数验证结论。

它会比较：

- 温度设定值
- 温度实际值
- 允许偏差

如果超差，会写入报警。

### `AddAlarmOnce`

这是一个轻量报警去重方法。

自动刷新时如果同一条联锁或参数报警反复出现，不会每个 Tick 都刷一条重复报警。

---

## 15. 寄存器高亮和报文面板

### `RegisterRows`

每一行现在不只是地址和值，还会带一个 `State`。

它的用途是：

- 让表格根据状态换底色
- 让你看出哪些值是新读到的
- 让你看出哪些值是刚写进去的

### `FrameSnapshot`

这是报文解析卡片绑定的数据源。

它会在这些时机刷新：

- 连接成功后
- 协议切换后
- 读保持寄存器后
- 写单个寄存器后
- 启动或停止自动刷新后

如果当前协议实现提供了 `IProtocolTraceSource`，它会显示最近一次抓到的原始报文。
如果没有，就显示学习提示文本。

---

## 16. RefreshCommandStates

这个方法负责刷新按钮可用性。

### 什么时候会调用

- 连接成功后
- 断开连接后
- 连接失败后

### 为什么需要它

因为命令的 `CanExecute` 会依赖当前连接状态。

如果不手动刷新，按钮状态可能不会立刻更新。

---

## 17. 你应该怎样读这个类

建议你按下面顺序读：

1. 先看字段
2. 再看构造函数
3. 再看属性
4. 再看四个命令方法
5. 再看 `ApplySelectedLearningCase`
6. 再看 `RefreshCasePresentations`
7. 最后看日志、报警、趋势和解析方法

这样会比从头到尾逐行啃更快。

---

## 18. 这个类的核心理解

如果只记一句话，就是：

> `MainViewModel` 把界面输入转成协议调用，再把寄存器结果转回监控、工序、联锁、参数、日志、报警和趋势。

这就是它的全部价值。
