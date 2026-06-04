using System.Formats.Asn1;

namespace Kinlo.Equipment.Devices.CIP;

/// <summary>
/// 轻量级CIP连接，适用于低端PLC
/// </summary>
[DeviceConnec([CommunicationEnum.CipOrmonPlcLight])]
public class OmronCIipLight : CipBase
{
  public OmronCIipLight(DeviceInfoModel info)
    : base(info) { }

  public override bool Open()
  {
    Close();
    string logHeader = DeviceInfo.ToDeviceLogHeader();
    var plcConnect = this.BuildCip(CipMode.无连接模式, 1, logHeader);
    if (plcConnect == null)
    {
      Close();
      return false;
    }

    Unconnected.Add(plcConnect);
    return true;
  }

  public override void Close()
  {
    string logHeader = DeviceInfo.ToDeviceLogHeader();
    try
    {
      while (Unconnected.TryTake(out var plcConn))
      {
        plcConn.Close(logHeader);
      }
    }
    catch (Exception ex)
    {
      $"关闭异常：{ex}".LogProcess(logHeader, Log4NetLevelEnum.错误);
    }
  }

  public override OperationResult<TClass> Scan<TClass>(
    SignalAddressModel address,
    TClass obj,
    string logHeader,
    DeviceOperationOptions? options = null
  )
    where TClass : class => base.ReadClass(address, obj, logHeader, options);

  #region Class3-在Light模式下因为只一个连接，所以要转换连接模式
  /// <summary>
  ///  有连接读取（最大支持 1996 byte）
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="address"></param>
  /// <param name="obj"></param>
  /// <param name="options"></param>
  /// <returns></returns>
  public override OperationResult<T> ReadLargeClass<T>(
    SignalAddressModel address,
    T obj,
    string logHeader,
    DeviceOperationOptions? options = null
  )
  {
    options ??= new DeviceOperationOptions();
    string errMsg = string.Empty;
    for (int retry = 0; retry < options.RetryCount; retry++)
    {
      if (IsShutdown)
        return OperationResult<T>.Failure(ResultTypeEnum.NG, errMsg);

      logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
      try
      {
        options ??= new DeviceOperationOptions();
        obj = Activator.CreateInstance<T>();
        var lableBytes = ParseCipLable.LableReadRequest(address.Lable, address.Length);

        var plc = Unconnected.Take();
        var result = CipConnectionManager.ExplicitClass3ForwardOpen(plc.Conn, plc.Session, logHeader);
        plc.ConnectionId = result.connectionId;
        plc.SetForwardContext(result.context);
        plc.Protocol = new OmronCipExplicitTcpProtocol(plc.Session, plc.ConnectionId);
        plc.ConnectMode = CipMode.有连接模式_每次重连;

        var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, logHeader, 2048);

        CipConnectionManager.ExplicitClass3ForwardClose(plc.Conn, plc.Session, plc.ForwardContext, logHeader);
        CipConnectionManager.CloseCipConnect(plc.Conn, plc.Session, logHeader);
        plc.Protocol = new OmronCipUcmmTcpProtocol(0, plc.Session);
        plc.ConnectMode = CipMode.无连接模式;

        errMsg = res.Message;
        if (res.State == CommState.Failed)
          continue;
        else if (res.State == CommState.NeedReconnect)
        {
          var mode = plc.ConnectMode;
          var index = plc.Index;
          plc.Close(logHeader);
          plc = this.BuildCip(mode, index, logHeader);
          if (plc == null) //重连失败
          {
            $"注意：重连失败！".LogProcess(logHeader);
            return OperationResult<T>.Failure(ResultTypeEnum.NG, errMsg);
          }
          Unconnected.Add(plc);
          continue;
        }

        var bytes = res.Data!;

        plc.OnWatchdogFed();
        Unconnected.Add(plc);

        int boolSize = 0;

        StructToBytes.FromBytes(obj, bytes.Skip(4).ToArray(), ref boolSize, 0, DeviceInfo.Communication);
        return OperationResult<T>.Success(obj);
      }
      catch (Exception ex)
      {
        if (IsShutdown)
          return OperationResult<T>.Failure(ResultTypeEnum.NG, errMsg);
        errMsg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
        errMsg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
    }
    return OperationResult<T>.Failure(ResultTypeEnum.NG, errMsg);
  }

