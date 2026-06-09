namespace Kinlo.Equipment.Devices.ElectronicScales;

public abstract class CachedWeighingScaleBase : DeviceBase, IContinuousReading
{
    /// <summary>
    /// 单帧长度（用于判断是否可以尝试解析）
    /// </summary>
    protected abstract int FrameLength { get; }

    /// <summary>
    /// 清零命令
    /// </summary>
    protected abstract byte[] ZeroCommand { get; }
    public ReconnectInfoModel ReconnectInfo { get; set; } = new ReconnectInfoModel();

    private readonly List<byte> _cacheByte = new List<byte>();
    private readonly object _lock = new object();

    public CachedWeighingScaleBase(DeviceInfoModel info) : base(info) { }

    public override bool Open()
    {
        Close();
        if (base.Open())
        {
            _ = ContinuousReading();
            return true;
        }
        return false;
    }

    private Task ContinuousReading()
    {
        return Task.Run(() =>
        {
            var logHeader = DeviceInfo.ToDeviceLogHeader();
            Thread.Sleep(200);
            while (!IsShutdown)
            {
                try
                {
                    lock (_lock)
                    {
                        if (_cacheByte.Count >= FrameLength * 20) //限制缓存以保证数据的实时性
                        {
                            _cacheByte.RemoveRange(0, _cacheByte.Count - FrameLength * 5);
                        }
                    }
                    var res = Connect.Read(512, logHeader);
                    if (res.State == CommState.Failed)
                    {
                        Thread.Sleep(300);
                        continue;
                    }
                    else if (res.State == CommState.NeedReconnect)
                    {
                        if (CanReconnect())
                            this.Reconnect(logHeader);
                        else
                            $"在{ReconnectInfo.TimeWindow}分钟内重连次数超{ReconnectInfo.MaxReconnectCount}次上限，不重连！".LogProcess(
                          logHeader
                        );
                        break; //一定要退出，重连打开时会重新开始一个任务
                    }
                    var raw = res.Data!;
                    if (raw != null && raw.Length > 0)
                    {
                        lock (_lock)
                            _cacheByte.AddRange(raw);
                    }

                    Thread.Sleep(20);
                }
                catch (Exception ex)
                {
                    $"[{DeviceInfo.Communication}]读取原始数据异常：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误);
                    Thread.Sleep(300);
                }
            }
        });
    }

    /// <summary>
    /// 解析一帧数据为重量
    /// </summary>
    protected abstract bool TryParseWeight(byte[] frame, string logHeader, out double weight);

    public override OperationResult<TValue> ReadValue<TValue>(
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
    )
      where TValue : default
    {
        if (typeof(TValue) != typeof(double))
        {
            var msg = $"[{DeviceInfo.ProcessesType}]传入数据类型非double！";
            msg.LogProcess(logHeader, Log4NetLevelEnum.错误);
            return OperationResult<TValue>.Failure(ResultTypeEnum.数据类型不对应, msg);
        }

        options ??= new DeviceOperationOptions();
        Thread.Sleep(200); //延时200ms取称重量
        OperationResult<double> rs = OperationResult<double>.Failure(ResultTypeEnum.NG, "默认");
        for (int r = 0; r < options.RetryCount; r++)
        {
            rs = OnReadValue(logHeader);
            if (rs.IsSuccess)
                return OperationResult<TValue>.Success((TValue)(object)rs.Value);
        }
        return OperationResult<TValue>.Failure(rs.ErrCode, rs.ErrorMessage, rs.Exception);
    }

