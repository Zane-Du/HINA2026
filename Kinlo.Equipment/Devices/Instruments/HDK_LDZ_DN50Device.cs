namespace Kinlo.Equipment.Devices.Instruments;

/// <summary>
/// 浮子流量计，贺德克(HDK-LDZ-DN50)
/// </summary>
[DeviceConnec([CommunicationEnum.HDK_LDZ_DN50流量计])]
public class HDK_LDZ_DN50Device : DeviceBase
{
  #region field

  byte[] _sendBytes = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD];

  public IProtocolHelper? ProtocolHelper { get; set; }
  #endregion
  public HDK_LDZ_DN50Device(DeviceInfoModel info)
    : base(info)
  {
    ProtocolHelper = new Modbus_RTU_Protocol();
  }

  public override OperationResult<TClass> ReadClass<TClass>(
    SignalAddressModel address,
    TClass obj,
    string logHeader,
    DeviceOperationOptions? options = null
  )
    where TClass : class
  {
    string errMsg = string.Empty;
    options ??= new DeviceOperationOptions();
    logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
    for (int r = 0; r < options.RetryCount; r++)
    {
      HDK_LDZ_DN50DTO data = new HDK_LDZ_DN50DTO();
      try
      {
        var res = Connect.WriteAndRead(_sendBytes, ProtocolHelper, logHeader);
        if (res.State == CommState.Failed)
        {
          Thread.Sleep(300);
          continue;
        }
        else if (res.State == CommState.NeedReconnect)
        {
          if (!this.Reconnect(logHeader))
            return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
          continue;
        }
        var bytes = res.Data!;

        List<byte> _list = new List<byte>();
        for (int i = 0; i < bytes.Length - 1; i += 2)
        {
          _list.Add(bytes[i + 1]);
          _list.Add(bytes[i]);
        }
        data.瞬时流量 = (float)Math.Round(BitConverter.ToSingle(_list.ToArray(), 0), 3);
        data.流量百分比 = (float)Math.Round(BitConverter.ToSingle(_list.ToArray(), 4), 3);
        data.总量 = (float)Math.Round(BitConverter.ToSingle(_list.ToArray(), 8), 3);
        data.瞬时流量单位 = HDK_LDZ_DN50DTO.GetInstantaneousFlowUnit((int)BitConverter.ToSingle(_list.ToArray(), 12));
        data.总量单位 = HDK_LDZ_DN50DTO.GetTotalnit((int)BitConverter.ToSingle(_list.ToArray(), 16));
        return OperationResult<TClass>.Success(data as TClass);
      }
      catch (Exception ex)
      {
        errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
        errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
    }
    return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
  }

  public override OperationResult<TValue> ReadValue<TValue>(
    SignalAddressModel address,
    string logHeader,
    DeviceOperationOptions? options = null
  )
    where TValue : default
  {
    throw new NotImplementedException();
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
    throw new NotImplementedException();
  }
}
