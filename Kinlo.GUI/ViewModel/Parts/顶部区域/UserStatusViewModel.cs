using HandyControl.Controls;

namespace Kinlo.GUI.ViewModel;

[UIDisplayAttribute(true)]
public class UserStatusViewModel : Screen
{
   private UserLoginType _selectLoginType;

   /// <summary>
   /// 登陆类型
   /// </summary>
   public UserLoginType SelectLoginType
   {
      get { return _selectLoginType; }
      set
      {
         if (_selectLoginType != value)
         {
            _selectLoginType = value;
            LoginTypeIsChecked = false;
         }
      }
   }

   public List<UserLoginType> LoginTypes { get; set; } = Enum.GetValues<UserLoginType>().ToList();
   public bool LoginTypeIsChecked { get; set; }
   public UsersStatusConfig UsersStatus { get; set; }
   DevicesConfig _devicesConfig;
   ParameterConfig _parameter;
   PLCSignalConfig _signal;
   MesInterfaceParameterConfig _mesInterfaceParameterConfig;
   IContainer _container;
   Stylet.IWindowManager _windowManager;

   public UserStatusViewModel(IContainer container, Stylet.IWindowManager windowManager)
   {
      _container = container;
      _windowManager = windowManager;
      UsersStatus = container.Get<UsersStatusConfig>();
      _parameter = container.Get<ParameterConfig>();
      _signal = container.Get<PLCSignalConfig>();
      _mesInterfaceParameterConfig = container.Get<MesInterfaceParameterConfig>();
      _devicesConfig = container.Get<DevicesConfig>();

      UsersStatus.LoginActionAsync = LoginAsync;
      //KeyboardHook.Hook.RegistrationListening(
      //   new ShortcutKeyModel
      //   {
      //      IsCrtl = true,
      //      IsAlt = true,
      //      Key = 'K',
      //      Action = () => LoginWithShortcutKey(1),
      //   }
      //);
      KeyboardHook.Hook.RegistrationListening(
         new ShortcutKeyModel
         {
            IsCrtl = true,
            Key = 'Z',
            Action = () => LoginWithShortcutKey(2),
         }
      );
      //KeyboardHook.Hook.RegistrationListening(
      //   new ShortcutKeyModel
      //   {
      //      IsCrtl = true,
      //      Key = 'G',
      //      Action = () => LoginWithShortcutKey(3),
      //   }
      //);
      //KeyboardHook.Hook.RegistrationListening(
      //   new ShortcutKeyModel
      //   {
      //      IsCrtl = true,
      //      Key = 'B',
      //      Action = () => LoginWithShortcutKey(4),
      //   }
      //);
      //KeyboardHook.Hook.RegistrationListening(
      //   new ShortcutKeyModel
      //   {
      //      IsCrtl = true,
      //      Key = 'B',
      //      Action = () => LoginWithShortcutKey(4),
      //   }
      //);
      RegisterUsbCardReader();
   }

   /// <summary>
   /// 注册的刷卡 卡号长度 列表
   /// </summary>
   int[] _cardLengthList = [];

   /// <summary>
   /// 注册 普通USB刷卡器 刷卡登陆
   /// </summary>
   void RegisterUsbCardReader()
   {
      //卡号长度 从6注册31个，如果确定卡号长度也可指定长度注册
      _cardLengthList = Enumerable.Range(6, 31).ToArray();

      //注册刷卡登陆
      foreach (var item in _cardLengthList)
      {
         KeyboardHook.Hook.RegistrationListening(item, barcode => UsersStatus.CardLogin(barcode));
      }
      $"[普通USB刷卡器] 注册卡号长度{string.Join(',', _cardLengthList)}完成".LogRun();
   }

   /// <summary>
   /// 注销 普通USB刷卡器 刷卡登陆
   /// </summary>
   void UnregisterUsbCardReader()
   {
      //注销刷卡登陆
      foreach (var item in _cardLengthList)
      {
         KeyboardHook.Hook.UnRegistrationListening(item);
      }
      $"[普通USB刷卡器] 注销卡号长度{string.Join(',', _cardLengthList)}完成".LogRun();
   }

