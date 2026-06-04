namespace Kinlo.Common.DAL;

public partial class DbHelper
{
  IContainer _container;
  ISqlSugarDbFactory _dbFactory;
  private DisplayDataCollection _displayDatas;
  private SnowflakeHelper _snowflakeHelper;
  private ParameterConfig _parameterConfig;

  public DbHelper(IContainer container)
  {
    _container = container;
    _dbFactory = _container.Get<ISqlSugarDbFactory>();
    _displayDatas = container.Get<DisplayDataCollection>();
    _parameterConfig = container.Get<ParameterConfig>();
    _snowflakeHelper = container.Get<SnowflakeHelper>();
  }

  #region 初始化数据库表
  /// <summary>
  /// 初始化数据库
  /// </summary>
  /// <param name="role"></param>
  /// <returns></returns>
  public async Task<bool> Initializer(DatabaseRole role)
  {
    return await _dbFactory.UsingDbAsync(
      role,
      async db =>
      {
        try
        {
          await Task.Run(() =>
          {
            $"初始化数据库开始！".LogRun();
            //   var _ret = db.Ado.IsValidConnection();
            db.DbMaintenance.CreateDatabase();
            db.Ado.ExecuteCommand("ALTER DATABASE weightdb CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci;");
            SyncSplitTableFiled(db);
            $"初始化数据库完成！".LogRun();
          });
          return true;
        }
        catch (Exception ex)
        {
          $"初始化数据库异常：{ex.Message}".LogRun();
          return false;
        }
      }
    );
  }

  //完整电池字段
  string _fields = string.Empty;
  string _aliasName = "main";

  //加别名
  string _aliasFields = string.Empty;

  /// <summary>
  ///
  /// </summary>
  /// <param name="db"></param>
  public void SyncSplitTableFiled(ISqlSugarClient db)
  {
    try
    {
      var types = new List<Type>();
      types.Add(typeof(InjectionDataModel));
      types.Add(typeof(GasConcentrationModel));
      types.Add(typeof(PlcStatusModel));
      types.Add(typeof(PlcAlarmModel));
      types.Add(typeof(MesResendModel));
      types.Add(_displayDatas.CompleteBatteryDatas.RuntimeBatteryType);
      var group = types.GroupBy(t => t).Select(x => x.Key).ToArray();
      db.CodeFirst.SplitTables().InitTables(group); //同步分表

      //取完整电池的字段
      var list = _displayDatas
        .CompleteBatteryDatas.RuntimeBatteryType.GetProperties()
        .Where(x =>
        {
          var att = x.GetCustomAttribute<SugarColumn>();
          if (att != null && att.IsIgnore)
            return false;
          return true;
        })
        .Select(p => p.Name)
        .ToList();

      _fields = string.Join(",", list);
      _aliasFields = string.Join(", ", list.Select(f => $"{_aliasName}.{f.Trim()}"));
    }
    catch (Exception ex)
    {
      $"同步数据库表异常：{ex.Message}".LogRun(Log4NetLevelEnum.错误, true);
    }
  }
  #endregion
}
