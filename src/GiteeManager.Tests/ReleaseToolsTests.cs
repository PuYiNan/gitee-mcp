using System.Text.Json.Nodes;
using GiteeManager.Core;
using GiteeManager.McpServer.Tools;

namespace GiteeManager.Tests;

/// <summary>ReleaseTools 工具层测试（AC-012 Release 代表性场景）：列表分页、创建 payload、冲突错误。</summary>
public class ReleaseToolsTests
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
    public async Task ReleaseList_WrapsPaginatedResult()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(200, """[{"tag_name":"v1.0.0"}]"""));
        var client = CreateClient(handler);

        var result = await ReleaseTools.ReleaseList(CreateConfig(), client, "demo", page: 2, per_page: 20);

        var node = JsonNode.Parse(result)!;
        Assert.Equal(1, node["items"]!.AsArray().Count);
        Assert.Equal(2, node["page"]!.GetValue<int>());
        Assert.EndsWith("/repos/PuYiNan/demo/releases", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ReleaseCreate_SendsPayloadWithTagName()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(201, """{"tag_name":"v1.0.0"}"""));
        var client = CreateClient(handler);

        var result = await ReleaseTools.ReleaseCreate(CreateConfig(), client, "demo", tag_name: "v1.0.0", body: "首个版本", prerelease: true);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        var body = JsonNode.Parse(Assert.Single(handler.RequestBodies))!;
        Assert.Equal("v1.0.0", body["tag_name"]!.GetValue<string>());
        Assert.Equal("首个版本", body["body"]!.GetValue<string>());
        Assert.True(body["prerelease"]!.GetValue<bool>());
        Assert.Equal("v1.0.0", JsonNode.Parse(result)!["tag_name"]!.GetValue<string>());
    }

    [Fact]
    public async Task ReleaseCreate_DuplicateTag_ConvertsToStructuredError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(409, """{"message":"标签已存在"}"""));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GiteeToolException>(() =>
            ReleaseTools.ReleaseCreate(CreateConfig(), client, "demo", tag_name: "v1.0.0"));

        var err = JsonNode.Parse(ex.Message)!["error"]!;
        Assert.Equal("conflict", err["type"]!.GetValue<string>());
        Assert.Equal("标签已存在", err["message"]!.GetValue<string>());
    }
}
