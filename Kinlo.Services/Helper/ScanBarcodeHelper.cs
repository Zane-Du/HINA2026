using System.Text.RegularExpressions;

namespace Kinlo.Services.Helper;

public static class ScanBarcodeHelper
{
    /// <summary>
    /// 扫码枪通用扫码方法
    /// </summary>
    /// <param name="device"></param>
    /// <param name="parameter"></param>
    /// <param name="context"></param>
    /// <param name="isHaveBatterys">如果是一扫多，指示通道是否有电池</param>
    /// <param name="logHeader"></param>
    /// <param name="pattern">正则校验规则</param>
    /// <returns></returns>
    public static ScanBarcodeResultDto[] ScanCode( IDevice device,  ParameterConfig parameter, PLCInteractAddressModel context,  bool[] isHaveBatterys,  string logHeader,   string pattern )
    {
        ScanBarcodeResultDto[] results = new ScanBarcodeResultDto[context.DataLength];
        for (int i = 0; i < context.DataLength; i++)
        {
            results[i] = new ScanBarcodeResultDto();
        }

        for (int i = 0; i <= parameter.RunParameter.RetryCountScanCode; i++)
        {
            $"开始扫码;".LogProcess(logHeader);
            var scanRs = device.ReadValue<string>(null, logHeader);
            if (!scanRs.IsSuccess)
            {
                continue;
            }
            string barcode = scanRs.Value!;
            $"扫码枪返回：{barcode}".LogProcess(logHeader);

            string[] barcodes = context.DataLength switch
            {
                var c when c == null => ["Error"],
                > 1 => barcode.Split(','),
                _ => [barcode],
            };

            if (barcodes.Length < context.DataLength)
            {
                $"扫码枪条码{barcode}条码个数和需要个数{context.DataLength}不符".LogProcess(  logHeader,    Log4NetLevelEnum.错误,     true);
                continue;
            }

            for (int k = 0; k < context.DataLength; k++)
            {
                results[k].Code = barcodes[k];
                if (!isHaveBatterys[k])
                {
                    results[k].ScanStatus = ScanBarcodeStatus.当前通道无电池;
                    $"通道[{k + 1}]当前通道无电池;".LogProcess(logHeader);
                }
                else
                {
                    if (string.IsNullOrEmpty(pattern))
                    {
                        results[k].ScanStatus = ScanBarcodeStatus.扫码成功;
                        $"条码[{barcodes[k]}]未配置校验规则，默认校验合格;".LogProcess(logHeader);
                    }
                    else
                    {
                        if (ValidationBarcode(barcodes[k], pattern))
                        {
                            results[k].ScanStatus = ScanBarcodeStatus.扫码成功;
                            $"条码[{barcodes[k]}]校验合格;".LogProcess(logHeader);
                        }
                        else
                        {
                            results[k].ScanStatus = ScanBarcodeStatus.扫码失败;
                            $"条码[{barcodes[k]}]校验失败;".LogProcess(logHeader, Log4NetLevelEnum.警告);
                        }
                    }
                }
            }

            if (results.All(x => x.ScanStatus != ScanBarcodeStatus.扫码失败))
            {
                return results;
            }

            Thread.Sleep(100);
        }

        return results;
    }

    public static bool ValidationBarcode(string barcode, string pattern)
    {
        if (barcode == null || string.IsNullOrEmpty(barcode) || barcode == "ERROR")
        {
            return false;
        }
        return Regex.IsMatch(barcode, pattern, RegexOptions.Compiled);
    }
}

public class ScanBarcodeResultDto
{
    public ScanBarcodeStatus ScanStatus { get; set; } = ScanBarcodeStatus.扫码失败;
    public string Code { get; set; } = string.Empty;
}

public enum ScanBarcodeStatus
{
    扫码成功,
    扫码失败,
    当前通道无电池,
}
