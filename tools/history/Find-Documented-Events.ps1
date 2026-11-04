<#
  Finds historical events of each day that are well documented, and nothing else. Nothing of the Wikipedia text is stored.

  For a day, the official Wikipedia "on this day" feed of five language editions (en, de, fr, es, ru) is read. An event is a candidate when the SAME
  article (the same Wikidata item) is listed for the same year by at least -MinLanguages editions. For the best candidates the references of the
  English article are read (MediaWiki API, prop=externallinks) and only links to OFFICIAL bodies are kept (official-domains.json). An event with no
  official reference is dropped. The result, per day, is a list of: year, article title and link (a pointer, for "learn more"), the number of
  language editions, and the official references. Texts for the app are then written from scratch.

  Politeness: honest user agent with a contact address, one request every 600 ms, one retry after HTTP 429 (waits as the server asks).
  Usage: powershell -File Find-Documented-Events.ps1 -Month 11 [-Day 9] [-MinLanguages 3] [-Top 4]
#>
param(
    [Parameter(Mandatory)] [int]$Month,
    [int]$Day = 0,
    [int]$MinLanguages = 3,
    [int]$Top = 4
)
$ErrorActionPreference = 'Stop'
$ua = 'Chronikologio-History/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$langs = 'en', 'de', 'fr', 'es', 'ru'
$policy = Get-Content (Join-Path $PSScriptRoot 'official-domains.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$outDir = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force $outDir | Out-Null

function Get-Json([string]$url) {
    foreach ($attempt in 1, 2) {
        try {
            $r = Invoke-RestMethod -Uri $url -UserAgent $ua -TimeoutSec 40
            Start-Sleep -Milliseconds 600
            return $r
        } catch {
            $status = 0
            if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
            if ($status -eq 404) { Start-Sleep -Milliseconds 600; return $null }
            if ($status -eq 429 -and $attempt -eq 1) {
                $wait = 0
                try { $wait = [int]($_.Exception.Response.Headers['Retry-After']) } catch { }
                if ($wait -lt 1) { $wait = 60 }
                Write-Host "Rate limited; waiting $wait s"
                Start-Sleep -Seconds $wait
                continue
            }
            Write-Warning "Request failed ($status): $url"
            Start-Sleep -Milliseconds 600
            return $null
        }
    }
}

function Test-OfficialHost([string]$link) {
    $u = $null
    if ($link.StartsWith('//')) { $link = 'https:' + $link }
    if (-not [uri]::TryCreate($link, [UriKind]::Absolute, [ref]$u)) { return $false }
    if ($u.Scheme -notin 'http', 'https') { return $false }
    $h = $u.Host.ToLowerInvariant()
    foreach ($bad in $policy.neverOfficial) { if ($h -eq $bad -or $h.EndsWith('.' + $bad)) { return $false } }
    foreach ($p in $policy.hostPatterns) { if ($h -match $p) { return $true } }
    return $false
}

$days = if ($Day -gt 0) { @($Day) } else { 1..[DateTime]::DaysInMonth(2024, $Month) }
$all = [ordered]@{}
foreach ($d in $days) {
    $mm = '{0:00}' -f $Month; $dd = '{0:00}' -f $d
    # (year|item) -> languages that list it
    $seen = @{}
    $enEvents = $null
    foreach ($lang in $langs) {
        $feed = Get-Json "https://$lang.wikipedia.org/api/rest_v1/feed/onthisday/events/$mm/$dd"
        if (-not $feed) { continue }
        if ($lang -eq 'en') { $enEvents = $feed.events }
        foreach ($e in $feed.events) {
            foreach ($p in $e.pages) {
                if (-not $p.wikibase_item) { continue }
                $key = "$($e.year)|$($p.wikibase_item)"
                if (-not $seen.ContainsKey($key)) { $seen[$key] = New-Object 'System.Collections.Generic.HashSet[string]' }
                [void]$seen[$key].Add($lang)
            }
        }
    }
    $candidates = @()
    foreach ($e in $enEvents) {
        $best = $null; $bestN = 0
        foreach ($p in $e.pages) {
            $key = "$($e.year)|$($p.wikibase_item)"
            if ($seen.ContainsKey($key) -and $seen[$key].Count -gt $bestN) { $best = $p; $bestN = $seen[$key].Count }
        }
        if ($best -and $bestN -ge $MinLanguages) {
            $candidates += [pscustomobject]@{ Year = $e.year; Page = $best; Languages = $bestN }
        }
    }
    $picked = @($candidates | Sort-Object -Property @{ Expression = 'Languages'; Descending = $true }, @{ Expression = 'Year'; Descending = $false } | Select-Object -First $Top)
    $result = @()
    foreach ($c in $picked) {
        $title = $c.Page.titles.canonical
        $parse = Get-Json ("https://en.wikipedia.org/w/api.php?action=parse&prop=externallinks&redirects=1&format=json&page=" + [uri]::EscapeDataString($title))
        $links = @()
        if ($parse -and $parse.parse) { $links = @($parse.parse.externallinks | Where-Object { Test-OfficialHost $_ } | Select-Object -Unique -First 5) }
        if ($links.Count -eq 0) { continue }   # no official documentation: dropped
        $result += [ordered]@{
            year = $c.Year
            article = $title
            articleUrl = $c.Page.content_urls.desktop.page
            wikidata = $c.Page.wikibase_item
            languages = $c.Languages
            officialReferences = $links
        }
    }
    $all["$mm-$dd"] = $result
    Write-Host ("{0}-{1}: {2} candidate(s) with official references" -f $mm, $dd, $result.Count)
}
$file = Join-Path $outDir ($(if ($Day -gt 0) { "$mm-$dd.json" } else { "$mm.json" }))
$all | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 $file
Write-Host "Wrote $file"
