using Kinlo.Common.Configurations;
using NPOI.SS.Formula.Functions;

namespace Kinlo.Common.Tools;

public static class ShiftHelper
{
   /// <summary>
   /// 根据时间返回班次
   /// </summary>
   /// <param name="t"></param>
   /// <param name="parameterConfig"></param>
   /// <returns></returns>
   public static ShiftType GetShiftByTime(this DateTime t, ParameterConfig parameterConfig)
   {
      var dayShif = parameterConfig.DeviceParameter.DayShift;
      var nightShift = parameterConfig.DeviceParameter.NightShift;

      TimeSpan currentTimeSpan = t.TimeOfDay;
      return (currentTimeSpan, dayShif, nightShift) switch
      {
         var e when e.currentTimeSpan >= e.dayShif && e.currentTimeSpan < e.nightShift => ShiftType.白班,
         _ => ShiftType.夜班,
      };
   }

   /// <summary>
   /// 根据时间返回班次详情
   /// </summary>
   /// <param name="t"></param>
   /// <param name="parameterConfig"></param>
   /// <returns></returns>
   public static ShiftInfo GetShiftInfoByTime(this DateTime t, ParameterConfig parameterConfig)
   {
      var dayShif = parameterConfig.DeviceParameter.DayShift;
      var nightShift = parameterConfig.DeviceParameter.NightShift;

      TimeSpan currentTimeSpan = t.TimeOfDay;
      var shift = (currentTimeSpan, dayShif, nightShift) switch
      {
         var e when e.currentTimeSpan >= e.dayShif && e.currentTimeSpan < e.nightShift => ShiftType.白班,
         _ => ShiftType.夜班,
      };

      return GetShiftInfoByDate(t, shift, parameterConfig);
   }

   /// <summary>
   /// 判断时间是否在当前班次
   /// </summary>
   /// <param name="lastExportTime"></param>
   /// <param name="parameterConfig"></param>
   /// <returns></returns>
   public static bool IsTimeInShift(this DateTime lastExportTime, DateTime currentTime, ParameterConfig parameterConfig)
   {
      var dayShifTime = parameterConfig.DeviceParameter.DayShift;
      var nightShiftTime = parameterConfig.DeviceParameter.NightShift;

      var currentDate = currentTime.Date;
      DateTime start = DateTime.Now;
      DateTime end = DateTime.Now;
      var shift = GetShiftByTime(currentTime, parameterConfig);

      if (shift == ShiftType.白班)
      {
         start = currentDate + dayShifTime;
         end = currentDate + nightShiftTime;
      }
      else
      {
         if (currentTime.TimeOfDay >= nightShiftTime)
         {
            start = currentDate + nightShiftTime;
            end = currentDate.AddDays(1) + dayShifTime;
         }
         else
         {
            start = currentDate.AddDays(-1) + nightShiftTime;
            end = currentDate + dayShifTime;
         }
      }
      return lastExportTime >= start && lastExportTime < end;
   }

   public record ShiftInfo(ShiftType shift, DateTime startTime, DateTime endTime);

   /// <summary>
   /// 根据时间获取上一班的时间范围及班次
   /// </summary>
   /// <param name="time"></param>
   /// <param name="parameterConfig"></param>
   /// <returns></returns>
   public static ShiftInfo GetPreShiftInfoByTime(this DateTime time, ParameterConfig parameterConfig)
   {
      var dayShifTime = parameterConfig.DeviceParameter.DayShift;
      var nightShiftTime = parameterConfig.DeviceParameter.NightShift;

      var currentShift = GetShiftByTime(time, parameterConfig);

      if (currentShift == ShiftType.白班)
      {
         var date = time.Date;
         return new ShiftInfo(ShiftType.夜班, date.AddDays(-1) + nightShiftTime, date + dayShifTime);
      }
      else
      {
         var date = time.TimeOfDay >= nightShiftTime ? time.Date : time.AddDays(-1).Date;
         return new ShiftInfo(ShiftType.白班, date + dayShifTime, date + nightShiftTime);
      }
   }

