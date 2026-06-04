using static Kinlo.Common.DAL.DbHelper;

namespace Kinlo.Services.Handlers;

[DeviceConnec(ProcessTypeEnum.前扫码, [CommunicationEnum.ScanCode_SR1000, CommunicationEnum.ScanCode_SR700])]
public class ScanCodeBeforeHandler : ServiceHandlerBase
{
    #region 构造函数方法
    public ScanCodeBeforeHandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }

    #endregion

    #region 复投模式方法
    /// <summary>
    /// 处理复投
    /// </summary>
    /// <param name="mainBattery"></param>
    /// <param name="plcDatas"></param>
    /// <param name="dtIndex"></param>
    /// <param name="logHeader"></param>
    /// <returns></returns>
    private async Task<bool> HandleRework(IBatMainModel mainBattery, List<ReceivePlcDataModel> plcDatas, int dtIndex, string logHeader
    )
    {
        #region 一注从前扫码复投(plc给ID为99时代表复投电池)，处理干重及短路数据
        bool isReworkMode = false; //是否为复投模式
        bool isTest = true; //一注是否测短路
        if (_parameterConfig.FunctionEnable.IsRewrokMode) //本地或PLC任一 一方为复投模式都可
            isReworkMode = true;
        else
        {
            if (plcDatas != null)
            {
                var plcData = plcDatas.FirstOrDefault(x => x.Index == dtIndex + 1);
                if (plcData != null && plcData.ID == 99) //plc给ID为99时代表复投电池
                    isReworkMode = true;
            }
        }
        if (!isReworkMode)
        {
            return isTest;
        }

        var oldBattery = await _sugarDB.GetLastBattereyByBarcodeAsync(mainBattery.Barcode, logHeader);
        if (oldBattery == null)
            return isTest;

        $"找到上次电池数据，当前为复投电池！变量注液开关：{(_parameterConfig.FunctionEnable.IsEnableVariableInjection ? "开启" : "关闭")}；复用短路数据开关：{(_parameterConfig.FunctionEnable.IsEnableReuseOldTest ? "开启" : "关闭")}；".LogProcess(
           logHeader,
           Log4NetLevelEnum.信息
        );
        mainBattery.ReproductionCount++;

        if (_parameterConfig.AdvancedConfig.ProductionType != ProductionTypeEnum.一次注液) //只有一注才关心干重及短路测试
            return isTest;

        //如果上次干重在范围，使用上次干重

        if (
           BatteryWeightValidator.IncomingWeightRangeCheck(
              ((IBatScanBeforeModel)oldBattery).NetWeight,
              BatteryWeightValidator.GetBeforWeightRange(_parameterConfig),
              _parameterConfig,
              logHeader
           ) == ResultTypeEnum.OK
        )
            ((IBatScanBeforeModel)mainBattery).NetWeight = ((IBatScanBeforeModel)oldBattery).NetWeight;

        //如果上次短路数据合格，使用上次短路数据，不用测试
        if (_parameterConfig.FunctionEnable.IsEnableReuseOldTest)
        {
            try
            {
                if (oldBattery is IBatHipotAc3200Model oldTest)
                {
                    if (oldTest != null)
                    {
                        if (oldTest.HipotResult == ResultTypeEnum.OK)
                        {
                            isTest = false; //不测短路
                            $"复投旧短路OK,使用旧短路数据！".LogProcess(logHeader, Log4NetLevelEnum.信息);
                            ExpressionAssignmentMapper<IBatHipotAc3200Model, IBatHipotAc3200Model>.Trans(
                               oldTest,
                               (IBatHipotAc3200Model)mainBattery
                            );
                        }
                        else
                        {
                            isTest = true; //要测短路
                            $"复投旧短路NG,不使用旧短路数据！".LogProcess(logHeader, Log4NetLevelEnum.信息);
                        }
                    }
                }
                else
                {
                    $"复投取旧短路数据 数据类型不对应！".LogProcess(logHeader, Log4NetLevelEnum.警告, true);
                }
            }
            catch (Exception e)
            {
                $"复投取旧短路数据异常：{e}".LogProcess(logHeader, Log4NetLevelEnum.警告, true);
            }
        }

        return isTest;
        #endregion
    }
    #endregion

    #region 写入PLC方法
    /// <summary>
    /// 写入PLC
    /// </summary>
    /// <param name="mainBattery"></param>
    /// <param name="dtIndex"></param>
    /// <param name="shortRSS"></param>
    /// <param name="logHeader"></param>
    private void WritePLC(IBatMainModel mainBattery, int dtIndex, float shortRSS, string logHeader)
    {
        var idTag = new SignalAddressModel($"{Context.DataAddress.Lable}.ToPCData[{dtIndex}].ID", 0);
        for (int n = 1; n < 4; n++)
        {
            if (_plc.WriteValue(mainBattery.Id, idTag, logHeader))
            {
                $"第[{n}]次地址[{JsonSerializer.Serialize(idTag)}]写入ID[{mainBattery.Id}] 成功,条码：{mainBattery.Barcode};".LogProcess(
                   logHeader
                );
                break;
            }
            else
            {
                $"第[{n}]次地址[{JsonSerializer.Serialize(idTag)}]写入ID[{mainBattery.Id}] 失败,条码：{mainBattery.Barcode};".LogProcess(
                   logHeader
                );
            }
        }

        var shortPath = new SignalAddressModel($"{Context.DataAddress.Lable}.ToPLCData[{dtIndex}].PCData1", 0);

        for (int n = 1; n < 4; n++)
        {
            if (_plc.WriteValue(shortRSS, shortPath, logHeader))
            {
                $"第[{n}]次地址[{JsonSerializer.Serialize(shortPath)}]写入是否测短路[{shortRSS}] 成功,条码：{mainBattery.Barcode};".LogProcess(
                   logHeader
                );
                break;
            }
            else
            {
                $"第[{n}]次地址[{JsonSerializer.Serialize(shortPath)}]写入是否测短路[{shortRSS}] 失败,条码：{mainBattery.Barcode};".LogProcess(
                   logHeader
                );
            }
        }
        var sendResult = ((IBatScanBeforeModel)mainBattery).BeforeScanResult;
        WriteResultAndCodeToPLC(mainBattery.Barcode, sendResult, mainBattery.MesInputStatus, dtIndex, logHeader);
    }

    /// <summary>
    /// 写入条码入结果
    /// </summary>
    /// <param name="barcode"></param>
    /// <param name="productResult"></param>
    /// <param name="mesResult"></param>
    /// <param name="dtIndex"></param>
    /// <param name="logHeader"></param>
    private void WriteResultAndCodeToPLC(string barcode, ResultTypeEnum productResult, ResultTypeEnum mesResult, int dtIndex, string logHeader)
    {
        var barceodeTag = new SignalAddressModel($"{Context.DataAddress.Lable}.ToPLCData[{dtIndex}].Code", 0);
        for (int n = 1; n < 4; n++)
        {
            if (_plc.WriteValue(barcode, barceodeTag, logHeader))
            {
                $"第[{n}]次 地址[{JsonSerializer.Serialize(barceodeTag)}]写入条码[{barcode}] 成功".LogProcess(logHeader);
                break;
            }
            else
            {
                $"第[{n}]次 地址[{JsonSerializer.Serialize(barceodeTag)}]写入条码[{barcode}] 失败".LogProcess(logHeader);
            }
        }

        new SignalAddressModel($"{Context.DataAddress.Lable}.ToPLCData[{dtIndex}]").WritePlcResult(
           productResult,
           mesResult,
           _plc,
           _parameterConfig,
           logHeader
        ); //写入PLC结果
    }


    #endregion

    #region MES方法
    #region MES进站方法
    /// <summary>
    /// MES进站
    /// </summary>
    /// <param name="mainBattery"></param>
    /// <param name="logHeader"></param>
    /// <returns></returns>
    private async Task MesInput(IBatMainModel mainBattery, string logHeader)
    {
        var call = _mesInterfaceParameterConfig.GetApiCall(new MesRequestBuildNJGX.ArgsProductEntry(mainBattery.Barcode));
        if (call != null && call.IsEnable) //有进站接口并已启用
        {
            var inputResult = await _mesService.SendAsync(
               call,
               mainBattery.Barcode,
               receiveMes => receiveMes.MesCommonParse(logHeader)
            ); //进站接口

            var isMesSuccess = inputResult.ResultStatus;
            mainBattery.MesInputStatus = isMesSuccess.ToResult();
            if (isMesSuccess != MesResultStatusEnum.成功)
            {
                $"条码[{mainBattery.Barcode}]进站失败，结果 {inputResult.ResultStatus}！".LogProcess(
                   logHeader,
                   Log4NetLevelEnum.错误
                );
            }
        }
    }

    #endregion

    #region MES二次注液多工序防呆检验生产方法
    /// <summary>
    /// 二次注液多工序防呆检验生产接口
    /// </summary>
    /// <param name="mainBattery"></param>
    /// <param name="logHeader"></param>
    /// <returns></returns>
    private async Task MesSecondInjValidation(IBatMainModel mainBattery, string logHeader)
    {
        var call = _mesInterfaceParameterConfig.GetApiCall(
           new MesRequestBuildNJGX.ArgsSecondInjValidation(mainBattery.Barcode)
        );
        //二次注液多工序防呆检验生产接口
        if (call != null && call.IsEnable)
        {
            var inputResult = await _mesService.SendAsync(
               call,
               mainBattery.Barcode,
               receiveMes => receiveMes.MesCommonParse(logHeader)
            );

            if (inputResult.ResultStatus != MesResultStatusEnum.成功)
            {
                if (mainBattery.MesInputStatus.GetResultArea() is ResultArea.Ignore or ResultArea.OK) //如果没进站或OK就赋值
                    mainBattery.MesInputStatus = inputResult.ResultStatus.ToResult();

                $"条码[{mainBattery.Barcode}]二次注液多工序防呆检验生产接口失败；结果：{inputResult.ResultStatus}！".LogProcess(
                   logHeader,
                   Log4NetLevelEnum.错误
                );
            }
        }
    }

    #endregion

    #region MES获取工单方法
    /// <summary>
    /// MES获取 工单
    /// </summary>
    /// <param name="mainBattery"></param>
    /// <param name="logHeader"></param>
    /// <returns></returns>
    private async Task MesGetWrokOrder(IBatMainModel mainBattery, ParameterConfig parameterConfig, string logHeader)
    {
        var workOrderNumber = parameterConfig.DeviceParameter.WorkOrderNo;
        var call = _mesInterfaceParameterConfig.GetApiCall(new MesRequestBuildNJGX.ArgsWorkOrder(mainBattery.Barcode));
        if (call != null && call.IsEnable) //有工单接口并已启用
        {
            var mesWorkOrderResult = await _mesService.SendAsync(
               call,
               mainBattery.Barcode,
               receiveMes => receiveMes.MesCommonParse(logHeader).WorkOrderParse()
            );

            var isMesSuccess = mesWorkOrderResult.ResultStatus;
            if (mesWorkOrderResult.ResultStatus == MesResultStatusEnum.成功) //更新数据
            {
                WorkOrderDto? workOrder = mesWorkOrderResult.Data?.FirstOrDefault();
                if (workOrder != null)
                {
                    workOrderNumber = workOrder.produceOrderCode;
                    //mainBattery.WorkOrderNumber = workOrder.produceOrderCode;
                    //_parameterConfig.UpdateParameter((param, display) =>//修改参数（包括显示页）
                    //{
                    //    param.DeviceParameter.WorkOrderNo = mainBattery.WorkOrderNumber;
                    //    display.DeviceParameter.WorkOrderNo = mainBattery.WorkOrderNumber;
                    //});
                }
                else
                {
                    $"条码[{mainBattery.Barcode}]获取工单失败，使用默认工单号！".LogProcess(
                       logHeader,
                       Log4NetLevelEnum.错误
                    );
                }
            }
        }
        mainBattery.WorkOrderNumber = workOrderNumber;
    }

    #endregion

    #region MES取前工序数据方法
    /// <summary>
    /// 取前工序数据
    /// </summary>
    /// <param name="mainBattery"></param>
    /// <param name="logHeader"></param>
    /// <returns></returns>
    private async Task GetPrimaryInjectData(IBatMainModel mainBattery, string logHeader)
    {
        if (
           mainBattery.FinalStatus == ResultTypeEnum.OK
           && _parameterConfig.FunctionEnable.IsEnableVariableInjection
           && (
              _parameterConfig.AdvancedConfig.ProductionType == ProductionTypeEnum.回氦
              || _parameterConfig.AdvancedConfig.ProductionType == ProductionTypeEnum.二次注液
              || _parameterConfig.AdvancedConfig.ProductionType == ProductionTypeEnum.三次注液
           )
        )
        {
            PreProcessData prePrcessData = new PreProcessData(PrePrcessDataEnum.失败, 0, 0, 0, 0, mainBattery.Barcode);

            //优先级1->如MES未取到数据再从设备取干重
            if (
               _parameterConfig.AdvancedConfig.IsEnableNetWeightFromDevice
               && prePrcessData.status != PrePrcessDataEnum.成功
            )
            {
                prePrcessData = await _sugarDB.GetOtherDatabaseDataAsync(mainBattery.Barcode, logHeader);
            }
            //优先级2->从MES取干重
            if (
               _parameterConfig.AdvancedConfig.IsEnablePreProcessDataFromMES
               && prePrcessData.status != PrePrcessDataEnum.成功
            ) //mes没有取数据接口
            {
                $"开始从MES获取前工序数据！".LogProcess(logHeader);
                var call = _mesInterfaceParameterConfig.GetApiCall(
                   new MesRequestBuildNJGX.ArgsGetPrimaryEntry(mainBattery.Barcode)
                );
                if (call != null && call.IsEnable) //有 获取一注数据接口并已启用
                {
                    var mesResult = await _mesService.SendAsync(
                       call,
                       mainBattery.Barcode,
                       receiveMes => receiveMes.MesCommonParse(logHeader).GetPrimaryInjectDataParse()
                    );

                    if (mesResult.ResultStatus == MesResultStatusEnum.成功)
                    {
                        var data = mesResult.Data?.FirstOrDefault(x => x.barcode == mainBattery.Barcode);
                        if (data != null)
                        {
                            prePrcessData = data;
                        }
                    }
                }
            }

            if (mainBattery is IBatScanBeforeModel batBefScan)
            {
                if (prePrcessData.status != PrePrcessDataEnum.失败)
                {
                    batBefScan.NetWeight = prePrcessData.frontWeight;
                    batBefScan.PreProcessWeight = prePrcessData.rearWeight;
                }
                batBefScan.BeforeScanResult = prePrcessData.status switch
                {
                    PrePrcessDataEnum.成功 => batBefScan.BeforeScanResult,
                    PrePrcessDataEnum.前工序数据不在范围 => ResultTypeEnum.前工序数据不在范围,
                    _ => ResultTypeEnum.取前工序数据失败,
                };
            }
            else
            {
                $"取电池干重 数据类型不对应！".LogProcess(logHeader, Log4NetLevelEnum.警告, true);
            }
        }
    }
    #endregion

    #endregion

    protected override async Task HandleCore(short plcValue)
    {

        #region 读取PLCID，判断是否为空
        string logHeader1 = Context.ToProcessLogHeader(plcValue);
        ScanBarcodeResultDto[] scanCodeResults = new ScanBarcodeResultDto[Context.DataLength];
        for (int i = 0; i < Context.DataLength; i++)
            scanCodeResults[i] = new ScanBarcodeResultDto();

        var isReadPlcSuccess = TryReadPlcData(out var plcDatas, showErrorDialog: false); //读取PLC数据
        if (isReadPlcSuccess)
        {
            $"收到PLC数据{JsonSerializer.Serialize(plcDatas)}".LogProcess(logHeader1);
        }
        else
        {
            $"未收到PLC数据！".LogProcess(logHeader1, Log4NetLevelEnum.警告);
        }
        #endregion

        #region 如果启用虚拟码或者空跑模式为虚拟条码
        if (_parameterConfig.FunctionEnable.IsEnablePseudoCode || _parameterConfig.FunctionEnable.IsEmptyLoadMode)
        {
            for (int k = 0; k < scanCodeResults.Length; k++)
            {
                scanCodeResults[k].ScanStatus = ScanBarcodeStatus.扫码成功;
                scanCodeResults[k].Code =
                   $"Empty{Guid.NewGuid().ToString().Substring(0, 5)}1234567890{new Random().Next(1, 9999):D4}";
            }
        }
        #endregion

        #region 根据信号配置界面拿到设备，执行前扫码方法（有PLC指定通道内是否有电池）
        else
        {
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
            scanCodeResults = ScanBarcodeHelpre.ScanCode(
               device,
               _parameterConfig,
               Context,
               isHaveBatterys.content,
               _taskLogHeader,
               _parameterConfig.AdvancedConfig.BatteryBarcodeValidationRule
            );
        }
        #endregion

        #region 如果ID为-1，执行点检方法
        if (plcDatas.Any(x => x.ID == -1)) //-1为点检
        {
            var inspectionResult = Context.ProcessesType.ScanCodeInspection(_inspectionConfig, plcValue, scanCodeResults);
            for (int i = 0; i < inspectionResult.Length; i++)
            {
                var result = inspectionResult[i];
                WriteResultAndCodeToPLC(result.barcode, result.result, ResultTypeEnum._, i, logHeader1);
            }
            return;
        }
        #endregion

        await Parallel.ForAsync(0, Context.DataLength, new ParallelOptions { MaxDegreeOfParallelism = Context.DataLength }, async (dtIndex, _) =>
        {
            #region 根据PLC结果判断当前通道内有无电池，如无电池直接返回
            int lane = plcValue + dtIndex; //通道
            var logHeader = Context.ToProcessLogHeader(lane);
            if (scanCodeResults[dtIndex].ScanStatus == ScanBarcodeStatus.当前通道无电池)
            {
                $"当前通道PLC指示无电池，直接返回；".LogProcess(logHeader, Log4NetLevelEnum.警告, true);
                return;
            }
            #endregion

            #region new一个电池，生成唯一雪花ID赋值给电池，本地属性赋值给电池
            var mainBattery = (IBatMainModel)Activator.CreateInstance(_displayDataCollection.CompleteBatteryDatas.RuntimeBatteryType);
            mainBattery.Id = _snowflakeHelper.NextId();
            IBatScanBeforeModel batBefScan = (IBatScanBeforeModel)mainBattery;
            batBefScan.BeforeScanTime = DateTime.Now;
            batBefScan.BeforeScanIndex = (byte)lane;
            mainBattery.DeviceCode = _parameterConfig.DeviceParameter.DeviceCode;
            mainBattery.ElectrolyteBatch = _parameterConfig.DeviceParameter.ElectrolyteLotCode;
            mainBattery.GlueNailBatch = _parameterConfig.DeviceParameter.GlueNailCode;
            mainBattery.SetBatteryRange(_parameterConfig); //写入电池范围值

            logHeader = Context.ToProcessLogHeader(lane, mainBattery.Id);
            bool isTest = true; //是否测短路

            batBefScan.BeforeScanResult =
               scanCodeResults[dtIndex].ScanStatus == ScanBarcodeStatus.扫码成功
                  ? ResultTypeEnum.OK
                  : ResultTypeEnum.扫码失败;

            string tempCode =
               batBefScan.BeforeScanResult == ResultTypeEnum.OK
                  ? scanCodeResults[dtIndex].Code
                  : mainBattery.Id.ToString();

            #endregion

            #region 如果扫码失败，报警
            if (batBefScan.BeforeScanResult == ResultTypeEnum.扫码失败)
            {
                mainBattery.MesOutputTime = batBefScan.BeforeScanTime;
                $"扫码失败".LogProcess(logHeader, Log4NetLevelEnum.警告, true);
            }
            #endregion

            #region 扫码成功扫码枪条码赋值给电池
            else
            {
                batBefScan.BeforeScanResult = ResultTypeEnum.OK;
                mainBattery.Barcode = scanCodeResults[dtIndex].Code;
                logHeader = Context.ToProcessLogHeader(lane, mainBattery.Id, mainBattery.Barcode);

            #endregion

            #region 如果复投模式开启，执行复投方法
                //处理复投电池
                isTest = await HandleRework(mainBattery, plcDatas, dtIndex + 1, logHeader);

                #endregion



            #region MES执行进站方法
                //MES进站
                await MesInput(mainBattery, logHeader);

                #endregion

            #region MES执行多工序防呆校验生产方法
                //二次注液多工序防呆检验生产接口
                if (_parameterConfig.AdvancedConfig.ProductionType == ProductionTypeEnum.二次注液)
                    await MesSecondInjValidation(mainBattery, logHeader);

                #endregion

            #region MES执行获取工单号方法
                //MES获取工单号
                await MesGetWrokOrder(mainBattery, _parameterConfig, logHeader);

                #endregion

            #region MES执行取前工序数据方法

                // 取一注数据优先级: MES -> 本地机台(如果用U盘导入数据，可以导入到本地，然后配置数据库连接取)
                await GetPrimaryInjectData(mainBattery, logHeader);
                #endregion



            #region 插入数据库电池表，插入本地电芯缓存
                _batteryCache.Put(mainBattery, logHeader); //电芯缓存
            }

            if (!await _sugarDB.InsertableByObjectAsync(mainBattery, logHeader))
                batBefScan.BeforeScanResult = ResultTypeEnum.保存数据库失败;

                #endregion

            #region 刷新界面，写入PLC结果
            //添加界面显示
            base.AddDisplayData(mainBattery);

            // 写入PLC ,为2不测短路，其它都测
            WritePLC(mainBattery, dtIndex, isTest ? 1f : 2f, logHeader);

            #endregion
        });
    }

}
