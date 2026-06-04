using System.Formats.Asn1;
using System.Windows.Interop;

namespace Kinlo.Equipment.Devices.VoltogeTest;

/// <summary>
/// 电压测试  Keysight
/// </summary>
[DeviceConnec([CommunicationEnum.VoltogeTest_Keysight])]
public class Keysight : DeviceBase
{
  private byte[] _query = Encoding.ASCII.GetBytes("READ?\r\n"); //查询最新测量值

  public Keysight(DeviceInfoModel info)
    : base(info) { }

  public override bool Open()
  {
    return base.Open();
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
      try
      {
        if (IsShutdown)
          return OperationResult<TValue>.Failure(ResultTypeEnum.NG, errMsg);

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

        Thread.Sleep(100);
        List<byte> bytelist = new List<byte>();

        var readRes = Connect.Read(1024, logHeader);
        commState = readRes.State;
        errMsg = readRes.Message;
        for (int r = 0; r < 2; r++)
        {
          if (readRes.State == CommState.Success && readRes.Data != null && readRes.Data.Length > 0)
          {
            bytelist.AddRange(readRes.Data);
            $"取得值，ASICC：{Encoding.ASCII.GetString(readRes.Data)}；bytes：[{BitConverter.ToString(readRes.Data)}]".LogProcess(
              logHeader,
              Log4NetLevelEnum.信息
            );
            break;
          }
        }

        var bytes = bytelist.ToArray();
        if (bytes == null || !bytes.Any())
        {
          commState = CommState.NeedReconnect;
          errMsg = $"[{Helper.GetCurrentMethodName()}] 未取到byte;";
          errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);

          continue;
        }

        var valueStr = Encoding.ASCII.GetString(bytes);
        $"读取数据成功，长度：{bytes.Length}；\r\nbytes：[{BitConverter.ToString(bytes)}]\r\n转换ASCII:[{valueStr}]".LogProcess(
          logHeader,
          Log4NetLevelEnum.信息
        );
        var listByte = bytes.SplitByteArray([0x0D, 0x0A]);
        var byteValue = listByte.Where(x => x.Count() == 15).LastOrDefault();
        if (byteValue == null)
        {
          $"报文不合法，长度错误！".LogProcess(logHeader, Log4NetLevelEnum.错误);
          continue;
        }
        valueStr = Encoding.ASCII.GetString(byteValue);
        if (float.TryParse(valueStr, out float value))
        {
          float f = (float)Math.Round(value, 6);
          if (f is TValue tv)
          {
            $"取值成功[{tv}]！".LogProcess(logHeader, Log4NetLevelEnum.成功);
            return OperationResult<TValue>.Success(tv);
          }
          else
          {
            errMsg = $"[{Helper.GetCurrentMethodName()}]传入数据类型和实际数据类型不对应;";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误);
            return OperationResult<TValue>.Failure(ResultTypeEnum.数据类型不对应, errMsg);
          }
        }
        else
          $"无法转换为指定类型，长度：{bytes.Length}；bytes：{BitConverter.ToString(bytes)},转换ASCII:{valueStr}".LogProcess(
            logHeader,
            Log4NetLevelEnum.错误
          );
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
