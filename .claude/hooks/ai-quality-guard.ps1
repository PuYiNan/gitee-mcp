$inputJson = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = $inputJson.tool_input.file_path
if (-not $path) { exit 0 }

$repoRoot = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
$guard = Join-Path $repoRoot '.ai-quality\scripts\Assert-AiEditAllowed.ps1'
if (-not (Test-Path -LiteralPath $guard)) { exit 0 }

try {
    & $guard -Path $path 2>&1 | ForEach-Object { [Console]::Error.WriteLine($_) }
    if (-not $?) { exit 2 }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 2
}
