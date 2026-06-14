# 项目架构图

这份图用最简单的方式说明当前项目里各层之间的关系。

## 总览

```text
┌─────────────────────────────────────────────────────────┐
│                         WPF View                        │
│  MainWindow.xaml                                         │
│  - 按钮                                                 │
│  - 输入框                                               │
│  - 下拉框                                               │
│  - 多页面 Tab 工作区                                    │
│  - 监控 / 工序 / 联锁 / 参数 / 日志 / 报警 / 趋势 / 报文 │
│  - 趋势页使用 Viewbox 适配窗口宽度                      │
└─────────────────────────────────────────────────────────┘
                           │
                           │ DataContext / Binding
                           ▼
┌─────────────────────────────────────────────────────────┐
│                      ViewModel 层                        │
│  MainViewModel                                          │
│  - 保存界面状态                                         │
│  - 执行连接/断开/读写命令                               │
│  - 生成日志 / 报警 / 趋势                               │
│  - 把寄存器翻译成 PLC 案例页面数据                      │
└─────────────────────────────────────────────────────────┘
                           │
                           │ 创建/调用
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    协议抽象层                            │
│  IPlcClient                                             │
│  - ConnectAsync                                         │
│  - DisconnectAsync                                      │
│  - ReadHoldingRegistersAsync                            │
│  - WriteSingleRegisterAsync                             │
└─────────────────────────────────────────────────────────┘
                           │
                           │ 由 ProtocolFactory 选择
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    协议实现层                            │
│  SimulationPlcClient                                    │
│  ModbusTcpPlcClient                                     │
│  ModbusRtuPlcClient                                     │
└─────────────────────────────────────────────────────────┘
                           │
                           │ 使用
                           ▼
┌─────────────────────────────────────────────────────────┐
│                     数据模型层                           │
│  ProtocolKind                                           │
│  ConnectionProfile                                      │
│  ConnectionState                                        │
│  RegisterValue                                          │
│  AlarmItem                                              │
│  TrendSample                                            │
│  ProtocolFrameSnapshot                                  │
│  PlcLearningCase / StatusMetric                         │
│  ProcessStep / InterlockItem                            │
│  TrendMaxLabel / TrendMinLabel / TrendLatestTimeLabel    │
└─────────────────────────────────────────────────────────┘
```

## 各层职责

### View

只负责显示和接收用户输入。

你可以把它理解成“窗口和控件”，尽量不要把业务逻辑写进这里。

### ViewModel

负责：

- 保存界面状态
- 响应按钮点击
- 执行周期刷新和自动轮询
- 组织协议调用
- 生成日志
- 维护报警和趋势数据
- 维护 PLC 学习案例
- 把寄存器值转换成监控总览、产线工序、安全联锁、参数验证页面数据

它是界面和协议之间的中间层。

### 协议抽象层

统一定义 PLC 通讯需要做的事情。

这样不同协议就可以用同一套调用方式。

### 协议实现层

真正干活的地方。

当前项目里有三种实现：

- `Simulation`：学习用模拟设备
- `Modbus TCP`
- `Modbus RTU`

### 数据模型层

放数据结构，尽量不写业务逻辑。

当前数据模型包括两类：

- 通讯模型：协议类型、连接参数、寄存器值、报警、趋势、报文快照
- 案例展示模型：PLC 学习案例、状态指标、工序状态、安全联锁项
- 趋势辅助数据：最大值、最小值、最新采样时间

这样代码会更清晰，也更适合后续扩展。

## 调用链

用户点击按钮后的顺序大致是：

```text
按钮点击 / 定时器 Tick
  -> MainViewModel 命令或自动刷新逻辑
  -> 解析输入参数
  -> ProtocolFactory 创建协议实现
  -> IPlcClient 执行连接/读写
  -> 结果写入 LogEntries / AlarmItems / TrendSamples / RegisterRows
  -> RefreshCasePresentations 把寄存器转换成案例页面数据
  -> 界面自动刷新
```

## 为什么要这样分层

因为 PLC 通讯项目很容易越写越乱。

如果不分层，很快会出现这些问题：

- XAML 里塞满业务逻辑
- 串口/TCP 细节和界面耦合
- 不同协议写成一大堆 `if else`
- 后面加新协议时到处改

现在这种结构的好处是：

- 好读
- 好改
- 好扩展
- 适合学习

## 你学的时候重点看什么

如果你只想抓主线，重点看这四条：

1. `MainWindow.xaml` 怎么绑定到 `MainViewModel`
2. `MainViewModel` 怎么调用 `IPlcClient`
3. `MainViewModel` 怎么把寄存器转换成案例页面数据
4. `ProtocolFactory` 怎么切到不同协议实现

把这四步看懂，你就能理解整个 Demo 的骨架。
