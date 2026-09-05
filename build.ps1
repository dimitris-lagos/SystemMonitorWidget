$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src'
$output = Join-Path $root 'artifacts'
$thirdParty = Join-Path $root 'third_party\OpenHardwareMonitor'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$executable = Join-Path $output 'SystemMonitorWidget-v2.5.2.exe'
$helper = Join-Path $output 'SystemMonitorWidget.FanHelper.exe'
$ohmLibrary = Join-Path $thirdParty 'OpenHardwareMonitorLib.dll'
$ohmLicense = Join-Path $thirdParty 'License.html'
$bundle = Join-Path $output 'SystemMonitorWidget-v2.5.2-win-x64.zip'
$checksum = Join-Path $output 'SystemMonitorWidget-v2.5.2-win-x64.sha256.txt'

if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw "C# compiler not found at $compiler" }
if (-not (Test-Path -LiteralPath $ohmLibrary -PathType Leaf)) { throw "OpenHardwareMonitorLib.dll not found at $ohmLibrary" }
New-Item -ItemType Directory -Path $output -Force | Out-Null

$files = @(
    'Program.cs',
    'HWiNFOReader.cs',
    'PhysicalMemory.cs',
    'ProcessUsageSampler.cs',
    'DashboardModel.cs',
    'FanControlModel.cs',
    'FanControlClient.cs',
    'FanCurveEditor.cs',
    'FanControlSettingsPanel.cs',
    'WidgetConfigV2.cs',
    'RoleDefinitionsV2.cs',
    'WidgetComponents.cs',
    'DashboardEditor.cs',
    'WidgetFormV2.cs',
    'SettingsFormV2.cs',
    'Properties\AssemblyInfo.cs'
) | ForEach-Object { Join-Path $source $_ }

& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$executable" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $files
if ($LASTEXITCODE -ne 0) { throw "Widget compilation failed with exit code $LASTEXITCODE" }

& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$helper" `
    "/win32manifest:$(Join-Path $source 'FanControlHelper.manifest')" `
    /reference:System.dll `
    /reference:System.Core.dll `
    "/reference:$ohmLibrary" `
    (Join-Path $source 'FanControlHelperProgram.cs') `
    (Join-Path $source 'Properties\AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw "Fan helper compilation failed with exit code $LASTEXITCODE" }

$bundledLibrary = Join-Path $output 'OpenHardwareMonitorLib.dll'
$bundledLicense = Join-Path $output 'OpenHardwareMonitor-License.html'
Copy-Item -LiteralPath $ohmLibrary -Destination $bundledLibrary -Force
Copy-Item -LiteralPath $ohmLicense -Destination $bundledLicense -Force
if (Test-Path -LiteralPath $bundle) { Remove-Item -LiteralPath $bundle -Force }
Compress-Archive -LiteralPath $executable, $helper, $bundledLibrary, $bundledLicense -DestinationPath $bundle -CompressionLevel Optimal
$bundleHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $bundle).Hash
[IO.File]::WriteAllText($checksum, $bundleHash + '  ' + [IO.Path]::GetFileName($bundle) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $executable, $helper, $bundledLibrary, $bundledLicense, $bundle, $checksum