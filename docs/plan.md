# gitee-mcp — 方案细化文档

> 版本：v0.1（方案细化稿） | 状态：待确认后动工
> 定位：一个"AI 原生可调用"的 Gitee 仓库管理工具（MCP Server），可移植、低依赖、认证走 HTTPS + 私人令牌。

---

## 1. 项目概述

- **名称**：gitee-mcp
- **形态**：MCP Server（Model Context Protocol），AI 客户端（PI / Claude / Cursor 等）可直接连接并自动发现全部能力
- **传输**：默认 **stdio**（本地进程，零网络最安全）；可选 **HTTP**（`gitee-mcp serve --port 8080`，供远程/多客户端）
- **可移植性**：发布为单文件 exe（自包含，目标机零 .NET 运行时依赖），`config.json` 与 exe 同目录，**整个文件夹拷贝即迁移**
- **认证**：Gitee 私人令牌（Personal Access Token），HTTPS + `access_token`（Gitee API v5 官方认证方式）

## 2. 技术栈

| 项 | 选型 | 理由 |
|----|------|------|
| 语言/框架 | **.NET 8.0（LTS）目标框架** + C#（本机 SDK 10 编译 net8.0 目标） | 兼容面最广，目标机运行时覆盖 8.0+；自包含发布则与运行时无关 |
| MCP SDK | `ModelContextProtocol` 2.0.0（微软官方 NuGet） | **已实测兼容 net8.0**（临时项目编译通过）；stdio + HTTP 双传输原生支持 |
| JSON | `System.Text.Json`（JsonNode 透传） | AOT 友好，无需反射，字段零遗漏 |
| CLI 解析 | 无（MCP 模式不需要；调试辅助用简单参数） | 减少依赖 |
| 测试 | xUnit + 假 HttpClient（MockHttp） | API 客户端可离线测试 |
| 发布 | `dotnet publish -r win-x64 --self-contained`；优先 Native AOT（体积 10-15MB），失败退单文件自包含（~70MB） | 目标机零依赖 |

## 3. 架构（三层）

```
┌─────────────────────────────────────────────────┐
│ AI 客户端（PI / Claude / Cursor / ...）           │
│   通过 MCP 协议发现工具 + 自动生成调用              │
└───────────────┬─────────────────────────────────┘
                │ stdio（默认）/ HTTP（serve 模式）
┌───────────────▼─────────────────────────────────┐
│ GiteeManager.McpServer（MCP Server 层）           │
│   工具注册：每个 MCP 工具 = 名称+描述+JSON Schema+Handler │
│   错误 → MCP isError 结果（结构化 JSON）           │
└───────────────┬─────────────────────────────────┘
┌───────────────▼─────────────────────────────────┐
│ GiteeManager.Core（业务核心，可测、与 MCP 解耦）    │
│   GiteeConfig    —— 配置加载（config.json + 环境变量）│
│   GiteeApiClient —— HttpClient 封装 + access_token 注入 │
│                    + 错误映射 + 分页                │
│   Models         —— 以 JsonNode 透传 + 关键字段强类型 │
└─────────────────────────────────────────────────┘
```

## 4. 配置设计（可移植性核心）

`config.json`（**exe 同目录**，首次运行自动生成）：

```json
{
  "username": "PuYiNan",
  "token": "你的私人令牌",
  "api_base": "https://gitee.com/api/v5",
  "default_per_page": 20,
  "max_per_page": 100
}
```

**环境变量覆盖（优先级高于 config.json）**：
- `GITEE_USERNAME` → username
- `GITEE_TOKEN` → token（推荐 CI/脚本场景用）
- `GITEE_API_BASE` → api_base

配置完全可编程化，无任何人机交互。

## 5. Gitee API v5 接口清单

Base: `https://gitee.com/api/v5`，认证：`?access_token=<token>`（统一由 ApiClient 注入）。

