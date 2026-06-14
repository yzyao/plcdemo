using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PlcDemo.Wpf.Models;
using PlcDemo.Wpf.Services;

namespace PlcDemo.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private const int LogLimit = 200;
    private const int AlarmLimit = 50;
    private const int TrendSampleLimit = 24;
    private const double TrendCanvasWidth = 1000d;
    private const double TrendCanvasHeight = 320d;
    private const double TrendLeftMargin = 72d;
    private const double TrendRightMargin = 28d;
    private const double TrendTopMargin = 28d;
    private const double TrendBottomMargin = 44d;
    private const ushort TrendAlarmThreshold = 1200;

    private readonly IProtocolFactory _protocolFactory;
    // 串行化协议调用，避免手动读写和自动轮询同时进入设备层。
    private readonly SemaphoreSlim _clientOperationGate = new(1, 1);
    // UI 线程上的定时器，用于周期读取寄存器。
    private readonly DispatcherTimer _refreshTimer;
    private IPlcClient? _client;

    // 下面这些字段对应界面上的输入项，统一用字符串保存，方便直接绑定到 TextBox。
    private ProtocolKind _selectedProtocol = ProtocolKind.Simulation;
    private string _host = "127.0.0.1";
    private string _port = "502";
    private string _unitId = "1";
    private string _serialPortName = "COM1";
    private string _baudRate = "9600";
    private string _startAddress = "0";
    private string _readCount = "4";
    private string _writeValue = "1234";
    private string _refreshIntervalMs = "1000";
    private string _connectionState = "未连接";
    private string _autoRefreshState = "未开始自动刷新";
    private string _selectedMode = "演示读取";
    private string _selectedProtocolDescription = "Simulation 适合学习协议调用流程，不依赖真实 PLC。";
    private ProtocolFrameSnapshot _frameSnapshot = ProtocolFrameSnapshot.Empty;
    private PointCollection _trendPolylinePoints = new();
    private PlcLearningCase? _selectedLearningCase;
    private string _trendSummaryText = "暂无趋势采样，执行一次读取后会自动绘制。";
    private string _trendMaxLabel = "最大值";
    private string _trendMinLabel = "最小值";
    private string _trendLatestTimeLabel = "时间";
    private string _alarmSummaryText = "暂无报警，连接失败、自动刷新跳过和阈值越界会出现在这里。";
    private string _monitorSummaryText = "还没有采样数据。先套用一个案例，再连接并读取。";
    private string _productionSummaryText = "还没有产线节拍数据。";
    private string _interlockSummaryText = "还没有安全联锁数据。";
    private string _parameterSummaryText = "还没有参数验证数据。";
    private string? _lastInterlockAlarmText;
    private string? _lastParameterAlarmText;
    // 记录周期刷新是否正在运行，决定按钮可用性和状态文案。
    private bool _isAutoRefreshing;

    public MainViewModel()
        : this(new ProtocolFactory())
    {
    }

    public MainViewModel(IProtocolFactory protocolFactory)
    {
        _protocolFactory = protocolFactory;
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background);
        _refreshTimer.Tick += RefreshTimerTick;

        ProtocolOptions = new ObservableCollection<ProtocolKind>(Enum.GetValues<ProtocolKind>());
        ModeOptions = new ObservableCollection<string>
        {
            "演示读取",
            "实际设备"
        };
        LearningCases = new ObservableCollection<PlcLearningCase>
        {
            new(
                "设备状态监控",
                "模拟一台设备的运行状态、产量、温度、压力、转速、良率和报警字。",
                0,
                8,
                1,
                "HR0=运行状态，HR1=当班产量，HR2=温度x0.1，HR3=压力x0.1，HR4=转速，HR5=良率x0.1，HR6=节拍ms，HR7=报警字。",
                "学习如何把一组寄存器翻译成监控看板。",
                "先连接 Simulation，再读取 HR0-HR7，观察监控总览如何刷新。"),
            new(
                "产线节拍分析",
                "模拟上料、加工、检测、下料四个工序的状态和节拍瓶颈。",
                10,
                6,
                2,
                "HR10-HR13=四个工序状态，HR14=当前节拍ms，HR15=瓶颈提示位。",
                "学习多工序状态码、节拍和瓶颈提示的展示方式。",
                "套用后读取 HR10-HR15，重点看“产线工序”页面。"),
            new(
                "安全联锁排查",
                "模拟安全门、急停、光栅、气压、伺服就绪和复位请求。",
                20,
                6,
                1,
                "HR20=安全门，HR21=急停，HR22=光栅，HR23=气压，HR24=伺服就绪，HR25=复位请求。",
                "学习如何把离散条件组合成“是否允许启动”的判断。",
                "读取 HR20-HR25，观察不满足条件时报警和联锁表如何提示。"),
            new(
                "参数写入验证",
                "模拟温度设定值、实际值和允许偏差，用于练习写入后验证。",
                30,
                3,
                700,
                "HR30=温度设定x0.1，HR31=温度实际x0.1，HR32=允许偏差x0.1。",
                "学习写寄存器后如何再读回来验证参数是否生效。",
                "套用后先写入 HR30，再读取 HR30-HR32，观察验证结论。")
        };
        _selectedLearningCase = LearningCases[0];

        // 预置一段寄存器数据，表格打开时就能看到内容。
        RegisterRows = new ObservableCollection<RegisterValue>(
            Enumerable.Range(0, 16).Select(address => new RegisterValue(address, 0, "初始值")));

        LogEntries = new ObservableCollection<string>();
        AlarmItems = new ObservableCollection<AlarmItem>();
        TrendSamples = new ObservableCollection<TrendSample>();
        StatusMetrics = new ObservableCollection<StatusMetric>();
        ProcessSteps = new ObservableCollection<ProcessStep>();
        InterlockItems = new ObservableCollection<InterlockItem>();

        AddLog("项目已初始化。");
        AddLog("建议先用 Simulation 模式理解连接、读、写、报警和趋势的完整流程。");
        AddLog("可以在右侧“案例说明”里套用不同 PLC 场景。");
        AddLog("寄存器表默认显示 0-15 号地址，读写后会自动更新。");
        AddLog("周期刷新默认 1000 ms，可手动启动或停止。");
        AddLog("Modbus TCP: MBAP + PDU；Modbus RTU: 地址 + 功能码 + 数据 + CRC16。");
        RefreshTrendPresentation();
        RefreshAlarmSummary();
        RefreshFrameSnapshot();
        RefreshCasePresentations();

        ConnectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, CanDisconnect);
        ReadHoldingRegistersCommand = new AsyncRelayCommand(ReadHoldingRegistersAsync, CanOperate);
        WriteSingleRegisterCommand = new AsyncRelayCommand(WriteSingleRegisterAsync, CanOperate);
        StartAutoRefreshCommand = new RelayCommand(_ => StartAutoRefresh(), _ => CanStartAutoRefresh());
        StopAutoRefreshCommand = new RelayCommand(_ => StopAutoRefresh(), _ => CanStopAutoRefresh());
        ApplySelectedCaseCommand = new RelayCommand(_ => ApplySelectedLearningCase(), _ => SelectedLearningCase is not null);
    }

    public ObservableCollection<ProtocolKind> ProtocolOptions { get; }

    public ObservableCollection<string> ModeOptions { get; }

    public ObservableCollection<PlcLearningCase> LearningCases { get; }

    // ObservableCollection 适合绑定表格/列表；集合变化后 UI 会自动刷新。
    public ObservableCollection<RegisterValue> RegisterRows { get; }

    public ObservableCollection<string> LogEntries { get; }

    public ObservableCollection<AlarmItem> AlarmItems { get; }

    public ObservableCollection<TrendSample> TrendSamples { get; }

    public ObservableCollection<StatusMetric> StatusMetrics { get; }

    public ObservableCollection<ProcessStep> ProcessSteps { get; }

    public ObservableCollection<InterlockItem> InterlockItems { get; }

    public PointCollection TrendPolylinePoints
    {
        get => _trendPolylinePoints;
        private set => SetProperty(ref _trendPolylinePoints, value);
    }

    public string TrendSummaryText
    {
        get => _trendSummaryText;
        private set => SetProperty(ref _trendSummaryText, value);
    }

    public string TrendMaxLabel
    {
        get => _trendMaxLabel;
        private set => SetProperty(ref _trendMaxLabel, value);
    }

    public string TrendMinLabel
    {
        get => _trendMinLabel;
        private set => SetProperty(ref _trendMinLabel, value);
    }

    public string TrendLatestTimeLabel
    {
        get => _trendLatestTimeLabel;
        private set => SetProperty(ref _trendLatestTimeLabel, value);
    }

    public string AlarmSummaryText
    {
        get => _alarmSummaryText;
        private set => SetProperty(ref _alarmSummaryText, value);
    }

    public string MonitorSummaryText
    {
        get => _monitorSummaryText;
        private set => SetProperty(ref _monitorSummaryText, value);
    }

    public string ProductionSummaryText
    {
        get => _productionSummaryText;
        private set => SetProperty(ref _productionSummaryText, value);
    }

    public string InterlockSummaryText
    {
        get => _interlockSummaryText;
        private set => SetProperty(ref _interlockSummaryText, value);
    }

    public string ParameterSummaryText
    {
        get => _parameterSummaryText;
        private set => SetProperty(ref _parameterSummaryText, value);
    }

    public ProtocolKind SelectedProtocol
    {
        get => _selectedProtocol;
        set
        {
            if (SetProperty(ref _selectedProtocol, value))
            {
                // 切换协议时，更新说明文字，帮助学习者理解当前协议的特点。
                SelectedProtocolDescription = value switch
                {
                    ProtocolKind.ModbusTcp => "Modbus TCP 通过 TCP 502 端口访问寄存器，适合练习报文封装和事务号。",
                    ProtocolKind.ModbusRtu => "Modbus RTU 通过串口发送帧，重点是站号、功能码、CRC16 和串口参数。",
                    _ => "Simulation 适合学习协议调用流程，不依赖真实 PLC。"
                };
                RefreshFrameSnapshot();
            }
        }
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string UnitId
    {
        get => _unitId;
        set => SetProperty(ref _unitId, value);
    }

    public string SerialPortName
    {
        get => _serialPortName;
        set => SetProperty(ref _serialPortName, value);
    }

    public string BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    public string StartAddress
    {
        get => _startAddress;
        set => SetProperty(ref _startAddress, value);
    }

    public string ReadCount
    {
        get => _readCount;
        set => SetProperty(ref _readCount, value);
    }

    public string WriteValue
    {
        get => _writeValue;
        set => SetProperty(ref _writeValue, value);
    }

    public string RefreshIntervalMs
    {
        get => _refreshIntervalMs;
        set => SetProperty(ref _refreshIntervalMs, value);
    }

    public string ConnectionState
    {
        get => _connectionState;
        private set => SetProperty(ref _connectionState, value);
    }

    public string AutoRefreshState
    {
        get => _autoRefreshState;
        private set => SetProperty(ref _autoRefreshState, value);
    }

    public string SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    public string SelectedProtocolDescription
    {
        get => _selectedProtocolDescription;
        private set => SetProperty(ref _selectedProtocolDescription, value);
    }

    public PlcLearningCase? SelectedLearningCase
    {
        get => _selectedLearningCase;
        set
        {
        if (SetProperty(ref _selectedLearningCase, value))
        {
            _lastInterlockAlarmText = null;
            _lastParameterAlarmText = null;
            ApplySelectedCaseCommand.RaiseCanExecuteChanged();
        }
        }
    }

    public ProtocolFrameSnapshot FrameSnapshot
    {
        get => _frameSnapshot;
        private set => SetProperty(ref _frameSnapshot, value);
    }

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand DisconnectCommand { get; }

    public AsyncRelayCommand ReadHoldingRegistersCommand { get; }

    public AsyncRelayCommand WriteSingleRegisterCommand { get; }

    public RelayCommand StartAutoRefreshCommand { get; }

    public RelayCommand StopAutoRefreshCommand { get; }

    public RelayCommand ApplySelectedCaseCommand { get; }

    private bool CanConnect() => _client is null;

    private bool CanDisconnect() => _client is not null;

    private bool CanOperate() => _client is not null;

    private bool CanStartAutoRefresh() => _client is not null && !_isAutoRefreshing;

    private bool CanStopAutoRefresh() => _isAutoRefreshing;

    private async Task ConnectAsync()
    {
        try
        {
            // 类似后端 Controller 收到请求后先组装 DTO，再交给 Service/Client。
            ConnectionState = "连接中...";
            _client = _protocolFactory.Create(SelectedProtocol);

            // 从界面字符串解析成实际参数，便于后续接真实设备时做校验。
            var unitId = ParseByte(UnitId, nameof(UnitId));
            var port = ParseInt(Port, nameof(Port));
            var profile = new ConnectionProfile(
                SelectedProtocol,
                Host,
                port,
                unitId,
                SerialPortName,
                ParseInt(BaudRate, nameof(BaudRate)));

            await _client.ConnectAsync(profile).ConfigureAwait(true);
            ConnectionState = $"已连接：{_client.Name}";
            AutoRefreshState = "未开始自动刷新";
            ResetSessionHistory();

            if (SelectedProtocol == ProtocolKind.ModbusRtu)
            {
                // RTU 模式下强调串口参数和帧结构。
                AddLog($"连接成功：协议={_client.Name}，串口={SerialPortName}@{BaudRate}，站号={unitId}");
                AddLog("RTU frame = UnitId + FunctionCode + Data + CRC16(low, high)");
            }
            else
            {
                AddLog($"连接成功：协议={_client.Name}，地址={Host}，端口={port}，站号={unitId}");
            }

            RefreshFrameSnapshot();
        }
        catch (Exception ex)
        {
            ConnectionState = "连接失败";
            AddAlarm(AlarmSeverity.Critical, "连接", $"连接失败：{ex.Message}");
            _client = null;
            RefreshFrameSnapshot();
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    private async Task DisconnectAsync()
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            StopAutoRefresh();
            await _client.DisconnectAsync().ConfigureAwait(true);
            AddLog("已断开连接。");
        }
        catch (Exception ex)
        {
            AddAlarm(AlarmSeverity.Warning, "断开", $"断开连接时发生异常：{ex.Message}");
        }
        finally
        {
            _client = null;
            ConnectionState = "未连接";
            AutoRefreshState = "未开始自动刷新";
            RefreshFrameSnapshot();
            RefreshCommandStates();
        }
    }

    private async Task ReadHoldingRegistersAsync()
    {
        // 读操作从按钮入口进入后统一走串行化保护，避免和自动刷新抢同一个设备连接。
        await ExecuteClientOperationAsync(() => ReadHoldingRegistersCoreAsync("手动读取")).ConfigureAwait(true);
    }

    private async Task WriteSingleRegisterAsync()
    {
        // 写操作同样串行化，真实 PLC 通讯通常不建议并发写同一个连接。
        await ExecuteClientOperationAsync(WriteSingleRegisterCoreAsync).ConfigureAwait(true);
    }

    private static int ParseInt(string value, string name)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new FormatException($"{name} 不是有效的整数。");
    }

    private static byte ParseByte(string value, string name)
    {
        if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new FormatException($"{name} 必须在 0 到 255 之间。");
    }

    private static ushort ParseUShort(string value, string name)
    {
        if (ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new FormatException($"{name} 必须在 0 到 65535 之间。");
    }

    private void AddLog(string message)
    {
        // 日志最新内容放在最前面，方便快速查看最近操作。
        var timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        LogEntries.Insert(0, $"[{timestamp}] {message}");
        TrimCollection(LogEntries, LogLimit);
    }

    private void AddAlarm(AlarmSeverity severity, string source, string message)
    {
        // 报警列表同样按最新优先显示，便于定位问题源头。
        var alarm = new AlarmItem(DateTime.Now, severity, source, message);
        AlarmItems.Insert(0, alarm);
        TrimCollection(AlarmItems, AlarmLimit);
        AlarmSummaryText = BuildAlarmSummaryText();
        AddLog($"报警[{alarm.SeverityLabel}] {source}：{message}");
    }

    private async Task ExecuteClientOperationAsync(Func<Task> operation, bool skipIfBusy = false)
    {
        if (skipIfBusy)
        {
            // 自动刷新只尝试一次，不排队，避免上一轮还没结束就堆积下一轮读请求。
            if (!await _clientOperationGate.WaitAsync(0).ConfigureAwait(true))
            {
                AddAlarm(AlarmSeverity.Warning, "自动刷新", "自动刷新跳过一次：上一轮通讯尚未完成。");
                return;
            }
        }
        else
        {
            await _clientOperationGate.WaitAsync().ConfigureAwait(true);
        }

        try
        {
            await operation().ConfigureAwait(true);
        }
        finally
        {
            _clientOperationGate.Release();
        }
    }

    private async Task ReadHoldingRegistersCoreAsync(string sourceLabel)
    {
        try
        {
            if (_client is null)
            {
                return;
            }

            var startAddress = ParseInt(StartAddress, nameof(StartAddress));
            var count = Math.Max(1, ParseInt(ReadCount, nameof(ReadCount)));

            // 读保持寄存器是 Modbus 学习里最常见的读操作。
            var registers = await _client.ReadHoldingRegistersAsync(startAddress, count).ConfigureAwait(true);
            UpdateRegisterRows(startAddress, registers, sourceLabel);
            RefreshCasePresentations();

            if (registers.Count > 0)
            {
                RecordTrendSample(startAddress, registers[0], sourceLabel);
            }

            var text = string.Join(", ", registers.Select((register, index) => $"{startAddress + index}={register}"));
            AddLog($"{sourceLabel} HR[{startAddress}, {count}] => {text}");
            RefreshFrameSnapshot();
        }
        catch (Exception ex)
        {
            AddAlarm(AlarmSeverity.Critical, sourceLabel, $"{sourceLabel}失败：{ex.Message}");
        }
    }

    private async Task WriteSingleRegisterCoreAsync()
    {
        try
        {
            if (_client is null)
            {
                return;
            }

            var address = ParseInt(StartAddress, nameof(StartAddress));
            var value = ParseUShort(WriteValue, nameof(WriteValue));

            // 单个寄存器写入对应 Modbus 0x06 功能码。
            await _client.WriteSingleRegisterAsync(address, value).ConfigureAwait(true);
            UpsertRegisterRow(address, value, "写入", RegisterValueState.Written);
            RefreshCasePresentations();
            AddLog($"写入 HR[{address}] = {value}");
            RefreshFrameSnapshot();
        }
        catch (Exception ex)
        {
            AddAlarm(AlarmSeverity.Critical, "写入", $"写入失败：{ex.Message}");
        }
    }

    private void StartAutoRefresh()
    {
        if (_client is null)
        {
            AddAlarm(AlarmSeverity.Warning, "自动刷新", "请先连接设备，再启动周期刷新。");
            return;
        }

        try
        {
            // 读取间隔过短时会让界面和设备都很忙，这里给一个最小值。
            var intervalMs = Math.Max(200, ParseInt(RefreshIntervalMs, nameof(RefreshIntervalMs)));
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
            _refreshTimer.Start();
            _isAutoRefreshing = true;
            AutoRefreshState = $"自动刷新中，间隔 {intervalMs} ms";
            AddLog($"自动刷新已启动，间隔 {intervalMs} ms。");
            RefreshFrameSnapshot();
            RefreshCommandStates();
        }
        catch (Exception ex)
        {
            AddAlarm(AlarmSeverity.Warning, "自动刷新", $"启动自动刷新失败：{ex.Message}");
        }
    }

    private void StopAutoRefresh()
    {
        if (!_isAutoRefreshing)
        {
            return;
        }

        _refreshTimer.Stop();
        _isAutoRefreshing = false;
        AutoRefreshState = "自动刷新已停止";
        AddLog("自动刷新已停止。");
        RefreshFrameSnapshot();
        RefreshCommandStates();
    }

    private void ApplySelectedLearningCase()
    {
        if (SelectedLearningCase is null)
        {
            return;
        }

        // 套用案例只是改变读写输入参数，不直接发起通讯；这样学习者能看到“配置”和“执行”的边界。
        StartAddress = SelectedLearningCase.StartAddress.ToString(CultureInfo.InvariantCulture);
        ReadCount = SelectedLearningCase.ReadCount.ToString(CultureInfo.InvariantCulture);
        WriteValue = SelectedLearningCase.SuggestedWriteValue.ToString(CultureInfo.InvariantCulture);
        SelectedMode = SelectedLearningCase.Title;
        AddLog($"已套用案例：{SelectedLearningCase.Title}，读取范围 {SelectedLearningCase.ReadRangeText}。");
        RefreshCasePresentations();
    }

    private async void RefreshTimerTick(object? sender, EventArgs e)
    {
        if (_client is null)
        {
            StopAutoRefresh();
            return;
        }

        try
        {
            // Tick 触发时只发起一次读操作，忙就跳过这一轮。
            await ExecuteClientOperationAsync(() => ReadHoldingRegistersCoreAsync("自动刷新"), skipIfBusy: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AddAlarm(AlarmSeverity.Critical, "自动刷新", $"自动刷新失败：{ex.Message}");
        }
    }

    private void UpdateRegisterRows(int startAddress, IReadOnlyList<ushort> registers, string source)
    {
        for (var i = 0; i < registers.Count; i++)
        {
            var address = startAddress + i;
            var existing = RegisterRows.FirstOrDefault(row => row.Address == address);
            var state = existing is null
                ? RegisterValueState.Read
                : existing.Value == registers[i]
                    ? existing.State == RegisterValueState.Written
                        ? RegisterValueState.Written
                        : RegisterValueState.Read
                    : RegisterValueState.Changed;
            UpsertRegisterRow(address, registers[i], source, state);
        }
    }

    private void UpsertRegisterRow(int address, ushort value, string source, RegisterValueState state)
    {
        // 保持表格按地址升序，方便观察寄存器范围。
        var insertIndex = 0;
        while (insertIndex < RegisterRows.Count && RegisterRows[insertIndex].Address < address)
        {
            insertIndex++;
        }

        var newRow = new RegisterValue(address, value, source, state);

        if (insertIndex < RegisterRows.Count && RegisterRows[insertIndex].Address == address)
        {
            RegisterRows[insertIndex] = newRow;
            return;
        }

        RegisterRows.Insert(insertIndex, newRow);
    }

    private void RefreshCasePresentations()
    {
        // 读写寄存器后统一刷新所有案例视图。
        // 这相当于后端里把外部系统原始数据组装成多个页面 DTO。
        RefreshMonitorOverview();
        RefreshProductionLine();
        RefreshSafetyInterlocks();
        RefreshParameterValidation();
    }

    private void RefreshMonitorOverview()
    {
        // HR0-HR7 对应“设备状态监控”案例。
        StatusMetrics.Clear();

        var runState = GetRegisterValue(0);
        var output = GetRegisterValue(1);
        var temperature = GetRegisterValue(2);
        var pressure = GetRegisterValue(3);
        var rpm = GetRegisterValue(4);
        var yield = GetRegisterValue(5);
        var cycleMs = GetRegisterValue(6);
        var alarmWord = GetRegisterValue(7);

        StatusMetrics.Add(new StatusMetric("运行状态", TranslateRunState(runState), string.Empty, "HR0：0=停机，1=运行，2=待机", runState == 1 ? CaseStatusLevel.Running : CaseStatusLevel.Idle));
        StatusMetrics.Add(new StatusMetric("当班产量", output.ToString(CultureInfo.InvariantCulture), "pcs", "HR1：产量计数", output > 0 ? CaseStatusLevel.Normal : CaseStatusLevel.Warning));
        StatusMetrics.Add(new StatusMetric("温度", FormatScaledDecimal(temperature), "°C", "HR2：温度值，按 0.1 缩放", temperature is >= 650 and <= 750 ? CaseStatusLevel.Normal : CaseStatusLevel.Warning));
        StatusMetrics.Add(new StatusMetric("压力", FormatScaledDecimal(pressure), "bar", "HR3：压力值，按 0.1 缩放", pressure is >= 55 and <= 65 ? CaseStatusLevel.Normal : CaseStatusLevel.Warning));
        StatusMetrics.Add(new StatusMetric("电机转速", rpm.ToString(CultureInfo.InvariantCulture), "rpm", "HR4：主轴或电机转速", rpm is >= 1200 and <= 1800 ? CaseStatusLevel.Normal : CaseStatusLevel.Warning));
        StatusMetrics.Add(new StatusMetric("良率", FormatScaledDecimal(yield), "%", "HR5：良率，按 0.1 缩放", yield >= 980 ? CaseStatusLevel.Normal : CaseStatusLevel.Warning));
        StatusMetrics.Add(new StatusMetric("当前节拍", cycleMs.ToString(CultureInfo.InvariantCulture), "ms", "HR6：单件生产节拍", cycleMs <= 5000 ? CaseStatusLevel.Normal : CaseStatusLevel.Warning));
        StatusMetrics.Add(new StatusMetric("报警字", $"0x{alarmWord:X4}", string.Empty, "HR7：位域报警，0 表示无报警", alarmWord == 0 ? CaseStatusLevel.Normal : CaseStatusLevel.Critical));

        MonitorSummaryText = alarmWord == 0
            ? $"设备{TranslateRunState(runState)}，产量 {output} pcs，良率 {FormatScaledDecimal(yield)}%。"
            : $"报警字非 0：0x{alarmWord:X4}，需要查看报警位定义。";
    }

    private void RefreshProductionLine()
    {
        // HR10-HR15 对应“产线节拍分析”案例。
        ProcessSteps.Clear();

        AddProcessStep("上料", 10, "物料进入工位，常见信号来自传感器或气缸到位。");
        AddProcessStep("加工", 11, "设备执行主加工动作，通常最影响节拍。");
        AddProcessStep("检测", 12, "视觉、称重、压力或尺寸检测。");
        AddProcessStep("下料", 13, "成品离开设备，影响整线流转。");

        var cycleMs = GetRegisterValue(14);
        var bottleneck = GetRegisterValue(15);
        ProductionSummaryText = bottleneck == 0
            ? $"当前节拍 {cycleMs} ms，暂无瓶颈提示。"
            : $"当前节拍 {cycleMs} ms，HR15 提示存在瓶颈，需要查看工序状态。";
    }

    private void AddProcessStep(string name, int address, string description)
    {
        var value = GetRegisterValue(address);
        ProcessSteps.Add(new ProcessStep(
            name,
            $"HR[{address}]",
            $"{value} / {TranslateStepState(value)}",
            description,
            value switch
            {
                1 => CaseStatusLevel.Running,
                2 => CaseStatusLevel.Normal,
                3 => CaseStatusLevel.Critical,
                _ => CaseStatusLevel.Idle
            }));
    }

    private void RefreshSafetyInterlocks()
    {
        // HR20-HR25 对应“安全联锁排查”案例。
        InterlockItems.Clear();

        AddInterlock("安全门闭合", 20, 1, "安全门没有闭合时，不应允许启动设备。");
        AddInterlock("急停释放", 21, 0, "急停按下时必须禁止动作。");
        AddInterlock("光栅无遮挡", 22, 1, "人员或物体遮挡光栅时，设备应停止。");
        AddInterlock("气压正常", 23, 1, "气压不足会导致气缸动作不到位。");
        AddInterlock("伺服就绪", 24, 1, "伺服未就绪时不能启动自动循环。");
        AddInterlock("无复位请求", 25, 0, "复位请求存在时，通常需要先完成复位流程。");

        var blocked = InterlockItems.Where(item => !item.IsOk).ToArray();
        InterlockSummaryText = blocked.Length == 0
            ? "全部联锁满足，可以进入启动条件判断。"
            : $"当前有 {blocked.Length} 个联锁不满足：{string.Join("、", blocked.Select(item => item.Name))}。";

        if (blocked.Length > 0 && SelectedLearningCase?.Title == "安全联锁排查")
        {
            AddAlarmOnce(ref _lastInterlockAlarmText, AlarmSeverity.Warning, "安全联锁", InterlockSummaryText);
        }
        else if (blocked.Length == 0)
        {
            _lastInterlockAlarmText = null;
        }
    }

    private void AddInterlock(string name, int address, ushort expected, string description)
    {
        var actual = GetRegisterValue(address);
        InterlockItems.Add(new InterlockItem(
            name,
            $"HR[{address}]",
            actual.ToString(CultureInfo.InvariantCulture),
            expected.ToString(CultureInfo.InvariantCulture),
            actual == expected,
            description));
    }

    private void RefreshParameterValidation()
    {
        // HR30-HR32 对应“参数写入验证”案例。
        var setpoint = GetRegisterValue(30);
        var actual = GetRegisterValue(31);
        var tolerance = GetRegisterValue(32);
        var delta = Math.Abs(setpoint - actual);
        var isOk = delta <= tolerance;

        ParameterSummaryText =
            $"温度设定 {FormatScaledDecimal(setpoint)} °C，实际 {FormatScaledDecimal(actual)} °C，允许偏差 {FormatScaledDecimal(tolerance)} °C，" +
            $"当前偏差 {FormatScaledDecimal((ushort)Math.Min(delta, ushort.MaxValue))} °C，验证结果：{(isOk ? "通过" : "超差")}。";

        if (!isOk && SelectedLearningCase?.Title == "参数写入验证")
        {
            AddAlarmOnce(ref _lastParameterAlarmText, AlarmSeverity.Warning, "参数验证", ParameterSummaryText);
        }
        else if (isOk)
        {
            _lastParameterAlarmText = null;
        }
    }

    private void AddAlarmOnce(ref string? lastMessage, AlarmSeverity severity, string source, string message)
    {
        // 自动刷新时避免同一条报警每个 Tick 都重复插入。
        if (lastMessage == message)
        {
            return;
        }

        lastMessage = message;
        AddAlarm(severity, source, message);
    }

    private ushort GetRegisterValue(int address)
        => RegisterRows.FirstOrDefault(row => row.Address == address)?.Value ?? 0;

    private static string TranslateRunState(ushort value)
        => value switch
        {
            0 => "停机",
            1 => "运行",
            2 => "待机",
            _ => "未知"
        };

    private static string TranslateStepState(ushort value)
        => value switch
        {
            0 => "待机",
            1 => "运行中",
            2 => "完成",
            3 => "故障",
            _ => "未知"
        };

    private static string FormatScaledDecimal(ushort value)
        => (value / 10d).ToString("0.0", CultureInfo.InvariantCulture);

    private void RecordTrendSample(int address, ushort value, string source)
    {
        var previousSample = TrendSamples.FirstOrDefault(sample => sample.Address == address);
        var sample = new TrendSample(DateTime.Now, address, value, source);

        // 趋势按“最新优先”存储，列表查看更顺手，绘图时再按时间顺序转换。
        TrendSamples.Insert(0, sample);
        TrimCollection(TrendSamples, TrendSampleLimit);
        RefreshTrendPresentation();

        if (value >= TrendAlarmThreshold && (previousSample is null || previousSample.Value < TrendAlarmThreshold))
        {
            AddAlarm(
                AlarmSeverity.Warning,
                "趋势阈值",
                $"HR[{address}] = {value}，已达到阈值 {TrendAlarmThreshold}。");
        }
    }

    private void RefreshTrendPresentation()
    {
        if (TrendSamples.Count == 0)
        {
            TrendPolylinePoints = new PointCollection();
            TrendSummaryText = "暂无趋势采样，执行一次读取后会自动绘制。";
            TrendMaxLabel = "最大值";
            TrendMinLabel = "最小值";
            TrendLatestTimeLabel = "时间";
            return;
        }

        var orderedSamples = TrendSamples.Reverse().ToArray();
        var minValue = orderedSamples.Min(sample => sample.Value);
        var maxValue = orderedSamples.Max(sample => sample.Value);
        var range = Math.Max(1, maxValue - minValue);
        var width = TrendCanvasWidth - TrendLeftMargin - TrendRightMargin;
        var height = TrendCanvasHeight - TrendTopMargin - TrendBottomMargin;
        var points = new PointCollection();

        if (orderedSamples.Length == 1)
        {
            points.Add(new Point(TrendLeftMargin + width / 2, TrendTopMargin + height / 2));
        }
        else
        {
            for (var i = 0; i < orderedSamples.Length; i++)
            {
                var sample = orderedSamples[i];
                var x = TrendLeftMargin + (width * i / (orderedSamples.Length - 1));
                var y = TrendTopMargin + (maxValue - sample.Value) * height / range;
                points.Add(new Point(x, y));
            }
        }

        TrendPolylinePoints = points;
        var latest = orderedSamples[^1];
        TrendMaxLabel = maxValue.ToString(CultureInfo.InvariantCulture);
        TrendMinLabel = minValue.ToString(CultureInfo.InvariantCulture);
        TrendLatestTimeLabel = latest.TimestampText;
        TrendSummaryText =
            $"最近 {orderedSamples.Length} 个采样点 | 最新 HR[{latest.Address}] = {latest.Value} | 区间 {minValue} - {maxValue}";
    }

    private void RefreshAlarmSummary()
    {
        AlarmSummaryText = BuildAlarmSummaryText();
    }

    private string BuildAlarmSummaryText()
    {
        if (AlarmItems.Count == 0)
        {
            return "暂无报警，连接失败、自动刷新跳过和阈值越界会出现在这里。";
        }

        var latest = AlarmItems[0];
        return $"当前 {AlarmItems.Count} 条报警 | 最新 {latest.SeverityLabel} / {latest.Source}：{latest.Message}";
    }

    private void ResetSessionHistory()
    {
        AlarmItems.Clear();
        TrendSamples.Clear();
        TrendPolylinePoints = new PointCollection();
        TrendSummaryText = "暂无趋势采样，执行一次读取后会自动绘制。";
        TrendMaxLabel = "最大值";
        TrendMinLabel = "最小值";
        TrendLatestTimeLabel = "时间";
        RefreshAlarmSummary();
    }

    private static void TrimCollection<T>(ICollection<T> collection, int limit)
    {
        while (collection.Count > limit)
        {
            if (collection is ObservableCollection<T> observable && observable.Count > 0)
            {
                observable.RemoveAt(observable.Count - 1);
                continue;
            }

            break;
        }
    }

    private void RefreshFrameSnapshot()
    {
        // 报文解析面板优先展示协议层最新一次抓到的原始帧。
        var trace = (_client as IProtocolTraceSource)?.LastTrace;
        FrameSnapshot = ProtocolFramePresenter.CreateSnapshot(SelectedProtocol, trace);
    }

    private void RefreshCommandStates()
    {
        // 连接状态变化后，刷新命令可用性。
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        ReadHoldingRegistersCommand.RaiseCanExecuteChanged();
        WriteSingleRegisterCommand.RaiseCanExecuteChanged();
        StartAutoRefreshCommand.RaiseCanExecuteChanged();
        StopAutoRefreshCommand.RaiseCanExecuteChanged();
    }
}
