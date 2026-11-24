$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$testExe = Join-Path $out 'RazerBatteryMonitor.Tests.exe'
$sources = @(
    (Join-Path $root 'src\DeviceState.cs'),
    (Join-Path $root 'src\RazerProtocol.cs'),
    (Join-Path $root 'src\SteelSeriesProtocol.cs'),
    (Join-Path $root 'src\SynapseLogReader.cs'),
    (Join-Path $root 'src\UsbDevicePresence.cs'),
    (Join-Path $root 'tests\ProtocolTests.cs')
)

& $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$testExe" /reference:System.dll /reference:System.Core.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed with exit code $LASTEXITCODE." }
$assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($testExe))
$result = $assembly.EntryPoint.Invoke($null, $null)
if ([int]$result -ne 0) { throw "Tests failed with exit code $result." }

