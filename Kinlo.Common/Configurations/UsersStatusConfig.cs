using HandyControl.Controls;
using Kinlo.Equipment.Devices.Fingerprints.Live20R;

namespace Kinlo.Common.Configurations;

public class UsersStatusConfig : ConfigurationBase
{
   /// <summary>
   /// 已登陆用户类型
   /// </summary>
   public LoggedInTypeEnum LoggedInUserType { get; set; }

   private UserModel _localLoggedinUser = new();

   /// <summary>
   /// 本地登陆用户
   /// </summary>
   [JsonIgnore]
   public UserModel LocalLoggedinUser
   {
      get { return _localLoggedinUser; }
      set
      {
         if (_localLoggedinUser != value)
         {
            _localLoggedinUser = value;
            RefreshAutoLogoutTimer();
         }
      }
   }

   /// <summary>
   /// 用户列表
   /// </summary>
   public ObservableCollection<UserModel> LocalUsers { get; set; } = new();

   /// <summary>
   /// 如果在用户配置界面，不登陆
   /// </summary>
   [JsonIgnore]
   public bool IsAutoLogin { get; set; } = true;

   /// <summary>
   /// 指纹库
   /// </summary>
   public Dictionary<int, byte[]> FingerpringDatas { get; set; } = new Dictionary<int, byte[]>();

   /// <summary>
   /// 实时指纹委托
   /// </summary>
   [JsonIgnore]
   public Action<byte[], Live20R>? CurrentFingerpringAction { get; set; } = null;

   [JsonIgnore]
   public Action<string>? CardAction { get; set; } = null;

   /// <summary>
   /// 指纹读取器
   /// </summary>
   [JsonIgnore]
   public Live20R? Live20RFingerprints { get; set; }

   /// <summary>
   /// 登陆委托
   /// </summary>
   [JsonIgnore]
   public Func<string, string, int, LoginAccountTypeEnum, Task<bool>>? LoginActionAsync { get; set; }

   /// <summary>
   /// 最后登陆或最后键鼠活动时间
   /// </summary>
   [JsonIgnore]
   public DateTime LastActivityTime { get; set; }
   ParameterConfig _parameterConfig;

   public UsersStatusConfig(StyletIoC.IContainer container, bool isStartup)
      : base(container, isStartup)
   {
      _parameterConfig = container.Get<ParameterConfig>();
      RefreshAutoLogoutTimer();
   }

