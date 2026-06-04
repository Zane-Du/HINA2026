using System.Formats.Asn1;

namespace Kinlo.Equipment.Devices.CodeScanningGun;

/// <summary>
/// 扫码枪通用类
/// </summary>
[DeviceConnec([CommunicationEnum.ScanCode_SR1000, CommunicationEnum.ScanCode_SR700])]
public class CodeScanningGunGeneric : DeviceBase
{
  private byte[] _start = Encoding.ASCII.GetBytes("LON\r\n");
  private byte[] _end = Encoding.ASCII.GetBytes("LOFF\r\n");

  public CodeScanningGunGeneric(DeviceInfoModel info)
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

  public override OperationResult<TValue> ReadValue<TValue>(
    SignalAddressModel address,
    string logHeader,
    DeviceOperationOptions? options = null
  )
    where TValue : default
  {
    options ??= new DeviceOperationOptions();
    logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
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

        Connect!.ClearCache(logHeader);
        var res = Connect.Write(_start, logHeader);
        errMsg = res.Message;
        if (res.State != CommState.Success)
          continue;

        Thread.Sleep(50);
        var readRes = Connect.Read(1024, logHeader);
        errMsg = readRes.Message;
        if (res.State != CommState.Success)
          continue;

        byte[] barcode_bytes = readRes.Data!;
        string barcode = Encoding.ASCII.GetString(barcode_bytes) ?? "";
        barcode = barcode.Trim('\u0002', '\u0003', '\r', '\n', ' ');
        return OperationResult<TValue>.Success((TValue)(object)barcode);
      }
      catch (Exception ex)
      {
        commState = CommState.NeedReconnect;
        errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
        errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
      finally
      {
        try
        {
          if (commState == CommState.Success)
          {
            var writeRes = Connect.Write(_end, logHeader);
            if (writeRes.State != CommState.Success)
            {
              $"扫码枪写入完成失败".LogProcess(logHeader);
            }
          }
        }
        catch { }
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
    throw new NotImplementedException();
  }
}
