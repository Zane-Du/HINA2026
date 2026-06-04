namespace Kinlo.Common.Models.ConfigModels.MesConfigModels;

[AddINotifyPropertyChangedInterface]
public class MesInterfaceCollectionModel
{
  /// <summary>
  /// HttpClient 基地址
  /// </summary>
  public string BaseAddress { get; set; } = string.Empty;

  /// <summary>
  /// HttpClient 基地址2
  /// </summary>
  public string BaseAddress2 { get; set; } = string.Empty;

  /// <summary>
  /// MES本地网口IP
  /// </summary>
  public string LocalMesIP { get; set; } = string.Empty;

  /// <summary>
  /// MES等待响应超时时间,单位为毫秒
  /// </summary>
  public int Timeout { get; set; } = 5000;

  public ObservableCollection<MesInterfaceInfoModel> MesParameterItems { get; set; } =
    new ObservableCollection<MesInterfaceInfoModel>();
}