   /// <summary>
   /// 快捷键登陆
   /// </summary>
   /// <param name="key"></param>
   /// <returns></returns>
   public bool LoginWithShortcutKey(int key)
   {
      ObservableCollection<RoleModel> _roles = _container.Get<RoleConfig>().Roles;
      UserModel? loginUser = key switch
      {
         1 => new UserModel { Account = "超级用户", Role = new RoleModel(ulong.MaxValue, "超级管理员", 6) },
         2 => new UserModel
         {
            Account = "调试管理员",
            Role = _roles.First(x => x.Name == DefaultRoleEnum.管理员.ToString()),
         },
         3 => new UserModel
         {
            Account = "调试工艺",
            Role = _roles.First(x => x.Name == DefaultRoleEnum.工艺.ToString()),
         },
         4 => new UserModel
         {
            Account = "调试设备",
            Role = _roles.First(x => x.Name == DefaultRoleEnum.设备.ToString()),
         },
         _ => null,
      };
      if (loginUser == null)
         return false;

      UsersStatus.LoggedInUserType = LoggedInTypeEnum.本地登陆;
      loginUser.LoginTime = DateTime.Now;
      UsersStatus.LocalLoggedinUser = loginUser;
      return true;
   }

   /// <summary>
   ///
   /// </summary>
   /// <param name="name">如果快捷登陆启用，1:超级管理员;2:调试员</param>
   /// <param name="password"></param>
   /// <param name="fingerprintId">指纹ID</param>
   /// <param name="loginType">登陆类型</param>
   /// <returns></returns>
   public async Task<bool> LoginAsync(string name, string password, int fingerprintId, LoginAccountTypeEnum loginType)
   {
      if (SelectLoginType is UserLoginType.用户登陆MES or UserLoginType.优先MES登陆) //如果开启MES登陆，优先MES登陆
      {
         if (await MesLoginAsync(name, password, loginType))
         {
            return true;
         }
         else
         {
            if (SelectLoginType is UserLoginType.用户登陆MES)
               return false;
         }
      }
      //  $"登陆类型：[{loginType}],密码[{password}],用户集：[{JsonSerializer.Serialize(UsersStatus.LocalUsers)}]".LogRun();
      var loginUser = loginType switch //本地登陆
      {
         LoginAccountTypeEnum.刷卡登陆 => UsersStatus.LocalUsers.FirstOrDefault(x => x.Password == password),
         var t when t == LoginAccountTypeEnum.指纹登陆 && fingerprintId > 0 => UsersStatus.LocalUsers.FirstOrDefault(
            x => x.FingerprintID == fingerprintId
         ),
         _ => UsersStatus.LocalUsers.FirstOrDefault(x => x.Account == name && x.Password == password),
      };
      if (loginUser != null)
      {
         UsersStatus.LoggedInUserType = LoggedInTypeEnum.本地登陆;
         loginUser.LoginTime = DateTime.Now;
         UsersStatus.LocalLoggedinUser = loginUser;
         await SyncPLC(loginUser); //同步PLC
         return true;
      }

      $"登陆失败,帐号或密码错误!".LogRun(Log4NetLevelEnum.错误, true);
      return false;
   }

   /// <summary>
   /// MES登陆
   /// </summary>
   /// <param name="account"></param>
   /// <param name="password"></param>
   /// <param name="loginType"></param>
   /// <returns></returns>
   private async Task<bool> MesLoginAsync(string account, string password, LoginAccountTypeEnum loginType)
   {
      MesService mesService = _container.Get<MesService>();
      if (loginType == LoginAccountTypeEnum.指纹登陆)
      {
         return false;
      }
      else //if (loginType == LoginAccountTypeEnum.刷卡登陆)//常规及刷卡登陆
      {
         // string pwd = Convert.ToBase64String(Encoding.UTF8.GetBytes(password));//转base64

         var call = _mesInterfaceParameterConfig.GetApiCall(new MesRequestBuildNJGX.ArgsMesLogin(account, password));
         if (call == null || !call.IsEnable)
         {
            Growl.Warning($"[MES登陆]接口未启用或未找到接口信息！");
            return false;
         }
         var result = await mesService.SendAsync(
            call,
            "MES登录",
            receiveMes => receiveMes.MesCommonParse("MES登陆").MesLoginParse()
         );

         if (result.ResultStatus == MesResultStatusEnum.成功)
         {
            result.Data.Role = _container.Get<RoleConfig>().Roles.First(x => x.Name == DefaultRoleEnum.生产.ToString());
            UsersStatus.LoggedInUserType = LoggedInTypeEnum.MES登陆;
            UsersStatus.LocalLoggedinUser = result.Data;
            await SyncPLC(result.Data); //同步PLC
         }
         else if (result.ResultStatus == MesResultStatusEnum.生成报文失败)
         {
            Growl.Warning("MES登陆接口生成报文失败！");
         }
         return result.ResultStatus == MesResultStatusEnum.成功;
      }
   }

