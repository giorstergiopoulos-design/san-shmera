<#
  Finds free photographs for the documented events (the ones with an encyclopedia article, tools\history\curated\*.tsv) on Wikimedia Commons,
  checks the licence of each file (public domain, CC0, CC BY only; share-alike is left out), downloads the best one at no more than 1280 px wide
  and writes data\images\eventimages.tsv for the app. Everything honest: user agent with a contact address, one request per 2 seconds.
  The text of the app is its own; only the photographs come from Commons, each with its creator, licence and source page.
  Review: tools\images\events\sheet-N.png (contact sheets) lets you look at the pictures before they ship; names in tools\images\events\rejected.txt are skipped.
  Usage: powershell -File New-EventImages.ps1 [-Download]
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

$years = @{}
foreach ($f in Get-ChildItem (Join-Path $root 'tools\history\out\*.json')) {
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($p in $j.PSObject.Properties) { foreach ($c in @($p.Value)) { $years["$($p.Name)|$($c.article)"] = $c.year } }
}
$rejected = @()
$rf = Join-Path $outDir 'rejected.txt'
if (Test-Path $rf) { $rejected = @(Get-Content $rf -Encoding UTF8 | Where-Object { $_ -and -not $_.StartsWith('#') }) }

$manifestFile = Join-Path $outDir 'manifest.json'
$manifest = @()
if (Test-Path $manifestFile) { $manifest = @(Get-Content $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ }) }
$done = @{}; foreach ($m in $manifest) { $done["$($m.key)|$($m.article)"] = $true }

foreach ($f in Get-ChildItem (Join-Path $root 'tools\history\curated\*.tsv')) {
    foreach ($line in Get-Content $f.FullName -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) { continue }
        $x = $line.Split('|'); if ($x.Count -ne 5) { continue }
        $key = $x[0]; $article = $x[1]
        if ($done.ContainsKey("$key|$article")) { continue }
        $year = $years["$key|$article"]
        $words = @(($article -replace '_', ' ' -replace '\(.*?\)', '') -split '\s+' | Where-Object { $_.Length -ge 4 } | ForEach-Object { $_.ToLowerInvariant() })
        $q = ($article -replace '_', ' ')
        $u = 'https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=12&gsrsearch=' + [uri]::EscapeDataString($q + ' filetype:bitmap') +
             '&prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=1280&iiextmetadatafilter=LicenseShortName|Artist|ImageDescription'
        $best = $null
        try {
            $r = Invoke-RestMethod -UserAgent $ua -Uri $u
            if ($r.query) {
                foreach ($p in ($r.query.pages.PSObject.Properties.Value | Sort-Object index)) {
                    $i = $p.imageinfo[0]
                    $lic = $licenseMap[[string]$i.extmetadata.LicenseShortName.value]
                    if (-not $lic -or $i.mime -notin 'image/jpeg', 'image/png') { continue }
                    if ([math]::Max($i.width, $i.height) -lt 900) { continue }
                    $title = ($p.title -replace '^File:', '').ToLowerInvariant()
                    if ($title -match 'map|flag|coat of arms|logo|diagram|chart|signature|stamp|banknote|coin') { continue }
                    $score = 0; foreach ($w in $words) { if ($title.Contains($w)) { $score += 2 } }
                    if ($score -eq 0) { continue }
                    $url = if ($i.width -gt 1280 -and $i.thumburl) { $i.thumburl } else { $i.url }
                    $cand = [pscustomobject]@{ score = $score; title = $p.title; url = $url; page = $i.descriptionurl; license = $lic; creator = (Strip-Html $i.extmetadata.Artist.value); caption = (Strip-Html $i.extmetadata.ImageDescription.value); mime = $i.mime }
                    if (-not $best -or $cand.score -gt $best.score) { $best = $cand }
                }
            }
        } catch { Write-Host "$key $article : FAILED $($_.Exception.Message)"; if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 30 } }
        $entry = [pscustomobject]@{ key = $key; year = $year; article = $article; greekTitle = $x[3]; found = [bool]$best; candidate = $best }
        $manifest += $entry
        Write-Host ("{0} {1}: {2}" -f $key, $article, $(if ($best) { $best.title } else { '-' }))
        [IO.File]::WriteAllText($manifestFile, ($manifest | ConvertTo-Json -Depth 5), $utf8)
        Start-Sleep -Seconds 2
    }
}

if (-not $Download) { Write-Host "manifest: $(@($manifest | Where-Object found).Count) of $($manifest.Count) events have a candidate"; exit }

$lines = New-Object Collections.Generic.List[string]
$lines.Add('# MM-DD|year|title|file|caption|creator|license|sourceUrl   (made by tools\images\New-EventImages.ps1; photographs from Wikimedia Commons, free licences only)')
$n = 0
$approvedFile = Join-Path $outDir 'approved.txt'
$approvedSet = @{}
if (Test-Path $approvedFile) { foreach ($l in Get-Content $approvedFile -Encoding UTF8) { if ($l -match '^\d+$') { $approvedSet[[int]$l] = $true } } }
$seq = 0
foreach ($m in $manifest) {
    if (-not $m.found) { continue }
    $seq++
    if ($approvedSet.Count -gt 0 -and -not $approvedSet.ContainsKey($seq)) { continue }
    if ($rejected -contains $m.candidate.title) { continue }
    $c = $m.candidate
    $ext = if ($c.mime -eq 'image/png') { '.png' } else { '.jpg' }
    $name = ('{0}-{1}{2}' -f $m.key, ($m.article -replace '[^A-Za-z0-9]+', '-').Trim('-').ToLowerInvariant(), $ext)
    $path = Join-Path $imgDir $name
    if (-not (Test-Path $path)) {
        try { Invoke-WebRequest -UserAgent $ua -Uri $c.url -OutFile $path -UseBasicParsing; Start-Sleep -Seconds 2 }
        catch { Write-Host "download failed $name $($_.Exception.Message)"; continue }
    }
    $creator = if ($c.creator) { $c.creator } else { 'Unknown author' }
    $caption = ($c.caption -replace '\|', '/'); if ($caption.Length -gt 160) { $caption = $caption.Substring(0, 160) + '…' }
    if (-not $caption) { $caption = ($c.title -replace '^File:', '') }
    $yearText = if ($m.year) { "$($m.year)" } else { '' }
    $lines.Add("$($m.key)|$yearText|$($m.greekTitle)|photos/events/$name|$caption|$($creator -replace '\|','/')|$($c.license)|$($c.page)")
    $n++
}
[IO.File]::WriteAllLines((Join-Path $root 'data\images\eventimages.tsv'), $lines, $utf8)
Write-Host "downloaded and listed $n photographs"