    /// <summary>
    /// 取重量并做对比
    /// </summary>
    /// <param name="logHeader"></param>
    /// <returns></returns>
    private OperationResult<double> OnReadValue(string logHeader)
    {
        int weighCount = 2; //对比重量的个数
        List<OperationResult<double>> weighCaches = new(); //对比重量
        Stopwatch weightStopwatch = new Stopwatch();
        weightStopwatch.Restart();

        while (
          weighCaches.Count < weighCount
          && weightStopwatch.ElapsedMilliseconds < 3000
          && DeviceInfo.TaskToken != null
          && !DeviceInfo.TaskToken.Token.IsCancellationRequested
        ) //对比重量是否一致
        {
            var weighRs = ReadWeight(logHeader);
            if (weighRs.IsSuccess)
                weighCaches.Add(weighRs);
            else
                weighCaches.Clear();

            if (weighCaches.Count == weighCount)
            {
                string weighStr = $"[{string.Join(',', weighCaches.Select(x => x.Value))}]";
                $"取对比重量：{weighStr}".LogProcess(logHeader);
                var max = weighCaches.Max(x => x.Value);
                var min = weighCaches.Min(x => x.Value);
                if (max - min <= 0.2) //接受误差0.2g
                {
                    $"通过重量比对，取得正确重量：[{weighCaches[^1].Value}]".LogProcess(logHeader);
                    return weighCaches[^1];
                }
                else
                {
                    $"未通过重量比对：{weighStr}".LogProcess(logHeader);
                    weighCaches.RemoveAt(0);
                }
            }
        }
        weightStopwatch.Stop();
        return OperationResult<double>.Failure(ResultTypeEnum.NG, "取值失败！");
    }

    /// <summary>
    /// 取称重量
    /// </summary>
    /// <param name="logHeader"></param>
    /// <returns></returns>
    private OperationResult<double> ReadWeight(string logHeader)
    {
        try
        {
            string msg = string.Empty;
            byte[]? bytes = null;
            for (int i = 0; i < 5; i++)
            {
                lock (_lock)
                {
                    if (_cacheByte.Count >= FrameLength * 2)
                    {
                        bytes = _cacheByte.ToArray();
                        _cacheByte.Clear();
                        break;
                    }
                }
                Thread.Sleep(100);
            }

            if (bytes == null || !bytes.Any() || bytes.Length < FrameLength)
            {
                msg =
                  $"[{DeviceInfo.ProcessesType}]称重取值 IP:{DeviceInfo.IPCOM}，端口：{DeviceInfo.Port},取值byte小于{FrameLength}!";
                msg.LogProcess(logHeader);
                return OperationResult<double>.Failure(ResultTypeEnum.空指针, msg);
            }

            if (TryParseWeight(bytes, logHeader, out double weight))
            {
                return OperationResult<double>.Success(weight);
            }
            else
            {
                msg = $"[{DeviceInfo.ProcessesType}] {BitConverter.ToString(bytes)} 重量解析失败！";
                msg.LogProcess(logHeader, Log4NetLevelEnum.错误);
                return OperationResult<double>.Failure(ResultTypeEnum.NG, msg);
            }
        }
        catch (Exception ex)
        {
            var msg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            msg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
            return OperationResult<double>.Failure(ResultTypeEnum.异常, msg, ex);
        }
    }

    /// <summary>
    /// 清零
    /// </summary>
    /// <param name="value"></param>
    /// <param name="address"></param>
    /// <param name="logHeader"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override bool WriteValue(
      object value,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
    )
    {
        logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
        options ??= new DeviceOperationOptions() { RetryCount = 3 };
        for (int i = 0; i < options.RetryCount; i++)
        {
            try
            {
                var res = Connect.Write(ZeroCommand, logHeader);
                if (res.State == CommState.Failed)
                {
                    Thread.Sleep(300);
                    continue;
                }
                else if (res.State == CommState.NeedReconnect)
                {
                    this.Reconnect(logHeader);
                    continue;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (DeviceInfo.TaskToken == null || DeviceInfo.TaskToken.Token.IsCancellationRequested)
                    break;
                $"[清零]异常：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误);
                Thread.Sleep(300);
            }
        }
        return false;
    }

    public override bool WriteClass<TClass>(
      TClass value,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
    )
    {
        throw new NotImplementedException();
    }

    public override OperationResult<TClass> ReadClass<TClass>(
      SignalAddressModel address,
      TClass obj,
      string logHeader,
      DeviceOperationOptions? options = null
    )
      where TClass : class
    {
        throw new NotImplementedException();
    }

    Task IContinuousReading.ContinuousReading()
    {
        return ContinuousReading();
    }

    public bool CanReconnect() => ReconnectInfo.CanReconnect();
}
