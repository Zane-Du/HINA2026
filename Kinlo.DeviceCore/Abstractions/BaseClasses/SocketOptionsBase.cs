using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kinlo.DeviceCore.Abstractions.BaseClasses;

public abstract class SocketOptionsBase
{
  public string IP { get; set; } = string.Empty;
  public int Port { get; set; }
  public int Timeout { get; set; } = 3000;
}
