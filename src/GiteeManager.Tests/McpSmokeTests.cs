using GiteeManager.Core;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace GiteeManager.Tests;

/// <summary>MCP stdio 冒烟测试（AC-009）：进程外启动真实 server，验证握手与工具发现。
/// 使用假 token，tools/list 阶段不访问网络；不调用会触发真实 API 的工具。</summary>
public class McpSmokeTests
{
    private static string ServerDllPath() =>
        typeof(GiteeManager.McpServer.Tools.AuthWhoamiTool).Assembly.Location;

    private static StdioClientTransportOptions CreateTransportOptions(string name) => new()
    {
        Name = name,
        Command = "dotnet",
        Arguments = [ServerDllPath()],
        EnvironmentVariables = new Dictionary<string, string?>
        {
            // 本机仅有 .NET 10 运行时：让 net8.0 server 进程 roll forward
            ["DOTNET_ROLL_FORWARD"] = "LatestMajor",
            // 假 token：仅用于让配置校验通过；冒烟阶段不调用 auth_whoami，不发网络请求
            [GiteeConfig.EnvToken] = "smoke-test-token",
            [GiteeConfig.EnvUsername] = "smoke-test-user"
        }
    };

    [Fact]
    public async Task StdioServer_HandshakeAndDiscoversAuthWhoami()
    {
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(CreateTransportOptions("gitee-mcp-smoke")));

        // initialize 握手在 CreateAsync 内完成，成功即通过
        var tools = await client.ListToolsAsync();

        var whoami = Assert.Single(tools, t => t.Name == "auth_whoami");
        Assert.False(string.IsNullOrWhiteSpace(whoami.Description), "auth_whoami 必须有非空描述");
        var schema = whoami.ProtocolTool.InputSchema;
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.True(schema.TryGetProperty("properties", out _), "auth_whoami 的 inputSchema 必须含 properties");
    }

    [Fact]
    public async Task StdioServer_ToolListOnlyIncludesRegisteredTools()
    {
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(CreateTransportOptions("gitee-mcp-smoke-2")));

        var tools = await client.ListToolsAsync();

        Assert.Single(tools); // M1 仅注册 auth_whoami
        Assert.Equal("auth_whoami", tools[0].Name);
    }
}
