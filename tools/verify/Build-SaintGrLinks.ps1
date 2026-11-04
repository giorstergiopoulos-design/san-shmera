<#
  Matches the saints of the app with the lives listed by saint.gr for the same date (tools\verify\saintgr\MM-DD.json, made by Fetch-SaintGr.ps1)
  and writes data\verification\verification-saintgr.tsv: for each matched commemoration a source link to its life at saint.gr
  (https://www.saint.gr/<id>/saint.aspx), shown after the text as "Συναξαριστής saint.gr". The text of the life is NOT copied.
  The names are compared in Greek, ignoring accents and the case of letters, so a match is a name of the app (or a variant) found among the words of a title on that day.
  Saints that appear on many days (the featured ones of the front page) are ignored.
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$utf8 = New-Object Text.UTF8Encoding($false)

function Fold([string]$s) {
    $d = $s.Normalize([Text.NormalizationForm]::FormD)
    $sb = New-Object Text.StringBuilder
    foreach ($c in $d.ToCharArray()) { if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($c) -ne 'NonSpacingMark') { [void]$sb.Append($c) } }
    return $sb.ToString().ToLowerInvariant().Replace([string][char]0x03C2, [string][char]0x03C3)
}

# saint.gr per date, and how many days each id is listed on
$sg = @{}; $count = @{}
foreach ($f in Get-ChildItem (Join-Path $PSScriptRoot 'saintgr\*.json')) {
    $items = @(Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ })
    $sg[$f.BaseName] = $items
    foreach ($i in $items) { $count[$i.id] = 1 + [int]$count[$i.id] }
}

$gen = @('αγιος','αγια','αγιοι','οσιος','οσια','οσιοι','μαρτυς','μαρτυρας','μαρτυρες','ιερομαρτυς','μεγαλομαρτυς','οσιομαρτυς','αποστολος','προφητης','επισκοπος','αρχιεπισκοπος','πατριαρχης','παπας','ο','η','οι','του','της','των','και','εν','ως','απο','μεγας','θαυματουργος','ομολογητης','ισαποστολος','συναξη','μνημη','ευρεση','μετακομιδη','νεομαρτυρας','δικαιος','παναγια')
$items = @()
foreach ($f in Get-ChildItem (Join-Path $root 'data\orthodox-el\*.json')) {
    $m = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($d in $m.days) { foreach ($c in @($d.commemorations)) { if (-not $c) { continue }
        $names = @(); foreach ($n in @($c.names)) { $names += $n.name; $names += @($n.variants) }
        $items += [pscustomobject]@{ Key = ('{0:00}-{1:00}' -f $m.month, $d.day); Title = $c.title; Names = $names } } }
}
foreach ($f in Get-ChildItem (Join-Path $root 'data\saints\saints-*.tsv')) {
    foreach ($line in Get-Content $f.FullName -Encoding UTF8) {
        if ($line.StartsWith('#') -or [string]::IsNullOrWhiteSpace($line)) { continue }
        $p = $line.Split('|'); $names = @()
        foreach ($x in $p[3].Split(';')) { $q = $x.Split('~'); $names += $q[0]; $names += @($q[1].Split(',') | Where-Object { $_ }) }
        $items += [pscustomobject]@{ Key = $p[0]; Title = $p[1]; Names = $names }
    }
}

$lines = New-Object Collections.Generic.List[string]
$lines.Add('# kind|MM-DD|year|title|status|url|citation   (made by tools\verify\Build-SaintGrLinks.ps1; do not edit by hand)')
$report = New-Object Collections.Generic.List[string]
$matched = 0
foreach ($it in $items) {
    $list = $sg[$it.Key]
    if (-not $list) { $report.Add("NO PAGE  $($it.Key) $($it.Title)"); continue }
    $myWords = @($it.Title -split '[\s,·«»()]+' | ForEach-Object { Fold $_ } | Where-Object { $_.Length -ge 4 -and $gen -notcontains $_ })
    $best = $null; $bestScore = 0
    foreach ($s in $list) {
        if ($count[$s.id] -gt 25) { continue }
        $theirs = @($s.title -split '[\s,·«»()]+' | ForEach-Object { Fold $_ } | Where-Object { $_.Length -ge 4 -and $gen -notcontains $_ })
        $score = 0
        foreach ($w in $myWords) { if ($theirs -contains $w) { $score += 2 } }
        foreach ($n in $it.Names) { $fn = Fold $n; if ($fn.Length -ge 4 -and ($theirs | Where-Object { $_ -eq $fn -or ($fn.Length -ge 6 -and $_.StartsWith($fn.Substring(0, $fn.Length - 2))) })) { $score += 1 } }
        if ($score -gt $bestScore) { $bestScore = $score; $best = $s }
    }
    if ($best -and $bestScore -ge 2) {
        $matched++
        $url = "https://www.saint.gr/$($best.id)/saint.aspx"
        $cit = 'Συναξαριστής του saint.gr: ο βίος και οι λεπτομέρειες του αγίου (για περισσότερα). Το κείμενο της εφαρμογής είναι γραμμένο με δικά μας λόγια.'
        $lines.Add("source|$($it.Key)||$($it.Title)|Unverified|$url|$cit")
    } else { $report.Add("NO LINK  $($it.Key) $($it.Title)  <>  " + (($list | Where-Object { $count[$_.id] -le 25 } | ForEach-Object { $_.title }) -join ' / ')) }
}
# saint.gr is credited only in Help (Πηγές και ευχαριστίες), not on each saint: this script only produces a report of what did not match, to check names and dates.
# Pass -WriteLinks to write the per-saint source links anyway.
if ($args -contains '-WriteLinks') { [IO.File]::WriteAllLines((Join-Path $root 'data\verification\verification-saintgr.tsv'), $lines, $utf8) }
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'report-saintgr.txt'), $report, $utf8)
Write-Host "linked $matched of $($items.Count)"
