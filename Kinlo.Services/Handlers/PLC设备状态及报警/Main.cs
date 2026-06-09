using KinloControls;

namespace Kinlo.Services.Handlers;


[DeviceConnec(ProcessTypeEnum.设备状态, [CommunicationEnum.None])] //指定工艺，可指定多个
public partial class PLcStatusAndAlarmHandler : ServiceHandlerBase
{
    #region 构造函数方法
    private short _lastPlcValue { get; set; } = -1;
    private readonly List<DeviceStateEnum> _deviceStateEnums = new List<DeviceStateEnum>();
    protected SignalAddressModel? _shutdownAddress = null; //主动停机原因地址

    public PLcStatusAndAlarmHandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken)
    {
        _deviceStateEnums = Enum.GetValues(typeof(DeviceStateEnum)).Cast<DeviceStateEnum>().ToList();
        if (!string.IsNullOrWhiteSpace(Context.ExtraDataAddress.Lable))
        {
            _shutdownAddress = Context.ExtraDataAddress;
        }
        else
        {
            var add = _plcSignalConfig.CustomPlcInteractAddresses.FirstOrDefault(x =>
               x.CustomInteractName == CustomInteractNameEnum.PLC主动停机原因
            );
            if (add != null)
            {
                _shutdownAddress = add.DataAddress;
            }
        }
    }
    #endregion

    #region 不给返回2信号的Handle方法
    /// <summary>
    /// 设备状态因无需写完成信号，需重写Handle方法
    /// </summary>
    /// <param name="plcValue"></param>
    /// <returns></returns>
    public override async Task<int> Handle(short plcValue)
    {
        try
        {
            await HandleCore(plcValue);
        }
        catch (Exception ex)
        {
            $"出现异常:{ex};".LogProcess(_taskLogHeader, Log4NetLevelEnum.错误, true);
        }
        finally
        {
            _lastPlcValue = plcValue;
            try
            {
                await Task.Delay(_parameterConfig.AdvancedConfig.DeviceStatusSpanMillisecond, _cancellationTokenSource.Token); //1秒防抖
            }
            catch (OperationCanceledException) { } // 外部取消时正常退出
        }
        return await Task.FromResult(Context.Key);
    }

    #endregion

    #region 处理设备状态方法
    /// <summary>
    /// 处理设备状态
    /// </summary>
    /// <param name="stateType"></param>
    /// <param name="oldPlcStatus"></param>
    /// <param name="newPlcStatus"></param>
    /// <returns></returns>
    public async Task PlcStatusHandleAsync(DeviceStateEnum stateType, bool oldPlcStatus, bool newPlcStatus)
    {
        try
        {
            // $"状态[{stateType}]新旧值：{newPlcStatus}=>{oldPlcStatus}".LogProcess(_taskLogHeader);
            if (!_plcStatusConfig.PlcStatusDisplayDic.TryGetValue(stateType, out var statusDescription))
            {
                $"未配置当前状态[{stateType}]".LogProcess(_taskLogHeader);
                return;
            }
            var now = DateTime.Now;

            if (newPlcStatus)
            {
                if (!oldPlcStatus) //新状态
                {
                    string stopReason = stateType == DeviceStateEnum.报警 ? await StartAlarmHandleAsync() : stateType.ToString(); //停机原因，只有待机(主动停机)和报警(被动停机)有停机原因

                    var item = CreateTimeLine(stateType, statusDescription, now, stopReason);

                    if (stateType == DeviceStateEnum.待机)
                    {
                        await StartManualShutdownHandleAsync(item);
                    }
                    else if (stateType is DeviceStateEnum.堵料 or DeviceStateEnum.待料)
                    {
                        await StartMaterialShortageHandleAsync(item);
                    }

                    await _plcStatusConfig.AddTimeline(item);
                }
                else //状态持续
                {
                    var lastItem = _plcStatusConfig.GetTimelineLastOrDefault(x => x.Value == (int)stateType);
                    if (lastItem != null)
                    {
                        if ((now - lastItem.EndTime) > TimeSpan.FromSeconds(3)) //超3s才更新UI
                            await UIThreadHelper.InvokeOnUiThreadAsync(() => lastItem.EndTime = now);
                    }

                    if (stateType == DeviceStateEnum.报警)
                        await PendingAlarmHandleAsync(); //持续报警
                }
            }
            else
            {
                if (oldPlcStatus) //状态切换至结束
                {
                    var lastItem = _plcStatusConfig.GetTimelineLastOrDefault(x => x.Value == (int)stateType);
                    if (lastItem != null)
                    {
                        if (stateType == DeviceStateEnum.报警)
                        {
                            await EndAalrmHandleAsync();
                        }
                        else if (stateType == DeviceStateEnum.待机)
                        {
                            await EndManualShutdownHandleAsync(lastItem); //待机（主动停机）是结束时才有原因
                        }
                        else if (stateType is DeviceStateEnum.堵料 or DeviceStateEnum.待料)
                        {
                            await EndMaterialShortageHandleAsync(lastItem);
                        }
                        if (_plcStatusConfig.PlcStatusPendingSaveTasks.TryRemove(lastItem.Id, out var task)) //保存
                            await task.Invoke(string.Empty);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            $"处理设备状态发生异常：{ex}".LogProcess(_taskLogHeader);
        }
    }

    #endregion

    #region 创建时间线及委托保存任务方法
    /// <summary>
    /// 创建时间线及委托保存任务
    /// </summary>
    /// <param name="plcStatus"></param>
    /// <param name="status"></param>
    /// <param name="color"></param>
    /// <returns></returns>
    private TimelineItem CreateTimeLine(DeviceStateEnum plcStatus, PlcStatusDisplayModel plcStatusDisplay, DateTime startTime, string stopReason)
    {
        var uiItem = new TimelineItem
        {
            Id = _snowflakeHelper.NextId(),
            Value = (int)plcStatus,
            StartTime = startTime,
            EndTime = startTime.AddMilliseconds(100),
            Label = plcStatusDisplay.Description,
            Color = plcStatusDisplay.Color,
            Message = stopReason,
        };
        //msg可用于结束状态时额外传入，当主动停机时是停机时才传入。还有在手动关闭软件时使用
        _plcStatusConfig.PlcStatusPendingSaveTasks.TryAdd(
          uiItem.Id,
          async msg =>
          {
              await UIThreadHelper.InvokeOnUiThreadAsync(() =>
              {
                  uiItem.EndTime = DateTime.Now;

                  if (
                (DeviceStateEnum)uiItem.Value == DeviceStateEnum.待机
                && string.IsNullOrWhiteSpace(uiItem.Message)
                && !string.IsNullOrWhiteSpace(msg)
              )
                      uiItem.Message = msg;
              });

              var entity = ToPlcStatusSaveData(uiItem);
              await _sugarDB.InsertableAsync(entity, _taskLogHeader);
          }
        );

        return uiItem;
    }

    #endregion

    #region PLC状态保存方法
    public PlcStatusModel ToPlcStatusSaveData(TimelineItem timeline)
    {
        return new PlcStatusModel
        {
            Id = timeline.Id,
            Shift = timeline.StartTime.GetShiftByTime(_parameterConfig),
            Status = (DeviceStateEnum)timeline.Value,
            StartTime = timeline.StartTime,
            EndTime = timeline.EndTime,
            Msg = timeline.Message.Length > 254 ? timeline.Message.Substring(0, 254) : timeline.Message,
        };
    }

    #endregion

    protected override async Task<int> HandleCore(short plcValue)
    {
        #region 开机第一次进入，直接返回
        if (_lastPlcValue == -1) //开机第一次进入
        {
            return Context.Key;
        }
        #endregion

        #region 添加主动被动待料堵料MES任务
        //LINQ方法，Select链式遍历等同于for循环
        var tasks = _deviceStateEnums.Select(stateType =>
        {
            var newPlcStatus = plcValue.GetPlcStatus(stateType);
            var oldPlcStatus = _lastPlcValue.GetPlcStatus(stateType);

            return PlcStatusHandleAsync(stateType, oldPlcStatus, newPlcStatus);
        }).ToList();
        #endregion

        #region 如果设备状态改变，添加上传MES任务
        if (plcValue != _lastPlcValue) //当设备状态改变时发送给MES
        {
            tasks.Add(DeviceStatusChangAsync(plcValue));
        }


        #endregion

        #region MES任务集合上传
        await Task.WhenAll(tasks);
        #endregion

        #region 裁剪PLC状态到24小时，保存PLC24小时状态，刷新界面
        await _plcStatusConfig.TrimPlcStatusToLast24HoursAsync();

        return Context.Key;

        #endregion

    }
}
