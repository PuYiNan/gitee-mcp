using GiteeManager.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace GiteeManager.McpServer.Tools;

/// <summary>仓库域工具：列出 / 详情 / 搜索 / 创建 / 删除。</summary>
[McpServerToolType]
public class RepoTools
{
    [McpServerTool]
    [Description("列出当前账户的 Gitee 仓库，支持类型筛选、排序与关键词过滤（分页）。返回 { items, page, per_page, returned }。")]
    public static async Task<string> RepoList(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库类型：owner(我创建的)/personal(个人)/member(我参与的)/public(公开)/private(私有)")] string? type = null,
        [Description("排序字段：full_name/created/updated/pushed")] string? sort = null,
        [Description("排序方向：asc/desc")] string? direction = null,
        [Description("页码（从 1 开始）")] int? page = null,
        [Description("每页数量（默认 20，最大 100）")] int? per_page = null,
        [Description("关键词过滤")] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var node = await client.GetUserReposAsync(type, sort, direction, page, per_page, keyword, cancellationToken);
            return ToolHelpers.WrapPaginated(node, page ?? 1, per_page ?? config.DefaultPerPage);
        });
    }

    [McpServerTool]
    [Description("获取单个 Gitee 仓库的完整详情。owner 缺省时使用配置的用户名。")]
    public static async Task<string> RepoGet(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var node = await client.GetRepoAsync(ToolHelpers.ResolveOwner(config, owner), repo, cancellationToken);
            return node?.ToJsonString() ?? "{}";
        });
    }

    [McpServerTool]
    [Description("在 Gitee 全站搜索仓库。返回透传 Gitee 结果（含 total_count 与 items）。")]
    public static async Task<string> RepoSearch(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("搜索关键词（必填）")] string q,
        [Description("页码（从 1 开始）")] int? page = null,
        [Description("每页数量（默认 20，最大 100）")] int? per_page = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var node = await client.SearchReposAsync(q, page, per_page, cancellationToken);
            return node?.ToJsonString() ?? """{"total_count":0,"items":[]}""";
        });
    }

    [McpServerTool]
    [Description("在 Gitee 创建仓库。仅 name 必填，其余参数可选。")]
    public static async Task<string> RepoCreate(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名称（必填；字母、数字、下划线、连字符，不超过 100 字符）")] string name,
        [Description("仓库描述")] string? description = null,
        [Description("是否私有仓库")] bool @private = false,
        [Description("是否自动初始化 README")] bool auto_init = false,
        [Description("Git 忽略模板，如 VisualStudio")] string? gitignore_template = null,
        [Description("开源许可证模板，如 MIT")] string? license_template = null,
        [Description("项目主页 URL")] string? homepage = null,
        [Description("是否启用 Issue")] bool? has_issues = null,
        [Description("是否启用 Wiki")] bool? has_wiki = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var payload = new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["private"] = @private,
                ["auto_init"] = auto_init
            };
            if (gitignore_template is not null) payload["gitignore_template"] = gitignore_template;
            if (license_template is not null) payload["license_template"] = license_template;
            if (homepage is not null) payload["homepage"] = homepage;
            if (has_issues is not null) payload["has_issues"] = has_issues;
            if (has_wiki is not null) payload["has_wiki"] = has_wiki;

            var node = await client.CreateRepoAsync(payload, cancellationToken);
            return node?.ToJsonString() ?? "{}";
        });
    }

    [McpServerTool]
    [Description("删除 Gitee 仓库（危险操作）。必须显式传 confirm=true 才会执行，否则拒绝。")]
    public static async Task<string> RepoDelete(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("删除确认开关：必须显式传 true 才会执行删除")] bool confirm = false,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            if (confirm != true)
            {
                throw new GiteeApiException(
                    0, "confirmation_required",
                    "删除仓库是危险操作，必须显式传 confirm=true 才会执行",
                    "确认删除后重试，传入 confirm=true");
            }

            var resolvedOwner = ToolHelpers.ResolveOwner(config, owner);
            await client.DeleteRepoAsync(resolvedOwner, repo, cancellationToken);
            return $$"""{"success":true,"message":"仓库 {{resolvedOwner}}/{{repo}} 已删除"}""";
        });
    }
}
