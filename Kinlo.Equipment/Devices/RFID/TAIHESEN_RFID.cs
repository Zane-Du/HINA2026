using System.Formats.Asn1;

namespace Kinlo.Equipment.Devices.RFID;

[DeviceConnec([CommunicationEnum.RFID_TAIHESEN])]
public class TAIHESEN_RFID : DeviceBase
{
  private byte[] _cmd = new byte[] { 0xAA, 0x00, 0x22, 0x00, 0x00, 0x22, 0xBB };

  public TAIHESEN_RFID(DeviceInfoModel info)
    : base(info) { }

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
    string msg = string.Empty;
    try
    {
      while (_stopwatch.ElapsedMilliseconds <= 8000)
      {
        var writeRes = Connect.Write(_cmd, logHeader);
        if (writeRes.State == CommState.Failed)
        {
          Thread.Sleep(300);
          continue;
        }
        else if (writeRes.State == CommState.NeedReconnect)
        {
          if (!this.Reconnect(logHeader))
            return OperationResult<TValue>.Failure(ResultTypeEnum.NG, writeRes.Message);
          continue;
        }
        Thread.Sleep(200);
        var res = Connect.Read(64, logHeader);
        if (res.State == CommState.Failed)
        {
          Thread.Sleep(300);
          continue;
        }
        else if (res.State == CommState.NeedReconnect)
        {
          if (!this.Reconnect(logHeader))
            return OperationResult<TValue>.Failure(ResultTypeEnum.NG, res.Message);
          continue;
        }
        var bytes = res.Data!;
        $"[TAIHESEN_RFID]byte：{BitConverter.ToString(bytes)}".LogProcess(logHeader, Log4NetLevelEnum.信息);
        if (bytes.Length > 12)
        {
          var code = string.Join("", bytes[8..^4].Select(x => x.ToString("X2")));
          if (!string.IsNullOrEmpty(code))
          {
            if (code is TValue tv)
              return OperationResult<TValue>.Success(tv);
            else
            {
              msg = $"[{Helper.GetCurrentMethodName()}]传入数据类型和实际数据类型不对应;";
              msg.LogProcess(logHeader, Log4NetLevelEnum.错误);
              return OperationResult<TValue>.Failure(ResultTypeEnum.数据类型不对应, msg);
            }
          }
        }
        Thread.Sleep(30);
      }
      msg = $"[{Helper.GetCurrentMethodName()}]读取失败;";
      msg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      return OperationResult<TValue>.Failure(ResultTypeEnum.NG, msg);
    }
    catch (Exception ex)
    {
      msg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
      msg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      return OperationResult<TValue>.Failure(ResultTypeEnum.异常, msg, ex);
    }
    finally
    {
      _stopwatch.Stop();
    }
  }

  public override bool WriteClass<TClass>( TClass value, SignalAddressModel address, string logHeader,DeviceOperationOptions? options = null )
  {
    throw new NotImplementedException();
  }

  public override bool WriteValue(object value,SignalAddressModel address,string logHeader,DeviceOperationOptions? options = null)
  {
    throw new NotImplementedException();
  }
}
