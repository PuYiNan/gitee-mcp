using GiteeManager.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace GiteeManager.McpServer.Tools;

/// <summary>Release 域工具：列表 / 创建。</summary>
[McpServerToolType]
public static class ReleaseTools
{
    [McpServerTool]
    [Description("列出指定 Gitee 仓库的 Release，支持分页。返回 { items, page, per_page, returned }。")]
    public static async Task<string> ReleaseList(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        [Description("页码（从 1 开始）")] int? page = null,
        [Description("每页数量（默认 20，最大 100）")] int? per_page = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var resolvedOwner = ToolHelpers.ResolveOwner(config, owner);
            var node = await client.GetReleasesAsync(resolvedOwner, repo, page, per_page, cancellationToken);
            return ToolHelpers.WrapPaginated(node, page ?? 1, per_page ?? config.DefaultPerPage);
        });
    }

    [McpServerTool]
    [Description("在指定 Gitee 仓库创建 Release。tag_name 必填。相同 tag 重复创建会返回冲突错误。")]
    public static async Task<string> ReleaseCreate(
        GiteeConfig config,
        GiteeApiClient client,
        [Description("仓库名")] string repo,
        [Description("版本标签（必填，如 v1.0.0）")] string tag_name,
        [Description("Release 名称，缺省为标签名")] string? name = null,
        [Description("Release 说明")] string? body = null,
        [Description("目标提交/分支（缺省为默认分支）")] string? target_commitish = null,
        [Description("是否预发布")] bool prerelease = false,
        [Description("仓库所有者，缺省为配置的用户名")] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolHelpers.ExecuteAsync(async () =>
        {
            var payload = new JsonObject
            {
                ["tag_name"] = tag_name,
                ["prerelease"] = prerelease
            };
            if (name is not null) payload["name"] = name;
            if (body is not null) payload["body"] = body;
            if (target_commitish is not null) payload["target_commitish"] = target_commitish;

            var node = await client.CreateReleaseAsync(ToolHelpers.ResolveOwner(config, owner), repo, payload, cancellationToken);
            return node?.ToJsonString() ?? "{}";
        });
    }
}
