namespace Kinlo.Common.Models.BatteryModels;

/// <summary>
/// 测短路 ST5520
/// </summary>
[BatteryDisplay(DisplayProcesses = [ProcessTypeEnum.测短路], DeviceCommunicationType = [CommunicationEnum.ShortCircuit_ST5520])]
[Languages(["测短路", "测短路", "Short circuit test"])]
[AddINotifyPropertyChangedInterface]
public partial class BatShortCircuitST5520Model
{
    /// <summary>
    /// 自动生成
    /// </summary>
    [SugarColumn(ColumnDescription = "短路时间")]
    [Languages(["短路时间", "短路时间", "ShortCircuit time"])]
    public DateTime? ShortCircuitTestRJTime { get; set; }

    /// <summary>
    /// 短路测试位置
    /// </summary>
    [SugarColumn(ColumnDescription = "短路测试位置", ColumnDataType = "tinyint UNSIGNED")] //UNSIGNED 无符号     
    [Languages(["短路测试位置", "短路测试位置", "ShortCircuit index"])]
    public byte? ShortCircuitTestRJIndex { get; set; }




    /// <summary>
    /// 电阻测试值1(MΩ)
    /// </summary>
    [SugarColumn(ColumnDescription = "电阻测试值1(MΩ)")]
    [Languages(["电阻测试值1", "", ""])]
    public float ResistanceTestValue1 { get; set; }

    /// <summary>
    /// 电阻测试值2(MΩ)
    /// </summary>
    [SugarColumn(ColumnDescription = "电阻测试值2(MΩ)")]
    [Languages(["电阻测试值2", "", ""])]
    public float ResistanceTestValue2 { get; set; }


    /// <summary>
    /// 电阻测试值3(MΩ)
    /// </summary>
    [SugarColumn(ColumnDescription = "电阻测试值3(MΩ)")]
    [Languages(["电阻测试值3", "", ""])]
    public float ResistanceTestValue3 { get; set; }



    /// <summary>
    /// 短路测试结果1
    /// </summary>
    [SugarColumn(ColumnDescription = "短路测试结果1")]
    [Languages(["短路测试结果1", "", ""])]
    public ResultTypeEnum ShortCircuitResult1 { get; set; }

    /// <summary>
    /// 短路测试结果2
    /// </summary>
    [SugarColumn(ColumnDescription = "短路测试结果2")]
    [Languages(["短路测试结果2", "", ""])]
    public ResultTypeEnum ShortCircuitResult2 { get; set; }

    /// <summary>
    /// 短路测试结果3
    /// </summary>
    [SugarColumn(ColumnDescription = "短路测试结果3")]
    [Languages(["短路测试结果3", "", ""])]
    public ResultTypeEnum ShortCircuitResult3 { get; set; }





    /// <summary>
    /// NG原因
    /// </summary>
    [SugarColumn(ColumnDescription = "NG原因")]
    [Languages(["NG原因", "", ""])]
    public string NgStr { get; set; }

    /// <summary>
    /// 短路测试结果
    /// </summary>
    [SugarColumn(ColumnDescription = "短路测试结果")]
    [Languages(["短路测试结果", "", ""])]
    public ResultTypeEnum ShortCircuitResult { get; set; }
}
