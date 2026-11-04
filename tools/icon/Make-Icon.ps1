<#
  Draws the icon of Chronikologio (a calendar page with the Greek letter Χ on a blue tile) and saves it as a multi-size .ico (PNG images inside, 16 to 256 px).
  Usage: powershell -File Make-Icon.ps1 [-Out <path to .ico>]
#>
param([string]$Out = (Join-Path $PSScriptRoot '..\..\src\Chronikologio.Windows\chronikologio.ico'))
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function New-Frame([int]$s) {
    $bmp = [System.Drawing.Bitmap]::new($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.Clear([System.Drawing.Color]::Transparent)
    $u = $s / 256.0

    function Round-Rect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = $r * 2
        $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
        $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
        $p.CloseFigure(); return $p
    }
    # blue tile
    $tile = Round-Rect (8 * $u) (8 * $u) (240 * $u) (240 * $u) (52 * $u)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 24, 52, 94))), $tile)
    # white calendar sheet
    $sheet = Round-Rect (44 * $u) (60 * $u) (168 * $u) (156 * $u) (20 * $u)
    $g.FillPath([System.Drawing.Brushes]::White, $sheet)
    # header band in the accent colour of the app
    $g.SetClip($sheet)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 188, 212))), (44 * $u), (60 * $u), (168 * $u), (44 * $u))
    $g.ResetClip()
    # binder rings
    foreach ($x in 88, 168) {
        $ring = Round-Rect (($x - 9) * $u) (44 * $u) (18 * $u) (36 * $u) (9 * $u)
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 230, 235, 242))), $ring)
        $g.DrawPath([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 24, 52, 94), [float][Math]::Max(1.0, 3 * $u)), $ring)
    }
    # the letter Chi
    $fontSize = 112 * $u
    if ($s -ge 24) {
        $font = New-Object System.Drawing.Font 'Georgia', $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
        $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
        $rect = New-Object System.Drawing.RectangleF (44 * $u), (100 * $u), (168 * $u), (116 * $u)
        $g.DrawString([string][char]0x03A7, $font, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 24, 52, 94))), $rect, $sf)
    } else {
        $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 24, 52, 94), [float][Math]::Max(1.5, 22 * $u))
        $g.DrawLine($pen, (84 * $u), (120 * $u), (172 * $u), (200 * $u)); $g.DrawLine($pen, (172 * $u), (120 * $u), (84 * $u), (200 * $u))
    }
    $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @()
foreach ($s in $sizes) {
    $b = New-Frame $s
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , @{ Size = $s; Bytes = $ms.ToArray() }
    $b.Dispose(); $ms.Dispose()
}
$dir = Split-Path -Parent $Out
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
    $dim = if ($p.Size -ge 256) { 0 } else { $p.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]$p.Bytes.Length); $w.Write([UInt32]$offset)
    $offset += $p.Bytes.Length
}
foreach ($p in $pngs) { $w.Write($p.Bytes) }
$w.Close(); $fs.Close()
# a PNG of the largest size next to it, for the documentation and the installer's wizard image
[System.IO.File]::WriteAllBytes((Join-Path $dir 'chronikologio-256.png'), $pngs[-1].Bytes)
Write-Host "icon written: $Out ($($pngs.Count) sizes)"
