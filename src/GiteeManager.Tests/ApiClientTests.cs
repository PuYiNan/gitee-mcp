using System.Text.Json.Nodes;
using GiteeManager.Core;

namespace GiteeManager.Tests;

/// <summary>GiteeApiClient 测试（AC-005/006/008）：认证注入、错误映射、GetCurrentUserAsync 字段透传。全程 FakeHttpMessageHandler，无真实网络。</summary>
public class ApiClientTests
{
    private const string TestToken = "tok-123";
    private const string TestApiBase = "https://gitee.com/api/v5";

    private static GiteeConfig CreateConfig() => new()
    {
        Username = "PuYiNan",
        Token = TestToken,
        ApiBase = TestApiBase
    };

    [Fact]
    public async Task GetCurrentUser_InjectsAccessTokenQuery()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(200, """{"login":"PuYiNan"}"""));
        var client = new GiteeApiClient(CreateConfig(), FakeHttpMessageHandler.CreateClient(handler, TestApiBase));

        await client.GetCurrentUserAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("access_token=tok-123", request.RequestUri!.Query);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsUserJsonWithTransparentFields()
    {
        const string userJson = """{"id":42,"login":"PuYiNan","name":"浦一南","email":"u@example.com","followers":10}""";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(200, userJson));
        var client = new GiteeApiClient(CreateConfig(), FakeHttpMessageHandler.CreateClient(handler, TestApiBase));

        var result = await client.GetCurrentUserAsync();

        Assert.NotNull(result);
        Assert.Equal(42, result!["id"]!.GetValue<int>());
        Assert.Equal("PuYiNan", result["login"]!.GetValue<string>());
        Assert.Equal("浦一南", result["name"]!.GetValue<string>());
        Assert.Equal(10, result["followers"]!.GetValue<int>());
    }

    [Theory]
    [InlineData(401, "unauthorized", "私人令牌")]
    [InlineData(403, "forbidden", "权限")]
    [InlineData(404, "not_found", "owner/repo")]
    [InlineData(409, "conflict", "换名")]
    [InlineData(429, "rate_limited", "稍后重试")]
    public async Task HttpErrors_MapToStructuredException(int statusCode, string expectedType, string suggestionKeyword)
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(statusCode, """{"message":"error from gitee"}"""));
        var client = new GiteeApiClient(CreateConfig(), FakeHttpMessageHandler.CreateClient(handler, TestApiBase));

        var ex = await Assert.ThrowsAsync<GiteeApiException>(() => client.GetCurrentUserAsync());

        Assert.Equal(statusCode, ex.Code);
        Assert.Equal(expectedType, ex.Type);
        Assert.Contains(suggestionKeyword, ex.Suggestion);
        Assert.False(string.IsNullOrWhiteSpace(ex.Suggestion));
    }

    [Fact]
    public async Task ErrorBody_ExtractsGiteeMessage()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(422, """{"message":"仓库名称已存在"}"""));
        var client = new GiteeApiClient(CreateConfig(), FakeHttpMessageHandler.CreateClient(handler, TestApiBase));

        var ex = await Assert.ThrowsAsync<GiteeApiException>(() => client.GetCurrentUserAsync());

        Assert.Equal("invalid_argument", ex.Type);
        Assert.Equal("仓库名称已存在", ex.Message);
        Assert.Equal("""{"message":"仓库名称已存在"}""", ex.GiteeDetail);
    }

    [Fact]
    public void ExceptionToJson_MatchesErrorProtocolShape()
    {
        var ex = new GiteeApiException(401, "unauthorized", "私人令牌无效或已过期", "重新生成令牌", "detail");

        var json = ex.ToJson();
        var node = JsonNode.Parse(json)!;

        Assert.Equal(401, node["error"]!["code"]!.GetValue<int>());
        Assert.Equal("unauthorized", node["error"]!["type"]!.GetValue<string>());
        Assert.Equal("重新生成令牌", node["error"]!["suggestion"]!.GetValue<string>());
        Assert.Equal("detail", node["error"]!["gitee_detail"]!.GetValue<string>());
    }
}
