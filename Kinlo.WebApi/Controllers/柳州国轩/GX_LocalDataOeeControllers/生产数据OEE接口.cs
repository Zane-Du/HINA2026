using Kinlo.Common.Models.OhtenModels;
using static Kinlo.Common.DAL.DbHelper;

namespace Kinlo.WebApi.Controllers;

public partial class GX_GetDataController : ControllerBase
{
  const double TPM = 18; //每分钟节拍——产18颗电池
  #region 生产数据接口
  public record ProductDataRequest(string traceID, string startTime, string endTime, string[] shifts);

  const string _productDataName = "生产数据接口";

  /// <summary>
  /// 生产数据统计
  /// </summary>
  /// <returns></returns>
  [HttpPost("/monitor/productData")]
  [ProducesResponseType(typeof(ApiResponse<List<ProductData>>), 200)] // 告诉 Swagger 成功时返回什么
  [ProducesResponseType(typeof(ApiResponse), 400)] // 告诉 Swagger 失败时返回什么
  [Languages(_productDataName)]
  public async Task<IActionResult> ProductData([FromBody] ProductDataRequest request)
  {
    var deviceName = _parameterConfig.DeviceParameter.DeviceName;
    var process = _parameterConfig.AdvancedConfig.ProductionType.ToString();
    WebLogModel webLogInfo = _productDataName.GetWebLogInfo(Request);
    try
    {
      if (request == null)
        return BadRequest(ApiResponse.Fail("", process, deviceName, "参数错误"));

      webLogInfo.TraceId = request.traceID;
      webLogInfo.RequestedMsg = JsonSerializer.Serialize(request);

      if (!DateTime.TryParse(request.startTime, out var start) || !DateTime.TryParse(request.endTime, out var end))
        return BadRequest(ApiResponse.Fail("", process, deviceName, "开始时间、结束时间或班次参数错误"));

      ShiftType? shift = request.shifts.ShiftParse();

      //计算查询的时间范围
      var queryTime = shift.CalculateTimeRangeByShift(start, end, _parameterConfig);

      var batterys = await _sugarDB.GetBattereyListByTimeRangeAsync(queryTime.start, queryTime.end);
      var deviceStatus = await _sugarDB.GetTimeRangePlcStatusAsync(queryTime.start, queryTime.end, shift, null);
      //分班次时间段统计数据
      var timeGroup = request.shifts.GetShiftFromTimeRange(start, end, _parameterConfig);

      var datas = timeGroup
        .Select(g =>
        {
          var productInfo = CalculateProductData(batterys, g.startTime, g.endTime);
          var statusTimeInfo = CalculateStatusTime(deviceStatus, g.startTime, g.endTime);
          var downtime = statusTimeInfo.downTimeRanges.CalculateTotalDuration(); //计算停机时间
          var plannedProductionTime = g.endTime - g.startTime; //计划运行时间

          var availability = plannedProductionTime.TotalMinutes.CalculateAvailability(downtime.TotalMinutes); // 计算可用性
          var performance = TPM.CalculatePerformance(
            productInfo.OutputCount,
            plannedProductionTime.TotalMinutes,
            downtime.TotalMinutes
          ); //计算性能
          var quality = (productInfo.OutputCount - productInfo.TotalNgCount).CalculateQuality(productInfo.OutputCount);
          var oee = availability.CalculateOEE(performance, quality);
          return new ProductData
          {
            createTime = g.startTime.ToLogTime(),
            shift = g.shift.ToString(),
            input = productInfo.InputCount,
            output = productInfo.OutputCount,
            oee = oee.ToString("0.#####"),
            defectRate =
              productInfo.OutputCount == 0
                ? "0"
                : (productInfo.TotalNgCount * 1.0 / productInfo.OutputCount).ToString("0.#####"),
            scanCodeNG = productInfo.ScanNgCount,
            beforeWeightNG = productInfo.BefWeightNgCount,
            noFluidInjectionNG = productInfo.LeakTestNgCount,
            multiLiquidNG = productInfo.OverElectrolyteCount,
            passiveDownTime = statusTimeInfo.PassiveDownTime,
            activeDownTime = statusTimeInfo.ActiveDownTime,
            blockMaterialTime = statusTimeInfo.BlockMaterialTime,
            waitMaterialTime = statusTimeInfo.WaitMaterialTime,
          };
        })
        .ToList();

      webLogInfo.Status = StatusTypeEnum.成功;
      webLogInfo.Level = LogNet.Enums.Log4NetLevelEnum.成功;
      return Ok(ApiResponse<List<ProductData>>.OK(webLogInfo.TraceId, process, deviceName, datas));
    }
    catch (Exception ex)
    {
      // 返回异常
      return StatusCode(500, ApiResponse.Fail(webLogInfo.TraceId, process, deviceName, ex.Message));
    }
    finally
    {
      webLogInfo.EndTime = DateTime.Now;
      webLogInfo.LogWeb(_otherParameterConfig.Value.CurrentLanguagesDictionary);
    }
  }
  #endregion

