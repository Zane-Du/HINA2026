namespace Kinlo.Equipment.Devices.ElectronicScales;

/// <summary>
/// Scale_科迪手工称_5100
/// </summary>
[DeviceConnec([CommunicationEnum.Scale_科迪手工称_5100])]
public class Scale_KD_5100 : CachedWeighingScaleBase
{
  protected override int FrameLength => 25;

  protected override byte[] ZeroCommand => throw new NotImplementedException("未实现");

  public Scale_KD_5100(DeviceInfoModel info)
    : base(info) { }

  protected override bool TryParseWeight(byte[] frame, string logHeader, out double weight)
  {
    weight = 0;
    List<byte[]> splitBytes = frame.SplitByteArray([0x0D, 0x0A]);
    int count = 1;
    for (int i = splitBytes.Count - 1; i >= 0; i--)
    {
      count++;
      var byteArray = splitBytes[i];
      if (byteArray.Length >= 23)
      {
        if (byteArray[0] == 0x53 && byteArray[1] == 0x54) //"ST" 0x53 ="S" 0X54="T" ,稳定
        {
          var weighStr = Encoding.ASCII.GetString(byteArray, 7, 10).Replace(" ", "");
          if (double.TryParse(weighStr, out weight))
          {
            $"取到稳定值[{weight}]！".LogProcess(logHeader);
            return true;
          }
        }
      }
      else
      {
        $"第{count}次读取字节不合法！字节:[{(byteArray == null ? "null" : BitConverter.ToString(byteArray))}];\r\n".LogProcess(
          logHeader
        );
      }

      if (count >= 5)
      {
        $"未取到稳定值!".LogProcess(logHeader);
        return false;
      }
    }
    return false;
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
