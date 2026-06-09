namespace Kinlo.Services.Handlers;

[DeviceConnec(ProcessTypeEnum.测短路, [CommunicationEnum.ShortCircuit_RJ6902R])] //指定工艺，可指定多个
public class RJ6902RHandler : ServiceHandlerBase
{
    #region 构造函数方法
    public RJ6902RHandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }

    #endregion

    protected override async Task HandleCore(short plcValue)
    {
        #region 从PLC读取电池ID数据
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

            #region 根据信号配置界面，拿到短路测试设备
            logHeader = Context.ToProcessLogHeader(plcValue, plcData.ID, mainBattery.Barcode);
            var device = _devicesConfig.GetRunDevice(Context, plcData.Index);

            IBatShortCircuitRj6902rModel shortCircuitTestRJ = (IBatShortCircuitRj6902rModel)mainBattery!;
            if (device == null)
            {
                shortCircuitTestRJ.ShortCircuitResult = ResultTypeEnum.未找到设备;
                AddDisplayData(mainBattery);
                _isDeviceAlarm = true;
                $"找不到设备，不给PLC发结果及完成信号，请停机检查！;".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
                return;
            }
            #endregion

            #region 执行短路测试方法，拿到短路测试值
            OperationResult<RJ6902RResultModel> deviceResult = OperationResult<RJ6902RResultModel>.Failure("默认");
            try
            {
                $"{(plcData.PLCDataType == 3 ? "负极外壳测试" : plcData.PLCData == 2 ? "正极外壳测试" : "正负极测试")}开始检测".LogProcess(logHeader);
                deviceResult = device.ReadClass<RJ6902RResultModel>(null, null, logHeader);
            }
            catch (Exception ex)
            {
                deviceResult = OperationResult<RJ6902RResultModel>.Failure(ResultTypeEnum.异常, "默认");
                $"获取结果时发生异常：{ex}".LogProcess(logHeader);
            }
            #endregion

            #region 短路测试结果赋值给电池
            shortCircuitTestRJ.ShortCircuitTestRJTime = DateTime.Now;
            shortCircuitTestRJ.ShortCircuitTestRJIndex = (byte)plcValue;
            switch (plcData.PLCData)
            {
                case 3: //负极外壳测试
                    break;
                case 2: //正极外壳测试
                    break;
                default: //正负极测试
                    if (!deviceResult.IsSuccess)
                    {
                        shortCircuitTestRJ.ShortCircuitResult = ResultTypeEnum.NG;
                    }
                    else
                    {
                        var rj6902RResult = deviceResult.Value!;
                        $"短路测试总结果:[{rj6902RResult.总结果}];获取结果原始值：[{deviceResult.Value?.TestMsg}]".LogProcess(logHeader);

                        shortCircuitTestRJ.ResistanceTestValue = rj6902RResult.电阻测试数据;
                        shortCircuitTestRJ.BoostTime = rj6902RResult.升压时间 / 10.00;
                        shortCircuitTestRJ.FallOne = rj6902RResult.跌落1;
                        shortCircuitTestRJ.FallTwo = rj6902RResult.跌落2;
                        shortCircuitTestRJ.ShellShortCircuitVoltage = rj6902RResult.VP电压;
                        shortCircuitTestRJ.ShellShortCircuitOpenCircuitResult = rj6902RResult.开路结果;
                        shortCircuitTestRJ.ShellShortCircuitFallOneResult = rj6902RResult.跌落1结果;
                        shortCircuitTestRJ.ShellShortCircuitFallTwoResult = rj6902RResult.跌落2结果;
                        shortCircuitTestRJ.ShellShortCircuitTLResult = rj6902RResult.TL结果;
                        shortCircuitTestRJ.ShellShortCircuitTHResult = rj6902RResult.TH结果;
                        shortCircuitTestRJ.ShellShortCircuitResistanceTestResult = rj6902RResult.电阻测试结果;
                        //如果要测外壳，此处重写
                        shortCircuitTestRJ.ShortCircuitResult = rj6902RResult.ConverterRJ6902R();
                    }
                    break;
            }

            #endregion

            #region 如果PLC给的ID为-1，执行点检方法
            if (isSpotCheck) //点检
            {
                AddDisplayData(mainBattery); //更新界面显示
                plcData.DataAddress.WritePlcResult(shortCircuitTestRJ.ShortCircuitResult, ResultTypeEnum._, _plc, _parameterConfig, logHeader); //写入PLC结果
                return;
            }
            #endregion

            #region 如果短路测试结果不合格，立刻执行上传MES方法
            if (shortCircuitTestRJ.ShortCircuitResult != ResultTypeEnum.OK)
            {
                // mainBattery.MesOutputTime = DateTime.Now;
                await MesOutput(mainBattery, logHeader);
            }
            #endregion

            #region 更新数据库电池表，刷新界面，写入PLC结果
            if (!await _sugarDB.UpdateByObjectAsync(mainBattery, logHeader))
            {
                shortCircuitTestRJ.ShortCircuitResult = ResultTypeEnum.保存数据库失败;
            }
            AddDisplayData(mainBattery);
            plcData.DataAddress.WritePlcResult(shortCircuitTestRJ.ShortCircuitResult, mainBattery.MesOutputStatus, _plc, _parameterConfig, logHeader); //写入PLC结果
            #endregion

        });
    }
}