   public override void Load()
   {
      try
      {
         var _dic = FileHelper.LoadToDictionary(this.GetType().Name);
         if (_dic != null && _dic.TryGetValue(nameof(FingerpringDatas), out object fingers) && fingers != null)
         {
            FingerpringDatas = JsonSerializer.Deserialize<Dictionary<int, byte[]>>(fingers.ToString());
         }
         if (FingerpringDatas == null)
            FingerpringDatas = new Dictionary<int, byte[]>();

         if (_dic != null && _dic.TryGetValue(nameof(LocalUsers), out object value) && value != null)
         {
            LocalUsers = JsonSerializer.Deserialize<ObservableCollection<UserModel>>(value.ToString());
         }
         if (LocalUsers == null)
            LocalUsers = new ObservableCollection<UserModel>();

         if (LocalUsers.Count == 0 || !LocalUsers.Any(x => x.Role.Name == DefaultRoleEnum.管理员.ToString()))
         {
            var _role = _container
               .Get<RoleConfig>()
               .Roles.FirstOrDefault(x => x.Name == DefaultRoleEnum.管理员.ToString());
            if (_role == null)
               _role = new RoleModel(ulong.MaxValue >> 1, DefaultRoleEnum.管理员.ToString(), 5);
            LocalUsers.Add(
               new UserModel
               {
                  Account = "admin",
                  Password = "admin",
                  RegisterTime = DateTime.Now,
                  Role = _role,
               }
            );
         }
      }
      catch (Exception ex)
      {
         $"[初始化UsersStatusConfig]异常：{ex}".LogRun(Log4NetLevelEnum.错误, true);
      }
      var _devicesConfig = _container.Get<DevicesConfig>();

      if (
         _devicesConfig.DeviceList.FirstOrDefault(x => x.Communication == CommunicationEnum.Live20R指纹器 && x.IsEnable)
         != null
      )
      {
         Live20RFingerprints = CreateLive20R(GlobalStaticTemporary.GlobalToken);
         if (Live20RFingerprints != null)
         {
            Live20RFingerprints.FingerpringAction = FingerprintLogin;
         }
      }

      try
      {
         if (
            _devicesConfig.DeviceList.FirstOrDefault(x =>
               x.Communication == CommunicationEnum.HX540_H_E刷卡器 && x.IsEnable
            ) != null
         )
         {
            var _card = new Kinlo.Equipment.Devices.CardReaders.HX540_H_E(GlobalStaticTemporary.GlobalToken);
            _card.Open();
            _card.CardAction = CardLogin;
         }
      }
      catch (Exception ex)
      {
         $"[初始化刷卡器]异常：{ex}".LogRun(Log4NetLevelEnum.错误, true);
      }

      try
      {
         var cards = _devicesConfig.DeviceList.Where(x =>
            x.Communication == CommunicationEnum.通用串口刷卡器 && x.IsEnable
         );
         if (cards.Any())
         {
            foreach (var card in cards)
            {
               $"[初始化通用串口刷卡器]开始!".LogRun(Log4NetLevelEnum.信息);
               if (card.TryCreateDevice(GlobalStaticTemporary.GlobalCancellationTokenSource, out var device))
               {
                  $"[初始化通用串口刷卡器]Com[{card.IPCOM}] Port[{card.Port}],成功!".LogRun(Log4NetLevelEnum.信息);
                  if (device is ICardReader<string> cardReader)
                  {
                     cardReader.CardAction = CardLogin;
                  }
               }
            }
         }
      }
      catch (Exception ex)
      {
         $"[初始化通用串口刷卡器]异常：{ex}".LogRun(Log4NetLevelEnum.错误, true);
      }
   }

   public bool CreateUser(UserModel user)
   {
      try
      {
         if (LocalUsers.Any(x => x.Account == user.Account))
         {
            Growl.Warning($"[新增用户]:用户名 [{user.Account}] 重复,请重新填写!");
            return false;
         }
         user.RegisterTime = DateTime.Now;
         LocalUsers.Add(user);
         this.Save(LocalLoggedinUser.Account, $"[新增用户]:[{user.Account}]");
         return true;
      }
      catch (Exception ex)
      {
         $"[新增用户]异常:\r\n{ex}".LogSetting(Log4NetLevelEnum.错误, true);
         return false;
      }
   }

   public bool UpdateUser(UserModel user)
   {
      try
      {
         StringBuilder sb = new StringBuilder();

         UserModel _user = LocalUsers.First(x => x.Account == user.Account);

         if (_user.Password != user.Password)
         {
            _user.Password = user.Password;
            sb.Append($" [修改了密码] \r\n");
         }
         if (_user.Name != user.Name)
         {
            _user.Name = user.Name;
            sb.Append($" [修改了姓名] \r\n");
            sb.Append($" [修改了姓名 {_user.Name}==>{_user.Name}] \r\n");
         }
         if (_user.Role.Level != user.Role.Level)
         {
            _user.Role = user.Role;
            sb.Append($" [{_user.Role.Name}==>{_user.Role.Name}] \r\n");
         }
         if (_user.Tel != user.Tel)
         {
            _user.Tel = user.Tel;
            sb.Append($" [{_user.Tel}==>{_user.Tel}] \r\n");
         }
         if (sb.Length > 0)
         {
            _user.UpdateTime = DateTime.Now;
            this.Save(LocalLoggedinUser.Account, $"[修改用户]:\r\n{sb}");
         }
         return true;
      }
      catch (Exception ex)
      {
         $"[修改用户]异常:\r\n{ex}".LogSetting(Log4NetLevelEnum.错误, true);
         return false;
      }
   }

