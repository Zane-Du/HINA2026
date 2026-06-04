using System.Windows.Data;
using HandyControl.Controls;
using Kinlo.Services.PeriodicTasks;

namespace Kinlo.GUI.ViewModel;

[UIDisplayAttribute(true)]
public class ProductionHistoryViewModel : Screen
{
   #region propertys
   /// <summary>
   /// 模糊查询
   /// </summary>
   public bool IsFuzzyQuery { get; set; }
   public QueryTimeType SelectTimeType { get; set; } = QueryTimeType.按进站时间;
   public List<QueryTimeType> QueryTimeTypes { get; set; } =
      Enum.GetValues(typeof(QueryTimeType)).OfType<QueryTimeType>().ToList();

   /// <summary>
   /// 数据总量
   /// </summary>
   public int TotalCount { get; set; }

   /// <summary>
   /// 总页数
   /// </summary>
   public int TotalPage { get; set; }

   /// <summary>
   /// 选中的页面索引
   /// </summary>
   public int PageIndex { get; set; } = 1;

   private int _dataCountPerPage = 25;

   /// <summary>
   /// 每页数量
   /// </summary>
   public int DataCountPerPage
   {
      get { return _dataCountPerPage; }
      set
      {
         if (_dataCountPerPage != value)
         {
            _dataCountPerPage = value;
            if (DataList != null) //如果有数据 即重新查询
            {
               QueryCMD();
            }
         }
      }
   }

   /// <summary>
   /// 开始时间
   /// </summary>
   public DateTime StartTime { get; set; }

   /// <summary>
   /// 结束时间
   /// </summary>
   public DateTime EndTime { get; set; }

   private string _barcode;

   /// <summary>
   /// 查询条码
   /// </summary>
   public string Barcode
   {
      get { return _barcode; }
      set
      {
         if (_barcode != value)
         {
            List<string> lines = new List<string>();

            using (StringReader reader = new StringReader(value))
            {
               string? line;
               while ((line = reader.ReadLine()) != null)
               {
                  if (!string.IsNullOrWhiteSpace(line)) // 跳过空行和只含空白的行
                  {
                     lines.Add(line.Trim());
                  }
               }
            }
            _barcode = string.Join(',', lines);
         }
      }
   }

   /// <summary>
   /// 去重
   /// </summary>
   public bool IsNotRepeat { get; set; } = false;

   public QueryBatteryTypeEnum QueryBatteryType { get; set; } = QueryBatteryTypeEnum.全部;
   public IEnumerable<QueryBatteryTypeEnum> QueryBatteryTypes
   {
      get
      {
         foreach (QueryBatteryTypeEnum type in Enum.GetValues(typeof(QueryBatteryTypeEnum)))
         {
            yield return type;
         }
      }
   }
   public QueryBatteryMESStatusEnum MesStatus { get; set; } = QueryBatteryMESStatusEnum.全部;
   public IEnumerable<QueryBatteryMESStatusEnum> MesStatuses
   {
      get
      {
         foreach (QueryBatteryMESStatusEnum type in Enum.GetValues(typeof(QueryBatteryMESStatusEnum)))
         {
            yield return type;
         }
      }
   }

   public ObservableCollection<QueryParameterModel> QueryParameters { get; set; }
   public EntityPropertyVisibleViewModel EntityPropertyVisibleVM { get; set; }
   public List<ExpandoObject> DataList { get; set; } = new();

   /// <summary>
   /// 显示数据的View
   /// </summary>
   public System.ComponentModel.ICollectionView? DisplayView { get; private set; }
   public object ShowGridData { get; set; }

