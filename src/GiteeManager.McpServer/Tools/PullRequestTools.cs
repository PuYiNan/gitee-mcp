using GiteeManager.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace GiteeManager.McpServer.Tools;

/// <summary>PR 域工具：列表 / 详情 / 创建 / 合并。</summary>
[McpServerToolType]
public static class PullRequestTools
{
    [McpServerTool]
    [Description("列出指定 Gitee 仓库的 PR，支持按 state/head/base 筛选与分页。返回 { items, page, per_page, returned }。")]
    public static async Task<string> PrList(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        [Description("PR 状态：open/closed/all/merged")] string? state = null,
        [Description("源分支筛选")] string? head = null,
        [Description("目标分支筛选")] string? @base = null,
        [Description("页码（从 1 开始）")] int? page = null,
        [Description("每页数量（默认 20，最大 100）")] int? per_page = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var resolvedOwner = ToolHelpers.ResolveOwner(config, owner);
            var node = await client.GetPullsAsync(resolvedOwner, repo, state, head, @base, page, per_page, cancellationToken);
            return ToolHelpers.WrapPaginated(node, page ?? 1, per_page ?? config.DefaultPerPage);
        });
    }

    [McpServerTool]
    [Description("获取单个 Gitee PR 的详情。")]
    public static async Task<string> PrGet(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("PR 编号")] int number,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var node = await client.GetPullAsync(ToolHelpers.ResolveOwner(config, owner), repo, number, cancellationToken);
            return node?.ToJsonString() ?? "{}";
        });
    }

    [McpServerTool]
    [Description("在指定 Gitee 仓库创建 PR。title/head/base 必填。")]
    public static async Task<string> PrCreate(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("PR 标题（必填）")] string title,
        [Description("源分支（必填，如 dev 或 user:dev）")] string head,
        [Description("目标分支（必填，如 master）")] string @base,
        [Description("PR 描述")] string? body = null,
        [Description("标签，逗号分隔（如 bug,feature）")] string? labels = null,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var payload = new JsonObject
            {
                ["title"] = title,
                ["head"] = head,
                ["base"] = @base
            };
            if (body is not null) payload["body"] = body;
            if (labels is not null) payload["labels"] = labels;

            var node = await client.CreatePullAsync(ToolHelpers.ResolveOwner(config, owner), repo, payload, cancellationToken);
            return node?.ToJsonString() ?? "{}";
        });
    }

    [McpServerTool]
    [Description("合并 Gitee PR。merge_method: merge/squash/rebase，默认 merge。已合并的 PR 重复合并会返回错误。")]
    public static async Task<string> PrMerge(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("PR 编号")] int number,
        [Description("合并方式：merge/squash/rebase，默认 merge")] string? merge_method = null,
        [Description("合并提交信息")] string? message = null,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var node = await client.MergePullAsync(
                ToolHelpers.ResolveOwner(config, owner), repo, number,
                merge_method ?? "merge", message, cancellationToken);
            return node?.ToJsonString() ?? """{"merged":true}""";
        });
    }
}
