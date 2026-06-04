namespace Kinlo.SharedBase.Rules;

/// <summary>
/// 结果分类
/// </summary>
public static class ResultTypeClassifier
{
  /// <summary>
  /// 获取结果区间
  /// </summary>
  /// <param name="result"></param>
  /// <returns></returns>
  public static ResultArea GetResultArea(this ResultTypeEnum result)
  {
    int v = (int)result;

    if (v <= 10)
      return ResultArea.Ignore;
    if (v <= 20)
      return ResultArea.OK;
    return ResultArea.NG;
  }
}
