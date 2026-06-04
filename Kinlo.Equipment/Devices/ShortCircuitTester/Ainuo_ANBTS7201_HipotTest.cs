using System.Formats.Asn1;
using System.Windows.Interop;

namespace Kinlo.Equipment.Devices.ShortCircuitTester;

[DeviceConnec([CommunicationEnum.ShortCircuit_Ainuo_ANBTS7201])]
public class Ainuo_ANBTS7201_HipotTest : DeviceBase
{
   private byte[] _startBytes = [0x7B, 0x00, 0x08, 0x02, 0x0F, 0xFF, 0x18, 0x7D]; //启动测试
   private byte[] _queryBytes = [0x7B, 0x00, 0x08, 0x02, 0xF0, 0x7C, 0x76, 0x7D];

   public Ainuo_ANBTS7201_HipotTest(DeviceInfoModel info)
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
            //Connect.Close();
            //Connect.Open();
            var res = Connect.WriteAndRead(_startBytes, new RJ6900SeriesProtocol(9), logHeader);

            if (res.State == CommState.Failed)
            {
               Thread.Sleep(300);
               continue;
            }
            else if (res.State == CommState.NeedReconnect)
            {
               if (!this.Reconnect(logHeader))
                  return OperationResult<TClass>.Failure(ResultTypeEnum.NG, res.Message);
               continue;
            }
            var bytes = res.Data!;

            $"启动测试!".LogProcess(logHeader);
            // $"启动测试返回报文，字节:[{BitConverter.ToString(bytes)}]".LogProcess(logHeader);
            if (bytes[4] != 0x0F)
            {
               errMsg = $"启动失败，状态码[{bytes[4]}]，字节:[{BitConverter.ToString(bytes)}]";
               res.State = CommState.Failed;
               res.Message = errMsg;
               errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
               continue;
            }

            Thread.Sleep(700);
            var queryResult = PollWithTimeoutSync(6, logHeader);
            if (!queryResult.status)
               return OperationResult<TClass>.Failure(ResultTypeEnum.NG, "读取失败");

