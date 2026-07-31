using GiteeManager.Core;

namespace GiteeManager.Tests;

/// <summary>GiteeConfig 配置加载与分页归一化测试（AC-002/003/004/007）。全程使用临时目录，不触碰真实环境变量与网络。</summary>
public class ConfigTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "gitee-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ClearEnv();
    }

    public void Dispose()
    {
        ClearEnv();
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // 临时目录清理失败不影响测试结论
        }
    }

    private static void ClearEnv()
    {
        Environment.SetEnvironmentVariable(GiteeConfig.EnvUsername, null);
        Environment.SetEnvironmentVariable(GiteeConfig.EnvToken, null);
        Environment.SetEnvironmentVariable(GiteeConfig.EnvApiBase, null);
    }

    private string WriteConfig(string content)
    {
        var path = Path.Combine(_tempDir, GiteeConfig.ConfigFileName);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_WithValidConfigFile_LoadsAllFields()
    {
        var path = WriteConfig(
            """{"username":"PuYiNan","token":"tok-123","api_base":"https://gitee.com/api/v5","default_per_page":30,"max_per_page":50}""");

        var config = GiteeConfig.Load(path);

        Assert.Equal("PuYiNan", config.Username);
        Assert.Equal("tok-123", config.Token);
        Assert.Equal("https://gitee.com/api/v5", config.ApiBase);
        Assert.Equal(30, config.DefaultPerPage);
        Assert.Equal(50, config.MaxPerPage);
    }

    [Fact]
    public void Load_WithoutConfigFile_UsesDefaultsAndEnvFallback()
    {
        // 无 config.json：环境变量提供全部必填项
        Environment.SetEnvironmentVariable(GiteeConfig.EnvUsername, "env-user");
        Environment.SetEnvironmentVariable(GiteeConfig.EnvToken, "env-tok");

        var config = GiteeConfig.Load(Path.Combine(_tempDir, "not-exist.json"));

        Assert.Equal("env-user", config.Username);
        Assert.Equal("env-tok", config.Token);
        Assert.Equal(GiteeConfig.DefaultApiBase, config.ApiBase);
    }

    [Fact]
    public void Load_EnvVarsOverrideConfigFile()
    {
        var path = WriteConfig(
            """{"username":"file-user","token":"file-tok","api_base":"https://file.example.com"}""");
        Environment.SetEnvironmentVariable(GiteeConfig.EnvUsername, "env-user");
        Environment.SetEnvironmentVariable(GiteeConfig.EnvToken, "env-tok");
        Environment.SetEnvironmentVariable(GiteeConfig.EnvApiBase, "https://env.example.com");

        var config = GiteeConfig.Load(path);

        Assert.Equal("env-user", config.Username);
        Assert.Equal("env-tok", config.Token);
        Assert.Equal("https://env.example.com", config.ApiBase);
    }

    [Fact]
    public void Load_MissingToken_ThrowsStructuredError()
    {
        var path = WriteConfig("""{"username":"PuYiNan"}""");

        var ex = Assert.Throws<GiteeApiException>(() => GiteeConfig.Load(path));

        Assert.Equal("missing_token", ex.Type);
        Assert.Contains("GITEE_TOKEN", ex.Suggestion);
        Assert.Contains("config.json", ex.Suggestion);
    }

    [Fact]
    public void Load_MissingUsername_ThrowsStructuredError()
    {
        Environment.SetEnvironmentVariable(GiteeConfig.EnvToken, "env-tok");
        var path = WriteConfig("""{"token":"file-tok"}""");

        var ex = Assert.Throws<GiteeApiException>(() => GiteeConfig.Load(path));

        Assert.Equal("missing_username", ex.Type);
        Assert.Contains("GITEE_USERNAME", ex.Suggestion);
    }

    [Fact]
    public void Load_InvalidJson_ThrowsStructuredError()
    {
        var path = WriteConfig("{ not valid json ");

        var ex = Assert.Throws<GiteeApiException>(() => GiteeConfig.Load(path));

        Assert.Equal("invalid_config", ex.Type);
    }

    [Fact]
    public void NormalizePerPage_ClampsAndDefaults()
    {
        var config = new GiteeConfig();

        Assert.Equal(20, config.NormalizePerPage(0));   // 默认值
        Assert.Equal(20, config.NormalizePerPage(-5));  // 负值 → 默认值
        Assert.Equal(50, config.NormalizePerPage(50));  // 正常值原样
        Assert.Equal(100, config.NormalizePerPage(101)); // 超过上限 → 钳制
        Assert.Equal(100, config.NormalizePerPage(1000)); // 远超上限 → 钳制
    }
}
