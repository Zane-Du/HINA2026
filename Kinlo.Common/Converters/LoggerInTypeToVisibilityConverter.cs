namespace Kinlo.Common.Converters;

public class LoggerInTypeToVisibilityConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if (value == null)
      return Visibility.Visible;

    LoggedInTypeEnum _type = (LoggedInTypeEnum)value;
    string _str = (string)parameter;
    return (_type, _str) switch
    {
      var t when t._str == "0" && t._type == LoggedInTypeEnum.未登陆 => Visibility.Visible,
      var t when t._str == "0" && t._type != LoggedInTypeEnum.未登陆 => Visibility.Collapsed,
      var t when t._str == "1" && t._type == LoggedInTypeEnum.未登陆 => Visibility.Collapsed,
      var t when t._str == "1" && t._type != LoggedInTypeEnum.未登陆 => Visibility.Visible,
    };
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
