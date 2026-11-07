# Turns a 4-frame sprite sheet into a looping GIF on a soft lavender card.
#   powershell -File tools\make-gif.ps1 -Sheet <sheet.png> -Out <out.gif>
param([string]$Sheet, [string]$Out, [int]$Frames = 4, [int]$Size = 360, [int]$Ms = 110)
Add-Type -AssemblyName System.Drawing
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("yappity-gif-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null

$src = New-Object System.Drawing.Bitmap $Sheet
$tileW = [int]($src.Width / $Frames)

# Find each frame's character (anything that isn't near-white or transparent).
$boxes = @()
for ($f = 0; $f -lt $Frames; $f++) {
    $minX = 99999; $minY = 99999; $maxX = 0; $maxY = 0
    for ($y = 0; $y -lt $src.Height; $y += 2) {
        for ($x = $f * $tileW; $x -lt ($f + 1) * $tileW; $x += 2) {
            $c = $src.GetPixel($x, $y)
            if ($c.A -gt 40 -and ($c.R -lt 238 -or $c.G -lt 238 -or $c.B -lt 238)) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    $boxes += ,@($minX, $minY, $maxX, $maxY)
}

# One shared scale so she doesn't change size between frames, aligned by her feet.
$maxH = ($boxes | ForEach-Object { $_[3] - $_[1] } | Measure-Object -Maximum).Maximum
$maxW = ($boxes | ForEach-Object { $_[2] - $_[0] } | Measure-Object -Maximum).Maximum
$scale = [math]::Min(($Size * 0.86) / $maxH, ($Size * 0.86) / $maxW)

for ($f = 0; $f -lt $Frames; $f++) {
    $b = $boxes[$f]
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'HighQuality'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $card = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 48; $s = $Size - 1
    $card.AddArc(0, 0, $r, $r, 180, 90); $card.AddArc($s - $r, 0, $r, $r, 270, 90); $card.AddArc($s - $r, $s - $r, $r, $r, 0, 90); $card.AddArc(0, $s - $r, $r, $r, 90, 90); $card.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(238, 235, 255))), $card)
    $w = ($b[2] - $b[0]) * $scale; $h = ($b[3] - $b[1]) * $scale
    $cx = ($b[0] + $b[2]) / 2
    $dx = $Size / 2 - $w / 2 + (($b[0] + $b[2]) / 2 - $cx) * $scale
    $dy = $Size * 0.93 - $h
    # Draw without the white background: copy only non-white pixels via a color key.
    $attr = New-Object System.Drawing.Imaging.ImageAttributes
    $attr.SetColorKey([System.Drawing.Color]::FromArgb(242, 242, 242), [System.Drawing.Color]::FromArgb(255, 255, 255))
    $g.SetClip($card)
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle ([int]$dx), ([int]$dy), ([int]$w), ([int]$h)), $b[0], $b[1], ($b[2] - $b[0]), ($b[3] - $b[1]), [System.Drawing.GraphicsUnit]::Pixel, $attr)
    $g.Dispose()
    $bmp.Save((Join-Path $tmp ("f{0}.png" -f $f)), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
$src.Dispose()

# Frame order: closed, half, open, half, then hold the closed smile a beat.
$order = @(0, 1, 2, 3, 1, 2, 3, 0)
$list = Join-Path $tmp 'list.txt'
$lines = foreach ($i in $order) { "file 'f$i.png'"; "duration " + ($Ms / 1000.0).ToString([Globalization.CultureInfo]::InvariantCulture) }
$lines += "file 'f0.png'"
Set-Content $list $lines -Encoding ascii
Push-Location $tmp
ffmpeg -v error -y -f concat -safe 0 -i list.txt -vf "split[a][b];[a]palettegen=reserve_transparent=1[p];[b][p]paletteuse=alpha_threshold=128" -loop 0 out.gif
Pop-Location
Copy-Item (Join-Path $tmp 'out.gif') $Out -Force
Remove-Item $tmp -Recurse -Force
Write-Host ("Saved {0} ({1} KB)" -f $Out, [math]::Round((Get-Item $Out).Length / 1KB))
