using GiteeManager.Core;
using GiteeManager.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

// gitee-mcp：AI 原生可调用的 Gitee 仓库管理工具（MCP Server，默认 stdio 传输）。
// 启动不依赖有效配置：工具发现（tools/list）不访问网络；认证问题在工具调用时返回结构化错误。
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // MCP stdio 要求日志输出到 stderr，避免污染 stdout 协议通道
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

GiteeConfig? config = null;
try
{
    config = GiteeConfig.Load();
}
catch (GiteeApiException)
{
    // 配置缺失/无效时照常启动；调用需要认证的工具时返回结构化错误（见 AuthWhoamiTool）
}

builder.Services.AddSingleton(config ?? new GiteeConfig());
builder.Services.AddSingleton<GiteeApiClient>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(serializerOptions: new JsonSerializerOptions
    {
        // 工具参数名使用 snake_case（如 per_page），与 Gitee API 参数惯例一致
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // net8 下 SDK 要求显式 TypeInfoResolver（否则 MakeReadOnly 抛异常）
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    });

await builder.Build().RunAsync();
