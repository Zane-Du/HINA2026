namespace Kinlo.Services.Handlers;

public partial class PLcStatusAndAlarmHandler
{
    #region MES设备状态改变上传方法
    /// <summary>
    /// 设备状态改变发给MES
    /// </summary>
    /// <param name="newStatus"></param>
    /// <returns></returns>
    private async Task DeviceStatusChangAsync(short newStatus)
    {
        try
        {
            var status = newStatus.ToDeviceState();
            var manualStatus = status.Any(x => x == DeviceStateEnum.待机) ? "0" : "1"; //手自动状态（0：手动；1：自动）
            var runStatus = status.Any(x => x != DeviceStateEnum.运行) ? "0" : "1"; //运行状态（0：非运行；1：运行）
            var waitStatus = status.Any(x => x != DeviceStateEnum.待机) ? "0" : "1"; //待机状态（0：非待机；1：待机）
            var faultStatus = status.Any(x => x != DeviceStateEnum.报警) ? "0" : "1"; //故障状态（0：非故障；1：故障）
            var repairStatus = "0"; //维修状态（0：非维修；1：维修）
            var stopStatus = "0"; //急停状态（0：非急停；1：急停）
            var equipSign = status switch
            {
                var s when s.Any(x => x == DeviceStateEnum.运行) => "1",
                var s when s.Any(x => x == DeviceStateEnum.待机) => "2",
                _ => "0",
            }; //设备状态指示牌（带反馈）（0：停机；1：运行；2：待机；3：故障；4：维修；5：急停；6：其它）
            var warningStatus = status.Any(x => x == DeviceStateEnum.报警) ? "1" : "0"; //报警喇叭状态（0：喇叭停止；1：喇叭报警）

            var args = new MesRequestBuildNJGX.ArgsDeviceStatus(
              manualStatus,
              runStatus,
              waitStatus,
              faultStatus,
              repairStatus,
              stopStatus,
              equipSign,
              warningStatus
            );

            var call = _mesInterfaceParameterConfig.GetApiCall(args);
            if (call != null && call.IsEnable) //有接口并已启用
            {
                var inputResult = await _mesService.SendAsync(
                  call,
                  "",
                  receiveMes => receiveMes.MesCommonParse(_taskLogHeader)
                );
            }
        }
        catch (Exception ex)
        {
            $"设备状态改变发给MES异常：{ex}".LogProcess(_taskLogHeader);
        }
    }

    #endregion
}
