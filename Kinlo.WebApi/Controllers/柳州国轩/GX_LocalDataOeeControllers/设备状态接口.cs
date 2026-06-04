namespace Kinlo.WebApi.Controllers;

[ApiController]
[Route("api/[controller]/[action]")] // 占位路由，运行时会被 DynamicRouteConvention 替换
[Languages("国轩获取本地数据接口", IsScanMethod = true)]
[Tags("国轩获取本地数据接口")] //给Swagger分类以及过滤控制器
public partial class GX_GetDataController : ControllerBase
{
  ParameterConfig _parameterConfig;
  DbHelper _sugarDB;
  Lazy<OtherParameterConfig> _otherParameterConfig;

  public GX_GetDataController(IContainer container)
  {
    _parameterConfig = container.Get<ParameterConfig>();
    _sugarDB = container.Get<DbHelper>();
    _otherParameterConfig = new Lazy<OtherParameterConfig>(() => container.Get<OtherParameterConfig>());
  }

  #region 获取设备状态变更记录接口
  public record DeviceStatusRequest(string traceID, string startTime, string endTime, string[] shifts);

  const string _deviceStatusName = "获取设备状态变更记录接口";

  /// <summary>
  /// 设备状态数据接口
  /// </summary>
  /// <returns></returns>
  [HttpPost("/monitor/deviceStatus")]
  [ProducesResponseType(typeof(ApiResponse<List<DeviceStatusData>>), 200)] // 告诉 Swagger 成功时返回什么
  [ProducesResponseType(typeof(ApiResponse), 400)] // 告诉 Swagger 失败时返回什么
  [Languages(_deviceStatusName)]
  public async Task<IActionResult> DeviceStatus([FromBody] DeviceStatusRequest request)
  {
    var deviceName = _parameterConfig.DeviceParameter.DeviceName;
    var process = _parameterConfig.AdvancedConfig.ProductionType.ToString();
    WebLogModel webLogInfo = _deviceStatusName.GetWebLogInfo(Request);
    try
    {
      if (request == null)
        return BadRequest(ApiResponse.Fail("", process, deviceName, "参数错误"));

      webLogInfo.TraceId = request.traceID;
      webLogInfo.RequestedMsg = JsonSerializer.Serialize(request);

      if (!DateTime.TryParse(request.startTime, out var start) || !DateTime.TryParse(request.endTime, out var end))
        return BadRequest(ApiResponse.Fail("", process, deviceName, "开始时间、结束时间或班次参数错误"));

      ShiftType? shift = request.shifts.ShiftParse();
      var queryTime = shift.CalculateTimeRangeByShift(start, end, _parameterConfig);

      var deviceStatus = await _sugarDB.GetTimeRangePlcStatusAsync(queryTime.start, queryTime.end, shift, null);

      var datas = deviceStatus
        .Select(x =>
        {
          return new DeviceStatusData
          {
            startTime = x.StartTime.ToLogTime(),
            endTime = x.EndTime.ToLogTime(),
            shift = x.Shift.HasValue ? x.Shift.Value.ToString() : "",
            status = ((short)x.Status).KeepBest().ToString(),
          };
        })
        .ToList();

      webLogInfo.Status = StatusTypeEnum.成功;
      webLogInfo.Level = LogNet.Enums.Log4NetLevelEnum.成功;
      return Ok(ApiResponse<List<DeviceStatusData>>.OK(webLogInfo.TraceId, process, deviceName, datas));
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

  #region 获取设备异常信息明细接口
  /// <summary>
  ///
  /// </summary>
  /// <param name="traceID"></param>
  /// <param name="startTime"></param>
  /// <param name="endTime"></param>
  /// <param name="stopTypes">停机类型</param>
  /// <param name="shifts"></param>
  public record DeviceExcpetionRequest(
    string traceID,
    string startTime,
    string endTime,
    string[] stopTypes,
    string[] shifts
  );

  const string _deviceExcpetionName = "获取设备异常信息明细接口";

  /// <summary>
  /// 设备异常数据
  /// </summary>
  /// <returns></returns>
  [HttpPost("/monitor/deviceException")]
  [ProducesResponseType(typeof(ApiResponse<List<DeviceExceptionData>>), 200)] // 告诉 Swagger 成功时返回什么
  [ProducesResponseType(typeof(ApiResponse), 400)] // 告诉 Swagger 失败时返回什么
  [Languages(_deviceExcpetionName)]
  public async Task<IActionResult> DeviceExcpetion([FromBody] DeviceExcpetionRequest request)
  {
    var deviceName = _parameterConfig.DeviceParameter.DeviceName;
    var process = _parameterConfig.AdvancedConfig.ProductionType.ToString();
    WebLogModel webLogInfo = _deviceExcpetionName.GetWebLogInfo(Request);
    try
    {
      if (request == null)
        return BadRequest(ApiResponse.Fail("", process, deviceName, "参数错误"));

      webLogInfo.TraceId = request.traceID;
      webLogInfo.RequestedMsg = JsonSerializer.Serialize(request);

      if (!DateTime.TryParse(request.startTime, out var start) || !DateTime.TryParse(request.endTime, out var end))
        return BadRequest(ApiResponse.Fail("", process, deviceName, "开始时间、结束时间或班次参数错误"));

      ShiftType? shift = request.shifts.ShiftParse();
      DeviceStateEnum[] deviceState = request.stopTypes.ShutdownTypeParse();
      var queryTime = shift.CalculateTimeRangeByShift(start, end, _parameterConfig);

      var deviceStatus = await _sugarDB.GetTimeRangePlcStatusAsync(queryTime.start, queryTime.end, shift, deviceState);

      var datas = deviceStatus
        .Select(x =>
        {
          return new DeviceExceptionData
          {
            startTime = x.StartTime.ToMesDateTime(),
            endTime = x.EndTime?.ToMesDateTime() ?? "",
            shift = x.Shift.HasValue ? x.Shift.Value.ToString() : "",
            stopType = x.Status == DeviceStateEnum.报警 ? "被动停机" : "主动停机",
            stopReason = x.Status switch
            {
              DeviceStateEnum.待机 => string.IsNullOrWhiteSpace(x.Msg) ? "主动停机" : x.Msg,
              _ => x.Msg,
            },
          };
        })
        .ToList();

      webLogInfo.Status = StatusTypeEnum.成功;
      webLogInfo.Level = LogNet.Enums.Log4NetLevelEnum.成功;
      return Ok(ApiResponse<List<DeviceExceptionData>>.OK(webLogInfo.TraceId, process, deviceName, datas));
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
}
