using Kinlo.Common.Cache;

namespace Kinlo.Common.DisplayDatas;

/// <summary>
/// 工序实时统计
/// </summary>
public class ProcessRatioDisplay : ConfigurationBase
{
    #region event

    /// <summary>
    /// 百分比统计变化通知
    /// </summary>
    public event Action<ProcessRatioItem[]>? DisplayRatioChanged;

    #endregion

    #region property

    /// <summary>
    /// 生产统计
    /// </summary>
    [JsonIgnore]
    public ProductCountDto ProductionCounter { get; set; } = new();

    /// <summary>
    ///// 最近24小时产值
    /// </summary>
    public Last24HourOutputValueDto Last24HourOutputValue { get; set; } = new();

    /// <summary>
    /// UI绑定工序统计
    /// </summary>
    [JsonIgnore]
    public ObservableRangeCollection<ProcessRatioItem> ProcessRatios { get; } = [];

    /// <summary>
    /// 工序统计规则
    /// </summary>
    [JsonIgnore]
    public List<ProcessRatioRule> ProcessRules { get; } = [];

    #endregion

    #region field

    /// <summary>
    /// 节流刷新器
    ///
    /// 3秒最多刷新一次
    /// </summary>
    [JsonIgnore]
    private readonly ThrottleHelper<IBatMainModel[]> _refreshThrottle;

    /// <summary>
    /// 节流保存
    ///
    /// 60秒最多保存一次
    /// </summary>
    [JsonIgnore]
    private readonly ThrottleHelper _saveThrottle;

    /// <summary>
    /// 全局配置
    /// </summary>
    [JsonIgnore]
    private readonly AppGlobalConfig _appGlobalConfig;

    /// <summary>
    /// 电池缓存
    /// </summary>
    [JsonIgnore]
    private readonly Lazy<IBatteryCache> _cacheLazy;

    /// <summary>
    /// 默认最小时间  MES未出站时：  默认时间一般远小于当前时间  用于判断是否真正出站
    /// </summary>
    [JsonIgnore]
    private readonly DateTime _defaultMinTime = new(2000, 1, 1);

    #endregion

    public ProcessRatioDisplay(StyletIoC.IContainer container, bool isStartup)
        : base(container, isStartup)
    {
        _cacheLazy = new Lazy<IBatteryCache>(() => container.Get<IBatteryCache>());

        // 3秒最多刷新一次
        _refreshThrottle = new ThrottleHelper<IBatMainModel[]>(TimeSpan.FromSeconds(3), DoRefreshStatisticsAsync);

        // 60秒最多保存一次
        _saveThrottle = new ThrottleHelper(
            TimeSpan.FromSeconds(60),
            () => this.Save("系统自动保存", "", isPopup: false, isPrintLog: false)
        );

        _appGlobalConfig = container.Get<AppGlobalConfig>();
    }

    public override void Load()
    {
        try
        {
            var json = FileHelper.LoadToString(this.GetType().Name);
            MapJsonProperties(json);
        }
        catch (Exception ex)
        {
            $"加载配置文件 {this.GetType().Name} 异常: {ex.Message}".LogRun(LogNet.Enums.Log4NetLevelEnum.错误);
        }
    }

    #region Reset

    /// <summary>
    /// 重置统计
    /// </summary>
    public async Task ResetAsync()
    {
        await UIThreadHelper.InvokeOnUiThreadAsync(() =>
        {
            ProductionCounter.InputCount = 0;
            ProductionCounter.OutputCount = 0;

            foreach (var item in ProcessRatios)
            {
                item.Reset();
            }
        });
    }

    #endregion

    #region Init

    /// <summary>
    /// 初始化统计规则
    /// </summary>
    public void Init(ObservableCollection<DisplayDataDto> processes)
    {
        ProcessRules.Clear();

        foreach (var process in processes)
        {
            var rules = BuildProcessRatioRules(process.OriginalClass, process.Description);

            ProcessRules.AddRange(rules);
        }
    }