   [Inject]
   public IWindowManager _windowManager { get; set; }
   #endregion
   #region field
   private Dialog _dialog;
   private OtherParameterConfig _otherParameter;
   private DisplayDataCollection _displayData;
   private IContainer _container;
   private DbHelper _sugarDB;
   private ParameterConfig _parameterConfig;
   private MesInterfaceParameterConfig _mesInterfaceParameterConfig;
   private IBatteryCache _batteryCache;
   private MesService _mesService;
   #endregion
   public ProductionHistoryViewModel(IContainer container)
   {
      _container = container;
      EndTime = DateTime.Now;
      StartTime = EndTime.AddDays(-1);
      EntityPropertyVisibleVM = container.Get<EntityPropertyVisibleViewModel>();
      _otherParameter = container.Get<OtherParameterConfig>();
      _mesInterfaceParameterConfig = container.Get<MesInterfaceParameterConfig>();
      _sugarDB = container.Get<DbHelper>();
      _displayData = container.Get<DisplayDataCollection>();
      QueryParameters = new ObservableCollection<QueryParameterModel>();
      _parameterConfig = container.Get<ParameterConfig>();
      _batteryCache = container.Get<IBatteryCache>();
      _mesService = container.Get<MesService>();
      CreateBatteryDataGrid();
   }

   /// <summary>
   /// 调整列
   /// </summary>
   /// <param name="processesType"></param>
   public void AdjustCmd()
   {
      _displayData.CompleteBatteryDatas.PropertyBindings.ForEach(propertyBinding =>
      {
         propertyBinding.IsSelected = false;
      });
      EntityPropertyVisibleVM.DisplayData = _displayData.CompleteBatteryDatas;
   }

   public void CreateBatteryDataGrid()
   {
      bool isGridData = false; //如果想切换可以把此选项加入设置，DataGrid相比ListView 性能太差
      if (isGridData)
         ShowGridData = CreateControlHelper.CreateDataGrid(
            $"{nameof(DisplayView)}",
            _displayData.CompleteBatteryDatas.PropertyBindings,
            true
         );
      else
      {
         ShowGridData = CreateControlHelper.CreateListView(
            $"{nameof(DisplayView)}",
            _displayData.CompleteBatteryDatas.PropertyBindings,
            true,
            true
         );

         #region 出站菜单
         //修正胶钉
         //MenuItem _menuItemCorrectingSealingNail = new MenuItem();
         //_menuItemCorrectingSealingNail.SetResourceReference(MenuItem.HeaderProperty, "修正胶钉");
         //_menuItemCorrectingSealingNail.Command = AsyncCommand.Create(async () => await CorrectingSealingNail(ShowGridData as ListView));

         //MES进站
         //MenuItem menuItemOutbound = new MenuItem();
         //menuItemOutbound.SetResourceReference(MenuItem.HeaderProperty, "MES进站");
         //menuItemOutbound.Command = AsyncCommand.Create(async () => await MesInbound(ShowGridData as ListView));
         //MES出站
         MenuItem menuItemInbound = new MenuItem();
         menuItemInbound.SetResourceReference(MenuItem.HeaderProperty, "MES出站");
         menuItemInbound.Command = AsyncCommand.Create(async () => await MesOutbound(ShowGridData as ListView));
         //MES进站及出站
         //MenuItem menuItemInAndOut = new MenuItem();
         //menuItemInAndOut.SetResourceReference(MenuItem.HeaderProperty, "MES进出站");
         //menuItemInAndOut.Command = AsyncCommand.Create(async () =>
         //{
         //    await MesInbound(ShowGridData as ListView);
         //    await MesOutbound(ShowGridData as ListView);
         //});

         ContextMenu contextMenu = new ContextMenu();
         // contextMenu.Items.Add(_menuItemCorrectingSealingNail);
         contextMenu.Items.Add(menuItemInbound);
         //contextMenu.Items.Add(menuItemOutbound);
         //contextMenu.Items.Add(menuItemInAndOut);
         ((ListView)ShowGridData).ContextMenu = contextMenu;
         #endregion
      }
   }

