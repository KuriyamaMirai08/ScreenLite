$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found.' }
$testFolder = Join-Path $PSScriptRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $testFolder | Out-Null
$commonSources = @('ScreenLite.cs','Appearance.cs','DisplaySystem.cs','DisplaySettingsPanel.cs','AssemblyInfo.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
foreach ($testName in @('DisplayTests','DpiTests')) {
    $testExe = Join-Path $testFolder ($testName + '.exe')
    $source = Join-Path $PSScriptRoot ('Tests\' + $testName + '.cs')
    & $compiler /nologo "/out:$testExe" "/main:$testName" "/win32manifest:$PSScriptRoot\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll $source $commonSources
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ScreenLite.exe.config') -Destination ($testExe + '.config') -Force
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