    /// <summary>
    /// 解析 Attribute 构建统计规则
    /// </summary>
    private List<ProcessRatioRule> BuildProcessRatioRules(Type? type, string processName)
    {
        if (type == null)
        {
            return [];
        }

        var rules = new List<ProcessRatioRule>();

        foreach (var property in type.GetProperties())
        {
            var attribute = property.GetCustomAttribute<ProcessRatioAttribute>();

            if (attribute == null)
            {
                continue;
            }

            // 如果没有指定显示名
            //
            // 默认使用工序名
            var displayName = string.IsNullOrWhiteSpace(attribute.DisplayName) ? processName : attribute.DisplayName;

            // UI统计对象
            var processRatio = new ProcessRatioItem { Process = displayName };

            // 运行时规则
            var rule = new ProcessRatioRule(
                displayName,
                property.Name,
                attribute.TimeName,
                processRatio,
                attribute.PreResultName
            );

            // 细分NG
            var details = property.GetCustomAttributes<ProcessRatioDetailAttribute>();

            foreach (var detail in details)
            {
                var detailItem = new RatioDetailItem { Name = detail.Name, Result = detail.Result };

                // UI集合
                processRatio.NgDetails.Add(detailItem);

                // 快速查找字典
                processRatio.NgDetailMap[detail.Result] = detailItem;
            }

            rules.Add(rule);
        }

        return rules;
    }

    #endregion

    #region Refresh

    /// <summary>
    /// 外部刷新入口
    /// </summary>
    public async Task Refresh(IBatMainModel[] batteries)
    {
        // 节流刷新
        await _refreshThrottle.InvokeAsync(batteries);

        // 节流保存
        await _saveThrottle.InvokeAsync();
    }

    #endregion

    #region Statistics

