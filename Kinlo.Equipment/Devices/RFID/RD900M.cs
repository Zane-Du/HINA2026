namespace Kinlo.Equipment.Devices.RFID;

/// <summary>
/// RFID扫码
/// </summary>
[DeviceConnec([CommunicationEnum.RFID_RD900M])]
public class RD900M : DeviceBase
{
  private byte[] _cmd = new byte[] { 0x04, 0x00, 0x01, 0xDB, 0x4B };

  public RD900M(DeviceInfoModel info)
    : base(info) { }

  public override bool Open()
  {
    if (base.Open())
    {
      return true;
    }
    return false;
  }

  Stopwatch _stopwatch = new Stopwatch();

  public override OperationResult<TValue> ReadValue<TValue>(
    SignalAddressModel address,
    string logHeader,
    DeviceOperationOptions? options = null
  )
    where TValue : default
  {
    logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
    _stopwatch.Restart();

    try
    {
      Connect.Close();
      Connect.Open();
      while (_stopwatch.ElapsedMilliseconds <= 5000)
      {
        Connect.Write(_cmd, logHeader);
        Thread.Sleep(100);
        var res = Connect.Read(64, logHeader);
        if (res.State == CommState.Failed)
        {
          Thread.Sleep(200);
          continue;
        }
        else if (res.State == CommState.NeedReconnect)
        {
          if (!this.Reconnect(logHeader))
            return OperationResult<TValue>.Failure(ResultTypeEnum.NG, res.Message);
          continue;
        }
        var bytes = res.Data!;
        $"[RD900M]byte：{BitConverter.ToString(bytes)}".LogProcess(logHeader, Log4NetLevelEnum.信息);
        if (bytes.Length > 8)
        {
          var code = string.Join("", bytes.Skip(6).Take(bytes.Length - 8).Select(x => x.ToString("X2")));
          if (!string.IsNullOrEmpty(code))
            return OperationResult<TValue>.Success((TValue)(object)code);
        }
        Thread.Sleep(30);
      }
      var msg = $"[{Helper.GetCurrentMethodName()}]读取失败;";
      msg.LogProcess(logHeader, Log4NetLevelEnum.错误);
      return OperationResult<TValue>.Failure(ResultTypeEnum.NG, msg);
    }
    catch (Exception ex)
    {
      var msg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
      msg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      return OperationResult<TValue>.Failure(ResultTypeEnum.异常, msg, ex);
    }
    finally
    {
      Connect.Close();
      _stopwatch.Stop();
    }
  }

  public override OperationResult<TClass> ReadClass<TClass>(
    SignalAddressModel address,
    TClass obj,
    string logHeader,
    DeviceOperationOptions? options = null
  )
    where TClass : class
  {
    throw new NotImplementedException("未实现");
  }

  public override bool WriteClass<TClass>(
    TClass value,
    SignalAddressModel address,
    string logHeader,
    DeviceOperationOptions? options = null
  )
  {
    throw new NotImplementedException("未实现");
  }

  public override bool WriteValue(
    object value,
    SignalAddressModel address,
    string logHeader,
    DeviceOperationOptions? options = null
  )
  {
    throw new NotImplementedException("未实现");
  }
}
