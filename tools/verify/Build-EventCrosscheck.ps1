<#
  Cross-checks the dates of the historical events of the app against the calendar pages of the encyclopedia:
    * the Greek day pages saved by tools\history\Fetch-GreekDayPages.ps1   (tools\history\el\MM-DD.txt)
    * the "on this day" lists of five language editions                   (tools\history\out\MM.json, year per article)
  An event is "Reviewed" when its year is listed for the same day in the Greek page, or in at least one of the candidate lists.
  Only the date is confirmed this way; the text of the event is the app's own. Writes data\verification\verification-events.tsv and tools\verify\report-events.txt.
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$utf8 = New-Object Text.UTF8Encoding($false)

$elYears = @{}
foreach ($f in Get-ChildItem (Join-Path $root 'tools\history\el\*.txt')) {
    $set = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($l in Get-Content $f.FullName -Encoding UTF8) { if ($l -match '^\s*(\d{1,4})(\s*π\.Χ\.)?\s*[-–:.]') { [void]$set.Add(($Matches[1] + $Matches[2]).Trim()) } }
    $elYears[$f.BaseName] = $set
}
$candYears = @{}
foreach ($f in Get-ChildItem (Join-Path $root 'tools\history\out\*.json')) {
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($p in $j.PSObject.Properties) {
        $set = New-Object 'System.Collections.Generic.HashSet[string]'
        foreach ($c in @($p.Value)) { if ($c.year) { [void]$set.Add([string]$c.year) } }
        $candYears[$p.Name] = $set
    }
}

$events = @()   # (key, year, title)
foreach ($f in Get-ChildItem (Join-Path $root 'data\events\events*.tsv')) {
    foreach ($l in Get-Content $f.FullName -Encoding UTF8) {
        if ($l.StartsWith('#') -or [string]::IsNullOrWhiteSpace($l)) { continue }
        $p = $l.Split('|'); if ($p.Count -lt 5 -or -not $p[1]) { continue }
        $events += [pscustomobject]@{ Key = $p[0]; Year = $p[1]; Title = $p[3] }
    }
}
foreach ($dir in 'history', 'history-cy', 'history-docs') {
    foreach ($f in Get-ChildItem (Join-Path $root "data\$dir\*.json")) {
        $m = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($d in $m.days) { foreach ($e in @($d.events)) { if ($e -and $e.year) { $events += [pscustomobject]@{ Key = ('{0:00}-{1:00}' -f $m.month, $d.day); Year = [string]$e.year; Title = $e.title } } } }
    }
}

$manual = @{}
$mf = Join-Path $PSScriptRoot 'manual-events.txt'
if (Test-Path $mf) { foreach ($l in Get-Content $mf -Encoding UTF8) { if (-not $l.StartsWith('#') -and $l.Contains('|')) { $manual[$l.Trim()] = $true } } }
$manual = @{}
$mf = Join-Path $PSScriptRoot 'manual-events.txt'
if (Test-Path $mf) { foreach ($l in Get-Content $mf -Encoding UTF8) { if (-not $l.StartsWith('#') -and $l.Contains('|')) { $manual[$l.Trim()] = $true } } }
$lines = New-Object Collections.Generic.List[string]
$lines.Add('# kind|MM-DD|year|title|status|url|citation   (made by tools\verify\Build-EventCrosscheck.ps1; do not edit by hand)')
$report = New-Object Collections.Generic.List[string]
$ok = 0; $no = 0
foreach ($e in $events) {
    $year = $e.Year.TrimStart('-')
    $yearKey = if ($e.Year.StartsWith('-')) { "$year π.Χ." } else { $year }
    $inEl = $elYears.ContainsKey($e.Key) -and $elYears[$e.Key].Contains($yearKey)
    $inCand = $candYears.ContainsKey($e.Key) -and $candYears[$e.Key].Contains($e.Year)
    if ($inEl -or $inCand) {
        $ok++
        $cit = if ($inEl) { 'Διασταύρωση ημερομηνίας: το έτος αναφέρεται για την ίδια ημέρα στο ημερολογιακό λήμμα της ελληνικής Wikipedia. Επιβεβαιώνεται μόνο η ημερομηνία· το κείμενο είναι δικό μας.' }
               else { 'Διασταύρωση ημερομηνίας: το έτος αναφέρεται για την ίδια ημέρα στις ημερολογιακές λίστες ξένων εκδόσεων της Wikipedia. Επιβεβαιώνεται μόνο η ημερομηνία· το κείμενο είναι δικό μας.' }
        $lines.Add("event|$($e.Key)|$($e.Year)|$($e.Title)|Reviewed||$cit")
    } elseif ($manual.ContainsKey("$($e.Key)|$($e.Year)")) {
        $ok++
        $lines.Add("event|$($e.Key)|$($e.Year)|$($e.Title)|Reviewed||Έλεγχος της ημερομηνίας από τον συντάκτη με βάση ευρέως γνωστά ιστορικά στοιχεία. Επιβεβαιώνεται μόνο η ημερομηνία· το κείμενο είναι δικό μας.")
    } elseif ($manual.ContainsKey("$($e.Key)|$($e.Year)")) {
        $ok++
        $lines.Add("event|$($e.Key)|$($e.Year)|$($e.Title)|Reviewed||Έλεγχος της ημερομηνίας από τον συντάκτη με βάση ευρέως γνωστά ιστορικά στοιχεία. Επιβεβαιώνεται μόνο η ημερομηνία· το κείμενο είναι δικό μας.")
    } else { $no++; $report.Add("NOT FOUND $($e.Key) $($e.Year) $($e.Title)") }
}
New-Item -ItemType Directory -Force (Join-Path $root 'data\verification') | Out-Null
[IO.File]::WriteAllLines((Join-Path $root 'data\verification\verification-events.tsv'), $lines, $utf8)
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'report-events.txt'), $report, $utf8)
Write-Host "events: cross-checked $ok, not found $no"
