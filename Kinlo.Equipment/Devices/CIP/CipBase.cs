namespace Kinlo.Equipment.Devices.CIP;

public abstract class CipBase : IPLC
{
   #region Properties
   /// <summary>
   /// 扫描线程专用连接
   /// </summary>
   protected CipClient? ScanConnected = null;

   /// <summary>
   /// PLC队列（无连接）
   /// </summary>
   protected readonly BlockingCollection<CipClient> Unconnected = new();

   /// <summary>
   /// PLC队列（有连接）
   /// </summary>
   protected readonly BlockingCollection<CipClient> Connected = new();
   public DeviceInfoModel DeviceInfo { get; set; }
   #endregion
   public CipBase(DeviceInfoModel info) => DeviceInfo = info;

   protected bool IsShutdown => DeviceInfo.TaskToken == null || DeviceInfo.TaskToken.IsCancellationRequested;

   public abstract bool Open();
   public abstract void Close();

   public virtual OperationResult<TClass> Scan<TClass>(
      SignalAddressModel address,
      TClass obj,
      string logHeader,
      DeviceOperationOptions? options = null
   )
      where TClass : class, new()
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
         try
         {
            logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
            obj ??= Activator.CreateInstance<TClass>();
            var lableBytes = ParseCipLable.LableReadRequest(address.Lable, address.Length);
            var res = ScanConnected.Conn.WriteAndRead(lableBytes, ScanConnected.Protocol, logHeader);
            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!ScanConnected.RepairCip())
                  return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
               continue;
            }

            var bytes = res.Data!;
            ScanConnected.OnWatchdogFed();
            int boolSize = 0;
            StructToBytes.FromBytes(obj, bytes.Skip(4).ToArray(), ref boolSize, 0, DeviceInfo.Communication);
            return OperationResult<TClass>.Success(obj);
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }
      return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
   }

   public virtual OperationResult<TClass> ReadClass<TClass>(
      SignalAddressModel address,
      TClass obj,
      string logHeader,
      DeviceOperationOptions? options = null
   )
      where TClass : class, new()
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
         try
         {
            logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
            obj ??= Activator.CreateInstance<TClass>();
            var lableBytes = ParseCipLable.LableReadRequest(address.Lable, address.Length);
            var plc = Unconnected.Take();
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);

               Unconnected.Add(plc);
               continue;
            }

            var bytes = res.Data!;
            plc.OnWatchdogFed();
            Unconnected.Add(plc);
            int boolSize = 0;

            StructToBytes.FromBytes(obj, bytes.Skip(4).ToArray(), ref boolSize, 0, DeviceInfo.Communication);
            return OperationResult<TClass>.Success(obj);
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }
      return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
   }

   public virtual OperationResult<TValue> ReadValue<TValue>(
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   ) => ReadValueCore<TValue>(Unconnected, address, logHeader, options);

   public OperationResult<TValue> ReadValueCore<TValue>(
      BlockingCollection<CipClient> connQueue,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return OperationResult<TValue>.Failure(ResultTypeEnum.NG, errMsg);
         try
         {
            logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
            Type type = typeof(TValue);
            var lableBytes = ParseCipLable.LableReadRequest(address.Lable, address.Length);
            var plc = connQueue.Take();
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return OperationResult<TValue>.Failure(ResultTypeEnum.NG, errMsg);

               connQueue.Add(plc);
               continue;
            }

            var bytes = res.Data!;
            plc.OnWatchdogFed();
            connQueue.Add(plc);
            var result = bytes[0] switch
            {
               0xD0 => new Func<TValue>(() =>
               {
                  var rsBytes = bytes.Skip(4).Take(bytes[2]).ToArray();
                  object str = Encoding.ASCII.GetString(rsBytes);
                  return (TValue)str;
               })(),
               _ => (TValue)StructToBytes.GetValue(type, bytes.Skip(2).ToArray(), 0, DeviceInfo.Communication),
            };
            return OperationResult<TValue>.Success(result);
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return OperationResult<TValue>.Failure(ResultTypeEnum.NG, errMsg);
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }
      return OperationResult<TValue>.Failure(ResultTypeEnum.NG, errMsg);
   }

   public virtual OperationResult<List<TValue>> ReadValues<TValue>(
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   ) => ReadValuesCore<TValue>(Unconnected, address, logHeader, options);

   public OperationResult<List<TValue>> ReadValuesCore<TValue>(
      BlockingCollection<CipClient> connQueue,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return OperationResult<List<TValue>>.Failure(ResultTypeEnum.NG, errMsg);
         logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);

         try
         {
            List<TValue> obj = new List<TValue>();
            var lableBytes = ParseCipLable.LableReadRequest(address.Lable, address.Length);
            var plc = connQueue.Take();
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return OperationResult<List<TValue>>.Failure(ResultTypeEnum.NG, errMsg);
               connQueue.Add(plc);
               continue;
            }

            var bytes = res.Data!;
            plc.OnWatchdogFed();
            connQueue.Add(plc);
            int stat = 2;
            while (stat < bytes.Length)
            {
               switch (typeof(TValue).Name)
               {
                  case "String":
                     int strLen = bytes[stat] + (bytes[stat + 1] << 8);
                     string _strValue = Encoding.ASCII.GetString(bytes.Skip(stat + 2).Take(strLen).ToArray());
                     obj.Add((TValue)(object)_strValue);
                     stat = stat + strLen + 2;
                     break;
                  default:
                     var info = CIPDataInfoHelper.CIPDataInfos.First(x => x.PropertyName == typeof(TValue).Name);
                     if (info == null)
                        obj.Add(default);
                     obj.Add(
                        (TValue)
                           StructToBytes.GetValue(
                              info.DataType,
                              bytes.Skip(stat).ToArray(),
                              0,
                              DeviceInfo.Communication
                           )
                     );
                     stat += info.Length;
                     break;
               }
            }
            return OperationResult<List<TValue>>.Success(obj);
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return OperationResult<List<TValue>>.Failure(ResultTypeEnum.NG, errMsg);
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }
      return OperationResult<List<TValue>>.Failure(ResultTypeEnum.NG, errMsg);
   }

   public virtual OperationResult<List<object>> ReadObjects(
      SignalAddressModel[] addresses,
      string logHeader,
      DeviceOperationOptions? options = null
   ) => ReadObjectsCore(Unconnected, addresses, logHeader, options);

   public OperationResult<List<object>> ReadObjectsCore(
      BlockingCollection<CipClient> connQueue,
      SignalAddressModel[] addresses,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return OperationResult<List<object>>.Failure(ResultTypeEnum.NG, errMsg);

         logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, addresses);
         List<object> _obj = new List<object>();
         try
         {
            var lableBytes = ParseCipLable.MultipleLableReadRequest(addresses);
            var plc = connQueue.Take();
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return OperationResult<List<object>>.Failure(ResultTypeEnum.NG, errMsg);
               connQueue.Add(plc);
               continue;
            }
            var bytes = res.Data!;
            plc.OnWatchdogFed();
            connQueue.Add(plc);
            int label_num = bytes[0] + (bytes[1] << 8);
            int stat = (label_num * 2 + 2);

            while (stat < bytes.Length)
            {
               if (bytes[stat] == 0xCC)
               {
                  stat += 4;
               }
               stat += 2;
               switch (bytes[stat - 2])
               {
                  case 0xD0:
                     int _strLen = bytes[stat] + (bytes[stat + 1] << 8);
                     string _strValue = Encoding.ASCII.GetString(bytes.Skip(stat + 2).Take(_strLen).ToArray());
                     _obj.Add(_strValue);
                     stat = stat + _strLen + 2;
                     break;
                  default:
                     var _info = CIPDataInfoHelper.CIPDataInfos.First(x => x.PropertyByre == bytes[stat - 2]);
                     if (_info == null)
                        _obj.Add(null);
                     _obj.Add(
                        StructToBytes.GetValue(_info.DataType, bytes.Skip(stat).ToArray(), 0, DeviceInfo.Communication)
                     );
                     stat += _info.Length;
                     break;
               }
            }
            return OperationResult<List<object>>.Success(_obj);
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return OperationResult<List<object>>.Failure(ResultTypeEnum.NG, errMsg);
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }
      return OperationResult<List<object>>.Failure(ResultTypeEnum.NG, errMsg);
   }

   public virtual OperationResult<List<TClass>> ReadClasses<TClass>(
      SignalAddressModel[] addresses,
      string logHeader,
      DeviceOperationOptions? options = null
   )
      where TClass : class, new() => ReadClassesCore<TClass>(Unconnected, addresses, logHeader, options);

   public OperationResult<List<TClass>> ReadClassesCore<TClass>(
      BlockingCollection<CipClient> connQueue,
      SignalAddressModel[] addresses,
      string logHeader,
      DeviceOperationOptions? options = null
   )
      where TClass : class, new()
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return OperationResult<List<TClass>>.Failure(ResultTypeEnum.NG, errMsg);
         logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, addresses);
         try
         {
            var lableBytes = ParseCipLable.MultipleLableReadRequest(addresses);
            var plc = connQueue.Take();
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return OperationResult<List<TClass>>.Failure(ResultTypeEnum.NG, errMsg);
               connQueue.Add(plc);
               continue;
            }

            var bytes = res.Data!;
            plc.OnWatchdogFed();
            connQueue.Add(plc);
            int label_num = bytes[0] + (bytes[1] << 8);
            int stat = (label_num * 2 + 2);
            int size = 0;
            List<TClass> datas = new List<TClass>();
            while (stat < bytes.Length)
            {
               if (bytes[stat] == 0xCC)
               {
                  stat += 8;
               }
               var obj = Activator.CreateInstance<TClass>();
               double sizt_count = StructToBytes.FromBytes(
                  obj,
                  bytes.Skip(stat).ToArray(),
                  ref size,
                  0,
                  DeviceInfo.Communication
               );
               datas.Add(obj);
               stat += (int)sizt_count;
            }
            return OperationResult<List<TClass>>.Success(datas);
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return OperationResult<List<TClass>>.Failure(ResultTypeEnum.NG, errMsg);
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }
      return OperationResult<List<TClass>>.Failure(ResultTypeEnum.NG, errMsg);
   }

   #region Class3-有连接

   /// <summary>
   /// CIP协议为有连接读取超大类（最大支持 1996 byte）
   /// </summary>
   /// <typeparam name="TClass"></typeparam>
   /// <param name="address"></param>
   /// <param name="obj"></param>
   /// <param name="options"></param>
   /// <returns></returns>
   public virtual OperationResult<TClass> ReadLargeClass<TClass>(
      SignalAddressModel address,
      TClass obj,
      string logHeader,
      DeviceOperationOptions? options = null
   )
      where TClass : class, new()
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
         logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
         try
         {
            obj ??= Activator.CreateInstance<TClass>();
            var lableBytes = ParseCipLable.LableReadRequest(address.Lable, address.Length);
            var plc = Connected.Take();
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader, 2048);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
               Connected.Add(plc);
               continue;
            }

            var bytes = res.Data!;
            plc.OnWatchdogFed();
            Connected.Add(plc);
            int boolSize = 0;

            StructToBytes.FromBytes(obj, bytes.Skip(4).ToArray(), ref boolSize, 0, DeviceInfo.Communication);
            return OperationResult<TClass>.Success(obj);
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }
      return OperationResult<TClass>.Failure(ResultTypeEnum.NG, errMsg);
   }

   /// <summary>
   /// 有连接读取超大数据（最大支持 1996 byte）
   /// </summary>
   /// <typeparam name="T"></typeparam>
   /// <param name="address"></param>
   /// <param name="options"></param>
   /// <returns></returns>
   public virtual OperationResult<List<T>> ReadLargeObjects<T>(
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int retry = 0; retry < options.RetryCount; retry++)
      {
         if (IsShutdown)
            return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, errMsg);
         logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
         try
         {
            Type type = typeof(T);
            if (type.Name == "String")
            {
               return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, "协议不支持字符串数组!!!");
            }
            var lableBytes = ParseCipLable.LableReadRequest(address.Lable, address.Length);
            var plc = Connected.Take();
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader, 2048);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, errMsg);
               Connected.Add(plc);
               continue;
            }

            var byteList = res.Data!;
            plc.OnWatchdogFed();
            Connected.Add(plc);
            int size = 0;
            List<T> datas = new List<T>();
            int stat = 4;
            if (type.BaseType.Name == "ValueType")
            {
               stat = 2;
               if (byteList[0] == 0xc1)
               {
                  var bs = byteList.Skip(2).ToArray();
                  foreach (var item in bs)
                  {
                     for (int i = 0; i < 8; i++)
                     {
                        var b = (item >> i) & 1;
                        bool bb = b == 1;
                        if (bb is T b3)
                           datas.Add(b3);
                     }
                  }
               }
               else
               {
                  while (stat < byteList.Length)
                  {
                     var _info = CIPDataInfoHelper.CIPDataInfos.First(x => x.DataType == type);
                     datas.Add(
                        (T)StructToBytes.GetValue(type, byteList.Skip(stat).ToArray(), 0, DeviceInfo.Communication)
                     );
                     stat += _info.Length;
                  }
               }
            }
            else
            {
               while (stat < byteList.Length)
               {
                  var obj = Activator.CreateInstance<T>();
                  double sizt_count = StructToBytes.FromBytes(
                     obj,
                     byteList.Skip(stat).ToArray(),
                     ref size,
                     0,
                     DeviceInfo.Communication
                  );
                  datas.Add(obj);
                  stat += (int)sizt_count;
               }
            }
            return OperationResult<List<T>>.Success(datas);
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, errMsg);
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }
      return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, errMsg);
   }

   public virtual bool WriteLargeClass<TClass>(
      TClass value,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
      where TClass : class
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return false;
         try
         {
            logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
            var plc = Connected.Take();
            var lableBytes = ParseCipLable.LableWriteClassRequest(
               address.Lable,
               value,
               DeviceInfo.Communication,
               plc,
               logHeader
            );
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return false;

               Connected.Add(plc);
               continue;
            }

            plc.OnWatchdogFed();
            Connected.Add(plc);
            if (res.State == CommState.Success)
               return true;
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return false;
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }

      return false;
   }
   #endregion
   public virtual bool WriteClass<TClass>(
      TClass value,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
      where TClass : class => WriteClassCore(Unconnected, value, address, logHeader, options);

   public bool WriteClassCore<TClass>(
      BlockingCollection<CipClient> connQueue,
      TClass value,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
      where TClass : class
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return false;
         try
         {
            logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
            var plc = connQueue.Take();
            var lableBytes = ParseCipLable.LableWriteClassRequest(
               address.Lable,
               value,
               DeviceInfo.Communication,
               plc,
               logHeader
            );
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return false;
               connQueue.Add(plc);
               continue;
            }

            plc.OnWatchdogFed();
            connQueue.Add(plc);
            if (res.State == CommState.Success)
               return true;
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return false;
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }

      return false;
   }

   public virtual bool WriteValue(
      object value,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   ) => WriteValueCore(Unconnected, value, address, logHeader, options);

   public bool WriteValueCore(
      BlockingCollection<CipClient> connQueue,
      object value,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      options ??= new DeviceOperationOptions();
      string errMsg = string.Empty;
      for (int i = 0; i < options.RetryCount; i++)
      {
         if (IsShutdown)
            return false;
         try
         {
            logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
            var lableBytes = ParseCipLable.LableWriteValueRequest(address.Lable, value, DeviceInfo.Communication);
            CipClient plc = connQueue.Take();
            var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader);

            errMsg = res.Message;
            if (res.State == CommState.Failed)
               continue;
            else if (res.State == CommState.NeedReconnect)
            {
               if (!plc.RepairCip())
                  return false;
               connQueue.Add(plc);
               continue;
            }

            plc.OnWatchdogFed();
            connQueue.Add(plc);
            if (res.State == CommState.Success)
               return true;
         }
         catch (Exception ex)
         {
            if (IsShutdown)
               return false;
            errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
            errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
         }
      }
      return false;
   }

   public bool WriteLargeValues<TValue>(
      List<TValue> values,
      SignalAddressModel address,
      string logHeader,
      DeviceOperationOptions? options = null
   )
   {
      throw new NotImplementedException();
   }
}
