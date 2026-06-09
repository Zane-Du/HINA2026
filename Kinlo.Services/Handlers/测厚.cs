namespace Kinlo.Services.Handlers;

/// <summary>
///  测厚
/// </summary>
[DeviceConnec(ProcessTypeEnum.测厚, CommunicationEnum.None)] //指定工艺，可指定多个
public class ThickHandler : ServiceHandlerBase
{
    #region 构造函数方法
    public ThickHandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }
    #endregion

    protected override async Task HandleCore(short plcValue)
    {

        #region 从PLC读取电池ID信息
        PlcToPcThicksDTU plcToPcThicks = new PlcToPcThicksDTU(Context.DataLength);
        _plc.ReadClass(Context.ExtraDataAddress, plcToPcThicks, _taskLogHeader);
        $"接收到的数据：{JsonSerializer.Serialize(plcToPcThicks, GenericHelper.SerializerOptions)}".LogProcess(_taskLogHeader);
        // await Parallel.ForEachAsync(plcDatas, new ParallelOptions { MaxDegreeOfParallelism = Context.DataLength + 2 }, async (plcData, _) =>
        #endregion

        await Parallel.ForAsync(0, plcToPcThicks.Battery.Count(), new ParallelOptions { MaxDegreeOfParallelism = Context.DataLength }, async (i, _) =>
        {

            #region 根据PLC给的ID，从缓存中拿到电池
            var plcData = plcToPcThicks.Battery[i];
            if (plcData.ID < 1)
            {
                return;
            }
            string logHeader = Context.ToProcessLogHeader(id: plcData.ID);
            var mainBattery = await _batteryCache.GetByIdAsync(plcData.ID, logHeader); //取缓存
            if (mainBattery == null)
            {
                _isDeviceAlarm = true; //有异常不写完成信号
                //WriteResult(ResultTypeEnum.数据库找不到电池, plcData.DataAddress, "数据库中不存在ID", plcData.Index);//写入PLC结果
                return;
            }
            #endregion

            #region PLC测厚数据赋值给电池
            logHeader = Context.ToProcessLogHeader(id: mainBattery.Id, barcode: mainBattery.Barcode);
            IBatThickModel batThick = (IBatThickModel)mainBattery;
            batThick.ThickTime = DateTime.Now;
            batThick.TopThickness = (float)Math.Round(plcData.CheckValue[0], 3);
            batThick.BottomThickness = (float)Math.Round(plcData.CheckValue[1], 3);
            batThick.TopThicknessResult = plcData.Result[0] == 1 ? "OK" : "NG";
            batThick.BottomThicknessResult = plcData.Result[1] == 1 ? "OK" : "NG";
            batThick.ThicknessRange = $"{plcToPcThicks.CheckLowerLimit}~{plcToPcThicks.CheckUpperLimit}";
            batThick.ThicknessPressure = plcToPcThicks.obligate;
            batThick.ThicknessPressureRange = $"{plcToPcThicks.obligateLowerLimit}~{plcToPcThicks.obligateUpperLimit}";
            batThick.TestThickResult =
             plcData.Result[0] == 1 && plcData.Result[1] == 1 ? ResultTypeEnum.OK : ResultTypeEnum.NG;
            batThick.ThickIndex = (byte)(i + 1);
            #endregion

            #region 如果测厚结果不合格，立刻执行MES上传方法
            if (batThick.TestThickResult != ResultTypeEnum.OK)
            {
                //  mainBattery.MesOutputTime = DateTime.Now;
                // await MesOutput(mainBattery, logHeader);
            }
            #endregion

            #region 更新数据库电池表，刷新界面，写入PLC结果
            if (!await _sugarDB.UpdateByObjectAsync(mainBattery, logHeader))
            {
                batThick.TestThickResult = ResultTypeEnum.保存数据库失败;
            }
            AddDisplayData(mainBattery);
            #endregion

        });
    }
}
