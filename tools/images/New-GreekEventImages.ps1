<#
  Finds free photographs for the Greek and Cypriot events written in data\events\events*.tsv (they have no encyclopedia article of their own).
  For each event the title is searched in the Greek encyclopedia, the lead image of the best matching article is taken, and its licence is checked on Wikimedia Commons
  (public domain, CC0, CC BY only). The candidates go to tools\images\events\greek-manifest.json; after looking at the contact sheets (Make-ContactSheets.ps1 -Manifest ...)
  the approved numbers go to tools\images\events\greek-approved.txt and  -Download  saves them and appends their lines to data\images\eventimages-gr.tsv.
  Honest user agent, one request every 2 seconds.
#>
param([switch]$Download)
$ErrorActionPreference = 'Continue'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$ua = 'Chronikologio-ImageTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$outDir = Join-Path $PSScriptRoot 'events'
$imgDir = Join-Path $root 'data\images\photos\events'
New-Item -ItemType Directory -Force $outDir, $imgDir | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
$licenseMap = @{ 'Public domain' = 'Public Domain'; 'CC0' = 'CC0 1.0'; 'CC BY 4.0' = 'CC BY 4.0'; 'CC BY 3.0' = 'CC BY 3.0'; 'CC BY 2.5' = 'CC BY 2.5'; 'CC BY 2.0' = 'CC BY 2.0' }
function Strip-Html([string]$s) { if (-not $s) { return '' }; ([regex]::Replace($s, '<[^>]+>', '') -replace '&amp;', '&' -replace '&quot;', '"' -replace '\s+', ' ').Trim() }
function Api([string]$url) { $r = Invoke-RestMethod -UserAgent $ua -Uri $url; Start-Sleep -Seconds 2; return $r }

$manifestFile = Join-Path $outDir 'greek-manifest.json'
$manifest = @()
if (Test-Path $manifestFile) { $manifest = @(Get-Content $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ }) }
$done = @{}; foreach ($m in $manifest) { $done["$($m.key)|$($m.year)|$($m.title)"] = $true }

$events = @()
foreach ($f in Get-ChildItem (Join-Path $root 'data\events\events*.tsv')) {
    foreach ($l in Get-Content $f.FullName -Encoding UTF8) {
        if ($l.StartsWith('#') -or [string]::IsNullOrWhiteSpace($l)) { continue }
        $x = $l.Split('|'); if ($x.Count -lt 5) { continue }
        if ($x[2] -notin 'Ελλάδα', 'Κύπρος', 'Μικρά Ασία', 'Πόντος') { continue }
        $events += [pscustomobject]@{ key = $x[0]; year = $x[1]; region = $x[2]; title = $x[3] }
    }
}
foreach ($e in $events) {
    if ($done.ContainsKey("$($e.key)|$($e.year)|$($e.title)")) { continue }
    $best = $null
    try {
        $q = [uri]::EscapeDataString(($e.title -replace '[«»"“”]', ''))
        $s = Api "https://el.wikipedia.org/w/api.php?action=query&format=json&list=search&srlimit=1&srsearch=$q"
        $hit = $s.query.search | Select-Object -First 1
        if ($hit) {
            $pi = Api ('https://el.wikipedia.org/w/api.php?action=query&format=json&prop=pageimages&piprop=name&titles=' + [uri]::EscapeDataString($hit.title))
            $page = $pi.query.pages.PSObject.Properties.Value | Select-Object -First 1
            if ($page.pageimage) {
                $file = 'File:' + ($page.pageimage -replace '_', ' ')
                $ii = Api ('https://commons.wikimedia.org/w/api.php?action=query&format=json&prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=1280&iiextmetadatafilter=LicenseShortName|Artist|ImageDescription&titles=' + [uri]::EscapeDataString($file))
                $p = $ii.query.pages.PSObject.Properties.Value | Select-Object -First 1
                $i = $p.imageinfo[0]
                $lic = $licenseMap[[string]$i.extmetadata.LicenseShortName.value]
                if ($i -and $lic -and $i.mime -in 'image/jpeg', 'image/png' -and [math]::Max($i.width, $i.height) -ge 900) {
                    $url = if ($i.width -gt 1280 -and $i.thumburl) { $i.thumburl } else { $i.url }
                    $best = [pscustomobject]@{ article = $hit.title; title = $p.title; url = $url; page = $i.descriptionurl; license = $lic; creator = (Strip-Html $i.extmetadata.Artist.value); caption = (Strip-Html $i.extmetadata.ImageDescription.value); mime = $i.mime }
                }
            }
        }
    } catch { Write-Host "$($e.key) $($e.title): FAILED $($_.Exception.Message)"; if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 30 } }
    $manifest += [pscustomobject]@{ key = $e.key; year = $e.year; region = $e.region; title = $e.title; found = [bool]$best; candidate = $best }
    Write-Host ("{0} {1}: {2}" -f $e.key, $e.title, $(if ($best) { $best.title } else { '-' }))
    [IO.File]::WriteAllText($manifestFile, ($manifest | ConvertTo-Json -Depth 5), $utf8)
}
if (-not $Download) { Write-Host "candidates: $(@($manifest | Where-Object found).Count) of $($manifest.Count)"; exit }

$approvedFile = Join-Path $outDir 'greek-approved.txt'
$approved = @{}
if (Test-Path $approvedFile) { foreach ($l in Get-Content $approvedFile -Encoding UTF8) { if ($l -match '^\d+$') { $approved[[int]$l] = $true } } }
$lines = New-Object Collections.Generic.List[string]
$lines.Add('# MM-DD|year|title|file|caption|creator|license|sourceUrl   (made by tools\images\New-GreekEventImages.ps1; photographs from Wikimedia Commons, free licences only)')
$seq = 0; $n = 0
foreach ($m in $manifest) {
    if (-not $m.found) { continue }
    $seq++
    if (-not $approved.ContainsKey($seq)) { continue }
    $c = $m.candidate
    $ext = if ($c.mime -eq 'image/png') { '.png' } else { '.jpg' }
    $name = ('{0}-gr-{1}{2}' -f $m.key, ($c.article -replace '[^\p{L}\p{N}]+', '-').Trim('-').ToLowerInvariant().GetHashCode().ToString('x'), $ext)
    $path = Join-Path $imgDir $name
    if (-not (Test-Path $path)) { try { Invoke-WebRequest -UserAgent $ua -Uri $c.url -OutFile $path -UseBasicParsing; Start-Sleep -Seconds 2 } catch { Write-Host "download failed $name"; continue } }
    $creator = if ($c.creator) { $c.creator } else { 'Unknown author' }
    $caption = ($c.caption -replace '\|', '/'); if ($caption.Length -gt 160) { $caption = $caption.Substring(0, 160) + '…' }; if (-not $caption) { $caption = ($c.title -replace '^File:', '') }
    $lines.Add("$($m.key)|$($m.year)|$($m.title)|photos/events/$name|$caption|$($creator -replace '\|','/')|$($c.license)|$($c.page)")
    $n++
}
[IO.File]::WriteAllLines((Join-Path $root 'data\images\eventimages-gr.tsv'), $lines, $utf8)
Write-Host "downloaded and listed $n photographs"
