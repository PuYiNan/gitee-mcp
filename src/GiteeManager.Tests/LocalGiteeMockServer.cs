using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GiteeManager.Tests;

/// <summary>本地 Gitee API mock（HttpListener，仅 127.0.0.1 随机端口）：按路径前缀返回预设响应并记录请求。
/// 用于端到端测试，避免真实网络。</summary>
public sealed class LocalGiteeMockServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    /// <summary>Base URL（含路径前缀，如 http://127.0.0.1:PORT/api/v5）。</summary>
    public string BaseUrl { get; }

    /// <summary>收到的请求记录：(方法, 路径, 查询串)。</summary>
    public List<(string Method, string Path, string Query)> Requests { get; } = [];

    /// <summary>收到的请求体记录（与 Requests 同序，无 body 时为 null）。</summary>
    public List<string?> RequestBodies { get; } = [];

    /// <summary>路径前缀 → (HTTP 状态码, JSON 响应体)。未匹配路由默认 404。</summary>
    public Dictionary<string, (int Status, string Json)> Routes { get; } = [];

    public LocalGiteeMockServer(string basePath = "/api/v5")
    {
        _listener = new HttpListener();
        var port = GetFreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        BaseUrl = $"http://127.0.0.1:{port}{basePath}";
        _listener.Start();
        _loop = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch
            {
                break; // 监听器停止
            }

            var path = ctx.Request.Url!.AbsolutePath;
            var query = ctx.Request.Url.Query;
            Requests.Add((ctx.Request.HttpMethod, path, query));

            string? requestBody = null;
            if (ctx.Request.HasEntityBody)
            {
                using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                requestBody = await reader.ReadToEndAsync();
            }
            RequestBodies.Add(requestBody);

            var (status, json) = Routes
                .FirstOrDefault(r => path.StartsWith(r.Key, StringComparison.OrdinalIgnoreCase))
                .Value;
            var body = Encoding.UTF8.GetBytes(json ?? "{}");
            ctx.Response.StatusCode = status == 0 ? 404 : status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
