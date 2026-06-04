using HandyControl.Controls;

namespace Kinlo.GUI.ViewModel;

[Languages(["MES参数", "Parameter MES", "MES parameter"], IsScanProperty = false)]
[UIDisplayAttribute(true, 52, (ulong)(DefaultRoleEnum.设备 | DefaultRoleEnum.工艺), isRunEdit: true, "\xe608")]
public class ConfigurationMesParameterViewModel : Screen, IMenu
{
  public MesParameterConfig MesParameterCopy { get; set; }
  public DisplayDataCollection DisplayData { get; set; }
  public RoleConfig Role { get; set; }
  public int TabIndex { get; set; }
  private MesParameterConfig MesParameter;
  private UsersStatusConfig _usersStatus;
  private IContainer _container;

  public ConfigurationMesParameterViewModel(IContainer container)
  {
    _container = container;
    MesParameter = _container.Get<MesParameterConfig>();
    DisplayData = _container.Get<DisplayDataCollection>();
    _usersStatus = _container.Get<UsersStatusConfig>();
    Role = _container.Get<RoleConfig>();
    Init();
  }

  private void Init()
  {
    MesParameterCopy = new MesParameterConfig(_container, false);
    foreach (var item in MesParameter.DeviceStartupParameters)
    {
      MesParameterItemModel _mesParameterItem = new MesParameterItemModel();
      ExpressionAssignmentMapper<MesParameterItemModel, MesParameterItemModel>.Trans(item, _mesParameterItem);
      MesParameterCopy.DeviceStartupParameters.Add(_mesParameterItem);
    }
    foreach (var item in MesParameter.ResultParameters)
    {
      MesParameterItemModel _mesParameterItem = new MesParameterItemModel();
      ExpressionAssignmentMapper<MesParameterItemModel, MesParameterItemModel>.Trans(item, _mesParameterItem);
      MesParameterCopy.ResultParameters.Add(_mesParameterItem);
    }
  }

  public void RemoveResultParamCmd(MesParameterItemModel mesParameterItem) =>
    MesParameterCopy.ResultParameters.Remove(mesParameterItem);

  public void RemoveStartDeviceCmd(MesParameterItemModel mesParameterItem) =>
    MesParameterCopy.DeviceStartupParameters.Remove(mesParameterItem);

  public void AddReslutParamCmd(DisplayPropertyBindingDto processProperty)
  {
    try
    {
      var item = new MesParameterItemModel();
      item.LocalPropertyName = processProperty.BindingPaht;
      item.LanguagerKey = processProperty.Description;
      item.LocalType = processProperty?.PropertyType;
      item.IsSelected = true;
      UIThreadHelper.InvokeOnUiThreadAsync(() =>
      {
        MesParameterCopy.ResultParameters.Add(item);
      });
    }
    catch (Exception) { }
  }

  public void AddStartDeviceParamCmd(ControlInfoModel processProperty)
  {
    try
    {
      var item = new MesParameterItemModel();
      item.LocalPropertyName = processProperty.BindingOrKey;
      item.LanguagerKey = processProperty.DisplayName;
      item.LocalType = processProperty?.Type;
      item.IsSelected = true;
      UIThreadHelper.InvokeOnUiThreadAsync(() =>
      {
        MesParameterCopy.DeviceStartupParameters.Add(item);
      });
    }
    catch (Exception ex) { }
  }

  /// <summary>
  /// 替换
  /// </summary>
  /// <param name="mesParameterItem"></param>
  public void ReplaceReslutParamCmd(DisplayPropertyBindingDto mesParameterItem)
  {
    for (int i = 0; i < MesParameterCopy.ResultParameters.Count; i++)
    {
      var item = MesParameterCopy.ResultParameters[i];
      if (item.IsSelected)
      {
        item.LocalPropertyName = mesParameterItem.BindingPaht;
        item.LanguagerKey = mesParameterItem.Description;
        item.LocalType = mesParameterItem?.PropertyType;
        if (MesParameterCopy.ResultParameters.Count > i + 1)
          MesParameterCopy.ResultParameters[i + 1].IsSelected = true;
        return;
      }
    }
  }

  /// <summary>
  /// 替换
  /// </summary>
  /// <param name="mesParameterItem"></param>
  public void ReplaceStartupParamCmd(ControlInfoModel mesParameterItem)
  {
    for (int i = 0; i < MesParameterCopy.DeviceStartupParameters.Count; i++)
    {
      var item = MesParameterCopy.DeviceStartupParameters[i];
      if (item.IsSelected)
      {
        item.LocalPropertyName = mesParameterItem.BindingOrKey;
        item.LanguagerKey = mesParameterItem.DisplayName;
        item.LocalType = mesParameterItem?.Type;
        if (MesParameterCopy.DeviceStartupParameters.Count > i + 1)
          MesParameterCopy.DeviceStartupParameters[i + 1].IsSelected = true;
        return;
      }
    }
  }

  /// <summary>
  /// 对比
  /// </summary>
  /// <returns></returns>
  private string Compare()
  {
    StringBuilder contrastMsg = new();
    MesParameter.DeviceStartupParameters.ForEach(item =>
    {
      if (!MesParameterCopy.DeviceStartupParameters.Any(x => x.LocalPropertyName == item.LocalPropertyName))
      {
        contrastMsg.Append($"删除：属性[{item.LocalPropertyName}]，MES编号[{item.MesCode}];");
      }
    });
    foreach (var item in MesParameterCopy.DeviceStartupParameters)
    {
      var old = MesParameter.DeviceStartupParameters.FirstOrDefault(x => x.LocalPropertyName == item.LocalPropertyName);
      if (old == null)
      {
        contrastMsg.Append($"新增：属性[{item.LocalPropertyName}]，MES编号[{item.MesCode}];");
      }
      else
      {
        contrastMsg.Append(old.CompareObject(item, new Dictionary<string, DifferenceResultDto>()));
      }
    }
    MesParameter.ResultParameters.ForEach(item =>
    {
      if (string.IsNullOrEmpty(item.LocalPropertyName) || string.IsNullOrEmpty(item.MesCode))
      {
        return;
      }

      if (!MesParameterCopy.ResultParameters.Any(x => x.MesCode == item.MesCode))
      {
        contrastMsg.Append($"删除：属性[{item.LocalPropertyName}]，MES编号[{item.MesCode}];");
      }
    });
    foreach (var item in MesParameterCopy.ResultParameters)
    {
      if (string.IsNullOrEmpty(item.LocalPropertyName) || string.IsNullOrEmpty(item.MesCode))
        continue;
      var _old = MesParameter.ResultParameters.FirstOrDefault(x => x.MesCode == item.MesCode);
      if (_old == null)
      {
        contrastMsg.Append($"新增：属性[{item.LocalPropertyName}]，MES编号[{item.MesCode}];");
      }
      else
      {
        contrastMsg.Append(_old.CompareObject(item, new Dictionary<string, DifferenceResultDto>()));
      }
    }
    return contrastMsg.ToString();
  }

  public async Task SaveCMD()
  {
    // if (CheckParam())
    await Save(Compare());
  }

  private bool CheckParam()
  {
    if (
      MesParameterCopy.DeviceStartupParameters.Any(x =>
        x.IsEnable && (string.IsNullOrEmpty(x.LocalPropertyName) || string.IsNullOrEmpty(x.MesCode))
      )
    )
    {
      Growl.Warning("[开机参数] 在启用上传送MES时，本地属性、MES编码不能为空！");
      return false;
    }
    if (
      MesParameterCopy.ResultParameters.Any(x =>
        x.IsEnable && (string.IsNullOrEmpty(x.LocalPropertyName) || string.IsNullOrEmpty(x.MesCode))
      )
    )
    {
      Growl.Warning("[结果参数] 在启用上传送MES时，本地属性、MES编码不能为空！");
      return false;
    }
    return true;
  }

