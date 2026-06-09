using Kinlo.Common.Models.OhtenModels;

namespace Kinlo.Services.Handlers;

/// <summary>
///  气体浓度
/// </summary>
[DeviceConnec(ProcessTypeEnum.注液站浓度, [CommunicationEnum.None])] //指定工艺，可指定多个
[DeviceConnec(ProcessTypeEnum.储液柜浓度, [CommunicationEnum.None])] //指定工艺，可指定多个
[DeviceConnec(ProcessTypeEnum.补液站浓度, [CommunicationEnum.None])] //指定工艺，可指定多个
public class ConcentrationHandler : ServiceHandlerBase
{
    #region 构造函数方法
    public ConcentrationHandler(IContainer container, IDevice plc, PLCInteractAddressModel plcInteractAddress, CancellationTokenSource taskToken) : base(container, plc, plcInteractAddress, taskToken) { }
    #endregion

    protected override async Task HandleCore(short plcValue)
    {
        #region 从PLC读取电池ID信息
        if (!TryReadPlcData(out List<ReceivePlcDataModel> plcDatas))
        {
            return;
        }
        #endregion

        await Parallel.ForEachAsync(plcDatas, new ParallelOptions { MaxDegreeOfParallelism = Context.DataLength }, async (plcData, _) =>
        {
            GasConcentrationModel gasConcentration = new GasConcentrationModel();
            gasConcentration.Id = _snowflakeHelper.NextId();
            gasConcentration.Concentration = plcData.PLCData;
            gasConcentration.Position = Context.ProcessesType switch
            {
                ProcessTypeEnum.注液站浓度 => ConcentrationPositionEnum.注液站,
                ProcessTypeEnum.储液柜浓度 => ConcentrationPositionEnum.储液柜,
                _ => ConcentrationPositionEnum.补液站,
            };
            string logHeader = Context.ToProcessLogHeader(id: gasConcentration.Id);
            await _sugarDB.InsertableAsync(gasConcentration, logHeader);
        });
    }
}
