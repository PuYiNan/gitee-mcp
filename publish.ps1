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

if (Test-Path $outPath) { Remove-Item $outPath -Recurse -Force }

Write-Host "==> 尝试 Native AOT 发布（-p:PublishAot=true）..."
dotnet publish $project -c $Configuration -r $Runtime -p:PublishAot=true -o $outPath -v:q --nologo
if ($LASTEXITCODE -eq 0) {
    $exe = Join-Path $outPath "GiteeManager.McpServer.exe"
    if (Test-Path $exe) {
        Rename-Item $exe "gitee-mcp.exe"
        Write-Host "AOT 发布成功：$outPath\gitee-mcp.exe"
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
