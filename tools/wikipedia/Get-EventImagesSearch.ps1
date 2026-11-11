<#
  Finds a photograph for the events that have none yet, by SEARCHING the Greek Wikipedia for the event (the day pages link only some events): the article found must
  (1) mention the year of the event in its introduction, (2) share a distinctive word with the title of our event, and (3) have a page image that is on Wikimedia Commons with a free licence
  (public domain, CC0, CC BY; never share-alike) and is not a map, a flag, a coat of arms, a logo or an SVG. A wrong picture is worse than none, so when in doubt nothing is taken.
  The licence, the author and the page of the picture are read from Commons and written next to it.
  Output: data\images\photos\events\s-*.jpg and data\images\eventimages-search.tsv (MM-DD|year|title|file|caption|creator|license|sourceUrl)
  Honest client name with the repository as the contact, one request every 2 seconds. It can be stopped and run again.
  Usage: powershell -File Get-EventImagesSearch.ps1 [-Max 2000]
#>
param([int]$Max = 2000)
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-DataTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$imgDir = Join-Path $root 'data\images\photos\events'
$outFile = Join-Path $root 'data\images\eventimages-search.tsv'
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
$stop = 'αρχιζει|τελειωνει|γινεται|υπογραφεται|εισβαλλει|ανακηρυσσει|παραδινεται|καταλαμβανει|ξεκινα|ολοκληρωνεται|εκλεγεται|διακηρυσσει|ιδρυεται|εγκαινιαζεται|πεθαινει|γεννιεται|οικουμενικη|ελληνικο|ελληνικη|ελληνικου'

# events that have no photograph yet
$have = @{}
foreach ($f in Get-ChildItem (Join-Path $root 'data\images\eventimages*.tsv')) { foreach ($l in Get-Content $f.FullName -Encoding UTF8) { if ($l -match '^(\d\d-\d\d)\|(-?\d+)\|') { $have["$($Matches[1])|$($Matches[2])"] = 1 } } }
$events = @()
foreach ($f in Get-ChildItem (Join-Path $root 'data\events\events*.tsv')) {
    foreach ($l in Get-Content $f.FullName -Encoding UTF8) {
        if ($l -notmatch '^(\d\d-\d\d)\|(\d{4})\|([^|]*)\|([^|]+)\|(.*)$') { continue }
        $k = "$($Matches[1])|$($Matches[2])"
        if ($have.ContainsKey($k)) { continue }
        $events += [pscustomobject]@{ Day = $Matches[1]; Year = $Matches[2]; Region = $Matches[3]; Title = $Matches[4]; Key = $k }
    }
}
$events = @($events | Sort-Object Key -Unique | Select-Object -First $Max)
Write-Host "events without a photograph: $($events.Count)"

