using GiteeManager.Core;
using ModelContextProtocol;
using System.Text.Json.Nodes;

namespace GiteeManager.McpServer.Tools;

/// <summary>工具层共享辅助：统一错误转换、owner 解析、分页包装。</summary>
internal static class ToolHelpers
{
    /// <summary>统一错误转换：Gitee 结构化异常 → IsError 工具结果（消息承载错误协议 JSON）。</summary>
    public static async Task<string> ExecuteAsync(Func<Task<string>> action)
    {
        try
        {
            return await action();
        }
        catch (GiteeApiException ex)
        {
            throw new GiteeToolException(ex.ToJson());
        }
    }

    /// <summary>解析仓库所有者：显式 owner 优先，缺省用配置用户名；均无则抛结构化错误。</summary>
    public static string ResolveOwner(GiteeConfig config, string? owner)
    {
        var resolved = owner ?? config.Username;
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new GiteeApiException(
                0, "missing_owner",
                "缺少仓库所有者",
                "显式传 owner 参数，或在 config.json / GITEE_USERNAME 中配置用户名");
        }
        return resolved;
    }

    /// <summary>分页包装：统一输出 { items, page, per_page, returned }。</summary>
    public static string WrapPaginated(JsonNode? node, int page, int per_page)
    {
        var items = node as JsonArray ?? node?["items"] as JsonArray ?? new JsonArray();
        return new JsonObject
        {
            ["items"] = items,
            ["page"] = page,
            ["per_page"] = per_page,
            ["returned"] = items.Count
        }.ToJsonString();
    }
}
