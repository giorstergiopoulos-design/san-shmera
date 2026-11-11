<#
  Finds a photograph for the events of data\events\events-wiki-*.tsv: the picture of the Greek Wikipedia article that the event points to (the page image of the first link of the line of the
  day page), when (1) the article is about the event itself - a word of its title appears in our title or text and it is not a country or a generic page -, (2) the picture is on Wikimedia Commons with a free
  licence (public domain, CC0, CC BY; never share-alike), (3) it is not a map, a flag, a coat of arms, a logo or an SVG. The licence, the author and the page of the picture are read from Commons.
  Output: data\images\photos\events\w-*.jpg and data\images\eventimages-wiki.tsv (MM-DD|year|title|file|caption|creator|license|sourceUrl)
  Honest client name with the repository as the contact, one request every 2 seconds. It can be stopped and run again.
  Usage: powershell -File Get-EventImages.ps1 -Wiki <wiki.tsv>
#>
param([Parameter(Mandatory)] [string]$Wiki)
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-DataTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$imgDir = Join-Path $root 'data\images\photos\events'
New-Item -ItemType Directory -Force $imgDir | Out-Null
$outFile = Join-Path $root 'data\images\eventimages-wiki.tsv'
$utf8 = New-Object System.Text.UTF8Encoding($true)
function Api([string]$url) {
    for ($try = 1; $try -le 5; $try++) {
        try { $r = Invoke-RestMethod -UserAgent $ua -Uri $url -TimeoutSec 60; Start-Sleep -Seconds 2; return $r }
        catch { if ($_.Exception.Message -match '429' -and $try -lt 5) { Start-Sleep -Seconds (60 * $try) } else { throw } }
    }
}
function Strip-Html([string]$s) { if (-not $s) { return '' }; ([regex]::Replace($s, '<[^>]+>', '') -replace '&amp;', '&' -replace '&quot;', '"' -replace '&#039;', "'" -replace '\s+', ' ').Trim() }
function Fold([string]$x) { (($x.Normalize([Text.NormalizationForm]::FormD) -replace '\p{Mn}', '').ToLower() -replace 'ς', 'σ' -replace '[^\p{L}\p{Nd}]+', ' ').Trim() }
$licenseMap = @{ 'Public domain' = 'Public Domain'; 'CC0' = 'CC0 1.0'; 'CC BY 4.0' = 'CC BY 4.0'; 'CC BY 3.0' = 'CC BY 3.0'; 'CC BY 2.5' = 'CC BY 2.5'; 'CC BY 2.0' = 'CC BY 2.0' }
$generic = 'ελλαδα|ηνωμενεσ πολιτειεσ|ηνωμενο βασιλειο|γαλλια|γερμανια|ιταλια|ισπανια|ρωσια|κινα|ιαπωνια|τουρκια|αγγλια|ευρωπη|αμερικη|ασια|αφρικη|β παγκοσμιοσ πολεμοσ|α παγκοσμιοσ πολεμοσ|αθηνα|παρισι|λονδινο|ρωμη|κωνσταντινουπολη|κυπροσ|ηπα|ε σ σ δ|σοβιετικη ενωση|ευρωπαικη ενωση|ο η ε'

# the first link and the score of every event line of the day pages
$link = @{}
foreach ($l in Get-Content $Wiki -Encoding UTF8) {
    $p = $l -split '\|', 6
    if ($p.Count -lt 6 -or $p[1] -ne 'Γεγονότα' -or -not $p[4]) { continue }
    $link["$($p[0])|$($p[2])"] = $p[4]
}
$events = @()
foreach ($f in Get-ChildItem (Join-Path $root 'data\events\events-wiki-*.tsv')) {
    foreach ($l in Get-Content $f.FullName -Encoding UTF8) {
        if ($l -notmatch '^(\d\d-\d\d)\|(-?\d+)\|([^|]*)\|([^|]+)\|(.*)$') { continue }
        $k = "$($Matches[1])|$($Matches[2])"
        if (-not $link.ContainsKey($k)) { continue }
        $events += [pscustomobject]@{ Day = $Matches[1]; Year = $Matches[2]; Title = $Matches[4]; Text = $Matches[5]; Link = $link[$k] }
    }
}
$cand = @($events | Where-Object {
        $lt = Fold $_.Link
        if ($lt -match "^($generic)$") { return $false }
        $words = @($lt -split ' ' | Where-Object { $_.Length -ge 5 })
        $hay = Fold ($_.Title + ' ' + $_.Text)
        foreach ($w in $words) { if ($hay.Contains($w.Substring(0, [Math]::Min($w.Length, 6)))) { return $true } }
        return $false
    })
Write-Host "events: $($events.Count), with an article about the event: $($cand.Count)"

