using GiteeManager.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace GiteeManager.McpServer.Tools;

/// <summary>Issue 域工具：列表 / 创建 / 关闭。</summary>
[McpServerToolType]
public static class IssueTools
{
    [McpServerTool]
    [Description("列出指定 Gitee 仓库的 Issue，支持按 state/labels 筛选与分页。返回 { items, page, per_page, returned }。")]
    public static async Task<string> IssueList(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        [Description("Issue 状态：open/closed/all")] string? state = null,
        [Description("标签筛选，逗号分隔（如 bug,feature）")] string? labels = null,
        [Description("页码（从 1 开始）")] int? page = null,
        [Description("每页数量（默认 20，最大 100）")] int? per_page = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var resolvedOwner = ToolHelpers.ResolveOwner(config, owner);
            var node = await client.GetIssuesAsync(resolvedOwner, repo, state, labels, page, per_page, cancellationToken);
            return ToolHelpers.WrapPaginated(node, page ?? 1, per_page ?? config.DefaultPerPage);
        });
    }

    [McpServerTool]
    [Description("在指定 Gitee 仓库创建 Issue。title 必填。")]
    public static async Task<string> IssueCreate(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("Issue 标题（必填）")] string title,
        [Description("Issue 描述")] string? body = null,
        [Description("标签，逗号分隔（如 bug,feature）")] string? labels = null,
        [Description("指派用户，逗号分隔")] string? assignees = null,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var payload = new JsonObject { ["title"] = title };
            if (body is not null) payload["body"] = body;
            if (labels is not null) payload["labels"] = labels;
            if (assignees is not null) payload["assignees"] = assignees;

            var node = await client.CreateIssueAsync(ToolHelpers.ResolveOwner(config, owner), repo, payload, cancellationToken);
            return node?.ToJsonString() ?? "{}";
        });
    }

    [McpServerTool]
    [Description("关闭指定 Gitee 仓库的 Issue（PATCH state=closed）。已关闭的 Issue 重复关闭会返回错误。")]
    public static async Task<string> IssueClose(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("Issue 编号")] int number,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var node = await client.CloseIssueAsync(ToolHelpers.ResolveOwner(config, owner), repo, number, cancellationToken);
            return node?.ToJsonString() ?? "{}";
        });
    }
}
