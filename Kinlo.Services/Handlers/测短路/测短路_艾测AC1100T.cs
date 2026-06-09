namespace Kinlo.Services.Handlers;

[DeviceConnec(ProcessTypeEnum.测短路, [CommunicationEnum.ShortCircuit_AC1100T])] //指定工艺，可指定多个
public class HipotAC1100THandler : ServiceHandlerBase
{
    public HipotAC1100THandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }

    protected override async Task HandleCore(short plcValue)
    {
        #region 从PLC读取电池ID信息
        if (!TryReadPlcData(out List<ReceivePlcDataModel> plcDatas))
        {
            return;
        }
        #endregion


        await Parallel.ForEachAsync(plcDatas, new ParallelOptions { MaxDegreeOfParallelism = Context.DataLength }, async (plcData, _) =>
        {

            #region 根据PLC给的ID，从缓存中拿到电池
            bool isSpotCheck = plcData.ID == -1; //是否为点检
            string logHeader = Context.ToProcessLogHeader(plcValue, plcData.ID);
            IBatMainModel? mainBattery = isSpotCheck switch
            {
                true => Activator.CreateInstance(_displayDataCollection.CompleteBatteryDatas.RuntimeBatteryType) as IBatMainModel, //点检
                _ => await _batteryCache.GetByIdAsync(plcData.ID, logHeader), //取缓存
            };
            if (mainBattery == null)
            {
                if (isSpotCheck)
                {
                    "创建点检电池对象失败；".LogProcess(logHeader, Log4NetLevelEnum.错误, true);

                }
                plcData.DataAddress.WritePlcResult(ResultTypeEnum.数据库找不到电池, ResultTypeEnum._, _plc, _parameterConfig, logHeader); //写入PLC结果
                return;
            }

            #endregion

            #region 根据信号配置界面，找到短路测试设备
            logHeader = Context.ToProcessLogHeader(plcValue, plcData.ID, mainBattery.Barcode);
            var device = _devicesConfig.GetRunDevice(Context, plcData.Index);

            var shortCircuitTestAC = (IBatHipotAc1100TModel)mainBattery!;
            if (device == null)
            {
                shortCircuitTestAC.HiptionOverallResult = ResultTypeEnum.未找到设备;
                AddDisplayData(mainBattery); //更新界面显示
                _isDeviceAlarm = true;
                $"找不到设备，不给PLC发结果及完成信号，请停机检查！;".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
                return;
            }

            #endregion

            #region 执行短路测试方法，拿到短路测试结果
            int optionType = 0;
            var oldResult = shortCircuitTestAC.HiptionOverallResult;

            var deviceResult = device.ReadClass<AC1100THipotResultModel>(null, null, logHeader, new DeviceOperationOptions { OperationType = optionType })!;
            #endregion

            #region 短路测试结果赋值给电池
            if (!deviceResult.IsSuccess)
            {
                shortCircuitTestAC.HiptionOverallResult = ResultTypeEnum.NG;
            }
            else
            {
                var result = deviceResult.Value!;
                shortCircuitTestAC.HipotTime = DateTime.Now;
                shortCircuitTestAC.HipotIndex = (byte)plcValue;
                shortCircuitTestAC.HiptionOverallResult = result.OverallResult;
                shortCircuitTestAC.HiptionCaseResult = result.CaseResult;
                shortCircuitTestAC.PositiveToNegativeResult = result.PositiveToNegativeResult;
                shortCircuitTestAC.PositiveToNegativeVpVoltage = result.PositiveToNegativeVpVoltage;
                shortCircuitTestAC.PositiveToNegativeVD1 = result.PositiveToNegativeVD1;
                shortCircuitTestAC.PositiveToNegativeVD2 = result.PositiveToNegativeVD2;
                shortCircuitTestAC.PositiveToNegativeVD3 = result.PositiveToNegativeVD3;
                shortCircuitTestAC.PositiveToNegativeTP = result.PositiveToNegativeTP;
                shortCircuitTestAC.PositiveToNegativeInsulation = result.PositiveToNegativeInsulation;

                shortCircuitTestAC.PositiveToCaseResult = result.PositiveToCaseResult;
                shortCircuitTestAC.PositiveToCaseVpVoltage = result.PositiveToCaseVpVoltage;
                shortCircuitTestAC.PositiveToCaseVD1 = result.PositiveToCaseVD1;
                shortCircuitTestAC.PositiveToCaseVD2 = result.PositiveToCaseVD2;
                shortCircuitTestAC.PositiveToCaseVD3 = result.PositiveToCaseVD3;
                shortCircuitTestAC.PositiveToCaseTP = result.PositiveToCaseTP;
                shortCircuitTestAC.PositiveToCaseInsulation = result.PositiveToCaseInsulation;
                shortCircuitTestAC.PositiveToCaseWeakConduction = result.PositiveToCaseWeakConduction;

                shortCircuitTestAC.NegativeToCaseResult = result.NegativeToCaseResult;
                shortCircuitTestAC.NegativeToCaseVpVoltage = result.NegativeToCaseVpVoltage;
                shortCircuitTestAC.NegativeToCaseVD1 = result.NegativeToCaseVD1;
                shortCircuitTestAC.NegativeToCaseVD2 = result.NegativeToCaseVD2;
                shortCircuitTestAC.NegativeToCaseVD3 = result.NegativeToCaseVD3;
                shortCircuitTestAC.NegativeToCaseTP = result.NegativeToCaseTP;
                shortCircuitTestAC.NegativeToCaseInsulation = result.NegativeToCaseInsulation;
            }

            #endregion

            #region 如果PLC给的ID为-1，执行点检方法
            if (isSpotCheck) //点检
            {
                AddDisplayData(mainBattery); //更新界面显示
                plcData.DataAddress.WritePlcResult(shortCircuitTestAC.HiptionOverallResult, ResultTypeEnum._, _plc, _parameterConfig, logHeader); //写入PLC结果
                return;
            }
            #endregion

            #region 如果短路测试结果不合格，立刻执行上传MES方法
            if (shortCircuitTestAC.HiptionOverallResult != ResultTypeEnum.OK)
            {
                //  mainBattery.MesOutputTime = DateTime.Now;
                await MesOutput(mainBattery, logHeader);
            }
            #endregion

            #region 更新数据库电池表，刷新界面，写入PLC结果
            if (!await _sugarDB.UpdateByObjectAsync(mainBattery, logHeader))
            {
                shortCircuitTestAC.HiptionOverallResult = ResultTypeEnum.保存数据库失败;
            }
            AddDisplayData(mainBattery); //更新界面显示
            plcData.DataAddress.WritePlcResult(shortCircuitTestAC.HiptionOverallResult, mainBattery.MesOutputStatus, _plc, _parameterConfig, logHeader);
            #endregion

        });
    }
}
