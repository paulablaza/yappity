# Makes the small mascot images that get built into Yappity.exe:
#   assets\mascot\<pose>.png       full body, 320 px tall (empty history, welcome)
#   assets\mascot\face-<pose>.png  head crop, 96 px (the bubble)
# Source images are in design\mascots\chatter-chan.
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$src = Join-Path $root 'design\mascots\chatter-chan'
$out = Join-Path $root 'assets\mascot'
New-Item -ItemType Directory -Force $out | Out-Null

function Bounds($bmp) {
    $minX = $bmp.Width; $minY = $bmp.Height; $maxX = 0; $maxY = 0
    for ($y = 0; $y -lt $bmp.Height; $y += 2) {
        for ($x = 0; $x -lt $bmp.Width; $x += 2) {
            if ($bmp.GetPixel($x, $y).A -gt 40) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    return @($minX, $minY, $maxX, $maxY)
}

function Save($bmp, $rect, $w, $h, $path) {
    $dst = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($dst)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.SmoothingMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, $w, $h), $rect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose(); $dst.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $dst.Dispose()
}

foreach ($f in Get-ChildItem $src -Filter 'pose-*.png') {
    $name = ($f.BaseName -replace '^pose-\d-', '')
    $bmp = New-Object System.Drawing.Bitmap $f.FullName
    $b = Bounds $bmp
    $bw = $b[2] - $b[0]; $bh = $b[3] - $b[1]

    # Full body, trimmed to the character with a little padding, 320 px tall.
    $pad = 12
    $full = New-Object System.Drawing.Rectangle ([math]::Max(0, $b[0] - $pad)), ([math]::Max(0, $b[1] - $pad)), ($bw + 2 * $pad), ($bh + 2 * $pad)
    $h = 320; $w = [int]($full.Width * $h / $full.Height)
    Save $bmp $full $w $h (Join-Path $out "$name.png")

    # Face: the hair takes up roughly the top 42% of a standing pose.
    $faceH = [int]($bh * $(if ($name -eq 'idle') { 0.52 } else { 0.42 }))
    $row = [int]($b[1] + $faceH * 0.55)
    $l = $bmp.Width; $r = 0
    for ($x = 0; $x -lt $bmp.Width; $x++) { for ($y = $b[1]; $y -lt $b[1] + $faceH; $y += 3) { if ($bmp.GetPixel($x, $y).A -gt 40) { if ($x -lt $l) { $l = $x }; if ($x -gt $r) { $r = $x }; break } } }
    $size = [int]($faceH * 0.98)
    $cx = [int](($l + $r) / 2)
    $face = New-Object System.Drawing.Rectangle ([math]::Max(0, $cx - $size / 2)), ([math]::Max(0, $b[1] - 6)), $size, $size
    Save $bmp $face 96 96 (Join-Path $out "face-$name.png")
    $bmp.Dispose()
    Write-Host "$name done"
}
