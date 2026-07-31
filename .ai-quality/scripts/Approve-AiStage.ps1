[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Requirements', 'Plan', 'Tests', 'Delivery')] [string] $Stage,
    [Parameter(Mandatory)] [ValidatePattern('^[a-z0-9][a-z0-9-]{2,63}$')] [string] $WorkItemId,
    [ValidateNotNullOrEmpty()] [string] $ApprovedBy,
    [string] $Note = ''
)

function Assert-ReadyFile([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing required artifact: $Path" }
    $content = Get-Content -Raw -LiteralPath $Path
    if ($content -match '\[TODO') { throw "Artifact still contains TODO placeholders: $Path" }
    return $content
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$configPath = Join-Path $repoRoot '.ai-quality\config.json'
$config = if (Test-Path -LiteralPath $configPath) {
    Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json
} else {
    [pscustomobject]@{}
}
$approvalMode = if ($config.approvalMode) { [string]$config.approvalMode } else { 'manual' }
if ($approvalMode -notin @('manual', 'trusted')) {
    throw "Unknown approvalMode '$approvalMode' in .ai-quality/config.json."
}
if ($approvalMode -eq 'trusted' -and (-not $config.trustAuthorizedBy -or -not $config.trustAuthorizedAt)) {
    throw 'Trusted mode is missing its authorization record. Re-enable it with: pwsh ./aq.ps1 trust -Enable -AuthorizedBy <name>'
}
$item = Join-Path $repoRoot ".ai-quality\work-items\$WorkItemId"
$statePath = Join-Path $item 'state.json'
if (-not (Test-Path -LiteralPath $statePath)) { throw "Unknown work item: $WorkItemId" }
$state = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json

$rules = @{
    Requirements = @{ From = 'discovery'; To = 'requirements-approved'; Artifact = 'spec.md' }
    Plan = @{ From = 'requirements-approved'; To = 'plan-approved'; Artifact = 'plan.md' }
    Tests = @{ From = 'plan-approved'; To = 'implementation-authorized'; Artifact = 'test-matrix.md' }
    Delivery = @{ From = 'verification-passed'; To = 'accepted'; Artifact = 'delivery.md' }
}
$rule = $rules[$Stage]
if ($state.state -ne $rule.From) {
    throw "Stage $Stage requires state '$($rule.From)', current state is '$($state.state)'."
}

$artifactPath = Join-Path $item $rule.Artifact
$content = Assert-ReadyFile $artifactPath
if ($Stage -eq 'Requirements' -and $content -notmatch 'AC-\d{3}') {
    throw 'Requirements must contain at least one AC-### acceptance criterion.'
}
if ($approvalMode -eq 'trusted') {
    if ($Stage -eq 'Requirements') {
        if ($content -match '(?im)^-\s*Status:\s*`?DRAFT`?\s*$') {
            throw 'Trusted approval requires spec.md Status to be READY, not DRAFT.'
        }
        if ($content -match '(?m)^\s*-\s*\[\s\]') {
            throw 'Trusted approval requires every readiness checklist item to be checked.'
        }
    }
    elseif ($Stage -eq 'Plan' -and $content -notmatch 'AC-\d{3}') {
        throw 'Trusted Plan approval requires at least one AC-### mapping.'
    }
    elseif ($Stage -eq 'Tests' -and ($content -notmatch 'T-\d{3}' -or $content -notmatch 'AC-\d{3}')) {
        throw 'Trusted Tests approval requires T-### cases mapped to AC-### criteria.'
    }
}
if ($Stage -eq 'Delivery') {
    & (Join-Path $PSScriptRoot 'Test-AiDelivery.ps1') -WorkItemId $WorkItemId
    if (-not $?) { throw 'Delivery validation failed.' }
}

$approvalAuthority = 'external-reviewer'
$effectiveApprover = $ApprovedBy
if ($approvalMode -eq 'manual') {
    if (-not $ApprovedBy) { throw 'Manual approval requires -ApprovedBy.' }
    $challenge = "APPROVE $WorkItemId $($Stage.ToUpperInvariant())"
    Write-Host "Human approval boundary. Review $artifactPath"
    $confirmation = Read-Host "Type exactly: $challenge"
    if ($confirmation -cne $challenge) { throw 'Approval cancelled: confirmation did not match.' }
}
else {
    $approvalAuthority = 'implementing-agent'
    $effectiveApprover = if ($config.trustedApprover) { [string]$config.trustedApprover } else { 'agent:trusted-mode' }
    Write-Warning "Trusted mode: $effectiveApprover is self-approving $Stage. This is not independent human review."
}

$approval = [ordered]@{
    schemaVersion = 2
    workItemId = $WorkItemId
    stage = $Stage
    approvedBy = $effectiveApprover
    approvalMode = $approvalMode
    approvalAuthority = $approvalAuthority
    trustAuthorizedBy = if ($approvalMode -eq 'trusted') { [string]$config.trustAuthorizedBy } else { '' }
    trustAuthorizedAt = if ($approvalMode -eq 'trusted') { [string]$config.trustAuthorizedAt } else { '' }
    approvedAt = (Get-Date).ToUniversalTime().ToString('o')
    artifact = $rule.Artifact
    artifactSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifactPath).Hash.ToLowerInvariant()
    note = $Note
}
$approvalPath = Join-Path $item "approvals\$($Stage.ToLowerInvariant()).json"
$approval | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $approvalPath -Encoding utf8

$state.state = $rule.To
$state.lastTransitionAt = (Get-Date).ToUniversalTime().ToString('o')
$state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statePath -Encoding utf8
Write-Host "Approved $Stage; state is now $($rule.To)."
