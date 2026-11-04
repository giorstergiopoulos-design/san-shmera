<#
  Cross-checks the dates of the saints and feasts of the app against the names that Orthocal gives for the same Julian date
  (tools\verify\orthocal\MM-DD.json, made by Fetch-Orthocal.ps1).
  Greek and English names are compared by their consonant skeletons (Νικόλαος = Nicholas = NKLS), which survives the different spellings.
  A commemoration is "Reviewed" when at least one of its personal names (or a variant) matches a name of that date.
  Writes
    data\verification\verification-saints.tsv   the lines the app loads (kind|MM-DD|year|title|status|url|citation)
    tools\verify\report-saints.txt              what did not match, for a human look
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')

function Strip([string]$s) {
    $d = $s.Normalize([Text.NormalizationForm]::FormD)
    $sb = New-Object Text.StringBuilder
    foreach ($c in $d.ToCharArray()) { if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($c) -ne 'NonSpacingMark') { [void]$sb.Append($c) } }
    return $sb.ToString().ToLowerInvariant().Replace([string][char]0x03C2, [string][char]0x03C3)   # final sigma -> sigma
}
$greek = @{ 'β'='B'; 'γ'='G'; 'δ'='D'; 'ζ'='Z'; 'θ'='T'; 'κ'='K'; 'λ'='L'; 'μ'='M'; 'ν'='N'; 'ξ'='KS'; 'π'='P'; 'ρ'='R'; 'σ'='S'; 'τ'='T'; 'φ'='F'; 'χ'='K'; 'ψ'='PS' }
$latin = @{ 'b'='B'; 'c'='K'; 'd'='D'; 'f'='F'; 'g'='G'; 'k'='K'; 'l'='L'; 'm'='M'; 'n'='N'; 'p'='P'; 'q'='K'; 'r'='R'; 's'='S'; 't'='T'; 'v'='B'; 'x'='KS'; 'z'='Z' }
function Skeleton([string]$word) {
    $w = Strip $word
    $isGreek = $w -match '[α-ω]'
    if (-not $isGreek) { $w = $w -replace 'ph', 'f' -replace 'th', 't' -replace 'ch', 'k' -replace 'ps', 'ps' }
    $map = if ($isGreek) { $greek } else { $latin }
    $sb = New-Object Text.StringBuilder
    foreach ($c in $w.ToCharArray()) { $k = [string]$c; if ($map.ContainsKey($k)) { [void]$sb.Append($map[$k]) } }
    return $sb.ToString()
}
function Distance([string]$a, [string]$b) {
    $n = $a.Length; $m = $b.Length
    $d = New-Object 'int[,]' ($n + 1), ($m + 1)
    for ($i = 0; $i -le $n; $i++) { $d[$i, 0] = $i }
    for ($j = 0; $j -le $m; $j++) { $d[0, $j] = $j }
    for ($i = 1; $i -le $n; $i++) { for ($j = 1; $j -le $m; $j++) {
        $cost = if ($a[$i - 1] -eq $b[$j - 1]) { 0 } else { 1 }
        $up = $d[($i - 1), $j] + 1
        $left = $d[$i, ($j - 1)] + 1
        $diag = $d[($i - 1), ($j - 1)] + $cost
        $d[$i, $j] = [Math]::Min([Math]::Min($up, $left), $diag)
    } }
    return $d[$n, $m]
}
function Same([string]$a, [string]$b) {
    if ($a.Length -lt 3 -or $b.Length -lt 3) { return $false }
    if ($a -eq $b) { return $true }
    if ($a.Length -ge 4 -and $b.Length -ge 4) { return (Distance $a $b) -le 1 }
    return $false
}
$stop = 'st','saint','saints','holy','the','of','and','our','father','fathers','venerable','righteous','apostle','apostles','prophet','martyr','martyrs','bishop','archbishop','patriarch','great','confessor','monk','abbot','new','virgin','mother','god','lord','blessed','equal','to','in','with','at','by','a','from','hieromartyr','wonderworker'

