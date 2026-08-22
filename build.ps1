$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src'
$output = Join-Path $root 'artifacts'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$executable = Join-Path $output 'SystemMonitorWidget-v2.1.exe'

if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "C# compiler not found at $compiler"
}

New-Item -ItemType Directory -Path $output -Force | Out-Null

$files = @(
    'Program.cs',
    'HWiNFOReader.cs',
    'PhysicalMemory.cs',
    'ProcessUsageSampler.cs',
    'DashboardModel.cs',
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

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Get-Item -LiteralPath $executable
