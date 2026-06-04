namespace Kinlo.Services.Handlers;

public partial class WeightAfterHandler
{
   protected async Task AfterWeightHanler( IBatMainModel mainBattery, OperationResult<double> deviceResult,  ReceivePlcDataModel plcData,  string logHeader )
   {

        #region 本地属性赋值给电池
        IBatWeightBeforeModel batBefWeight = _parameterConfig.AdvancedConfig.ProductionType == ProductionTypeEnum.回氦 ? new BatWeightBeforeModel() : (IBatWeightBeforeModel)mainBattery;

        IBatScanBeforeModel batBefScan = (IBatScanBeforeModel)mainBattery;
        IBatTestLeakModel batTestLeak = (IBatTestLeakModel)mainBattery;
        IBatWeightAfterModel batAftWeight = (IBatWeightAfterModel)mainBattery;

        if (deviceResult.IsSuccess)
        {
            batAftWeight.AfterWeight = batAftWeight.FirstInjectWeight = deviceResult.Value;
        }
        batAftWeight.AfterWeightTime = DateTime.Now;
        batAftWeight.AfterWeightIndex = plcData.Index;
        batAftWeight.ActualInjectionVolume = batAftWeight.FirstInjectWeight.GetInjectVolume(batBefWeight.BeforeWegiht, _parameterConfig);
        batAftWeight.TargetInjectionVolumeDeviation = Math.Round(batAftWeight.ActualInjectionVolume - batBefWeight.TargetInjectionVolume, 3);
        batAftWeight.TotalInjectionVolume = mainBattery.GetTotalInjectVolume(_parameterConfig, logHeader);
        batAftWeight.TotalInjectionVolumeDeviation = mainBattery.GetTotalInjectionVolumeDeviation(_parameterConfig, logHeader);

        #endregion

        #region 计算出注液结果和后称重结果
        if (!deviceResult.IsSuccess)
        {
            batAftWeight.FirstInjectResult = batAftWeight.AfterWeighingResult = ResultTypeEnum.取值失败;
        }
        else
        {
            batAftWeight.FirstInjectResult = mainBattery.GetTotalInjectionVolumeResult(_parameterConfig, logHeader);
            //最终称重检测
            batAftWeight.AfterWeighingResult = mainBattery.FinalWeightRangeCheck(_parameterConfig, logHeader);
        }
        #endregion

        #region 如果注液结果或后称重结果不为OK，立刻执行上传MES方法 
        if (batAftWeight.InjectResult != ResultTypeEnum.OK || batAftWeight.AfterWeighingResult != ResultTypeEnum.OK)
        {
         //mainBattery.MesOutputTime = DateTime.Now;//还要过补液，不算出站
         await MesOutput(mainBattery, logHeader);
        }
        #endregion

        #region 发送补液量到PLC
        //发送补液量到PLC（注液OK或NG都发，如果OK可以覆盖之前的）
        var supplementaryRes = SupplementaryElectrolyteToPlc(batAftWeight.TotalInjectionVolumeDeviation, plcData, logHeader);

        #endregion

        #region 综合判断注液结果
        //单纯计算发给PLC结果，此过程不会给电池赋值
        var sendPlcResult = ClaculatePlcResult(
           batAftWeight.FirstInjectResult,
           batAftWeight.AfterWeighingResult,
           supplementaryRes,
           mainBattery,
           logHeader
        );
        #endregion

        #region 如果结果为注液量偏少，标记回流原因
        //如果是注液量偏少，就会回流，标记回流原因，真实回流后记录回流次数
        if (sendPlcResult == ResultTypeEnum.注液量偏少)
        {
            AddOrUpdateReworkReason(mainBattery);
        }
        #endregion

        #region 更新数据库电池表，刷新界面，写入PLC结果，插入或更新数据库注液表
        //保存本工序数据
        if (!await _sugarDB.UpdateByObjectAsync(mainBattery, logHeader))
        {
            sendPlcResult = ResultTypeEnum.保存数据库失败;
            batAftWeight.FirstInjectResult = ResultTypeEnum.保存数据库失败;
        }

        plcData.DataAddress.WritePlcResult(sendPlcResult, mainBattery.MesOutputStatus, _plc, _parameterConfig, logHeader); //写入PLC结果

        AddDisplayData(mainBattery);

        if (mainBattery is IBatInjectStationModel inj) //写入注液量表，不等待
        {
            var injectionData = new InjectionDataModel //记录注液量相关
            {
                Id = mainBattery.Id,
                Barcode = mainBattery.Barcode,
                InjectionTime = inj.InjectElectrolyteTime,
                StationNo = inj.InjectPumpNo,
                NeedleNo = inj.InjectNozzleNo,
                TargetInjectionVolume = batBefWeight.TargetInjectionVolume,
                InjectionValue = batAftWeight.ActualInjectionVolume,
            };
            _ = _sugarDB.InsertOrUpdateInjectionAsync(injectionData);
        }

        #endregion
    }
}
