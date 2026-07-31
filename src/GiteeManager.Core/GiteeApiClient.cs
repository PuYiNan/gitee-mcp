using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace GiteeManager.Core;

/// <summary>
/// Gitee API v5 客户端：统一注入 access_token 查询参数、分页归一化、结构化错误映射。
/// 返回 JsonNode（透传 Gitee 原始字段，零遗漏，AOT 友好）。
/// </summary>
public sealed class GiteeApiClient
{
    private readonly HttpClient _http;
    private readonly GiteeConfig _config;

    public GiteeApiClient(GiteeConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _http = httpClient ?? new HttpClient();
        _http.BaseAddress = new Uri(config.ApiBase);
    }

    /// <summary>获取当前用户（GET /user），auth_whoami 工具的后端能力。</summary>
    public Task<JsonNode?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, "/user", cancellationToken);

    /// <summary>列出当前账户仓库（GET /user/repos），支持筛选、排序、分页与关键词过滤。</summary>
    public Task<JsonNode?> GetUserReposAsync(
        string? type = null,
        string? sort = null,
        string? direction = null,
        int? page = null,
        int? perPage = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, object?>();
        if (type is not null) query["type"] = type;
        if (sort is not null) query["sort"] = sort;
        if (direction is not null) query["direction"] = direction;
        if (page is not null) query["page"] = page;
        if (perPage is not null) query["per_page"] = _config.NormalizePerPage(perPage.Value);
        if (keyword is not null) query["q"] = keyword;
        return SendAsync(HttpMethod.Get, "/user/repos", cancellationToken, query);
    }

    /// <summary>获取仓库详情（GET /repos/{owner}/{repo}）。</summary>
    public Task<JsonNode?> GetRepoAsync(string owner, string repo, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"/repos/{Escape(owner)}/{Escape(repo)}", cancellationToken);

    /// <summary>全局搜索仓库（GET /search/repositories）。</summary>
    public Task<JsonNode?> SearchReposAsync(
        string query,
        int? page = null,
        int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var q = new Dictionary<string, object?> { ["q"] = query };
        if (page is not null) q["page"] = page;
        if (perPage is not null) q["per_page"] = _config.NormalizePerPage(perPage.Value);
        return SendAsync(HttpMethod.Get, "/search/repositories", cancellationToken, q);
    }

    /// <summary>创建仓库（POST /user/repos）。payload 为 Gitee 创建仓库参数字段（JsonObject）。</summary>
    public Task<JsonNode?> CreateRepoAsync(JsonObject payload, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, "/user/repos", cancellationToken, body: payload);

    /// <summary>删除仓库（DELETE /repos/{owner}/{repo}）。调用方必须已完成 confirm 校验。</summary>
    public Task<JsonNode?> DeleteRepoAsync(string owner, string repo, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, $"/repos/{Escape(owner)}/{Escape(repo)}", cancellationToken);

    private static string Escape(string value) => Uri.EscapeDataString(value);

    /// <summary>发送请求：注入 access_token、分页钳制、错误映射、JSON 透传。</summary>
    public async Task<JsonNode?> SendAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken = default,
        Dictionary<string, object?>? query = null,
        JsonNode? body = null)
    {
        var uri = BuildUri(path, query);
        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw MapError((int)response.StatusCode, responseBody);
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        return JsonNode.Parse(responseBody);
    }

    /// <summary>构造请求 URI：BaseAddress + 路径 + access_token 注入 + 自定义查询参数。</summary>
    private Uri BuildUri(string path, Dictionary<string, object?>? query)
    {
        var baseUri = new Uri(_config.ApiBase);
        var builder = new UriBuilder(baseUri) { Path = path.TrimStart('/') };

        var parts = new List<string> { $"access_token={Uri.EscapeDataString(_config.Token)}" };
        if (query is not null)
        {
            foreach (var (key, value) in query)
            {
                if (value is null)
                {
                    continue;
                }
                var valueStr = value switch
                {
                    string s => s,
                    bool b => b ? "true" : "false",
                    int i => i.ToString(),
                    _ => value.ToString() ?? ""
                };
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(valueStr)}");
            }
        }

        builder.Query = string.Join("&", parts);
        return builder.Uri;
    }

    /// <summary>HTTP 状态码 → 结构化错误（code/type/message/suggestion/gitee_detail）。</summary>
    private static GiteeApiException MapError(int statusCode, string body)
    {
        var detail = string.IsNullOrWhiteSpace(body) ? null : body;
        var (type, suggestion, defaultMessage) = statusCode switch
        {
            401 => ("unauthorized", "重新生成 Gitee 私人令牌，检查 GITEE_TOKEN / config.json", "私人令牌无效或已过期"),
            403 => ("forbidden", "检查私人令牌权限范围是否包含对应操作", "无权限执行该操作"),
            404 => ("not_found", "检查 owner/repo 拼写是否正确", "资源不存在"),
            400 or 422 => ("invalid_argument", "按错误详情修正参数", "参数不合法"),
            409 => ("conflict", "换名或先删除旧资源", "资源冲突"),
            429 => ("rate_limited", "稍后重试", "触发 Gitee API 频率限制"),
            _ => ("http_error", "根据状态码排查请求", $"HTTP {(int)statusCode}")
        };

        // 尝试提取 Gitee 返回体中的 message 字段作为更具体的错误消息
        string? message = null;
        try
        {
            if (JsonNode.Parse(body) is JsonObject obj && obj["message"]?.GetValue<string>() is { } m)
            {
                message = m;
            }
        }
        catch
        {
            // 非 JSON 错误体，忽略
        }

        return new GiteeApiException(statusCode, type, message ?? defaultMessage, suggestion, detail);
    }
}
