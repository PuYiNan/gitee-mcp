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
    public async Task StdioServer_ExposesAllEightTools()
    {
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(CreateTransportOptions("gitee-mcp-smoke-2")));

        var tools = await client.ListToolsAsync();

        var names = tools.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            ["auth_whoami", "branch_list", "repo_create", "repo_delete", "repo_get", "repo_list", "repo_search", "tag_list"],
            names);
        Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Description), $"{t.Name} 必须有非空描述"));
    }

    [Fact]
    public async Task StdioServer_BranchTagSchemasExposeParams()
    {
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(CreateTransportOptions("gitee-mcp-smoke-4")));

        var tools = await client.ListToolsAsync();
        foreach (var name in new[] { "branch_list", "tag_list" })
        {
            var tool = Assert.Single(tools, t => t.Name == name);
            var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");
            Assert.True(properties.TryGetProperty("owner", out _), $"{name} schema 缺 owner");
            Assert.True(properties.TryGetProperty("repo", out _), $"{name} schema 缺 repo");
            Assert.True(properties.TryGetProperty("sort", out _), $"{name} schema 缺 sort");
            Assert.True(properties.TryGetProperty("page", out _), $"{name} schema 缺 page");
            Assert.True(properties.TryGetProperty("per_page", out _), $"{name} schema 缺 per_page");
        }
    }

    [Fact]
    public async Task StdioServer_RepoListSchemaUsesSnakeCaseParams()
    {
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(CreateTransportOptions("gitee-mcp-smoke-3")));

        var tools = await client.ListToolsAsync();
        var repoList = Assert.Single(tools, t => t.Name == "repo_list");
        var properties = repoList.ProtocolTool.InputSchema.GetProperty("properties");

        // M2 工具参数：snake_case 命名 + 全部在 schema 中（AC-002）
        Assert.True(properties.TryGetProperty("type", out _));
        Assert.True(properties.TryGetProperty("sort", out _));
        Assert.True(properties.TryGetProperty("direction", out _));
        Assert.True(properties.TryGetProperty("page", out _));
        Assert.True(properties.TryGetProperty("per_page", out _));
        Assert.True(properties.TryGetProperty("keyword", out _));
    }
}