$lines = New-Object Collections.Generic.List[string]; $done = @{}
if (Test-Path $outFile) { foreach ($l in Get-Content $outFile -Encoding UTF8) { $lines.Add($l); if ($l -match '^(\d\d-\d\d)\|(-?\d+)\|') { $done["$($Matches[1])|$($Matches[2])"] = 1 } } }
if ($lines.Count -eq 0) { $lines.Add('# MM-DD|year|title|file|caption|creator|license|sourceUrl   (photographs from Wikimedia Commons, free licences only; made by tools\wikipedia\Get-EventImagesSearch.ps1)') }
# pictures of a stopped run that were not written to the list are removed (they would be unused files)
$listed = @{}; foreach ($l in $lines) { $q = $l -split '\|'; if ($q.Count -ge 5) { $listed[($q[3] -replace '.*/', '')] = 1 } }
foreach ($orphan in Get-ChildItem $imgDir -File -Filter 's-*') { if (-not $listed.ContainsKey($orphan.Name)) { Remove-Item $orphan.FullName -Force } }
$tried = Join-Path $env:LOCALAPPDATA 'Chronikologio-tools\event-search-tried.txt'; New-Item -ItemType Directory -Force (Split-Path $tried) | Out-Null   # events looked at without a result: not asked again (kept outside the repository)
$triedSet = @{}; if (Test-Path $tried) { foreach ($l in Get-Content $tried) { $triedSet[$l] = 1 } }
$todo = @($events | Where-Object { -not $done.ContainsKey($_.Key) -and -not $triedSet.ContainsKey($_.Key) })
Write-Host "to do: $($todo.Count)"
$seen = @{}; $added = 0; $n = 0
foreach ($e in $todo) {
    $n++
    try {
        $words = @((Fold $e.Title) -split ' ' | Where-Object { $_.Length -ge 5 -and ($stop -notmatch "(^|\|)$_(\||$)") } | ForEach-Object { $_.Substring(0, [Math]::Min(6, $_.Length)) })
        if ($words.Count -eq 0) { Add-Content $tried $e.Key; continue }
        $q = [uri]::EscapeDataString("$($e.Title) $($e.Year)")
        $r = Api "https://el.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&generator=search&gsrsearch=$q&gsrlimit=4&gsrnamespace=0&prop=pageimages|extracts&piprop=name&exintro=1&explaintext=1&exlimit=4&exsentences=6"
        $best = $null
        foreach ($pg in @($r.query.pages | Sort-Object index)) {
            if (-not $pg.pageimage) { continue }
            if (-not $pg.extract -or -not $pg.extract.Contains($e.Year)) { continue }   # the introduction must name the year
            $hay = Fold ($pg.title + ' ' + $pg.extract.Substring(0, [Math]::Min(300, $pg.extract.Length)))
            $hit = @($words | Where-Object { $hay.Contains($_) }).Count
            if ($hit -ge 1) { $best = $pg; break }
        }
        if (-not $best) { Add-Content $tried $e.Key; continue }
        $file = $best.pageimage
        if ($file -match '(?i)map|flag|logo|coat|signature|stamp|seal|locator|\.svg$|diagram|chart') { Add-Content $tried $e.Key; continue }
        $c = Api ("https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2&prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=900&iiextmetadatafilter=LicenseShortName|Artist|ImageDescription&titles=" + [uri]::EscapeDataString("File:$file"))
        $ii = @($c.query.pages)[0].imageinfo; if ($ii) { $ii = $ii[0] }
        if (-not $ii -or $ii.mime -notin 'image/jpeg', 'image/png' -or [Math]::Max($ii.width, $ii.height) -lt 500) { Add-Content $tried $e.Key; continue }
        $lic = $licenseMap[[string]$ii.extmetadata.LicenseShortName.value]; if (-not $lic) { Add-Content $tried $e.Key; continue }
        if ($seen.ContainsKey($file)) { Add-Content $tried $e.Key; continue }
        $url = if ($ii.width -gt 900 -and $ii.thumburl) { $ii.thumburl } else { $ii.url }
        $ext = if ($ii.mime -eq 'image/png') { '.png' } else { '.jpg' }
        $name = "s-$($e.Day)-$($e.Year)-$(([guid]::NewGuid().ToString('N')).Substring(0, 5))$ext"
        try { Invoke-WebRequest -UserAgent $ua -Uri $url -OutFile (Join-Path $imgDir $name) -UseBasicParsing -TimeoutSec 90; Start-Sleep -Seconds 2 } catch { Write-Host "download failed $name"; continue }
        $seen[$file] = 1
        $creator = Strip-Html $ii.extmetadata.Artist.value; if (-not $creator) { $creator = 'Unknown author' }
        $caption = Strip-Html $ii.extmetadata.ImageDescription.value; if (-not $caption) { $caption = $best.title }; if ($caption.Length -gt 160) { $caption = $caption.Substring(0, 160) + '…' }
        $lines.Add("$($e.Day)|$($e.Year)|$($e.Title -replace '\|','/')|photos/events/$name|$($caption -replace '\|','/')|$($creator -replace '\|','/')|$lic|$($ii.descriptionurl)")
        $added++
    } catch { Write-Host "failed $($e.Key): $($_.Exception.Message)" }
    if ($n % 20 -eq 0) { [IO.File]::WriteAllLines($outFile, $lines, $utf8); Write-Host "$n of $($todo.Count) looked at, $added photographs" }
}
[IO.File]::WriteAllLines($outFile, $lines, $utf8)
Write-Host "done: $n looked at, $added photographs"
