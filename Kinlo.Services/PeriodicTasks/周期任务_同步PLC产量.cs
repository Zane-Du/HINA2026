namespace Kinlo.Services.PeriodicTasks;

public partial class PeriodicTasksHelper
{
   bool isSync = false;

   public async Task PlcProductionSyncService(DateTime t, IContainer container)
   {
      try
      {
         if (t.Second % 10 != 0 || isSync)
            return;
         isSync = true;
         PLCSignalConfig plcSignalConfig = container.Get<PLCSignalConfig>();
         var plcDataTag = plcSignalConfig.CustomPlcInteractAddresses.FirstOrDefault(x =>
            x.CustomInteractName == CustomInteractNameEnum.PC至PLC生产数据
         );
         if (plcDataTag == null || !plcDataTag.IsEnable)
         {
            //"[同步PLC生产数据]未配置PLC生产数据交互地址！".LogRun(Log4NetLevelEnum.警告);
            return;
         }
         var devicesConfig = container.Get<DevicesConfig>();

         var plcs = devicesConfig.GetRunDevices(x => x.DeviceInfo.ProcessesType == ProcessTypeEnum.PLC);
         if (plcs != null && plcs.Count > 0)
         {
            foreach (var item in plcs)
            {
               PlcProductionSync(container, (IPLC)item, plcDataTag);
            }
         }
         else
         {
            var clients = devicesConfig.DeviceList.Where(x => x.ProcessesType == ProcessTypeEnum.PLC);
            foreach (var client in clients)
            {
               await client.WithCreatedDeviceAsync(async d =>
                  await Task.Run(() => PlcProductionSync(container, (IPLC)d, plcDataTag))
               );
            }
         }
      }
      catch (Exception ex)
      {
         $"[同步PLC生产数据]异常：{ex}！".LogRun(Log4NetLevelEnum.警告);
      }
      finally
      {
         isSync = false;
      }
   }

   /// <summary>
   /// 产量同步至PLC
   /// </summary>
   /// <param name="container"></param>
   /// <param name="plc"></param>
   /// <param name="customPlcInteractAddress"></param>
   public void PlcProductionSync(IContainer container, IPLC plc, CustomPlcInteractAddressModel customPlcInteractAddress)
   {
      string logHeader = $"[同步PLC生产数据]:";
      PlcProductionSyncModel plcProduction = new PlcProductionSyncModel();
      plcProduction.Shift = _appGlobalConfig.ShiftSwitchInfo.Shift == ShiftType.白班 ? (short)1 : (short)2;
      plcProduction.Input = _processRatioDisplay.ProductionCounter.InputCount;
      plcProduction.Output = _processRatioDisplay.ProductionCounter.OutputCount;

      foreach (var item in _processRatioDisplay.Last24HourOutputValue.HourlyDatas)
      {
         if (plcProduction.HourCount.Length >= item.Time.Hour)
            plcProduction.HourCount[item.Time.Hour] = item.ProductionCount;
      }
      foreach (var item in _processRatioDisplay.ProcessRatios)
      {
         int index = item.Process switch
         {
            nameof(ProcessTypeEnum.前扫码) => 0,
            nameof(ProcessTypeEnum.测短路) or nameof(ProcessTypeEnum.测电压) => 1,
            nameof(ProcessTypeEnum.前称重) => 2,
            nameof(ProcessTypeEnum.测漏) => 3,
            nameof(ProcessTypeEnum.注液) => 4,
            nameof(ProcessTypeEnum.后称重) => 5,
            nameof(ProcessTypeEnum.打钉检测) => 6,
            _ => -1,
         };
         if (index >= 0 && plcProduction.PlcProcessData.Length > index)
         {
            var data = plcProduction.PlcProcessData[index];
            data.OkCount = item.OkTotal;
            data.Ng1Count = item.NgTotal;
            data.Ng2Count = 0;
            data.PassRate = (float)Math.Round(item.OkRatio, 2);
            data.NgProportion = (float)Math.Round(item.NgRatio, 2);
         }
      }
      Up24HourData(container);

      if (!plc.WriteClass(plcProduction, new SignalAddressModel(customPlcInteractAddress.DataAddress.Lable), logHeader))
      {
         $"同步生产数据至PLC工失败！".LogRun(Log4NetLevelEnum.错误, true);
      }
   }

   /// <summary>
   /// 更新24小时产量数据，剔除不在24小时范围内的数据
   /// </summary>
   /// <param name="container"></param>
   public void Up24HourData(IContainer container)
   {
      try
      {
         DateTime now = DateTime.Now;
         DateTime currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0);
         // 24 小时下限（含）
         DateTime minHour = currentHour.AddHours(-23);
         foreach (var item in _processRatioDisplay.Last24HourOutputValue.HourlyDatas) //去掉不在24小时范围内的数据
         {
            item.Time = new DateTime(item.Time.Year, item.Time.Month, item.Time.Day, item.Time.Hour, 0, 0, 0); //强制只保留小时
            if (item.Time < minHour || item.Time > currentHour)
            {
               // 只保留小时，拉回到当前 24 小时内
               item.Time = new DateTime(currentHour.Year, currentHour.Month, currentHour.Day, item.Time.Hour, 0, 0);
               item.ProductionCount = 0;
               item.ValueSuffix = " 颗";
               item.Subtitle = $"{item.Time:dd日 HH时}~{item.Time.AddHours(1):HH}时";
            }
         }
      }
      catch (Exception ex)
      {
         $"剔除不在24小时范围内的数据异常：{ex}".LogRun(Log4NetLevelEnum.警告);
      }
   }
}