| 功能 | 端点 | 方法 | 关键参数 |
|------|------|------|----------|
| 当前用户 | `/user` | GET | — |
| 仓库列表（授权用户） | `/user/repos` | GET | type(owner/personal/member/public/private), sort(created/updated/pushed/full_name), direction, page, per_page |
| 仓库详情 | `/repos/{owner}/{repo}` | GET | — |
| 搜索仓库 | `/search/repositories` | GET | q, page, per_page |
| 创建仓库 | `/user/repos` | POST | name*, description, private, has_issues, has_wiki, auto_init, gitignore_template, license_template, homepage, path |
| 删除仓库 | `/repos/{owner}/{repo}` | DELETE | — |
| 分支列表 | `/repos/{owner}/{repo}/branches` | GET | sort(name/updated), page, per_page |
| 标签列表 | `/repos/{owner}/{repo}/tags` | GET | sort(name/updated), page, per_page |
| PR 列表 | `/repos/{owner}/{repo}/pulls` | GET | state(open/closed/all/merged), head, base, sort, direction, page, per_page |
| PR 详情 | `/repos/{owner}/{repo}/pulls/{number}` | GET | — |
| 创建 PR | `/repos/{owner}/{repo}/pulls` | POST | title*, head*, base*, body, labels |
| 合并 PR | `/repos/{owner}/{repo}/pulls/{number}/merge` | PUT | merge_method(merge/squash/rebase), message |
| Issue 列表 | `/repos/{owner}/{repo}/issues` | GET | state(open/closed/all), labels, sort, direction, page, per_page |
| 创建 Issue | `/repos/{owner}/{repo}/issues` | POST | title*, body, labels, assignees |
| 关闭 Issue | `/repos/{owner}/{repo}/issues/{number}` | PATCH | state: "closed" |
| Release 列表 | `/repos/{owner}/{repo}/releases` | GET | page, per_page, direction |
| 创建 Release | `/repos/{owner}/{repo}/releases` | POST | tag_name*, name, body, target_commitish, prerelease |

* 分页：`page` 从 1 开始；`per_page` 默认 20，最大 100。*
* 标注 `*` 为必填。*

## 6. MCP 工具规格（17 个）

> 每个工具 = 名称 + 描述（给 AI 看）+ inputSchema（JSON Schema）+ 输出（JSON 字符串的 TextContent）。
> 输出**一律 JSON**，无交互提示；危险操作用参数开关而非确认框。

### 6.1 认证
| 工具 | 描述 | 输入 | 输出 |
|------|------|------|------|
| `auth_whoami` | 验证私人令牌有效性并返回当前用户信息 | 无 | User 对象 |

### 6.2 仓库（repo）
| 工具 | 描述 | 输入（JSON Schema） | 输出 |
|------|------|------|------|
| `repo_list` | 列出当前账户的仓库 | type(enum, 默认 personal), sort(enum), direction(enum), page:int, per_page:int, keyword:str | 仓库数组 + 分页信息 |
| `repo_get` | 获取仓库详情 | owner*, repo* | 仓库对象 |
| `repo_search` | 全局搜索仓库 | q*, page, per_page | 结果数组 + total_count |
| `repo_create` | 创建仓库 | name*(pattern: 字母数字-_，≤100), description, private:bool=false, auto_init:bool, gitignore_template, license_template, homepage, has_issues:bool, has_wiki:bool | 新仓库对象 |
| `repo_delete` | 删除仓库（**必须显式传 confirm:true**） | owner*, repo*, confirm*:bool | 删除成功消息 |

### 6.3 分支/标签
| 工具 | 描述 | 输入 | 输出 |
|------|------|------|------|
| `branch_list` | 仓库分支列表 | owner*, repo*, sort(enum), page, per_page | 分支数组 |
| `tag_list` | 仓库标签列表 | owner*, repo*, sort(enum), page, per_page | 标签数组 |

### 6.4 PR
| 工具 | 描述 | 输入 | 输出 |
|------|------|------|------|
| `pr_list` | 仓库 PR 列表 | owner*, repo*, state(enum), head, base, page, per_page | PR 数组 |
| `pr_get` | PR 详情 | owner*, repo*, number*:int | PR 对象 |
| `pr_create` | 创建 PR | owner*, repo*, title*, head*, base*, body, labels[] | PR 对象 |
| `pr_merge` | 合并 PR | owner*, repo*, number*, merge_method(enum: merge/squash/rebase), message | 合并结果 |

### 6.5 Issue
| 工具 | 描述 | 输入 | 输出 |
|------|------|------|------|
| `issue_list` | 仓库 Issue 列表 | owner*, repo*, state(enum), labels[], page, per_page | Issue 数组 |
| `issue_create` | 创建 Issue | owner*, repo*, title*, body, labels[] | Issue 对象 |
| `issue_close` | 关闭 Issue | owner*, repo*, number* | Issue 对象（state=closed） |

### 6.6 Release
| 工具 | 描述 | 输入 | 输出 |
|------|------|------|------|
| `release_list` | Release 列表 | owner*, repo*, page, per_page | Release 数组 |
| `release_create` | 创建 Release | owner*, repo*, tag_name*, name, body, target_commitish, prerelease:bool | Release 对象 |

> 所有 `owner` 可省略（默认取 config.username）；`page` 默认 1，`per_page` 默认 20。

