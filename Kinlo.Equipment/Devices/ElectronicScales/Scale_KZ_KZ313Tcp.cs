using static Kinlo.Equipment.Devices.ElectronicScales.Scale_KZ_KZ313Rtu;

namespace Kinlo.Equipment.Devices.ElectronicScales;

/// <summary>
/// 可竹电子称（KZ313）
/// </summary>
[DeviceConnec([CommunicationEnum.Scale_KZ313_TCP])]
public class Scale_KZ_KZ313Tcp : DeviceBase
{
  byte[] _readRequest = [0x01, 0x03, 0X00, 0x00, 0x00, 0x03, 0x05, 0xCB];
  byte[] _zeroClear = [0x01, 0x06, 0X00, 0x32, 0x00, 0x01, 0xE9, 0xC5];
  IProtocolHelper ProtocolHelper;
  private List<byte> _cacheByte = new List<byte>(1024);

  public Scale_KZ_KZ313Tcp(DeviceInfoModel info)
    : base(info)
  {
    ProtocolHelper = new Modbus_TCP_Protocol(1);
    _readRequest = [0x03, 0X00, 0x00, 0x00, 0x03];
    _zeroClear = [0x06, 0X00, 0x32, 0x00, 0x01];
  }

  public override bool Open()
  {
    if (base.Open())
    {
      return true;
    }
    return false;
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
  ///
  /// </summary>
  /// <typeparam name="TValue"></typeparam>
  /// <param name="address"></param>
  /// <param name="count"></param>
  /// <returns>-1为未取到值</returns>
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
    for (int k = 0; k < options.RetryCount; k++)
    {
      if (IsShutdown)
        return OperationResult<TValue>.Failure(ResultTypeEnum.NG, "");

      try
      {
        List<StableStatus> stableStatuses = new List<StableStatus>();
        for (int i = 0; i < 3; i++) //最多测3组。每组取3次，任一组OK直接返回
        {
          Connect.Close();
          Connect.Open();
          var res = Connect.WriteAndRead(_readRequest, ProtocolHelper, logHeader, readLength: 20);
          Connect.Close();
          var stableState = IsStable(res.Data!, i + 1);
          stableStatuses.Add(stableState);
          Thread.Sleep(100);
        }
        var finalRes = CalculateResult<TValue>(stableStatuses, k + 1, logHeader);
        if (finalRes.IsSuccess)
          return finalRes;
      }
      catch (Exception ex)
      {
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

  /// <summary>
  /// 清零
  /// </summary>
  /// <param name="value"></param>
  /// <param name="address"></param>
  /// <param name="offset"></param>
  /// <param name="count"></param>
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
        if (IsShutdown)
          return false;
        Connect.Close();
        Connect.Open();
        var r = Connect.Write(ProtocolHelper.Serialize(_zeroClear), logHeader);
        Connect.Close();
        return r.State == CommState.Success;
      }
      catch (Exception ex)
      {
        if (IsShutdown)
          return false;
        $"清零异常：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误);
      }
      Thread.Sleep(200);
    }
    return false;
  }
}
