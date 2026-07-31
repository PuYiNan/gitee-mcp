<#
.SYNOPSIS
gitee-mcp one-click installer: downloads the release build from GitHub/Gitee cloud,
installs it, and optionally registers it with AI clients (PI / Claude Code / Claude Desktop / Cursor).

.DESCRIPTION
Automates: download zip -> extract & install -> collect/read token & write config.json ->
write MCP config for selected AI clients -> stdio smoke test.

All script text is ASCII-only for maximum compatibility (any PowerShell, any encoding).

.EXAMPLE
# Interactive mode
powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1

# Parameterized mode
powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1 -Dir D:/tools/gitee-mcp -Username park-yinan -Token <token> -Agents pi,claude-code

# One-liner from cloud (Gitee)
powershell -NoProfile -ExecutionPolicy Bypass -c "irm https://gitee.com/park-yinan/gitee-mcp/raw/master/install.ps1 | iex"

# One-liner from cloud (GitHub)
powershell -NoProfile -ExecutionPolicy Bypass -c "irm https://raw.githubusercontent.com/PuYiNan/gitee-mcp/master/install.ps1 | iex"
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

# ========== 1. Install directory ==========
if ([string]::IsNullOrWhiteSpace($Dir)) {
    $Dir = Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) $ProjectName
}
New-Item -ItemType Directory -Path $Dir -Force | Out-Null

# ========== 2. Download zip from cloud (dual-source fallback) ==========
$sources = @()
if ($Source -eq "gitee") { $sources = @("gitee") }
elseif ($Source -eq "github") { $sources = @("github") }
else { $sources = @("gitee", "github") } # auto: Gitee first (faster in CN)

$zipPath = Join-Path $env:TEMP "$ProjectName-$Version.zip"
$downloaded = $false
foreach ($src in $sources) {
    $url = if ($src -eq "gitee") {
        "https://gitee.com/$RepoGitee/releases/download/$Version/$ZipName"
    } else {
        "https://github.com/$RepoGitHub/releases/download/$Version/$ZipName"
    }
    Write-Host "==> Downloading from $src : $url"
    try {
        Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
        $downloaded = $true
        Write-Host "    Downloaded ($([math]::Round((Get-Item $zipPath).Length / 1MB, 1)) MB)"
        break
    }
    catch {
        Write-Warning "$src download failed: $($_.Exception.Message)"
    }
}
if (-not $downloaded) { throw "Failed to download $ZipName from cloud. Check network or version $Version." }

# ========== 3. Extract & install ==========
Write-Host "==> Installing to: $Dir"
$extractDir = Join-Path $env:TEMP "$ProjectName-extract"
if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force
Copy-Item -Path (Join-Path $extractDir "*") -Destination $Dir -Recurse -Force
$exePath = Join-Path $Dir "gitee-mcp.exe"
if (-not (Test-Path $exePath)) { throw "Install failed: $exePath not found" }

# ========== 4. Token & config.json ==========
if ([string]::IsNullOrWhiteSpace($Token)) { $Token = [Environment]::GetEnvironmentVariable("GITEE_TOKEN") }
$tokenSet = -not [string]::IsNullOrWhiteSpace($Token)
if (-not $tokenSet -and $Interactive) {
    Write-Host ""
    $input = Read-Host "Enter Gitee personal access token (empty to skip; configure later in config.json or GITEE_TOKEN)"
    if (-not [string]::IsNullOrWhiteSpace($input)) { $Token = $input; $tokenSet = $true }
}
if ([string]::IsNullOrWhiteSpace($Username)) { $Username = [Environment]::GetEnvironmentVariable("GITEE_USERNAME") }
if ([string]::IsNullOrWhiteSpace($Username) -and $Interactive) {
    $Username = Read-Host "Enter Gitee username (e.g. park-yinan)"
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
    Write-Host "config.json written (token stored here only)"
}
else {
    Write-Host "No token configured: skipped config.json (copy config.example.json later, or set GITEE_TOKEN)"
    if (-not (Test-Path (Join-Path $Dir "config.json"))) {
        Copy-Item (Join-Path $Dir "config.example.json") (Join-Path $Dir "config.json")
    }
}

# ========== 5. Select AI clients to register ==========
if ($SkipAgents) { $Agents = @() }
elseif ($Agents.Count -eq 0 -and $Interactive) {
    Write-Host ""
    Write-Host "==> Select AI clients to register (comma-separated, e.g. 1,2; 0 = all; empty = skip):"
    Write-Host "  1) pi             (Pi / pi-mcp-adapter global config)"
    Write-Host "  2) claude-code    (Claude Code, .mcp.json in current dir)"
    Write-Host "  3) claude-desktop (Claude Desktop)"
    Write-Host "  4) cursor         (Cursor global config)"
    $sel = Read-Host "Select"
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

# ========== 6. Write per-agent MCP config ==========
if ($Agents.Count -gt 0) {
    Write-Host ""
    Write-Host "==> Registering AI clients:"
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
    Write-Host "No AI client selected (configure later, see mcp-config.example.json)"
}

# ========== 7. stdio smoke test ==========
Write-Host ""
Write-Host "==> Verifying install (stdio smoke test)..."
$probe = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"installer","version":"1.0"}}}' + "`n" +
         '{"jsonrpc":"2.0","method":"notifications/initialized"}' + "`n" +
         '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
try {
    $out = ($probe | & $exePath 2>$null) -join "`n"
    if ($out -match '"tools"') {
        $count = ([regex]::Matches($out, '"name":"')).Count
        Write-Host "  Smoke OK: $count tools available"
    }
    else { Write-Warning "Smoke test did not return tools list (check install integrity)" }
}
catch {
    Write-Warning "Smoke test failed: $($_.Exception.Message)"
}

# ========== 8. Summary ==========
Write-Host ""
Write-Host "=========================================="
Write-Host " gitee-mcp installed"
Write-Host "   Install dir : $Dir"
Write-Host "   Executable  : $exePath"
Write-Host "   Agents      : $($(if ($Agents.Count -gt 0) { $Agents -join ", " } else { "none" }))"
Write-Host "   Note        : restart the AI client (Pi / Claude Desktop) to pick up new config"
Write-Host "=========================================="

# ========== Helpers ==========
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

    $servers = $root.mcpServers
    $existing = @($servers.PSObject.Properties | Where-Object { $_.Name -eq $Name })
    foreach ($p in $existing) { $servers.PSObject.Properties.Remove($p.Name) }
    $servers | Add-Member -NotePropertyName $Name -NotePropertyValue $Server -Force

    $root | ConvertTo-Json -Depth 8 | Set-Content -Path $Path -Encoding utf8
}
