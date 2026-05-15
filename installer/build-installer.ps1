param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$InnoCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "minimal-switcher.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$script = Join-Path $PSScriptRoot "minimal-switcher.iss"

if (-not (Test-Path $InnoCompiler)) {
    throw "Inno Setup compiler not found at '$InnoCompiler'. Install Inno Setup 6 or pass -InnoCompiler."
}

[xml]$projectXml = Get-Content $project
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $projectXml.Project.PropertyGroup.Version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = "0.1.0"
}

New-Item -ItemType Directory -Force $publishDir, $installerDir | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

$env:APP_VERSION = $Version
$env:PUBLISH_DIR = $publishDir
$env:INSTALLER_OUTPUT_DIR = $installerDir

& $InnoCompiler $script
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

Write-Host "Installer created in $installerDir"
