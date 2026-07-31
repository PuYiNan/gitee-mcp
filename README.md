# gitee-mcp — AI 原生可调用的 Gitee 仓库管理工具

通过 [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) 让 AI 客户端（PI / Claude / Cursor 等）直接管理你的 Gitee 账户与代码仓库：列出、创建、删除仓库，管理分支、标签、PR、Issue、Release。

- **AI 原生**：17 个 MCP 工具自动发现，结构化 JSON 输出，确定性错误协议（AI 可读并可自我修复）
- **可移植**：单文件 exe（Native AOT，约 22MB），目标机**零 .NET 运行时依赖**，拷贝文件夹即迁移
- **认证**：HTTPS + Gitee 私人令牌（Personal Access Token），可编程化注入

## 快速开始

### 1. 获取私人令牌

Gitee → 设置 → 安全设置 → **私人令牌** → 生成新令牌（勾选 `projects` 权限即可）。

> ⚠️ 令牌只显示一次，妥善保管；不要提交到代码仓库或写入笔记。

### 2. 配置

复制 `config.example.json` 为 `config.json`（与 exe 同目录），填入用户名与令牌：

```json
{
  "username": "你的Gitee用户名",
  "token": "你的私人令牌",
  "api_base": "https://gitee.com/api/v5"
}
```

也支持环境变量覆盖（优先级更高，适合脚本/CI）：`GITEE_USERNAME`、`GITEE_TOKEN`、`GITEE_API_BASE`。

### 3. 接入 AI 客户端

**PI**（mcp-config.example.json 为完整样例）：

```json
{
  "mcpServers": {
    "gitee-mcp": {
      "command": "D:/tools/gitee-mcp/gitee-mcp.exe",
      "args": [],
      "env": { "GITEE_TOKEN": "你的私人令牌" }
    }
  }
}
```

连接后 AI 即可调用 17 个工具管理你的 Gitee。

## 两种运行模式

| 模式 | 命令 | 适用场景 |
|------|------|---------|
| **stdio**（默认） | `gitee-mcp` | AI 客户端本地拉起，零网络，最安全 |
| **serve**（HTTP） | `gitee-mcp serve --port 8080` | 常驻服务，多客户端/远程访问 |

> ⚠️ serve 模式**仅监听 127.0.0.1**（本机），无鉴权。如需对外暴露（如内网 VPN 场景），请自行置于可信网络并承担风险。

## 工具清单（17 个）

| 类别 | 工具 |
|------|------|
| 认证 | `auth_whoami` |
| 仓库 | `repo_list` `repo_get` `repo_search` `repo_create` `repo_delete` |
| 分支/标签 | `branch_list` `tag_list` |
| PR | `pr_list` `pr_get` `pr_create` `pr_merge` |
| Issue | `issue_list` `issue_create` `issue_close` |
| Release | `release_list` `release_create` |

- 危险操作 `repo_delete` 必须显式传 `confirm: true` 才执行
- 所有列表支持分页（`per_page` 默认 20，最大 100）与结构化错误（`{ error: { code, type, message, suggestion } }`）

## 迁移到另一台电脑

1. 拷贝 `gitee-mcp.exe` + `config.json`（或设环境变量）到目标机任意目录
2. 目标机**无需安装 .NET 运行时**
3. 按第 3 步配置 AI 客户端

## 从源码构建

```powershell
# 开发
dotnet build GiteeManager.slnx

# 测试（无需真实令牌）
dotnet test src/GiteeManager.Tests

# 发布（AOT 优先，失败自动退自包含单文件）
./publish.ps1
```

## 安全说明

- 令牌仅存于本机 `config.json` 或环境变量，建议配置文件仅本人可读
- serve 模式仅监听 127.0.0.1；日志走 stderr，不输出令牌
- 本项目不收集任何使用数据

## 项目结构

```
src/GiteeManager.Core/       # Gitee API v5 客户端（认证注入、错误映射）
src/GiteeManager.McpServer/  # MCP Server（stdio + serve 双模式，17 工具）
src/GiteeManager.Tests/      # 71+ 测试（MockHttp + 本地 mock 端到端，无真实网络）
```

## 许可与说明

个人工具项目，按需使用。Gitee 商标与 API 归 Gitee 所有。
