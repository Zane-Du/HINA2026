namespace Kinlo.Services.Handlers;

[DeviceConnec(ProcessTypeEnum.测短路, [CommunicationEnum.ShortCircuit_ST5520])] //指定工艺，可指定多个
public class ST5520Handler : ServiceHandlerBase
{
    #region 构造函数方法
    public ST5520Handler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }
    #endregion

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

            #region 根据信号配置界面，拿到短路测试设备
            logHeader = Context.ToProcessLogHeader(plcValue, plcData.ID, mainBattery.Barcode);
            var device = _devicesConfig.GetRunDevice(Context, plcData.Index);
            var shortData = (IBatShortCircuitST5520Model)mainBattery;
            if (device == null)
            {
                //shortData.HipotResult = ResultTypeEnum.未找到设备;
                AddDisplayData(mainBattery); //更新界面显示
                _isDeviceAlarm = true;
                $"找不到设备，不给PLC发结果及完成信号，请停机检查！;".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
                return;
            }

            #endregion

            #region 执行短路测试方法，拿到短路测试结果
            var deviceResult = device.ReadClass<ST5520ResultModel>(null!, null!, logHeader, null)!;

            #endregion

            #region 短路测试结果赋值给电池
            if (!deviceResult.IsSuccess)
            {
                shortData.ShortCircuitResult = ResultTypeEnum.NG;
            }
            else
            {
                //shortData.ResistanceTestValue1 = deviceResult.Resistance;
                //shortData.ShortCircuitResult1 = deviceResult.Result == 1 ? ResultTypeEnum.OK : ResultTypeEnum.Hipot_NG;
                //shortData.ShortCircuitResult = deviceResult.Result == 1 ? ResultTypeEnum.OK : ResultTypeEnum.Hipot_NG;

            }

            #endregion

            #region 如果PLC给的ID为-1，执行点检方法
            if (isSpotCheck) //点检
            {
                AddDisplayData(mainBattery); //更新界面显示
                plcData.DataAddress.WritePlcResult(shortData.ShortCircuitResult, ResultTypeEnum._, _plc, _parameterConfig, logHeader); //写入PLC结果
                return;
            }

            #endregion

            #region 如果短路测试结果不合格，立刻执行上传MES方法
            if (shortData.ShortCircuitResult != ResultTypeEnum.OK)
            {
                // mainBattery.MesOutputTime = DateTime.Now;
                await MesOutput(mainBattery, logHeader);
            }
            #endregion

            #region 更新数据库电池表，刷新界面，写入PLC结果
            if (!await _sugarDB.UpdateByObjectAsync(mainBattery, logHeader))
            {
                shortData.ShortCircuitResult = ResultTypeEnum.保存数据库失败;
            }

            AddDisplayData(mainBattery); //更新界面显示
            plcData.DataAddress.WritePlcResult(shortData.ShortCircuitResult, mainBattery.MesOutputStatus, _plc, _parameterConfig, logHeader);

            #endregion

        });
    }
}

