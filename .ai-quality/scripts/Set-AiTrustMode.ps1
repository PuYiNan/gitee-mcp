[CmdletBinding()]
param(
    [switch] $Enable,
    [switch] $Disable,
    [string] $AuthorizedBy
)

if ($Enable -and $Disable) { throw 'Choose either -Enable or -Disable.' }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$configPath = Join-Path $repoRoot '.ai-quality\config.json'
if (-not (Test-Path -LiteralPath $configPath)) { throw "Missing configuration: $configPath" }
$config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json

function Set-ConfigProperty([string] $Name, $Value) {
    if ($config.PSObject.Properties.Name -contains $Name) {
        $config.$Name = $Value
    }
    else {
        $config | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
}

if (-not $Enable -and -not $Disable) {
    $mode = if ($config.approvalMode) { $config.approvalMode } else { 'manual' }
    [pscustomobject]@{
        approvalMode = $mode
        trustedApprover = $config.trustedApprover
        trustAuthorizedBy = $config.trustAuthorizedBy
        trustAuthorizedAt = $config.trustAuthorizedAt
        independentReview = ($mode -eq 'manual')
    } | Format-List
    exit 0
}

if ($Enable) {
    if (-not $AuthorizedBy) { throw 'Enabling trusted mode requires -AuthorizedBy.' }
    $challenge = 'ENABLE TRUSTED MODE'
    Write-Warning 'Trusted mode lets the implementing Agent approve its own Requirements, Plan, Tests, and Delivery stages.'
    Write-Warning 'State checks, artifact hashes, Full verification, and delivery validation remain mandatory, but independent review is removed.'
    $confirmation = Read-Host "Type exactly: $challenge"
    if ($confirmation -cne $challenge) { throw 'Trusted mode was not enabled: confirmation did not match.' }
    Set-ConfigProperty 'schemaVersion' 2
    Set-ConfigProperty 'approvalMode' 'trusted'
    Set-ConfigProperty 'trustedApprover' 'agent:trusted-mode'
    Set-ConfigProperty 'trustAuthorizedBy' $AuthorizedBy
    Set-ConfigProperty 'trustAuthorizedAt' (Get-Date).ToUniversalTime().ToString('o')
    $config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configPath -Encoding utf8
    Write-Host "Trusted mode enabled by $AuthorizedBy."
}
else {
    Set-ConfigProperty 'schemaVersion' 2
    Set-ConfigProperty 'approvalMode' 'manual'
    Set-ConfigProperty 'trustAuthorizedBy' ''
    Set-ConfigProperty 'trustAuthorizedAt' ''
    $config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configPath -Encoding utf8
    Write-Host 'Manual approval mode enabled.'
}