   /// <summary>
   /// 修正胶钉
   /// </summary>
   /// <param name="listView"></param>
   /// <returns></returns>
   public async Task CorrectingSealingNail(ListView? listView)
   {
      if (listView == null || listView.SelectedItems.Count == 0)
      {
         Growl.Warning("请先选择列！");
         return;
      }
      foreach (var item in listView.SelectedItems)
      {
         try
         {
            //var _batMain = item as BatMainModel;
            //if (_batMain != null)
            //{
            //    BatNailModel? _batNail = _batMain.GetProcess<BatNailModel>();
            //    if (_batNail != null)
            //    {
            //        _batNail.AfterNailResult = ResultTypeEnum.合格;
            //        //更新指定列
            //        var _upDic = new Dictionary<string, object>
            //        {
            //            { nameof(BatMainModel.Id),_batMain.Id },
            //            { nameof(BatMainModel.FinalStatus),_batMain.FinalStatus },
            //        };

            //        StringBuilder stringBuilder = new StringBuilder();
            //        stringBuilder.Append($"ID:{_batMain.Id}，条码：{_batMain.Barcode},密封钉结果修正为[{ResultTypeEnum.合格}];");
            //        if (await _sugarDB.UpdateBatteryAsync(_batNail))
            //        {
            //            if (await _sugarDB.UpdateColumnsAsync<BatMainModel>(_upDic, _batMain.Id, _batMain.Barcode))
            //            {
            //                stringBuilder.ToString().LogRun(Log4NetLevelEnum.成功);
            //                Growl.Success(stringBuilder.ToString());
            //            }
            //            else
            //            {
            //                Growl.Warning($"密封钉条码：{_batMain.Barcode},修正失败!");
            //            }
            //        }
            //        else
            //        {
            //            Growl.Warning($"密封钉条码：{_batMain.Barcode},修正失败!");
            //        }

            //    }
            //}
         }
         catch (Exception ex)
         {
            $"[修正胶钉]异常：{ex}".LogRun(Log4NetLevelEnum.错误);
         }
      }
   }

   /// <summary>
   /// MES进站
   /// </summary>
   /// <param name="listView"></param>
   /// <returns></returns>
   public async Task MesInbound(ListView? listView)
   {
      //try
      //{
      //    if (listView == null || listView.SelectedItems.Count == 0)
      //    {
      //        Growl.Warning("请先选择列！");
      //        return;
      //    }
      //    var dictionarys = listView.SelectedItems.OfType<IDictionary<string, object>>();
      //    var ids = dictionarys.Select(x => (long)x[nameof(BatMainModel.Id)]).ToList();
      //    await Task.Run(async () =>
      //    {
      //        foreach (var id in ids)
      //        {
      //            string logHeader = $"[手动MES进站]ID:{id}！";
      //            var mainBattery = await _batteryCache.GetByIdAsync(id, logHeader);

      //            if (mainBattery != null)
      //            {
      //                var call = _mesInterfaceParameterConfig.GetApiCall(new MesRequestBuildNJGX.ArgsProductEntry(mainBattery.Barcode));
      //                if (call == null || !call.IsEnable)
      //                {
      //                    Growl.Warning($"[手动MES进站]接口未启用或未找到接口信息！");
      //                    return;
      //                }
      //                var mesReslut = await _mesService.SendAsync(call, mainBattery.Barcode,
      //                    receive => receive.MesGeneralParse(logHeader));

      //                mainBattery.MesInputStatus = mesReslut.ResultStatus switch
      //                {
      //                    MesResultStatusEnum.成功 => ResultTypeEnum.OK,
      //                    MesResultStatusEnum.MES判定NG => ResultTypeEnum.MES判定NG,
      //                };

      //                logHeader = $"[手动MES进站]条码：{mainBattery.Barcode},ID:{mainBattery.Id},MES结果：{mesReslut.ResultStatus}！";
      //                logHeader.LogRun(mainBattery.MesInputStatus == ResultTypeEnum.OK ? Log4NetLevelEnum.成功 : Log4NetLevelEnum.错误, true);
      //                //更新指定列
      //                var _upDic = new Dictionary<string, object>
      //                {
      //                       { nameof(BatMainModel.Id),mainBattery.Id },
      //                       { nameof(BatMainModel.MesInputStatus), mainBattery.MesInputStatus },
      //                       { nameof(BatMainModel.FinalStatus), mainBattery.FinalStatus },
      //                };

      //                if (!await _sugarDB.UpdateColumnsAsync(_upDic, mainBattery.Id, mainBattery.Barcode, logHeader))
      //                {
      //                    logHeader = $"[手动MES进站]条码：{mainBattery.Barcode},ID:{mainBattery.Id} ,保存数据失败!";
      //                    mainBattery.MesInputStatus = ResultTypeEnum.保存数据库失败;
      //                    logHeader.LogRun(mainBattery.MesInputStatus == ResultTypeEnum.OK ? Log4NetLevelEnum.成功 : Log4NetLevelEnum.错误, true);
      //                }

      //                UIThreadHelper.InvokeOnUiThreadAsync(() =>
      //                {
      //                    var dictionary = dictionarys.FirstOrDefault(x => (long)x[nameof(BatMainModel.Id)] == mainBattery.Id);
      //                    dictionary[nameof(BatMainModel.MesInputStatus)] = mainBattery.MesInputStatus;
      //                    dictionary[nameof(BatMainModel.FinalStatus)] = mainBattery.FinalStatus;
      //                });
      //            }
      //        }
      //    });
      //}
      //catch (Exception ex)
      //{
      //    $"[手动MES进站]异常：{ex}".LogRun(Log4NetLevelEnum.错误, true);
      //}
   }

