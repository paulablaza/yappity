# Measures Yappity's memory the way Task Manager shows it ("Memory" column = private working set).
#   powershell -File tools\benchmark.ps1 [-Exe bin\Yappity.exe]
# Uses a throwaway data folder, so your real settings and history aren't touched.
param([string]$Exe = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Yappity.exe'))
$ErrorActionPreference = 'Stop'
$name = [IO.Path]::GetFileNameWithoutExtension($Exe)
$dataDir = Join-Path $env:TEMP ('yappity-bench-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory $dataDir | Out-Null
'{"ApiKeyProtected":"","Theme":"dark"}' | Set-Content (Join-Path $dataDir 'settings.json') -Encoding ascii
$env:YAPPITY_HOME = $dataDir

function Mem($p) {
    $p.Refresh()
    $c = Get-Counter "\Process($name)\Working Set - Private" -ErrorAction SilentlyContinue
    $tm = if ($c) { $c.CounterSamples[0].CookedValue / 1MB } else { 0 }
    '{0,-34} Task Manager {1,6:N1} MB    working set {2,6:N1} MB    committed {3,6:N1} MB' -f $args[0], $tm, ($p.WorkingSet64 / 1MB), ($p.PrivateMemorySize64 / 1MB)
}

"Yappity memory benchmark ($((Get-Item $Exe).Length / 1KB -as [int]) KB exe)"
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $Exe -ArgumentList '--history' -PassThru
while ($p.MainWindowHandle -eq 0 -and $sw.ElapsedMilliseconds -lt 15000) { Start-Sleep -Milliseconds 50; $p.Refresh() }
"Window open after {0} ms" -f $sw.ElapsedMilliseconds
Start-Sleep 2
Mem $p 'History window open'
$p.CloseMainWindow() | Out-Null
Start-Sleep 7
Mem $p 'Window closed, idle in tray'
Start-Sleep 20
Mem $p 'Idle in tray, 30 s later'
Stop-Process -Id $p.Id -Force
Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue

