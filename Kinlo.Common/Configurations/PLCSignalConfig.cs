namespace Kinlo.Common.Configurations
{
  public class PLCSignalConfig : ConfigurationBase
  {
    public PLCSignalConfig(StyletIoC.IContainer container, bool isStartup)
      : base(container, isStartup) { }

    /// <summary>
    /// plc扫描服务
    /// </summary>
    public ObservableCollection<PLCScanSignalModel> PLCScanSignals { get; set; } = new();

    /// <summary>
    /// plc数据交互信号
    /// </summary>
    public ObservableCollection<PLCInteractAddressModel> PLCInteractAddresses { get; set; } = new();

    /// <summary>
    /// PLC报警地址
    /// </summary>
    public PLCAlarmAddressModel PLCAlarmAddresses { get; set; } = new();

    /// <summary>
    /// 自定义PLC交互地址
    /// </summary>
    public ObservableCollection<CustomPlcInteractAddressModel> CustomPlcInteractAddresses { get; set; } = new();

    public override void Load()
    {
      try
      {
        var _dic = FileHelper.LoadToDictionary(this.GetType().Name);
        if (_dic != null)
        {
          if (_dic.TryGetValue(nameof(PLCScanSignals), out object value) && value != null)
            PLCScanSignals = JsonSerializer.Deserialize<ObservableCollection<PLCScanSignalModel>>(value.ToString())!;
          if (_dic.TryGetValue(nameof(PLCInteractAddresses), out object value1) && value1 != null)
            PLCInteractAddresses = JsonSerializer.Deserialize<ObservableCollection<PLCInteractAddressModel>>(
              value1.ToString()
            )!;
          if (_dic.TryGetValue(nameof(PLCAlarmAddresses), out object value2) && value2 != null)
            PLCAlarmAddresses = JsonSerializer.Deserialize<PLCAlarmAddressModel>(value2.ToString())!;
          if (_dic.TryGetValue(nameof(CustomPlcInteractAddresses), out object value3) && value3 != null)
            CustomPlcInteractAddresses = JsonSerializer.Deserialize<
              ObservableCollection<CustomPlcInteractAddressModel>
            >(value3.ToString())!;
        }

        #region 旧项目兼容 行号列号，新项目将删除 行号及列号，改为在扩展属性内
        bool isNeedSave = false;
        ProcessTypeEnum[] legthItems = [ProcessTypeEnum.回氦, ProcessTypeEnum.回氦打钢珠];
        ProcessTypeEnum[] rowItems = [ProcessTypeEnum.注液, ProcessTypeEnum.测漏, ProcessTypeEnum.静置站金寨];
        ProcessTypeEnum[] colItems = [ProcessTypeEnum.注液, ProcessTypeEnum.测漏, ProcessTypeEnum.静置站金寨];

        foreach (var item in PLCInteractAddresses)
        {
          if (legthItems.Contains(item.ProcessesType))
          {
            if (!item.ExtensionProps.Any(x => x.Type == ExtensionType.长度1))
            {
              isNeedSave = true;
              item.ExtensionProps.Add(new ExtensionItem(ExtensionType.长度1, item.RowCount.ToString()));
            }
          }
          if (rowItems.Contains(item.ProcessesType))
          {
            if (!item.ExtensionProps.Any(x => x.Type == ExtensionType.行数))
            {
              isNeedSave = true;
              item.ExtensionProps.Add(new ExtensionItem(ExtensionType.行数, item.RowCount.ToString()));
            }
          }
          if (colItems.Contains(item.ProcessesType))
          {
            if (!item.ExtensionProps.Any(x => x.Type == ExtensionType.列数))
            {
              isNeedSave = true;
              item.ExtensionProps.Add(new ExtensionItem(ExtensionType.列数, item.ColumnCount.ToString()));
            }
          }
        }
        if (isNeedSave)
        {
          this.Save("系统自动保存", "旧项目兼容 行号列号", isPopup: false);
        }
        #endregion
      }
      catch (Exception ex)
      {
        $"[初始化PLCSignalConfig]异常：{ex}".LogRun(Log4NetLevelEnum.错误, true);
      }
    }

    public override void Save(
      string userName,
      string revise,
      bool isPopup = true,
      bool isPrintLog = true,
      string saveName = ""
    )
    {
      PLCInteractAddresses = PLCInteractAddresses
        .OrderBy(x => x.ProductionIndex)
        .ThenBy(x => x.StartCommand.Index)
        .ThenBy(x => x.DeviceStartIndex)
        .ToObservableCollection();
      base.Save(userName, revise, isPopup, isPrintLog, saveName);
    }

    /// <summary>
    /// 同步cmd索引
    /// </summary>
    public void SyncPlcCmdIndex()
    {
      foreach (var interact in PLCInteractAddresses)
      {
        var service = PLCScanSignals.FirstOrDefault(m => m.ServiceName == interact.ServiceName);
        if (service != null)
        {
          var add = service.StartSignas.FirstOrDefault(x => x.Tag.Lable == interact.StartCommand.Tag.Lable);
          if (add != null)
          {
            interact.StartCommand.Index = add.Index;
          }
        }
      }
    }
  }
}