   /// <summary>
   /// MES出站
   /// </summary>
   /// <param name="listView"></param>
   /// <returns></returns>
   public async Task MesOutbound(ListView? listView)
   {
      try
      {
         if (listView == null || listView.SelectedItems.Count == 0)
         {
            Growl.Warning("请先选择列！");
            return;
         }
         var dictionarys = listView.SelectedItems.OfType<IDictionary<string, object>>();
         var ids = dictionarys.Select(x => (long)x[nameof(BatMainModel.Id)]).ToList();
         await Task.Run(async () =>
         {
            StringBuilder stringBuilder = new StringBuilder();
            List<MesResendModel> upResends = new List<MesResendModel>();
            List<IBatMainModel> upBatterys = new();
            foreach (var id in ids)
            {
               string logHeader = $"[手动出站-id{id}]";
               var batMain = await _batteryCache.GetByIdAsync(id, logHeader);
               if (batMain != null)
               {
                  var sendResult = await MesOutboundHelper.MesOutput(_container, _mesService, batMain, logHeader);
                  if (sendResult == OutputStatus.未上传)
                  {
                     Growl.Warning("接口未开启，请开启后再上传；");
                     return;
                  }

                  if (sendResult is OutputStatus.成功 or OutputStatus.MES判定NG) //只有成功或失败才需更新补传表
                  {
                     upBatterys.Add(batMain);
                     stringBuilder.AppendLine(
                        $"[手动MES出站]条码：{batMain.Barcode},ID:{batMain.Id},MES结果：{batMain.MesOutputStatus}！"
                     );
                     var factory = _container.Get<ISqlSugarDbFactory>();
                     var resend = await factory.UsingDbAsync(async db =>
                        await db.Queryable<MesResendModel>().FirstAsync(x => x.Id == batMain.Id)
                     ); //取MES补传表

                     if (resend != null)
                     {
                        resend.ResendCount++;
                        resend.LastResult = batMain.MesOutputStatus;
                        resend.LastUpdateTime = batMain.MesOutputTime;
                        resend.ResendStatus = sendResult switch
                        {
                           OutputStatus.成功 => ResendStatusEnum.上传成功,
                           _ => ResendStatusEnum.上传失败,
                        };
                        upResends.Add(resend);
                     }
                  }
                  else
                  {
                     stringBuilder.AppendLine(
                        $"[手动MES出站]条码：{batMain.Barcode},ID:{batMain.Id},MES结果：{batMain.MesOutputStatus}，不更新数据！"
                     );
                  }
               }
            }
            stringBuilder.ToString().LogProcess("[手动MES出站]", isPrompt: true);
            if (
               await PeriodicTasksHelper.UpdateTran(_sugarDB, upBatterys, upResends, _batteryCache, "[手动MES出站]")
               == PeriodicTasksHelper.ResendResultEnum.成功
            )
               "出站后更新成功".LogProcess("[手动MES出站]", Log4NetLevelEnum.成功);
            else
               "出站后更新失败".LogProcess("[手动MES出站]", Log4NetLevelEnum.错误, isPrompt: true);
         });
      }
      catch (Exception ex)
      {
         $"[手动MES出站]异常：{ex}".LogRun(Log4NetLevelEnum.错误, true);
      }
   }

