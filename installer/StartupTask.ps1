param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Install", "Uninstall", "Run")]
    [string]$Mode,

    [string]$ExePath = ""
)

$ErrorActionPreference = "Stop"
$taskName = "MinimalSwitcher"

if ($Mode -eq "Uninstall") {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    exit 0
}

if ($Mode -eq "Run") {
    Start-ScheduledTask -TaskName $taskName
    exit 0
}

if ([string]::IsNullOrWhiteSpace($ExePath) -or -not (Test-Path -LiteralPath $ExePath)) {
    throw "Invalid Minimal Switcher executable path: $ExePath"
}

$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory (Split-Path -LiteralPath $ExePath)
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $identity
$principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet `
    -Compatibility Win8 `
    -ExecutionTimeLimit (New-TimeSpan -Seconds 0) `
    -MultipleInstances IgnoreNew `
    -Priority 4 `
    -StartWhenAvailable

Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings `
    -Description "Start Minimal Switcher at sign-in with elevated privileges." `
    -Force | Out-Null
