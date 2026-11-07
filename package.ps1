[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $PSScriptRoot 'dist' }
$helperExecutable = Join-Path $PSScriptRoot 'bin\Horizon Folder Helper.exe'
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -Test }
if (-not (Test-Path -LiteralPath $helperExecutable)) { throw 'Run build.ps1 before packaging.' }
$helperVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($helperVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain major.minor.patch.' }
if ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($helperExecutable).FileVersion -ne ($helperVersion + '.0')) {
    throw 'Build version does not match VERSION.'
}
$helperDist = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $helperDist -Force | Out-Null
$helperArchive = Join-Path $helperDist ('HorizonFolderHelper-' + $helperVersion + '-windows.zip')
$helperFiles = @($helperExecutable)
foreach ($helperName in @('README.md', 'LICENSE', 'CHANGELOG.md', 'Как пользоваться.txt')) {
    $helperFiles += Join-Path $PSScriptRoot $helperName
}
Compress-Archive -LiteralPath $helperFiles -DestinationPath $helperArchive -Force
$helperDigest = (Get-FileHash -LiteralPath $helperArchive -Algorithm SHA256).Hash.ToLowerInvariant()
($helperDigest + '  ' + [System.IO.Path]::GetFileName($helperArchive)) |
    Set-Content -LiteralPath (Join-Path $helperDist 'SHA256SUMS.txt') -Encoding ASCII
Get-Item -LiteralPath $helperArchive | Select-Object FullName, Length