   public void SelectedOKAllCMD(System.Windows.Controls.Primitives.ToggleButton toggleButton)
   {
      foreach (var item in QueryParameters)
      {
         item.IsOk = (bool)toggleButton.IsChecked;
      }
   }

   public void SelectedNGAllCMD(System.Windows.Controls.Primitives.ToggleButton toggleButton)
   {
      foreach (var item in QueryParameters)
      {
         item.IsNg = (bool)toggleButton.IsChecked;
      }
   }

   public void ClearSelectedCMD()
   {
      foreach (var item in QueryParameters)
      {
         item.IsNg = false;
         item.IsOk = false;
      }
   }

   #region 查询
   /// <summary>
   ///
   /// </summary>
   /// <param name="sender"></param>
   /// <param name="e"></param>
   public async void PaginationPageUpdatedCMD(object sender, HandyControl.Data.FunctionEventArgs<int> e)
   {
      PageIndex = e.Info;
      if (DataCountPerPage > 2000)
      {
         Growl.Warning("单页数据量请勿大于2000！");
         return;
      }

      sw.Restart();
      DataList = await QueryData(StartTime, EndTime, Barcode, QueryBatteryType, MesStatus, true, false);
      DisplayView = CollectionViewSource.GetDefaultView(DataList);
      sw.Stop();
      $"查询用时:{sw.ElapsedMilliseconds}ms".LogRun();
   }

   Stopwatch sw = Stopwatch.StartNew();

   /// <summary>
   /// 查询
   /// </summary>
   public async void QueryCMD()
   {
      if (DataCountPerPage > 2000)
      {
         Growl.Warning("单页数据量请勿大于2000！");
         return;
      }
      sw.Restart();
      DataList = await QueryData(StartTime, EndTime, Barcode, QueryBatteryType, MesStatus, true, true);
      DisplayView = CollectionViewSource.GetDefaultView(DataList);
      sw.Stop();
      $"查询用时:{sw.ElapsedMilliseconds}ms".LogRun();
   }

   /// <summary>
   /// 导出当天数据
   /// </summary>
   public async void ExportExcelDailyDataCMD()
   {
      sw.Restart();
      DateTime _dateTime = DateTime.Now;
      var _start = new DateTime(_dateTime.Year, _dateTime.Month, _dateTime.Day, 0, 0, 0);
      var _end = new DateTime(_dateTime.Year, _dateTime.Month, _dateTime.Day, 23, 59, 59);
      var data = await QueryData(
         _start,
         _end,
         string.Empty,
         QueryBatteryTypeEnum.全部,
         QueryBatteryMESStatusEnum.全部,
         false,
         true
      );
      if (data != null)
      {
         try
         {
            _dialog = Dialog.Show(GenericHelper.CreateLoadingCircle(), ProductionHistoryLayoutViewModel.DialogToke);
            await Task.Run(() =>
               ExcelHelper.ExportBattery(data, _otherParameter, _displayData.CompleteBatteryDatas.PropertyBindings)
            );
         }
         finally
         {
            _dialog.Close();
         }
      }
      sw.Stop();
      $"导出当天数据用时:{sw.ElapsedMilliseconds}ms".LogRun();
   }

   /// <summary>
   /// 导出数据
   /// </summary>
   public async void ExportExcelCMD()
   {
      sw.Restart();
      var data = await QueryData(StartTime, EndTime, Barcode, QueryBatteryType, MesStatus, false, true);
      if (data != null)
      {
         try
         {
            _dialog = Dialog.Show(GenericHelper.CreateLoadingCircle(), ProductionHistoryLayoutViewModel.DialogToke);
            await Task.Run(() =>
               ExcelHelper.ExportBattery(data, _otherParameter, _displayData.CompleteBatteryDatas.PropertyBindings)
            );
         }
         finally
         {
            _dialog.Close();
         }
      }
      sw.Stop();
      $"导出数据用时:{sw.ElapsedMilliseconds}ms".LogRun();
   }

