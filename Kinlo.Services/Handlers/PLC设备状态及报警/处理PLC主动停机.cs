using KinloControls;

namespace Kinlo.Services.Handlers;

public partial class PLcStatusAndAlarmHandler
{
    #region MES手动停机上传方法
    /// <summary>
    /// 发生新的手动停机，一般是发给MES
    /// </summary>
    /// <param name="oldPlcStatus"></param>
    /// <param name="newPlcStatus"></param>
    private async Task StartManualShutdownHandleAsync(TimelineItem shutdown)
    {
        var args = new MesRequestBuildNJGX.ArgsActiveShutdown(
          0,
          shutdown.Id.ToString(),
          "",
          shutdown.StartTime,
          null,
          string.Empty
        );
        var call = _mesInterfaceParameterConfig.GetApiCall(args);
        if (call != null && call.IsEnable) //有接口并已启用
        {
            var inputResult = await _mesService.SendAsync(call, "", receiveMes => receiveMes.MesCommonParse(_taskLogHeader));
        }
    }

    #endregion


    #region MES主动停机结束上传方法
    /// <summary>
    /// 主动停机结束
    /// </summary>
    /// <returns></returns>
    private async Task EndManualShutdownHandleAsync(TimelineItem shutdown)
    {
        string stopReason = string.Empty; //停机原因
        if (_shutdownAddress == null)
        {
            stopReason = "未配置主动停机读取标签,无法读取主动停机原因；";
            stopReason.LogProcess(_taskLogHeader);
        }
        else
        {
            var stopReasonResult = _plc.ReadValue<short>(_shutdownAddress, _taskLogHeader);
            if (stopReasonResult.IsSuccess)
            {
                try
                {
                    if (Enum.IsDefined(typeof(PlcStopReasonTypeEnum), (int)stopReasonResult.Value))
                    {
                        var reason = (PlcStopReasonTypeEnum)stopReasonResult.Value;
                        stopReason = reason.ToString();
                    }
                    else
                    {
                        stopReason = $"主动停机原因[{stopReasonResult.Value}]不是有效枚举；";
                        stopReason.LogProcess(_taskLogHeader);
                    }
                }
                catch (Exception ex)
                {
                    stopReason = $"主动停机原因[{stopReasonResult.Value}]转换异常：{ex.Message}；";
                    stopReason.LogProcess(_taskLogHeader);
                }
            }
            else
            {
                stopReason = $"读取主动停机原因失败，原因：{stopReasonResult.ErrorMessage}";
                stopReason.LogProcess(_taskLogHeader);
            }
        }
        await UIThreadHelper.InvokeOnUiThreadAsync(() =>
        {
            shutdown.EndTime = DateTime.Now;
            shutdown.Message = stopReason;
        });

        var span = Math.Round(((DateTime)shutdown.EndTime - shutdown.StartTime).TotalSeconds);
        var args = new MesRequestBuildNJGX.ArgsActiveShutdown(  1, shutdown.Id.ToString(),   shutdown.Message, shutdown.StartTime,  shutdown.EndTime,span.ToString());
        var call = _mesInterfaceParameterConfig.GetApiCall(args);

        if (call != null && call.IsEnable) //有接口并已启用
        {
            var inputResult = await _mesService.SendAsync(call, "", receiveMes => receiveMes.MesCommonParse(_taskLogHeader));
        }
        //手工停机结束，使用 timeline在此传递给MES
    }

    #endregion
}
