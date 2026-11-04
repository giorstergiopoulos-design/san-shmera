<#
  Finds free photographs for the saints listed in tools\images\events\saint-queries.tsv (MM-DD|beginning of the title of the saint|search words). The words are searched on Wikimedia Commons (file namespace),
  the licence of each hit is checked (public domain, CC0, CC BY only), and the first candidates go to tools\images\events\saint-manifest.json.
  After looking at the contact sheets (Make-ContactSheets.ps1 -Manifest events\saint-manifest.json) the approved numbers go to events\saint-approved.txt and -Download
  saves those photographs and writes data\images\saintimages-q.tsv. Honest user agent, one request every 2 seconds.
#>
param([switch]$Download, [int]$PerEvent = 2)
$ErrorActionPreference = 'Continue'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$ua = 'Chronikologio-ImageTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$outDir = Join-Path $PSScriptRoot 'events'
$imgDir = Join-Path $root 'data\images\icons'
New-Item -ItemType Directory -Force $imgDir | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
$licenseMap = @{ 'Public domain' = 'Public Domain'; 'CC0' = 'CC0 1.0'; 'CC BY 4.0' = 'CC BY 4.0'; 'CC BY 3.0' = 'CC BY 3.0'; 'CC BY 2.5' = 'CC BY 2.5'; 'CC BY 2.0' = 'CC BY 2.0' }
function Strip-Html([string]$s) { if (-not $s) { return '' }; ([regex]::Replace($s, '<[^>]+>', '') -replace '&amp;', '&' -replace '&quot;', '"' -replace '\s+', ' ').Trim() }
function Api([string]$url) { $r = Invoke-RestMethod -UserAgent $ua -Uri $url; Start-Sleep -Seconds 2; return $r }

$titles = @{}
foreach ($f in Get-ChildItem (Join-Path $root 'data\events\events*.tsv')) { foreach ($l in Get-Content $f.FullName -Encoding UTF8) { if ($l.StartsWith('#') -or -not $l.Trim()) { continue }; $x = $l.Split('|'); if ($x.Count -ge 5) { $titles["$($x[0])|$($x[1])"] = $x[3] } } }

$manifestFile = Join-Path $outDir 'saint-manifest.json'
$manifest = @()
if (Test-Path $manifestFile) { $manifest = @(Get-Content $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ }) }
$done = @{}; foreach ($m in $manifest) { $done["$($m.key)|$($m.year)"] = $true }

if (-not $Download) {
    foreach ($l in Get-Content (Join-Path $outDir 'saint-queries.tsv') -Encoding UTF8) {
        if ($l.StartsWith('#') -or -not $l.Trim()) { continue }
        $x = $l.Split('|'); $k = "$($x[0])|$($x[1])"
        if ($done.ContainsKey($k)) { continue }
        $n = 0
        try {
            $q = [uri]::EscapeDataString($x[2])
            $r = Api "https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=12&gsrsearch=$q&prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=1280&iiextmetadatafilter=LicenseShortName|Artist|ImageDescription"
            $pages = @($r.query.pages.PSObject.Properties.Value | Sort-Object index)
            foreach ($p in $pages) {
                if ($n -ge $PerEvent) { break }
                $i = $p.imageinfo[0]; if (-not $i) { continue }
                if ($p.title -match '(?i)\b(map|flag|logo|coat of arms|diagram|signature|stamp|banknote|sign)\b') { continue }
                $lic = $licenseMap[[string]$i.extmetadata.LicenseShortName.value]
                if (-not $lic -or $i.mime -notin 'image/jpeg', 'image/png' -or [math]::Max($i.width, $i.height) -lt 900) { continue }
                $url = if ($i.width -gt 1280 -and $i.thumburl) { $i.thumburl } else { $i.url }
                $manifest += [pscustomobject]@{ key = $x[0]; year = $x[1]; title = $x[1]; found = $true; article = $x[2]; candidate = [pscustomobject]@{ title = $p.title; url = $url; page = $i.descriptionurl; license = $lic; creator = (Strip-Html $i.extmetadata.Artist.value); caption = (Strip-Html $i.extmetadata.ImageDescription.value); mime = $i.mime } }
                $n++
            }
        } catch { Write-Host "$k FAILED $($_.Exception.Message)"; if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 30 } }
        if ($n -eq 0) { $manifest += [pscustomobject]@{ key = $x[0]; year = $x[1]; title = $x[1]; found = $false; article = $x[2]; candidate = $null } }
        Write-Host "$k $($x[2]): $n"
        [IO.File]::WriteAllText($manifestFile, ($manifest | ConvertTo-Json -Depth 5), $utf8)
    }
    Write-Host "candidates: $(@($manifest | Where-Object found).Count)"; exit
}

$approved = @{}
$af = Join-Path $outDir 'saint-approved.txt'
if (Test-Path $af) { foreach ($l in Get-Content $af -Encoding UTF8) { if ($l -match '^\d+$') { $approved[[int]$l] = $true } } }
$lines = New-Object Collections.Generic.List[string]
$lines.Add('# MM-DD|title|file|caption|creator|license|sourceUrl   (made by tools\images\New-QueryImages.ps1; photographs from Wikimedia Commons, free licences only)')
$seq = 0; $n = 0; $got = @{}
foreach ($m in $manifest) {
    if (-not $m.found) { continue }
    $seq++
    if (-not $approved.ContainsKey($seq)) { continue }
    if ($got["$($m.key)|$($m.year)"]) { continue }   # one photograph per event
    $c = $m.candidate
    $ext = if ($c.mime -eq 'image/png') { '.png' } else { '.jpg' }
    $name = '{0}-s{1}{2}' -f $m.key, $seq, $ext
    $path = Join-Path $imgDir $name
    if (-not (Test-Path $path)) { try { Invoke-WebRequest -UserAgent $ua -Uri $c.url -OutFile $path -UseBasicParsing; Start-Sleep -Seconds 2 } catch { Write-Host "download failed $name"; continue } }
    $creator = if ($c.creator) { $c.creator } else { 'Unknown author' }
    $caption = ($c.caption -replace '\|', '/'); if ($caption.Length -gt 160) { $caption = $caption.Substring(0, 160) + '…' }; if (-not $caption) { $caption = ($c.title -replace '^File:', '') }
    $lines.Add("$($m.key)|$($m.title)|icons/$name|$caption|$($creator -replace '\|','/')|$($c.license)|$($c.page)")
    $got["$($m.key)|$($m.year)"] = $true; $n++
}
[IO.File]::WriteAllLines((Join-Path $root 'data\images\saintimages-q.tsv'), $lines, $utf8)
Write-Host "downloaded and listed $n photographs"