  #region OEE
  public record OutputoeeRequest(string traceID, string startTime, string endTime, string[] shifts, string span);

  const string _outputoeeName = "获取产出和OEE接口";

  /// <summary>
  /// 获取产出和OEE接口
  /// </summary>
  /// <returns></returns>
  [HttpPost("/monitor/outputoee")]
  [ProducesResponseType(typeof(ApiExpandResponse<List<OutputOeeData>>), 200)] // 告诉 Swagger 成功时返回什么
  [ProducesResponseType(typeof(ApiExpandResponse), 400)] // 告诉 Swagger 失败时返回什么
  [Languages(_outputoeeName)]
  public async Task<IActionResult> Outputoee([FromBody] OutputoeeRequest request)
  {
    var deviceName = _parameterConfig.DeviceParameter.DeviceName;
    var process = _parameterConfig.AdvancedConfig.ProductionType.ToString();
    WebLogModel webLogInfo = _outputoeeName.GetWebLogInfo(Request);
    try
    {
      if (request == null)
        return BadRequest(ApiExpandResponse.Fail("", process, deviceName, "", "参数错误"));

      webLogInfo.TraceId = request.traceID;
      webLogInfo.RequestedMsg = JsonSerializer.Serialize(request);

      if (!DateTime.TryParse(request.startTime, out var start) || !DateTime.TryParse(request.endTime, out var end))
        return BadRequest(
          ApiExpandResponse.Fail("", process, deviceName, request.span, "开始时间、结束时间或班次参数错误")
        );

      ShiftType? shift = request.shifts.ShiftParse();
      //计算查询的时间范围
      var queryTime = shift.CalculateTimeRangeByShift(start, end, _parameterConfig);

      var batterys = await _sugarDB.GetBattereyListByTimeRangeAsync(queryTime.start, queryTime.end);
      var deviceStatus = await _sugarDB.GetTimeRangePlcStatusAsync(queryTime.start, queryTime.end, shift, null);
      //分班次时间段统计数据
      var timeGroup = request.shifts.GetShiftFromTimeRange(start, end, _parameterConfig);
      var datas = timeGroup
        .Select(g =>
        {
          var productInfo = CalculateProductData(batterys, g.startTime, g.endTime);
          var statusTimeInfo = CalculateStatusTime(deviceStatus, g.startTime, g.endTime);
          var downtime = statusTimeInfo.downTimeRanges.CalculateTotalDuration(); //计算停机时间
          var plannedProductionTime = g.endTime - g.startTime; //计划运行时间

          var availability = plannedProductionTime.TotalMinutes.CalculateAvailability(downtime.TotalMinutes); // 计算可用性
          var performance = TPM.CalculatePerformance(
            productInfo.OutputCount,
            plannedProductionTime.TotalMinutes,
            downtime.TotalMinutes
          ); //计算性能
          var quality = (productInfo.OutputCount - productInfo.TotalNgCount).CalculateQuality(productInfo.OutputCount);
          var oee = Math.Round(availability.CalculateOEE(performance, quality), 3);
          return new OutputOeeData
          {
            startTime = g.startTime.ToLogTime(),
            endTime = g.endTime.ToLogTime(),
            output = productInfo.OutputCount.ToString(),
            oee = oee.ToString(),
          };
        })
        .ToList();

      webLogInfo.Status = StatusTypeEnum.成功;
      webLogInfo.Level = LogNet.Enums.Log4NetLevelEnum.成功;
      return Ok(
        ApiExpandResponse<List<OutputOeeData>>.OK(webLogInfo.TraceId, process, deviceName, request.span, datas)
      );
    }
    catch (Exception ex)
    {
      // 返回异常
      return StatusCode(500, ApiExpandResponse.Fail(webLogInfo.TraceId, process, deviceName, "", ex.Message));
    }
    finally
    {
      webLogInfo.EndTime = DateTime.Now;
      webLogInfo.LogWeb(_otherParameterConfig.Value.CurrentLanguagesDictionary);
    }
  }
  #endregion

  #region 辅助方法

