$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found.' }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\ScreenLite.ico" "/out:$PSScriptRoot\ScreenLite.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll "$PSScriptRoot\ScreenLite.cs" "$PSScriptRoot\Appearance.cs" "$PSScriptRoot\AssemblyInfo.cs" "$PSScriptRoot\DisplaySystem.cs" "$PSScriptRoot\DisplaySettingsPanel.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Built: $PSScriptRoot\ScreenLite.exe"



