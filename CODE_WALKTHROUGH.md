# 代码导读

这份文件不是讲所有代码细节，而是按“程序真正怎么跑起来”的顺序，把调用链串起来。

## 1. 程序从哪里开始

入口是 WPF 的 `App` 和 `MainWindow`。

- [App.xaml](./PlcDemo.Wpf/App.xaml)
- [MainWindow.xaml](./PlcDemo.Wpf/MainWindow.xaml)

`App.xaml` 负责加载资源字典和启动主窗口。
`MainWindow.xaml` 负责显示界面，并把 `MainViewModel` 作为 `DataContext`。

你可以把它理解成：

```text
程序启动 -> 加载窗口 -> 绑定 ViewModel -> 界面开始响应用户操作
```

## 2. 界面上的控件为什么能自动显示数据

关键在 `DataContext` 绑定。

在 [MainWindow.xaml](./PlcDemo.Wpf/MainWindow.xaml) 里：

- `TextBox` 绑定到 `Host`、`Port`、`UnitId` 等属性
- `ComboBox` 绑定到 `SelectedProtocol`
- `Button` 绑定到命令对象
- `ListBox` 绑定到日志集合 `LogEntries`
- `DataGrid` 绑定到寄存器、报警列表
- `Polyline` 绑定到趋势采样点
- `TabControl` 展示监控总览、案例说明、产线工序、安全联锁、参数验证等页面

这就是 WPF MVVM 的核心思路：

- View 只管展示
- ViewModel 只管状态和逻辑

## 3. 点击按钮后发生什么

点击“连接”时，执行的是 [MainViewModel.cs](./PlcDemo.Wpf/ViewModels/MainViewModel.cs) 里的 `ConnectCommand`。

它内部会做这几件事：

1. 读取界面输入
2. 解析字符串参数
3. 生成 `ConnectionProfile`
4. 通过工厂创建协议实现
5. 调用 `ConnectAsync`
6. 把结果写入日志和状态

也就是说，界面不直接知道“到底是 Modbus TCP 还是 RTU”。
它只知道：

- 当前选了什么协议
- 当前的连接参数是什么
- 交给 `IPlcClient` 去执行

## 4. 为什么要有 IPlcClient

[`IPlcClient`](./PlcDemo.Wpf/Services/IPlcClient.cs) 是整个协议层的统一接口。

它把不同协议的共同动作抽出来：

- `ConnectAsync`
- `DisconnectAsync`
- `ReadHoldingRegistersAsync`
- `WriteSingleRegisterAsync`

这样 ViewModel 不需要关心具体协议细节。

如果以后你要加别的协议，比如：

- S7
- MC Protocol
- FINS

只要实现这个接口，UI 侧基本不用改。

## 5. 工厂负责什么

[`ProtocolFactory`](./PlcDemo.Wpf/Services/ProtocolFactory.cs) 的作用很简单：

- 选 `Simulation` 就返回模拟实现
- 选 `ModbusTcp` 就返回 TCP 实现
- 选 `ModbusRtu` 就返回 RTU 实现

它的价值是把“选择协议”这件事集中起来，避免在 ViewModel 里写一堆 `if else`。

## 6. Simulation 是干什么的

[`SimulationPlcClient`](./PlcDemo.Wpf/Services/Protocols/Simulation/SimulationPlcClient.cs) 是学习用的“假设备”。

它的特点：

- 不需要真实 PLC
- 不需要网络
- 不需要串口
- 可以直接模拟读写寄存器

适合你先理解：

- 连接是什么意思
- 读取寄存器是什么意思
- 写入寄存器是什么意思
- 同一批寄存器如何驱动多个业务页面

现在它还会给几个固定地址段返回有业务含义的数据：

| 地址段 | 案例 |
| --- | --- |
| HR0-HR7 | 设备状态监控 |
| HR10-HR15 | 产线节拍分析 |
| HR20-HR25 | 安全联锁排查 |
| HR30-HR32 | 参数写入验证 |

## 7. Modbus TCP 是怎么工作的

[`ModbusTcpPlcClient`](./PlcDemo.Wpf/Services/Protocols/Modbus/ModbusTcpPlcClient.cs) 演示了 Modbus TCP 的基本结构。

它做的事情可以拆成三段：

1. 建立 TCP 连接
2. 组装请求帧
3. 读取响应帧并解析

你要重点关注这几个概念：

- `MBAP Header`
- `PDU`
- `UnitId`
- `FunctionCode`
- `TransactionId`

如果你把这几个字段搞懂，Modbus TCP 就基本入门了。

## 8. Modbus RTU 是怎么工作的

[`ModbusRtuPlcClient`](./PlcDemo.Wpf/Services/Protocols/Modbus/ModbusRtuPlcClient.cs) 演示了 RTU 的帧结构。

它和 TCP 最大的区别是：

- 没有 MBAP Header
- 依赖串口参数
- 末尾要带 CRC16

你要重点看：

- 请求帧怎么拼
- CRC 怎么算
- 读保持寄存器和写单寄存器的帧怎么区分

当前项目里的 RTU 实现偏“学习版”，重点是让你看懂报文结构。

## 9. 日志为什么重要

日志由 [MainViewModel.cs](./PlcDemo.Wpf/ViewModels/MainViewModel.cs) 里的 `LogEntries` 管理。

