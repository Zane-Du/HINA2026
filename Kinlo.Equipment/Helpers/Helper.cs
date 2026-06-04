using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace Kinlo.Equipment.Helpers;

public static class Helper
{
  /// <summary>
  /// 用byte[]分割另一个byte[]
  /// </summary>
  /// <param name="input"></param>
  /// <param name="separator"></param>
  /// <returns></returns>
  public static List<byte[]> SplitByteArray(this byte[] input, byte[] separator)
  {
    List<byte[]> result = new List<byte[]>();
    List<byte> current = new List<byte>();

    for (int i = 0; i < input.Length; i++)
    {
      if (MatchSeparator(input, i, separator))
      {
        if (current.Count > 0)
        {
          result.Add(current.ToArray());
          current.Clear();
        }
        i += separator.Length - 1; // Skip over the separator
      }
      else
      {
        current.Add(input[i]);
      }
    }

    if (current.Count > 0)
    {
      result.Add(current.ToArray());
    }

    return result;
  }

  static bool MatchSeparator(byte[] input, int index, byte[] separator)
  {
    if (index + separator.Length > input.Length)
      return false;

    for (int i = 0; i < separator.Length; i++)
    {
      if (input[index + i] != separator[i])
        return false;
    }
    return true;
  }

  public static string ToDeviceLogHeader(this DeviceInfoModel deviceInfo)
  {
    if (deviceInfo == null)
      return "[未知设备]";
    return $"[{deviceInfo.ServiceName}-{deviceInfo.ProcessesType}-{deviceInfo.Communication}-{deviceInfo.Index}-{deviceInfo.IPCOM}:{deviceInfo.Port}]";
  }

  public static string SplitDeviceLogHeader(
    this DeviceInfoModel deviceInfo,
    string logHeader,
    params SignalAddressModel[]? signalAddress
  )
  {
    string des = string.Empty;
    if (signalAddress != null)
    {
      List<string> tags = new List<string>();
      foreach (var item in signalAddress)
      {
        if (item != null && !string.IsNullOrEmpty(item.Lable))
        {
          tags.Add(item.Lable);
        }
      }
      des = $" [标签或地址：{string.Join(',', tags)}]";
    }
    return $"{logHeader}{(deviceInfo == null ? "" : $" [{deviceInfo.IPCOM}-{deviceInfo.Port}]")}{des}";
    ;
  }

  /// <summary>
  /// 取方法名
  /// </summary>
  /// <param name="memberName"></param>
  /// <returns></returns>
  public static string GetCurrentMethodName([CallerMemberName] string memberName = "") => memberName;
}
