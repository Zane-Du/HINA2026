using System.Text;

namespace Kinlo.Equipment.Devices.ElectronicScales;

/// <summary>
/// AND电子称
/// </summary>
[DeviceConnec([CommunicationEnum.Scale_AND_4531B])]
public class Scale_AND_4531B : CachedWeighingScaleBase
{
  protected override byte[] ZeroCommand => Encoding.ASCII.GetBytes("Z\r\n");
  protected override int FrameLength => 15;

  public Scale_AND_4531B(DeviceInfoModel info)
    : base(info) { }

  protected override bool TryParseWeight(byte[] frame, string logHeader, out double weight)
  {
    weight = 0;

    var strArray = Encoding
      .ASCII.GetString(frame)
      .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    var str = strArray.LastOrDefault(x => x.Length == 13 && x.Contains("WT,"));
    if (str == null)
    {
      $"未取到正确字节或未取到稳定值!".LogProcess(logHeader);
      return false;
    }
    if (str.Substring(0, 2).ToUpper() == "WT")
    {
      string weighStr = str.Substring(3, 8).Replace(" ", "");
      if (double.TryParse(weighStr, out weight))
      {
        $"取到稳定值[{weight}]！".LogProcess(logHeader);
        return true;
      }
      else
      {
        $"称稳定，但值未能正常转换!".LogProcess(logHeader);
        return false;
      }
    }
    else
    {
      $"未取到稳定值!".LogProcess(logHeader);
      return false;
    }
  }
}
