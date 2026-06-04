
//namespace Kinlo.Services.Handlers;


//[DeviceConnec(ProcessTypeEnum.测短路, [CommunicationEnum.ShortCircuit_ST5520])]//指定工艺，可指定多个
//public class ST5520Handler : ServiceHandlerBase
//{
//    private volatile int _outbound = 0, _okCount = 0, _ngCount = 0;
//    public ST5520Handler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken)
//    {

//    }
//    protected async override Task<int> HandleCore(short plcValue)
//    {
//        #region 写入PLC-1是否失败
//        _outbound = 0; _okCount = 0; _ngCount = 0;
//        if (!StartTask()) { return await base.Handle(); }
//        #endregion
//        try
//        {
//            ConcurrentQueue<IBatMainModel> queue = new ConcurrentQueue<IBatMainModel>();

//            #region 读取PLC当前电池ID，类型，读取PLC数据
//            if (!ReadPLCData(out List<ReceivePLCDataModel> plcDatas))
//                return await base.Handle();
//            TaskLog($"记忆ID：{string.Join(";", plcDatas.Select(x => x.ID))}", 0);
//            #endregion

//            await Parallel.ForEachAsync(plcDatas, new ParallelOptions { MaxDegreeOfParallelism = Context.DataLength }, async (plcData, _) =>
//            {
//                //我怀疑结果未判定就是短路测试延时那里的问题
//                Thread.Sleep(_parameterConfig.RunParameter.ShortDelay);

//                byte zane = plcData.Index;

//                #region 根据信号配置界面，获取到设备配置界面的设备
//                if (plcData.ID == 0)
//                    return;
//                #region 点睛之笔
//                if (Context.StartCommand.Tag.Lable == "PC_CMD.ShortCicuteCMD[1]")
//                {
//                    plcData.Index += 4;
//                }
//                if (Context.StartCommand.Tag.Lable == "PC_CMD.ShortCicuteCMD[2]")
//                {
//                    plcData.Index += 8;
//                }
//                #endregion

//                var _device = devicesConfig.RunDeviceList.FirstOrDefault(x
//                            => x.DeviceInfo.ServiceName == Context.ServiceName
//                            && x.DeviceInfo.ProcessesType == Context.ProcessesType
//                            && x.DeviceInfo.Communication == Context.DeviceCommunicationType
//                            && x.DeviceInfo.Index == plcData.Index);
//                ST5520ResultModel? st5520Result = null;

//                #endregion

//                #region 执行短路测试方法，拿到短路测试结果
//                if (_device == null)
//                {
//                    st5520Result = new ST5520ResultModel { Result = 98 };
//                    TaskLog($"找不到设备;", plcData.Index, Log4NetLevelEnum.错误, true);
//                }
//                else
//                {
//                    try
//                    {
//                        TaskLog($"短路测试开始检测", plcData.Index);
//                        string status = string.Empty;
//                        st5520Result = _device.ReadClass<ST5520ResultModel>(null);
//                        TaskLog($"获取电阻值：{st5520Result?.Resistance}", plcData.Index);
//                    }
//                    catch (Exception ex)
//                    {
//                        st5520Result = new ST5520ResultModel { Result = 99 };
//                        TaskLog($"获取结果时发生异常：{ex.ToString().Trim()}", plcData.Index);
//                    }
//                    if (st5520Result == null)
//                    {
//                        st5520Result = new ST5520ResultModel { Result = 99 };
//                        TaskLog($"采集数据异常", plcData.Index, Log4NetLevelEnum.错误);
//                    }
//                }
//                #endregion

//                #region 如果PLC给的ID为-1，执行点检方法
//                if (plcData.ID == -1)
//                {
//                    if (_device == null)
//                    {
//                        _isDeviceAlarm = true;
//                        TaskLog($"找不到设备;", plcData.Index, Log4NetLevelEnum.错误, true);
//                        WriteResult(ResultTypeEnum.找不到设备, plcData.DataAddress, string.Empty, plcData.Index);//写入PLC结果
//                        return;
//                    }
//                    var _lower = _parameterConfig.RunParameter.SpotCheckResistanceLower1;
//                    var _up = _parameterConfig.RunParameter.SpotCheckResistanceUpper1;

