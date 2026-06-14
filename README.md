from gpt codex

# PLC 通讯学习 Demo

这是一个面向 `.NET WPF` 的 PLC 通讯学习脚手架。

目标不是一次做成完整上位机，而是把常见通讯协议的结构先搭出来，方便你按层学习：

- WPF 界面怎么组织
- MVVM 怎么把 UI 和业务分开
- PLC 通讯协议怎么抽象
- Modbus TCP 和 Modbus RTU 的差异是什么
- 真实设备接入时需要替换哪一层

## 如果你有 .NET 后端 API 经验，怎么理解这个项目

这个项目可以先按“桌面端版本的分层应用”来理解：

| ASP.NET Core API 经验 | 在这个 WPF Demo 里对应什么 | 你应该关注什么 |
| --- | --- | --- |
| Controller / Minimal API Endpoint | `Button Command` / `MainViewModel` 方法 | 用户点击按钮就是一次入口调用 |
| Request DTO / Query 参数 | `TextBox` 绑定的 `Host`、`Port`、`StartAddress` | UI 输入先是字符串，执行命令时再解析 |
| Application Service | `MainViewModel` | 编排连接、读写、日志、报警、趋势 |
| 外部系统接口 / Repository | `IPlcClient` | 把真实 PLC、模拟设备、不同协议隔离在接口后面 |
| DI 选择实现 | `ProtocolFactory` | 根据协议类型创建 `Simulation`、`ModbusTcp`、`ModbusRtu` |
| BackgroundService / 定时任务 | `DispatcherTimer` | 周期轮询设备，但运行在 UI 调度上下文里 |
| 日志 / 错误响应 | `LogEntries` / `AlarmItems` | 通讯项目里日志和报警是定位问题的主线 |
| Response Model | `ObservableCollection` / 绑定属性 | ViewModel 数据变化后界面自动刷新 |

最大的思维切换是：

- Web API 是“请求来了 -> 返回响应”
- WPF 是“用户操作或定时器触发 -> 修改 ViewModel 状态 -> UI 自动刷新”

如果你想专门按后端开发者视角学习，先看：

- [DOTNET_API_DEVELOPER_GUIDE.md](./DOTNET_API_DEVELOPER_GUIDE.md)

## 现在这套项目已经有什么

- WPF 主界面
- 协议抽象层 `IPlcClient`
- 协议工厂 `ProtocolFactory`
- 模拟设备 `SimulationPlcClient`
- Modbus TCP 学习实现
- Modbus RTU 学习实现
- 寄存器表
- 通讯日志
- 报警列表
- 自适应趋势图
- 报文解析面板
- 周期刷新 / 自动轮询
- 多个 PLC 学习案例：
  - 设备状态监控
  - 产线节拍分析
  - 安全联锁排查
  - 参数写入验证

## 你先看什么

如果你是有 .NET 后端 API 经验的新手，建议按这个顺序读：

1. 先运行 `Simulation`，不要一开始就接真实 PLC
2. 看 [DOTNET_API_DEVELOPER_GUIDE.md](./DOTNET_API_DEVELOPER_GUIDE.md)，把 WPF 概念映射到你熟悉的后端概念
3. 看 [MainWindow.xaml](./PlcDemo.Wpf/MainWindow.xaml)，理解控件如何绑定 ViewModel
4. 看 [MainViewModel.cs](./PlcDemo.Wpf/ViewModels/MainViewModel.cs)，理解按钮命令如何编排协议调用
5. 看 [PLC_CASES.md](./PLC_CASES.md)，理解几个典型 PLC 场景怎么映射到寄存器
6. 看 [IPlcClient.cs](./PlcDemo.Wpf/Services/IPlcClient.cs)，理解协议抽象层
7. 看 [ProtocolFactory.cs](./PlcDemo.Wpf/Services/ProtocolFactory.cs)，理解协议实现的选择点
8. 看 [SimulationPlcClient.cs](./PlcDemo.Wpf/Services/Protocols/Simulation/SimulationPlcClient.cs)，先把“设备读写”流程跑通
9. 看 [ModbusTcpPlcClient.cs](./PlcDemo.Wpf/Services/Protocols/Modbus/ModbusTcpPlcClient.cs)，学习 TCP 报文结构
10. 看 [ModbusRtuPlcClient.cs](./PlcDemo.Wpf/Services/Protocols/Modbus/ModbusRtuPlcClient.cs)，学习 RTU 串口帧和 CRC

如果你想按“运行时调用链”来理解，可以继续看这份导读：

- [CODE_WALKTHROUGH.md](./CODE_WALKTHROUGH.md)

如果你想先看整体关系图，可以看：

- [ARCHITECTURE.md](./ARCHITECTURE.md)

如果你想看 `MainViewModel` 的逐方法讲解，可以看：

- [MAINVIEWMODEL_GUIDE.md](./MAINVIEWMODEL_GUIDE.md)

如果你想看 Modbus 报文结构图，可以看：

- [MODBUS_FRAMES.md](./MODBUS_FRAMES.md)

如果你想按真实上位机场景学习，可以看：

- [PLC_CASES.md](./PLC_CASES.md)

## 当前项目结构

### `PlcDemo.Wpf`

WPF 程序本体，包含：

- 界面
- ViewModel
- 协议抽象
- 模拟通讯
- Modbus TCP / RTU 学习实现

