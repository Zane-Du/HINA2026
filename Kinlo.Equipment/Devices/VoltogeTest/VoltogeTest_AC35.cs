using System.Formats.Asn1;

namespace Kinlo.Equipment.Devices.VoltogeTest;

/// <summary>
/// 艾测电压测试 AC35系列
/// </summary>
[DeviceConnec([CommunicationEnum.VoltogeTest_AC35])]
public class VoltogeTest_AC35 : DeviceBase
{
  //private byte[] _query = Encoding.ASCII.GetBytes(":FETCh?\r\n");//读取一次
  private byte[] _query = Encoding.ASCII.GetBytes(":READ?\r\n"); //触发测试并读取最新测量值

  public VoltogeTest_AC35(DeviceInfoModel info)
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

  /// <summary>
  /// 读取电压
  /// </summary>
  /// <typeparam name="TValue"></typeparam>
  /// <param name="address"></param>
  /// <param name="length"></param>
  /// <param name="count"></param>
  /// <returns></returns>
  public override OperationResult<TValue> ReadValue<TValue>(
    SignalAddressModel address,
    string logHeader,
    DeviceOperationOptions? options = null
  )
    where TValue : default
  {
    logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
    options ??= new DeviceOperationOptions() { RetryCount = 3 };
    string errMsg = string.Empty;
    CommState commState = CommState.Success;
    for (int i = 0; i < options.RetryCount; i++)
    {
      if (IsShutdown)
        return OperationResult<TValue>.Failure(ResultTypeEnum.NG, errMsg);

      try
      {
        if (commState == CommState.Failed)
          Thread.Sleep(300);
        else if (commState == CommState.NeedReconnect)
        {
          if (!this.Reconnect(logHeader))
            return OperationResult<TValue>.Failure(ResultTypeEnum.NG, errMsg);
        }

        var res = Connect.Write(_query, logHeader);
        commState = res.State;
        errMsg = res.Message;
        if (commState != CommState.Success)
          continue;
        Thread.Sleep(20);
        var readRes = Connect.Read(1024, logHeader);
        commState = readRes.State;
        errMsg = readRes.Message;
        if (commState != CommState.Success)
          continue;
        var bytes = readRes.Data!;
        if (bytes == null || !bytes.Any())
        {
          commState = CommState.Failed;
          errMsg = $"[{Helper.GetCurrentMethodName()}] 未取到byte;";
          errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
          continue;
        }

        var valutStr = Encoding.ASCII.GetString(bytes);
        $"读取数据，长度：{bytes.Length}；bytes：{BitConverter.ToString(bytes)},转换ASCII:{valutStr.Trim('\r', '\n')}".LogProcess(
          logHeader
        );
        var values = valutStr.Split(',');
        if (values.Length != 2)
        {
          $"读取数据 [{valutStr}] 个数小于2个".LogProcess(logHeader);
          continue;
        }

        if (
          double.TryParse(values[0].Trim(), out var acResistance) && double.TryParse(values[1].Trim(), out var voltoge)
        )
        {
          (double, double) result = (acResistance, voltoge);
          if (result is TValue r)
            return OperationResult<TValue>.Success(r);
          else
          {
            errMsg = $"[{Helper.GetCurrentMethodName()}]传入数据类型和实际数据类型不对应;";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误);
            return OperationResult<TValue>.Failure(ResultTypeEnum.数据类型不对应, errMsg);
          }
        }
        else
        {
          $"数据无法正常转换！".LogProcess(logHeader);
          continue;
        }
      }
      catch (Exception ex)
      {
        commState = CommState.NeedReconnect;
        errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
        errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
    }
    return OperationResult<TValue>.Failure(ResultTypeEnum.NG, errMsg);
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

  public override bool WriteValue(
    object value,
    SignalAddressModel address,
    string logHeader,
    DeviceOperationOptions? options = null
  )
  {
    logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
    options ??= new DeviceOperationOptions() { RetryCount = 3 };
    string errMsg = string.Empty;
    for (int i = 0; i < options.RetryCount; i++)
    {
      if (IsShutdown)
        return false;

      try
      {
        var res = Connect.Write((byte[])value, logHeader);
        if (res.State == CommState.Failed)
        {
          Thread.Sleep(300);
          continue;
        }
        else if (res.State == CommState.NeedReconnect)
        {
          if (!this.Reconnect(logHeader))
            return false;
          continue;
        }
        return true;
      }
      catch (Exception ex)
      {
        errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
        errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
    }
    return false;
  }
}