# orthocal names per date -> list of skeletons
$ortho = @{}
foreach ($f in Get-ChildItem (Join-Path $PSScriptRoot 'orthocal\*.json')) {
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $sk = @()
    foreach ($n in @($j.names)) { foreach ($w in ($n -split '[^A-Za-zÀ-ÿ]+')) { if ($w.Length -ge 3 -and $stop -notcontains $w.ToLowerInvariant()) { $s = Skeleton $w; if ($s.Length -ge 3) { $sk += $s } } } }
    $ortho[$f.BaseName] = [pscustomobject]@{ Url = $j.url; Names = @($j.names); Skeletons = @($sk | Select-Object -Unique) }
}

# the app's commemorations: (date, title, names+variants)
$items = @()
foreach ($f in Get-ChildItem (Join-Path $root 'data\orthodox-el\*.json')) {
    $m = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($d in $m.days) { foreach ($c in @($d.commemorations)) {
        if (-not $c) { continue }
        $names = @(); foreach ($n in @($c.names)) { $names += $n.name; $names += @($n.variants) }
        $items += [pscustomobject]@{ Key = ('{0:00}-{1:00}' -f $m.month, $d.day); Title = $c.title; Names = $names }
    } }
}
foreach ($f in Get-ChildItem (Join-Path $root 'data\saints\saints-*.tsv')) {
    foreach ($line in Get-Content $f.FullName -Encoding UTF8) {
        if ($line.StartsWith('#') -or [string]::IsNullOrWhiteSpace($line)) { continue }
        $p = $line.Split('|'); $names = @()
        foreach ($x in $p[3].Split(';')) { $q = $x.Split('~'); $names += $q[0]; $names += @($q[1].Split(',') | Where-Object { $_ }) }
        $items += [pscustomobject]@{ Key = $p[0]; Title = $p[1]; Names = $names }
    }
}

# matches checked by eye: "MM-DD|start of the title"
$manual = @()
$manualFile = Join-Path $PSScriptRoot 'manual-matches.txt'
if (Test-Path $manualFile) { foreach ($l in Get-Content $manualFile -Encoding UTF8) { if (-not $l.StartsWith('#') -and $l.Contains('|')) { $x = $l.Split('|'); $manual += [pscustomobject]@{ Key = $x[0]; Start = $x[1] } } } }

$lines = New-Object Collections.Generic.List[string]
$lines.Add('# kind|MM-DD|year|title|status|url|citation   (made by tools\verify\Build-Crosscheck.ps1; do not edit by hand)')
$report = New-Object Collections.Generic.List[string]
$ok = 0; $bad = 0; $nodata = 0
foreach ($it in $items) {
    $o = $ortho[$it.Key]
    if (-not $o) { $nodata++; $report.Add("NO DATA  $($it.Key) $($it.Title)"); continue }
    $hit = $null
    foreach ($n in $it.Names) { $s = Skeleton $n; foreach ($os in $o.Skeletons) { if (Same $s $os) { $hit = $n; break } }; if ($hit) { break } }
    if ($hit) {
        $ok++
        $cit = "Διασταύρωση ημερομηνίας με το ανοιχτό εορτολόγιο Orthocal (βασίζεται στο εορτολόγιο της OCA): το όνομα «$hit» εμφανίζεται την ίδια ημέρα. Έγινε σύγκριση ονομάτων, όχι αντιγραφή κειμένου."
        $lines.Add("saint|$($it.Key)||$($it.Title)|Reviewed|$($o.Url)|$cit")
    } elseif ($manual | Where-Object { $_.Key -eq $it.Key -and $it.Title.StartsWith($_.Start) }) {
        $ok++
        $cit = 'Διασταύρωση ημερομηνίας με το ανοιχτό εορτολόγιο Orthocal (βασίζεται στο εορτολόγιο της OCA): η εορτή αναφέρεται την ίδια ημέρα. Έγινε έλεγχος ονομάτων από τον συντάκτη, όχι αντιγραφή κειμένου.'
        $lines.Add("saint|$($it.Key)||$($it.Title)|Reviewed|$($o.Url)|$cit")
    } else {
        $bad++
        $report.Add("NO MATCH $($it.Key) $($it.Title)  <>  " + ($o.Names -join ' / '))
    }
}
New-Item -ItemType Directory -Force (Join-Path $root 'data\verification') | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllLines((Join-Path $root 'data\verification\verification-saints.tsv'), $lines, $utf8)
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'report-saints.txt'), $report, $utf8)
Write-Host "matched $ok, not matched $bad, no data $nodata"
