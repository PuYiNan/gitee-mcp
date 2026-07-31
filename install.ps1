<#
.SYNOPSIS
gitee-mcp 一键安装器：从云端（GitHub/Gitee Release）下载发布产物并安装，
可选接入多个 AI 客户端（PI / Claude Code / Claude Desktop / Cursor）。

.DESCRIPTION
自动完成：下载 zip -> 解压安装 -> 引导/读取令牌并写 config.json ->
按选择写入各 AI 客户端的 MCP 配置 -> stdio 冒烟验证。

.EXAMPLE
# 交互模式（引导式）
powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1

# 参数模式（可脚本化）
powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1 -Dir D:/tools/gitee-mcp -Username park-yinan -Token <token> -Agents pi,claude-code

# 远程一行命令（从云端拉取本脚本后直接执行）
powershell -NoProfile -ExecutionPolicy Bypass -c "irm https://gitee.com/park-yinan/gitee-mcp/raw/main/install.ps1 | iex"
#>
[CmdletBinding()]
param(
    [string] $Dir = "",
    [string] $Username = "",
    [string] $Token = "",
    [string[]] $Agents = @(),
    [ValidateSet("auto", "github", "gitee")] [string] $Source = "auto",
    [string] $Version = "v1.0.0",
    [switch] $SkipAgents
)

$ErrorActionPreference = "Stop"
$ProjectName = "gitee-mcp"
$RepoGitee = "park-yinan/gitee-mcp"
$RepoGitHub = "PuYiNan/gitee-mcp"
$ZipName = "gitee-mcp.zip"
$Interactive = $Host.Name -ne "ServerRemoteHost"

# ========== 1. 安装目录 ==========
if ([string]::IsNullOrWhiteSpace($Dir)) {
    $Dir = Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) $ProjectName
}
New-Item -ItemType Directory -Path $Dir -Force | Out-Null

# ========== 2. 从云端下载 zip（双源自动回退） ==========
$sources = @()
if ($Source -eq "gitee") { $sources = @("gitee") }
elseif ($Source -eq "github") { $sources = @("github") }
else { $sources = @("gitee", "github") } # auto：国内优先 Gitee

$zipPath = Join-Path $env:TEMP "$ProjectName-$Version.zip"
$downloaded = $false
foreach ($src in $sources) {
    $url = if ($src -eq "gitee") {
        "https://gitee.com/$RepoGitee/releases/download/$Version/$ZipName"
    } else {
        "https://github.com/$RepoGitHub/releases/download/$Version/$ZipName"
    }
    Write-Host "==> 从 $src 下载：$url"
    try {
        Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
        $downloaded = $true
        Write-Host "    下载完成（$([math]::Round((Get-Item $zipPath).Length / 1MB, 1)) MB）"
        break
    }
    catch {
        Write-Warning "$src 下载失败：$($_.Exception.Message)"
    }
}
if (-not $downloaded) { throw "从云端下载 $ZipName 失败：请检查网络或版本号 $Version 是否存在" }

# ========== 3. 解压安装 ==========
Write-Host "==> 安装到：$Dir"
$extractDir = Join-Path $env:TEMP "$ProjectName-extract"
if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force
Copy-Item -Path (Join-Path $extractDir "*") -Destination $Dir -Recurse -Force
$exePath = Join-Path $Dir "gitee-mcp.exe"
if (-not (Test-Path $exePath)) { throw "安装失败：未找到 $exePath" }

# ========== 4. 令牌与 config.json ==========
if ([string]::IsNullOrWhiteSpace($Token)) { $Token = [Environment]::GetEnvironmentVariable("GITEE_TOKEN") }
$tokenSet = -not [string]::IsNullOrWhiteSpace($Token)
if (-not $tokenSet -and $Interactive) {
    Write-Host ""
    $input = Read-Host "输入 Gitee 私人令牌（留空跳过，稍后可填 config.json 或设 GITEE_TOKEN）"
    if (-not [string]::IsNullOrWhiteSpace($input)) { $Token = $input; $tokenSet = $true }
}
if ([string]::IsNullOrWhiteSpace($Username)) { $Username = [Environment]::GetEnvironmentVariable("GITEE_USERNAME") }
if ([string]::IsNullOrWhiteSpace($Username) -and $Interactive) {
    $Username = Read-Host "输入 Gitee 用户名（如 park-yinan）"
}

if ($tokenSet -and -not [string]::IsNullOrWhiteSpace($Username)) {
    $config = [pscustomobject]@{
        username = $Username
        token = $Token
        api_base = "https://gitee.com/api/v5"
        default_per_page = 20
        max_per_page = 100
    }
    $config | ConvertTo-Json | Set-Content -Path (Join-Path $Dir "config.json") -Encoding utf8
    Write-Host "已写入 config.json（令牌仅存于此单点）"
}
else {
    Write-Host "未配置令牌：跳过 config.json（后续复制 config.example.json 填写，或设置 GITEE_TOKEN）"
    if (-not (Test-Path (Join-Path $Dir "config.json"))) {
        Copy-Item (Join-Path $Dir "config.example.json") (Join-Path $Dir "config.json")
    }
}

