using System.Text.Json.Nodes;
using GiteeManager.Core;
using GiteeManager.McpServer.Tools;

namespace GiteeManager.Tests;

/// <summary>RepoTools 工具层测试（AC-004/005/008/009）：confirm 校验、owner 缺省、per_page 钳制、错误转换。全程 MockHttp。</summary>
public class RepoToolsTests
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

    private static JsonObject ExtractError(Exception ex) =>
        JsonNode.Parse(ex.Message)!["error"]!.AsObject();    [Fact]
    public async Task RepoDelete_WithoutConfirm_DoesNotSendRequest()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("confirm=false 时不应发送请求"));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GiteeToolException>(() =>
            RepoTools.RepoDelete(CreateConfig(), client, "demo", confirm: false));

        Assert.Empty(handler.Requests);
        var err = ExtractError(ex);
        Assert.Equal("confirmation_required", err["type"]!.GetValue<string>());
        Assert.Contains("confirm", err["suggestion"]!.GetValue<string>());
    }

    [Fact]
    public async Task RepoDelete_WithoutConfirmParam_AlsoRefuses()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("缺省 confirm 时不应发送请求"));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GiteeToolException>(() =>
            RepoTools.RepoDelete(CreateConfig(), client, "demo"));

        Assert.Empty(handler.Requests);
        Assert.Equal("confirmation_required", ExtractError(ex)["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task RepoDelete_WithConfirm_SendsDeleteRequest()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(204, ""));
        var client = CreateClient(handler);

        var result = await RepoTools.RepoDelete(CreateConfig(), client, "demo", confirm: true);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.EndsWith("/repos/PuYiNan/demo", request.RequestUri!.AbsolutePath);
        var node = JsonNode.Parse(result)!;
        Assert.Equal(true, node["success"]!.GetValue<bool>());
        Assert.Contains("已删除", node["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task RepoGet_OwnerDefaultsToConfiguredUsername()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(200, """{"full_name":"PuYiNan/demo"}"""));
        var client = CreateClient(handler);

        var result = await RepoTools.RepoGet(CreateConfig(), client, "demo");

        Assert.EndsWith("/repos/PuYiNan/demo", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("PuYiNan/demo", JsonNode.Parse(result)!["full_name"]!.GetValue<string>());
    }

    [Fact]
    public async Task RepoGet_ExplicitOwnerOverridesConfig()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(200, """{"full_name":"other/demo"}"""));
        var client = CreateClient(handler);

        await RepoTools.RepoGet(CreateConfig(), client, "demo", owner: "other");

        Assert.EndsWith("/repos/other/demo", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task RepoList_ClampsPerPage()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(200, "[]"));
        var client = CreateClient(handler);

        await RepoTools.RepoList(CreateConfig(), client, perPage: 0);
        await RepoTools.RepoList(CreateConfig(), client, perPage: 101);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("per_page=20", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("per_page=100", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task RepoList_WrapsPaginatedResult()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(200, """[{"name":"a"},{"name":"b"}]"""));
        var client = CreateClient(handler);

        var result = await RepoTools.RepoList(CreateConfig(), client, page: 2, perPage: 20);

        var node = JsonNode.Parse(result)!;
        Assert.Equal(2, node["items"]!.AsArray().Count);
        Assert.Equal(2, node["page"]!.GetValue<int>());
        Assert.Equal(20, node["per_page"]!.GetValue<int>());
        Assert.Equal(2, node["returned"]!.GetValue<int>());
    }

    [Fact]
    public async Task NotFoundError_ConvertsToStructuredToolError()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(404, """{"message":"仓库不存在"}"""));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GiteeToolException>(() =>
            RepoTools.RepoGet(CreateConfig(), client, "no-such-repo"));

        var err = ExtractError(ex);
        Assert.Equal(404, err["code"]!.GetValue<int>());
        Assert.Equal("not_found", err["type"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(err["suggestion"]!.GetValue<string>()));
    }

    [Fact]
    public async Task ConflictError_OnCreate_ExtractsGiteeMessage()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(409, """{"message":"仓库已存在"}"""));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GiteeToolException>(() =>
            RepoTools.RepoCreate(CreateConfig(), client, "demo"));

        var err = ExtractError(ex);
        Assert.Equal("conflict", err["type"]!.GetValue<string>());
        Assert.Equal("仓库已存在", err["message"]!.GetValue<string>());
    }
}
