[CmdletBinding()]
param(
    [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $csc)) {
    throw 'The built-in .NET Framework C# compiler was not found.'
}

$out = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -Recurse | ForEach-Object FullName)
if ($sources.Count -eq 0) { throw 'No C# sources found.' }

$app = Join-Path $out 'RazerBatteryMonitor.exe'
$args = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/debug:pdbonly',
    ('/out:' + $app),
    ('/win32manifest:' + (Join-Path $root 'app.manifest')),
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
) + $sources

& $csc @args
if ($LASTEXITCODE -ne 0) { throw "Application compilation failed with exit code $LASTEXITCODE." }

if ($RunTests) {
    & (Join-Path $root 'test.ps1')
}

$hash = (Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Built $app"
Write-Host "SHA256 $hash"

