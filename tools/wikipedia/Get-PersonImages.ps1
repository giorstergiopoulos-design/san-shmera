<#
  Finds a portrait for the people of data\births\births-wiki.tsv and data\deaths\deaths-wiki.tsv: the picture of the article of the person in the Greek Wikipedia
  (the page image), when that picture is on Wikimedia Commons with a free licence (public domain, CC0 or CC BY; never share-alike). The licence, the author and the page of the
  picture are read from Commons and written next to it. Only pictures that pass are downloaded (at most 900 px wide). The best known people come first. It can be stopped and run again.
  Output: data\images\photos\people\*.jpg and data\images\personimages-wiki.tsv  (MM-DD|b or d|year|name|file|caption|creator|license|sourceUrl)
  Honest client name with the repository as the contact, one request every 2 seconds, a wait after a 429 answer.
  Usage: powershell -File Get-PersonImages.ps1 -Wiki <wiki.tsv> [-Max 3000]
#>
param([Parameter(Mandatory)] [string]$Wiki, [int]$Max = 3000)
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-DataTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$imgDir = Join-Path $root 'data\images\photos\people'
New-Item -ItemType Directory -Force $imgDir | Out-Null
$outFile = Join-Path $root 'data\images\personimages-wiki.tsv'
$utf8 = New-Object System.Text.UTF8Encoding($true)
function Api([string]$url) {
    for ($try = 1; $try -le 5; $try++) {
        try { $r = Invoke-RestMethod -UserAgent $ua -Uri $url -TimeoutSec 60; Start-Sleep -Seconds 2; return $r }
        catch { if ($_.Exception.Message -match '429' -and $try -lt 5) { Start-Sleep -Seconds (60 * $try) } else { throw } }
    }
}
function Strip-Html([string]$s) { if (-not $s) { return '' }; ([regex]::Replace($s, '<[^>]+>', '') -replace '&amp;', '&' -replace '&quot;', '"' -replace '&#039;', "'" -replace '\s+', ' ').Trim() }
function Fold([string]$x) { (($x.Normalize([Text.NormalizationForm]::FormD) -replace '\p{Mn}', '').ToLower() -replace 'ς', 'σ' -replace '[^\p{L}\p{Nd}]+', ' ').Trim() }
$licenseMap = @{ 'Public domain' = 'Public Domain'; 'CC0' = 'CC0 1.0'; 'CC BY 4.0' = 'CC BY 4.0'; 'CC BY 3.0' = 'CC BY 3.0'; 'CC BY 2.5' = 'CC BY 2.5'; 'CC BY 2.0' = 'CC BY 2.0'; 'CC BY-SA' = $null }

# the people (best known first) with the title of their article
$score = @{}; $title = @{}
foreach ($l in Get-Content $Wiki -Encoding UTF8) {
    $p = $l -split '\|', 6
    if ($p.Count -lt 6 -or $p[1] -notin 'Γεννήσεις', 'Θάνατοι' -or -not $p[4]) { continue }
    $kind = if ($p[1] -eq 'Θάνατοι') { 'd' } else { 'b' }
    $name = ($p[5] -split ',', 2)[0].Trim()
    $key = "$kind|$($p[0])|$($p[2])|$(Fold $name)"
    $score[$key] = [int]$p[3]; $title[$key] = $p[4]
}
$people = @()
foreach ($f in @(@('births-wiki.tsv', 'births', 'b'), @('deaths-wiki.tsv', 'deaths', 'd'))) {
    $path = Join-Path $root "data\$($f[1])\$($f[0])"
    if (-not (Test-Path $path)) { continue }
    foreach ($l in Get-Content $path -Encoding UTF8) {
        if ($l -notmatch '^(\d\d-\d\d)\|(-?\d+)\|[^|]*\|([^|]+)\|') { continue }
        $key = "$($f[2])|$($Matches[1])|$($Matches[2])|$(Fold $Matches[3])"
        if ($title.ContainsKey($key)) { $people += [pscustomobject]@{ Kind = $f[2]; Day = $Matches[1]; Year = $Matches[2]; Name = $Matches[3]; Title = $title[$key]; Score = $score[$key] } }
    }
}
$people = @($people | Sort-Object { -$_.Score } | Select-Object -First $Max)
Write-Host "people with an article: $($people.Count)"

