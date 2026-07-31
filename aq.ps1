[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory)]
    [ValidateSet('new', 'status', 'approve', 'verify', 'check-delivery')]
    [string] $Command,

    [string] $Title,
    [string] $Id,
    [switch] $UiScope,
    [string] $WorkItemId,
    [ValidateSet('Requirements', 'Plan', 'Tests', 'Delivery')] [string] $Stage,
    [string] $ApprovedBy,
    [string] $Note = '',
    [ValidateSet('Quick', 'Full')] [string] $Mode = 'Full',
    [string] $Target,
    [switch] $Json
)

$scripts = Join-Path $PSScriptRoot '.ai-quality\scripts'

switch ($Command) {
    'new' {
        if (-not $Title) { throw 'new requires -Title.' }
        $parameters = @{ Title = $Title; UiScope = $UiScope }
        if ($Id) { $parameters.Id = $Id }
        & (Join-Path $scripts 'New-AiWorkItem.ps1') @parameters
    }
    'status' {
        $parameters = @{ Json = $Json }
        if ($WorkItemId) { $parameters.WorkItemId = $WorkItemId }
        & (Join-Path $scripts 'Get-AiWorkflowStatus.ps1') @parameters
    }
    'approve' {
        if (-not $Stage -or -not $WorkItemId -or -not $ApprovedBy) {
            throw 'approve requires -Stage, -WorkItemId, and -ApprovedBy.'
        }
        & (Join-Path $scripts 'Approve-AiStage.ps1') -Stage $Stage -WorkItemId $WorkItemId -ApprovedBy $ApprovedBy -Note $Note
    }
    'verify' {
        if (-not $WorkItemId) { throw 'verify requires -WorkItemId.' }
        $parameters = @{ WorkItemId = $WorkItemId; Mode = $Mode }
        if ($Target) { $parameters.Target = $Target }
        & (Join-Path $scripts 'Invoke-AiQualityGate.ps1') @parameters
    }
    'check-delivery' {
        if (-not $WorkItemId) { throw 'check-delivery requires -WorkItemId.' }
        & (Join-Path $scripts 'Test-AiDelivery.ps1') -WorkItemId $WorkItemId
    }
}

if ($LASTEXITCODE) { exit $LASTEXITCODE }