   public bool DeleteUser(UserModel user)
   {
      try
      {
         LocalUsers.Remove(user);
         this.Save(LocalLoggedinUser.Account, $"[删除用户]:[{user.Account}]", true);
         return true;
      }
      catch (Exception ex)
      {
         $"[删除用户]异常:\r\n{ex}".LogSetting(Log4NetLevelEnum.错误, true);
         return false;
      }
   }

   public void FingerprintLogin(byte[] fingerprint, string fingerprintStr)
   {
      if (IsAutoLogin)
      {
         int _fingerKey = Live20RFingerprints.DBIdentify(fingerprint);
         _ = LoginActionAsync(fingerprintStr, string.Empty, _fingerKey, LoginAccountTypeEnum.指纹登陆);
      }
      else
      {
         CurrentFingerpringAction?.Invoke(fingerprint, Live20RFingerprints);
      }
   }

   public void CardLogin(string barcode)
   {
      if (IsAutoLogin)
      {
         _ = LoginActionAsync.Invoke(string.Empty, barcode, 0, LoginAccountTypeEnum.刷卡登陆);
      }
      else
      {
         CardAction?.Invoke(barcode);
      }
   }

   /// <summary>
   /// Live20R指纹器
   /// </summary>
   /// <param name="cancellationToken"></param>
   /// <returns></returns>
   /// <exception cref="Exception"></exception>
   public Live20R? CreateLive20R(CancellationToken cancellationToken)
   {
      try
      {
         var _ive20R = new Kinlo.Equipment.Devices.Fingerprints.Live20R.Live20R(cancellationToken);
         _ive20R.Open();
         if (FingerpringDatas != null)
         {
            List<int> ids = new List<int>();
            for (int i = FingerpringDatas.Count - 1; i > -1; i--)
            {
               int _fingerpringId = FingerpringDatas.ElementAt(i).Key;
               if (!LocalUsers.Any(x => x.FingerprintID == _fingerpringId))
               {
                  FingerpringDatas.Remove(_fingerpringId);
                  ids.Add(_fingerpringId);
               }
            }
            if (ids.Count > 0)
            {
               string _msg = $"[初始化UsersStatusConfig]清除本地无用户指纹ID：{string.Join(",", ids)}";
               this.Save("系统初始化", _msg);
               _msg.LogRun(Log4NetLevelEnum.信息);
            }
            FingerpringDatas
               ?.ToList()
               .ForEach(item =>
               {
                  _ive20R.DBAdd(item.Key, item.Value);
               });
         }
         return _ive20R;
      }
      catch (Exception ex)
      {
         $"[初始化UsersStatusConfig] 异常：{ex}".LogRun(Log4NetLevelEnum.警告);
      }
      return null;
   }

   #region 用户自动退出
   /// <summary>
   /// 用户活动时调用
   /// </summary>
   public void RefreshAutoLogoutTimer()
   {
      LastActivityTime = DateTime.Now; // 仅更新时间戳
   }

   public event Action? BackHome = null;

   public async Task AutoLogoutTimerTick(DateTime time)
   {
      if (_parameterConfig.AdvancedConfig.AutoExitSuperAdminTime <= 0) //0秒为不退出
         return;
      // 检查是否超时
      if ((time - LastActivityTime).TotalSeconds >= _parameterConfig.AdvancedConfig.AutoExitSuperAdminTime)
      {
         await UIThreadHelper.InvokeOnUiThreadAsync(() =>
         {
            if (LocalLoggedinUser.Role.Level == ulong.MaxValue) //当登陆的为超级管理员时自动退出
            {
               LocalLoggedinUser = new UserModel();
               LoggedInUserType = LoggedInTypeEnum.未登陆;
               Growl.Info("长时间无操作，已自动退出登录。");
            }
            BackHome?.Invoke();
         });
      }
   }
   #endregion
}
