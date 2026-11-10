<#
  Makes data\saints\saints-syn-MM.tsv from the day lists read by Get-SynaxarionDays.ps1 (-Days) and the names the database already has (-Names, written by the test
  DumpNames: CHRON_NAMES=<file>). For every saint of the Megas Synaxaristis of a day whose first name does not celebrate that day in the database yet, a row is written:
    MM-DD|title|summary|names|sid
  The title is the heading of the saint put into the modern Greek spelling (one accent, no breathings); the summary is a plain statement that the memory exists and that the life
  is not written yet; the names are the first name of the saint with the etymology the database already has for that name (a name the database does not know gets none);
  sid is the number of the page of the saint on synaxarion.gr, shown as the source. No text of the site is copied: the lives are written afterwards, in our own words, in data\lives.
  Usage: powershell -File Build-SynaxarionSaints.ps1 -Days <days.tsv> -Names <ournames.txt> [-Out <data\saints folder>]
#>
param([Parameter(Mandatory)] [string]$Days, [Parameter(Mandatory)] [string]$Names, [string]$Out = (Join-Path $PSScriptRoot '..\..\data\saints'))
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($true)

function Fold([string]$x) { (($x.Normalize([Text.NormalizationForm]::FormD) -replace '\p{Mn}', '').ToLower() -replace 'ς', 'σ') }

# polytonic -> monotonic: grave, circumflex and acute become one acute; breathings and the iota subscript go; accents of the little words go too
function Mono([string]$s) {
    $sb = New-Object Text.StringBuilder
    foreach ($c in $s.Normalize([Text.NormalizationForm]::FormD).ToCharArray()) {
        $code = [int]$c
        if ($code -in 0x0300, 0x0301, 0x0342) { [void]$sb.Append([char]0x0301) }
        elseif ($code -in 0x0313, 0x0314, 0x0345, 0x0343, 0x0344) { }
        else { [void]$sb.Append($c) }
    }
    $t = $sb.ToString().Normalize([Text.NormalizationForm]::FormC)
    $little = @{ 'τής' = 'της'; 'τών' = 'των'; 'τού' = 'του'; 'τόν' = 'τον'; 'τήν' = 'την'; 'τό' = 'το'; 'τά' = 'τα'; 'τούς' = 'τους'; 'τοίς' = 'τοις'; 'τώ' = 'τω'; 'καί' = 'και'; 'είς' = 'εις'; 'ώς' = 'ως'; 'σύν' = 'συν'; 'πρός' = 'προς'; 'έν' = 'εν'; 'έκ' = 'εκ'; 'έξ' = 'εξ' }
    return [regex]::Replace($t, '(?<![\p{L}])[\p{L}]{1,4}(?![\p{L}])', { param($m) if ($little.ContainsKey($m.Value)) { $little[$m.Value] } else { $m.Value } })
}

# our names by folded name: the part "Name~variants~origin~meaning" of the saints files, and the days each name has
$known = @{}; $ourDayName = @{}; $ourTitles = @{}
foreach ($l in Get-Content $Names -Encoding UTF8) {
    $p = $l -split '\|'
    if ($p[0] -eq 'title') { if (-not $ourTitles.ContainsKey($p[1])) { $ourTitles[$p[1]] = New-Object Collections.Generic.List[string] }; $ourTitles[$p[1]].Add((Fold $p[2])); continue }
    if ($p.Count -lt 5) { continue }
    $f = Fold $p[1]
    if (-not $known.ContainsKey($f)) { $known[$f] = "$($p[1])~$($p[2])~$($p[3])~$($p[4])" }
    $ourDayName["$($p[0])|$f"] = 1
    foreach ($v in ($p[2] -split ',')) { if ($v) { $ourDayName["$($p[0])|$(Fold $v)"] = 1 } }
}
$skipWords = 'συναξισ|μνημη|ανακομιδη|αγιεσ|εγκαινια|ευρεσισ|μετακομιδη|δυο|τρεισ|οι|ο|η|των|τησ|του|της|θαυμα|προεορτια|αποδοση|εορτη|ιερα|ημερα|ετερα|ετεροσ|αλλοι|αλλεσ'
$roles = '^(Ὁ|Ἡ|Οἱ|Τὸ)\s+'
$titles = '^(Ἅγιος|Ἁγία|Ἅγιοι|Ὅσιος|Ὁσία|Ὅσιοι|Μάρτυρας|Ἱερομάρτυρας|Μεγαλομάρτυρας|Νεομάρτυρας|Ἀπόστολος|Προφήτης|Πατριάρχης|Ἀρχιεπίσκοπος|Δίκαιος|Ὁσιομάρτυρας|Ἅγιες|Ἁγίες|Ὅσιες)\s+'

