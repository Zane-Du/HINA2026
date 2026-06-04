namespace Kinlo.Services.Handlers;

[DeviceConnec( ProcessTypeEnum.套杯扫码, [CommunicationEnum.RFID_RD900M, CommunicationEnum.RFID_BIS00EJ, CommunicationEnum.RFID_TAIHESEN])]
[DeviceConnec( ProcessTypeEnum.托盘扫码, [CommunicationEnum.RFID_RD900M, CommunicationEnum.RFID_BIS00EJ, CommunicationEnum.RFID_TAIHESEN])]
public class RFIDReadCodeHandler : ServiceHandlerBase
{
    #region 构造函数方法
    public RFIDReadCodeHandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }

    #endregion

    #region 写入PLC条码，ID，结果方法
    private void SendPlcData(ResultTypeEnum result, string barcode)
    {
        #region 写入PLC条码
        var _barceodeTag = new SignalAddressModel($"{Context.DataAddress.Lable}.ToPLCData[{0}].Code", 0);
        for (int n = 1; n < 4; n++)
        {
            if (_plc.WriteValue(barcode, _barceodeTag, _taskLogHeader))
            {
                $"第[{n}]次 地址[{JsonSerializer.Serialize(_barceodeTag)}]写入{Context.ProcessesType}字符型条码[{barcode}] 成功".LogProcess(
                  _taskLogHeader
                );
                break;
            }
            else
            {
                $"第[{n}]次 地址[{JsonSerializer.Serialize(_barceodeTag)}]写入{Context.ProcessesType}字符型条码[{barcode}] 失败".LogProcess(
                  _taskLogHeader
                );
            }
        }
        #endregion

        #region 写入PLC托盘ID
        //if (Context.ProcessesType == ProcessTypeEnum.托盘扫码)//托盘唯一ID，保存静置站数据用
        //{
        //    long _trayId = _snowflakeHelper.NextId();
        //    var _idTag = new SignalAddressModel($"{Context.DataAddress.Lable}.ToPCData[{0}].ID", 0);
        //    for (int n = 1; n < 4; n++)
        //    {
        //        if (_plc.WriteValue(_trayId, _idTag))
        //        {
        //            TaskLog($"第[{n}]次地址[{JsonSerializer.Serialize(_idTag)}]托盘ID[{_trayId}] 成功;", Context.DeviceStartIndex);
        //            break;
        //        }
        //        else
        //        {
        //            TaskLog($"第[{n}]次地址[{JsonSerializer.Serialize(_idTag)}]托盘ID[{_trayId}] 失败;", Context.DeviceStartIndex);
        //        }
        //    }
        //}
        #endregion

        #region 写入PLC结果
        new SignalAddressModel($"{Context.DataAddress.Lable}.ToPLCData[0]", 0).WritePlcResult(result, ResultTypeEnum._, _plc, _parameterConfig, _taskLogHeader); //写入PLC结果

        #endregion
    }

    #endregion
    protected override Task HandleCore(short plcValue)
    {
        #region 根据信号配置界面，拿到托盘/套杯扫码设备
        if (_parameterConfig.FunctionEnable.IsEmptyLoadMode) //空跑则不去获取设备数据
        {
            $"开启空载模式，随机生成{Context.ProcessesType}号码;".LogProcess(_taskLogHeader, Log4NetLevelEnum.警告);
            var code = new Random().Next(1, 100).ToString();
            SendPlcData(ResultTypeEnum.OK, code);
            return Task.CompletedTask;
        }
        var device = _devicesConfig.GetRunDevice(Context, Context.DeviceStartIndex);

        if (device == null)
        {
            _isDeviceAlarm = true;
            $"找不到设备，不给PLC发结果及完成信号，请停机检查！;".LogProcess(_taskLogHeader, Log4NetLevelEnum.错误, true);
            return Task.CompletedTask;
        }
        #endregion

        #region 执行托盘/套杯扫码方法，拿到扫码值
        var deviceResult = device.ReadValue<string>(new SignalAddressModel(), _taskLogHeader);
        (ResultTypeEnum result, string code) sendRs = deviceResult switch
        {
            var dr when dr.IsSuccess && !string.IsNullOrWhiteSpace(dr.Value) => (ResultTypeEnum.OK, dr.Value),
            _ => (ResultTypeEnum.NG, "Error"),
        };
        #endregion

        #region 写入PLC条码，ID，结果
        SendPlcData(sendRs.result, sendRs.code);
        #endregion

        return Task.CompletedTask;
    }

}
