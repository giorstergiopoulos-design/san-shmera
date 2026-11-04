<#
  Builds data\history-docs\MM.json from
    * tools\history\out\MM.json        the candidates found by Find-Documented-Events.ps1 (year, article, official references)
    * tools\history\curated\*.tsv      the texts written for the app:  MM-DD|article|region|title|summary
  Every curated event gets the year of its candidate, up to three official references and one pointer to the encyclopedia article.
  Existing hand-written events (data\history\*.json, data\history-cy\*.json) are not touched: when a candidate of the same date and year exists,
  its sources are added as a "source supplement" that the loader attaches to the existing event.
  Usage: powershell -File Build-DocumentedEvents.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$outDir = Join-Path $root 'data\history-docs'
New-Item -ItemType Directory -Force $outDir | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)

function J([string]$s) {
    $s = $s.Replace('\', '\\').Replace('"', '\"').Replace("`t", ' ').Replace("`r", '').Replace("`n", '\n')
    '"' + $s + '"'
}
function Rank-Host([string]$u) {
    $h = ([uri]$u).Host.ToLowerInvariant()
    if ($h -match '(^|\.)(loc\.gov|britannica\.com|bnf\.fr|un\.org|europa\.eu|archives\.gov|nasa\.gov|nobelprize\.org|si\.edu)$') { return 0 }
    if ($h -match '\.gov(\.[a-z]{2})?$|\.int$') { return 1 }
    return 2
}
function Sources-Json($c) {
    $refs = @($c.officialReferences | Sort-Object { Rank-Host $_ } | Select-Object -First 3)
    $items = @()
    foreach ($r in $refs) {
        $items += '{ "kind": "official-reference", "citation": "Αναφορά από τη βιβλιογραφία του λήμματος. Δεν έχει ανοιχτεί και ελεγχθεί.", "url": ' + (J $r) + ' }'
    }
    $items += '{ "kind": "encyclopedia-pointer", "citation": "Λήμμα ανοιχτής εγκυκλοπαίδειας, μόνο για περισσότερες πληροφορίες.", "url": ' + (J $c.articleUrl) + ' }'
    return ($items -join ', ')
}

# candidates: "MM-DD" -> list
$cand = @{}
foreach ($f in Get-ChildItem (Join-Path $PSScriptRoot 'out\*.json')) {
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($p in $j.PSObject.Properties) { $cand[$p.Name] = @($p.Value) }
}

# curated texts
$curated = @{}
foreach ($f in Get-ChildItem (Join-Path $PSScriptRoot 'curated\*.tsv')) {
    foreach ($line in Get-Content $f.FullName -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) { continue }
        $x = $line.Split('|')
        if ($x.Count -ne 5) { Write-Warning "Bad line: $line"; continue }
        if (-not $curated.ContainsKey($x[0])) { $curated[$x[0]] = @() }
        $curated[$x[0]] += [pscustomobject]@{ Article = $x[1]; Region = $x[2]; Title = $x[3]; Summary = $x[4] }
    }
}

# existing hand-written events: (MM-DD, year) -> true
$existing = @{}
foreach ($dir in 'data\history', 'data\history-cy') {
    foreach ($f in Get-ChildItem (Join-Path $root "$dir\*.json")) {
        $m = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($d in $m.days) { foreach ($e in @($d.events)) { if ($e) { $existing[('{0:00}-{1:00}' -f $m.month, $d.day) + '|' + $e.year] = $e.title } } }
    }
}

$stats = @{ events = 0; supplements = 0; missing = 0 }
for ($month = 1; $month -le 12; $month++) {
    $days = @()
    for ($day = 1; $day -le [DateTime]::DaysInMonth(2024, $month); $day++) {
        $key = '{0:00}-{1:00}' -f $month, $day
        $parts = @()
        # new documented events from the curated texts
        $evJson = @()
        foreach ($c in @($curated[$key])) {
            if (-not $c) { continue }
            $match = @($cand[$key] | Where-Object { $_.article -eq $c.Article }) | Select-Object -First 1
            if (-not $match) { Write-Warning "$key $($c.Article): not among the candidates, skipped"; $stats.missing++; continue }
            $evJson += '{ "year": ' + $match.year + ', "title": ' + (J $c.Title) + ', "summary": ' + (J $c.Summary) + ', "region": ' + (J $c.Region) +
                       ', "status": "Unverified", "sources": [ ' + (Sources-Json $match) + ' ] }'
            $stats.events++
        }
        if ($evJson.Count -gt 0) { $parts += '"events": [ ' + ($evJson -join ', ') + ' ]' }
        # supplements for existing events: same date and year as a candidate that is not one of the curated ones
        $supJson = @()
        foreach ($c in @($cand[$key])) {
            if (-not $c) { continue }
            if (-not $existing.ContainsKey("$key|$($c.year)")) { continue }
            if (@($curated[$key] | Where-Object { $_ -and $_.Article -eq $c.article }).Count -gt 0) { continue }
            $supJson += '{ "year": ' + $c.year + ', "sources": [ ' + (Sources-Json $c) + ' ] }'
            $stats.supplements++
        }
        if ($supJson.Count -gt 0) { $parts += '"sourceSupplements": [ ' + ($supJson -join ', ') + ' ]' }
        if ($parts.Count -gt 0) { $days += '    { "day": ' + $day + ', ' + ($parts -join ', ') + ' }' }
    }
    if ($days.Count -eq 0) { continue }
    $text = '{' + "`n" + '  "tradition": "history",' + "`n" + '  "calendar": "gregorian",' + "`n" + '  "month": ' + $month + ',' + "`n" +
            '  "days": [' + "`n" + ($days -join ",`n") + "`n" + '  ]' + "`n" + '}' + "`n"
    [IO.File]::WriteAllText((Join-Path $outDir ('{0:00}.json' -f $month)), $text, $utf8)
}
Write-Host ("Wrote {0} documented events and {1} source supplements; {2} curated lines had no matching candidate." -f $stats.events, $stats.supplements, $stats.missing)