//                    ResultTypeEnum _checkResult = ResultTypeEnum.不合格;
//                    if (st5520Result.Resistance <= _up && st5520Result.Resistance >= _lower)
//                    {
//                        _checkResult = ResultTypeEnum.合格;
//                    }
//                    for (int n = 1; n < 4; n++)
//                    {
//                        var _st5520Tag = new SignalAddressModel($"{plcData.DataAddress.Lable}.PCData2", 0);
//                        var checkValue = Convert.ToSingle(st5520Result.Resistance);
//                        if (_plc.WriteValue(checkValue, _st5520Tag))
//                        {
//                            TaskLog($"短路测试点检阻值：{st5520Result.Resistance}; 第[{n}]次写入标签 [{_st5520Tag.Lable}] 成功", plcData.Index);
//                            break;
//                        }
//                    }
//                    "短路测试点检".LogCheck(_checkResult.ToString(), plcData.Index.ToString(), st5520Result.Resistance.ToString(), _up.ToString(), _lower.ToString(), _parameterConfig.DeviceParameter.DeviceCode, _checkResult == ResultTypeEnum.合格 ? Log4NetLevelEnum.成功 : Log4NetLevelEnum.错误);
//                    WriteResult(_checkResult, plcData.DataAddress, "点检", plcData.Index);

//                    #region 点检显示界面保存数据库
//                    var mainBattery2 = (IBatMainModel)Activator.CreateInstance(_displayDataCollection.CompleteBatteryDatas.RuntimeBatteryModel);
//                    mainBattery2.Id = _snowflakeHelper.NextId();
//                    mainBattery2.Barcode = "短路测试点检";
//                    var beforeWeight2 = (IBatShortCircuitST5520Model)mainBattery2;
//                    var beforeWeight3 = (IBatScanBeforeModel)mainBattery2;
//                    beforeWeight3.BeforeScanTime = DateTime.Now;
//                    beforeWeight2.ResistanceTestValue1 = (float)(st5520Result.Resistance);
//                    beforeWeight2.ShortCircuitResult = _checkResult;
//                    beforeWeight2.ShortCircuitTestRJIndex = plcData.Index;
//                    beforeWeight2.ShortCircuitTestRJTime = DateTime.Now;

//                    if(!await _sugarDB.InsertableByObjectAsync(mainBattery2))
//                    {
//                        beforeWeight2.ShortCircuitResult = ResultTypeEnum.保存数据库失败;
//                    }
//                    AddDisplayData(mainBattery2);
//                    #endregion

//                    #region 点检保存csv
//                    if (mainBattery2 != null)
//                    {
                       
//                        queue.Enqueue((IBatMainModel)mainBattery2);
//                    }
//                    #endregion

//                    return;
//                }
//                if (plcData.ID == -2)
//                {
//                    if (_device == null)
//                    {
//                        _isDeviceAlarm = true;
//                        TaskLog($"找不到设备;", plcData.Index, Log4NetLevelEnum.错误, true);
//                        WriteResult(ResultTypeEnum.找不到设备, plcData.DataAddress, string.Empty, plcData.Index);//写入PLC结果
//                        return;
//                    }
//                    var _lower = _parameterConfig.RunParameter.SpotCheckResistanceLower2;
//                    var _up = _parameterConfig.RunParameter.SpotCheckResistanceUpper2;

