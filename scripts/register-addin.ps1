[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("2024", "2025", "2026")]
    [string]$RevitVersion = "2026",

    [ValidateSet("CurrentUser", "AllUsers")]
    [string]$Scope = "CurrentUser",

    [string]$AssemblyPath
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$manifestTemplatePath = Join-Path $repoRoot "src/RevitMcp.RevitAddin/Manifest/RevitMcp.RevitAddin.addin"

if (-not (Test-Path $manifestTemplatePath)) {
    throw "Manifest template not found at $manifestTemplatePath"
}

if (-not $AssemblyPath) {
    $tfm = if ($RevitVersion -eq "2024") { "net48" } else { "net8.0-windows" }
    $AssemblyPath = Join-Path $repoRoot "src/RevitMcp.RevitAddin/bin/$Configuration/$tfm/RevitMcp.RevitAddin.dll"
}

if (-not (Test-Path $AssemblyPath)) {
    throw "Built add-in assembly not found at $AssemblyPath. Build the add-in first."
}

$addinRoot = switch ($Scope) {
    "CurrentUser" { Join-Path $env:APPDATA "Autodesk/Revit/Addins/$RevitVersion" }
    "AllUsers" { Join-Path $env:ProgramData "Autodesk/Revit/Addins/$RevitVersion" }
}

New-Item -ItemType Directory -Path $addinRoot -Force | Out-Null

$manifestOutputPath = Join-Path $addinRoot "RevitMcp.RevitAddin.addin"
$assemblyPathEscaped = [System.Security.SecurityElement]::Escape((Resolve-Path $AssemblyPath).Path)
$manifestContent = Get-Content -Raw -Path $manifestTemplatePath
$manifestContent = [System.Text.RegularExpressions.Regex]::Replace(
    $manifestContent,
    "<Assembly>.*?</Assembly>",
    "<Assembly>$assemblyPathEscaped</Assembly>"
)

Set-Content -Path $manifestOutputPath -Value $manifestContent -Encoding utf8

Write-Host "Registered Revit add-in manifest." -ForegroundColor Green
Write-Host "  Scope: $Scope"
Write-Host "  RevitVersion: $RevitVersion"
Write-Host "  Manifest: $manifestOutputPath"
Write-Host "  Assembly: $AssemblyPath"

