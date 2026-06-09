namespace Kinlo.Services.Handlers;

public partial class WeightAfterHandler
{
    /// <summary>
    /// 注液过少回流称重
    /// </summary>
    /// <param name="mainBattery"></param>
    /// <param name="weiging"></param>
    /// <param name="plcData"></param>
    /// <param name="logHeader"></param>
    /// <returns></returns>
    public async Task RefillHandle(IBatMainModel mainBattery, OperationResult<double> deviceResult, ReceivePlcDataModel plcData, string logHeader)
    {
        #region 本地属性赋值给电池
        var batBefScan = (IBatScanBeforeModel)mainBattery;
        var batAftWeight = (IBatWeightAfterModel)mainBattery;
        var autoRefillWeight = (IBatWeightAutoRefillModel)mainBattery;

        if (deviceResult.IsSuccess)
        {
            batAftWeight.AfterWeight = autoRefillWeight.AutoRefillWeight = deviceResult.Value;
        }

        autoRefillWeight.AutoRefillTime = DateTime.Now;
        autoRefillWeight.AutoRefillWeightIndex = plcData.Index;
        autoRefillWeight.AutoRefillVolume = autoRefillWeight.AutoRefillWeight - batAftWeight.FirstInjectWeight;

        batAftWeight.AfterWeighingResult = mainBattery.FinalWeightRangeCheck(_parameterConfig, logHeader);
        batAftWeight.TotalInjectionVolume = mainBattery.GetTotalInjectVolume(_parameterConfig, logHeader);
        batAftWeight.TotalInjectionVolumeDeviation = mainBattery.GetTotalInjectionVolumeDeviation(_parameterConfig, logHeader);
        autoRefillWeight.AutoRefillResult = mainBattery.GetTotalInjectionVolumeResult(_parameterConfig, logHeader);

        $"少液回流称重:[{autoRefillWeight.AutoRefillWeight}],称重结果为:[{batAftWeight.AfterWeighingResult}];回流注液结果为:[{autoRefillWeight.AutoRefillResult}]；".LogProcess(logHeader);

        #endregion

        #region 发送补液量至PLC

        //发送补液量至PLC
        var supplementaryRes = SupplementaryElectrolyteToPlc(batAftWeight.TotalInjectionVolumeDeviation, plcData, logHeader);

        #endregion

        #region 综合判断注液结果，发送给PLC
        //单纯获取发给PLC结果，此过程不会给电池赋值
        var sendPlcResult = ClaculatePlcResult(autoRefillWeight.AutoRefillResult, batAftWeight.AfterWeighingResult, supplementaryRes, mainBattery, logHeader);

        #endregion

        #region 如果还是注液量偏少，标记回流原因
        //如果是注液量偏少，就会回流，标记回流原因，真实回流后记录回流次数
        if (sendPlcResult == ResultTypeEnum.注液量偏少)
        {
            AddOrUpdateReworkReason(mainBattery);
        }
        #endregion

        #region 更新数据库电池表，刷新界面，写入PLC结果
        if (!await _sugarDB.UpdateByObjectAsync(mainBattery, logHeader))
        {
            autoRefillWeight.AutoRefillResult = ResultTypeEnum.保存数据库失败;
            sendPlcResult = ResultTypeEnum.保存数据库失败;
        }

        var processesDatas = _displayDataCollection.ProcessesDatas.FirstOrDefault(x =>  x.Processes == ProcessTypeEnum.回流补液 );
        processesDatas?.AddDisplayData(mainBattery); //更新至补液界面显示

        plcData.DataAddress.WritePlcResult(sendPlcResult, ResultTypeEnum._, _plc, _parameterConfig, logHeader); //写入PLC结果

        #endregion

    }
}
