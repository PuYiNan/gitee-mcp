using GiteeManager.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

// ===== 工具序列化选项（snake_case 参数名 + net8 TypeInfoResolver）=====
var toolSerializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    TypeInfoResolver = new DefaultJsonTypeInfoResolver()
};

// ===== 局部函数 =====
int? ParsePort(string[] a)
{
    for (var i = 1; i < a.Length - 1; i++)
    {
        if (a[i] == "--port" && int.TryParse(a[i + 1], out var port))
        {
            return port;
        }
    }
    return null;
}

void RegisterCoreServices(IServiceCollection services)
{
    GiteeConfig? config = null;
    try
    {
        config = GiteeConfig.Load();
    }
    catch (GiteeApiException)
    {
        // 配置缺失/无效时照常启动；调用需要认证的工具时返回结构化错误
    }
    services.AddSingleton(config ?? new GiteeConfig());
    services.AddSingleton<GiteeApiClient>();
}

async Task<int> RunHttpServerAsync(string[] a)
{
    var port = ParsePort(a) ?? 8080;
    var builder = WebApplication.CreateBuilder(a);
    builder.Services.AddLogging(logging => logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace));
    RegisterCoreServices(builder.Services);
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.Stateless = true)
        .WithToolsFromAssembly(serializerOptions: toolSerializerOptions);
    var app = builder.Build();
    app.MapMcp();
    // 安全：默认仅监听 loopback（DNS 重绑定防护）；如需对外暴露由用户自行承担风险
    await app.RunAsync(url: $"http://127.0.0.1:{port}");
    return 0;
}

// ===== 参数分发 =====
if (args.Length > 0 && args[0] != "serve")
{
    Console.Error.WriteLine("用法：gitee-mcp [serve --port <端口>]");
    Console.Error.WriteLine("  （默认 stdio 模式；serve 以 HTTP 模式启动 MCP Server，仅监听 127.0.0.1）");
    return 1;
}

if (args.Length > 0 && args[0] == "serve")
{
    return await RunHttpServerAsync(args);
}

// ===== stdio 模式（默认）=====
var stdioBuilder = Host.CreateApplicationBuilder(args);
stdioBuilder.Logging.AddConsole(consoleLogOptions =>
{
    // MCP stdio 要求日志输出到 stderr，避免污染 stdout 协议通道
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
RegisterCoreServices(stdioBuilder.Services);
stdioBuilder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(serializerOptions: toolSerializerOptions);

await stdioBuilder.Build().RunAsync();
return 0;
