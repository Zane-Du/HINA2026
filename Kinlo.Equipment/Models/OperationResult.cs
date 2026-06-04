namespace Kinlo.Equipment.Models;

public class CommResult
{
  public CommState State { get; set; }
  public string Message { get; set; } = string.Empty;

  public static CommResult Ok() => new() { State = CommState.Success };

  public static CommResult Fail(CommState state, string msg) => new() { State = state, Message = msg };
}

public class CommResult<T>
{
  public CommState State { get; set; }
  public T? Data { get; set; }
  public string Message { get; set; } = string.Empty;

  public static CommResult<T> Ok(T data) => new() { State = CommState.Success, Data = data };

  public static CommResult<T> Fail(CommState state, string msg) => new() { State = state, Message = msg };
}

public enum CommState
{
  /// <summary>
  /// 成功
  /// </summary>
  Success = 0, // 成功

  /// <summary>
  /// 业务失败（PLC返回错误、数据错误）
  /// </summary>
  Failed = 1, // 业务失败（PLC返回错误、数据错误）

  /// <summary>
  /// 通信链路异常，需要重连
  /// </summary>
  NeedReconnect = 2, // 通信链路异常，需要重连
}

/// <summary>
/// 目前用于仪器读取返回，后期也可以扩展到其它
/// </summary>
/// <typeparam name="T"></typeparam>
public class OperationResult<T>
{
  public bool IsSuccess { get; init; }
  public T? Value { get; init; }
  public ResultTypeEnum ErrCode { get; set; }
  public string? ErrorMessage { get; init; }
  public Exception? Exception { get; init; }

  public static OperationResult<T> Success(T value) =>
    new()
    {
      IsSuccess = true,
      ErrCode = ResultTypeEnum.OK,
      Value = value,
    };

  public static OperationResult<T> Failure(string message, Exception? ex = null) =>
    new()
    {
      IsSuccess = false,
      ErrCode = ResultTypeEnum.NG,
      ErrorMessage = message,
      Exception = ex,
    };

  public static OperationResult<T> Failure(ResultTypeEnum errCode, string message, Exception? ex = null) =>
    new()
    {
      IsSuccess = false,
      ErrorMessage = message,
      ErrCode = errCode,
      Exception = ex,
    };
}
