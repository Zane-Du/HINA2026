namespace Kinlo.Services.Handlers;

[DeviceConnec(ProcessTypeEnum.套杯扫码, [CommunicationEnum.ScanCode_SR1000, CommunicationEnum.ScanCode_SR700])] //指定工艺，可指定多个
[DeviceConnec(ProcessTypeEnum.托盘扫码, [CommunicationEnum.ScanCode_SR1000, CommunicationEnum.ScanCode_SR700])] //指定工艺，可指定多个
public class ScanCodeCupAndTrayHandler : ServiceHandlerBase
{
    #region 构造函数方法
    public ScanCodeCupAndTrayHandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }
    #endregion

    protected override Task HandleCore(short plcValue)
    {
        #region 根据信号配置界面，拿到扫码设备
        var device = _devicesConfig.GetRunDevice(Context, Context.DeviceStartIndex);

        if (device == null)
        {
            _isDeviceAlarm = true;
            $"找不到设备，不给PLC发结果及完成信号，请停机检查！;".LogProcess(_taskLogHeader, Log4NetLevelEnum.错误, true);
            return Task.CompletedTask;
        }
        #endregion

        #region 执行扫码方法，写入PLC条码
        else
        {
            int dataIndex = Context.DeviceStartIndex - 1; //数据索引
            var isHaveBatterys = Enumerable.Repeat(true, Context.DataLength).ToArray();
            var scanResults = ScanBarcodeHelper.ScanCode(device, _parameterConfig, Context, isHaveBatterys, _taskLogHeader, _parameterConfig.AdvancedConfig.InjectionTrayCodeValidationRule);
            if (scanResults[0].ScanStatus == ScanBarcodeStatus.扫码成功)
            {
                for (int n = 1; n < 4; n++)
                {
                    var _barceodeTag = new SignalAddressModel($"{Context.DataAddress.Lable}[{dataIndex}].ToPLCData[0].Code", 0);
                    if (_plc.WriteValue(scanResults[0].Code, _barceodeTag, _taskLogHeader))
                    {
                        $"条码：{scanResults[0].Code}; 第[{n}]次写入 [{JsonSerializer.Serialize(_barceodeTag)}] 成功".LogProcess(_taskLogHeader);
                        break;
                    }
                    else
                    {
                        $"条码：{scanResults[0].Code}; 第[{n}]次写入 [{JsonSerializer.Serialize(_barceodeTag)}] 失败".LogProcess(_taskLogHeader);
                    }
                }
            }
            var result = (scanResults[0].ScanStatus == ScanBarcodeStatus.扫码成功 ? ResultTypeEnum.OK : ResultTypeEnum.NG);
            new SignalAddressModel($"{Context.DataAddress.Lable}.ToPLCData[0]", 0).WritePlcResult(result, ResultTypeEnum._, _plc, _parameterConfig, _taskLogHeader); //写入PLC结果
        }

        #endregion

        return Task.CompletedTask;
    }
}
