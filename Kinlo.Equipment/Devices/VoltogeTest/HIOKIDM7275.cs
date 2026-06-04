using System.Formats.Asn1;

namespace Kinlo.Equipment.Devices.VoltogeTest;

/// <summary>
/// 日志电压测试 DM7275
/// </summary>
[DeviceConnec([CommunicationEnum.VoltogeTest_HIOKI_DM7275])]
public class HIOKIDM7275 : DeviceBase
{
  private byte[] _query = Encoding.ASCII.GetBytes(":FETCh?\r\n"); //查询最新测量值
  private byte[] _startAndQuery = Encoding.ASCII.GetBytes(":READ?\r\n"); //测量(等待触发并读取测量值)

  public HIOKIDM7275(DeviceInfoModel info)
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
        {
          Thread.Sleep(300);
        }
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

        Thread.Sleep(100);
        var readRes = Connect.Read(1024, logHeader);
        commState = readRes.State;
        errMsg = readRes.Message;
        if (commState != CommState.Success)
          continue;
        var bytes = readRes.Data!;
        var valutStr = Encoding.ASCII.GetString(bytes);
        if (bytes.Length == 16)
        {
          valutStr = Encoding.ASCII.GetString(bytes, 0, 14).Trim();
          $"读取数据成功，长度：{bytes.Length}；bytes：{BitConverter.ToString(bytes)},转换ASCII:{valutStr}".LogProcess(
            logHeader,
            Log4NetLevelEnum.信息
          );
          if (float.TryParse(valutStr, out float value))
          {
            float f = (float)Math.Round(value, 5);
            if (f is TValue tv)
              return OperationResult<TValue>.Success(tv);
            else
            {
              errMsg = $"[{Helper.GetCurrentMethodName()}]传入数据类型和实际数据类型不对应;";
              errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误);
              return OperationResult<TValue>.Failure(ResultTypeEnum.数据类型不对应, errMsg);
            }
          }
        }
        else
        {
          errMsg = $"读取数据错误，长度：{bytes.Length}；bytes：{BitConverter.ToString(bytes)},转换ASCII:{valutStr}";
          errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误);
          commState = CommState.Failed;
          continue;
        }
      }
      catch (Exception ex)
      {
        commState = CommState.Failed;
        errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
        errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
    }
    return OperationResult<TValue>.Failure(ResultTypeEnum.NG, "读取失败！");
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
