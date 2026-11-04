<#
  For the days that have no Greek event yet, takes one Greek article of the newsbeast index per day and reads the opening of the article to learn WHAT happened and WHEN
  (the years named in it). Only a short snippet is kept in tools\verify\newsbeast-picks.tsv as WORKING NOTES: the events of the app are written in our own words
  and their year is cross-checked with a second source before they ship.
  One request per 1.5 seconds, honest user agent.
  Usage: powershell -File Fetch-NewsbeastPicks.ps1 -Days "01-01,01-02,..."   (days without a Greek event)
#>
param([Parameter(Mandatory)] [string]$Days)
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-VerifyTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$out = Join-Path $PSScriptRoot 'newsbeast-picks2.tsv'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$want = $Days.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$idx = Get-Content (Join-Path $PSScriptRoot 'newsbeast-index.tsv') -Encoding UTF8 | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { $p = $_.Split('|', 5); [pscustomobject]@{ Key = $p[0]; Section = $p[1]; Url = $p[3]; Title = $p[4] } }
$done = @{}
if (Test-Path $out) { foreach ($l in Get-Content $out -Encoding UTF8) { $done[$l.Split('|')[0]] = $true } } else { [IO.File]::WriteAllText($out, '', $utf8) }
foreach ($key in $want) {
    if ($done.ContainsKey($key)) { continue }
    $a = $idx | Where-Object { $_.Key -eq $key -and $_.Section -eq 'greece' } | Select-Object -First 1
    if (-not $a) { continue }
    try {
        $r = Invoke-WebRequest -UserAgent $ua -Uri $a.Url -UseBasicParsing
        $text = [regex]::Replace($r.Content, '(?is)<(script|style|nav|header|footer)[^>]*>.*?</\1>', ' ')
        $paras = @([regex]::Matches($text, '(?is)<p[^>]*>(.*?)</p>') | ForEach-Object { ([regex]::Replace($_.Groups[1].Value, '<[^>]+>', '') -replace '&nbsp;', ' ' -replace '&quot;', '"' -replace '&amp;', '&' -replace '\s+', ' ').Trim() } | Where-Object { $_.Length -ge 120 -and $_ -notmatch 'ΡΟΗ ΕΙΔΗΣΕΩΝ|Newsroom|σχόλια' })
        $snippet = (($paras | Select-Object -First 3) -join ' ')
        if ($snippet.Length -gt 900) { $snippet = $snippet.Substring(0, 900) }
        $years = ([regex]::Matches($snippet + ' ' + $a.Title, '\b(1[5-9]\d\d|20[0-2]\d)\b') | ForEach-Object { $_.Value } | Select-Object -Unique | Select-Object -First 4) -join ','
        [IO.File]::AppendAllText($out, ("{0}|{1}|{2}|{3}|{4}`n" -f $key, $a.Url, ($a.Title -replace '\|', '/'), $years, ($snippet -replace '\|', '/')), $utf8)
        Write-Host "$key ok ($years)"
    } catch { Write-Host "$key FAILED $($_.Exception.Message)"; if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 30 } }
    Start-Sleep -Milliseconds 1500
}
