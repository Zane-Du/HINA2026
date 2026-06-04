using System.Data;
using System.Dynamic;
using HandyControl.Tools.Extension;

namespace Kinlo.Common.DAL;

public partial class DbHelper
{
   #region  查询其它数据库 注液前后数据
   /// <summary>
   /// 取前工序数据
   /// </summary>
   /// <param name="status"></param>
   /// <param name="frontWeight"></param>
   /// <param name="rearWeight"></param>
   /// <param name="preVoltage"></param>
   /// <param name="nailHeight"></param>
   public record PreProcessData(
      PrePrcessDataEnum status,
      double frontWeight,
      double rearWeight,
      double preVoltage,
      double nailHeight,
      string barcode
   );

   public enum PrePrcessDataEnum
   {
      成功,
      失败,
      前工序数据不在范围,
   }

   /// <summary>
   /// 查询其它数据库 注液前后数据
   /// </summary>
   /// <param name="barcode"></param>
   /// <param name="logHeader"></param>
   /// <param name="count"></param>
   /// <returns></returns>
   public async Task<PreProcessData> GetOtherDatabaseDataAsync(string barcode, string logHeader, int count = 2)
   {
      var result = await _dbFactory.UsingDbAsync(
         DatabaseRole.RemoteDb1,
         async db => await OnGetOtherDatabaseDataAsync(db, barcode, logHeader, count)
      );
      if (result.status == PrePrcessDataEnum.失败)
      {
         result = await _dbFactory.UsingDbAsync(
            DatabaseRole.RemoteDb2,
            async db => await OnGetOtherDatabaseDataAsync(db, barcode, logHeader, count)
         );
      }
      return result!;
   }

   private async Task<PreProcessData> OnGetOtherDatabaseDataAsync(
      ISqlSugarClient db,
      string barcode,
      string logHeader,
      int count = 2
   )
   {
      if (db == null)
         return new PreProcessData(PrePrcessDataEnum.失败, 0, 0, 0, 0, barcode);

      float frontWeight = 0,
         rearWeight = 0,
         preVoltage = 0,
         nailHeight = 0;
      DateTime dateTime = DateTime.Now;
      string fields = _parameterConfig.AdvancedConfig.ProductionType switch
      {
         ProductionTypeEnum.回氦 =>
            $"{nameof(BatScanBeforeModel.NetWeight)},{nameof(BatWeightAfterModel.AfterWeight)},{nameof(BatWeightReplenishModel.ReplenishWeight)},{nameof(BatVoltageTestModel.TestVoltageValue)},{nameof(BatNailModel.NailHeight)}",
         _ =>
            $"{nameof(BatScanBeforeModel.NetWeight)},{nameof(BatWeightAfterModel.AfterWeight)},{nameof(BatWeightReplenishModel.ReplenishWeight)}",
      };
      for (int i = 0; i < count; i++)
      {
         try
         {
            var tableName = GetSplitTableNameByType(typeof(BatMainModel), dateTime.AddMonths(-i)); //根据时间获取表名
            var sql =
               @$"SELECT {fields} FROM {tableName} WHERE Barcode='{barcode}' ORDER BY {nameof(BatMainModel.Id)} desc LIMIT 1";
            $"[查询其它数据库_注液前后数据]开始查询,表名[{tableName}]；".LogProcess(logHeader);
            var battery = await db.SqlQueryable<ExpandoObject>(sql).FirstAsync();
            if (battery != null)
            {
               var dic = (IDictionary<string, object>)battery;
               float.TryParse(dic[nameof(BatScanBeforeModel.NetWeight)].ToString(), out frontWeight);
               float afterWeight = 0,
                  replenishWeight = 0;
               float.TryParse(dic[nameof(BatWeightAfterModel.AfterWeight)].ToString(), out afterWeight);
               float.TryParse(dic[nameof(BatWeightReplenishModel.ReplenishWeight)].ToString(), out replenishWeight);
               rearWeight = replenishWeight > 0 ? replenishWeight : afterWeight;
               if (_parameterConfig.AdvancedConfig.ProductionType == ProductionTypeEnum.回氦)
               {
                  float.TryParse(dic[nameof(BatVoltageTestModel.TestVoltageValue)].ToString(), out preVoltage);
                  float.TryParse(dic[nameof(BatNailModel.NailHeight)].ToString(), out nailHeight);
                  $"[查询其它数据库_注液前后数据]取到数据,表名[{tableName}]，干重：{frontWeight}，后称重：{afterWeight}，补液称重：{replenishWeight}，前工序电压：{preVoltage}，前工序胶钉高度：{nailHeight}；".LogProcess(
                     logHeader,
                     Log4NetLevelEnum.成功
                  );
                  if (frontWeight > 0 && preVoltage > 0)
                  {
                     $"[查询其它数据库_注液前后数据]取到数据,表名[{tableName}]；".LogProcess(
                        logHeader,
                        Log4NetLevelEnum.成功
                     );
                     return new PreProcessData(
                        PrePrcessDataEnum.成功,
                        frontWeight,
                        rearWeight,
                        preVoltage,
                        nailHeight,
                        barcode
                     );
                  }
                  else
                  {
                     return new PreProcessData(
                        PrePrcessDataEnum.前工序数据不在范围,
                        frontWeight,
                        rearWeight,
                        preVoltage,
                        nailHeight,
                        barcode
                     );
                  }
               }
               else
               {
                  $"[查询其它数据库_注液前后数据]取到数据,表名[{tableName}]，干重：{frontWeight}，后称重：{afterWeight}，补液称重：{replenishWeight}；".LogProcess(
                     logHeader,
                     Log4NetLevelEnum.成功
                  );
                  if (frontWeight > 0)
                  {
                     $"[查询其它数据库_注液前后数据]取到数据,表名[{tableName}]；".LogProcess(
                        logHeader,
                        Log4NetLevelEnum.成功
                     );
                     return new PreProcessData(
                        PrePrcessDataEnum.成功,
                        frontWeight,
                        rearWeight,
                        preVoltage,
                        nailHeight,
                        barcode
                     );
                  }
                  else
                  {
                     return new PreProcessData(
                        PrePrcessDataEnum.前工序数据不在范围,
                        frontWeight,
                        rearWeight,
                        preVoltage,
                        nailHeight,
                        barcode
                     );
                  }
               }
            }
         }
         catch (Exception ex)
         {
            $"[查询其它数据库]异常：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误);
            // return (false, frontWeight, rearWeight, preVoltage, nailHeight);
         }
      }

      $"[查询其它数据库]未取到数据！".LogProcess(logHeader, Log4NetLevelEnum.错误);
      return new PreProcessData(PrePrcessDataEnum.失败, frontWeight, rearWeight, preVoltage, nailHeight, barcode);
   }
   #endregion