$perMonth = @{}
$seen = @{}
foreach ($l in Get-Content $Days -Encoding UTF8) {
    if ($l -notmatch '^(\d\d)-(\d\d)\|(\d+)\|(.*)$') { continue }
    $day = "$($Matches[1])-$($Matches[2])"; $sid = $Matches[3]; $heading = ($Matches[4] -split '\|', 2)[0]
    $t = $heading -replace $roles, ''; $t = $t -replace $titles, ''
    $words = @($t -split '[\s,\(]+' | Where-Object { $_ })
    if ($words.Count -eq 0) { continue }
    $first = $words[0] -replace '[«»]', ''
    if ($first.Length -lt 3 -or (Fold $first) -match "^($skipWords)$") { continue }
    $f = Fold $first
    $secondName = ''
    if ($words.Count -ge 3 -and (Fold $words[1]) -eq 'και' -and $words[2][0] -cmatch '\p{Lu}') { $secondName = $words[2] -replace '[«»]', '' }
    # the day already celebrates this name (a saint of the database): nothing to add
    if ($ourDayName.ContainsKey("$day|$f")) { continue }
    $key = "$day|$sid"; if ($seen.ContainsKey($key)) { continue }; $seen[$key] = 1
    # a saint the database has already under another heading: the same first name and another long word in common with a title of that day
    $dup = $false
    if ($ourTitles.ContainsKey($day)) { foreach ($ot in $ourTitles[$day]) { if ($ot -match [regex]::Escape($f)) { $mine = @($words | ForEach-Object { Fold $_ } | Where-Object { $_.Length -ge 5 -and $_ -ne $f }); foreach ($w in $mine) { if ($ot -match [regex]::Escape($w)) { $dup = $true } } } } }
    if ($dup) { continue }

    $title = Mono ($heading -replace $roles, '')
    $title = $title.Substring(0, 1).ToUpper() + $title.Substring(1)
    $tkey = "$day|$(Fold $title)"; if ($seen.ContainsKey($tkey)) { continue }; $seen[$tkey] = 1
    if ($title.Length -gt 160) { $title = $title.Substring(0, 160).TrimEnd() }
    $nameParts = New-Object Collections.Generic.List[string]
    if ($known.ContainsKey($f)) { $nameParts.Add($known[$f]) }
    if ($secondName) { $f2 = Fold $secondName; if ($known.ContainsKey($f2) -and -not $ourDayName.ContainsKey("$day|$f2")) { $nameParts.Add($known[$f2]) } }
    $summary = 'Η μνήμη αναφέρεται στον Μέγα Συναξαριστή για αυτή την ημέρα. Ο βίος δεν έχει ακόμη γραφτεί στη βάση.'
    $row = "$day|$title|$summary|$($nameParts -join ';')|$sid" -replace "`r", ' '
    $m = $day.Substring(0, 2)
    if (-not $perMonth.ContainsKey($m)) { $perMonth[$m] = New-Object Collections.Generic.List[string] }
    $perMonth[$m].Add($row)
}
$total = 0
foreach ($m in $perMonth.Keys | Sort-Object) {
    $lines = New-Object Collections.Generic.List[string]
    $lines.Add('# MM-DD|title|summary|names|sid   names = Name~variants~origin~meaning separated by ;   sid = the page of the saint on synaxarion.gr (the source)')
    $lines.Add('# Made by tools\synaxarion\Build-SynaxarionSaints.ps1 from the day lists of the Megas Synaxaristis (heading and number of the page only). The lives are written in data\lives, in our own words.')
    foreach ($r in ($perMonth[$m] | Sort-Object)) { $lines.Add($r) }
    [IO.File]::WriteAllLines((Join-Path $Out "saints-syn-$m.tsv"), $lines, $utf8)
    $total += $perMonth[$m].Count
}
Write-Host "rows: $total"