它的作用不是“好看”，而是帮助你观察：

- 什么时候连接成功
- 什么时候发生读写
- 协议参数有没有问题
- 设备返回了什么结果

学习通讯协议时，日志比界面控件更重要。

## 10. 自动刷新怎么挂到界面上

主界面里“刷新间隔 / 开始周期刷新 / 停止周期刷新”这一组控件，对应的是 [MainViewModel.cs](./PlcDemo.Wpf/ViewModels/MainViewModel.cs) 里的周期刷新逻辑。

它们分别对应：

- `RefreshIntervalMs`：刷新间隔输入框
- `StartAutoRefreshCommand`：开始周期刷新
- `StopAutoRefreshCommand`：停止周期刷新
- `AutoRefreshState`：当前刷新状态显示

真正执行轮询的是 `DispatcherTimer`：

- 定时器每次 Tick 时会尝试读一次保持寄存器
- 如果上一轮还没结束，就跳过这一轮
- 这样不会把读请求排成一串，适合学习“节流式轮询”

你可以把它理解成：

```text
点击开始 -> 启动定时器 -> 周期读寄存器 -> 更新表格和日志 -> 点击停止
```

## 11. 新增的寄存器表格

右侧主工作区里有一个“寄存器”标签页。

它绑定的是 [MainViewModel.cs](./PlcDemo.Wpf/ViewModels/MainViewModel.cs) 里的 `RegisterRows`。

这个表格的作用是：

- 直接显示寄存器地址
- 直接显示读到或写入的值
- 直接显示最近一次更新来源

这样你不用只看日志，也能直观看到读写结果。

## 12. 寄存器高亮怎么理解

寄存器表现在会根据最近一次更新的来源显示不同背景色。

你可以这样理解：

- `初始`：项目启动时的默认值
- `读取`：最近一次是读操作带回来的值
- `变化`：这次读到的值和表里已有值不一致
- `写入`：最近一次是写操作改掉的值

这比只看一个纯数字更容易观察“值是怎么变的”。

## 13. 报警列表怎么用

右侧主工作区里的“报警”标签页会显示：

- 时间
- 级别
- 来源
- 内容

它的典型来源有：

- 连接失败
- 写入失败
- 自动刷新跳过
- 阈值越界

这能帮助你把“现象”和“通讯过程”对上。

## 14. 多个 PLC 案例页面怎么用

右侧现在有几个更接近真实上位机的页面：

- `监控总览`：把 HR0-HR7 翻译成运行状态、产量、温度、压力、转速、良率、节拍、报警字
- `案例说明`：列出每个案例的寄存器范围和学习重点
- `产线工序`：把 HR10-HR15 翻译成上料、加工、检测、下料状态和节拍信息
- `安全联锁`：把 HR20-HR25 和期望值对比，判断是否允许启动
- `参数验证`：把 HR30-HR32 翻译成设定值、实际值、允许偏差和验证结论

这些页面背后的核心代码在 [MainViewModel.cs](./PlcDemo.Wpf/ViewModels/MainViewModel.cs)：

- `LearningCases`
- `ApplySelectedLearningCase`
- `RefreshCasePresentations`
- `RefreshMonitorOverview`
- `RefreshProductionLine`
- `RefreshSafetyInterlocks`
- `RefreshParameterValidation`

你可以把它理解成后端 API 里的“把外部系统原始数据组装成页面 DTO”。

## 15. 趋势图怎么用

右侧主工作区里的“趋势”标签页会显示：

- 折线图
- 最新采样点摘要
- 最近几个采样的列表

趋势图外层已经用 `Viewbox` 做了自适应缩放，并补了：

- 最大值
- 最小值
- 最新采样时间

当前项目里，趋势样本来自读保持寄存器成功后的采样。

如果你以后想扩展成真正的过程曲线，思路也一样：

- 先采样
- 再存历史
- 再画出来

## 16. 报文解析面板怎么用

右侧主工作区里的“报文”标签页负责展示协议报文解析。

它会展示：

- 最近一次请求帧
- 最近一次响应帧
- 字段拆解结果

如果你选的是 `Modbus TCP`，它会把帧拆成：

- `TransactionId`
- `ProtocolId`
- `Length`
- `UnitId`
- `FunctionCode`
- 具体数据字段

如果你选的是 `Modbus RTU`，它会重点显示：

- `UnitId`
- `FunctionCode`
- 数据字段
- `CRC16`

## 17. 建议的学习路线

如果你按这个项目边看边学，我建议按下面顺序：

1. 先只看 `Simulation`
2. 先套用“设备状态监控”案例并读取 HR0-HR7
3. 再看 `MainWindow.xaml`
4. 再看 `MainViewModel`
5. 再看 `PlcCaseModels`
6. 再看 `IPlcClient`
7. 再看 `ProtocolFactory`
8. 再看 `Modbus TCP`
9. 再看 `Modbus RTU`

这样你会比较容易把“界面、状态、协议、报文”串起来。

## 18. 如果你继续扩展

下一步最值得做的是：

- 更像 HMI 的寄存器监控表格
- 更完整的报警确认和历史记录
- 趋势缩放和多曲线对比
- 真实串口/网络访问

这些功能会让你从“能通信”慢慢走到“像一个真正的上位机”。
