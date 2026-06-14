# Modbus 报文结构图

这份文档专门讲 Modbus TCP 和 Modbus RTU 的帧长什么样。

重点不是背公式，而是看懂每个字节在干什么。

---

## 1. Modbus TCP 报文结构

Modbus TCP 的请求大致分两部分：

```text
MBAP Header + PDU
```

### MBAP Header

```text
TransactionId(2) + ProtocolId(2) + Length(2) + UnitId(1)
```

### PDU

```text
FunctionCode(1) + Data(n)
```

### 完整结构图

```text
┌─────────────── MBAP Header ───────────────┐┌──── PDU ────┐
│ TransactionId │ ProtocolId │ Length │ UnitId │ Function │ Data │
│      2        │     2      │   2    │   1    │    1     │  n   │
└────────────────────────────────────────────┘└─────────────┘
```

### 各字段作用

- `TransactionId`
  - 用来对应请求和响应
  - 你可以理解为“流水号”

- `ProtocolId`
  - 固定为 `0`
  - 表示 Modbus TCP

- `Length`
  - 后面数据的总长度
  - 包括 `UnitId + PDU`

- `UnitId`
  - 站号
  - 在网关场景里很重要

- `FunctionCode`
  - 操作类型
  - 例如 `0x03` 读保持寄存器，`0x06` 写单个寄存器

- `Data`
  - 具体参数
  - 比如起始地址、读取数量、写入值

---

## 2. 读保持寄存器 0x03

### 请求结构

```text
FunctionCode = 0x03
Data = StartAddress(2) + Quantity(2)
```

### 请求帧示意

```text
┌────────────── MBAP Header ───────────────┐┌──────────── PDU ────────────┐
│ TransactionId │ ProtocolId │ Length │Unit│ Function=03 │ Start │ Count │
└──────────────────────────────────────────┘└─────────────────────────────┘
```

### 响应结构

```text
FunctionCode(1) + ByteCount(1) + RegisterData(n)
```

### 响应帧示意

```text
┌──────────── PDU ────────────┐
│ Function=03 │ ByteCount │ Data... │
└─────────────────────────────┘
```

### 解析规则

- `ByteCount` 表示后面有多少个字节
- 每两个字节表示一个寄存器
- 高字节在前，低字节在后

---

## 3. 写单个寄存器 0x06

### 请求结构

```text
FunctionCode = 0x06
Data = Address(2) + Value(2)
```

### 请求帧示意

```text
┌────────────── MBAP Header ───────────────┐┌──────────── PDU ────────────┐
│ TransactionId │ ProtocolId │ Length │Unit│ Function=06 │ Address │ Value │
└──────────────────────────────────────────┘└─────────────────────────────┘
```

### 响应结构

写单个寄存器时，很多设备会回传和请求几乎一样的内容：

```text
FunctionCode(1) + Address(2) + Value(2)
```

---

## 4. Modbus RTU 报文结构

Modbus RTU 没有 MBAP Header。

它的结构更短：

```text
UnitId + FunctionCode + Data + CRC16
```

### 完整结构图

```text
┌─────── RTU Frame ───────┐
│ UnitId │ Function │ Data │ CRC16 │
│   1    │    1     │  n   │  2    │
└─────────────────────────┘
```

### CRC16 位置

CRC16 是 2 个字节：

- 低字节在前
- 高字节在后

也就是：

```text
CRC Low + CRC High
```

---

## 5. RTU 读保持寄存器 0x03

### 请求帧

```text
UnitId + 03 + StartAddress(2) + Count(2) + CRC16(2)
```

### 响应帧

```text
UnitId + 03 + ByteCount + RegisterData... + CRC16(2)
```

### 字节示意

```text
[地址][功能码][字节数][数据高][数据低]...[CRC低][CRC高]
```

---

## 6. RTU 写单个寄存器 0x06

### 请求帧

```text
UnitId + 06 + Address(2) + Value(2) + CRC16(2)
```

### 响应帧

通常会回显请求内容：

```text
UnitId + 06 + Address(2) + Value(2) + CRC16(2)
```

---

## 7. TCP 和 RTU 的核心区别

### Modbus TCP

- 基于网络
- 有 MBAP Header
- 重点看 `TransactionId` 和 `Length`

### Modbus RTU

- 基于串口
- 没有 MBAP Header
- 重点看 `CRC16`

### 一句话总结

- TCP 适合看“封装和连接”
- RTU 适合看“帧和校验”

---

## 8. 对照代码看哪里

### Modbus TCP

- [ModbusTcpPlcClient.cs](./PlcDemo.Wpf/Services/Protocols/Modbus/ModbusTcpPlcClient.cs)

重点看：

- `BuildMbapFrame`
- `BuildReadHoldingRegistersPdu`
- `BuildWriteSingleRegisterPdu`
- `ParseRegisterResponse`

### Modbus RTU

- [ModbusRtuPlcClient.cs](./PlcDemo.Wpf/Services/Protocols/Modbus/ModbusRtuPlcClient.cs)

重点看：

- `BuildReadHoldingRegistersFrame`
- `BuildWriteSingleRegisterFrame`
- `ComputeCrc16`
- `ParseReadResponse`

---

## 9. 在界面里怎么看

当前项目右侧主工作区有一个“报文”标签页。

它会把最近一次读写拆成两层来显示：

1. 原始十六进制帧
2. 字段级拆解说明

### 看 TCP 时重点盯什么

- `TransactionId` 是否前后一致
- `Length` 是否正确
- `UnitId` 是否符合站号
- `FunctionCode` 是 `03` 还是 `06`

### 看 RTU 时重点盯什么

- `UnitId`
- `FunctionCode`
- `CRC16` 低字节 / 高字节顺序
- 读响应里的 `ByteCount`

---

## 10. 学习时记住这几个点

1. 寄存器地址和显示地址不一定总是一样，现场项目要确认厂家定义
2. 字节序很重要，高低字节写反就会出问题
3. TCP 和 RTU 的差别主要是“外壳”
4. 功能码决定你在做什么
5. CRC16 决定 RTU 帧是否可信

如果你把这些点和代码对上，Modbus 基础就算入门了。
