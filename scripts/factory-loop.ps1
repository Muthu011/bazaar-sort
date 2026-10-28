# Bazaar Sort factory loop (Windows PowerShell). Run from the repo root with Unity open:
#   powershell -ExecutionPolicy Bypass -File scripts\factory-loop.ps1
# Stop: create an empty file named STOP in the repo root.
param(
  [int]$IdleMinutes = 120,      # wait between runs when there is nothing urgent
  [int]$BusyMinutes = 10,       # wait when the last run finished a task
  [int]$StartHour = 7,          # only work between these hours (local time)
  [int]$EndHour = 23,
  [int]$MaxTurns = 80
)
$ErrorActionPreference = "Continue"
New-Item -ItemType Directory -Force -Path logs | Out-Null

function Run-Claude([string]$Prompt) {
  $log = "logs\$(Get-Date -Format yyyy-MM-dd).log"
  Add-Content $log "`n===== $(Get-Date -Format s) $Prompt ====="
  $out = & claude -p $Prompt --max-turns $MaxTurns 2>&1 | Out-String
  Add-Content $log $out
  return $out
}

while ($true) {
  if (Test-Path STOP) { Write-Host "STOP file found. Exiting."; break }
  $now = Get-Date
  if ($now.Hour -lt $StartHour -or $now.Hour -ge $EndHour) { Start-Sleep -Seconds 900; continue }

  $today = $now.ToString("yyyy-MM-dd")
  if ($now.Hour -ge 8 -and -not (Test-Path "logs\digest-$today.done")) {
    Run-Claude "/daily-digest" | Out-Null
    New-Item "logs\digest-$today.done" -ItemType File | Out-Null
  }
  $week = Get-Date -UFormat %V
  if ($now.DayOfWeek -eq "Monday" -and $now.Hour -ge 9 -and -not (Test-Path "logs\plan-$($now.Year)-W$week.done")) {
    Run-Claude "/plan-week" | Out-Null
    New-Item "logs\plan-$($now.Year)-W$week.done" -ItemType File | Out-Null
  }
  if ($now.DayOfWeek -eq "Thursday" -and $now.Hour -ge 14 -and -not (Test-Path "logs\rc-$($now.Year)-W$week.done")) {
    Run-Claude "/release-candidate" | Out-Null
    New-Item "logs\rc-$($now.Year)-W$week.done" -ItemType File | Out-Null
  }

  $result = Run-Claude "/work-next"
  if ($result -match "No ready work") { Start-Sleep -Seconds ($IdleMinutes * 60) }
  else { Start-Sleep -Seconds ($BusyMinutes * 60) }
}