### `Models`

放通讯相关的数据结构：

- `ProtocolKind`
- `ConnectionProfile`
- `ConnectionState`
- `RegisterValue`
- `AlarmItem`
- `TrendSample`
- `ProtocolFrameSnapshot`
- `PlcLearningCase`
- `StatusMetric`
- `ProcessStep`
- `InterlockItem`

### `Services`

放通讯协议和工厂：

- `IPlcClient`：统一的 PLC 通讯接口
- `IProtocolFactory`：根据协议类型创建对应实现
- `SimulationPlcClient`：模拟设备
- `ModbusTcpPlcClient`：Modbus TCP 学习实现
- `ModbusRtuPlcClient`：Modbus RTU 学习实现

### `ViewModels`

放界面逻辑：

- 连接
- 断开
- 读保持寄存器
- 写单个寄存器
- 日志显示
- 报警记录
- 趋势采样
- PLC 案例状态转换
- 监控总览 / 产线工序 / 安全联锁 / 参数验证数据组装

## 这个 Demo 怎么用

### 1. 用 Simulation 模式入门

这是最适合初学的模式。

你可以先观察：

- 切换不同 PLC 学习案例后，起始地址和读取数量怎么变化
- 点击“连接”之后状态怎么变化
- 读寄存器时日志怎么输出
- 写寄存器时数据怎么保存
- 监控总览、产线工序、安全联锁、参数验证页面怎么从寄存器推导状态
- 报警列表和趋势图怎么变化

它不依赖真实 PLC，只是模拟协议调用流程。

### 2. 用几个典型 PLC 场景学习

当前项目内置了四个案例：

| 案例 | 寄存器范围 | 学习重点 |
| --- | --- | --- |
| 设备状态监控 | HR0-HR7 | 状态码、缩放值、报警字、监控看板 |
| 产线节拍分析 | HR10-HR15 | 多工序状态、节拍、瓶颈提示 |
| 安全联锁排查 | HR20-HR25 | 实际值、期望值、是否允许启动 |
| 参数写入验证 | HR30-HR32 | 写入后读回、设定值和实际值偏差 |

建议操作顺序：

1. 协议选择 `Simulation`
2. 在左侧选择一个案例
3. 点击“套用案例到读写参数”
4. 点击“连接”
5. 点击“读取保持寄存器”
6. 切换右侧不同页面观察结果
7. 最后再看“寄存器”和“报文”，把业务展示和底层数据对上

趋势页现在做了自适应缩放，右侧空间变窄时不会裁切折线图。
它还会显示最大值、最小值和最新采样时间，方便判断当前曲线是不是正常刷新。

### 3. 再切到 Modbus TCP

Modbus TCP 适合学习：

- TCP 连接怎么建立
- MBAP 头是什么
- PDU 里放什么
- 站号 `UnitId` 的作用

你可以重点看：

- 请求帧如何拼出来
- 响应帧如何解析出来
- 异常响应怎么处理

### 4. 再看 Modbus RTU

Modbus RTU 适合学习：

- 串口通信和网络通信的区别
- RTU 帧结构
- CRC16 校验
- 读写保持寄存器的报文格式

RTU 帧的基本结构：

```text
UnitId + FunctionCode + Data + CRC16(low, high)
```

## 协议知识速记

### Modbus TCP

- 基于 TCP/IP
- 默认端口是 `502`
- 常见帧结构：
  - `MBAP Header`
  - `PDU`
- 适合局域网、网关、上位机直连

### Modbus RTU

- 基于串口
- 重点是波特率、校验位、停止位
- 数据以二进制帧传输
- 帧尾使用 `CRC16`

### 常见寄存器概念

- `Coils`：线圈，单比特，可读可写
- `Discrete Inputs`：离散输入，只读
- `Input Registers`：输入寄存器，只读
- `Holding Registers`：保持寄存器，可读可写

## 当前已实现的能力

- WPF 界面
- MVVM 命令
- 协议抽象层
- 模拟设备
- Modbus TCP 学习实现
- Modbus RTU 学习实现
- 寄存器表
- 日志面板
- 报警列表
- 自适应趋势图
- 周期刷新 / 自动轮询
- 报文解析面板
- 寄存器变化高亮
- 多案例页面：
  - 监控总览
  - 案例说明
  - 产线工序
  - 安全联锁
  - 参数验证

## 后续可以继续做什么

如果你想继续学习，我建议下一步按这个顺序扩展：

1. 把模拟实现换成真正串口/网络访问
2. 把报警字拆成 bit 级报警明细
3. 做更完整的报警确认和历史记录
4. 做配方管理和参数批量下发
5. 做更丰富的趋势缩放、多曲线对比和坐标轴刻度
6. 再扩展到别的 PLC 协议，比如：
   - S7
   - MC Protocol
   - FINS
   - Ethernet/IP

## 运行

```powershell
dotnet restore
dotnet run --project .\PlcDemo.Wpf\PlcDemo.Wpf.csproj
```

## 备注

这个项目目前重点是“学习协议结构和 WPF 分层”，不是工业现场可直接投产的通讯框架。
如果你要接真实 PLC，后面还要补：

- 超时重试
- 断线重连
- 数据缓存
- 线程安全
- 设备型号差异适配
- 日志持久化