            var result = GetResult(queryResult.bytes!, logHeader);

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
         //finally
         //{
         //    Connect.Close();
         //}
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
            var res = Connect.Write(_queryBytes, logHeader);
            if (res.State == CommState.Success)
            {
               Thread.Sleep(20);
               var readRes = Connect.Read(4096, logHeader);
               if (res.State == CommState.Failed)
               {
                  Thread.Sleep(300);
                  continue;
               }
               else if (res.State == CommState.NeedReconnect)
               {
                  if (!this.Reconnect(logHeader))
                     return (false, []);
                  continue;
               }
               if (readRes.Data!.Length > 9)
               {
                  bytes = readRes.Data;
                  if (bytes[0] == 0x7B && bytes[bytes.Length - 1] == 0x7D)
                  {
                     if (RJ6900SeriesProtocol.OnGetVerifySum(bytes)) // 校验和正确
                        break;
                     else
                     {
                        $"查询失败，仪器校验和错误，字节[{BitConverter.ToString(bytes)}]".LogProcess(
                           logHeader,
                           Log4NetLevelEnum.错误,
                           true
                        );
                     }
                  }
                  else
                  {
                     $"查询失败，仪器起始或结束字节不符合，字节[{BitConverter.ToString(bytes)}]".LogProcess(
                        logHeader,
                        Log4NetLevelEnum.错误,
                        true
                     );
                  }
               }
               else
               {
                  $"查询失败，仪器字节长度不符合！".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
               }
            }
            Thread.Sleep(100); // 每次轮询间隔 100 毫秒
         }
         if (bytes == null)
         {
            $"查询失败，未收到仪器字节！".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
            return (false, bytes);
         }
         else
         {
            $"查询成功".LogProcess(logHeader, Log4NetLevelEnum.成功);
            // $"查询成功，字节[{BitConverter.ToString(bytes)}]".LogProcess(logHeader, Log4NetLevelEnum.成功);
            return (true, bytes);
         }
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

   private int ToUInt(byte[] bytes, int start) =>
      (int)((bytes[start] << 24) | (bytes[start + 1] << 16) | (bytes[start + 2] << 8) | bytes[start + 3]);

   private ushort ToUShort(byte[] bytes, int start) => (ushort)((bytes[start] << 8) | bytes[start + 1]);

   /// <summary>
   /// 客户俩种仪器混用，此处解析为Ac3200HipotResultModel
   /// </summary>
   /// <param name="bytes1"></param>
   /// <param name="logHeader"></param>
   /// <returns></returns>
   Ac3200HipotResultModel GetResult(byte[] bytes1, string logHeader)
   {
      var bytes = bytes1[6..];
      var result = new Ac3200HipotResultModel();
      result.HipotFallOne = ToUShort(bytes, 0);
      result.HipotFallTwo = ToUShort(bytes, 2);
      result.HipotFallThree = ToUShort(bytes, 4);
      result.HipotVpVoltage = ToUShort(bytes, 6);
      result.HipotPulseTp = (float)(ToUShort(bytes, 8) / 100.00);
      // result.InsulationTestValue = (float)(ToUInt(bytes, 12) / 10.0);
      ResultTypeEnum testResult = ParsePulseResult(bytes, 14); //脉冲结果
      result.HipotPulseResult = testResult.ToString();
      var resutl1 = ParseResult(bytes[23], ResultTypeEnum.电阻测试NG); //电阻结果
      result.ResistanceTestResult = resutl1 == ResultTypeEnum._ ? "" : resutl1.ToString();

      result.HipotResult = ParseResult(bytes[24], ResultTypeEnum.NG); //总结果

      if (result.HipotResult != ResultTypeEnum.OK && result.HipotResult != ResultTypeEnum._)
      {
         result.HipotResult = (testResult, resutl1) switch
         {
            var r when r.testResult != ResultTypeEnum.OK => r.testResult,
            var r when r.resutl1 != ResultTypeEnum.OK => r.resutl1,
            _ => result.HipotResult,
         };
      }

      if (bytes1.Length > 31) //取波形
      {
         var curBytes = bytes1[29..(bytes1.Length - 2)];
         result.CurvePoint = string.Join(',', curBytes.Select(x => x.ToString()));
      }
      else
      {
         $"未取到波形！".LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         result.CurvePoint = "";
      }
      return result;
   }

   ResultTypeEnum ParseResult(byte b, ResultTypeEnum ngResult)
   {
      return b switch
      {
         0x00 => ResultTypeEnum._,
         0x01 => ResultTypeEnum.OK,
         _ => ngResult,
      };
   }

   /// <summary>
   /// 解析脉冲结果
   /// </summary>
   /// <param name="b"></param>
   /// <returns></returns>
   ResultTypeEnum ParsePulseResult(byte[] bytes, int start)
   {
      return bytes switch
      {
         var bs when bs[start + 0] == 0xff => ResultTypeEnum.开路,
         var bs when bs[start + 1] == 0xff => ResultTypeEnum.严重短路,
         var bs when bs[start + 2] == 0xff => ResultTypeEnum.欠压,
         var bs when bs[start + 3] == 0xff => ResultTypeEnum.过压,
         var bs when bs[start + 4] == 0xff => ResultTypeEnum.VD1_NG,
         var bs when bs[start + 5] == 0xff => ResultTypeEnum.VD2_NG,
         var bs when bs[start + 6] == 0xff => ResultTypeEnum.VD3_NG,
         var bs when bs[start + 7] == 0xff => ResultTypeEnum.TL_NG,
         var bs when bs[start + 8] == 0xff => ResultTypeEnum.TH_NG,
         _ => ResultTypeEnum.OK,
      };
   }

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