   #region 同步PLC
   /// <summary>
   /// 同步PLC
   /// </summary>
   /// <returns></returns>
   public async Task SyncPLC(UserModel user)
   {
      if (_parameter.FunctionEnable.IsEnableSyncPLCInquire  && HandyControl.Controls.MessageBox.Show(    $"要同步[{user.Role.Name}权限至PLC吗?",   "提示：", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
      {
         return;
      }

      var plc = _devicesConfig.GetRunDevice(x => x.DeviceInfo.ProcessesType == ProcessTypeEnum.PLC);
      if (plc != null)
      {
         Send(plc, user);
      }
      else
      {
         var device = _devicesConfig.DeviceList.FirstOrDefault(x => x.ProcessesType == ProcessTypeEnum.PLC);
         if (device == null)
         {
            Growl.Warning($"未找到PLC配置文件！");
            return;
         }
         await device.WithCreatedDeviceAsync(async plc => await Task.Run(() => Send(plc, user)));
      }
   }

   private void Send(IDevice plc, UserModel user)
   {
      //写入权限
      var custom = _signal.CustomPlcInteractAddresses.FirstOrDefault(x =>
         x.CustomInteractName == CustomInteractNameEnum.PC至PLC用户权限
      );
      if (custom != null && custom.IsEnable && !string.IsNullOrEmpty(custom.DataAddress.Lable))
      {
         var levelResult = plc.WriteValue(user.Role.PlcLevel, custom.DataAddress, "[PLC权限]写入权限");
         $"[PLC权限] 权限 [{user.Role.PlcLevel}],写入 [{JsonSerializer.Serialize(custom.DataAddress)}] {(levelResult ? "成功" : "失败")}".LogRun(
            levelResult ? Log4NetLevelEnum.成功 : Log4NetLevelEnum.警告
         );
      }
      else
      {
         $"[PLC权限] 未设定相关地址或未启用，不写入PLC;".LogRun(Log4NetLevelEnum.警告);
      }

      custom = _signal.CustomPlcInteractAddresses.FirstOrDefault(x =>
         x.CustomInteractName == CustomInteractNameEnum.PC至PLC用户名
      );
      if (custom != null && custom.IsEnable && !string.IsNullOrEmpty(custom.DataAddress.Lable))
      {
         //写入名字
         var sendRss = true;
         short[] nameBytes = Encoding.UTF8.GetBytes(user.Name).Select(x => (short)x).ToArray();

         var nameTag = custom.DataAddress.Lable;
         for (int i = 0; i < nameBytes.Length; i++) //写入名字
         {
            if (i < 16) //PLC数组长度为20
               if (!plc.WriteValue(nameBytes[i], new SignalAddressModel($"{nameTag}[{i}]"), "[PLC用户名]写入名字"))
                  sendRss = false;
         }
         $"[PLC用户名] 名字 [{user.Name}],写入 [{nameTag}] {(sendRss ? "成功" : "失败")}".LogRun(
            sendRss ? Log4NetLevelEnum.成功 : Log4NetLevelEnum.警告
         );
      }
      else
      {
         $"[PLC用户名] 未设定相关地址或未启用，不写入PLC;".LogRun(Log4NetLevelEnum.警告);
      }
   }
   #endregion
   public void LocalLoginCMD()
   {
      UserLoginViewModel _userLoginVM = _container.Get<UserLoginViewModel>();
      _windowManager.ShowDialog(_userLoginVM);
   }

   public void LocalLogOutCMD()
   {
      UsersStatus.LocalLoggedinUser = new();
      UsersStatus.LoggedInUserType = LoggedInTypeEnum.未登陆;
   }
}

[Languages]
public enum UserLoginType
{
   [Languages("用户登陆本地")]
   用户登陆本地,

   [Languages("用户登陆MES")]
   用户登陆MES,

   [Languages("优先MES登陆")]
   优先MES登陆,
}
