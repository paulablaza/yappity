# Builds the Microsoft Store package: bin\Yappity-<version>-x64.msix
# Run:  powershell -ExecutionPolicy Bypass -File build-msix.ps1
# The first run downloads Microsoft's packaging tools (Microsoft.Windows.SDK.BuildTools from
# nuget.org, about 22 MB) into .tools\. The package is unsigned; the Store signs it on upload.
param([string]$Arch = 'x64')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-Location $PSScriptRoot
Add-Type -AssemblyName System.Drawing

# Store versions have four parts and the last one must be 0.
$m = [regex]::Match((Get-Content src\App.cs -Raw), 'Version = "(\d+)\.(\d+)\.(\d+)"')
if (-not $m.Success) { throw "Couldn't read App.Version from src\App.cs" }
$version = '{0}.{1}.{2}.0' -f $m.Groups[1].Value, $m.Groups[2].Value, $m.Groups[3].Value

# ---- Packaging tools ----
$toolsVersion = '10.0.28000.2957'
$tools = Join-Path $PSScriptRoot ".tools\sdk-buildtools-$toolsVersion"
if (-not (Test-Path $tools)) {
    Write-Host "Downloading Microsoft.Windows.SDK.BuildTools $toolsVersion..."
    $zip = "$tools.zip"
    New-Item -ItemType Directory -Force (Split-Path $tools) | Out-Null
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$toolsVersion/microsoft.windows.sdk.buildtools.$toolsVersion.nupkg" -OutFile $zip -UseBasicParsing
    Expand-Archive $zip $tools -Force
    Remove-Item $zip
}
function Tool([string]$name) {
    $t = Get-ChildItem $tools -Recurse -Filter $name | Where-Object { $_.DirectoryName -like '*\x64' } | Select-Object -First 1
    if (-not $t) { throw "$name not found in $tools" }
    return $t.FullName
}
$makeappx = Tool 'makeappx.exe'
$makepri = Tool 'makepri.exe'

# ---- Stage the package folder ----
$stage = Join-Path $PSScriptRoot 'bin\msix'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$img = Join-Path $stage 'Images'
New-Item -ItemType Directory -Force $img | Out-Null

& (Join-Path $PSScriptRoot 'build.ps1') -Out 'bin\msix\Yappity.exe'

(Get-Content packaging\AppxManifest.xml -Raw).Replace('$VERSION$', $version).Replace('$ARCH$', $Arch) |
    Set-Content (Join-Path $stage 'AppxManifest.xml') -Encoding UTF8

# ---- Logos, drawn from the 512 px Yappy-chan logo ----
$art = [System.Drawing.Image]::FromFile((Resolve-Path docs\yappy-logo.png).Path)
function Logo([string]$name, [int]$w, [int]$h, [double]$fill) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $size = [int][math]::Round([math]::Min($w, $h) * $fill)
    $g.DrawImage($art, [int](($w - $size) / 2), [int](($h - $size) / 2), $size, $size)
    $g.Dispose()
    $bmp.Save((Join-Path $img $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
foreach ($s in 100, 200, 400) {
    $k = $s / 100
    Logo "StoreLogo.scale-$s.png" (50 * $k) (50 * $k) 1.0
    Logo "Square44x44Logo.scale-$s.png" (44 * $k) (44 * $k) 1.0
    Logo "Square150x150Logo.scale-$s.png" (150 * $k) (150 * $k) 0.66
    Logo "Wide310x150Logo.scale-$s.png" (310 * $k) (150 * $k) 0.66
}
foreach ($t in 16, 24, 32, 48, 256) {
    Logo "Square44x44Logo.targetsize-$t.png" $t $t 1.0
    Logo "Square44x44Logo.targetsize-$($t)_altform-unplated.png" $t $t 1.0
    Logo "Square44x44Logo.targetsize-$($t)_altform-lightunplated.png" $t $t 1.0
}
$art.Dispose()

# ---- resources.pri, so Windows picks the right logo size ----
$priConfig = Join-Path $env:TEMP 'yappity-priconfig.xml'
& $makepri createconfig /cf $priConfig /dq en-US /o | Out-Null
# One resources.pri with every logo size in it. The default config splits sizes into extra
# files meant for bundles, which a single .msix doesn't load.
$cfg = [xml](Get-Content $priConfig -Raw)
$pack = $cfg.SelectSingleNode('//packaging')
if ($pack) { [void]$pack.ParentNode.RemoveChild($pack) }
$cfg.Save($priConfig)
& $makepri new /pr $stage /cf $priConfig /mn (Join-Path $stage 'AppxManifest.xml') /of (Join-Path $stage 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri failed." }
Remove-Item $priConfig

# ---- Pack ----
$out = Join-Path $PSScriptRoot "bin\Yappity-$version-$Arch.msix"
& $makeappx pack /d $stage /p $out /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx failed." }
$size = [math]::Round((Get-Item $out).Length / 1KB)
Write-Host "Built $out ($size KB)"
