namespace Kinlo.Equipment.Model.PLCInteractive;

public class PlcToPcInjectionDTU
{
  static int _lineCount = 5,
    _columnCount = 12;
  public PlcToPcInjectionLineDTU[] Lines { get; set; }
  public byte[] TrayCode { get; set; } = new byte[14];
  public byte[] CupCode { get; set; } = new byte[14];
  public short Result { get; set; }

  public PlcToPcInjectionDTU(int lineCount, int columnCount)
  {
    _lineCount = lineCount;
    _columnCount = columnCount;
    Lines = Init();
  }

  public PlcToPcInjectionDTU()
  {
    Lines = Init();
  }

  private PlcToPcInjectionLineDTU[] Init()
  {
    var lines = new PlcToPcInjectionLineDTU[_lineCount];
    for (int i = 0; i < _lineCount; i++)
    {
      lines[i] = new PlcToPcInjectionLineDTU(_columnCount);
    }
    return lines;
  }
}

public class PlcToPcInjectionLineDTU
{
  int _columnCount = 12;
  public PlcToPcInjectionItemDTU[] Columns { get; set; }

  public PlcToPcInjectionLineDTU(int count)
  {
    _columnCount = count;
    Init();
  }

  public PlcToPcInjectionLineDTU()
  {
    Init();
  }

  void Init()
  {
    Columns = new PlcToPcInjectionItemDTU[_columnCount];
    for (int i = 0; i < _columnCount; i++)
    {
      Columns[i] = new PlcToPcInjectionItemDTU();
    }
  }
}

public class PlcToPcInjectionItemDTU
{
  /// <summary>
  /// 泵号
  /// </summary>
  public short InjectionPumpNo { get; set; }

  /// <summary>
  /// 站号
  /// </summary>
  public short InjectionStationNo { get; set; }

  /// <summary>
  /// 整体注液时长
  /// </summary>
  public short InjectionTime { get; set; }

  /// <summary>
  /// 打液时长
  /// </summary>
  public short InjectionDuration { get; set; }

  /// <summary>
  /// 注液嘴号
  /// </summary>
  public short InjectionNozzle { get; set; }
  public short MyProperty1 { get; set; }
  public int MyProperty { get; set; }

  /// <summary>
  /// ID
  /// </summary>
  public long ID { get; set; }
}
