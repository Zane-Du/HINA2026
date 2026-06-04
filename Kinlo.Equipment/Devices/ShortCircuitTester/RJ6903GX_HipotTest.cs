using System.Text.Json;

namespace Kinlo.Equipment.Devices.ShortCircuitTester;

[DeviceConnec([CommunicationEnum.ShortCircuit_RJ6903GX])]
public class RJ6903GX_HipotTest : DeviceBase
{
   private byte[] _startBytes = [0x7B, 0x00, 0x08, 0x02, 0x0F, 0xFF, 0x18, 0x7D]; //启动测试
   private byte[] _queryBytes = [0x7B, 0x00, 0x08, 0x02, 0xF0, 0xD1, 0xCB, 0x7D];
   private byte[] _queryCurvePoint = [0x7B, 0x00, 0x08, 0x02, 0xF0, 0xC2, 0xBC, 0x7D];

   public RJ6903GX_HipotTest(DeviceInfoModel info)
      : base(info) { }

   public override OperationResult<TClass> ReadClass<TClass>(
      SignalAddressModel address,
      TClass obj,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
      options ??= new DeviceOperationOptions() { RetryCount = 3 };
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);

         try
         {
            Connect.Close();
            Connect.Open();

            var queryResult = PollWithTimeoutSync(10, logHeader);
            if (!queryResult.status)
               return OperationResult<TClass>.Failure(ResultTypeEnum.NG, "读取失败");

            var result = ParseResult(queryResult.bytes!, logHeader);
            #region 取点
            //var writRes = Connect.WriteAndRead(_queryCurvePoint, null, logHeader, 4096);
            //if (writRes.State == CommState.Failed)
            //{
            //    Thread.Sleep(300);
            //    continue;
            //}
            //else if (writRes.State == CommState.NeedReconnect)
            //{
            //    if (!this.Reconnect(logHeader))
            //        return OperationResult<TClass>.Failure(ResultTypeEnum.NG, writRes.Message);
            //    continue;
            //}
            //var curvePointBytes = writRes.Data!;
            //if (curvePointBytes != null)
            //{
            //    if (curvePointBytes.Length >= 480)
            //        result.CurvePoint = BitConverter.ToString(curvePointBytes[6..(curvePointBytes.Length - 2)]);
            //    else
            //    {
            //        $"取到波形长度不对应：[{BitConverter.ToString(curvePointBytes)}]！".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
            //        result.CurvePoint = string.Empty;
            //    }
            //}
            //else
            //{
            //    $"未取到波形！".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
            //    result.CurvePoint = string.Empty;
            //}
            #endregion
            if (result is TClass rs)
               return OperationResult<TClass>.Success(rs);
            else
            {
               var msg = $"[{Helper.GetCurrentMethodName()}]传入数据类型和实际数据类型不对应;";
               msg.LogProcess(logHeader, Log4NetLevelEnum.错误);
               return OperationResult<TClass>.Failure(ResultTypeEnum.数据类型不对应, msg);
            }
         }
         catch (Exception ex)
         {
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
         finally
         {
            Connect?.Close();
         }
      }
      return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
   }

   /// <summary>
   /// 执行带超时的同步轮询操作
   /// </summary>
   /// <param name="timeoutSeconds">超时时间（秒）</param>
   /// <returns>轮询得到的结果，如果超时或失败则返回 null</returns>
   private (bool status, byte[]? bytes) PollWithTimeoutSync(int timeoutSeconds, string logHeader)
   {
      Stopwatch stopwatch = Stopwatch.StartNew(); // 开始计时
      byte[]? bytes = null;
      try
      {
         while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
         {
            var res = Connect.WriteAndRead(_queryBytes, new RJ6900SeriesProtocol(87), logHeader);
            if (res.State == CommState.Failed)
            {
               Thread.Sleep(300);
               continue;
            }
            else if (res.State == CommState.NeedReconnect)
            {
               if (!this.Reconnect(logHeader))
                  return (false, Array.Empty<byte>());
               continue;
            }
            bytes = res.Data!;
            if (bytes != null && bytes.Length == 87)
               return (true, bytes);

            Thread.Sleep(100); // 每次轮询间隔 100 毫秒
         }

         $"查询失败，收到仪器字节不合规 :[{(bytes == null ? "" : BitConverter.ToString(bytes))}]！".LogProcess(
            logHeader,
            Log4NetLevelEnum.错误,
            true
         );
         return (false, bytes);
      }
      catch (Exception ex)
      {
         $"查询异常：{ex}！".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         return (false, bytes);
      }
      finally
      {
         stopwatch.Stop();
      }
   }

   /// <summary>
   /// 解析测试结果
   /// </summary>
   /// <param name="bytes"></param>
   /// <param name="logHeader"></param>
   /// <returns></returns>
   private RJ6903GXHipotResultModel ParseResult(byte[] bytes, string logHeader)
   {
      var result = new RJ6903GXHipotResultModel();
      if (bytes == null || bytes.Length < 87)
      {
         $"测短路结果字节长度过短或为空！".LogProcess(logHeader);
         result.OverallResult = ResultTypeEnum.NG;
         return result;
      }
      //总结果
      result.OverallResult = bytes[6] == 1 ? ResultTypeEnum.OK : ResultTypeEnum.NG;

      //使用 ReadOnlySpan 只读，AsSpan切片性能较高
      ReadOnlySpan<byte> positiveToNegativeBytes = bytes.AsSpan(8, 25); // 正负极: 8-32 (25 bytes)
      ReadOnlySpan<byte> positiveToCaseBytes = bytes.AsSpan(34, 25); // 正极壳: 34-58 (25 bytes)
      ReadOnlySpan<byte> negativeToCaseBytes = bytes.AsSpan(60, 25); // 负极壳: 60-84 (25 bytes)

      result.PositiveToNegative = ParseChanne(positiveToNegativeBytes, "正负极", logHeader);
      result.PositiveToCase = ParseChanne(positiveToCaseBytes, "正极壳", logHeader);
      result.NegativeToCase = ParseChanne(negativeToCaseBytes, "负极壳", logHeader);

      ResultTypeEnum[] hipotChannels =
      [
         result.PositiveToNegative.ChannelResult,
         result.PositiveToCase.ChannelResult,
         result.NegativeToCase.ChannelResult,
      ];

      // 结果仲裁
      result.OverallResult = EvaluateResult(result.OverallResult, hipotChannels, "测短路总结果");
      return result;
   }

   private HipotChannelResult ParseChanne(ReadOnlySpan<byte> bytes, string channelName, string logHeader)
   {
      var result = new HipotChannelResult(channelName);

      if (bytes == null || bytes.Length < 25)
      {
         $"{channelName}结果字节长度过短或为空！".LogProcess(logHeader);
         result.ChannelResult = ResultTypeEnum.NG;
         return result;
      }

      result.Vd1 = ToUShort(bytes, 0);
      result.Vd2 = ToUShort(bytes, 2);
      result.Vd3 = ToUShort(bytes, 4);
      result.VpVoltage = ToUShort(bytes, 6);
      result.TpTime = ToUShort(bytes, 8) / 100.00;
      result.Insulation = ToUInt(bytes, 10) / 10.0;

      //  细分项检查 (使用 Span 避免内存分配,比如[14..24]这种性能更高)
      ReadOnlySpan<byte> itemSpan = bytes.Slice(14, 10);
      var itemResList = new List<ResultTypeEnum>(itemSpan.Length);

      //小项目结果判定
      for (int i = 0; i < itemSpan.Length; i++)
         itemResList.Add(ByteToItemResult(itemSpan[i], i));

      // 总体项目结果判定
      result.ChannelResult = ByteToTotalResult(bytes[^1]);

      // 结果仲裁
      result.ChannelResult = EvaluateResult(result.ChannelResult, itemResList, channelName);
      return result;
   }

   /// <summary>
   /// 结果仲裁
   /// </summary>
   /// <param name="totalRes"></param>
   /// <param name="itemRes"></param>
   /// <param name="logHeader"></param>
   /// <returns></returns>
   private ResultTypeEnum EvaluateResult(ResultTypeEnum totalRes, IEnumerable<ResultTypeEnum> itemRes, string logHeader)
   {
      var hasNg = itemRes.TryFindFirst(x => x != ResultTypeEnum.OK && x != ResultTypeEnum._, out var value);

      if (totalRes == ResultTypeEnum.OK)
      {
         // 总体显示OK，但细分有错 -> 以细分错误为准
         if (hasNg)
         {
            $"{logHeader}总体结果为OK，但细分项发现异常({JsonSerializer.Serialize(itemRes)})，修正结果。".LogProcess(
               logHeader
            );
            return value;
         }
      }
      else if (totalRes == ResultTypeEnum.NG)
      {
         // 总体显示OK，但细分有错 -> 以细分错误为准
         if (hasNg)
         {
            return value;
         }
         else
         {
            $"{logHeader}总体结果为NG，但细分项未检测到具体错误，判定为未知异常。".LogProcess(logHeader);
            return ResultTypeEnum.异常;
         }
      }
      return totalRes;
   }

   /// <summary>
   /// 字节转总结果
   /// </summary>
   /// <param name="b"></param>
   /// <returns></returns>
   private ResultTypeEnum ByteToTotalResult(byte b) =>
      b switch
      {
         0x01 => ResultTypeEnum.OK,
         0X00 => ResultTypeEnum._,
         _ => ResultTypeEnum.NG,
      };

   /// <summary>
   /// 字节转小项结果
   /// </summary>
   /// <param name="bt"></param>
   /// <param name="index"></param>
   /// <returns></returns>
   private ResultTypeEnum ByteToItemResult(byte bt, int index) =>
      bt switch
      {
         0x01 => ResultTypeEnum.OK,
         0X00 => ResultTypeEnum._,
         0XFF => new Func<int, ResultTypeEnum>(i =>
            Rj6903GxResultDic.TryGetValue(i, out var value) ? value : ResultTypeEnum.NG
         )(index),
         _ => ResultTypeEnum.NG,
      };

   private Dictionary<int, ResultTypeEnum> Rj6903GxResultDic = new Dictionary<int, ResultTypeEnum>
   {
      { 0, ResultTypeEnum.E01_开路1 },
      { 1, ResultTypeEnum.E02_严重短路 },
      { 2, ResultTypeEnum.E03_电压欠压 },
      { 3, ResultTypeEnum.E04_电压过压 },
      { 4, ResultTypeEnum.E05_升压阶段Vd1超限 },
      { 5, ResultTypeEnum.E06_电压保持Vd2超限 },
      { 6, ResultTypeEnum.E11_自由放电Vd3超限 },
      { 7, ResultTypeEnum.E07_TLNg_Tp超下限 },
      { 8, ResultTypeEnum.E08_THNg_Tp超上限 },
      { 9, ResultTypeEnum.E09_电阻超下限 },
   };

   private int ToUInt(ReadOnlySpan<byte> bytes, int start) =>
      (int)((bytes[start] << 24) | (bytes[start + 1] << 16) | (bytes[start + 2] << 8) | bytes[start + 3]);

   private ushort ToUShort(ReadOnlySpan<byte> bytes, int start) => (ushort)((bytes[start] << 8) | bytes[start + 1]);

   public override OperationResult<TValue> ReadValue<TValue>(
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      throw new NotImplementedException();
   }

   public override bool WriteClass<TClass>(
      TClass value,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      throw new NotImplementedException();
   }

   public override bool WriteValue(
      object value,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      throw new NotImplementedException();
   }
}