$lines = New-Object Collections.Generic.List[string]; $done = @{}
if (Test-Path $outFile) { foreach ($l in Get-Content $outFile -Encoding UTF8) { $lines.Add($l); if ($l -match '^(\d\d-\d\d)\|(-?\d+)\|') { $done["$($Matches[1])|$($Matches[2])"] = 1 } } }
if ($lines.Count -eq 0) { $lines.Add('# MM-DD|year|title|file|caption|creator|license|sourceUrl   (photographs from Wikimedia Commons, free licences only; made by tools\wikipedia\Get-EventImages.ps1)') }
$todo = @($cand | Where-Object { -not $done.ContainsKey("$($_.Day)|$($_.Year)") })
$seen = @{}; $added = 0
for ($i = 0; $i -lt $todo.Count; $i += 40) {
    $chunk = @($todo[$i..([Math]::Min($i + 39, $todo.Count - 1))])
    try {
        $q = [uri]::EscapeDataString((($chunk | ForEach-Object { $_.Link } | Select-Object -Unique) -join '|'))
        $r = Api "https://el.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&redirects=1&prop=pageimages&piprop=name&titles=$q"
        $fileOf = @{}; foreach ($pg in @($r.query.pages)) { if ($pg.pageimage) { $fileOf[$pg.title] = $pg.pageimage } }
        $redir = @{}; foreach ($x in @($r.query.redirects)) { if ($x) { $redir[$x.from] = $x.to } }
        $norm = @{}; foreach ($x in @($r.query.normalized)) { if ($x) { $norm[$x.from] = $x.to } }
        $files = @($chunk | ForEach-Object { $t = $_.Link; if ($norm.ContainsKey($t)) { $t = $norm[$t] }; if ($redir.ContainsKey($t)) { $t = $redir[$t] }; $fileOf[$t] } | Where-Object { $_ } | Select-Object -Unique)
        if ($files.Count -eq 0) { continue }
        $c = Api ("https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2&prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=900&iiextmetadatafilter=LicenseShortName|Artist|ImageDescription&titles=" + [uri]::EscapeDataString((($files | ForEach-Object { "File:$_" }) -join '|')))
        $info = @{}; foreach ($pg in @($c.query.pages)) { if ($pg.imageinfo) { $info[($pg.title -replace '^File:', '' -replace '_', ' ')] = $pg.imageinfo[0] } }
        foreach ($e in $chunk) {
            $t = $e.Link; if ($norm.ContainsKey($t)) { $t = $norm[$t] }; if ($redir.ContainsKey($t)) { $t = $redir[$t] }
            $file = $fileOf[$t]; if (-not $file) { continue }
            $ii = $info[($file -replace '_', ' ')]
            if (-not $ii -or $ii.mime -notin 'image/jpeg', 'image/png' -or [Math]::Max($ii.width, $ii.height) -lt 500) { continue }
            if ($file -match '(?i)map|flag|logo|coat|signature|stamp|seal|locator|\.svg$|diagram|chart') { continue }
            $lic = $licenseMap[[string]$ii.extmetadata.LicenseShortName.value]; if (-not $lic) { continue }
            if ($seen.ContainsKey($file)) { continue }
            $url = if ($ii.width -gt 900 -and $ii.thumburl) { $ii.thumburl } else { $ii.url }
            $ext = if ($ii.mime -eq 'image/png') { '.png' } else { '.jpg' }
            $name = "w-$($e.Day)-$($e.Year)-$(([guid]::NewGuid().ToString('N')).Substring(0, 5))$ext"
            try { Invoke-WebRequest -UserAgent $ua -Uri $url -OutFile (Join-Path $imgDir $name) -UseBasicParsing -TimeoutSec 90; Start-Sleep -Seconds 2 } catch { Write-Host "download failed $name"; continue }
            $seen[$file] = 1
            $creator = Strip-Html $ii.extmetadata.Artist.value; if (-not $creator) { $creator = 'Unknown author' }
            $caption = (Strip-Html $ii.extmetadata.ImageDescription.value); if (-not $caption) { $caption = $e.Link }; if ($caption.Length -gt 160) { $caption = $caption.Substring(0, 160) + '…' }
            $lines.Add("$($e.Day)|$($e.Year)|$($e.Title -replace '\|','/')|photos/events/$name|$($caption -replace '\|','/')|$($creator -replace '\|','/')|$lic|$($ii.descriptionurl)")
            $added++
        }
    } catch { Write-Host "chunk failed: $($_.Exception.Message)" }
    [IO.File]::WriteAllLines($outFile, $lines, $utf8)
    Write-Host "$([Math]::Min($i + 40, $todo.Count)) of $($todo.Count) looked at, $added photographs"
}
[IO.File]::WriteAllLines($outFile, $lines, $utf8)
