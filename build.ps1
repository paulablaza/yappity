# Builds bin\Yappity.exe with the C# compiler that ships with Windows.
# Nothing to install: run  powershell -ExecutionPolicy Bypass -File build.ps1
param([string]$Out = 'bin\Yappity.exe')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) { throw ".NET Framework 4.x compiler not found at $csc" }

$wpf = Join-Path $fw 'WPF'
$refs = 'System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Net.Http.dll,System.Web.Extensions.dll,System.Security.dll,System.Xaml.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll'
$sources = Get-ChildItem src -Filter *.cs | ForEach-Object { $_.FullName }
New-Item -ItemType Directory -Force bin | Out-Null

# Chatter-Chan images and the icon are made by tools\make-art.py and built into the .exe.
$res = Get-ChildItem assets\mascot -Filter '*.png' | ForEach-Object { '/resource:' + $_.FullName + ',' + $_.Name }

$cscArgs = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/utf8output',
    "/out:$Out", "/lib:$fw,$wpf", "/r:$refs", '/win32manifest:assets\app.manifest', '/win32icon:assets\icon.ico') + $res + $sources
$output = & $csc @cscArgs 2>&1
$output | Where-Object { $_ -notmatch 'This compiler is provided as part of the Microsoft|C# 5|Version 4\.|For compilers that support newer|go\.microsoft\.com|^\s*$' } | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$size = [math]::Round((Get-Item $Out).Length / 1KB)
Write-Host "Built $Out ($size KB)"
