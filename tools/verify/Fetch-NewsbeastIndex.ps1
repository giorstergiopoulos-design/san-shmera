<#
  Reads the listing pages of the "Σαν σήμερα" tag of newsbeast.gr (/tag/san-simera/page/N) and builds an INDEX of the daily articles:
  the day and month from the address (…-san-simera-6-dekemvriou), the section (greece or world), the id, the address and the title.
  The articles themselves are not copied. The index is a work list: it shows which events a newspaper column covers on each date,
  to cross-check the dates of the app's events and to find events that are missing; the app's texts are written in our own words.
  One request per 1.5 seconds, honest user agent. Re-running continues where it stopped.
  Output: tools\verify\newsbeast-index.tsv   MM-DD|section|id|url|title
#>
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-VerifyTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$out = Join-Path $PSScriptRoot 'newsbeast-index.tsv'
$state = Join-Path $PSScriptRoot 'newsbeast-index.state'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$months = @{ ianouariou = 1; fevrouariou = 2; martiou = 3; apriliou = 4; maiou = 5; iouniou = 6; iouliou = 7; avgoustou = 8; septemvriou = 9; oktovriou = 10; noemvriou = 11; dekemvriou = 12 }
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
if (Test-Path $out) { foreach ($l in Get-Content $out -Encoding UTF8) { $p = $l.Split('|'); if ($p.Count -ge 3) { [void]$seen.Add($p[2]) } } } else { [IO.File]::WriteAllText($out, "# MM-DD|section|id|url|title`n", $utf8) }
$start = 1
if (Test-Path $state) { $start = [int](Get-Content $state) + 1 }
$last = 242
for ($page = $start; $page -le $last; $page++) {
    $url = if ($page -eq 1) { 'https://www.newsbeast.gr/tag/san-simera' } else { "https://www.newsbeast.gr/tag/san-simera/page/$page" }
    try {
        $r = Invoke-WebRequest -UserAgent $ua -Uri $url -UseBasicParsing
        $rows = New-Object Collections.Generic.List[string]
        foreach ($x in [regex]::Matches($r.Content, '<a[^>]+href="(?<h>(?:https://www\.newsbeast\.gr)?/(?<s>[a-z\-]+)/arthro/(?<id>\d+)/(?<slug>[^"]*?san-simera[^"]*))"[^>]*>(?<t>.*?)</a>', 'Singleline')) {
            $id = $x.Groups['id'].Value
            if ($seen.Contains($id)) { continue }
            $slug = $x.Groups['slug'].Value
            $m = [regex]::Match($slug, 'san-simera-(\d{1,2})-([a-z]+)')
            if (-not $m.Success -or -not $months.ContainsKey($m.Groups[2].Value)) { continue }
            $title = ([regex]::Replace($x.Groups['t'].Value, '<[^>]+>', '') -replace '&nbsp;', ' ' -replace '\|', '/' -replace '\s+', ' ').Trim()
            if ($title.Length -lt 8) { continue }
            [void]$seen.Add($id)
            $h = $x.Groups['h'].Value; if (-not $h.StartsWith('http')) { $h = 'https://www.newsbeast.gr' + $h }
            $rows.Add(('{0:00}-{1:00}|{2}|{3}|{4}|{5}' -f $months[$m.Groups[2].Value], [int]$m.Groups[1].Value, $x.Groups['s'].Value, $id, $h, $title))
        }
        if ($rows.Count -gt 0) { [IO.File]::AppendAllLines($out, $rows, $utf8) }
        Set-Content -Path $state -Value $page
        Write-Host "page ${page}: $($rows.Count) new"
    } catch {
        Write-Host "page ${page}: FAILED $($_.Exception.Message)"
        if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 30; $page-- }
    }
    Start-Sleep -Milliseconds 1500
}
