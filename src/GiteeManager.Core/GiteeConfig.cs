using System.Text.Json;
using System.Text.Json.Serialization;

namespace GiteeManager.Core;

/// <summary>
/// gitee-mcp 配置：从 config.json（exe 同目录）加载，环境变量（GITEE_USERNAME / GITEE_TOKEN / GITEE_API_BASE）覆盖。
/// 完全可编程化，无任何人机交互。
/// </summary>
public sealed class GiteeConfig
{
    public const string DefaultApiBase = "https://gitee.com/api/v5";
    public const string ConfigFileName = "config.json";
    public const string EnvUsername = "GITEE_USERNAME";
    public const string EnvToken = "GITEE_TOKEN";
    public const string EnvApiBase = "GITEE_API_BASE";

    public string Username { get; set; } = "";

    public string Token { get; set; } = "";

    public string ApiBase { get; set; } = DefaultApiBase;

    public int DefaultPerPage { get; set; } = 20;

    public int MaxPerPage { get; set; } = 100;

    /// <summary>加载配置：config.json（缺省为 exe 同目录）+ 环境变量覆盖 + 校验。文件不存在时使用默认值。</summary>
    public static GiteeConfig Load(string? configPath = null)
    {
        var path = configPath ?? Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        var config = new GiteeConfig();

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var fileConfig = JsonSerializer.Deserialize<ConfigFile>(json, JsonOptions);
                if (fileConfig is not null)
                {
                    config.Username = fileConfig.Username ?? "";
                    config.Token = fileConfig.Token ?? "";
                    if (!string.IsNullOrWhiteSpace(fileConfig.ApiBase))
                    {
                        config.ApiBase = fileConfig.ApiBase!;
                    }
                    if (fileConfig.DefaultPerPage is > 0)
                    {
                        config.DefaultPerPage = fileConfig.DefaultPerPage.Value;
                    }
                    if (fileConfig.MaxPerPage is > 0)
                    {
                        config.MaxPerPage = fileConfig.MaxPerPage.Value;
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new GiteeApiException(
                    0, "invalid_config",
                    $"config.json 解析失败：{ex.Message}",
                    $"检查 {path} 是否为有效 JSON");
            }
        }

        // 环境变量覆盖（优先级高于 config.json）
        var envUser = Environment.GetEnvironmentVariable(EnvUsername);
        var envToken = Environment.GetEnvironmentVariable(EnvToken);
        var envApiBase = Environment.GetEnvironmentVariable(EnvApiBase);
        if (!string.IsNullOrWhiteSpace(envUser))
        {
            config.Username = envUser;
        }
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            config.Token = envToken;
        }
        if (!string.IsNullOrWhiteSpace(envApiBase))
        {
            config.ApiBase = envApiBase;
        }

        config.ValidateOrThrow();
        return config;
    }

    /// <summary>校验配置：缺少 token / 用户名时抛结构化错误，消息给出设置途径。</summary>
    public void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new GiteeApiException(
                0, "missing_token",
                "缺少 Gitee 私人令牌",
                $"设置环境变量 {EnvToken}，或在 exe 同目录的 {ConfigFileName} 中配置 token");
        }
        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new GiteeApiException(
                0, "missing_username",
                "缺少 Gitee 用户名",
                $"设置环境变量 {EnvUsername}，或在 exe 同目录的 {ConfigFileName} 中配置 username");
        }
    }

    /// <summary>归一化分页：0/负值 → 默认值；超过 MaxPerPage → 钳制到 MaxPerPage。</summary>
    public int NormalizePerPage(int perPage) =>
        perPage <= 0 ? DefaultPerPage : Math.Min(perPage, MaxPerPage);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>config.json 文件结构（snake_case 键名）。</summary>
    private sealed class ConfigFile
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("api_base")]
        public string? ApiBase { get; set; }

        [JsonPropertyName("default_per_page")]
        public int? DefaultPerPage { get; set; }

        [JsonPropertyName("max_per_page")]
        public int? MaxPerPage { get; set; }
    }
}
