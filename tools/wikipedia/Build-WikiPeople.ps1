<#
  Makes data\births\births-wiki.tsv and data\deaths\deaths-wiki.tsv from the day pages read by Get-WikiDays.ps1 (-Wiki): for every day the best known people of the
  Greek Wikipedia lists "Γεννήσεις" and "Θάνατοι" (best known = the most language editions of the article, at least -MinScore, at most -PerDay), without the people the database already has.
  Only facts are taken (name, year, a short description of who the person was); nothing else of the pages. The source is named in Help (Πηγές) and in the file header.
  Format: MM-DD|year|region|name|description   (region "Ελλάδα" for a person of the Greek world, else "Κόσμος")
  Usage: powershell -File Build-WikiPeople.ps1 -Wiki <wiki.tsv> [-PerDay 4] [-MinScore 25]
#>
param([Parameter(Mandatory)] [string]$Wiki, [int]$PerDay = 4, [int]$MinScore = 25)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$utf8 = New-Object System.Text.UTF8Encoding($true)
function Fold([string]$x) { (($x.Normalize([Text.NormalizationForm]::FormD) -replace '\p{Mn}', '').ToLower() -replace 'ς', 'σ' -replace '[^\p{L}\p{Nd}]+', ' ').Trim() }

$have = @{}
foreach ($f in Get-ChildItem (Join-Path $root 'data\births\births*.tsv'), (Join-Path $root 'data\deaths\deaths*.tsv') -ErrorAction SilentlyContinue) {
    if ($f.Name -like '*-wiki.tsv') { continue }
    foreach ($l in Get-Content $f.FullName -Encoding UTF8) { if ($l -match '^(\d\d-\d\d)\|(-?\d+)\|[^|]*\|([^|]+)\|') { $have["$($f.Name -like 'deaths*')|$($Matches[1])|$(Fold $Matches[3])"] = 1; $have["$($f.Name -like 'deaths*')|$($Matches[1])|y$($Matches[2])"] = 1 } }
}
$greek = 'Έλληνας|Ελληνίδα|Ελλήνων|ελληνικ|Κύπριος|Κύπρια|κυπριακ|Βυζαντιν|Πόντιος|Μικρασιάτης|Κρητικός|Αθηναίος|Αρχαίος Έλληνας|Μακεδόνας|Σπαρτιάτης|Θεσσαλονικιός|Καισαρείας|Κωνσταντινουπόλεως|Πατριάρχης|Ορθόδοξ'
$rows = @{ 'False' = New-Object Collections.Generic.List[object]; 'True' = New-Object Collections.Generic.List[object] }
foreach ($l in Get-Content $Wiki -Encoding UTF8) {
    $p = $l -split '\|', 6
    if ($p.Count -lt 6 -or $p[1] -notin 'Γεννήσεις', 'Θάνατοι') { continue }
    $score = 0; [void][int]::TryParse($p[3], [ref]$score)
    if ($score -lt $MinScore) { continue }
    $year = 0; if (-not [int]::TryParse($p[2], [ref]$year)) { continue }
    $text = $p[5].Trim()
    if ($text -notmatch '^([^,]{3,60}),\s*(.{4,160})$') { continue }
    $name = $Matches[1].Trim(); $desc = $Matches[2].Trim()
    if ($name -match ' και |\(|\[' ) { continue }
    $desc = $desc.Substring(0, 1).ToUpper() + $desc.Substring(1)
    $deaths = $p[1] -eq 'Θάνατοι'
    if ($have.ContainsKey("$deaths|$($p[0])|$(Fold $name)") -or $have.ContainsKey("$deaths|$($p[0])|y$year")) { continue }   # the same person under another spelling has the same day and year
    $region = if ($desc -match $greek -or $name -match $greek) { 'Ελλάδα' } else { 'Κόσμος' }
    $rows["$deaths"].Add([pscustomobject]@{ Day = $p[0]; Year = $year; Region = $region; Name = $name; Desc = $desc; Score = $score })
}
foreach ($deaths in $false, $true) {
    $lines = New-Object Collections.Generic.List[string]
    $kind = if ($deaths) { 'Πέθαναν' } else { 'Γεννήθηκαν' }
    $lines.Add("# $kind σαν σήμερα. MM-DD|year|region|name|description   (the best known people of the day: name, year and who they were; from the Greek Wikipedia lists of the day pages, facts only)")
    $lines.Add('# Written by tools\wikipedia\Build-WikiPeople.ps1. Dates are those of the Gregorian calendar. Source: el.wikipedia.org (named in Help).')
    foreach ($g in ($rows["$deaths"] | Group-Object Day | Sort-Object Name)) {
        $seen = @{}
        foreach ($r in ($g.Group | Sort-Object { -$_.Score }, Year)) {
            $k = Fold $r.Name; if ($seen.ContainsKey($k)) { continue }; $seen[$k] = 1
            if ($seen.Count -gt $PerDay) { break }
            $lines.Add("$($r.Day)|$($r.Year)|$($r.Region)|$($r.Name)|$($r.Desc)".Replace('|', '|'))
        }
    }
    $dir = Join-Path $root $(if ($deaths) { 'data\deaths' } else { 'data\births' })
    New-Item -ItemType Directory -Force $dir | Out-Null
    $file = Join-Path $dir $(if ($deaths) { 'deaths-wiki.tsv' } else { 'births-wiki.tsv' })
    [IO.File]::WriteAllLines($file, $lines, $utf8)
    Write-Host "$(Split-Path $file -Leaf): $($lines.Count - 2) people"
}