  private record DownTimeInfo(int PassiveDownTime,int ActiveDownTime,int BlockMaterialTime,int WaitMaterialTime, List<(DateTime start, DateTime end)> downTimeRanges);

  /// <summary>
  /// 计算设备状态各区间时间
  /// </summary>
  /// <param name="deviceStatus"></param>
  /// <param name="start"></param>
  /// <param name="end"></param>
  /// <returns></returns>
  private DownTimeInfo CalculateStatusTime(List<PlcStatusModel> deviceStatus, DateTime start, DateTime end)
  {
    int passiveDownTime = 0;
    int activeDownTime = 0;
    int blockMaterialTime = 0;
    int waitMaterialTime = 0;
    List<(DateTime start, DateTime end)> downTimeRanges = new(); //停机时间段列表

    var groupStatus = deviceStatus.Where(s => s.EndTime > start && s.StartTime < end);
    foreach (var status in groupStatus)
    {
      if (status.Status == DeviceStateEnum.运行)
        continue;

      var statusStartTime = status.StartTime < start ? start : status.StartTime;
      var statusEndTime = status.EndTime!.Value > end ? end : status.EndTime.Value;

      downTimeRanges.Add((statusStartTime, statusEndTime));
      switch (status.Status)
      {
        case DeviceStateEnum.报警:
          passiveDownTime += (int)(statusEndTime - statusStartTime).TotalSeconds;
          break;
        case DeviceStateEnum.待机:
          activeDownTime += (int)(statusEndTime - statusStartTime).TotalSeconds;
          break;
        case DeviceStateEnum.堵料:
          blockMaterialTime += (int)(statusEndTime - statusStartTime).TotalSeconds;
          break;
        case DeviceStateEnum.待料:
          waitMaterialTime += (int)(statusEndTime - statusStartTime).TotalSeconds;
          break;
      }
    }
    return new DownTimeInfo(passiveDownTime, activeDownTime, blockMaterialTime, waitMaterialTime, downTimeRanges);
  }

  /// <summary>
  ///
  /// </summary>
  /// <param name="InputCount">进站数量</param>
  /// <param name="OutputCount">出站数量</param>
  /// <param name="TotalNgCount">总NG数量</param>
  /// <param name="ScanNgCount">前扫码NG</param>
  /// <param name="BefWeightNgCount">前称NG</param>
  /// <param name="LeakTestNgCount">测漏NG</param>
  /// <param name="LowerElectrolyteCount">注液过少</param>
  /// <param name="OverElectrolyteCount">注液过多</param>
  private record ProductInfo(
    int InputCount,
    int OutputCount,
    int TotalNgCount,
    int ScanNgCount,
    int BefWeightNgCount,
    int LeakTestNgCount,
    int LowerElectrolyteCount,
    int OverElectrolyteCount
  );

  /// <summary>
  /// 计算生产数据统计信息
  /// </summary>
  /// <param name="batterys"></param>
  /// <param name="start"></param>
  /// <param name="end"></param>
  /// <returns></returns>
  private ProductInfo CalculateProductData(List<OeeStatDto> batterys, DateTime start, DateTime end)
  {
    int inputCount = batterys.Count(x => x.CreateTime >= start && x.CreateTime < end);
    var outputData = batterys.Where(x => x.MesOutputTime >= start && x.MesOutputTime < end);
    int outputCount = 0;
    int totalNgCount = 0;
    int scanNgCount = 0;
    int befWeightNgCount = 0;
    int leakTestNgCount = 0;
    int overElectrolyteCount = 0;
    int lowerElectrolyteCount = 0;
    foreach (var bat in outputData)
    {
      outputCount++;

      if (bat.FinalStatus != ResultTypeEnum.OK)
        totalNgCount++;
      if (bat.NgProcesses == ProcessTypeEnum.前扫码)
        scanNgCount++;
      if (bat.NgProcesses == ProcessTypeEnum.前称重)
        befWeightNgCount++;
      if (bat.NgProcesses == ProcessTypeEnum.测漏)
        leakTestNgCount++;
      if (bat.NgProcesses == ProcessTypeEnum.注液 && bat.FinalStatus == ResultTypeEnum.注液量偏多)
        overElectrolyteCount++;
      if (bat.NgProcesses == ProcessTypeEnum.注液 && bat.FinalStatus == ResultTypeEnum.注液量偏少)
        lowerElectrolyteCount++;
    }
    return new ProductInfo(
      inputCount,
      outputCount,
      totalNgCount,
      scanNgCount,
      befWeightNgCount,
      leakTestNgCount,
      lowerElectrolyteCount,
      overElectrolyteCount
    );
  }
  #endregion
}
