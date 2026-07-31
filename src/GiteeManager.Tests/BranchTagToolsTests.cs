using System.Text.Json.Nodes;
using GiteeManager.Core;
using GiteeManager.McpServer.Tools;

namespace GiteeManager.Tests;

/// <summary>BranchTagTools 工具层测试（AC-005/006/007/008）：owner 缺省、per_page 钳制、分页包装、错误转换。全程 MockHttp。</summary>
public class BranchTagToolsTests
{
    private const string TestToken = "tok-123";
    private const string TestApiBase = "https://gitee.com/api/v5";

    private static GiteeConfig CreateConfig() => new()
    {
        Username = "PuYiNan",
        Token = TestToken,
        ApiBase = TestApiBase
    };

    private static GiteeApiClient CreateClient(FakeHttpMessageHandler handler) =>
        new(CreateConfig(), FakeHttpMessageHandler.CreateClient(handler, TestApiBase));

    [Fact]
    public async Task BranchList_OwnerDefaultsToConfiguredUsername()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(200, """[{"name":"main"},{"name":"dev"}]"""));
        var client = CreateClient(handler);

        var result = await BranchTagTools.BranchList(CreateConfig(), client, "demo");

        Assert.EndsWith("/repos/PuYiNan/demo/branches", handler.Requests[0].RequestUri!.AbsolutePath);
        var node = JsonNode.Parse(result)!;
        Assert.Equal(2, node["items"]!.AsArray().Count);
        Assert.Equal(1, node["page"]!.GetValue<int>());
    }

    [Fact]
    public async Task BranchList_ClampsPerPage()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(200, "[]"));
        var client = CreateClient(handler);

        await BranchTagTools.BranchList(CreateConfig(), client, "demo", per_page: 0);
        await BranchTagTools.BranchList(CreateConfig(), client, "demo", per_page: 101);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("per_page=20", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("per_page=100", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task BranchList_EmptyResult_ReturnsEmptyItems()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(200, "[]"));
        var client = CreateClient(handler);

        var result = await BranchTagTools.BranchList(CreateConfig(), client, "empty-repo");

        var node = JsonNode.Parse(result)!;
        Assert.Empty(node["items"]!.AsArray());
        Assert.Equal(0, node["returned"]!.GetValue<int>());
    }

    [Fact]
    public async Task TagList_CallsTagsEndpoint()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(200, """[{"name":"v1.0.0"},{"name":"v1.1.0"}]"""));
        var client = CreateClient(handler);

        var result = await BranchTagTools.TagList(CreateConfig(), client, "demo", sort: "name");

        Assert.EndsWith("/repos/PuYiNan/demo/tags", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("sort=name", handler.Requests[0].RequestUri!.Query);
        Assert.Equal(2, JsonNode.Parse(result)!["items"]!.AsArray().Count);
    }

    [Fact]
    public async Task BranchList_RepoNotFound_ConvertsToStructuredError()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(404, """{"message":"仓库不存在"}"""));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GiteeToolException>(() =>
            BranchTagTools.BranchList(CreateConfig(), client, "no-such-repo"));

        var err = JsonNode.Parse(ex.Message)!["error"]!;
        Assert.Equal(404, err["code"]!.GetValue<int>());
        Assert.Equal("not_found", err["type"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(err["suggestion"]!.GetValue<string>()));
    }

    [Fact]
    public async Task BranchList_MissingOwner_ThrowsStructuredError()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("缺 owner 不应发送请求"));
        var config = new GiteeConfig { Token = TestToken, ApiBase = TestApiBase }; // Username 为空
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GiteeToolException>(() =>
            BranchTagTools.BranchList(config, client, "demo"));

        Assert.Empty(handler.Requests);
        Assert.Equal("missing_owner", JsonNode.Parse(ex.Message)!["error"]!["type"]!.GetValue<string>());
    }
}
