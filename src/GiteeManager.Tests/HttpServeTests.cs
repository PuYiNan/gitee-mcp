using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using GiteeManager.Core;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace GiteeManager.Tests;

/// <summary>HTTP serve 模式测试（AC-006/007/008）：进程外 serve 启动、仅监听 127.0.0.1、HTTP 客户端全链路、无效参数退出。</summary>
public class HttpServeTests : IDisposable
{
    private static string ServerDllPath() =>
        typeof(GiteeManager.McpServer.Tools.AuthWhoamiTool).Assembly.Location;

    private readonly List<Process> _processes = [];

    private Process StartServe(int port, string apiBase, string? token = "fake-token")
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            Arguments = $"\"{ServerDllPath()}\" serve --port {port}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.Environment["DOTNET_ROLL_FORWARD"] = "LatestMajor";
        psi.Environment[GiteeConfig.EnvApiBase] = apiBase;
        psi.Environment[GiteeConfig.EnvToken] = token;
        psi.Environment[GiteeConfig.EnvUsername] = "PuYiNan";
        var process = Process.Start(psi)!;
        _processes.Add(process);
        return process;
    }

    private static async Task WaitForPortAsync(int port, int attempts = 40)
    {
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                return;
            }
            catch
            {
                await Task.Delay(250);
            }
        }
        throw new TimeoutException($"端口 {port} 在等待时间内未就绪");
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        foreach (var p in _processes)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                }
                p.Dispose();
            }
            catch
            {
                // 清理失败不影响测试结论
            }
        }
    }

    [Fact]
    public async Task Serve_ListensOnLoopbackAndExposesSeventeenTools()
    {
        using var mock = new LocalGiteeMockServer();
        var port = GetFreePort();
        StartServe(port, mock.BaseUrl);
        await WaitForPortAsync(port);

        // 监听地址验证：netstat 中该端口的 LISTENING 行，本地地址列必须绑定 127.0.0.1
        var netstat = await RunAndReadAsync("netstat", "-ano");
        var listenLines = netstat.Split('\n')
            .Where(l => l.Contains($":{port}", StringComparison.OrdinalIgnoreCase) && l.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(listenLines);
        Assert.All(listenLines, l =>
        {
            // netstat 行格式：TCP  <本地地址>  <远程地址>  LISTENING  <PID>；仅校验本地地址列
            var parts = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(parts.Length >= 5, $"netstat 行格式异常: {l}");
            Assert.StartsWith("127.0.0.1:", parts[1]);
        });

        // HTTP 客户端连接：tools/list 17 工具（MCP 端点映射在根路径 /）
        await using var client = await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri($"http://127.0.0.1:{port}/")
            }));

        var tools = await client.ListToolsAsync();
        Assert.Equal(17, tools.Count);
    }

    [Fact]
    public async Task Serve_RepoList_EndToEndViaMock()
    {
        using var mock = new LocalGiteeMockServer();
        mock.Routes["/api/v5/user/repos"] = (200,
            """[{"full_name":"PuYiNan/http-demo"}]""");
        var port = GetFreePort();
        StartServe(port, mock.BaseUrl);
        await WaitForPortAsync(port);

        await using var client = await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri($"http://127.0.0.1:{port}/")
            }));

        var result = await client.CallToolAsync("repo_list", new Dictionary<string, object?>());

        Assert.False(result.IsError is true);
        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        Assert.Single(JsonNode.Parse(text)!["items"]!.AsArray());
        var req = Assert.Single(mock.Requests);
        Assert.Equal("/api/v5/user/repos", req.Path);
    }

    [Fact]
    public async Task InvalidArgument_PrintsUsageAndExitsNonZero()
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            Arguments = $"\"{ServerDllPath()}\" bad-argument",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.Environment["DOTNET_ROLL_FORWARD"] = "LatestMajor";
        using var process = Process.Start(psi)!;

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("用法", stderr);
    }

    private static async Task<string> RunAndReadAsync(string command, string args)
    {
        var psi = new ProcessStartInfo(command, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}
