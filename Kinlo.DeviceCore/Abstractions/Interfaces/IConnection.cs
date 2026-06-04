namespace Kinlo.DeviceCore.Abstractions.Interfaces;

/// <summary>
/// 表示一个“基础通信连接抽象”
/// 用于封装 TCP / UDP / Serial / Socket 等底层通信能力
///
/// 核心职责：
/// 1. 管理连接生命周期（Connect / Disconnect）
/// 2. 提供字节流级别的收发能力（byte[] IO）
/// 3. 提供异步事件驱动数据接收机制
/// 4. 不涉及任何协议解析或业务逻辑
/// </summary>
/// <typeparam name="TOptions">
/// 连接配置类型（如 TcpOptions / SerialOptions）
///
/// 用于描述连接所需的基础参数，例如：
/// - IP / Port（TCP/UDP）
/// - COM Port / BaudRate（Serial）
/// - Timeout / BufferSize 等
///
/// 注意：
/// 该泛型仅用于“连接层配置”，不包含协议或业务语义
/// </typeparam>
public interface IConnection<TOptions> : IDisposable
{
  /// <summary>
  /// 异步建立连接
  /// 根据 TOptions 初始化底层通信通道
  /// </summary>
  /// <param name="options">连接配置参数</param>
  /// <param name="token">取消令牌，用于中断连接过程</param>
  Task ConnectAsync(TOptions options, CancellationToken token = default);

  /// <summary>
  /// 异步断开连接
  /// 会释放底层 socket / serial 资源
  /// </summary>
  Task DisconnectAsync();

  /// <summary>
  /// 当前连接是否处于可用状态
  /// 用于判断通信链路是否已建立
  /// </summary>
  bool IsConnected { get; }

  /// <summary>
  /// 发送原始字节数据
  /// 注意：
  /// - 仅负责 IO 写入
  /// - 不负责协议封装
  /// </summary>
  /// <param name="data">待发送的原始字节数据</param>
  /// <param name="token">取消令牌</param>
  /// <returns>实际发送的字节数</returns>
  Task<int> SendAsync(byte[] data, CancellationToken token = default);

  /// <summary>
  /// 接收原始字节数据
  /// 注意：
  /// - 返回的是未解析的原始 byte[]
  /// - 协议解析应由 Protocol 层完成
  /// </summary>
  /// <param name="token">取消令牌</param>
  /// <returns>接收到的字节数据</returns>
  Task<byte[]> ReceiveAsync(CancellationToken token = default);

  /// <summary>
  /// 数据接收事件（推送模式）
  /// 当底层收到数据时触发
  /// 常用于：
  /// - 扫码枪（主动上报）
  /// - UDP广播
  /// - 异步IO场景
  /// </summary>
  event Action<byte[]>? DataReceived;

  /// <summary>
  /// 连接断开事件
  /// 用于通知上层（Device / Runtime）进行：
  /// - 重连策略
  /// - 状态切换
  /// - 告警触发
  /// </summary>
  event Action? Disconnected;
}