//                    ResultTypeEnum _checkResult = ResultTypeEnum.不合格;
//                    if (st5520Result.Resistance <= _up && st5520Result.Resistance >= _lower)
//                    {
//                        _checkResult = ResultTypeEnum.合格;
//                    }
//                    for (int n = 1; n < 4; n++)
//                    {
//                        var _st5520Tag = new SignalAddressModel($"{plcData.DataAddress.Lable}.PCData2", 0);
//                        var checkValue = Convert.ToSingle(st5520Result.Resistance);
//                        if (_plc.WriteValue(checkValue, _st5520Tag))
//                        {
//                            TaskLog($"短路测试点检阻值：{st5520Result.Resistance}; 第[{n}]次写入标签 [{_st5520Tag.Lable}] 成功", plcData.Index);
//                            break;
//                        }
//                    }
//                    "短路测试点检".LogCheck(_checkResult.ToString(), plcData.Index.ToString(), st5520Result.Resistance.ToString(), _up.ToString(), _lower.ToString(), _parameterConfig.DeviceParameter.DeviceCode, _checkResult == ResultTypeEnum.合格 ? Log4NetLevelEnum.成功 : Log4NetLevelEnum.错误);
//                    WriteResult(_checkResult, plcData.DataAddress, "点检", plcData.Index);
//                    return;
//                }
//                if (plcData.ID == -3)
//                {
//                    if (_device == null)
//                    {
//                        _isDeviceAlarm = true;
//                        TaskLog($"找不到设备;", plcData.Index, Log4NetLevelEnum.错误, true);
//                        WriteResult(ResultTypeEnum.找不到设备, plcData.DataAddress, string.Empty, plcData.Index);//写入PLC结果
//                        return;
//                    }
//                    var _lower = _parameterConfig.RunParameter.SpotCheckResistanceLower3;
//                    var _up = _parameterConfig.RunParameter.SpotCheckResistanceUpper3;

//                    ResultTypeEnum _checkResult = ResultTypeEnum.不合格;
//                    if (st5520Result.Resistance <= _up && st5520Result.Resistance >= _lower)
//                    {
//                        _checkResult = ResultTypeEnum.合格;
//                    }
//                    for (int n = 1; n < 4; n++)
//                    {
//                        var _st5520Tag = new SignalAddressModel($"{plcData.DataAddress.Lable}.PCData2", 0);
//                        var checkValue = Convert.ToSingle(st5520Result.Resistance);
//                        if (_plc.WriteValue(checkValue, _st5520Tag))
//                        {
//                            TaskLog($"短路测试点检阻值：{st5520Result.Resistance}; 第[{n}]次写入标签 [{_st5520Tag.Lable}] 成功", plcData.Index);
//                            break;
//                        }
//                    }
//                    "短路测试点检".LogCheck(_checkResult.ToString(), plcData.Index.ToString(), st5520Result.Resistance.ToString(), _up.ToString(), _lower.ToString(), _parameterConfig.DeviceParameter.DeviceCode, _checkResult == ResultTypeEnum.合格 ? Log4NetLevelEnum.成功 : Log4NetLevelEnum.错误);
//                    WriteResult(_checkResult, plcData.DataAddress, "点检", plcData.Index);
//                    return;
//                }

//                #endregion

//                #region 根据PLC给的ID，从缓存中拿到电池

//                var battery = await _batteryCache.GeByIdAsync(plcData.ID);//取缓存 
//                if (battery == null)
//                {
//                    TaskLog($"数据库中不存在ID:{plcData.ID};", Context.DeviceStartIndex, Log4NetLevelEnum.错误);
//                    _isDeviceAlarm = true;
//                    return;
//                }

//                #endregion

//                #region 判断短路测试结果123是否合格
//                var STtime = plcData.PLCData;
//                var shortData = (IBatShortCircuitST5520Model)battery;
//                shortData.ShortCircuitTestRJTime = DateTime.Now;
//                //shortData.ShortCircuitTestRJIndex = plcData.Index;
//                shortData.ShortCircuitTestRJIndex = zane;

//                shortData.NgStr = st5520Result.NgStr;



//                if (Context.StartCommand.Tag.Lable == "PC_CMD.ShortCicuteCMD[0]")
//                {
//                    shortData.ResistanceTestValue1 = st5520Result.Resistance;
//                    shortData.ShortCircuitResult1 = st5520Result.Result == 1 ? ResultTypeEnum.合格 : ResultTypeEnum.短路NG;
//                    shortData.ShortCircuitResult = st5520Result.Result == 1 ? ResultTypeEnum.合格 : ResultTypeEnum.短路NG;
//                }
//                else if(Context.StartCommand.Tag.Lable == "PC_CMD.ShortCicuteCMD[1]")
//                {
//                    shortData.ResistanceTestValue2 = st5520Result.Resistance;
//                    shortData.ShortCircuitResult2 = st5520Result.Result == 1 ? ResultTypeEnum.合格 : ResultTypeEnum.短路NG;
//                    if(shortData.ShortCircuitResult == ResultTypeEnum.合格)
//                    {
//                        shortData.ShortCircuitResult = st5520Result.Result == 1 ? ResultTypeEnum.合格 : ResultTypeEnum.短路NG;

