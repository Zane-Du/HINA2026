namespace Kinlo.Equipment.Devices.ElectronicScales;

/// <summary>
/// Scale_鸿伟城
/// </summary>
[DeviceConnec([CommunicationEnum.Scale_鸿伟城])]
public class Scale_HWC : CachedWeighingScaleBase
{
  protected override byte[] ZeroCommand => [0x5A, 0x0D, 0x0A];
  protected override int FrameLength => 13;

  public Scale_HWC(DeviceInfoModel info)
    : base(info) { }

  protected override bool TryParseWeight(byte[] frame, string logHeader, out double weight)
  {
    weight = 0;

    var strArray = Encoding
      .ASCII.GetString(frame)
      .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    var str = strArray.LastOrDefault(x => x.Length == 11); //此处不需要稳定吗（重写时无文档无法确定）？2025 12 17 刘亮
    if (str == null)
    {
      $"未取到正确字节或未取到稳定值!".LogProcess(logHeader);
      return false;
    }
    string weighStr = str.Substring(0, 8).Replace(" ", "");
    if (double.TryParse(weighStr, out weight))
    {
      $"取到稳定值[{weight}]！".LogProcess(logHeader);
      return true;
    }
    else
    {
      $"值未能正常转换!".LogProcess(logHeader);
      return false;
    }
  }
}
