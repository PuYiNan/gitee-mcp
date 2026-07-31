using System.Text.Json.Nodes;
using GiteeManager.Core;
using GiteeManager.McpServer.Tools;

namespace GiteeManager.Tests;

/// <summary>PullRequestTools 工具层测试（AC-012 PR 代表性场景）：owner 缺省、per_page 钳制、错误转换、创建/合并调用。</summary>
public class PullRequestToolsTests
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
    public async Task PrList_OwnerDefaultsToConfigAndClampsPerPage()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(200, "[]"));
        var client = CreateClient(handler);

        await PullRequestTools.PrList(CreateConfig(), client, "demo", per_page: 0);
        await PullRequestTools.PrList(CreateConfig(), client, "demo", per_page: 101);

        Assert.EndsWith("/repos/PuYiNan/demo/pulls", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("per_page=20", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("per_page=100", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task PrCreate_SendsPayloadWithRequiredFields()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(201, """{"number":13}"""));
        var client = CreateClient(handler);

        var result = await PullRequestTools.PrCreate(CreateConfig(), client, "demo", title: "feat", head: "dev", @base: "master", body: "说明");

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        var body = JsonNode.Parse(Assert.Single(handler.RequestBodies))!;
        Assert.Equal("feat", body["title"]!.GetValue<string>());
        Assert.Equal("dev", body["head"]!.GetValue<string>());
        Assert.Equal("master", body["base"]!.GetValue<string>());
        Assert.Equal("说明", body["body"]!.GetValue<string>());
        Assert.Equal(13, JsonNode.Parse(result)!["number"]!.GetValue<int>());
    }

    [Fact]
    public async Task PrMerge_DefaultsToMergeMethod()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(200, """{"merged":true}"""));
        var client = CreateClient(handler);

        await PullRequestTools.PrMerge(CreateConfig(), client, "demo", number: 12);

        var body = JsonNode.Parse(Assert.Single(handler.RequestBodies))!;
        Assert.Equal("merge", body["merge_method"]!.GetValue<string>());
    }

    [Fact]
    public async Task PrGet_NotFound_ConvertsToStructuredError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(404, """{"message":"PR 不存在"}"""));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GiteeToolException>(() =>
            PullRequestTools.PrGet(CreateConfig(), client, "demo", number: 99));

        var err = JsonNode.Parse(ex.Message)!["error"]!;
        Assert.Equal("not_found", err["type"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(err["suggestion"]!.GetValue<string>()));
    }
}
