[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^[a-z0-9][a-z0-9-]{2,63}$')] [string] $WorkItemId
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$item = Join-Path $repoRoot ".ai-quality\work-items\$WorkItemId"
$errors = [System.Collections.Generic.List[string]]::new()

function Require-File([string] $Path, [string] $Label) {
    if (-not (Test-Path -LiteralPath $Path)) {
        $errors.Add("Missing ${Label}: $Path")
        return $null
    }
    return Get-Content -Raw -LiteralPath $Path
}

$stateContent = Require-File (Join-Path $item 'state.json') 'state'
$spec = Require-File (Join-Path $item 'spec.md') 'specification'
$matrix = Require-File (Join-Path $item 'test-matrix.md') 'test matrix'
$delivery = Require-File (Join-Path $item 'delivery.md') 'delivery report'
$gateContent = Require-File (Join-Path $item 'evidence\latest-quality-gate.json') 'Full gate evidence'

if ($stateContent) {
    $state = $stateContent | ConvertFrom-Json
    if ($state.state -ne 'verification-passed') { $errors.Add("State is '$($state.state)', expected 'verification-passed'.") }
}
if ($gateContent) {
    $gate = $gateContent | ConvertFrom-Json
    if ($gate.mode -ne 'Full' -or $gate.overall -ne 'Passed') { $errors.Add('Latest quality gate is not a passing Full run.') }
}
foreach ($pair in @(@('specification', $spec), @('test matrix', $matrix), @('delivery report', $delivery))) {
    if ($pair[1] -and $pair[1] -match '\[TODO') { $errors.Add("$($pair[0]) contains TODO placeholders.") }
}

if ($spec) {
    $criteria = @([regex]::Matches($spec, 'AC-\d{3}') | ForEach-Object Value | Sort-Object -Unique)
    if ($criteria.Count -eq 0) { $errors.Add('No AC-### acceptance criteria found.') }
    foreach ($criterion in $criteria) {
        if ($matrix -and $matrix -notmatch [regex]::Escape($criterion)) { $errors.Add("$criterion is missing from the test matrix.") }
        if ($delivery -and $delivery -notmatch "(?m)^\|\s*$criterion\s*\|\s*PASS\s*\|") {
            $errors.Add("$criterion does not have a PASS row in delivery.md.")
        }
    }
}
if ($delivery) {
    if ($delivery -notmatch '(?m)^- Overall status: `COMPLETE`\s*$') { $errors.Add('Delivery overall status is not COMPLETE.') }
    if ($delivery -match '\b(FAIL|UNVERIFIED)\b') { $errors.Add('Delivery contains FAIL or UNVERIFIED results.') }
}

if ($errors.Count -gt 0) {
    Write-Error ("Delivery validation failed:`n- " + ($errors -join "`n- "))
    exit 1
}

$configPath = Join-Path $repoRoot '.ai-quality\config.json'
$config = if (Test-Path -LiteralPath $configPath) { Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json } else { [pscustomobject]@{} }
if ($config.approvalMode -eq 'trusted') {
    Write-Host "Delivery validation passed for $WorkItemId. Trusted mode permits Agent acceptance; independent review was not performed."
}
else {
    Write-Host "Delivery validation passed for $WorkItemId. Human/PR acceptance is still required."
}
exit 0
