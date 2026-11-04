<#
  Makes contact sheets of the candidate photographs of events (tools\images\events\manifest.json) so they can be looked at before anything ships:
  20 thumbnails per sheet (5 x 4), each with the date, the article and the file name.
  Thumbnails are small copies (320 px) kept in the folder given by -Work (a scratch folder, not in the app).
  Usage: powershell -File Make-ContactSheets.ps1 -Work <folder>
#>
param([Parameter(Mandatory)] [string]$Work, [string]$Manifest = 'events\manifest.json')
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Drawing
$ua = 'Chronikologio-ImageTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
New-Item -ItemType Directory -Force $Work | Out-Null
$items = @(@(Get-Content (Join-Path $PSScriptRoot $Manifest) -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ }) | Where-Object found)
$i = 0
foreach ($m in $items) {
    $i++
    $file = Join-Path $Work ('{0:000}.img' -f $i)
    if (-not (Test-Path $file)) {
        $u = ($m.candidate.url -replace '\?.*$', '') -replace '/\d+px-', '/330px-'   # 330 is one of the standard thumbnail widths of Wikimedia (20, 40, 60, 120, 250, 330, 500, 960, 1280, 1920, 3840)
        try { Invoke-WebRequest -UserAgent $ua -Uri $u -OutFile $file -UseBasicParsing; Start-Sleep -Milliseconds 800 } catch { Write-Host "thumb $i failed: $($_.Exception.Message)"; if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 30 } }
    }
}
$cols = 5; $rows = 4; $cw = 300; $ch = 250
$font = New-Object System.Drawing.Font('Segoe UI', 8)
for ($s = 0; $s * 20 -lt $items.Count; $s++) {
    $bmp = New-Object System.Drawing.Bitmap ($cols * $cw), ($rows * $ch)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::White)
    for ($k = 0; $k -lt 20; $k++) {
        $idx = $s * 20 + $k
        if ($idx -ge $items.Count) { break }
        $m = $items[$idx]
        $x = ($k % $cols) * $cw; $y = [math]::Floor($k / $cols) * $ch
        $f = Join-Path $Work ('{0:000}.img' -f ($idx + 1))
        if (Test-Path $f) {
            try {
                $bytes = [IO.File]::ReadAllBytes($f); $ms = New-Object IO.MemoryStream (, $bytes)
                $img = [System.Drawing.Image]::FromStream($ms)
                $scale = [math]::Min(($cw - 8) / $img.Width, 190 / $img.Height)
                $g.DrawImage($img, $x + 4, $y + 4, [int]($img.Width * $scale), [int]($img.Height * $scale))
                $img.Dispose()
            } catch { }
        }
        $name = if ($m.article) { $m.article } else { $m.title }
        $label = ('#{0} {1} {2}' -f ($idx + 1), $m.key, $name) + "`n" + ($m.candidate.title -replace '^File:', '')
        $rect = New-Object System.Drawing.RectangleF ($x + 4), ($y + 196), ($cw - 8), 52
        $g.DrawString($label, $font, [System.Drawing.Brushes]::Black, $rect)
        $g.DrawRectangle([System.Drawing.Pens]::LightGray, $x, $y, $cw - 1, $ch - 1)
    }
    $g.Dispose()
    $bmp.Save((Join-Path $Work ('sheet-{0:00}.png' -f ($s + 1))), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
Write-Host "sheets: $([math]::Ceiling($items.Count / 20))"