    /// <summary>
    /// 执行统计刷新
    /// </summary>
    private async Task DoRefreshStatisticsAsync(IBatMainModel[] batteries)
    {
#if DEBUG
        // Stopwatch stopwatch = Stopwatch.StartNew();
#endif

        int inputCount = 0;
        int outputCount = 0;
        int cacheCount = 0;

        // 要删除的缓存ID
        List<long> removeCacheIds = new();

        // 最新电池映射  同条码只保留最新一个参与统计
        Dictionary<string, IBatMainModel> latestBatteryMap = new(StringComparer.OrdinalIgnoreCase);

        // 重置统计
        foreach (var rule in ProcessRules)
            rule.ProcessRatio.Reset();

        #region 保留每个条码最新数据
        foreach (var battery in batteries)
        {
            // 条码为空 无法参与统计
            if (string.IsNullOrWhiteSpace(battery.Barcode))
            {
                removeCacheIds.Add(battery.Id);
                continue;
            }

            // 已存在相同条码
            if (latestBatteryMap.TryGetValue(battery.Barcode, out var oldBattery))
            {
                // 当前数据更新  保留最新ID
                if (battery.Id > oldBattery.Id)
                {
                    removeCacheIds.Add(oldBattery.Id);
                    latestBatteryMap[battery.Barcode] = battery;
                }
                else
                {
                    // 当前是旧数据
                    removeCacheIds.Add(battery.Id);
                }
            }
            else
            {
                latestBatteryMap[battery.Barcode] = battery;
            }
        }
        #endregion

        #region 统计
        var timInfo = GetOutputTime();
        int previousMinuteQuantity = 0,
            currentMinuteQuantity = 0,
            currentHourQuantity = 0;
        foreach (var battery in latestBatteryMap.Values)
        {
            cacheCount++;

            // 投入数量
            if (battery.CreateTime >= _appGlobalConfig.ShiftSwitchInfo.LastResetTime)
                inputCount++;

            // 出站数量
            if (
                battery.MesOutputTime >= _appGlobalConfig.ShiftSwitchInfo.LastResetTime
                && battery.FinalStatus.GetResultArea() == ResultArea.OK
            )
                outputCount++;

            // 累加工序统计
            AccumulateProcessStatistics(battery);

            AccumulateHourOutput(
                battery,
                timInfo,
                ref previousMinuteQuantity,
                ref currentMinuteQuantity,
                ref currentHourQuantity
            );

            // 检查缓存是否过期
            if (IsCacheExpired(battery))
                removeCacheIds.Add(battery.Id);
        }
        #endregion

        // 百分比计算
        foreach (var rule in ProcessRules)
            CalculateRatio(rule.ProcessRatio);

        // 排序
        var sortedProcessRatios = ProcessRules.Select(x => x.ProcessRatio).OrderByDescending(x => x.NgTotal).ToList();
        // UI更新
        await UIThreadHelper.InvokeOnUiThreadAsync(() =>
        {
            //工序产量统计
            ProductionCounter.InputCount = inputCount;
            ProductionCounter.OutputCount = outputCount;
            ProductionCounter.CacheCount = cacheCount;
            ProcessRatios.Clear();
            ProcessRatios.AddRange(sortedProcessRatios);

            //分钟产量
            Last24HourOutputValue.CurrentMinuteCount = currentMinuteQuantity;
            Last24HourOutputValue.CurrentCountingMinute = timInfo.currentMinuteStart;
            Last24HourOutputValue.LastMinuteCount = previousMinuteQuantity;

            //小时产量
            #region 更新24小时产量分布

            var currentHourData = Last24HourOutputValue.HourlyDatas.FirstOrDefault(x =>
                x.Hour == timInfo.currentHourStart.Hour
            );
            if (currentHourData == null)
            {
                currentHourData = new HourlyData { Time = timInfo.currentHourStart, ValueSuffix = " 颗" };
                Last24HourOutputValue.HourlyDatas.Add(currentHourData);
                Last24HourOutputValue.HourlyDatas.SortBy(x => x.Hour);
            }
            currentHourData.Time = timInfo.currentHourStart;
            currentHourData.Subtitle = $"{timInfo.currentHourStart:dd日 HH时}~{timInfo.currentHourEnd}时";
            currentHourData.IsCurrentHour = true;
            currentHourData.ProductionCount = currentHourQuantity;
            Last24HourOutputValue.CurrentCountingHour = timInfo.currentHourStart.Hour;
            #endregion
        });

        DisplayRatioChanged?.Invoke(sortedProcessRatios.ToArray());

        // 删除过期缓存
        if (removeCacheIds.Count > 0)
            _cacheLazy.Value.RemoveByIds(removeCacheIds);

#if DEBUG
        // stopwatch.Stop();
        // $"统计用时:{stopwatch.ElapsedMilliseconds}ms".LogRun();
#endif
    }

    /// <summary>
    /// 判断缓存是否已过期
    /// </summary>
    private bool IsCacheExpired(IBatMainModel battery)
    {
        // 正常过期时间  为避免跨班边界误删： 额外保留2小时
        var expiredTime = _appGlobalConfig.ShiftSwitchInfo.LastResetTime.AddHours(-2);

        // 最大保留时间  某些异常数据：  无法判断出站状态  超过最大时间强制删除
        var maxExpiredTime = _appGlobalConfig.ShiftSwitchInfo.LastResetTime.AddHours(-12);

        // 已出站
        if (battery.MesOutputTime > _defaultMinTime)
            return battery.MesOutputTime < expiredTime;

        // 未出站
        switch (battery.NgProcesses)
        {
            // 未NG  仍在流程中
            case ProcessTypeEnum._:
                return false;

            // 前扫码NG 历史兼容：老版本未赋值出站时间
            case ProcessTypeEnum.前扫码:
                return battery.CreateTime < expiredTime;

            // 其它异常情况
            default:
                return battery.CreateTime < maxExpiredTime;
        }
    }

    #region 计算小时产量

