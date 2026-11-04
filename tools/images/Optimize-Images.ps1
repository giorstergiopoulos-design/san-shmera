<#
  Makes the photographs and icons of data\images smaller so the installer is smaller: no side longer than -MaxSide pixels, JPEG at -Quality.
  The images are only ever shown beside the text (a few hundred pixels wide), so the originals from Wikimedia Commons are far larger than needed.
  A file is rewritten only when the new file is smaller. The source page and licence of each image stay in the data files.
  Usage: powershell -File Optimize-Images.ps1 [-MaxSide 900] [-Quality 80]
#>
param([int]$MaxSide = 900, [int]$Quality = 80)
Add-Type -AssemblyName System.Drawing
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..\data\images')
$jpeg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
$params = New-Object System.Drawing.Imaging.EncoderParameters 1
$params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([int64]$Quality)
$before = 0; $after = 0; $changed = 0
foreach ($f in Get-ChildItem $root -Recurse -File | Where-Object { $_.Extension -in '.jpg', '.jpeg', '.png' }) {
    $before += $f.Length
    try {
        $bytes = [IO.File]::ReadAllBytes($f.FullName)
        $ms = New-Object IO.MemoryStream (, $bytes)
        $img = [System.Drawing.Image]::FromStream($ms)
        $scale = [math]::Min(1.0, $MaxSide / [double][math]::Max($img.Width, $img.Height))
        if ($scale -ge 1.0 -and $bytes.Length -lt 350KB) { $img.Dispose(); $ms.Dispose(); $after += $bytes.Length; continue }   # already small: not re-encoded again (every re-encoding loses a little quality)
        $w = [int][math]::Round($img.Width * $scale); $h = [int][math]::Round($img.Height * $scale)
        $isPng = $f.Extension -eq '.png'
        $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        if (-not $isPng) { $g.Clear([System.Drawing.Color]::White) }
        $g.DrawImage($img, 0, 0, $w, $h); $g.Dispose(); $img.Dispose(); $ms.Dispose()
        $out = New-Object IO.MemoryStream
        if ($isPng) { $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png) } else { $bmp.Save($out, $jpeg, $params) }
        $bmp.Dispose()
        if ($out.Length -lt $bytes.Length) { [IO.File]::WriteAllBytes($f.FullName, $out.ToArray()); $after += $out.Length; $changed++ } else { $after += $bytes.Length }
        $out.Dispose()
    } catch { Write-Host "skipped $($f.Name): $($_.Exception.Message)"; $after += $f.Length }
}
"{0} files rewritten; {1:N1} MB -> {2:N1} MB" -f $changed, ($before / 1MB), ($after / 1MB)
