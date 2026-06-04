namespace Kinlo.GUI.View
{
  /// <summary>
  /// UpdateReplenishVolumeView.xaml 的交互逻辑
  /// </summary>
  public partial class WeighingElectrolyteReplenishView : Window
  {
    public WeighingElectrolyteReplenishView()
    {
      InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
      ((WeighingElectrolyteReplenishViewModel)this.DataContext).CancelCMD();
      base.OnClosed(e);
    }
  }
}