## 7. 错误处理协议

**统一错误结构**（MCP isError 结果，内容为 JSON）：

```json
{
  "error": {
    "code": 401,
    "type": "unauthorized",
    "message": "私人令牌无效或已过期",
    "suggestion": "检查 GITEE_TOKEN / config.json 中的 token 是否正确",
    "gitee_detail": "原始错误信息（如有）"
  }
}
```

**HTTP 状态 → 类型映射**：

| 状态码 | type | message | suggestion |
|--------|------|---------|------------|
| 401 | unauthorized | 令牌无效/过期 | 重新生成私人令牌 |
| 403 | forbidden | 无权限 | 检查 token 权限范围是否包含对应操作 |
| 404 | not_found | 资源不存在 | 检查 owner/repo 拼写 |
| 400/422 | invalid_argument | 参数不合法 | 按错误详情修正参数 |
| 409 | conflict | 资源冲突（如仓库重名） | 换名或先删除旧仓库 |
| 429 | rate_limited | 触发频率限制 | 稍后重试 |
| 网络异常 | network_error | 连接失败 | 检查网络/API 地址 |

## 8. 幂等与安全设计

- **读操作**天然幂等；**repo_delete / pr_merge / issue_close / release_create** 等写操作重复执行由 Gitee 状态码兜底（如 PR 已合并 → 返回明确错误而非崩溃）
- `repo_delete` 强制 `confirm: true`（Schema 层必填），**绝无交互式确认**
- token 不出本机进程（stdio 模式）；HTTP 模式启动时打印安全提示（仅监听 localhost 时允许无鉴权，绑定非 localhost 需 `--require-auth` 提示或文档声明风险）
- 所有 list 类工具返回分页信息，避免单次输出撑爆 AI 上下文

## 9. 项目结构

```
D:/LXCODE/PIAgent/GiteeManager/
├── GiteeManager.sln
├── src/
│   ├── GiteeManager.Core/
│   │   ├── GiteeConfig.cs          # 配置加载（config.json + 环境变量合并）
│   │   ├── GiteeApiClient.cs       # HttpClient + access_token 注入 + 错误映射
│   │   ├── GiteeApiException.cs    # 结构化 API 异常
│   │   ├── Models/                 # User/Repo/Branch/Tag/PullRequest/Issue/Release
│   │   └── (JsonNode 透传，保留全部原始字段)
│   ├── GiteeManager.McpServer/
│   │   ├── Program.cs              # 入口：stdio（默认）/ serve --port（HTTP）
│   │   ├── GiteeToolFactory.cs     # 工具注册表（17 个工具）
│   │   ├── Tools/                  # 每个工具一个类：Name/Description/Schema/Handler
│   │   └── McpErrorMapper.cs       # API 异常 → MCP isError JSON
│   └── GiteeManager.Tests/
│       ├── ConfigTests.cs          # 配置优先级、缺 token 报错
│       ├── ApiClientTests.cs       # MockHttp：认证注入、错误映射、分页
│       └── ToolSchemaTests.cs      # 17 个工具的 Schema 完整性
├── config.example.json
├── mcp-config.example.json         # 各 AI 客户端接入配置样例
├── docs/plan.md                    # 本文档
└── README.md                       # 安装/迁移/接入说明
```

## 10. 发布与部署（可移植性）

```bash
# 单文件自包含（兜底方案）
dotnet publish src/GiteeManager.McpServer -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# Native AOT（首选，体积小启动快；需 VS C++ Build Tools）
dotnet publish src/GiteeManager.McpServer -c Release -r win-x64 -p:PublishAot=true
```

**产物**（整个文件夹即工具）：
```
gitee-mcp/
├── gitee-mcp.exe        # 单文件可执行
├── config.json          # 用户配置（首次运行生成）
└── README.md
```

**迁移到另一台电脑**：拷贝文件夹 → 编辑 `config.json`（或设 `GITEE_TOKEN` 环境变量）→ 完成。

**PI 接入样例**（mcp-config.example.json）：
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

## 11. 里程碑（实施顺序）

| 阶段 | 内容 | 验收标准 |
|------|------|----------|
| **M1 骨架** | 解决方案（net8.0 目标）+ Core：GiteeConfig 加载、GiteeApiClient 认证注入、错误映射、auth_whoami + 最小 MCP Server 冒烟 | `dotnet build` 通过；ConfigTests/ApiClientTests 绿；MCP 冒烟可启动 |
| **M2 仓库域** | repo_list/get/search/create/delete | 工具 Schema 测试 + MockHttp 测试绿 |
| **M3 分支/标签** | branch_list、tag_list | 同上 |
| **M4 PR/Issue/Release** | pr_*、issue_*、release_*（9 个工具） | 同上 |
| **M5 MCP 接入** | McpServer 项目：stdio + serve 双模式、工具注册、错误映射 | 用 MCP 调试客户端（如 mcp-inspector）实测 17 个工具可发现可调用 |
| **M6 发布验证** | 单文件发布（AOT 优先）、PI 实测接入、README | 另一台干净电脑（无 .NET）拷贝即用；PI 中能真实调用 repo_list 等 |

