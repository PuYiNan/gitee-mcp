using System.Text.Json.Nodes;
using GiteeManager.Core;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace GiteeManager.Tests;

/// <summary>stdio 端到端测试（AC-003/004/005）：进程外真实 server + 本地 mock，验证 tools/call 全链路
/// （MCP 客户端 → 工具层 → GiteeApiClient → HTTP mock → 结构化 JSON）。仅连接 localhost，无真实网络。</summary>
public class E2ETests
{
    private static string ServerDllPath() =>
        typeof(GiteeManager.McpServer.Tools.AuthWhoamiTool).Assembly.Location;

    private static StdioClientTransportOptions CreateTransport(string name, string apiBase, string token, string username) => new()
    {
        Name = name,
        Command = "dotnet",
        Arguments = [ServerDllPath()],
        EnvironmentVariables = new Dictionary<string, string?>
        {
            ["DOTNET_ROLL_FORWARD"] = "LatestMajor",
            [GiteeConfig.EnvApiBase] = apiBase,
            [GiteeConfig.EnvToken] = token,
            [GiteeConfig.EnvUsername] = username
        }
    };

    private static string SingleText(CallToolResult result) =>
        Assert.Single(result.Content.OfType<TextContentBlock>()).Text;

    [Fact]
    public async Task RepoList_EndToEnd_ViaMock()
    {
        using var mock = new LocalGiteeMockServer();
        mock.Routes["/api/v5/user/repos"] = (200,
            """[{"full_name":"PuYiNan/a"},{"full_name":"PuYiNan/b"}]""");
        await using var client = await McpClient.CreateAsync(
            new StdioClientTransport(CreateTransport("e2e-repo-list", mock.BaseUrl, "fake-token", "PuYiNan")));

        var result = await client.CallToolAsync("repo_list", new Dictionary<string, object?>());

        Assert.False(result.IsError is true);
        var node = JsonNode.Parse(SingleText(result))!;
        Assert.Equal(2, node["items"]!.AsArray().Count);
        var req = Assert.Single(mock.Requests);
        Assert.Equal("GET", req.Method);
        Assert.Equal("/api/v5/user/repos", req.Path); // api_base 前缀保留（M5 修复锁定）
        Assert.Contains("access_token=fake-token", req.Query);
    }

    [Fact]
    public async Task RepoGet_NotFound_ReturnsIsErrorWithStructuredError()
    {
        using var mock = new LocalGiteeMockServer(); // 默认 404
        await using var client = await McpClient.CreateAsync(
            new StdioClientTransport(CreateTransport("e2e-not-found", mock.BaseUrl, "fake-token", "PuYiNan")));

        var result = await client.CallToolAsync("repo_get",
            new Dictionary<string, object?> { ["repo"] = "no-such" });

        Assert.True(result.IsError is true);
        var text = SingleText(result);
        Assert.Contains("not_found", text);
        Assert.Contains("suggestion", text);
    }

    [Fact]
    public async Task AuthWhoami_WithoutToken_ReturnsStructuredErrorAndServerSurvives()
    {
        await using var client = await McpClient.CreateAsync(
            new StdioClientTransport(CreateTransport("e2e-no-token", "http://127.0.0.1:9", "", "PuYiNan")));

        var result = await client.CallToolAsync("auth_whoami", new Dictionary<string, object?>());

        Assert.True(result.IsError is true);
        Assert.Contains("missing_token", SingleText(result));

        // server 未崩溃：仍可正常发现工具
        var tools = await client.ListToolsAsync();
        Assert.Equal(17, tools.Count);
    }

    [Fact]
    public async Task RepoCreate_EndToEnd_PassesRequiredNameParam()
    {
        // 诊断回归：MCP 协议层必填参数 name 必须完整到达工具（排查 pi-mcp-adapter 调用丢参问题）
        using var mock = new LocalGiteeMockServer();
        mock.Routes["/api/v5/user/repos"] = (201, """{"full_name":"park-yinan/diag-repo"}""");
        await using var client = await McpClient.CreateAsync(
            new StdioClientTransport(CreateTransport("e2e-repo-create", mock.BaseUrl, "fake-token", "PuYiNan")));

        var result = await client.CallToolAsync("repo_create",
            new Dictionary<string, object?> { ["name"] = "diag-repo", ["private"] = false });

        Assert.False(result.IsError is true);
        var req = Assert.Single(mock.Requests);
        Assert.Equal("POST", req.Method);
        Assert.Equal("/api/v5/user/repos", req.Path);
        var body = JsonNode.Parse(Assert.Single(mock.RequestBodies))!;
        Assert.Equal("diag-repo", body["name"]!.GetValue<string>());
        Assert.False(body["private"]!.GetValue<bool>());
    }
}