   #region 电芯插入数据
   /// <summary>
   /// 电芯插入数据
   /// </summary>
   /// <param name="data"></param>
   /// <returns></returns>
   public async Task<bool> InsertableByObjectAsync<T>(T data, string logHeader)
      where T : IBatMainModel
   {
      return await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db =>
         {
            try
            {
               for (int i = 0; i < 3; i++)
               {
                  if ((await db.InsertableByObject(data).SplitTable().ExecuteCommandAsync()) > 0)
                  {
                     $"[插入数据]第{i + 1}次成功;".LogProcess(logHeader, Log4NetLevelEnum.成功);
                     return true;
                  }
                  else
                  {
                     $"[插入数据]第{i + 1}次失败;".LogProcess(logHeader, Log4NetLevelEnum.错误);
                  }
               }
            }
            catch (Exception ex)
            {
               $"[插入数据]异常： {ex};".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
            }
            return false;
         }
      );
   }

   /// <summary>
   /// 批量插入数据
   /// </summary>
   /// <param name="datas"></param>
   /// <returns></returns>
   public async Task<bool> InsertableByObjectsAsync<T>(string logHeader, params T[] datas)
      where T : IBatMainModel =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnInsertableByObjectsAsync(db, logHeader, datas)
      );

   private async Task<bool> OnInsertableByObjectsAsync<T>(ISqlSugarClient db, string logHeader, params T[] datas)
      where T : IBatMainModel
   {
      try
      {
         var stopwatch = new Stopwatch();
         stopwatch.Start();
         Dictionary<string, List<T>> upDic = new();
         foreach (var ds in datas)
         {
            string tableName = GetSplitTableNameByType(typeof(BatMainModel), ds.CreateTime);
            if (upDic.TryGetValue(tableName, out var ls))
            {
               ls.Add(ds);
            }
            else
            {
               upDic.Add(tableName, new List<T> { ds });
            }
         }
         StringBuilder stringBuilder = new StringBuilder();
         bool[] isSuccess = Enumerable.Repeat(false, upDic.Count).ToArray();
         for (int k = 0; k < upDic.Count; k++)
         {
            var item = upDic.ElementAt(k);
            for (int i = 0; i < 3; i++)
            {
               var retInt = await db.InsertableByObject(item.Value).SplitTable().ExecuteCommandAsync();
               if (retInt >= item.Value.Count)
               {
                  stringBuilder.AppendLine(
                     $"[批量插入数据]第{i + 1}次成功,表名：[{item.Key}],ID：{string.Join(',', item.Value.Select(x => x.Id))},条码：{string.Join(',', item.Value.Select(x => x.Barcode))}"
                  );
                  isSuccess[k] = true;
                  break;
               }
               else
               {
                  isSuccess[k] = false;
                  stringBuilder.AppendLine(
                     $"[批量插入数据]第{i + 1}次失败,表名：[{item.Key}],ID：{string.Join(',', item.Value.Select(x => x.Id))},条码：{string.Join(',', item.Value.Select(x => x.Barcode))}"
                  );
               }
               Thread.Sleep(2);
            }
         }
         stopwatch.Stop();
         bool isSuccessAll = isSuccess.All(x => x);
         $"[批量插入数据]用时{stopwatch.ElapsedMilliseconds}ms,{stringBuilder}".LogProcess(
            logHeader,
            isSuccessAll ? Log4NetLevelEnum.成功 : Log4NetLevelEnum.错误
         );
         return isSuccessAll;
      }
      catch (Exception ex)
      {
         $"[批量插入数据]异常,ID：{string.Join(',', datas.Select(x => x.Id))},条码：{string.Join(',', datas.Select(x => x.Barcode))}，详情： {ex}".LogProcess(
            logHeader,
            Log4NetLevelEnum.错误,
            true
         );
      }
      return false;
   }

   /// <summary>
   /// 泛型插入数据
   /// </summary>
   /// <param name="data"></param>
   /// <returns></returns>
   public async Task<bool> InsertableAsync<T>(T data, string logHeader)
      where T : class, new() =>
      await _dbFactory.UsingDbAsync(DatabaseRole.LocalDb1, async db => await OnInsertableAsync(db, data, logHeader));

   private async Task<bool> OnInsertableAsync<T>(ISqlSugarClient db, T data, string logHeader)
      where T : class, new()
   {
      try
      {
         for (int i = 0; i < 3; i++)
         {
            if ((await db.Insertable(data).SplitTable().ExecuteCommandAsync()) > 0)
            {
               $"[插入数据] 类名:[{data.GetType().Name}] 第{i + 1}次成功;".LogProcess(logHeader, Log4NetLevelEnum.成功);
               return true;
            }
            else
            {
               $"[插入数据] 类名:[{data.GetType().Name}] 第{i + 1}次失败;".LogProcess(logHeader, Log4NetLevelEnum.错误);
            }
         }
      }
      catch (Exception ex)
      {
         $"[插入数据]异常：{ex};".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
      return false;
   }
   #endregion

   #region 插入或更新
   /// <summary>
   /// 工序插入数据需调用此方法（插入或更新）
   /// </summary>
   /// <param name="data"></param>
   /// <returns></returns>
   public async Task<bool> InsertOrUpdateByBatteryBase<T>(T data, string logHeader)
      where T : IBatMainModel =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await Task.Run(() => OnInsertOrUpdateByBatteryBase(db, data, logHeader))
      );

   private bool OnInsertOrUpdateByBatteryBase<T>(ISqlSugarClient db, T data, string logHeader)
      where T : IBatMainModel
   {
      try
      {
         for (int i = 0; i < 3; i++)
         {
            if (db.StorageableByObject(data).SplitTable().ExecuteCommand() > 0) //插入或更新
            {
               $"[动态插入或更新数据]第{i + 1}成功,类：{typeof(T).Name};".LogProcess(logHeader, Log4NetLevelEnum.成功);
               return true;
            }
            else
            {
               $"[动态插入或更新数据]第{i + 1}次失败,类：{typeof(T).Name};".LogProcess(
                  logHeader,
                  Log4NetLevelEnum.错误
               );
            }
         }
      }
      catch (Exception ex)
      {
         $"[动态插入或更新数据]类：{typeof(T).Name},异常,：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
      return false;
   }
   #endregion

   #region 更新数据
   /// <summary>
   /// 更新数据
   /// </summary>
   /// <param name="data"></param>
   /// <returns></returns>
   public async Task<bool> UpdateBatteryAsync<T>(T data, string logHeader)
      where T : class, IBatMainModel, new() =>
      await _dbFactory.UsingDbAsync(DatabaseRole.LocalDb1, async db => await OnUpdateBatteryAsync(db, data, logHeader));

   private async Task<bool> OnUpdateBatteryAsync<T>(ISqlSugarClient db, T data, string logHeader)
      where T : class, IBatMainModel, new()
   {
      try
      {
         var _tableName = db.SplitHelper<T>().GetTableName(data.CreateTime); //根据时间获取表名,精准更新表
         for (int i = 0; i < 3; i++)
         {
            if ((await db.Updateable(data).AS(_tableName).ExecuteCommandAsync()) > 0)
            {
               $"[更新数据]第{i + 1}次成功,表名：[{_tableName}]".LogProcess(logHeader, Log4NetLevelEnum.成功);
               return true;
            }
            else
            {
               $"[更新数据]第{i + 1}次失败,表名：[{_tableName}]".LogProcess(logHeader, Log4NetLevelEnum.错误);
            }
            Thread.Sleep(2);
         }
      }
      catch (Exception ex)
      {
         $"[更新数据] 工序类：[{typeof(T).Name}]异常：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
      return false;
   }

   /// <summary>
   /// 更新数据
   /// </summary>
   /// <param name="data"></param>
   /// <returns></returns>
   public async Task<bool> UpdateByObjectAsync<T>(T data, string logHeader)
      where T : IBatMainModel =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnUpdateByObjectAsync(db, data, logHeader)
      );

   private async Task<bool> OnUpdateByObjectAsync<T>(ISqlSugarClient db, T data, string logHeader)
      where T : IBatMainModel
   {
      try
      {
         var _tableName = GetSplitTableNameByType(typeof(BatMainModel), data.CreateTime); //根据时间获取表名,精准更新表
         for (int i = 0; i < 3; i++)
         {
            if ((await db.UpdateableByObject(data).AS(_tableName).ExecuteCommandAsync()) > 0)
            {
               $"[更新数据]第{i + 1}次成功,表名：[{_tableName}]".LogProcess(logHeader, Log4NetLevelEnum.成功);
               return true;
            }
            else
            {
               $"[更新数据]第{i + 1}次失败,表名：[{_tableName}]".LogProcess(logHeader, Log4NetLevelEnum.错误);
            }
            Thread.Sleep(2);
         }
      }
      catch (Exception ex)
      {
         $"[更新数据]工序类：[{typeof(T).Name}]异常：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
      return false;
   }

   /// <summary>
   /// 批量更新数据
   /// </summary>
   /// <param name="datas"></param>
   /// <returns></returns>
   public async Task<bool> UpdateByObjectsAsync<T>(string logHeader, params T[] datas)
      where T : IBatMainModel =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnUpdateByObjectsAsync(db, logHeader, datas)
      );

   private async Task<bool> OnUpdateByObjectsAsync<T>(ISqlSugarClient db, string logHeader, params T[] datas)
      where T : IBatMainModel
   {
      try
      {
         Stopwatch stopwatch = new Stopwatch();
         stopwatch.Start();
         Dictionary<string, List<T>> upDic = new();
         foreach (var ds in datas)
         {
            string tableName = GetSplitTableNameByType(typeof(BatMainModel), ds.CreateTime); //根据时间获取表名,精准更新表
            if (upDic.TryGetValue(tableName, out var ls))
            {
               ls.Add(ds);
            }
            else
            {
               upDic.Add(tableName, new List<T> { ds });
            }
         }
         bool[] isSuccess = Enumerable.Repeat(false, upDic.Count).ToArray();
         StringBuilder stringBuilder = new StringBuilder();
         for (int k = 0; k < upDic.Count; k++)
         {
            var item = upDic.ElementAt(k);
            for (int i = 0; i < 3; i++)
            {
               var retInt = await db.UpdateableByObject(item.Value).AS(item.Key).ExecuteCommandAsync();
               if (retInt >= item.Value.Count)
               {
                  stringBuilder.AppendLine(
                     $"[批量更新数据]第{i + 1}次成功,表名：[{item.Key}],ID：{string.Join(',', item.Value.Select(x => x.Id))},条码：{string.Join(',', item.Value.Select(x => x.Barcode))}"
                  );
                  isSuccess[k] = true;
                  break;
               }
               else
               {
                  isSuccess[k] = false;
                  stringBuilder.AppendLine(
                     $"[批量更新数据]第{i + 1}次失败,表名：[{item.Key}],ID：{string.Join(',', item.Value.Select(x => x.Id))},条码：{string.Join(',', item.Value.Select(x => x.Barcode))}"
                  );
               }
               Thread.Sleep(2);
            }
         }
         stopwatch.Stop();
         bool isSuccessAll = isSuccess.All(x => x);
         $"[批量更新数据]用时{stopwatch.ElapsedMilliseconds}ms,{stringBuilder}".LogProcess(
            logHeader,
            isSuccessAll ? Log4NetLevelEnum.成功 : Log4NetLevelEnum.错误
         );
         return isSuccessAll;
      }
      catch (Exception ex)
      {
         $"[批量更新数据]异常,ID：{string.Join(',', datas.Select(x => x.Id))},条码：{string.Join(',', datas.Select(x => x.Barcode))}，详情：{ex}".LogProcess(
            logHeader,
            Log4NetLevelEnum.错误,
            true
         );
      }
      return false;
   }

   /// <summary>
   /// 指定列更新，注意（需更新的字典索引0务必为表id,主表为Id）
   /// </summary>
   /// <typeparam name="T"></typeparam>
   /// <param name="upDictionary">需要更新的键值对（索引0务必为表id,主表为Id）</param>
   /// <param name="id">电池ID，注意务必为电池ID，非表ID</param>
   /// <param name="barcode">电池条码</param>
   /// <returns></returns>
   public async Task<bool> UpdateColumnsAsync(
      Dictionary<string, object> upDictionary,
      long id,
      string barcode,
      string logHeader
   ) =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnUpdateColumnsAsync(db, upDictionary, id, barcode, logHeader)
      );

   private async Task<bool> OnUpdateColumnsAsync(
      ISqlSugarClient db,
      Dictionary<string, object> upDictionary,
      long id,
      string barcode,
      string logHeader
   )
   {
      try
      {
         var tableName = GetSplitTableNameByType(typeof(BatMainModel), SnowflakeHelper.GetDateTimeFromId(id)); //根据时间获取表名,精准更新表
         for (int i = 0; i < 3; i++)
         {
            var _ret = await db.Updateable(upDictionary)
               .AS(tableName)
               .WhereColumns(upDictionary.ElementAt(0).Key)
               .ExecuteCommandAsync();
            if (_ret > 0)
            {
               $"[指定列更新]{i + 1}成功".LogProcess(logHeader, Log4NetLevelEnum.成功);
               return true;
            }
            else
            {
               $"[指定列更新]第{i + 1}次失败".LogProcess(logHeader, Log4NetLevelEnum.错误);
            }
            Thread.Sleep(2);
         }
      }
      catch (Exception ex)
      {
         $"[指定列更新]异常：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
      return false;
   }
   #endregion

   #region 电芯查询相关

   /// <summary>
   ///  按进站时间范围取数据
   /// </summary>
   /// <typeparam name="T"></typeparam>
   /// <param name="batteryId"></param>
   /// <returns></returns>
   public async Task<List<T>> GetDatasByInputTimeRangeAsync<T>(DateTime startTime, DateTime endTime, string exp)
      where T : class, IRownNumber, new() =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnGetTimeRangeDataAsync<T>(db, startTime, endTime, exp)
      );

   private async Task<List<T>> OnGetTimeRangeDataAsync<T>(
      ISqlSugarClient db,
      DateTime startTime,
      DateTime endTime,
      string exp
   )
      where T : class, IRownNumber, new()
   {
      try
      {
         long startId = _snowflakeHelper.GetMinIdFromDateTime(startTime);
         long endId = _snowflakeHelper.GetMaxIdFromDateTime(endTime);
         //var monthCount = endTime.Month - startTime.Month + 1;
         var monthCount = startTime.GetMonthCount(endTime);

         List<ISugarQueryable<T>> methods = new();
         for (int i = 0; i < monthCount; i++)
         {
            var tableName = db.SplitHelper<T>().GetTableName(startTime.AddMonths(i)); //根据时间获取表名
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
               $"[获取时间范围内数据]无此表：[{tableName}]".LogRun(Log4NetLevelEnum.警告);
               continue;
            }
            var processData = db.Queryable<T>().AS(tableName).Where(x => x.Id >= startId && x.Id <= endId);

            if (!string.IsNullOrEmpty(exp))
               processData.Where(exp);

            methods.Add(processData);
         }
         if (methods.Count > 0)
         {
            var results = await db.UnionAll(methods)
               .Select((x) => new T { RowNumber = SqlFunc.RowNumber(x.Id) }, true)
               .ToListAsync();
            return results;
         }
      }
      catch (Exception ex)
      {
         $"[获取时间范围内数据]异常,开始时间：{startTime:yyyy-MM-dd HH:mm:ss},结束时间：{endTime:yyyy-MM-dd HH:mm:ss}，详情：{ex}".LogRun(
            Log4NetLevelEnum.错误
         );
      }
      return new List<T>();
   }

   /// <summary>
   /// 按时间范围取电芯数据
   /// </summary>
   /// <typeparam name="T"></typeparam>
   /// <param name="startTime"></param>
   /// <param name="endTime"></param>
   /// <param name="byInputTime"></param>
   /// <param name="isFuzzyQuery"></param>
   /// <returns></returns>
   public async Task<List<T>> GetBatterysByOutputTimeRangeAsync<T>(
      DateTime startTime,
      DateTime endTime,
      bool byInputTime,
      bool isFuzzyQuery
   )
      where T : class, new() =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await GetBatterysByOutputTimeRangeAsync<T>(db, startTime, endTime, byInputTime, isFuzzyQuery)
      );

   private async Task<List<T>> GetBatterysByOutputTimeRangeAsync<T>(
      ISqlSugarClient db,
      DateTime startTime,
      DateTime endTime,
      bool byInputTime,
      bool isFuzzyQuery
   )
      where T : class, new()
   {
      try
      {
         string sql = await GetQueryableByBarcdoe(
            startTime,
            endTime,
            string.Empty,
            QueryBatteryTypeEnum.全部,
            QueryBatteryMESStatusEnum.全部,
            true,
            byInputTime,
            isFuzzyQuery: isFuzzyQuery
         );
         if (sql == null || string.IsNullOrEmpty(sql))
         {
            return new List<T>();
         }
         var sugarQueryable = db.SqlQueryable<T>(sql);
         return await Task.Run(() => sugarQueryable.ToList());
      }
      catch (Exception ex)
      {
         $"[按出站时间范围取电芯数据]异常,开始时间：{startTime:yyyy-MM-dd HH:mm:ss},结束时间：{endTime:yyyy-MM-dd HH:mm:ss}，详情：{ex}".LogRun(
            Log4NetLevelEnum.错误
         );
      }
      return new List<T>();
   }

   public record OeeStatDto(
      DateTime CreateTime,
      DateTime MesOutputTime,
      ResultTypeEnum FinalStatus,
      ProcessTypeEnum NgProcesses
   );

   /// <summary>
   /// 按时间范围查询部分字段（进出站）并按创建时间排序
   /// </summary>
   /// <param name="startTime">为进站时间</param>
   /// <param name="endTime">为出站时间</param>
   /// <returns></returns>
   public async Task<List<OeeStatDto>> GetBattereyListByTimeRangeAsync(DateTime startTime, DateTime endTime) =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnGetBattereyListByTimeRangeAsync(db, startTime, endTime)
      );

   private async Task<List<OeeStatDto>> OnGetBattereyListByTimeRangeAsync(
      ISqlSugarClient db,
      DateTime startTime,
      DateTime endTime
   )
   {
      var monthCount = startTime.GetMonthCount(endTime);
      List<OeeStatDto> batterys = new();
      Type type = _displayDatas.CompleteBatteryDatas.RuntimeBatteryType;
      try
      {
         for (int i = 0; i < monthCount; i++)
         {
            var tableName = GetSplitTableNameByType(type, startTime.AddMonths(i)); //根据时间获取表名
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
               $"[按时间范围查询电池（进出站）]无此表[{tableName}]!".LogRun(Log4NetLevelEnum.信息);
               continue;
            }

            var datas = await db.QueryableByObject(type)
               .AS(tableName)
               .Where(
                  $"{nameof(BatMainModel.CreateTime)}>=@createTime AND {nameof(IBatMainModel.MesOutputTime)}<@outputTime",
                  new { createTime = startTime, outputTime = endTime }
               )
               .Select(
                  $"{nameof(OeeStatDto.CreateTime)},{nameof(OeeStatDto.MesOutputTime)},{nameof(OeeStatDto.FinalStatus)},{nameof(OeeStatDto.NgProcesses)}"
               )
               .OrderBy(
                  new List<OrderByModel>
                  {
                     new OrderByModel { FieldName = nameof(BatMainModel.Id), OrderByType = OrderByType.Asc },
                  }
               )
               .ToDataTableAsync();

            if (datas is DataTable dt)
            {
               foreach (DataRow row in dt.Rows)
               {
                  batterys.Add(
                     new OeeStatDto(
                        // 索引取值，性能好
                        Convert.ToDateTime(row[0]),
                        Convert.ToDateTime(row[1]),
                        (ResultTypeEnum)Convert.ToInt32(row[2]),
                        (ProcessTypeEnum)Convert.ToInt32(row[3])
                     )
                  );
               }
            }
         }
      }
      catch (Exception ex)
      {
         $"[按时间范围查询电池（进出站）]异常,详情：{ex}".LogRun(Log4NetLevelEnum.错误);
      }

      return batterys;
   }

   /// <summary>
   /// 查最近生产电芯
   /// </summary>
   /// <param name="barcode"></param>
   /// <param name="months">要查几个月</param>
   /// <returns></returns>
   public async Task<List<IBatMainModel>> GetBattereyListAsync(int count = 3000) =>
      await _dbFactory.UsingDbAsync(DatabaseRole.LocalDb1, async db => await OnGetBattereyListAsync(db, count));

   private async Task<List<IBatMainModel>> OnGetBattereyListAsync(ISqlSugarClient db, int count = 3000)
   {
      List<IBatMainModel> batterys = new();
      int months = 2;
      int queryCount = count;
      DateTime dateTime = DateTime.Now;
      Type type = _displayDatas.CompleteBatteryDatas.RuntimeBatteryType;
      try
      {
         for (int i = 0; i < months; i++)
         {
            var tableName = GetSplitTableNameByType(type, dateTime.AddMonths(-i)); //根据时间获取表名
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
               $"[查询最近生产电芯]无此表[{tableName}]!".LogRun(Log4NetLevelEnum.信息);
               continue;
            }

            var datas = await db.QueryableByObject(type)
               .AS(tableName)
               .OrderBy(
                  new List<OrderByModel>
                  {
                     new OrderByModel { FieldName = nameof(BatMainModel.Id), OrderByType = OrderByType.Desc },
                  }
               )
               .ToPageListAsync(1, queryCount);

            var row = datas as IEnumerable;
            if (row != null)
            {
               foreach (var item in row)
               {
                  batterys.Add((IBatMainModel)item);
               }
            }
            if (batterys.Count >= count)
               return batterys;
            queryCount -= batterys.Count;
         }
      }
      catch (Exception ex)
      {
         $"[根据条码查最近生产电芯]异常,详情：{ex}".LogRun(Log4NetLevelEnum.错误);
      }

      return batterys;
   }

   /// <summary>
   /// ID查询数据(泛型)
   /// </summary>
   /// <typeparam name="T"></typeparam>
   /// <param name="Id"></param>
   /// <returns></returns>
   public async Task<T?> QueryableByIdAsync<T>(long Id, string logHeader)
      where T : class, new() =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnQueryableByIdAsync<T>(db, Id, logHeader)
      );

   private async Task<T?> OnQueryableByIdAsync<T>(ISqlSugarClient db, long Id, string logHeader)
      where T : class, new()
   {
      try
      {
         if (Id == 0)
            return null;
         var tableName = db.SplitHelper<T>().GetTableName(SnowflakeHelper.GetDateTimeFromId(Id)); //根据时间获取表名
         if (!db.DbMaintenance.IsAnyTable(tableName, false))
         {
            $"[根据ID查询数据(泛型)]无此表[{tableName}],ID：{Id}".LogProcess(logHeader, Log4NetLevelEnum.错误);
            return null;
         }
         var result = await db.Queryable<T>().AS(tableName).InSingleAsync(Id);
         return result;
      }
      catch (Exception ex)
      {
         $"[根据ID查询数据(泛型)]异常,ID：{Id},详情：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误);
      }
      return null;
   }

   /// <summary>
   /// ID查询电芯
   /// </summary>
   /// <param name="type"></param>
   /// <param name="Id"></param>
   /// <returns></returns>
   public async Task<IBatMainModel?> GetBatteryByIdAsync(long Id, string logHeader) =>
      await _dbFactory.UsingDbAsync(DatabaseRole.LocalDb1, async db => await OnGetBatteryByIdAsync(db, Id, logHeader));

   private async Task<IBatMainModel?> OnGetBatteryByIdAsync(ISqlSugarClient db, long Id, string logHeader)
   {
      try
      {
         Type type = _displayDatas.CompleteBatteryDatas.RuntimeBatteryType;
         if (Id == 0)
            return null;
         string tableName = GetSplitTableNameByType(type, SnowflakeHelper.GetDateTimeFromId(Id)); //根据时间获取表名
         if (!db.DbMaintenance.IsAnyTable(tableName, false))
         {
            $"[根据ID查询数据]无此表[{tableName}],ID：{Id}".LogProcess(logHeader, Log4NetLevelEnum.错误);
            return null;
         }
         var battery = await db.QueryableByObject(type).AS(tableName).InSingleAsync(Id);
         if (battery != null)
            return (IBatMainModel)battery;
      }
      catch (Exception ex)
      {
         $"[根据ID查询数据]异常,ID：{Id},详情：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误);
      }
      return null;
   }

   /// <summary>
   /// 根据ID批量查询
   /// </summary>
   /// <param name="ids"></param>
   /// <param name="logHeader"></param>
   /// <returns></returns>
   public async Task<List<IBatMainModel>> GetBatteryListByIdsAsync(string logHeader, params long[] ids) =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnGetBatteryListByIdsAsync(db, logHeader, ids)
      );

   private async Task<List<IBatMainModel>> OnGetBatteryListByIdsAsync(
      ISqlSugarClient db,
      string logHeader,
      params long[] ids
   )
   {
      List<IBatMainModel> batterys = new List<IBatMainModel>();
      try
      {
         Type type = _displayDatas.CompleteBatteryDatas.RuntimeBatteryType;
         Dictionary<string, List<long>> tableNames = new();
         foreach (var id in ids)
         {
            string tableName = GetSplitTableNameByType(type, SnowflakeHelper.GetDateTimeFromId(id)); //根据时间获取表名
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
               $"[根据多ID查询数据]无此表[{tableName}],ID：{id},忽略ID;".LogProcess(logHeader, Log4NetLevelEnum.错误);
            }
            else
            {
               if (tableNames.TryGetValue(tableName, out var nameList))
               {
                  nameList.Add(id);
               }
               else
               {
                  tableNames[tableName] = [id];
               }
            }
         }

         foreach (var item in tableNames)
         {
            var obj = await db.QueryableByObject(type)
               .AS(item.Key)
               .Where("Id IN (@ids)", new { ids = item.Value })
               .ToListAsync();
            var list = ((IEnumerable)obj).Cast<IBatMainModel>().ToList();
            if (list.Count > 0)
            {
               batterys.AddRange(list);
            }
         }
      }
      catch (Exception ex)
      {
         $"[根据多ID查询数据]异常,详情：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }

      return batterys;
   }

   /// <summary>
   /// 根据条码查最近生产电芯
   /// </summary>
   /// <param name="barcode"></param>
   /// <param name="months">要查几个月</param>
   /// <returns></returns>
   public async Task<IBatMainModel?> GetLastBattereyByBarcodeAsync(string barcode, string logHeader, int months = 2) =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnGetLastBattereyByBarcodeAsync(db, barcode, logHeader, months)
      );

   private async Task<IBatMainModel?> OnGetLastBattereyByBarcodeAsync(
      ISqlSugarClient db,
      string barcode,
      string logHeader,
      int months = 2
   )
   {
      DateTime dateTime = DateTime.Now;
      Type type = _displayDatas.CompleteBatteryDatas.RuntimeBatteryType;
      try
      {
         for (int i = 0; i < months; i++)
         {
            var tableName = GetSplitTableNameByType(type, dateTime.AddMonths(-i)); //根据时间获取表名
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
               $"[根据条码查最近生产电芯]无此表[{tableName}],条码：{barcode}".LogProcess(
                  logHeader,
                  Log4NetLevelEnum.信息
               );
               continue;
            }

            var battery = await db.QueryableByObject(type)
               .AS(tableName)
               .Where($"{nameof(BatMainModel.Barcode)}=@barcode", new { barcode = barcode })
               .OrderBy(
                  new List<OrderByModel>
                  {
                     new OrderByModel { FieldName = nameof(BatMainModel.Id), OrderByType = OrderByType.Desc },
                  }
               )
               .FirstAsync();
            if (battery != null)
               return battery as IBatMainModel;
         }
      }
      catch (Exception ex)
      {
         $"[根据条码查最近生产电芯]异常,条码：{barcode},详情：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误);
      }
      return null;
   }

   /// <summary>
   /// 根据条码模糊查询数据
   /// </summary>
   /// <param name="barcode"></param>
   /// <param name="days">查最近几天</param>
   /// <returns></returns>
   public async Task<ObservableCollection<IBatMainModel>> GetProcessByBarcodeFuzzyAsync(
      string barcode,
      string logHeader,
      int days = 10
   ) =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db => await OnGetProcessByBarcodeFuzzyAsync(db, barcode, logHeader, days)
      );

   private async Task<ObservableCollection<IBatMainModel>> OnGetProcessByBarcodeFuzzyAsync(
      ISqlSugarClient db,
      string barcode,
      string logHeader,
      int days = 10
   )
   {
      ObservableCollection<IBatMainModel> result = new();
      try
      {
         Type type = _displayDatas.CompleteBatteryDatas.RuntimeBatteryType;
         var endTime = DateTime.Now;
         var startTime = endTime.AddDays(-days);
         long startId = _snowflakeHelper.GetMinIdFromDateTime(startTime);
         long endId = _snowflakeHelper.GetMaxIdFromDateTime(endTime);
         //var months = endTime.Month - startTime.Month + 1;
         var monthCount = startTime.GetMonthCount(endTime);
         for (int i = 0; i < monthCount; i++)
         {
            var tableName = GetSplitTableNameByType(type, endTime.AddMonths(-i)); //根据时间获取表名 取表名
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
               $"[根据条码模糊查询数据]无此表[{tableName}],条码：{barcode}".LogProcess(
                  logHeader,
                  Log4NetLevelEnum.信息
               );
               continue;
            }
            var battery = await db.QueryableByObject(type)
               .AS(tableName)
               .Where($"{nameof(BatMainModel.Barcode)} LIKE'%{barcode}%'")
               .OrderBy(
                  new List<OrderByModel>
                  {
                     new OrderByModel { FieldName = nameof(BatMainModel.Id), OrderByType = OrderByType.Desc },
                  }
               )
               .ToListAsync();
            if (battery != null)
               result.AddRange((IEnumerable<IBatMainModel>)battery);
         }
      }
      catch (Exception ex)
      {
         $"[根据条码模糊查询数据]异常,条码：{barcode},详情：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误);
      }
      return result;
   }

   /// <summary>
   /// 通过条码在时间跨度查询电芯
   /// </summary>
   /// <typeparam name="T"></typeparam>
   /// <param name="startTime"></param>
   /// <param name="endTime"></param>
   /// <param name="barcode"></param>
   /// <param name="queryBatteryType">查询电池类型（合格或不合格）</param>
   /// <param name="queryBatteryMESStatus">查询电池MES状态</param>
   /// <param name="isNotRepeat">是否去重</param>
   ///  /// <param name="byInputTime">按进站时间计算</param>
   ///  /// <param name="isFuzzyQuery">是否模糊查询</param>
   /// <returns></returns>
   public async Task<string> GetQueryableByBarcdoe(
      DateTime startTime,
      DateTime endTime,
      string barcode,
      QueryBatteryTypeEnum queryBatteryType,
      QueryBatteryMESStatusEnum queryBatteryMESStatus,
      bool isNotRepeat,
      bool byInputTime,
      bool isFuzzyQuery
   ) =>
      await _dbFactory.UsingDbAsync(
         DatabaseRole.LocalDb1,
         async db =>
            await Task.Run(() =>
               OnGetQueryableByBarcdoe(
                  db,
                  startTime,
                  endTime,
                  barcode,
                  queryBatteryType,
                  queryBatteryMESStatus,
                  isNotRepeat,
                  byInputTime,
                  isFuzzyQuery
               )
            )
      );

   private string OnGetQueryableByBarcdoe(
      ISqlSugarClient db,
      DateTime startTime,
      DateTime endTime,
      string barcode,
      QueryBatteryTypeEnum queryBatteryType,
      QueryBatteryMESStatusEnum queryBatteryMESStatus,
      bool isNotRepeat,
      bool byInputTime,
      bool isFuzzyQuery
   )
   {
      try
      {
         string queryTableExp = GetQueryTableExp(
            barcode,
            startTime,
            endTime,
            queryBatteryType,
            queryBatteryMESStatus,
            string.Empty,
            byInputTime,
            isFuzzyQuery
         );
         string aliasQueryTableExp = GetQueryTableExp(
            barcode,
            startTime,
            endTime,
            queryBatteryType,
            queryBatteryMESStatus,
            $"{_aliasName}.",
            byInputTime,
            isFuzzyQuery
         );

         var timeSpan = endTime - startTime;
         var monthCount = startTime.GetMonthCount(endTime);
         if (!byInputTime)
            ++monthCount; //如果按出站时间查询，那数据有可能在上一个月，所以需加一个月
         var sqls = new List<string>();
         for (int i = 0; i < monthCount; i++)
         {
            var tableName = GetSplitTableNameByType(typeof(BatMainModel), endTime.AddMonths(-i)); //根据时间获取表名 取表名
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
               $"[根据条码模糊查询数据]无此表[{tableName}],条码：{barcode}".LogRun(Log4NetLevelEnum.信息);
               continue;
            }

            string partSql = string.Empty;
            if (isNotRepeat) //去重
            {
               partSql =
                  $@"
                    SELECT {_aliasFields}
                    FROM {tableName} {_aliasName}
                    INNER JOIN (
                        SELECT {nameof(BatMainModel.Barcode)}, MAX({nameof(BatMainModel.Id)}) AS {nameof(BatMainModel.Id)}
                        FROM {tableName}
                        WHERE {queryTableExp}
                        GROUP BY Barcode
                    ) sub ON {_aliasName}.{nameof(BatMainModel.Id)} = sub.{nameof(BatMainModel.Id)}
                    WHERE {aliasQueryTableExp} ";
            }
            else //不去重
            {
               partSql =
                  $@"
                    SELECT {_aliasFields}
                    FROM {tableName} {_aliasName}
                    WHERE {aliasQueryTableExp} ";
            }
            sqls.Add(partSql);
         }

         if (sqls.Count == 0)
            return string.Empty;

         return string.Join(" UNION ALL ", sqls);
      }
      catch (Exception ex)
      {
         $"[历史查询数据]异常,条码：{barcode},详情：{ex}".LogRun(Log4NetLevelEnum.错误);
      }
      return string.Empty;
   }

   /// <summary>
   /// 获取查询where语句
   /// </summary>
   /// <param name="barcode"></param>
   /// <param name="startId"></param>
   /// <param name="endId"></param>
   /// <param name="queryBatteryType"></param>
   /// <param name="queryBatteryMESStatus"></param>
   /// <param name="alias"></param>
   /// <param name="byInputTime">按进站时间计算</param>
   /// <param name="isFuzzyQuery">是否模糊查询</param>
   /// <returns></returns>
   private string GetQueryTableExp(
      string barcode,
      DateTime startTime,
      DateTime endTime,
      QueryBatteryTypeEnum queryBatteryType,
      QueryBatteryMESStatusEnum queryBatteryMESStatus,
      string alias,
      bool byInputTime,
      bool isFuzzyQuery
   )
   {
      StringBuilder queryTableExp = new StringBuilder();
      if (byInputTime)
      {
         long startId = _snowflakeHelper.GetMinIdFromDateTime(startTime);
         long endId = _snowflakeHelper.GetMaxIdFromDateTime(endTime);
         queryTableExp.Append(
            $"{alias}{nameof(BatMainModel.Id)} >= {startId} AND {alias}{nameof(BatMainModel.Id)} <= {endId} "
         );
      }
      else
      {
         queryTableExp.Append(
            $"{alias}{nameof(BatMainModel.MesOutputTime)} >= '{startTime}' AND {alias}{nameof(BatMainModel.MesOutputTime)} <= '{endTime}' "
         );
      }

      string mesExp = queryBatteryMESStatus switch
      {
         QueryBatteryMESStatusEnum.全部 => string.Empty,
         QueryBatteryMESStatusEnum.未进站 => $" AND {alias}{nameof(BatMainModel.MesInputStatus)}<11 ",
         QueryBatteryMESStatusEnum.未出站 => $" AND {alias}{nameof(BatMainModel.MesOutputStatus)}<11 ",
         QueryBatteryMESStatusEnum.进站失败 => $" AND {alias}{nameof(BatMainModel.MesInputStatus)}>20 ",
         QueryBatteryMESStatusEnum.出站失败 => $" AND {alias}{nameof(BatMainModel.MesOutputStatus)}>20 ",
         QueryBatteryMESStatusEnum.进站或出站失败 =>
            $" AND ({alias}{nameof(BatMainModel.MesInputStatus)}>20 OR {alias}{nameof(BatMainModel.MesOutputStatus)}>20) ",
         // QueryBatteryMESStatusEnum.发送测试 => $" AND ({alias}{nameof(BatMainModel.MesInputStatus)}={(int)ResultTypeEnum.发送MES测试数据} OR {alias}{nameof(BatMainModel.MesOutputStatus)}={(int)ResultTypeEnum.发送MES测试数据}) ",
      };
      queryTableExp.Append(mesExp);

      if (queryBatteryType == QueryBatteryTypeEnum.只看合格)
         queryTableExp.Append($" AND {alias}{nameof(BatMainModel.FinalStatus)}<21 ");
      else if (queryBatteryType == QueryBatteryTypeEnum.只看不合格)
         queryTableExp.Append($" AND {alias}{nameof(BatMainModel.FinalStatus)}>20 ");

      if (!string.IsNullOrWhiteSpace(barcode))
      {
         if (barcode.Contains(','))
         {
            var barcodes = barcode.Split(',').Select(b => b.Trim()).ToList();
            string barcodeQuery = string.Join(
               " OR ",
               barcodes.Select(b => $"{alias}{nameof(BatMainModel.Barcode)} = '{b}' ")
            );
            queryTableExp.Append($" AND {barcodeQuery} ");
         }
         else if (isFuzzyQuery)
         {
            queryTableExp.Append($" AND {alias}{nameof(BatMainModel.Barcode)} LIKE '%{barcode.Trim()}%' ");
         }
         else
         {
            queryTableExp.Append($" AND {alias}{nameof(BatMainModel.Barcode)} = '{barcode.Trim()}' ");
         }
      }

      return queryTableExp.ToString();
   }

   #endregion

   #region 辅助方法
   /// <summary>
   /// 取分表表名
   /// </summary>
   /// <param name="type"></param>
   /// <param name="dateTime"></param>
   /// <returns></returns>
   public static string GetSplitTableNameByType(Type type, DateTime dateTime)
   {
      var _attribe = type.GetCustomAttribute<SugarTable>();
      if (_attribe != null)
      {
         var _tableNames = _attribe.TableName.Split('_');
         if (_tableNames.Length > 0)
         {
            return $"{_tableNames[0]}_{GetSplitTableSuffix(dateTime)}".ToLower();
         }
      }
      return $"{type.Name}_{GetSplitTableSuffix(dateTime)}".ToLower();
   }

   /// <summary>
   /// 生成分表后缀
   /// </summary>
   /// <param name="type"></param>
   /// <returns></returns>
   private static string GetSplitTableSuffix(DateTime dateTime) => $"{dateTime.Year}{dateTime.Month:D2}01";

   /// <summary>
   /// 检查日期是否为当月最后一天
   /// </summary>
   /// <param name="dateTime"></param>
   /// <returns></returns>
   private static bool CheckIsMonthLastDay(DateTime dateTime)
   {
      var _newDtae = dateTime.AddMonths(1);
      int _lastDay = (new DateTime(_newDtae.Year, _newDtae.Month, 1)).AddDays(-1).Day;
      return _lastDay == dateTime.Day;
   }

   #endregion
}
