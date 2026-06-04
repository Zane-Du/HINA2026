namespace Kinlo.Services.Handlers;

[DeviceConnec(ProcessTypeEnum.后扫码, [CommunicationEnum.ScanCode_SR1000, CommunicationEnum.ScanCode_SR700])]
public class ScanCodeAfterHandler : ServiceHandlerBase
{
    #region 构造函数方法
    public ScanCodeAfterHandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }

    #endregion

    protected override async Task HandleCore(short plcValue)
   {
        #region 从PLC读取电池ID信息
        var isReadPlcSuccess = TryReadPlcData(out List<ReceivePlcDataModel> plcDatas);
        if (!isReadPlcSuccess)
            return;
        #endregion

        #region 如果启用空跑模式，直接返回
        string logHeader1 = Context.ToProcessLogHeader(plcValue);
        if (_parameterConfig.FunctionEnable.IsEmptyLoadMode) //空跑则不去获取设备数据
        {
            $"开启空载模式，直接合格完成;".LogProcess(logHeader1, Log4NetLevelEnum.警告);
            var address = new SignalAddressModel(
               $"{Context.DataAddress.Lable}.ToPLCData[{Context.DeviceStartIndex - 1}]",
               0
            );
            address.WritePlcResult(ResultTypeEnum.OK, ResultTypeEnum._, _plc, _parameterConfig, logHeader1);
            return;
        }

        #endregion

        #region 根据信号配置界面拿到设备，执行后扫码方法
        var device = _devicesConfig.GetRunDevice(Context, Context.DeviceStartIndex);
        if (device == null)
        {
            _isDeviceAlarm = true;
            $"找不到设备，不给PLC发结果及完成信号，请停机检查！;".LogProcess(logHeader1, Log4NetLevelEnum.错误, true);
            return;
        }
        var isHaveBatterys = GetLaneBatteryStatus(isReadPlcSuccess, plcDatas, logHeader1);
        if (!isHaveBatterys.status) //PLC指定通道是否有电池
        {
            _isDeviceAlarm = true;
            return;
        }
        var scanCodeResults = ScanBarcodeHelpre.ScanCode(
           device,
           _parameterConfig,
           Context,
           isHaveBatterys.content,
           logHeader1,
           _parameterConfig.AdvancedConfig.BatteryBarcodeValidationRule
        );

        #endregion
        await Parallel.ForEachAsync(plcDatas,  new ParallelOptions { MaxDegreeOfParallelism = Context.DataLength },   async (plcData, _) =>
        {
             #region Log日志记录
             int lane = plcValue + plcData.Index - 1; //通道
             int scanIndex = plcData.Index <= 0 ? 0 : plcData.Index - 1; //条码位置
             var logHeader = Context.ToProcessLogHeader(lane, plcData.ID);
             if (scanCodeResults.Length <= scanIndex)
             {
                 $"PLC数据对应位置[{scanIndex + 1}]大于扫码枪最大个数[{scanCodeResults.Length}]!".LogProcess(
                    logHeader,
                    Log4NetLevelEnum.错误,
                    true
                 );
                 plcData.DataAddress.WritePlcResult(
                    ResultTypeEnum.索引超界,
                    ResultTypeEnum._,
                    _plc,
                    _parameterConfig,
                    logHeader
                 );
                 return;
             }
             IBatScanAfterModel? afterScan = null;
             IBatMainModel? mainBattery = null;
             logHeader = Context.ToProcessLogHeader(lane, plcData.ID, scanCodeResults[scanIndex].Code);

             #endregion

             #region 如果PLC给的ID为-1，执行点检方法
             if (plcData.ID == -1)
            {
               "点检!".LogProcess(logHeader);
               mainBattery = (IBatMainModel)
                  Activator.CreateInstance(_displayDataCollection.CompleteBatteryDatas.RuntimeBatteryType);
               afterScan = (IBatScanAfterModel)mainBattery;
               afterScan.AfterIndex = (byte)lane;
               mainBattery.Barcode = scanCodeResults[scanIndex].Code;
               afterScan.AfterScanResult =
                  scanCodeResults[scanIndex].ScanStatus == ScanBarcodeStatus.扫码失败
                     ? ResultTypeEnum.扫码失败
                     : ResultTypeEnum.OK;
               AddDisplayData(mainBattery);
               plcData.DataAddress.WritePlcResult(
                  afterScan.AfterScanResult,
                  ResultTypeEnum._,
                  _plc,
                  _parameterConfig,
                  logHeader
               );
               return;
            }
             #endregion

             #region 根据PLC给的ID，从缓存中拿到电池，后扫结果赋值给电池
             mainBattery = await _batteryCache.GetByIdAsync(plcData.ID, logHeader);
             if (mainBattery == null)
             {
                 plcData.DataAddress.WritePlcResult(
                    ResultTypeEnum.数据库找不到电池,
                    ResultTypeEnum._,
                    _plc,
                    _parameterConfig,
                    logHeader
                 ); //写入PLC结果
                 return;
             }
             logHeader = Context.ToProcessLogHeader(lane, plcData.ID, mainBattery.Barcode);
             afterScan = (IBatScanAfterModel)mainBattery;
             afterScan.AfterIndex = (byte)plcValue;
             afterScan.AfterScanTime = DateTime.Now;

             afterScan.AfterScanResult =
                scanCodeResults[scanIndex].ScanStatus == ScanBarcodeStatus.扫码成功
                   ? ResultTypeEnum.OK
                   : ResultTypeEnum.扫码失败;

             #endregion

             #region 判断电池后扫码是否==电池前扫码
             if (afterScan.AfterScanResult == ResultTypeEnum.OK)
             {
                 if (scanCodeResults[scanIndex].Code == mainBattery.Barcode)
                 {
                     afterScan.AfterScanResult = ResultTypeEnum.OK;
                 }
                 else
                 {
                     afterScan.AfterScanResult = ResultTypeEnum.条码对比NG;
                     $"-[ID：[{mainBattery.Id} ,记忆条码：[{mainBattery.Barcode}] ;扫码枪条码：[{scanCodeResults[scanIndex].Code}] 不同！,[{Context.DeviceStartIndex}]号扫码枪，位置[{plcData.Index}];".LogProcess(
                        logHeader,
                        Log4NetLevelEnum.错误
                     );
                 }
             }
             #endregion

             #region 更新数据库电池表，刷新界面，写入PLC结果
             if (!await _sugarDB.UpdateByObjectAsync(mainBattery, logHeader))
             {
                 afterScan.AfterScanResult = ResultTypeEnum.保存数据库失败;
             }

             AddDisplayData(mainBattery);
             plcData.DataAddress.WritePlcResult(afterScan.AfterScanResult, ResultTypeEnum._, _plc, _parameterConfig, logHeader); //写入PLC结果

             #endregion

         }
      );
   }
}
