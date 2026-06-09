namespace Kinlo.Equipment.Devices.ElectronicScales;

/// <summary>
/// 普瑞逊TC06电子称
/// </summary>
[DeviceConnec([CommunicationEnum.Scale_Pris_TC06])]
public class Scale_Pris_TC06 : CachedWeighingScaleBase
{
    public Scale_Pris_TC06(DeviceInfoModel info)  : base(info) { }

    protected override int FrameLength => 20;

    protected override byte[] ZeroCommand => Encoding.ASCII.GetBytes("DT\r\n");

    protected override bool TryParseWeight(byte[] frame, string logHeader, out double weight)
    {
        weight = 0;

        var strArray = Encoding
          .ASCII.GetString(frame)
          .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var str = strArray.LastOrDefault(x =>
          x.Length == 18 && !x.Contains("WT,") && (x.Contains("ST,GS") || x.Contains("ST,NT"))
        );
        if (str == null)
        {
            $"未取到正确字节或未取到稳定值!".LogProcess(logHeader);
            return false;
        }
        if (str.Substring(0, 2).ToUpper() == "ST")
        {
            string weighStr = str.Substring(6, 8).Replace(" ", "");
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
