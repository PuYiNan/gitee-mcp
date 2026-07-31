using GiteeManager.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace GiteeManager.McpServer.Tools;

/// <summary>分支/标签域工具：列出仓库分支与标签。</summary>
[McpServerToolType]
public class BranchTagTools
{
    [McpServerTool]
    [Description("列出指定 Gitee 仓库的分支，支持按名称/更新时间排序与分页。返回 { items, page, per_page, returned }。")]
    public static async Task<string> BranchList(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        [Description("排序方式：name(名称)/updated(最近更新)")] string? sort = null,
        [Description("页码（从 1 开始）")] int? page = null,
        [Description("每页数量（默认 20，最大 100）")] int? per_page = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var resolvedOwner = ToolHelpers.ResolveOwner(config, owner);
            var node = await client.GetBranchesAsync(resolvedOwner, repo, sort, page, per_page, cancellationToken);
            return ToolHelpers.WrapPaginated(node, page ?? 1, per_page ?? config.DefaultPerPage);
        });
    }

    [McpServerTool]
    [Description("列出指定 Gitee 仓库的标签，支持按名称/更新时间排序与分页。返回 { items, page, per_page, returned }。")]
    public static async Task<string> TagList(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        [Description("排序方式：name(名称)/updated(最近更新)")] string? sort = null,
        [Description("页码（从 1 开始）")] int? page = null,
        [Description("每页数量（默认 20，最大 100）")] int? per_page = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var resolvedOwner = ToolHelpers.ResolveOwner(config, owner);
            var node = await client.GetTagsAsync(resolvedOwner, repo, sort, page, per_page, cancellationToken);
            return ToolHelpers.WrapPaginated(node, page ?? 1, per_page ?? config.DefaultPerPage);
        });
    }
}
