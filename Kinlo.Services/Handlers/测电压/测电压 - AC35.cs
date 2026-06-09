namespace Kinlo.Services.Handlers;

[DeviceConnec(ProcessTypeEnum.测电压, [CommunicationEnum.VoltogeTest_AC35])] //指定工艺，可指定多个
public class TestVoltageAc35Handler : ServiceHandlerBase
{
    public TestVoltageAc35Handler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }

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

            #region 根据信号配置界面，找到电压测试设备
            logHeader = Context.ToProcessLogHeader(plcValue, plcData.ID, mainBattery.Barcode);
            var device = _devicesConfig.GetRunDevice(Context, plcData.Index);
            var voltage = (IBatVoltageTestAc35Model)mainBattery!;
            if (device == null)
            {
                voltage.VoltageTestResult = ResultTypeEnum.未找到设备;
                AddDisplayData(mainBattery);
                _isDeviceAlarm = true;
                $"找不到设备，不给PLC发结果及完成信号，请停机检查！;".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
                return;
            }

            #endregion

            #region 执行电压测试方法，拿到电压测试结果
            $"开始检测".LogProcess(logHeader);
            var deviceRs = device.ReadValue<(double acResistance, double voltage)>(null, logHeader, new DeviceOperationOptions(retryCount: _parameterConfig.RunParameter.VoltageTestCount + 1));

            if (deviceRs.IsSuccess)
            {
                voltage.AcResistance = Math.Round(deviceRs.Value.acResistance, 2);
                voltage.TestVoltageValue = Math.Round(deviceRs.Value.voltage, 5);
            }
            else
            {
                $"采集数据失败！".LogProcess(logHeader, Log4NetLevelEnum.警告);
            }
            #endregion

            #region 电压测试结果赋值给电池
            voltage.TestVoltageTime = DateTime.Now;
            voltage.TestVoltageIndex = (byte)plcValue;
            voltage.VoltageRange = $"{_parameterConfig.RunParameter.VoltageUpper}~{_parameterConfig.RunParameter.VoltageLower}";

            voltage.VoltageTestResult = deviceRs switch
            {
                var dr
                when dr.IsSuccess && dr.Value.voltage >= _parameterConfig.RunParameter.VoltageLower && dr.Value.voltage <= _parameterConfig.RunParameter.VoltageUpper => ResultTypeEnum.OK,
                _ => ResultTypeEnum.NG,
            };
            #endregion

            #region 如果PLC给的ID为-1，执行点检方法
            if (isSpotCheck) //点检
            {
                float checkValue = deviceRs.Value.voltage == null ? 0 : (float)deviceRs.Value.voltage;
                voltage.VoltageTestResult = Context.ProcessesType.RangeInspection(plcValue, _inspectionConfig, (deviceRs.IsSuccess, checkValue));
                OnSupplementaryElectrolyteToPlc(plcData, checkValue, logHeader, "[点检发送电压]");
                plcData.DataAddress.WritePlcResult(voltage.VoltageTestResult, ResultTypeEnum._, _plc, _parameterConfig, logHeader); //写入PLC结果
                                                                                                                                    //   AddDisplayData(mainBattery);//更新界面显示
                return;
            }
            #endregion

            #region 如果电压测试结果不合格，立刻执行MES上传方法
            if (voltage.VoltageTestResult != ResultTypeEnum.OK)
            {
                // mainBattery.MesOutputTime = DateTime.Now;
                await MesOutput(mainBattery, logHeader);
            }
            #endregion

            #region 更新数据库电池表，刷新界面，写入PLC结果
            if (!await _sugarDB.UpdateByObjectAsync(mainBattery, logHeader))
            {
                voltage.VoltageTestResult = ResultTypeEnum.保存数据库失败;
            }
            AddDisplayData(mainBattery);
            plcData.DataAddress.WritePlcResult(voltage.VoltageTestResult, mainBattery.MesOutputStatus, _plc, _parameterConfig, logHeader); //写入PLC结果
            #endregion

        });
    }
}
