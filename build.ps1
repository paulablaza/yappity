# Builds bin\Yappity.exe with the C# compiler that ships with Windows.
# Nothing to install: run  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) { throw ".NET Framework 4.x compiler not found at $csc" }

$refs = 'System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Net.Http.dll,System.Web.Extensions.dll,System.Security.dll'
$sources = Get-ChildItem src -Filter *.cs | ForEach-Object { $_.FullName }
New-Item -ItemType Directory -Force bin | Out-Null

function Compile($out, $extra) {
    $cscArgs = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/utf8output',
        "/out:$out", "/r:$refs", '/win32manifest:assets\app.manifest') + $extra + $sources
    $output = & $csc @cscArgs 2>&1
    $output | Where-Object { $_ -notmatch 'This compiler is provided as part of the Microsoft|C# 5|Version 4\.|For compilers that support newer|go\.microsoft\.com|^\s*$' } | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}

# The .exe icon is drawn by the app itself, so the first build makes it.
if (-not (Test-Path assets\icon.ico)) {
    Compile 'bin\icon-maker.exe' @()
    & .\bin\icon-maker.exe --write-icon assets\icon.ico | Out-Null
    Remove-Item bin\icon-maker.exe
}

Compile 'bin\Yappity.exe' @('/win32icon:assets\icon.ico')
$size = [math]::Round((Get-Item bin\Yappity.exe).Length / 1KB)
Write-Host "Built bin\Yappity.exe ($size KB)"
