# gitee-mcp 发布脚本：Native AOT 优先，失败自动退回自包含单文件（目标机均零 .NET 依赖）。
[CmdletBinding()]
param(
    [string] $Output = "release/gitee-mcp",
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64"
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $repoRoot "src\GiteeManager.McpServer\GiteeManager.McpServer.csproj"
$outPath = Join-Path $repoRoot $Output

# 打发布 zip（供云端 Release 附件分发，install.ps1 从云端下载）
function New-GiteeMcpZip([string] $PublishDir) {
    # 确保文档与样例进入发布目录
    foreach ($doc in @("README.md", "config.example.json", "mcp-config.example.json")) {
        Copy-Item (Join-Path $repoRoot $doc) (Join-Path $PublishDir $doc) -Force
    }
    $zipPath = Join-Path $repoRoot "gitee-mcp.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    $files = @(
        (Join-Path $PublishDir "gitee-mcp.exe"),
        (Join-Path $PublishDir "aspnetcorev2_inprocess.dll"),
        (Join-Path $PublishDir "README.md"),
        (Join-Path $PublishDir "config.example.json"),
        (Join-Path $PublishDir "mcp-config.example.json")
    ) | Where-Object { Test-Path $_ }
    Compress-Archive -Path $files -DestinationPath $zipPath -Force
    Write-Host "发布 zip：$zipPath（$([math]::Round((Get-Item $zipPath).Length / 1MB, 1)) MB）"
}

if (Test-Path $outPath) { Remove-Item $outPath -Recurse -Force }

Write-Host "==> 尝试 Native AOT 发布（-p:PublishAot=true）..."
dotnet publish $project -c $Configuration -r $Runtime -p:PublishAot=true -o $outPath -v:q --nologo
if ($LASTEXITCODE -eq 0) {
    $exe = Join-Path $outPath "GiteeManager.McpServer.exe"
    if (Test-Path $exe) {
        Rename-Item $exe "gitee-mcp.exe"
        Write-Host "AOT 发布成功：$outPath\gitee-mcp.exe"
        New-GiteeMcpZip $outPath
        exit 0
    }
}

Write-Host "==> AOT 发布失败，退回自包含单文件发布..."
if (Test-Path $outPath) { Remove-Item $outPath -Recurse -Force }
dotnet publish $project -c $Configuration -r $Runtime --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $outPath -v:q --nologo
if ($LASTEXITCODE -ne 0) { throw "自包含发布失败（exit $LASTEXITCODE）" }
Rename-Item (Join-Path $outPath "GiteeManager.McpServer.exe") "gitee-mcp.exe"
Write-Host "自包含发布成功：$outPath\gitee-mcp.exe"
New-GiteeMcpZip $outPath
