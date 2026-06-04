namespace Kinlo.Common.Model;

[AddINotifyPropertyChangedInterface]
public class QueryParameterModel
{
  public string Description { get; set; } = string.Empty;
  public string PropertyName { get; set; } = string.Empty;

  private bool _isOk;

  public bool IsOk
  {
    get { return _isOk; }
    set
    {
      if (_isOk != value)
      {
        _isOk = value;
        if (value)
        {
          IsNg = false;
          Station = 1;
        }
        else
        {
          if (!IsNg)
            Station = 0;
        }
      }
    }
  }

  private bool _isNg;

  public bool IsNg
  {
    get { return _isNg; }
    set
    {
      if (_isNg != value)
      {
        _isNg = value;
        if (value)
        {
          IsOk = false;
          Station = 2;
        }
        else
        {
          if (!IsOk)
            Station = 0;
        }
      }
    }
  }

  /// <summary>
  /// 0未选择，1:OK,2:ng
  /// </summary>
  public int Station { get; set; }
}
