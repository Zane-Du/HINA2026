using LiveChartsCore.SkiaSharpView.Painting;

namespace Kinlo.GUI.Helpers;

public static class Common
{
  public static SolidColorPaint TextPaint { get; set; } =
    new SolidColorPaint() { Color = SKColors.DarkSlateGray, SKTypeface = SKFontManager.Default.MatchCharacter('汉') };
}
