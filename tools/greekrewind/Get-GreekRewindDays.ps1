<#
  Reads the "Ιστορικό Ημερολόγιο" of https://www.greekrewind.gr (robots.txt allows it and asks for a crawl delay of 10 seconds, which is kept): for every day the events,
  the births, the deaths, the religious feasts and the anniversaries it lists. The result is a reading aid kept OUTSIDE the repository (-Out): the texts of the site are not copied into the app;
  they are read to find Greek events that the database lacks, which are then written in our own words, and the site is named as a source in Help (Πηγές).
  Output: MM-DD|section|year|text
  Usage: powershell -File Get-GreekRewindDays.ps1 -Out <file>
#>
param([Parameter(Mandatory)] [string]$Out)
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-DataTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
Add-Type -AssemblyName System.Web
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Get-Page([string]$url) {
    for ($try = 1; $try -le 4; $try++) {
        try { $r = (Invoke-WebRequest -UserAgent $ua -Uri $url -UseBasicParsing -TimeoutSec 60).Content; Start-Sleep -Seconds 10; return $r }
        catch { if ($_.Exception.Message -match '429|503' -and $try -lt 4) { Start-Sleep -Seconds (60 * $try) } else { Write-Host "FAILED $url $($_.Exception.Message)"; Start-Sleep -Seconds 10; return $null } }
    }
}
function Clean([string]$s) { ([System.Web.HttpUtility]::HtmlDecode([regex]::Replace($s, '<[^>]+>', ' ')) -replace '\s+', ' ').Trim() }
$months = 'ianoyarios', 'fevroyarios', 'martios', 'aprilios', 'maios', 'ioynios', 'ioylios', 'aygoystos', 'septemvrios', 'oktovrios', 'noemvrios', 'dekemvrios'
$lines = New-Object Collections.Generic.List[string]
if (Test-Path $Out) { foreach ($l in Get-Content $Out -Encoding UTF8) { $lines.Add($l) } }
$have = @{}; foreach ($l in $lines) { $have[$l.Substring(0, 5)] = 1 }
for ($m = 1; $m -le 12; $m++) {
    $page = Get-Page "https://www.greekrewind.gr/$($months[$m - 1])/"
    if (-not $page) { continue }
    $links = [regex]::Matches($page, 'href="(/\d+-[a-z]+/)">(\d+)') | ForEach-Object { [pscustomobject]@{ Url = $_.Groups[1].Value; Day = [int]$_.Groups[2].Value } } | Sort-Object Day -Unique
    foreach ($d in $links) {
        $key = '{0:00}-{1:00}' -f $m, $d.Day
        if ($have.ContainsKey($key)) { continue }
        $html = Get-Page "https://www.greekrewind.gr$($d.Url)"
        if (-not $html) { continue }
        # the text of the day: from the date heading to the copyright line
        $text = [regex]::Replace($html, '(?s)<script.*?</script>|<style.*?</style>', '')
        $block = if ($text -match '(?s)(\d+ [Α-ΩΆ-Ώ]+ΟΥ</.*?)©') { $Matches[1] } else { $text }
        # the structure: a heading (Γεγονότα, Γεννήσεις ...), then for every item a line with the year and a line with the text
        $flat = [regex]::Replace($block, '</(?:h\d|p|div|li|br)>|<br\s*/?>', "`n")
        $rows = @($flat -split "`n" | ForEach-Object { ([System.Web.HttpUtility]::HtmlDecode([regex]::Replace($_, '<[^>]+>', ' ')) -replace '\s+', ' ').Trim() } | Where-Object { $_ })
        $section = ''; $year = ''; $n = 0
        foreach ($row in $rows) {
            if ($row -match '^(Θρησκευτικές Εορτές|Επέτειοι|Γεγονότα|Γεννήσεις|Θάνατοι)\s*(.*)$') {
                $section = $Matches[1]; $year = ''
                if ($Matches[2]) { $lines.Add("$key|$section||$($Matches[2])"); $n++ }
                continue
            }
            if (-not $section) { continue }
            if ($section -in 'Θρησκευτικές Εορτές', 'Επέτειοι') { $lines.Add("$key|$section||$row"); $n++; continue }
            if ($row -match '^(\d{3,4})$') { $year = $Matches[1]; continue }
            if ($row -match '^(\d{3,4})\s+(.+)$') { $lines.Add("$key|$section|$($Matches[1])|$($Matches[2])"); $n++; continue }
            if ($year) { $lines.Add("$key|$section|$year|$row"); $n++ }
        }        if ($n -eq 0) { $lines.Add("$key|EMPTY||") }
        Write-Host "$key $n"
        [IO.File]::WriteAllLines($Out, $lines, $utf8)
    }
}
