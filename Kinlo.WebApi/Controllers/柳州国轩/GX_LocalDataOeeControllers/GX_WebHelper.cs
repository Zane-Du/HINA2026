namespace Kinlo.WebApi;

public static class GX_WebHelper
{
    /// <summary>
    /// 解析国轩前端返回的班次
    /// </summary>
    /// <param name="shifts"></param>
    /// <returns></returns>
    public static ShiftType? ShiftParse(this string[] shifts) =>
      shifts switch
      {
          { Length: 0 } => null, //空数组或 null
          var s when s.Any(x => x == ShiftType.白班.ToString()) && s.Any(x => x == ShiftType.夜班.ToString()) => null,
          var s when s.Any(x => x == ShiftType.白班.ToString()) => ShiftType.白班,
          var s when s.Any(x => x == ShiftType.夜班.ToString()) => ShiftType.夜班,
          _ => null,
      };

    /// <summary>
    /// 解析国轩前端返回的停机类型
    /// </summary>
    /// <param name="shutdownTypes"></param>
    /// <returns></returns>
    public static DeviceStateEnum[] ShutdownTypeParse(this string[] shutdownTypes) =>
      shutdownTypes switch
      {
          { Length: 0 } => [DeviceStateEnum.待机, DeviceStateEnum.报警], //空数组或 null
          var d when d.Any(x => x == "主动停机") && d.Any(x => x == "被动停机") =>
          [
              DeviceStateEnum.待机,
              DeviceStateEnum.报警,
          ],
          var d when d.Any(x => x == "主动停机") => [DeviceStateEnum.待机],
          var d when d.Any(x => x == "被动停机") => [DeviceStateEnum.报警],
          _ => [DeviceStateEnum.待机, DeviceStateEnum.报警],
      };
}
