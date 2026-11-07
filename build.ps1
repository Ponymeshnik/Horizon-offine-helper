[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $PSScriptRoot 'bin' }
if ($env:OS -ne 'Windows_NT') { throw 'This application must be built on Windows.' }
$helperOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $helperOutput -Force | Out-Null
$helperCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $helperCompiler)) {
    $helperCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $helperCompiler)) { throw 'Install .NET Framework 4.8 before building.' }
$helperExecutable = Join-Path $helperOutput 'Horizon Folder Helper.exe'
$helperSource = Join-Path $PSScriptRoot 'src'
$compilerArguments = @(
    '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/utf8output', '/codepage:65001',
    ('/out:{0}' -f $helperExecutable),
    ('/win32manifest:{0}' -f (Join-Path $helperSource 'app.manifest')),
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll',
    '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll',
    '/reference:Microsoft.CSharp.dll',
    (Join-Path $helperSource 'HelperCore.cs'), (Join-Path $helperSource 'Program.cs')
)
& $helperCompiler @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Helper compilation failed.' }
$helperVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($helperVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain major.minor.patch.' }
if ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($helperExecutable).FileVersion -ne ($helperVersion + '.0')) {
    throw 'VERSION and AssemblyFileVersion in src/Program.cs must match.'
}

if ($Test) {
    $helperTestRoot = Join-Path $PSScriptRoot ('obj\self-test-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $helperTestRoot -Force | Out-Null
    $helperReport = Join-Path $helperTestRoot 'report.json'
    $helperPreviousHome = $env:HORIZON_FOLDER_HELPER_HOME
    try {
        $env:HORIZON_FOLDER_HELPER_HOME = Join-Path $helperTestRoot 'home'
        $helperTestProcess = Start-Process -FilePath $helperExecutable -ArgumentList @('--self-test', '--report', ('"{0}"' -f $helperReport)) -WindowStyle Hidden -Wait -PassThru
        if ($helperTestProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $helperReport)) {
            throw ('Self-test failed. See ' + $helperReport)
        }
        $helperResult = Get-Content -LiteralPath $helperReport -Raw | ConvertFrom-Json
        if (-not $helperResult.ok -or $helperResult.passed -lt 1) { throw 'Self-test report did not indicate success.' }
        Write-Host ('Passed {0} built-in checks.' -f $helperResult.passed)
    } finally {
        $env:HORIZON_FOLDER_HELPER_HOME = $helperPreviousHome
    }
}
Get-Item -LiteralPath $helperExecutable | Select-Object FullName, Length
