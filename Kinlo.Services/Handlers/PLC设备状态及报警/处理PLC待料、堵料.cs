using KinloControls;

namespace Kinlo.Services.Handlers;

public partial class PLcStatusAndAlarmHandler
{
    #region MES待料，堵料开始上传方法
    /// <summary>
    /// 发生新的待料、堵料，一般是发给MES
    /// </summary>
    /// <param name="oldPlcStatus"></param>
    /// <param name="newPlcStatus"></param>
    private async Task StartMaterialShortageHandleAsync(TimelineItem shutdown)
    {
        var args = new MesRequestBuildNJGX.ArgsMaterialShortage( 0,   shutdown.Id.ToString(), shutdown.Message,    shutdown.StartTime,  null, string.Empty );
        var call = _mesInterfaceParameterConfig.GetApiCall(args);
        if (call != null && call.IsEnable) //有接口并已启用
        {
            var inputResult = await _mesService.SendAsync(call, "", receiveMes => receiveMes.MesCommonParse(_taskLogHeader));
        }
    }

    #endregion

    #region MES待料，堵料结束上传方法
    /// <summary>
    /// 待料、堵料结束
    /// </summary>
    /// <returns></returns>
    private async Task EndMaterialShortageHandleAsync(TimelineItem shutdown)
    {
        await UIThreadHelper.InvokeOnUiThreadAsync(() =>
        {
            shutdown.EndTime = DateTime.Now;
        });

        var span = Math.Round((shutdown.StartTime - (DateTime)shutdown.EndTime).TotalSeconds);
        var args = new MesRequestBuildNJGX.ArgsMaterialShortage(    1,    shutdown.Id.ToString(),   shutdown.Message, shutdown.StartTime,  shutdown.EndTime,  span.ToString());

        var call = _mesInterfaceParameterConfig.GetApiCall(args);

        if (call != null && call.IsEnable) //有接口并已启用
        {
            var inputResult = await _mesService.SendAsync(call, "", receiveMes => receiveMes.MesCommonParse(_taskLogHeader));
        }
        //手工停机结束，使用 timeline在此传递给MES
    }

    #endregion

}
