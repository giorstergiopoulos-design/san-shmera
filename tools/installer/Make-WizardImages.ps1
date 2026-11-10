<#
  Makes the pictures of the installer from the icon of the app: installer\assets\wizard-large.bmp (164 x 314, the left side of the first and last pages)
  and installer\assets\wizard-small.bmp (55 x 55, the corner of the other pages). The Inno Setup script uses them.
  Usage: powershell -File Make-WizardImages.ps1
#>
Add-Type -AssemblyName System.Drawing
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$icon = [System.Drawing.Image]::FromFile((Join-Path $root 'src\Chronikologio.Windows\chronikologio-256.png'))
$out = Join-Path $root 'installer\assets'
New-Item -ItemType Directory -Force $out | Out-Null

function New-Canvas([int]$w, [int]$h, [System.Drawing.Color]$back) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear($back)
    return @($bmp, $g)
}

# large: dark panel in the colours of the app, the icon, the name, an accent stripe
$bmp, $g = New-Canvas 164 314 ([System.Drawing.Color]::FromArgb(24, 24, 28))
$accent = [System.Drawing.Color]::FromArgb(0, 188, 212)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $accent), 0, 0, 6, 314)
$g.DrawImage($icon, 22, 40, 120, 120)
$font = New-Object System.Drawing.Font 'Segoe UI', 15, ([System.Drawing.FontStyle]::Bold)
$sub = New-Object System.Drawing.Font 'Segoe UI', 9
$center = New-Object System.Drawing.StringFormat; $center.Alignment = [System.Drawing.StringAlignment]::Center
$g.DrawString("Χρονικολόγιο", $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 6, 176, 158, 30), $center)
$g.DrawString("Timeline Calendar", $sub, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(154, 154, 168))), (New-Object System.Drawing.RectangleF 6, 206, 158, 20), $center)
$g.DrawString("Εορτολόγιο · Σαν σήμερα", $sub, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(154, 154, 168))), (New-Object System.Drawing.RectangleF 6, 280, 158, 20), $center)
$g.Dispose(); $bmp.Save((Join-Path $out 'wizard-large.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp); $bmp.Dispose()

# small: the icon on white
$bmp, $g = New-Canvas 55 55 ([System.Drawing.Color]::White)
$g.DrawImage($icon, 3, 3, 49, 49)
$g.Dispose(); $bmp.Save((Join-Path $out 'wizard-small.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp); $bmp.Dispose()
$icon.Dispose()
Write-Host 'wizard-large.bmp and wizard-small.bmp written'
