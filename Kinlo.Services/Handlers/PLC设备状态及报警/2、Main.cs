using KinloControls;

namespace Kinlo.Services.Handlers;

public partial class PLcStatusAndAlarmHandler
{

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
}
