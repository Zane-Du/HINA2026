using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kinlo.DeviceCore.Options;

public class SerialOptions
{
  /// <summary>
  /// 串口号
  /// </summary>
  public string PortName { get; set; } = "COM1"; // 串口号

  /// <summary>
  /// 波特率
  /// </summary>
  public int BaudRate { get; set; } = 9600; // 波特率

  /// <summary>
  /// 数据位
  /// </summary>
  public int DataBits { get; set; } = 8; // 数据位

  /// <summary>
  /// 停止位
  /// </summary>
  public StopBits StopBits { get; set; } = StopBits.One; // 停止位

  /// <summary>
  /// 校验位
  /// </summary>
  public Parity Parity { get; set; } = Parity.None; // 校验位
}