  /// <summary>
  ///  有连接读取（最大支持 1996 byte）
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="address"></param>
  /// <param name="options"></param>
  /// <returns></returns>
  public override OperationResult<List<T>> ReadLargeObjects<T>(
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
        return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, errMsg);
      logHeader = DeviceInfo.SplitDeviceLogHeader(logHeader, address);
      try
      {
        options ??= new DeviceOperationOptions();
        Type type = typeof(T);
        if (type.Name == "String")
        {
          return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, "协议不支持字符串数组!!!");
        }

        var lableBytes = ParseCipLable.LableReadRequest(address.Lable, address.Length);
        var plc = Unconnected.Take();
        var result = CipConnectionManager.ExplicitClass3ForwardOpen(plc.Conn, plc.Session, logHeader);
        plc.ConnectionId = result.connectionId;
        plc.SetForwardContext(result.context);
        plc.Protocol = new OmronCipExplicitTcpProtocol(plc.Session, plc.ConnectionId);
        plc.ConnectMode = CipMode.有连接模式_每次重连;

        var res = plc.Conn.WriteAndRead(lableBytes, plc.Protocol, address.Lable, 2048);
        CipConnectionManager.ExplicitClass3ForwardClose(plc.Conn, plc.Session, plc.ForwardContext, logHeader);
        CipConnectionManager.CloseCipConnect(plc.Conn, plc.Session, logHeader);
        plc.Protocol = new OmronCipUcmmTcpProtocol(0, plc.Session);
        plc.ConnectMode = CipMode.无连接模式;

        errMsg = res.Message;
        if (res.State == CommState.Failed)
          continue;
        else if (res.State == CommState.NeedReconnect)
        {
          var mode = plc.ConnectMode;
          var index = plc.Index;
          plc.Close(logHeader);
          plc = this.BuildCip(mode, index, logHeader);
          if (plc == null) //重连失败
          {
            $"注意：重连失败！".LogProcess(logHeader);
            return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, errMsg);
          }
          Unconnected.Add(plc);
          continue;
        }
        var byteList = res.Data!;

        plc.OnWatchdogFed();
        Unconnected.Add(plc);

        int size = 0;
        List<T> datas = new List<T>();
        int stat = 4;
        if (type.BaseType.Name == "ValueType")
        {
          while (stat < byteList.Length)
          {
            switch (byteList[0])
            {
              case 0xC3:
                datas.Add((T)StructToBytes.GetValue(type, byteList.Skip(stat).ToArray(), 0, DeviceInfo.Communication));
                stat += 2;
                break;
            }
            datas.Add((T)StructToBytes.GetValue(type, byteList.Skip(stat).ToArray(), 0, DeviceInfo.Communication));
            switch (byteList[0])
            {
              case 0xC4:
              case 0xC8:
              case 0xCA:
                stat += 4;
                break;
              case 0xC5:
              case 0xC9:
              case 0xCB:
                stat += 8;
                break;
              case 0xC7:
                stat += 2;
                break;
            }
          }
        }
        else
        {
          try
          {
            while (stat < byteList.Length)
            {
              if (stat == 504) { }
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
              //if (!isConnection)
              //{
              //    stat += 4;
              //}
            }
          }
          catch (Exception ex)
          {
            ex.ToString().LogProcess(logHeader, Log4NetLevelEnum.错误);
          }
        }
        return OperationResult<List<T>>.Success(datas);
      }
      catch (Exception ex)
      {
        if (IsShutdown)
          return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, errMsg);
        var msg = $"[{Helper.GetCurrentMethodName()}]异常：{ex};";
        msg.LogProcess(logHeader, Log4NetLevelEnum.错误, true);
      }
    }
    return OperationResult<List<T>>.Failure(ResultTypeEnum.NG, errMsg);
  }

  #endregion
}
