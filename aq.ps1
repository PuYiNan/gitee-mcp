[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory)]
    [ValidateSet('new', 'status', 'trust', 'approve', 'verify', 'check-delivery')]
    [string] $Command,

    [string] $Title,
    [string] $Id,
    [switch] $UiScope,
    [string] $WorkItemId,
    [ValidateSet('Requirements', 'Plan', 'Tests', 'Delivery')] [string] $Stage,
    [string] $ApprovedBy,
    [string] $AuthorizedBy,
    [switch] $Enable,
    [switch] $Disable,
    [string] $Note = '',
    [ValidateSet('Quick', 'Full')] [string] $Mode = 'Full',
    [string] $Target,
    [switch] $Json
)

$scripts = Join-Path $PSScriptRoot '.ai-quality\scripts'
$scriptSucceeded = $false

switch ($Command) {
    'new' {
        if (-not $Title) { throw 'new requires -Title.' }
        $parameters = @{ Title = $Title; UiScope = $UiScope }
        if ($Id) { $parameters.Id = $Id }
        & (Join-Path $scripts 'New-AiWorkItem.ps1') @parameters
        $scriptSucceeded = $?
    }
    'status' {
        $parameters = @{ Json = $Json }
        if ($WorkItemId) { $parameters.WorkItemId = $WorkItemId }
        & (Join-Path $scripts 'Get-AiWorkflowStatus.ps1') @parameters
        $scriptSucceeded = $?
    }
    'trust' {
        if ($Enable -and $Disable) { throw 'trust accepts either -Enable or -Disable, not both.' }
        $parameters = @{}
        if ($Enable) {
            if (-not $AuthorizedBy) { throw 'trust -Enable requires -AuthorizedBy.' }
            $parameters.Enable = $true
            $parameters.AuthorizedBy = $AuthorizedBy
        }
        elseif ($Disable) {
            $parameters.Disable = $true
        }
        & (Join-Path $scripts 'Set-AiTrustMode.ps1') @parameters
        $scriptSucceeded = $?
    }
    'approve' {
        if (-not $Stage -or -not $WorkItemId) {
            throw 'approve requires -Stage and -WorkItemId. Manual mode also requires -ApprovedBy.'
        }
        $parameters = @{ Stage = $Stage; WorkItemId = $WorkItemId; Note = $Note }
        if ($ApprovedBy) { $parameters.ApprovedBy = $ApprovedBy }
        & (Join-Path $scripts 'Approve-AiStage.ps1') @parameters
        $scriptSucceeded = $?
    }
    'verify' {
        if (-not $WorkItemId) { throw 'verify requires -WorkItemId.' }
        $parameters = @{ WorkItemId = $WorkItemId; Mode = $Mode }
        if ($Target) { $parameters.Target = $Target }
        & (Join-Path $scripts 'Invoke-AiQualityGate.ps1') @parameters
        $scriptSucceeded = $?
    }
    'check-delivery' {
        if (-not $WorkItemId) { throw 'check-delivery requires -WorkItemId.' }
        & (Join-Path $scripts 'Test-AiDelivery.ps1') -WorkItemId $WorkItemId
        $scriptSucceeded = $?
    }
}

if (-not $scriptSucceeded) { exit 1 }
exit 0
