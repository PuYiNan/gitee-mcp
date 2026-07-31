using GiteeManager.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace GiteeManager.McpServer.Tools;

/// <summary>认证域工具：验证私人令牌并返回当前用户信息。</summary>
[McpServerToolType]
public class AuthWhoamiTool
{
    [McpServerTool]
    [Description("验证 Gitee 私人令牌的有效性，返回当前用户信息（JSON）。用于确认配置的令牌可用、以及后续操作以哪个账户身份执行。")]
    public static async Task<string> AuthWhoami(
        GiteeConfig config,
        GiteeApiClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            // 配置无效（缺 token / 用户名）时直接返回结构化错误，不发起网络请求
            config.ValidateOrThrow();
            var user = await client.GetCurrentUserAsync(cancellationToken);
            return user?.ToJsonString() ?? "{}";
        }
        catch (GiteeApiException ex)
        {
            // 以 IsError=true 的工具结果返回结构化错误 JSON，AI 可直接读取并采取建议动作
            throw new GiteeToolException(ex.ToJson());
        }
    }
}

/// <summary>工具级错误：消息承载结构化错误 JSON（{ error: { code, type, message, suggestion, gitee_detail } }）。
/// MCP SDK 会将继承 McpException 的异常消息原样放入 IsError=true 的工具结果，供 AI 读取。</summary>
internal sealed class GiteeToolException(string jsonError) : McpException(jsonError);