    /// <summary>
    /// 小时产量统计时间
    /// </summary>
    private readonly record struct HourOutputTime(
        DateTime previousMinuteStart,
        DateTime currentMinuteStart,
        DateTime currentMinuteEnd,
        DateTime currentHourStart,
        DateTime currentHourEnd
    );

    private HourOutputTime GetOutputTime()
    {
        var now = DateTime.Now;

        var currentHourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

        var currentHourEnd = currentHourStart.AddHours(1);

        var currentMinuteStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

        var currentMinuteEnd = currentMinuteStart.AddMinutes(1);

        var previousMinuteStart = currentMinuteStart.AddMinutes(-1);
        return new HourOutputTime(
            previousMinuteStart,
            currentMinuteStart,
            currentMinuteEnd,
            currentHourStart,
            currentHourEnd
        );
    }

    private void AccumulateHourOutput(
        IBatMainModel battery,
        HourOutputTime time,
        ref int previousMinuteQuantity,
        ref int currentMinuteQuantity,
        ref int currentHourQuantity
    )
    {
        var outputTime = battery.MesOutputTime;

        // 未出站 或 NG
        if (outputTime < _defaultMinTime || battery.FinalStatus.GetResultArea() != ResultArea.OK)
        {
            return;
        }

        if (outputTime >= time.previousMinuteStart && outputTime < time.currentMinuteStart)
        {
            previousMinuteQuantity++;
        }

        if (outputTime >= time.currentMinuteStart && outputTime < time.currentMinuteEnd)
        {
            currentMinuteQuantity++;
        }

        if (outputTime >= time.currentHourStart && outputTime < time.currentHourEnd)
        {
            currentHourQuantity++;
        }
    }

    #endregion

    /// <summary>
    /// 统计单个电池
    /// </summary>
    private void AccumulateProcessStatistics(IBatMainModel battery)
    {
        foreach (var rule in ProcessRules)
        {
            try
            {
                // 获取工序时间与结果
                if (
                    battery[rule.TimeName] is not DateTime time
                    || battery[rule.ResultName] is not ResultTypeEnum result
                )
                    continue;

                // 超出统计范围
                if (time < _appGlobalConfig.ShiftSwitchInfo.LastResetTime)
                    continue;

                // 前置结果过滤
                if (
                    rule.PreResultName != null
                    && battery[rule.PreResultName] is ResultTypeEnum preResult
                    && preResult.GetResultArea() == ResultArea.NG
                )
                    continue;

                // 当前结果区域
                var resultArea = result.GetResultArea();
                // 忽略结果
                if (resultArea == ResultArea.Ignore)
                    continue;

                // NG
                if (resultArea == ResultArea.NG)
                {
                    rule.ProcessRatio.NgTotal++;

                    // 细分NG统计
                    if (rule.ProcessRatio.NgDetailMap.TryGetValue(result, out var detail))
                        detail.Count++;

                    continue;
                }

                // OK
                rule.ProcessRatio.OkTotal++;
            }
            catch (Exception ex)
            {
                $"统计工序 {rule.Process} 实时数据时发生异常：{ex}".LogRun(Log4NetLevelEnum.错误, true);
            }
        }
    }

    /// <summary>
    /// 计算百分比
    /// </summary>
    private static void CalculateRatio(ProcessRatioItem item)
    {
        item.TotalCount = item.OkTotal + item.NgTotal;

        // 无数据
        if (item.TotalCount == 0)
        {
            item.OkRatio = 100;
            item.NgRatio = 0;

            foreach (var detail in item.NgDetails)
            {
                detail.Ratio = 0;
            }

            return;
        }

        // OK比例
        item.OkRatio = Math.Round(item.OkTotal * 100.0 / item.TotalCount, 2);

        // NG比例
        item.NgRatio = Math.Round(item.NgTotal * 100.0 / item.TotalCount, 2);

        // NG细分比例
        foreach (var detail in item.NgDetails)
        {
            detail.Ratio = Math.Round(detail.Count * 100.0 / item.TotalCount, 2);
        }
    }

    #endregion
}
