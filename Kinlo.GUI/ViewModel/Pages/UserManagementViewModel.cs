using Kinlo.SharedBase.Interfacs;

namespace Kinlo.GUI.ViewModel;

[Languages(["用户管理", "Pengelolaan Pengguna", "User management"], IsScanProperty = false)]
[UIDisplayAttribute(true, 21, ((ulong)1) << 62, isRunEdit: true, "\xe660")]
public class UserManagementViewModel : Screen, IMenu
{
  IContainer _container;
  IWindowManager _windowManager;
  public UsersStatusConfig UsersStatus { get; set; }

  public UserManagementViewModel(IContainer container, IWindowManager windowManager)
  {
    _container = container;
    _windowManager = windowManager;
    UsersStatus = container.Get<UsersStatusConfig>();
  }

  /// <summary>
  /// 新增用户
  /// </summary>
  public void CreateUserCMD()
  {
    UserRegisterViewModel _userLoginVM = _container.Get<UserRegisterViewModel>();
    _userLoginVM.User = new UserModel { Account = "", Name = "" };
    _userLoginVM.LoginType = LoginTypeEnum.用户注册;
    _windowManager.ShowDialog(_userLoginVM);
  }

  /// <summary>
  ///  修改用户
  /// </summary>
  public void UpeateUserCMD(UserModel user)
  {
    UserModel _user = new UserModel
    {
      Account = user.Account,
      Name = user.Name,
      Password = user.Password,
      FingerprintID = user.FingerprintID,
      //MESPassword = user.MESPassword,
      //MESAccount = user.MESAccount,
      Role = user.Role,
      LoginTime = user.LoginTime,
      RegisterTime = user.RegisterTime,
      Tel = user.Tel,
      UpdateTime = user.UpdateTime,
    };
    UserRegisterViewModel _userLoginVM = _container.Get<UserRegisterViewModel>();
    _userLoginVM.User = _user;
    _userLoginVM.LoginType = LoginTypeEnum.修改用户;
    _windowManager.ShowDialog(_userLoginVM);
  }

  /// <summary>
  ///  删除用户
  /// </summary>
  public void DeleteUserCMD(UserModel user)
  {
    var _dialog = MessageBox.Show(
      $"确定删除用户:[{user.Account}]?",
      "警告:",
      MessageBoxButton.OKCancel,
      MessageBoxImage.Warning
    );
    if (_dialog != MessageBoxResult.OK)
      return;
    ((UserManagementView)this.View).dataGrid.UnselectAll();
    UsersStatus.DeleteUser(user);
  }

  public void Load()
  {
    UsersStatus.IsAutoLogin = false;
  }

  public bool Unload() => UsersStatus.IsAutoLogin = true;
}
