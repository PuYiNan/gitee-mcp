using System.Text.Json;

namespace GiteeManager.Core;

/// <summary>Gitee API 结构化异常：可序列化为 { error: { code, type, message, suggestion, gitee_detail } }，供 AI 读取并采取建议动作。</summary>
public sealed class GiteeApiException : Exception
{
    public int Code { get; }

    public string Type { get; }

    public string Suggestion { get; }

    public string? GiteeDetail { get; }

    public GiteeApiException(int code, string type, string message, string suggestion, string? giteeDetail = null)
        : base(message)
    {
        Code = code;
        Type = type;
        Suggestion = suggestion;
        GiteeDetail = giteeDetail;
    }

    /// <summary>序列化为结构化 JSON（snake_case 键名，与方案文档错误协议一致）。</summary>
    public string ToJson()
    {
        var payload = new Dictionary<string, object?>
        {
            ["error"] = new Dictionary<string, object?>
            {
                ["code"] = Code,
                ["type"] = Type,
                ["message"] = Message,
                ["suggestion"] = Suggestion,
                ["gitee_detail"] = GiteeDetail
            }
        };
        return JsonSerializer.Serialize(payload);
    }
}
