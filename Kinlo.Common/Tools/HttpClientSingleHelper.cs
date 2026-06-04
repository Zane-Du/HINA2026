using Kinlo.LogNet.Models;
using Kinlo.SharedBase.Model;

namespace Kinlo.Common.Tools;

/// <summary>
/// httpclient 务必使用单例
/// </summary>
public class HttpClientSingleHelper
{
  private SocketsHttpHandler socketsHttpHandler = new SocketsHttpHandler()
  {
    UseCookies = false, // 是否自动处理cookie
    SslOptions = new System.Net.Security.SslClientAuthenticationOptions()
    {
      RemoteCertificateValidationCallback = (sender, cer, chain, err) => true,
    },

    //ConnectTimeout = Timeout.InfiniteTimeSpan, //建立TCP连接时的超时时间,默认不限制
    //Expect100ContinueTimeout = TimeSpan.FromSeconds(1),  //等待服务返回statusCode=100的超时时间,默认1秒
    //AllowAutoRedirect = true,//是否自动重定向
    //MaxAutomaticRedirections = 50//自动重定向的最大次数
    //MaxConnectionsPerServer = 100, //每个请求连接的最大数量，默认是int.MaxValue,可以认为是不限制
    //PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),//连接池中TCP连接最多可以闲置多久,默认2分钟
    //PooledConnectionLifetime = Timeout.InfiniteTimeSpan, //连接最长的存活时间,默认是不限制的,一般不用设置、
    //AutomaticDecompression = DecompressionMethods.GZip, //是否压缩，默认是None，即不压缩
    //MaxResponseHeadersLength = 64, //响应头数据大小限制,单位: KB默认：64，即：http响应头最大64KB，一般不用设置
  };
  public HttpClient HttpClientSingle;

  public HttpClientSingleHelper(StyletIoC.IContainer container)
  {
    MesInterfaceParameterConfig _mesParameter = container.Get<MesInterfaceParameterConfig>();
    HttpClientSingle = new HttpClient(socketsHttpHandler);
    // HttpClientSingle.BaseAddress = new Uri(_mesParameter.MesInterfaceInfo.BaseAddress);
    HttpClientSingle.Timeout = TimeSpan.FromMilliseconds(_mesParameter.MesInterfaceInfo.Timeout);
  }

  /// <summary>
  ///
  /// </summary>
  /// <param name="parameters">上传的参数字典</param>
  /// <param name="barcode">条码，如果有的话</param>
  /// <returns></returns>
  public async Task<(bool isSuccess, string content)> PostAsync(string _url, HttpContent sendMessage)
  {
    try
    {
      // StringContent _stringContent = new StringContent(sendMessage, Encoding.UTF8, "application/json");
      var response = await HttpClientSingle.PostAsync(_url, sendMessage);
      // _response.EnsureSuccessStatusCode();//如果不返回200就抛出异常
      var statusCode = response.StatusCode; //返回状态码
      var header = response.Headers; //返回响应头
      string responseBody = await response.Content.ReadAsStringAsync();
      return (true, responseBody ??= string.Empty);
    }
    catch (Exception ex)
    {
      return (false, ex.Message);
    }
  }
}