   /// <summary>
   /// 获取当天时间班次详情（DateTime会转换为date）
   /// </summary>
   /// <param name="date"></param>
   /// <param name="shift"></param>
   /// <param name="parameterConfig"></param>
   /// <returns></returns>
   public static ShiftInfo GetShiftInfoByDate(this DateTime date, ShiftType shift, ParameterConfig parameterConfig)
   {
      TimeSpan dayShift = parameterConfig.DeviceParameter.DayShift;
      TimeSpan nightShift = parameterConfig.DeviceParameter.NightShift;
      var groupStartTime = date.Date;

      return shift == ShiftType.白班
         ? new ShiftInfo(shift, groupStartTime.Date + dayShift, groupStartTime.Date + nightShift)
         : new ShiftInfo(shift, groupStartTime.Date + nightShift, groupStartTime.Date.AddDays(1) + dayShift);
   }

   /// <summary>
   /// 根据班计算时间范围
   /// </summary>
   /// <param name="shift"></param>
   /// <param name="start"></param>
   /// <param name="end"></param>
   /// <param name="parameterConfig"></param>
   /// <returns></returns>
   public static (DateTime start, DateTime end) CalculateTimeRangeByShift(
      this ShiftType? shift,
      DateTime start,
      DateTime end,
      ParameterConfig parameterConfig
   ) =>
      shift switch
      {
         null => (
            start.Date + parameterConfig.DeviceParameter.DayShift,
            end.AddDays(1).Date + parameterConfig.DeviceParameter.NightShift
         ),
         ShiftType.白班 => (
            start.Date + parameterConfig.DeviceParameter.DayShift,
            end.Date + parameterConfig.DeviceParameter.NightShift
         ),
         _ => (
            start.Date + parameterConfig.DeviceParameter.NightShift,
            end.AddDays(1).Date + parameterConfig.DeviceParameter.NightShift
         ),
      };

   /// <summary>
   /// 获取时间范围内的所有班次及班次的时间列表
   /// </summary>
   /// <param name="shifts"></param>
   /// <param name="start"></param>
   /// <param name="end"></param>
   /// <param name="parameterConfig"></param>
   /// <returns></returns>
   public static List<ShiftInfo> GetShiftFromTimeRange(
      this string[] shifts,
      DateTime start,
      DateTime end,
      ParameterConfig parameterConfig
   )
   {
      List<ShiftType> shiftTypes = new List<ShiftType>();
      var baseType = Enum.GetValues<ShiftType>();
      foreach (var item in shifts)
      {
         foreach (var s in baseType)
         {
            if (item == s.ToString())
               shiftTypes.Add(s);
         }
      }

      if (shiftTypes.Count == 0)
         return new List<ShiftInfo>();

      return shiftTypes.ToArray().GetShiftFromDateRange(start, end, parameterConfig);
   }

   /// <summary>
   /// 获取时间范围内的所有班次及班次的时间列表(注意 以天计算)
   /// </summary>
   /// <param name="shifts"></param>
   /// <param name="startDate"></param>
   /// <param name="endDate"></param>
   /// <param name="parameterConfig"></param>
   /// <returns></returns>
   public static List<ShiftInfo> GetShiftFromDateRange(
      this ShiftType[] shifts,
      DateTime startDate,
      DateTime endDate,
      ParameterConfig parameterConfig
   )
   {
      TimeSpan dayShift = parameterConfig.DeviceParameter.DayShift;
      TimeSpan nightShift = parameterConfig.DeviceParameter.NightShift;
      List<ShiftInfo> timeGroup = new();

      var orderedShifts = shifts.OrderBy(x => (int)x).ToList();
      var currentTime = startDate.Date;
      while (currentTime <= endDate.Date)
      {
         foreach (var shift in orderedShifts)
         {
            ShiftInfo info = GetShiftInfoByDate(currentTime, shift, parameterConfig);
            timeGroup.Add(info);
         }
         currentTime = currentTime.AddDays(1);
      }
      return timeGroup;
   }
}