## 12. 风险与对策

| 风险 | 对策 |
|------|------|
| Native AOT 缺 C++ Build Tools | 自动退 PublishSingleFile 自包含（同样零依赖，仅体积大） |
| Gitee 接口字段与文档偏差 | JsonNode 透传零字段遗漏；错误映射兜底原始信息 |
| token 明文存储 | 默认明文（可移植必须）+ README 安全提示；后续可选 DPAPI 加密开关 |
| HTTP 模式 token 暴露 | 默认仅 localhost；文档声明风险 |
| MCP SDK 2.0 与 net8.0 兼容性 | ✅ **已实测验证**（net8.0 临时项目编译通过），风险消除；M1 仍做最小 MCP Server 冒烟验证 |

---

## 13. 未来扩展规划（暂不实现，按需启用）

### 13.1 按 AI 价值排序的扩展工具

**第一梯队（对 AI 管理仓库价值最高）**

| 工具 | 说明 | 价值 |
|------|------|------|
| `content_get` / `content_list` / `content_tree` | 读取仓库文件与目录树 | AI 直接查看代码 |
| `content_create` / `content_update` / `content_delete` | 通过 API 增删改文件 | AI 直接改仓库文件 |
| `commit_list` / `commit_get` | 提交历史与详情 | AI 理解代码演进 |
| `pr_files` | PR 变更文件列表 | AI 审查 PR |
| `compare` | 分支/提交差异 | AI 代码审查、版本对比 |
| `repo_snapshot` | 仓库全貌 JSON 快照（元数据+分支+PR+Issue+近期提交） | AI 一次调用理解仓库状态 |
| `git_clone` | 调用本机 Git 克隆到本地 | 桥接本地工作区 |

**第二梯队（常用管理操作）**

- `repo_update`（编辑描述/语言/主页/默认分支/可见性）、`repo_rename`、`repo_transfer`
- `repo_fork` / `repo_forks`、`repo_star` / `repo_unstar`
- `branch_create` / `branch_delete` / `branch_rename`、`tag_create` / `tag_delete`
- `pr_update` / `pr_close` / `pr_comment`、`issue_update` / `issue_comment` / `issue_assignees`
- `release_update` / `release_delete`、`release_upload_asset` / `release_download_asset`（AI 自动发布构建产物）
- `webhook_list` / `webhook_create` / `webhook_delete`（CI 自动化）
- `deploy_key_list` / `deploy_key_create` / `deploy_key_delete`（部署密钥，自动化 clone 免密）
- `search_issues` / `search_prs`（扩展搜索）
- `milestone_list` / `milestone_create` / `milestone_update`（里程碑）

**第三梯队（账户/组织/社交/其他）**

- `user_get` / `user_follow` / `user_unfollow`
- `org_list` / `org_get` / `org_repos` / `org_members`
- `gist_list` / `gist_create` / `gist_delete`（代码片段）
- `notification_list` / `notification_mark_read`（通知）
- `repo_contributors` / `repo_languages` / `repo_commit_stats`（统计报表）
- `enterprise_*`（Gitee 企业版：企业成员/仓库/任务看板，需确认是否使用企业版）
- `repo_import`（从 GitHub/GitLab 导入，需确认 Gitee API 是否暴露）

### 13.2 协议与配置扩展

- **MCP Resources**：暴露“我的仓库列表”为资源，AI 可浏览式发现仓库
- **MCP Prompts**：预置常用操作模板（“创建仓库”“发布 Release”），AI 一键套用
- **多账户 profiles**：config.json 支持多 profile（个人/企业/多账号），`--profile` 切换
- **HTTP 常驻 + 定时任务**：serve 模式下定期生成仓库快照/巡检

### 13.3 扩展原则

- 每个新工具遵循同一模板：名称 + 描述 + Schema + Handler，注册进 `GiteeToolFactory` 即可
- 优先实现第一梯队（读文件/提交/PR 审查类），这些对 AI 自动化价值最大
- 所有扩展保持 JsonNode 透传输出，不改动 Core 层结构

---

*待用户确认本方案后开始 M1 实施。*