   /// <summary>
   ///
   /// </summary>
   /// <param name="startTime"></param>
   /// <param name="endTime"></param>
   /// <param name="barcode">查询条码</param>
   /// <param name="queryBatteryType">查询电池类型</param>
   /// <param name="queryBatteryMESStatus">查询电池MES状态</param>
   /// <param name="isDisplay">导出数据或展示数据</param>
   /// <param name="isFirst">展示数据第一次查询（非分页）</param>
   /// <returns></returns>
   private async Task<List<ExpandoObject>> QueryData(
      DateTime startTime,
      DateTime endTime,
      string barcode,
      QueryBatteryTypeEnum queryBatteryType,
      QueryBatteryMESStatusEnum queryBatteryMESStatus,
      bool isDisplay,
      bool isFirst
   )
   {
      if (endTime < startTime)
      {
         Growl.Warning($"结束日期小于开始日期，请重新选择!");
         return new List<ExpandoObject>();
      }
      _dialog = Dialog.Show(GenericHelper.CreateLoadingCircle(), ProductionHistoryLayoutViewModel.DialogToke);

      try
      {
         var byInputTime = SelectTimeType == QueryTimeType.按进站时间;
         var sql = await _sugarDB.GetQueryableByBarcdoe(
            startTime,
            endTime,
            barcode,
            queryBatteryType,
            queryBatteryMESStatus,
            IsNotRepeat,
            byInputTime,
            IsFuzzyQuery
         );

         if (sql == null || string.IsNullOrEmpty(sql))
         {
            TotalCount = 0;
            TotalPage = 0;
            return new List<ExpandoObject>();
         }
         var factory = _container.Get<ISqlSugarDbFactory>();
         using var db = factory.CreateClient(DatabaseRole.LocalDb1);
         if (db == null)
            return new List<ExpandoObject>();

         var sugarQueryable = db.SqlQueryable<ExpandoObject>(sql);
         if (isDisplay)
         {
            List<ExpandoObject> queryData = new();
            if (isFirst) //展示数据第一次查询（非分页）
            {
               RefAsync<int> totalCount = 0; //异步 REF和OUT不支持异步
               RefAsync<int> totalPage = 0; //异步 REF和OUT不支持异步
               queryData = await sugarQueryable.ToOffsetPageAsync(PageIndex, DataCountPerPage, totalCount, totalPage);
               TotalCount = totalCount.Value;
               TotalPage = totalPage.Value;
            }
            else
            {
               queryData = await sugarQueryable.ToOffsetPageAsync(PageIndex, DataCountPerPage);
            }
            return queryData;
         }
         else
         {
            return await Task.Run(() => sugarQueryable.ToList());
         }
      }
      catch (Exception ex)
      {
         $"[查询数据]出现异常：{ex}".LogRun(Log4NetLevelEnum.错误, true);
      }
      finally
      {
         _dialog.Close();
      }
      return new List<ExpandoObject>();
   }
   #endregion

   ManualTrayLoadingViewModel _trayLoadingViewModel;

   /// <summary>
   ///  //手动托盘装载
   /// </summary>
   public void MaualTrayLoadingCmd()
   {
      try
      {
         if (_trayLoadingViewModel != null && _trayLoadingViewModel.View != null)
         {
            ((ManualTrayLoadingView)_trayLoadingViewModel.View).Activate();
            return;
         }
         _trayLoadingViewModel ??= _container.Get<ManualTrayLoadingViewModel>();
         _windowManager.ShowWindow(_trayLoadingViewModel);
      }
      catch (Exception ex)
      {
         $"打开注液手工组盘窗口异常：{ex}".LogRun(Log4NetLevelEnum.警告, true);
      }
   }
}

public enum QueryTimeType
{
   按进站时间,
   按出站时间,
}