  private async Task Save(string contrastMsg)
  {
    await Task.Run(async () =>
    {
      if (!string.IsNullOrEmpty(contrastMsg))
      {
        await UIThreadHelper.InvokeOnUiThreadAsync(() =>
        {
          MesParameter.DeviceStartupParameters.Clear();
          MesParameter.ResultParameters.Clear();
          MesParameterCopy.DeviceStartupParameters = new ObservableCollection<MesParameterItemModel>(
            MesParameterCopy.DeviceStartupParameters.OrderByDescending(x => x.IsEnable)
          );
          MesParameterCopy.ResultParameters = new ObservableCollection<MesParameterItemModel>(
            MesParameterCopy.ResultParameters.OrderByDescending(x => x.IsEnable)
          );

          foreach (var item in MesParameterCopy.DeviceStartupParameters)
          {
            MesParameterItemModel mesParameterItem = new MesParameterItemModel();
            ExpressionAssignmentMapper<MesParameterItemModel, MesParameterItemModel>.Trans(item, mesParameterItem);
            MesParameter.DeviceStartupParameters.Add(mesParameterItem);
          }
          foreach (var item in MesParameterCopy.ResultParameters)
          {
            MesParameterItemModel mesParameterItem = new MesParameterItemModel();
            ExpressionAssignmentMapper<MesParameterItemModel, MesParameterItemModel>.Trans(item, mesParameterItem);
            mesParameterItem.ValueConverter = MesParameter.GetMesValueConverter(mesParameterItem.ConverterName);
            MesParameter.ResultParameters.Add(mesParameterItem);
          }
        });
        MesParameter.Save(_usersStatus.LocalLoggedinUser.Account, contrastMsg);
      }
      else
      {
        Growl.Success($"文件未修改！");
      }
      return true;
    });
  }

  public void ImportExcelCmd()
  {
    var dialog = new OpenFileDialog
    {
      Title = "请选择文件",
      Filter = "Excel 文件|*.xlsx;*.xls", // 比如 "Excel 文件|*.xlsx;*.xls|所有文件|*.*"
      Multiselect = false, // 是否允许多选
    };

    bool? result = dialog.ShowDialog();
    if (result == true)
    {
      var lists = ExcelHelper.ImproExcel<MesParameterItemModel>(dialog.FileName);
      foreach (var item in lists)
      {
        if (!string.IsNullOrEmpty(item.MesCode))
        {
          if (TabIndex == 0)
          {
            if (!MesParameterCopy.ResultParameters.Any(x => x.MesCode == item.MesCode))
            {
              MesParameterCopy.ResultParameters.Add(item);
            }
          }
          else
          {
            if (!MesParameterCopy.DeviceStartupParameters.Any(x => x.MesCode == item.MesCode))
            {
              MesParameterCopy.DeviceStartupParameters.Add(item);
            }
          }
        }
      }
    }
  }

  public void ExportExcelCmd()
  {
    SaveFileDialog dlg = new SaveFileDialog();
    dlg.Filter = "Excel 文件|*.xlsx;*.xls";
    dlg.FileName = DateTime.Now.ToString("文件名_yyyy-MM-dd HH点mm分ss秒");
    if (dlg.ShowDialog() == true)
    {
      var data =
        TabIndex == 0 ? MesParameterCopy.ResultParameters.ToList() : MesParameterCopy.DeviceStartupParameters.ToList();
      data.ExportExcel(dlg.FileName, true);
    }
  }

  public void ExpandCmd()
  {
    if (TabIndex == 0)
    {
      foreach (var item in DisplayData.AvailableResultParameters)
      {
        item.IsSelected = true;
      }
    }
  }