$done = @{}; $lines = New-Object Collections.Generic.List[string]
if (Test-Path $outFile) { foreach ($l in Get-Content $outFile -Encoding UTF8) { $lines.Add($l); if ($l -match '^(\d\d-\d\d)\|([bd])\|(-?\d+)\|([^|]+)\|') { $done["$($Matches[2])|$($Matches[1])|$($Matches[3])|$(Fold $Matches[4])"] = 1 } } }
if ($lines.Count -eq 0) { $lines.Add('# MM-DD|b (birth) or d (death)|year|name|file|caption|creator|license|sourceUrl   (portraits from Wikimedia Commons, free licences only; made by tools\wikipedia\Get-PersonImages.ps1)') }
$seen = @{}   # the same file for two people (a double portrait) is used once
$todo = @($people | Where-Object { -not $done.ContainsKey("$($_.Kind)|$($_.Day)|$($_.Year)|$(Fold $_.Name)") })
Write-Host "to do: $($todo.Count)"
$added = 0
for ($i = 0; $i -lt $todo.Count; $i += 40) {
    $chunk = @($todo[$i..([Math]::Min($i + 39, $todo.Count - 1))])
    try {
        $q = [uri]::EscapeDataString((($chunk | ForEach-Object { $_.Title } | Select-Object -Unique) -join '|'))
        $r = Api "https://el.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&redirects=1&prop=pageimages&piprop=name&titles=$q"
        $fileOf = @{}
        foreach ($pg in @($r.query.pages)) { if ($pg.pageimage) { $fileOf[$pg.title] = $pg.pageimage } }
        $redir = @{}; foreach ($x in @($r.query.redirects)) { if ($x) { $redir[$x.from] = $x.to } }
        $norm = @{}; foreach ($x in @($r.query.normalized)) { if ($x) { $norm[$x.from] = $x.to } }
        $files = @($chunk | ForEach-Object { $t = $_.Title; if ($norm.ContainsKey($t)) { $t = $norm[$t] }; if ($redir.ContainsKey($t)) { $t = $redir[$t] }; $fileOf[$t] } | Where-Object { $_ } | Select-Object -Unique)
        if ($files.Count -eq 0) { continue }
        $fq = [uri]::EscapeDataString((($files | ForEach-Object { "File:$_" }) -join '|'))
        $c = Api "https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2&prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=900&iiextmetadatafilter=LicenseShortName|Artist|ImageDescription&titles=$fq"
        $info = @{}
        foreach ($pg in @($c.query.pages)) { if ($pg.imageinfo) { $info[($pg.title -replace '^File:', '')] = $pg.imageinfo[0] } }
        foreach ($person in $chunk) {
            $t = $person.Title; if ($norm.ContainsKey($t)) { $t = $norm[$t] }; if ($redir.ContainsKey($t)) { $t = $redir[$t] }
            $file = $fileOf[$t]; if (-not $file -or -not $info.ContainsKey(($file -replace '_', ' '))) { if ($file -and $info.ContainsKey($file)) { } else { continue } }
            $ii = if ($info.ContainsKey($file)) { $info[$file] } else { $info[($file -replace '_', ' ')] }
            if (-not $ii -or $ii.mime -notin 'image/jpeg', 'image/png' -or [Math]::Max($ii.width, $ii.height) -lt 300) { continue }
            $lic = $licenseMap[[string]$ii.extmetadata.LicenseShortName.value]
            if (-not $lic) { continue }
            if ($file -match '(?i)\b(map|flag|logo|coat of arms|signature|stamp)\b|\.svg$') { continue }
            if ($seen.ContainsKey($file)) { continue }
            $url = if ($ii.width -gt 900 -and $ii.thumburl) { $ii.thumburl } else { $ii.url }
            $slug = ($person.Day + '-' + $person.Kind + '-' + $person.Year)
            $ext = if ($ii.mime -eq 'image/png') { '.png' } else { '.jpg' }
            $name = "$slug-$(([guid]::NewGuid().ToString('N')).Substring(0, 6))$ext"
            $path = Join-Path $imgDir $name
            try { Invoke-WebRequest -UserAgent $ua -Uri $url -OutFile $path -UseBasicParsing -TimeoutSec 90; Start-Sleep -Seconds 2 } catch { Write-Host "download failed $name"; if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 60 }; continue }
            $seen[$file] = 1
            $creator = Strip-Html $ii.extmetadata.Artist.value; if (-not $creator) { $creator = 'Unknown author' }
            $caption = $person.Name
            $lines.Add("$($person.Day)|$($person.Kind)|$($person.Year)|$($person.Name)|photos/people/$name|$($caption -replace '\|','/')|$($creator -replace '\|','/')|$lic|$($ii.descriptionurl)")
            $added++
        }
    } catch { Write-Host "chunk failed: $($_.Exception.Message)" }
    [IO.File]::WriteAllLines($outFile, $lines, $utf8)
    Write-Host "$([Math]::Min($i + 40, $todo.Count)) of $($todo.Count) looked at, $added portraits"
}
[IO.File]::WriteAllLines($outFile, $lines, $utf8)
