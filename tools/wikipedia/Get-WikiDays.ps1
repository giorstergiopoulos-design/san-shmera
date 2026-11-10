<#
  Reads the day pages of the Greek Wikipedia ("6 Δεκεμβρίου": sections Γεγονότα, Γεννήσεις, Θάνατοι) through the MediaWiki API and measures how well known each
  person or event is (the number of language editions of the article of the first link, from Wikidata). The result is a reading aid kept OUTSIDE the
  repository (-Out): the texts of Wikipedia are CC BY-SA and are not copied into the app; only the facts (who, when) are used, written in our own words, and the
  article is named as the source.
  Output: MM-DD|section|year|score|firstLinkTitle|plain text
  Usage: powershell -File Get-WikiDays.ps1 -Out <file>
  Generic client name, one request every 2 seconds, and a wait of one to five minutes after a 429 answer (API etiquette: sequential requests).
#>
param([Parameter(Mandatory)] [string]$Out, [int]$From = 1, [int]$To = 366)
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-DataTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$months = 'Ιανουαρίου','Φεβρουαρίου','Μαρτίου','Απριλίου','Μαΐου','Ιουνίου','Ιουλίου','Αυγούστου','Σεπτεμβρίου','Οκτωβρίου','Νοεμβρίου','Δεκεμβρίου'
function Api([string]$url) {
    for ($try = 1; $try -le 6; $try++) {
        try { $r = Invoke-RestMethod -UserAgent $ua -Uri $url -TimeoutSec 60; Start-Sleep -Seconds 2; return $r }
        catch { if ($_.Exception.Message -match '429' -and $try -lt 6) { Write-Host "429, waiting $($60 * $try) s"; Start-Sleep -Seconds (60 * $try) } else { throw } }
    }
}
function Plain([string]$s) {
    $s = [regex]::Replace($s, '\[\[(?:[^\]|]*\|)?([^\]]*)\]\]', '$1')
    $s = [regex]::Replace($s, "'{2,}", '')
    $s = [regex]::Replace($s, '<ref[^>]*>.*?</ref>|<ref[^>]*/>|<[^>]+>|\{\{[^}]*\}\}', '')
    return ($s -replace '\s+', ' ').Trim()
}
function Score([string[]]$titles) {
    $res = @{}
    for ($i = 0; $i -lt $titles.Count; $i += 40) {
        $chunk = $titles[$i..([Math]::Min($i + 39, $titles.Count - 1))]
        $q = [uri]::EscapeDataString(($chunk -join '|'))
        try {
            $r = Api "https://el.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&redirects=1&prop=pageprops&ppprop=wikibase_item&titles=$q"
            $map = @{}   # normalised/redirected title -> from title
            foreach ($n in @($r.query.normalized)) { if ($n) { $map[$n.to] = $n.from } }
            $from = @{}; foreach ($t in $chunk) { $from[$t] = $t }
            $qid = @{}
            foreach ($p in @($r.query.pages)) { if ($p.pageprops.wikibase_item) { $qid[$p.title] = $p.pageprops.wikibase_item } }
            $redir = @{}; foreach ($x in @($r.query.redirects)) { if ($x) { $redir[$x.from] = $x.to } }
            $ids = @($qid.Values | Select-Object -Unique)
            $counts = @{}
            for ($j = 0; $j -lt $ids.Count; $j += 40) {
                $idc = $ids[$j..([Math]::Min($j + 39, $ids.Count - 1))] -join '|'
                $w = Api "https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&props=sitelinks&ids=$([uri]::EscapeDataString($idc))"
                foreach ($e in $w.entities.PSObject.Properties) { $counts[$e.Name] = @($e.Value.sitelinks.PSObject.Properties).Count }
            }
            foreach ($t in $chunk) {
                $norm = $t; if ($map.ContainsValue($t)) { $norm = ($map.GetEnumerator() | Where-Object { $_.Value -eq $t } | Select-Object -First 1).Key }
                if ($redir.ContainsKey($norm)) { $norm = $redir[$norm] }
                $res[$t] = if ($qid.ContainsKey($norm)) { [int]$counts[$qid[$norm]] } else { 0 }
            }
        } catch { Write-Host "score chunk failed: $($_.Exception.Message)"; foreach ($t in $chunk) { if (-not $res.ContainsKey($t)) { $res[$t] = 0 } } }
    }
    return $res
}

$lines = New-Object Collections.Generic.List[string]
if (Test-Path $Out) { foreach ($l in Get-Content $Out -Encoding UTF8) { $lines.Add($l) } }
$have = @{}; foreach ($l in $lines) { $have[$l.Substring(0, 5)] = $true }
$start = [datetime]'2024-01-01'
for ($i = $From - 1; $i -lt $To; $i++) {
    $d = $start.AddDays($i); $key = $d.ToString('MM-dd')
    if ($have.ContainsKey($key)) { continue }
    $page = "$($d.Day) $($months[$d.Month - 1])"
    try {
        $r = Api "https://el.wikipedia.org/w/api.php?action=parse&format=json&formatversion=2&prop=wikitext&redirects=1&page=$([uri]::EscapeDataString($page))"
        $text = $r.parse.wikitext
        $rows = New-Object Collections.Generic.List[object]
        $section = ''
        foreach ($line in ($text -split "`n")) {
            if ($line -match '^==\s*([^=]+?)\s*==\s*$') { $section = $Matches[1]; continue }
            if ($section -notin 'Γεγονότα', 'Γεννήσεις', 'Θάνατοι') { continue }
            if ($line -notmatch '^\*\s*(?:\[\[)?(-?\d{1,4})(?:\]\])?(?:\s*(?:π\.Χ\.|π\.Χ))?\s*[-–—]\s*(.+)$') { continue }
            $year = [int]$Matches[1]; $rest = $Matches[2]
            if ($line -match 'π\.Χ') { $year = -[Math]::Abs($year) }
            $first = if ($rest -match '\[\[([^\]|#]+)') { $Matches[1].Trim() } else { '' }
            $rows.Add([pscustomobject]@{ Section = $section; Year = $year; First = $first; Text = (Plain $rest) })
        }
        $titles = @($rows | Where-Object { $_.First } | ForEach-Object { $_.First } | Select-Object -Unique)
        $scores = if ($titles.Count -gt 0) { Score $titles } else { @{} }
        foreach ($row in $rows) {
            $sc = if ($row.First -and $scores.ContainsKey($row.First)) { $scores[$row.First] } else { 0 }
            $lines.Add(("$key|$($row.Section)|$($row.Year)|$sc|$($row.First)|$($row.Text)").Replace("`r", ' '))
        }
        if ($rows.Count -eq 0) { $lines.Add("$key||||") }
        Write-Host "$key $($rows.Count)"
    } catch { Write-Host "$key FAILED $($_.Exception.Message)" }
    [IO.File]::WriteAllLines($Out, $lines, $utf8)
}
