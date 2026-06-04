namespace Kinlo.Services.Handlers;

[DeviceConnec(ProcessTypeEnum.前称重清零, [CommunicationEnum.None])]
[DeviceConnec(ProcessTypeEnum.后称重清零, [CommunicationEnum.None])]
[DeviceConnec(ProcessTypeEnum.补液称清零, [CommunicationEnum.None])]
[DeviceConnec(ProcessTypeEnum.下料称重清零, [CommunicationEnum.None])]
[DeviceConnec(ProcessTypeEnum.回氦称重清零, [CommunicationEnum.None])]
public class WeightZeroingHandler : ServiceHandlerBase
{
    #region 构造函数方法
    //添加界面显示
    List<int> _alarmDevice = new List<int>();
    SignalAddressModel? _alarmAddress = null;

    public WeightZeroingHandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken)
    {
        _alarmAddress = Context.ProcessesType switch
        {
            ProcessTypeEnum.前称重清零 => _plcSignalConfig.PLCAlarmAddresses.Alarm_Zeroing_BeforeWeighing,
            ProcessTypeEnum.后称重清零 or ProcessTypeEnum.回氦称重清零 => _plcSignalConfig
              .PLCAlarmAddresses
              .Alarm_Zeroing_AfterWeighing,
            ProcessTypeEnum.补液称清零 => _plcSignalConfig.PLCAlarmAddresses.Alarm_Zeroing_RefillWeighing,
            ProcessTypeEnum.下料称重清零 => _plcSignalConfig.PLCAlarmAddresses.Alarm_Zeroing_DownWeighing,
        };
    }
    #endregion

    protected override Task HandleCore(short plcValue)
    {
        #region 根据信号配置界面，找到称重设备
        _alarmDevice.Clear();
        var devices = _devicesConfig.GetRunDevices(x =>
          x.DeviceInfo.ServiceName == Context.ServiceName
          && x.DeviceInfo.Communication == Context.DeviceCommunicationType
          && Context.ProcessesType.ToString().Contains(x.DeviceInfo.ProcessesType.ToString())
        );

        if (devices.Count < 1)
        {
            _isDeviceAlarm = true;
            $"找不到设备，不给PLC发结果及完成信号，请停机检查！;".LogProcess(_taskLogHeader, Log4NetLevelEnum.错误, true);
            return Task.CompletedTask;
            ;
        }
        if (devices.Count < Context.DataLength)
        {
            $"注意：实际设备数量{devices.Count}小于设置数量{Context.DataLength};".LogProcess(
              _taskLogHeader,
              Log4NetLevelEnum.警告,
              true
            );
        }
        #endregion

        for (int i = 0; i < 3; i++)
        {
            #region 执行称重设备写入0方法
            Parallel.ForEach(devices, device =>
            {
                device.WriteValue(0, null, Context.ToProcessLogHeader(device.DeviceInfo.Index));
            });
            Thread.Sleep(500);
            _alarmDevice.Clear();
            #endregion

            Parallel.ForEach(  devices,   device =>
            {
                #region 读取称重设备值是否为0
                var deviceResult = device.ReadValue<double>(null, Context.ToProcessLogHeader(device.DeviceInfo.Index));
                var sendRs = deviceResult switch
                {
                    var dr when dr.IsSuccess && dr.Value >= -0.1 && dr.Value <= 0.1 => ResultTypeEnum.OK,
                    _ => ResultTypeEnum.NG,
                };

                ResultTypeEnum rs = ResultTypeEnum.OK;
                if (sendRs != ResultTypeEnum.OK)
                {
                    _alarmDevice.Add(device.DeviceInfo.Index);
                }
                #endregion

                #region 写入PLC结果
                new SignalAddressModel($"{Context.DataAddress.Lable}.ToPLCData[{device.DeviceInfo.Index - 1}]").WritePlcResult(rs, ResultTypeEnum._, _plc, _parameterConfig, Context.ToProcessLogHeader(device.DeviceInfo.Index)); //写入PLC结果

                #endregion
            });

          if (!_alarmDevice.Any())
            break;
        }
        #region 如果称重设备有报警，报警
        if (_alarmDevice.Any())
        {
            $"{string.Join(',', _alarmDevice.Select(x => $"{x}号"))} 无法清零！".LogProcess(
              _taskLogHeader,
              Log4NetLevelEnum.错误,
              true
            );
            // _isDeviceAlarm = true;
            // WritePlcSingle(1, _alarmAddress);报警 弃用
        }
        #endregion

        return Task.CompletedTask;
    }
}