//                    }
//                }
//                else
//                {
//                    shortData.ResistanceTestValue3 = st5520Result.Resistance;
//                    shortData.ShortCircuitResult3 = st5520Result.Result == 1 ? ResultTypeEnum.合格 : ResultTypeEnum.短路NG;

//                    if (shortData.ShortCircuitResult == ResultTypeEnum.合格)
//                    {
//                        shortData.ShortCircuitResult = st5520Result.Result == 1 ? ResultTypeEnum.合格 : ResultTypeEnum.短路NG;
//                    }
//                }


//                #endregion

             
//                #region 如果短路测试结果123有任何一个不合格，立刻执行上传MES方法
//                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

//                if (Context.StartCommand.Tag.Lable == "PC_CMD.ShortCicuteCMD[0]")
//                {
//                    if (shortData.ShortCircuitResult1 != ResultTypeEnum.合格)
//                    {
//                        $"条码{battery.Barcode}测短路不合格，立刻执行上传MES方法".LogRun(Log4NetLevelEnum.错误);

//                        await SendMesData(battery);


//                        #region 保存csv
//                        if (battery != null)
//                        {
//                            queue.Enqueue((IBatMainModel)battery);
//                        }

                        
                        
//                        #endregion

//                    }
//                }
//                else if (Context.StartCommand.Tag.Lable == "PC_CMD.ShortCicuteCMD[1]")
//                {
//                    if (shortData.ShortCircuitResult1 == ResultTypeEnum.合格 && shortData.ShortCircuitResult2 == ResultTypeEnum.短路NG)
//                    {
//                        $"条码{battery.Barcode}测短路不合格，立刻执行上传MES方法".LogRun(Log4NetLevelEnum.错误);

//                        await SendMesData(battery);
//                    }
//                }
//                else
//                {
//                    if (shortData.ShortCircuitResult1 == ResultTypeEnum.合格 && shortData.ShortCircuitResult2 == ResultTypeEnum.合格 && shortData.ShortCircuitResult3 == ResultTypeEnum.短路NG)
//                    {
//                        $"条码{battery.Barcode}测短路不合格，立刻执行上传MES方法".LogRun(Log4NetLevelEnum.错误);

//                        await SendMesData(battery);
//                    }
//                }

//                #endregion

//                #region 更新数据库电池表，写入PLC，更新界面



//                if (!await _sugarDB.UpdateByObjectAsync(battery))
//                {
//                    shortData.ShortCircuitResult = ResultTypeEnum.保存数据库失败;
//                }
//                if (shortData.ShortCircuitResult != ResultTypeEnum.合格)
//                    Interlocked.Increment(ref _ngCount);
//                else
//                    Interlocked.Increment(ref _okCount);

//                RemoveThenAddDisplayData(battery);
//                WriteResult(shortData.ShortCircuitResult, plcData.DataAddress, battery.Barcode, plcData.Index);

//                #endregion
//            });

//            #region 点检记录保存csv
//            if (plcDatas[0].ID == -1)
//            {
//                List<IBatMainModel> batteryList = new List<IBatMainModel>();
//                while (queue.TryDequeue(out IBatMainModel item))
//                {
//                    batteryList.Add(item);
//                }

//                ExportToCsv2(batteryList.ToArray());
//            }
//            //正常生产
//            else
//            {
//                List<IBatMainModel> batteryList = new List<IBatMainModel>();
//                while (queue.TryDequeue(out IBatMainModel item))
//                {
//                    batteryList.Add(item);
//                }

//                if(batteryList.Any()==true)
//                {
//                    ExportToCsv(batteryList.ToArray());
//                }
//            }

//            #endregion

//            #region 更新百分比界面
//            _displayPercentageCollection.AddPercentage(Context.ProcessesType, _okCount, _ngCount, 0);

//            #endregion
//        }
//        catch (Exception ex)
//        {

//            TaskLog($"出现异常:{ex};", Context.DeviceStartIndex, Log4NetLevelEnum.错误, true);
//        }
//        #region 写入PLC CMD=2
//        return await base.Handle();

//        #endregion
//    }
//}