  public void FoldCmd()
  {
    if (TabIndex == 0)
    {
      foreach (var item in DisplayData.AvailableResultParameters)
      {
        item.IsSelected = false;
      }
    }
  }

  /// <summary>
  /// 相似度阈值
  /// </summary>
  public double Threshold { get; set; } = 0.25;
  public int DisplayCount { get; set; } = 6;

  public void OpenRecommendedCmd()
  {
    if (this.View is ConfigurationMesParameterView view)
    {
      var col = view.ResultParamDataGrid.Columns.FirstOrDefault(x => x.Header is string s && s == "本地推荐属性");
      if (col != null)
        col.Visibility = Visibility.Visible;
    }
    if (TabIndex == 0)
    {
      foreach (var item in MesParameterCopy.ResultParameters)
      {
        var lists = new List<FieldMatchResult>();
        foreach (var locals in DisplayData.AvailableResultParameters)
        {
          var list = FuzzyMatcherUniversal.Match(
            item.MesName,
            locals.OriginalClassProperties.Select(x => (x.Description, (object)x.BindingPaht)).ToList(),
            topN: DisplayCount,
            threshold: Threshold
          );
          if (list.Count > 0)
            lists.AddRange(list);
        }
        item.Candidates.Clear();
        item.Candidates.AddRange(
          lists
            .OrderByDescending(x => x.Score)
            .Select(x => new CandidateItem(x.LocalField, (string)x.Tag))
            .Take(DisplayCount)
        );
      }
    }
  }

  public void ClearRecommendedCmd()
  {
    if (this.View is ConfigurationMesParameterView view)
    {
      var col = view.ResultParamDataGrid.Columns.FirstOrDefault(x => x.Header is string s && s == "本地推荐属性");
      if (col != null)
        col.Visibility = Visibility.Collapsed;
    }
    if (TabIndex == 0)
    {
      foreach (var item in MesParameterCopy.ResultParameters)
      {
        item.Candidates.Clear();
      }
    }
  }

  public void SelectedCmd(CandidateItem candidate)
  {
    var p = MesParameterCopy.ResultParameters.FirstOrDefault(x => x.IsSelected);
    if (p != null)
    {
      p.LanguagerKey = candidate.Description;
      p.LocalPropertyName = candidate.PropertyName;
    }
  }

  /// <summary>
  /// 一键应用最相似属性
  /// </summary>
  public void UseHighestSimilarityCmd()
  {
    foreach (var item in MesParameterCopy.ResultParameters)
    {
      if (
        string.IsNullOrEmpty(item.LocalPropertyName)
        && item.Candidates.Count > 0
        && !string.IsNullOrEmpty(item.Candidates[0].PropertyName)
      )
      {
        item.LocalPropertyName = item.Candidates[0].PropertyName;
        item.LanguagerKey = item.Candidates[0].Description;
      }
    }
  }

  public void ClearLocalCmd()
  {
    var p = MesParameterCopy.ResultParameters.FirstOrDefault(x => x.IsSelected);
    if (p != null)
    {
      p.LocalPropertyName = p.LanguagerKey = string.Empty;
    }
  }

  public void Load() { }

  public bool Unload()
  {
    try
    {
      var msg = Compare();
      if (!string.IsNullOrEmpty(msg))
      {
        var rs = System.Windows.MessageBox.Show("有修改未保存，是否保存？", "提示", MessageBoxButton.YesNoCancel);
        if (rs == MessageBoxResult.Yes)
        {
          //if (!CheckParam())
          //    return false;

          _ = Save(msg);
        }
        else if (rs == MessageBoxResult.No)
        {
          UIThreadHelper.InvokeOnUiThreadAsync(() => Init());
          return true;
        }
        else
        {
          return false;
        }
      }
      return true;
    }
    catch (Exception ex)
    {
      $"{ex}".LogRun();
      return false;
    }
  }
}
