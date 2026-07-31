using System.Text.Json.Nodes;
using GiteeManager.Core;
using GiteeManager.McpServer.Tools;

namespace GiteeManager.Tests;

/// <summary>IssueTools 工具层测试（AC-012 Issue 代表性场景）：创建 payload、关闭调用、错误转换。</summary>
public class IssueToolsTests
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
    public async Task IssueList_SendsFilterParams()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(200, "[]"));
        var client = CreateClient(handler);

        await IssueTools.IssueList(CreateConfig(), client, "demo", state: "open", labels: "bug");

        Assert.EndsWith("/repos/PuYiNan/demo/issues", handler.Requests[0].RequestUri!.AbsolutePath);
        var q = handler.Requests[0].RequestUri!.Query;
        Assert.Contains("state=open", q);
        Assert.Contains("labels=bug", q);
    }

    [Fact]
    public async Task IssueCreate_SendsPayloadWithTitle()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(201, """{"number":5}"""));
        var client = CreateClient(handler);

        var result = await IssueTools.IssueCreate(CreateConfig(), client, "demo", title: "bug report", labels: "bug");

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        var body = JsonNode.Parse(handler.RequireSingleBody())!;
        Assert.Equal("bug report", body["title"]!.GetValue<string>());
        Assert.Equal("bug", body["labels"]!.GetValue<string>());
        Assert.Equal(5, JsonNode.Parse(result)!["number"]!.GetValue<int>());
    }

    [Fact]
    public async Task IssueClose_SendsPatchWithClosedState()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(200, """{"state":"closed"}"""));
        var client = CreateClient(handler);

        await IssueTools.IssueClose(CreateConfig(), client, "demo", number: 5);

        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
        Assert.EndsWith("/repos/PuYiNan/demo/issues/5", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("closed", JsonNode.Parse(handler.RequireSingleBody())!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task IssueClose_AlreadyClosed_ConvertsToStructuredError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(422, """{"message":"该 Issue 已关闭"}"""));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GiteeToolException>(() =>
            IssueTools.IssueClose(CreateConfig(), client, "demo", number: 5));

        Assert.Equal("invalid_argument", JsonNode.Parse(ex.Message)!["error"]!["type"]!.GetValue<string>());
    }
}