# ========== 5. 选择要接入的 AI 客户端 ==========
if ($SkipAgents) { $Agents = @() }
elseif ($Agents.Count -eq 0 -and $Interactive) {
    Write-Host ""
    Write-Host "==> 选择要接入的 AI 客户端（逗号分隔多选，如 1,2；输入 0 接入全部；留空跳过）："
    Write-Host "  1) pi             （Pi / pi-mcp-adapter 全局配置）"
    Write-Host "  2) claude-code    （Claude Code，当前目录 .mcp.json）"
    Write-Host "  3) claude-desktop （Claude Desktop）"
    Write-Host "  4) cursor         （Cursor 全局配置）"
    $sel = Read-Host "选择"
    $selected = @()
    foreach ($item in ($sel -split ",")) {
        $item = $item.Trim()
        if ($item -eq "0") { $selected = @("pi", "claude-code", "claude-desktop", "cursor"); break }
        elseif ($item -eq "1" -or $item -ieq "pi") { $selected += "pi" }
        elseif ($item -eq "2" -or $item -ieq "claude-code") { $selected += "claude-code" }
        elseif ($item -eq "3" -or $item -ieq "claude-desktop") { $selected += "claude-desktop" }
        elseif ($item -eq "4" -or $item -ieq "cursor") { $selected += "cursor" }
    }
    $Agents = $selected | Select-Object -Unique
}

# ========== 6. 写入各 agent 配置 ==========
if ($Agents.Count -gt 0) {
    Write-Host ""
    Write-Host "==> 接入 AI 客户端："
    $serverEntry = [pscustomobject]@{ command = $exePath; args = @() }
    if ($Agents -contains "pi") {
        $path = Join-Path $HOME ".config\mcp\mcp.json"
        Add-McpServer -Path $path -Server $serverEntry -Name "gitee-mcp"
        Write-Host "  [pi]            -> $path"
    }
    if ($Agents -contains "claude-code") {
        $path = Join-Path (Get-Location) ".mcp.json"
        Add-McpServer -Path $path -Server $serverEntry -Name "gitee-mcp"
        Write-Host "  [claude-code]   -> $path"
    }
    if ($Agents -contains "claude-desktop") {
        $path = Join-Path $env:APPDATA "Claude\claude_desktop_config.json"
        Add-McpServer -Path $path -Server $serverEntry -Name "gitee-mcp"
        Write-Host "  [claude-desktop]-> $path"
    }
    if ($Agents -contains "cursor") {
        $path = Join-Path $HOME ".cursor\mcp.json"
        Add-McpServer -Path $path -Server $serverEntry -Name "gitee-mcp"
        Write-Host "  [cursor]        -> $path"
    }
}
else {
    Write-Host ""
    Write-Host "未选择接入 AI 客户端（可稍后手动配置，参考 mcp-config.example.json）"
}

# ========== 7. stdio 冒烟验证 ==========
Write-Host ""
Write-Host "==> 验证安装（stdio 冒烟）..."
$probe = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"installer","version":"1.0"}}}' + "`n" +
         '{"jsonrpc":"2.0","method":"notifications/initialized"}' + "`n" +
         '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
try {
    $out = ($probe | & $exePath 2>$null) -join "`n"
    if ($out -match '"tools"') {
        $count = ([regex]::Matches($out, '"name":"')).Count
        Write-Host "  冒烟通过：$count 个工具可用"
    }
    else { Write-Warning "冒烟未返回工具列表（检查安装完整性）" }
}
catch {
    Write-Warning "冒烟执行失败：$($_.Exception.Message)"
}

# ========== 8. 汇总 ==========
Write-Host ""
Write-Host "=========================================="
Write-Host " gitee-mcp 安装完成"
Write-Host "   安装目录 : $Dir"
Write-Host "   可执行   : $exePath"
Write-Host "   接入 agent: $($(if ($Agents.Count -gt 0) { $Agents -join ", " } else { "无" }))"
Write-Host "   注意     : 接入的 AI 客户端需重启后生效（如 Pi / Claude Desktop）"
Write-Host "=========================================="

# ========== 辅助函数 ==========
function Add-McpServer {
    param([string] $Path, [object] $Server, [string] $Name)
    $dir = Split-Path $Path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    $root = $null
    if (Test-Path $Path) {
        try { $root = Get-Content -Raw -Path $Path | ConvertFrom-Json } catch { $root = $null }
    }
    if ($null -eq $root) { $root = [pscustomobject]@{ mcpServers = [pscustomobject]@{} } }
    if ($null -eq $root.mcpServers) { $root | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([pscustomobject]@{}) -Force }

    # 删除旧条目后重建（避免 Add-Member 对已存在属性报错）
    $servers = $root.mcpServers
    $existing = @($servers.PSObject.Properties | Where-Object { $_.Name -eq $Name })
    foreach ($p in $existing) { $servers.PSObject.Properties.Remove($p.Name) }
    $servers | Add-Member -NotePropertyName $Name -NotePropertyValue $Server -Force

    $root | ConvertTo-Json -Depth 8 | Set-Content -Path $Path -Encoding utf8
}
